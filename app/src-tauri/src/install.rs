//! Installing / checking MelonLoader and the server client mod in the game folder.

use std::fs;
use std::io::{Cursor, Read};
use std::path::{Path, PathBuf};
use std::time::Duration;

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::manifest::Manifest;

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
    /// MelonLoader finished its slow first IL2CPP setup (it writes Il2CppAssemblies on the first launch).
    pub first_launch_done: bool,
}

/// What this launcher installed last time, kept next to the mod's own data.
#[derive(Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Marker {
    melonloader: Option<String>,
    server_mod: Option<String>,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Progress<'a> {
    pub step: &'a str,
    pub label: &'a str,
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
        first_launch_done,
    }
}

fn download(
    on_progress: &dyn Fn(Progress),
    step: &str,
    label: &str,
    url: &str,
    expected_sha256: &str,
) -> Result<Vec<u8>, String> {
    let emit = |done, total| on_progress(Progress { step, label, done, total });
    emit(0, 0);
    let mut response = ureq::get(url)
        .config()
        .timeout_global(Some(Duration::from_secs(180)))
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
    let mut bytes = Vec::with_capacity(total as usize);
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

/// Pulls just the client DLL out of the mod archive and checks it against the expected hash.
fn extract_dll(bytes: &[u8], manifest: &Manifest, game: &Path) -> Result<(), String> {
    let mut archive = zip::ZipArchive::new(Cursor::new(bytes)).map_err(|e| format!("Bad mod archive: {e}"))?;
    let mut entry = archive
        .by_name(&manifest.mod_package.dll)
        .map_err(|_| format!("The mod archive is missing {}", manifest.mod_package.dll))?;
    let mut dll = Vec::with_capacity(entry.size() as usize);
    entry.read_to_end(&mut dll).map_err(|e| e.to_string())?;
    if sha256_hex(&dll) != manifest.mod_package.dll_sha256 {
        return Err("The server mod DLL didn't match its checksum, so I didn't install it. Tell Blazzer.".into());
    }
    let target = game_file(game, &manifest.mod_package.dll);
    fs::create_dir_all(target.parent().expect("mod dll has a parent")).map_err(|e| e.to_string())?;
    fs::write(&target, dll).map_err(|e| format!("Couldn't write the server mod: {e}"))
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
        let bytes = download(on_progress, "melonloader", &label, &manifest.melonloader.url, &manifest.melonloader.sha256)?;
        extract_all(&bytes, game)?;
        marker.melonloader = Some(manifest.melonloader.version.clone());
        write_marker(game, &marker)?;
    }

    if !current.server_mod.up_to_date {
        let mod_package = &manifest.mod_package;
        let label = format!("server mod {}", mod_package.version);
        let bytes = download(on_progress, "mod", &label, &mod_package.url, &mod_package.sha256)?;
        extract_dll(&bytes, manifest, game)?;
        for stale in &mod_package.remove_dlls {
            let path = game_file(game, stale);
            if path.is_file() {
                fs::remove_file(&path).map_err(|e| format!("Couldn't remove the old {stale}: {e}"))?;
            }
        }
        marker.server_mod = Some(mod_package.version.clone());
        write_marker(game, &marker)?;
    }
    Ok(())
}

/// Removes the server mod and this launcher's marker; with `melonloader`, also MelonLoader's loader files.
/// Leaves Mods/, UserData/ and anything else the player added.
pub fn uninstall(game: &Path, manifest: &Manifest, melonloader: bool) -> Result<(), String> {
    let remove_file = |path: PathBuf| -> Result<(), String> {
        if path.is_file() {
            fs::remove_file(&path).map_err(|e| format!("Couldn't remove {}: {e}", path.display()))?;
        }
        Ok(())
    };
    remove_file(game_file(game, &manifest.mod_package.dll))?;
    remove_file(game.join(MARKER))?;
    if melonloader {
        remove_file(game.join("version.dll"))?;
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
    use std::cell::Cell;

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
        assert_eq!(after.melonloader.version.as_deref(), Some("0.7.2"));
        assert!(!game.join("Mods").join("DedicatedServerMod_Mono_Client.dll").exists(), "wrong-runtime DLL removed");
        assert!(game.join("Mods").join("SomeoneElsesMod.dll").exists(), "other mods untouched");

        // A second run has nothing to do.
        events.set(0);
        install(&game, &manifest, &|_| events.set(events.get() + 1)).unwrap();
        assert_eq!(events.get(), 0);

        uninstall(&game, &manifest, true).unwrap();
        let removed = state(&game, &manifest);
        assert!(!removed.melonloader.installed && !removed.server_mod.installed);
        assert!(game.join("Mods").join("SomeoneElsesMod.dll").exists(), "uninstall keeps other mods");
        assert!(game.join("Schedule I.exe").exists());
        fs::remove_dir_all(&game).unwrap();
    }
}
