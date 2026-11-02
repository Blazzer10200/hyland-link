mod favorites;
mod game;
mod install;
mod manifest;
mod status;

use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;

use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter, Manager, State};
use tauri_plugin_opener::OpenerExt;
use tauri_plugin_updater::UpdaterExt;

use manifest::{Manifest, Source};

struct Shared {
    manifest: Mutex<(Manifest, Source)>,
}

#[derive(Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct Settings {
    game_path: Option<String>,
}

fn settings_file(app: &AppHandle) -> Result<PathBuf, String> {
    Ok(app.path().app_config_dir().map_err(|e| e.to_string())?.join("settings.json"))
}

fn load_settings(app: &AppHandle) -> Settings {
    settings_file(app)
        .ok()
        .and_then(|p| fs::read_to_string(p).ok())
        .and_then(|t| serde_json::from_str(&t).ok())
        .unwrap_or_default()
}

fn save_settings(app: &AppHandle, settings: &Settings) -> Result<(), String> {
    let path = settings_file(app)?;
    fs::create_dir_all(path.parent().expect("settings file has a parent")).map_err(|e| e.to_string())?;
    fs::write(path, serde_json::to_string_pretty(settings).map_err(|e| e.to_string())?).map_err(|e| e.to_string())
}

fn manifest(shared: &State<Shared>) -> Manifest {
    shared.manifest.lock().expect("manifest lock").0.clone()
}

fn detect_game(app: &AppHandle, manifest: &Manifest) -> game::GameInfo {
    let manual = load_settings(app).game_path.map(PathBuf::from);
    game::detect(manifest.game.app_id, manual.as_deref())
}

fn require_game(app: &AppHandle, manifest: &Manifest) -> Result<(PathBuf, game::GameInfo), String> {
    let info = detect_game(app, manifest);
    let path = info.path.clone().ok_or("I can't find Schedule I. Use “Choose game folder” to point me at it.")?;
    if info.running {
        return Err("Schedule I is running. Close the game first, then try again.".into());
    }
    Ok((PathBuf::from(path), info))
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct Snapshot {
    manifest: Manifest,
    manifest_source: Source,
    launcher_version: String,
    game: game::GameInfo,
    install: Option<install::InstallState>,
    favorite_added: bool,
}

#[tauri::command]
fn get_state(app: AppHandle, shared: State<Shared>) -> Snapshot {
    let (manifest, source) = shared.manifest.lock().expect("manifest lock").clone();
    let game = detect_game(&app, &manifest);
    let dir = game.path.as_ref().map(PathBuf::from);
    Snapshot {
        install: dir.as_deref().map(|d| install::state(d, &manifest)),
        favorite_added: dir.as_deref().is_some_and(|d| favorites::has(d, &manifest.server.host, manifest.server.port)),
        launcher_version: app.package_info().version.to_string(),
        manifest,
        manifest_source: source,
        game,
    }
}

/// Re-reads the online version file (blocking network call, so off the main thread).
#[tauri::command]
async fn refresh_manifest(shared: State<'_, Shared>) -> Result<(), String> {
    let resolved = tauri::async_runtime::spawn_blocking(manifest::resolve).await.map_err(|e| e.to_string())?;
    *shared.manifest.lock().expect("manifest lock") = resolved;
    Ok(())
}

#[tauri::command]
async fn run_install(app: AppHandle, shared: State<'_, Shared>) -> Result<(), String> {
    let manifest = manifest(&shared);
    let (dir, info) = require_game(&app, &manifest)?;
    if let Some(branch) = info.branch {
        return Err(format!(
            "Your game is on the “{branch}” beta branch. In Steam set Schedule I → Properties → Betas to None, let it update, then try again."
        ));
    }
    let emitter = app.clone();
    tauri::async_runtime::spawn_blocking(move || {
        install::install(&dir, &manifest, &|p| {
            let _ = emitter.emit("install-progress", p);
        })?;
        favorites::ensure(&dir, &manifest.server.name, &manifest.server.host, manifest.server.port)
    })
    .await
    .map_err(|e| e.to_string())?
}

#[tauri::command]
async fn query_server(shared: State<'_, Shared>) -> Result<status::ServerStatus, String> {
    let server = manifest(&shared).server;
    tauri::async_runtime::spawn_blocking(move || status::query(&server.host, server.port))
        .await
        .map_err(|e| e.to_string())
}

#[tauri::command]
fn play(app: AppHandle, shared: State<Shared>) -> Result<(), String> {
    let app_id = manifest(&shared).game.app_id;
    app.opener()
        .open_url(format!("steam://rungameid/{app_id}"), None::<&str>)
        .map_err(|e| format!("Couldn't start Steam ({e}). Open Steam and launch Schedule I from there."))
}

/// Accepts a hand-picked folder only if it really holds the game.
#[tauri::command]
fn set_game_path(app: AppHandle, path: String) -> Result<(), String> {
    let dir = PathBuf::from(&path);
    if !game::is_game_dir(&dir) {
        return Err(format!("That folder doesn't have “{}” in it. Pick the folder the game is installed in.", game::EXE_NAME));
    }
    save_settings(&app, &Settings { game_path: Some(path) })
}

#[tauri::command]
fn open_game_folder(app: AppHandle, shared: State<Shared>) -> Result<(), String> {
    let path = detect_game(&app, &manifest(&shared)).path.ok_or("I can't find Schedule I yet.")?;
    app.opener().open_path(path, None::<&str>).map_err(|e| e.to_string())
}

#[tauri::command]
fn open_launcher_download(app: AppHandle, shared: State<Shared>) -> Result<(), String> {
    let url = manifest(&shared).launcher.download_url;
    if !url.starts_with("https://github.com/") {
        return Err("No download link for the new launcher yet. Ask Blazzer for it.".into());
    }
    app.opener().open_url(url, None::<&str>).map_err(|e| e.to_string())
}

/// Asks GitHub Releases for a newer signed launcher. Ok(None) = already on the latest.
#[tauri::command]
async fn check_update(app: AppHandle) -> Result<Option<String>, String> {
    let update = app.updater().map_err(|e| e.to_string())?.check().await.map_err(|e| e.to_string())?;
    Ok(update.map(|u| u.version))
}

/// Downloads the new launcher, streaming "update-progress" (0..=100). The plugin checks the minisign signature
/// against the pubkey in tauri.conf.json first. On Windows the NSIS installer then closes this app and reopens
/// the new one; restart() only runs where that doesn't happen.
#[tauri::command]
async fn install_update(app: AppHandle) -> Result<(), String> {
    let update = app
        .updater()
        .map_err(|e| e.to_string())?
        .check()
        .await
        .map_err(|e| e.to_string())?
        .ok_or("No launcher update is waiting.")?;
    let progress = app.clone();
    let mut downloaded: u64 = 0;
    update
        .download_and_install(
            move |chunk, total| {
                downloaded += chunk as u64;
                if let Some(total) = total.filter(|t| *t > 0) {
                    let _ = progress.emit("update-progress", ((downloaded * 100) / total).min(100));
                }
            },
            || {},
        )
        .await
        .map_err(|e| e.to_string())?;
    app.restart();
}

#[tauri::command]
fn uninstall(app: AppHandle, shared: State<Shared>, melonloader: bool) -> Result<(), String> {
    let manifest = manifest(&shared);
    let (dir, _) = require_game(&app, &manifest)?;
    install::uninstall(&dir, &manifest, melonloader)
}

/// Windows 11 draws a light 1px border on undecorated windows; tint it to the app's edge color and keep round corners.
fn style_window_frame(window: &tauri::WebviewWindow) {
    use windows_sys::Win32::Graphics::Dwm::{
        DWMWA_BORDER_COLOR, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, DwmSetWindowAttribute,
    };
    let Ok(hwnd) = window.hwnd() else { return };
    let border: u32 = 0x003b_312a; // COLORREF 0x00BBGGRR for #2a313b
    let corners = DWMWCP_ROUND;
    unsafe {
        DwmSetWindowAttribute(hwnd.0 as _, DWMWA_BORDER_COLOR as u32, (&raw const border).cast(), 4);
        DwmSetWindowAttribute(hwnd.0 as _, DWMWA_WINDOW_CORNER_PREFERENCE as u32, (&raw const corners).cast(), 4);
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .manage(Shared { manifest: Mutex::new((manifest::baked(), Source::BuiltIn { reason: "checking for updates".into() })) })
        .setup(|app| {
            if let Some(window) = app.get_webview_window("main") {
                style_window_frame(&window);
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            get_state,
            refresh_manifest,
            run_install,
            query_server,
            play,
            set_game_path,
            open_game_folder,
            open_launcher_download,
            check_update,
            install_update,
            uninstall
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
