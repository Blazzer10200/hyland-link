//! The version file: which MelonLoader / mod builds to install, where the server is.
//! A copy is compiled in; the online copy wins when it has a higher `revision`.

use std::time::Duration;

use serde::{Deserialize, Serialize};

pub const REMOTE_URL: &str =
    "https://raw.githubusercontent.com/Blazzer10200/hyland-link/main/manifest/manifest.json";
const BAKED: &str = include_str!("../../../manifest/manifest.json");
const SCHEMA: u32 = 1;
/// Downloads must start on GitHub; GitHub then redirects to its own asset host. Checksums guard the bytes.
const ALLOWED_DOWNLOAD_HOST: &str = "https://github.com/";

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
    pub launcher: Launcher,
    #[serde(default)]
    pub notice: String,
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
    fn validate(&self) -> Result<(), String> {
        if self.schema != SCHEMA {
            return Err(format!("version file schema {} (this launcher understands {SCHEMA})", self.schema));
        }
        for (name, url, sha) in [
            ("MelonLoader", &self.melonloader.url, &self.melonloader.sha256),
            ("server mod", &self.mod_package.url, &self.mod_package.sha256),
        ] {
            if !url.starts_with(ALLOWED_DOWNLOAD_HOST) {
                return Err(format!("{name} download isn't on GitHub: {url}"));
            }
            if !is_sha256(sha) {
                return Err(format!("{name} checksum is malformed"));
            }
        }
        if !is_sha256(&self.mod_package.dll_sha256) {
            return Err("server mod DLL checksum is malformed".into());
        }
        if self.mod_package.dll.contains("..") || self.mod_package.remove_dlls.iter().any(|d| d.contains("..")) {
            return Err("server mod paths must stay inside the game folder".into());
        }
        Ok(())
    }
}

fn is_sha256(s: &str) -> bool {
    s.len() == 64 && s.bytes().all(|b| b.is_ascii_hexdigit())
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
