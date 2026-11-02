//! Installing / checking MelonLoader, the server client mod and the server's extra mods in the game folder.

use std::collections::BTreeMap;
use std::fs;
use std::io::{Cursor, Read};
use std::path::{Path, PathBuf};
use std::time::Duration;

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::manifest::{ExtraMod, Manifest};

const MARKER: &str = "UserData/HylandLink.json";
const MAX_DOWNLOAD: u64 = 256 * 1024 * 1024;

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Component {
    pub installed: bool,
    pub version: Option<String>,
    pub up_to_date: bool,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct InstallState {
    pub melonloader: Component,
    pub server_mod: Component,
    /// The server's extra mods this PC needs, in manifest order.
    pub mods: Vec<ModStatus>,
    /// Mods this launcher installed that the server dropped; the next install removes them.
    pub stale_mods: Vec<StaleMod>,
    /// MelonLoader finished its slow first IL2CPP setup (it writes Il2CppAssemblies on the first launch).
    pub first_launch_done: bool,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ModStatus {
    pub id: String,
    pub name: String,
    pub blurb: String,
    /// The version the server runs.
    pub version: String,
    pub installed: bool,
    /// The version on this PC, when known.
    pub installed_version: Option<String>,
    pub up_to_date: bool,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StaleMod {
    pub id: String,
    pub name: String,
    pub version: String,
}

/// What this launcher installed last time, kept next to the mod's own data.
#[derive(Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Marker {
    melonloader: Option<String>,
    server_mod: Option<String>,
    /// Extra mods by id, with every file written, so removal never touches anything else.
    #[serde(default)]
    mods: BTreeMap<String, InstalledMod>,
}

#[derive(Clone, Default, Serialize, Deserialize)]
struct InstalledMod {
    name: String,
    version: String,
    files: Vec<String>,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Progress<'a> {
    pub step: &'a str,
    pub label: &'a str,
    /// 1-based position among this step's downloads (extra mods come several to a step).
    pub item: usize,
    pub items: usize,
    pub done: u64,
    pub total: u64,
}

fn read_marker(game: &Path) -> Marker {
    fs::read_to_string(game.join(MARKER)).ok().and_then(|t| serde_json::from_str(&t).ok()).unwrap_or_default()
}

fn sha256_hex(bytes: &[u8]) -> String {
    Sha256::digest(bytes).iter().map(|b| format!("{b:02x}")).collect()
}

fn game_file(game: &Path, relative: &str) -> PathBuf {
    relative.split('/').fold(game.to_path_buf(), |path, part| path.join(part))
}

fn file_matches(path: &Path, sha256: &str) -> bool {
    fs::read(path).map(|b| sha256_hex(&b) == sha256).unwrap_or(false)
}

fn write_game_file(game: &Path, relative: &str, data: &[u8]) -> Result<(), String> {
    let target = game_file(game, relative);
    fs::create_dir_all(target.parent().expect("game file has a parent")).map_err(|e| e.to_string())?;
    fs::write(&target, data).map_err(|e| format!("Couldn't write {relative}: {e}"))
}

fn remove_game_file(game: &Path, relative: &str) -> Result<(), String> {
    let path = game_file(game, relative);
    if path.is_file() {
        fs::remove_file(&path).map_err(|e| format!("Couldn't remove {relative}: {e}"))?;
    }
    Ok(())
}

fn mod_status(game: &Path, m: &ExtraMod, marker: &Marker) -> ModStatus {
    let previous = marker.mods.get(&m.id);
    let exists = |relative: &String| game_file(game, relative).is_file();
    let installed = m.files.iter().any(|f| exists(&f.path)) || previous.is_some_and(|p| p.files.iter().any(exists));
    // A file an older version shipped that this one doesn't still has to go.
    let leftovers =
        previous.is_some_and(|p| p.files.iter().any(|old| m.files.iter().all(|f| &f.path != old) && exists(old)));
    let up_to_date = !leftovers && m.files.iter().all(|f| file_matches(&game_file(game, &f.path), &f.sha256));
    ModStatus {
        id: m.id.clone(),
        name: m.name.clone(),
        blurb: m.blurb.clone(),
        version: m.version.clone(),
        installed,
        installed_version: if up_to_date { Some(m.version.clone()) } else { previous.map(|p| p.version.clone()) },
        up_to_date,
    }
}

fn is_stale(manifest: &Manifest, id: &str) -> bool {
    !manifest.client_mods().any(|m| m.id == id)
}

fn stale_mods(game: &Path, manifest: &Manifest, marker: &Marker) -> Vec<StaleMod> {
    marker
        .mods
        .iter()
        .filter(|(id, m)| is_stale(manifest, id) && m.files.iter().any(|f| game_file(game, f).is_file()))
        .map(|(id, m)| StaleMod { id: id.clone(), name: m.name.clone(), version: m.version.clone() })
        .collect()
}

/// MelonLoader writes "MelonLoader v0.7.2 Open-Beta" near the top of Latest.log on every launch.
fn melonloader_version_from_log(game: &Path) -> Option<String> {
    let log = fs::read_to_string(game.join("MelonLoader").join("Latest.log")).ok()?;
    log.lines().take(40).find_map(|line| {
        let start = line.find("MelonLoader v")? + "MelonLoader v".len();
        let version: String = line[start..].chars().take_while(|c| c.is_ascii_digit() || *c == '.').collect();
        (!version.is_empty()).then_some(version)
    })
}

pub fn state(game: &Path, manifest: &Manifest) -> InstallState {
    let marker = read_marker(game);

    let ml_installed = game.join("version.dll").is_file() && game.join("MelonLoader").is_dir();
    let ml_version = if ml_installed { marker.melonloader.clone().or_else(|| melonloader_version_from_log(game)) } else { None };
    let ml_current = ml_version.as_deref() == Some(manifest.melonloader.version.as_str());

    let dll = game_file(game, &manifest.mod_package.dll);
    let mod_installed = dll.is_file();
    let mod_current = mod_installed
        && fs::read(&dll).map(|b| sha256_hex(&b) == manifest.mod_package.dll_sha256).unwrap_or(false);
    let mod_version = match (mod_installed, mod_current) {
        (true, true) => Some(manifest.mod_package.version.clone()),
        (true, false) => marker.server_mod.clone(),
        _ => None,
    };

    let assemblies = game.join("MelonLoader").join("Il2CppAssemblies");
    let first_launch_done = fs::read_dir(&assemblies).map(|mut d| d.next().is_some()).unwrap_or(false);

    InstallState {
        melonloader: Component { installed: ml_installed, version: ml_version, up_to_date: ml_installed && ml_current },
        server_mod: Component { installed: mod_installed, version: mod_version, up_to_date: mod_current },
        mods: manifest.client_mods().map(|m| mod_status(game, m, &marker)).collect(),
        stale_mods: stale_mods(game, manifest, &marker),
        first_launch_done,
    }
}

fn download(
    on_progress: &dyn Fn(Progress),
    step: &str,
    label: &str,
    (item, items): (usize, usize),
    url: &str,
    expected_sha256: &str,
) -> Result<Vec<u8>, String> {
    let emit = |done, total| on_progress(Progress { step, label, item, items, done, total });
    emit(0, 0);
    // Connecting and the first response byte must be quick; the body may take a while on a slow connection.
    let mut response = ureq::get(url)
        .config()
        .timeout_connect(Some(Duration::from_secs(15)))
        .timeout_recv_response(Some(Duration::from_secs(30)))
        .timeout_global(Some(Duration::from_secs(15 * 60)))
        .build()
        .call()
        .map_err(|e| format!("Couldn't download {label} ({e}). Check your internet and try again."))?;
    let total = response
        .headers()
        .get("content-length")
        .and_then(|v| v.to_str().ok())
        .and_then(|v| v.parse::<u64>().ok())
        .unwrap_or(0);

    let mut reader = response.body_mut().with_config().limit(MAX_DOWNLOAD).reader();
    let mut bytes = Vec::with_capacity(total.min(MAX_DOWNLOAD) as usize);
    let mut chunk = [0u8; 64 * 1024];
    loop {
        let n = reader.read(&mut chunk).map_err(|e| format!("The {label} download was interrupted ({e})."))?;
        if n == 0 {
            break;
        }
        bytes.extend_from_slice(&chunk[..n]);
        emit(bytes.len() as u64, total);
    }

    let actual = sha256_hex(&bytes);
    if actual != expected_sha256 {
        return Err(format!(
            "{label} didn't match its checksum, so I didn't install it. Tell Blazzer (got {}…).",
            &actual[..12]
        ));
    }
    Ok(bytes)
}

/// Extracts every file of the archive into `dest`, refusing entries that would escape it.
fn extract_all(bytes: &[u8], dest: &Path) -> Result<(), String> {
    let mut archive = zip::ZipArchive::new(Cursor::new(bytes)).map_err(|e| format!("Bad MelonLoader archive: {e}"))?;
    for i in 0..archive.len() {
        let mut entry = archive.by_index(i).map_err(|e| e.to_string())?;
        let relative = entry.enclosed_name().ok_or_else(|| format!("Unsafe path in archive: {}", entry.name()))?;
        let target = dest.join(relative);
        if entry.is_dir() {
            fs::create_dir_all(&target).map_err(|e| e.to_string())?;
            continue;
        }
        if let Some(parent) = target.parent() {
            fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
        let mut out = Vec::with_capacity(entry.size() as usize);
        entry.read_to_end(&mut out).map_err(|e| e.to_string())?;
        fs::write(&target, out).map_err(|e| format!("Couldn't write {}: {e}", target.display()))?;
    }
    Ok(())
}

/// Reads the named entries out of a zip, checking each against its hash before anything is written.
fn extract_verified(bytes: &[u8], what: &str, wanted: &[(&str, &str)]) -> Result<Vec<Vec<u8>>, String> {
    let mut archive = zip::ZipArchive::new(Cursor::new(bytes)).map_err(|e| format!("Bad {what} archive: {e}"))?;
    let mut contents = Vec::with_capacity(wanted.len());
    for (name, sha256) in wanted {
        let mut entry = archive.by_name(name).map_err(|_| format!("The {what} archive is missing {name}"))?;
        let mut data = Vec::with_capacity(entry.size() as usize);
        entry.read_to_end(&mut data).map_err(|e| e.to_string())?;
        if sha256_hex(&data) != *sha256 {
            return Err(format!("{what}: {name} didn't match its checksum, so I didn't install it. Tell Blazzer."));
        }
        contents.push(data);
    }
    Ok(contents)
}

/// Pulls just the client DLL out of the mod archive and checks it against the expected hash.
fn extract_dll(bytes: &[u8], manifest: &Manifest, game: &Path) -> Result<(), String> {
    let package = &manifest.mod_package;
    let dll = extract_verified(bytes, "server mod", &[(package.dll.as_str(), package.dll_sha256.as_str())])?.remove(0);
    write_game_file(game, &package.dll, &dll)
}

/// Whether a mod on the current list ships this path (case-insensitive, like the Windows game folder).
fn claimed(manifest: &Manifest, path: &str) -> bool {
    manifest.client_mods().flat_map(|m| &m.files).any(|f| f.path.eq_ignore_ascii_case(path))
}

/// Writes one verified extra mod, dropping files an older version of it shipped that no current mod ships.
fn install_mod(game: &Path, manifest: &Manifest, m: &ExtraMod, bytes: &[u8], marker: &mut Marker) -> Result<(), String> {
    let wanted: Vec<(&str, &str)> = m.files.iter().map(|f| (f.source.as_str(), f.sha256.as_str())).collect();
    let contents = extract_verified(bytes, &m.name, &wanted)?;
    if let Some(previous) = marker.mods.get(&m.id) {
        for old in previous.files.iter().filter(|old| !claimed(manifest, old)) {
            remove_game_file(game, old)?;
        }
    }
    for (file, data) in m.files.iter().zip(&contents) {
        write_game_file(game, &file.path, data)?;
    }
    marker.mods.insert(
        m.id.clone(),
        InstalledMod {
            name: m.name.clone(),
            version: m.version.clone(),
            files: m.files.iter().map(|f| f.path.clone()).collect(),
        },
    );
    Ok(())
}

/// Removes the mods this launcher installed that the server no longer runs, file by file from the marker.
fn remove_stale(game: &Path, manifest: &Manifest, marker: &mut Marker) -> Result<(), String> {
    let stale: Vec<String> = marker.mods.keys().filter(|id| is_stale(manifest, id)).cloned().collect();
    for id in stale {
        for file in marker.mods[&id].files.iter().filter(|f| !claimed(manifest, f)) {
            remove_game_file(game, file)?;
        }
        marker.mods.remove(&id);
    }
    Ok(())
}

fn write_marker(game: &Path, marker: &Marker) -> Result<(), String> {
    let path = game.join(MARKER);
    fs::create_dir_all(path.parent().expect("marker has a parent")).map_err(|e| e.to_string())?;
    fs::write(&path, serde_json::to_string_pretty(marker).map_err(|e| e.to_string())?).map_err(|e| e.to_string())
}

/// Installs whatever is missing or outdated. Each package is downloaded and verified before anything is written.
pub fn install(game: &Path, manifest: &Manifest, on_progress: &dyn Fn(Progress)) -> Result<(), String> {
    let current = state(game, manifest);
    let mut marker = read_marker(game);

    if !current.melonloader.up_to_date {
        let label = format!("MelonLoader {}", manifest.melonloader.version);
        let bytes =
            download(on_progress, "melonloader", &label, (1, 1), &manifest.melonloader.url, &manifest.melonloader.sha256)?;
        extract_all(&bytes, game)?;
        marker.melonloader = Some(manifest.melonloader.version.clone());
        write_marker(game, &marker)?;
    }

    if !current.server_mod.up_to_date {
        let mod_package = &manifest.mod_package;
        let label = format!("server mod {}", mod_package.version);
        let bytes = download(on_progress, "mod", &label, (1, 1), &mod_package.url, &mod_package.sha256)?;
        extract_dll(&bytes, manifest, game)?;
        for stale in mod_package.remove_dlls.iter().filter(|d| !claimed(manifest, d)) {
            remove_game_file(game, stale)?;
        }
        marker.server_mod = Some(mod_package.version.clone());
        write_marker(game, &marker)?;
    }

    let tracked = marker.mods.len();
    remove_stale(game, manifest, &mut marker)?;
    if marker.mods.len() != tracked {
        write_marker(game, &marker)?;
    }

    // Checked again after the removals above, so a file one of them deleted is put back.
    let pending: Vec<&ExtraMod> =
        manifest.client_mods().filter(|m| !mod_status(game, m, &marker).up_to_date).collect();
    for (i, m) in pending.iter().enumerate() {
        let label = format!("{} {}", m.name, m.version);
        let bytes = download(on_progress, "mods", &label, (i + 1, pending.len()), &m.url, &m.sha256)?;
        install_mod(game, manifest, m, &bytes, &mut marker)?;
        write_marker(game, &marker)?;
    }
    Ok(())
}

/// Removes the server mod, the server's extra mods and this launcher's marker; with `melonloader`, also
/// MelonLoader's loader files. Leaves UserData/ and every mod the player added themselves.
pub fn uninstall(game: &Path, manifest: &Manifest, melonloader: bool) -> Result<(), String> {
    let marker = read_marker(game);
    for file in marker.mods.values().flat_map(|m| &m.files) {
        remove_game_file(game, file)?;
    }
    // Files that match the current list byte for byte came from this launcher too, even without a marker entry.
    for file in manifest.client_mods().flat_map(|m| &m.files) {
        if file_matches(&game_file(game, &file.path), &file.sha256) {
            remove_game_file(game, &file.path)?;
        }
    }
    remove_game_file(game, &manifest.mod_package.dll)?;
    remove_game_file(game, MARKER)?;
    if melonloader {
        remove_game_file(game, "version.dll")?;
        let loader = game.join("MelonLoader");
        if loader.is_dir() {
            fs::remove_dir_all(&loader).map_err(|e| format!("Couldn't remove MelonLoader: {e}"))?;
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::manifest::{ModFile, Side};
    use std::cell::Cell;

    fn zip_of(files: &[(&str, &[u8])]) -> Vec<u8> {
        use std::io::Write;
        let mut writer = zip::ZipWriter::new(Cursor::new(Vec::new()));
        let options = zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Stored);
        for (name, data) in files {
            writer.start_file(*name, options).unwrap();
            writer.write_all(data).unwrap();
        }
        writer.finish().unwrap().into_inner()
    }

    fn extra(id: &str, side: Side, archive: &[u8], files: &[(&str, &str, &[u8])]) -> ExtraMod {
        ExtraMod {
            id: id.into(),
            name: format!("Mod {id}"),
            version: "1.0.0".into(),
            blurb: String::new(),
            side,
            url: "https://thunderstore.io/package/download/x/y/1.0.0/".into(),
            sha256: sha256_hex(archive),
            files: files
                .iter()
                .map(|(source, path, data)| ModFile { source: (*source).into(), path: (*path).into(), sha256: sha256_hex(data) })
                .collect(),
        }
    }

    /// Install, update (with a renamed file), refuse a tampered archive, then remove when the server drops it.
    #[test]
    fn installs_updates_and_removes_extra_mods() {
        let game = std::env::temp_dir().join(format!("hyland-link-mods-{}", std::process::id()));
        let _ = fs::remove_dir_all(&game);
        fs::create_dir_all(game.join("Mods")).unwrap();
        fs::write(game.join("Mods").join("Mine.dll"), b"a friend's own mod").unwrap();
        let mut manifest = crate::manifest::baked();

        let v1 = zip_of(&[("Mods/Example.dll", b"v1"), ("README.md", b"hi")]);
        let mut example = extra("example", Side::Both, &v1, &[("Mods/Example.dll", "Mods/Example.dll", b"v1")]);
        let server_only = extra("admin", Side::Server, &v1, &[("Mods/Example.dll", "Mods/Admin.dll", b"v1")]);
        manifest.mods = vec![example.clone(), server_only];

        let fresh = state(&game, &manifest);
        assert_eq!(fresh.mods.len(), 1, "server-only mods never reach players");
        assert!(!fresh.mods[0].installed && !fresh.mods[0].up_to_date);

        let mut marker = read_marker(&game);
        install_mod(&game, &manifest, &example, &v1, &mut marker).unwrap();
        write_marker(&game, &marker).unwrap();
        assert!(state(&game, &manifest).mods[0].up_to_date);

        let v2 = zip_of(&[("Example2.dll", b"v2")]);
        example = ExtraMod { version: "2.0.0".into(), ..extra("example", Side::Both, &v2, &[("Example2.dll", "Mods/Example2.dll", b"v2")]) };
        manifest.mods = vec![example.clone()];
        let outdated = state(&game, &manifest);
        assert!(outdated.mods[0].installed && !outdated.mods[0].up_to_date);
        assert_eq!(outdated.mods[0].installed_version.as_deref(), Some("1.0.0"));

        install_mod(&game, &manifest, &example, &v2, &mut marker).unwrap();
        write_marker(&game, &marker).unwrap();
        assert!(!game.join("Mods").join("Example.dll").exists(), "the old version's file is gone");
        assert!(state(&game, &manifest).mods[0].up_to_date);

        let tampered = zip_of(&[("Example2.dll", b"not what the manifest says")]);
        assert!(install_mod(&game, &manifest, &example, &tampered, &mut marker).is_err());
        assert_eq!(fs::read(game.join("Mods").join("Example2.dll")).unwrap(), b"v2", "nothing written on a bad hash");

        manifest.mods.clear();
        assert_eq!(state(&game, &manifest).stale_mods.len(), 1);
        remove_stale(&game, &manifest, &mut marker).unwrap();
        write_marker(&game, &marker).unwrap();
        assert!(!game.join("Mods").join("Example2.dll").exists());
        assert!(state(&game, &manifest).stale_mods.is_empty());
        assert!(game.join("Mods").join("Mine.dll").exists(), "a friend's own mod is never touched");
        fs::remove_dir_all(&game).unwrap();
    }

    /// A file that moves from one mod to another (or a renamed mod id) is still there after the install.
    #[test]
    fn a_file_that_moves_to_another_mod_is_kept() {
        let game = std::env::temp_dir().join(format!("hyland-link-move-{}", std::process::id()));
        let _ = fs::remove_dir_all(&game);
        fs::create_dir_all(game.join("Mods")).unwrap();
        let mut manifest = crate::manifest::baked();

        let shared = zip_of(&[("Shared.dll", b"same bytes")]);
        let old = extra("old", Side::Client, &shared, &[("Shared.dll", "Mods/Shared.dll", b"same bytes")]);
        manifest.mods = vec![old.clone()];
        let mut marker = read_marker(&game);
        install_mod(&game, &manifest, &old, &shared, &mut marker).unwrap();
        write_marker(&game, &marker).unwrap();

        // Renamed id, same file and hash: the new id already counts as installed, and the stale old id must not
        // take the file with it.
        manifest.mods = vec![extra("new", Side::Client, &shared, &[("Shared.dll", "Mods/Shared.dll", b"same bytes")])];
        remove_stale(&game, &manifest, &mut marker).unwrap();
        assert!(!marker.mods.contains_key("old"));
        assert!(game.join("Mods").join("Shared.dll").exists(), "the renamed mod's file is kept");
        assert!(state(&game, &manifest).mods[0].up_to_date);
        fs::remove_dir_all(&game).unwrap();
    }

    /// Real downloads from GitHub into a fake game folder: `cargo test -- --ignored`.
    #[test]
    #[ignore]
    fn installs_and_uninstalls_into_a_fake_game_folder() {
        let game = std::env::temp_dir().join(format!("hyland-link-install-{}", std::process::id()));
        let _ = fs::remove_dir_all(&game);
        fs::create_dir_all(game.join("Mods")).unwrap();
        fs::write(game.join("Schedule I.exe"), b"fake").unwrap();
        fs::write(game.join("Mods").join("DedicatedServerMod_Mono_Client.dll"), b"old runtime").unwrap();
        fs::write(game.join("Mods").join("SomeoneElsesMod.dll"), b"keep me").unwrap();
        let manifest = crate::manifest::baked();

        let before = state(&game, &manifest);
        assert!(!before.melonloader.installed && !before.server_mod.installed);

        let events = Cell::new(0u32);
        install(&game, &manifest, &|_| events.set(events.get() + 1)).unwrap();
        assert!(events.get() > 2, "progress events were emitted");

        let after = state(&game, &manifest);
        assert!(after.melonloader.up_to_date, "{:?}", after.melonloader);
        assert!(after.server_mod.up_to_date, "{:?}", after.server_mod);
        assert_eq!(after.melonloader.version.as_deref(), Some(manifest.melonloader.version.as_str()));
        assert!(!game.join("Mods").join("DedicatedServerMod_Mono_Client.dll").exists(), "wrong-runtime DLL removed");
        assert!(game.join("Mods").join("SomeoneElsesMod.dll").exists(), "other mods untouched");
        let extra_files: Vec<_> =
            manifest.mods.iter().filter(|m| m.on_client()).flat_map(|m| &m.files).map(|f| game.join(&f.path)).collect();
        for file in &extra_files {
            assert!(file.exists(), "extra mod file installed: {}", file.display());
        }

        // A second run has nothing to do.
        events.set(0);
        install(&game, &manifest, &|_| events.set(events.get() + 1)).unwrap();
        assert_eq!(events.get(), 0);

        // An older MelonLoader (every player when the manifest moves on) gets the new one over it.
        let mut marker = read_marker(&game);
        marker.melonloader = Some("0.7.2".into());
        write_marker(&game, &marker).unwrap();
        assert!(!state(&game, &manifest).melonloader.up_to_date);
        install(&game, &manifest, &|_| {}).unwrap();
        let upgraded = state(&game, &manifest);
        assert!(upgraded.melonloader.up_to_date && upgraded.server_mod.up_to_date, "{:?}", upgraded.melonloader);

        uninstall(&game, &manifest, true).unwrap();
        let removed = state(&game, &manifest);
        assert!(!removed.melonloader.installed && !removed.server_mod.installed);
        assert!(game.join("Mods").join("SomeoneElsesMod.dll").exists(), "uninstall keeps other mods");
        assert!(extra_files.iter().all(|file| !file.exists()), "uninstall removes extra mods");
        assert!(game.join("Schedule I.exe").exists());
        fs::remove_dir_all(&game).unwrap();
    }
}
