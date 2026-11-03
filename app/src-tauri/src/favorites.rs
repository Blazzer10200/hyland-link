//! Pre-adding the server to the mod's Favorites (UserData/DedicatedServerClientServers.json).
//! The mod loads this with Newtonsoft (PascalCase, unknown fields kept), dedupes by host:port,
//! and fills missing fields with defaults, so we add only Id/Name/Host/Port and leave the rest alone.

use std::fs;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use serde_json::{Value, json};
use sha2::{Digest, Sha256};

fn file(game: &Path) -> PathBuf {
    game.join("UserData").join("DedicatedServerClientServers.json")
}

fn same_endpoint(entry: &Value, host: &str, port: u16) -> bool {
    let entry_host = entry.get("Host").and_then(Value::as_str).unwrap_or_default();
    let entry_port = entry.get("Port").and_then(Value::as_u64).unwrap_or(38465);
    entry_host.trim().eq_ignore_ascii_case(host) && entry_port == u64::from(port)
}

enum LoadError {
    /// The file is there but isn't JSON (a crash mid-write, a hand edit).
    Unparsable,
    Io(String),
}

fn load(path: &Path) -> Result<Option<Value>, LoadError> {
    match fs::read_to_string(path) {
        Ok(text) => serde_json::from_str(&text).map(Some).map_err(|_| LoadError::Unparsable),
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(None),
        Err(e) => Err(LoadError::Io(format!("Couldn't open your saved-servers file: {e}"))),
    }
}

/// The shape the mod reads: an object whose Favorites, when present, is a list (or null).
fn usable(doc: &Value) -> bool {
    doc.is_object() && doc.get("Favorites").is_none_or(|f| f.is_null() || f.is_array())
}

/// Keeps a saved-servers file the mod can't use under a new name (never deletes it), so Favorites can start fresh.
fn set_aside(path: &Path) -> Result<(), String> {
    let secs = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs()).unwrap_or_default();
    let backup = path.with_extension(format!("json.unreadable-{secs}"));
    fs::rename(path, &backup).map_err(|e| format!("Your saved-servers file is damaged and I couldn't move it aside: {e}"))
}

pub fn has(game: &Path, host: &str, port: u16) -> bool {
    matches!(load(&file(game)), Ok(Some(doc)) if doc
        .get("Favorites")
        .and_then(Value::as_array)
        .is_some_and(|list| list.iter().any(|e| same_endpoint(e, host, port))))
}

fn new_id() -> String {
    let nanos = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_nanos()).unwrap_or_default();
    let digest = Sha256::digest(format!("{nanos}-{}", std::process::id()).as_bytes());
    digest.iter().take(16).map(|b| format!("{b:02x}")).collect()
}

/// Adds the server at the top of Favorites unless it's already there. A file it can't use is kept aside, not rewritten.
pub fn ensure(game: &Path, name: &str, host: &str, port: u16) -> Result<(), String> {
    let path = file(game);
    let fresh = || json!({ "Favorites": [], "History": [] });
    let mut doc = match load(&path) {
        Ok(Some(doc)) if usable(&doc) => doc,
        Ok(None) => fresh(),
        Ok(Some(_)) | Err(LoadError::Unparsable) => {
            set_aside(&path)?;
            fresh()
        }
        Err(LoadError::Io(e)) => return Err(e),
    };
    let root = doc.as_object_mut().ok_or("Your saved-servers file isn't in the expected format, so I left it alone.")?;
    let favorites = root.entry("Favorites").or_insert_with(|| json!([]));
    if favorites.is_null() {
        *favorites = json!([]);
    }
    let list = favorites.as_array_mut().ok_or("Your saved-servers file isn't in the expected format, so I left it alone.")?;
    if list.iter().any(|e| same_endpoint(e, host, port)) {
        return Ok(());
    }
    list.insert(0, json!({ "Id": new_id(), "Name": name, "Host": host, "Port": port }));

    fs::create_dir_all(path.parent().expect("favorites file has a parent")).map_err(|e| e.to_string())?;
    let tmp = path.with_extension("json.tmp");
    let text = serde_json::to_string_pretty(&doc).map_err(|e| e.to_string())?;
    fs::write(&tmp, text).map_err(|e| format!("Couldn't save the server to Favorites: {e}"))?;
    fs::rename(&tmp, &path).map_err(|e| format!("Couldn't save the server to Favorites: {e}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn game_dir(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("hyland-link-fav-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(dir.join("UserData")).unwrap();
        dir
    }

    #[test]
    fn creates_the_file_when_missing() {
        let game = game_dir("new");
        assert!(!has(&game, "69.50.245.12", 38465));
        ensure(&game, "Blazzer's Schedule I", "69.50.245.12", 38465).unwrap();
        assert!(has(&game, "69.50.245.12", 38465));
        let doc: Value = serde_json::from_str(&fs::read_to_string(file(&game)).unwrap()).unwrap();
        assert_eq!(doc["Favorites"][0]["Port"], 38465);
        assert_eq!(doc["History"], json!([]));
        fs::remove_dir_all(&game).unwrap();
    }

    #[test]
    fn keeps_existing_servers_and_fields_and_never_duplicates() {
        let game = game_dir("existing");
        let original = json!({
            "Favorites": [{ "Id": "abc", "Name": "Other", "Host": "1.2.3.4", "Port": 38465, "PingMilliseconds": 40, "Extra": true }],
            "History": [{ "Id": "h1", "Host": "5.6.7.8", "Port": 1 }]
        });
        fs::write(file(&game), original.to_string()).unwrap();
        ensure(&game, "Blazzer's Schedule I", "69.50.245.12", 38465).unwrap();
        ensure(&game, "Blazzer's Schedule I", "69.50.245.12", 38465).unwrap();
        let doc: Value = serde_json::from_str(&fs::read_to_string(file(&game)).unwrap()).unwrap();
        let favorites = doc["Favorites"].as_array().unwrap();
        assert_eq!(favorites.len(), 2);
        assert_eq!(favorites[0]["Host"], "69.50.245.12");
        assert_eq!(favorites[1], original["Favorites"][0]);
        assert_eq!(doc["History"], original["History"]);
        fs::remove_dir_all(&game).unwrap();
    }

    #[test]
    fn keeps_an_unreadable_file_aside_and_starts_fresh() {
        for (name, broken) in [("corrupt", "{ not json"), ("wrong-shape", r#"{ "Favorites": 5 }"#)] {
            let game = game_dir(name);
            fs::write(file(&game), broken).unwrap();
            ensure(&game, "x", "69.50.245.12", 38465).unwrap();
            assert!(has(&game, "69.50.245.12", 38465));
            let kept: Vec<String> = fs::read_dir(game.join("UserData"))
                .unwrap()
                .filter_map(|e| e.ok()?.file_name().into_string().ok())
                .filter(|n| n.starts_with("DedicatedServerClientServers.json.unreadable-"))
                .collect();
            assert_eq!(kept.len(), 1, "{name}: the damaged file is kept");
            assert_eq!(fs::read_to_string(game.join("UserData").join(&kept[0])).unwrap(), broken);
            fs::remove_dir_all(&game).unwrap();
        }
    }
}
