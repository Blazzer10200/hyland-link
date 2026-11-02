//! The version file: which MelonLoader / mod builds to install, where the server is, and the server's extra mods.
//! A copy is compiled in; the online copy wins when it has a higher `revision`.
//! The server applies the same file (schedule1-server `s1ds mods apply`) with the same rules, so both sides match.

use std::collections::HashSet;
use std::time::Duration;

use serde::{Deserialize, Serialize};

pub const REMOTE_URL: &str =
    "https://raw.githubusercontent.com/Blazzer10200/hyland-link/main/manifest/manifest.json";
const BAKED: &str = include_str!("../../../manifest/manifest.json");
const SCHEMA: u32 = 2;
/// Downloads must start on GitHub or Thunderstore; both redirect to their own CDN. Checksums guard the bytes.
const ALLOWED_DOWNLOAD_HOSTS: [&str; 2] = ["https://github.com/", "https://thunderstore.io/"];
/// The only game folders an extra mod may write into.
const MOD_DIRS: [&str; 4] = ["Mods/", "Plugins/", "UserLibs/", "UserData/"];

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Manifest {
    pub schema: u32,
    pub revision: u32,
    pub server: Server,
    pub game: Game,
    pub melonloader: Package,
    #[serde(rename = "mod")]
    pub mod_package: ModPackage,
    /// Extra mods the server runs, in display order.
    #[serde(default)]
    pub mods: Vec<ExtraMod>,
    pub launcher: Launcher,
    #[serde(default)]
    pub notice: String,
}

/// Where an extra mod has to be: on the server, on every player's PC, or both.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum Side {
    Both,
    Client,
    Server,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ExtraMod {
    pub id: String,
    pub name: String,
    pub version: String,
    #[serde(default)]
    pub blurb: String,
    pub side: Side,
    pub url: String,
    pub sha256: String,
    pub files: Vec<ModFile>,
}

/// One file taken from the mod's zip (`source`) and written to `path` in the game folder.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct ModFile {
    pub source: String,
    pub path: String,
    pub sha256: String,
}

impl ExtraMod {
    pub fn on_client(&self) -> bool {
        self.side != Side::Server
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Server {
    pub name: String,
    pub host: String,
    pub port: u16,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Game {
    pub app_id: u32,
    pub version: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct Package {
    pub version: String,
    pub url: String,
    pub sha256: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ModPackage {
    pub version: String,
    pub url: String,
    pub sha256: String,
    pub dll: String,
    pub dll_sha256: String,
    #[serde(default)]
    pub remove_dlls: Vec<String>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Launcher {
    pub version: String,
    #[serde(default)]
    pub download_url: String,
}

/// Where the active manifest came from, shown in the UI so a stale copy is never silent.
#[derive(Clone, Debug, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum Source {
    Online,
    BuiltIn { reason: String },
}

impl Manifest {
    pub fn client_mods(&self) -> impl Iterator<Item = &ExtraMod> {
        self.mods.iter().filter(|m| m.on_client())
    }

    fn validate(&self) -> Result<(), String> {
        if self.schema != SCHEMA {
            return Err(format!("version file schema {} (this launcher understands {SCHEMA})", self.schema));
        }
        for (name, url, sha) in [
            ("MelonLoader", &self.melonloader.url, &self.melonloader.sha256),
            ("server mod", &self.mod_package.url, &self.mod_package.sha256),
        ] {
            check_download(name, url, sha)?;
        }
        if !is_sha256(&self.mod_package.dll_sha256) {
            return Err("server mod DLL checksum is malformed".into());
        }
        if self.mod_package.dll.contains("..") || self.mod_package.remove_dlls.iter().any(|d| d.contains("..")) {
            return Err("server mod paths must stay inside the game folder".into());
        }

        let mut ids = HashSet::new();
        let mut paths: HashSet<String> = HashSet::from([self.mod_package.dll.to_ascii_lowercase()]);
        for m in &self.mods {
            let valid_id = !m.id.is_empty()
                && m.id.bytes().all(|b| b.is_ascii_lowercase() || b.is_ascii_digit() || b == b'-' || b == b'_');
            if !valid_id {
                return Err(format!("mod id “{}” must be lowercase letters, digits, - or _", m.id));
            }
            if !ids.insert(m.id.as_str()) {
                return Err(format!("mod id “{}” is listed twice", m.id));
            }
            check_download(&m.name, &m.url, &m.sha256)?;
            if m.files.is_empty() {
                return Err(format!("{} has no files", m.name));
            }
            for f in &m.files {
                if f.source.is_empty() || !is_sha256(&f.sha256) {
                    return Err(format!("{}: a file entry is incomplete", m.name));
                }
                if !is_mod_path(&f.path) {
                    return Err(format!(
                        "{}: “{}” must be a file under {}",
                        m.name,
                        f.path,
                        MOD_DIRS.join(", ")
                    ));
                }
                // Case-insensitive, like the Windows game folder.
                if !paths.insert(f.path.to_ascii_lowercase()) {
                    return Err(format!("{}: “{}” is already used by another mod", m.name, f.path));
                }
            }
        }
        Ok(())
    }
}

fn check_download(name: &str, url: &str, sha: &str) -> Result<(), String> {
    if !ALLOWED_DOWNLOAD_HOSTS.iter().any(|host| url.starts_with(host)) {
        return Err(format!("{name} download isn't on GitHub or Thunderstore: {url}"));
    }
    if !is_sha256(sha) {
        return Err(format!("{name} checksum is malformed"));
    }
    Ok(())
}

fn is_sha256(s: &str) -> bool {
    s.len() == 64 && s.bytes().all(|b| b.is_ascii_hexdigit())
}

/// A relative, forward-slash file path inside one of MOD_DIRS, with no way out of the game folder.
fn is_mod_path(path: &str) -> bool {
    MOD_DIRS.iter().any(|dir| path.starts_with(dir) && path.len() > dir.len())
        && !path.ends_with('/')
        && !path.contains('\\')
        && !path.contains(':')
        && path.split('/').all(|part| !part.is_empty() && part != "." && part != "..")
}

pub fn baked() -> Manifest {
    let manifest: Manifest = serde_json::from_str(BAKED).expect("built-in manifest.json must parse");
    manifest.validate().expect("built-in manifest.json must validate");
    manifest
}

fn fetch_remote() -> Result<Manifest, String> {
    let mut response = ureq::get(REMOTE_URL)
        .config()
        .timeout_global(Some(Duration::from_secs(8)))
        .build()
        .call()
        .map_err(|e| format!("couldn't reach the update server ({e})"))?;
    let body = response
        .body_mut()
        .read_to_string()
        .map_err(|e| format!("couldn't read the version file ({e})"))?;
    let manifest: Manifest =
        serde_json::from_str(&body).map_err(|e| format!("the online version file is broken ({e})"))?;
    manifest.validate()?;
    Ok(manifest)
}

/// Online copy when reachable, valid and at least as new as the built-in one; otherwise built-in, with the reason.
pub fn resolve() -> (Manifest, Source) {
    let built_in = baked();
    match fetch_remote() {
        Ok(remote) if remote.revision >= built_in.revision => (remote, Source::Online),
        Ok(_) => (built_in, Source::BuiltIn { reason: "the online version file is older than this launcher".into() }),
        Err(reason) => (built_in, Source::BuiltIn { reason }),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    const SHA: &str = "0000000000000000000000000000000000000000000000000000000000000000";

    fn with_mod(path: &str, url: &str) -> Manifest {
        let mut manifest = baked();
        manifest.mods = vec![ExtraMod {
            id: "example".into(),
            name: "Example".into(),
            version: "1.0.0".into(),
            blurb: String::new(),
            side: Side::Both,
            url: url.into(),
            sha256: SHA.into(),
            files: vec![ModFile { source: "Example.dll".into(), path: path.into(), sha256: SHA.into() }],
        }];
        manifest
    }

    #[test]
    fn the_shipped_manifest_validates() {
        baked();
    }

    #[test]
    fn accepts_thunderstore_mods_under_the_mod_folders() {
        let url = "https://thunderstore.io/package/download/Someone/Example/1.0.0/";
        with_mod("Mods/Example.dll", url).validate().unwrap();
        with_mod("UserLibs/Dep.dll", url).validate().unwrap();
    }

    #[test]
    fn rejects_paths_that_leave_the_mod_folders() {
        let url = "https://github.com/someone/example/releases/download/v1/Example.zip";
        for path in ["Mods/../version.dll", "version.dll", "Mods/", "Mods\\Example.dll", "C:/Mods/x.dll", "/Mods/x.dll", "Mods//x.dll"] {
            assert!(with_mod(path, url).validate().is_err(), "{path} should be rejected");
        }
    }

    #[test]
    fn rejects_other_hosts_and_clashes() {
        assert!(with_mod("Mods/Example.dll", "https://example.com/Example.zip").validate().is_err());
        let url = "https://github.com/someone/example/releases/download/v1/Example.zip";
        let manifest = baked();
        assert!(with_mod(&manifest.mod_package.dll, url).validate().is_err(), "can't overwrite the server mod");

        let mut twice = with_mod("Mods/Example.dll", url);
        twice.mods.push(twice.mods[0].clone());
        assert!(twice.validate().is_err(), "duplicate id");
        twice.mods[1].id = "other".into();
        assert!(twice.validate().is_err(), "same file from two mods");
    }
}
