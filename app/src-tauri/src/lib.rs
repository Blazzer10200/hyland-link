mod favorites;
mod game;
mod game_log;
mod install;
mod manifest;
mod status;

use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;

use log::{info, warn};
use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter, Manager, State};
use tauri_plugin_log::{RotationStrategy, Target, TargetKind, TimezoneStrategy};
use tauri_plugin_opener::OpenerExt;
use tauri_plugin_updater::UpdaterExt;

use manifest::{Manifest, Source};

struct Shared {
    manifest: Mutex<(Manifest, Source)>,
    /// The game folder and whether it ran, as last seen: the log polls use it, and the launcher log notes changes.
    game: Mutex<(Option<String>, bool)>,
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

/// Polled every few seconds. It scans processes and hashes the installed mod files, so it runs off the main thread.
#[tauri::command]
async fn get_state(app: AppHandle, shared: State<'_, Shared>) -> Result<Snapshot, String> {
    let (manifest, source) = shared.manifest.lock().expect("manifest lock").clone();
    let snapshot = tauri::async_runtime::spawn_blocking(move || {
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
    })
    .await
    .map_err(|e| e.to_string())?;

    let game = &snapshot.game;
    let mut seen = shared.game.lock().expect("game lock");
    if seen.0 != game.path {
        info!("Game folder: {}", game.path.as_deref().unwrap_or("not found"));
    }
    if seen.1 != game.running {
        info!("Schedule I {}", if game.running { "started" } else { "closed" });
    }
    *seen = (game.path.clone(), game.running);
    drop(seen);
    Ok(snapshot)
}

/// Re-reads the online version file (blocking network call, so off the main thread).
#[tauri::command]
async fn refresh_manifest(shared: State<'_, Shared>) -> Result<(), String> {
    let resolved = tauri::async_runtime::spawn_blocking(manifest::resolve).await.map_err(|e| e.to_string())?;
    let mut current = shared.manifest.lock().expect("manifest lock");
    let describe = |(m, s): &(Manifest, Source)| format!("revision {} from {s:?}", m.revision);
    if describe(&current) != describe(&resolved) {
        info!("Mod list: {}", describe(&resolved));
    }
    *current = resolved;
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
    info!("Install started (mod list revision {})", manifest.revision);
    let emitter = app.clone();
    let result = tauri::async_runtime::spawn_blocking(move || {
        install::install(&dir, &manifest, &|p| {
            if p.done == 0 {
                info!("Downloading {}", p.label);
            }
            let _ = emitter.emit("install-progress", p);
        })?;
        favorites::ensure(&dir, &manifest.server.name, &manifest.server.host, manifest.server.port)
    })
    .await
    .map_err(|e| e.to_string())?;
    match &result {
        Ok(()) => info!("Install finished"),
        Err(e) => warn!("Install failed: {e}"),
    }
    result
}

#[tauri::command]
async fn query_server(shared: State<'_, Shared>) -> Result<status::ServerStatus, String> {
    let server = manifest(&shared).server;
    tauri::async_runtime::spawn_blocking(move || status::query(&server.host, server.port))
        .await
        .map_err(|e| e.to_string())
}

/// Hides MelonLoader's console first (the launcher shows its log instead), then starts the game through Steam.
#[tauri::command]
fn play(app: AppHandle, shared: State<Shared>) -> Result<(), String> {
    let manifest = manifest(&shared);
    let (dir, _) = require_game(&app, &manifest)?;
    // Cosmetic: if the setting can't be written, the console shows up and the game still starts.
    match game_log::hide_console(&dir) {
        Ok(true) => info!("Turned MelonLoader's console window off in UserData/Loader.cfg"),
        Ok(false) => {}
        Err(e) => warn!("{e} The console window will show."),
    }
    info!("Play: starting Schedule I through Steam");
    app.opener()
        .open_url(format!("steam://rungameid/{}", manifest.game.app_id), None::<&str>)
        .map_err(|e| format!("Couldn't start Steam ({e}). Open Steam and launch Schedule I from there."))
}

/// "Leave & close": the game quits the normal way, so the client mod saves and disconnects first.
#[tauri::command]
fn quit_game() -> Result<(), String> {
    let windows = game::request_close()?;
    info!("Asked Schedule I to close ({windows} window{})", if windows == 1 { "" } else { "s" });
    Ok(())
}

/// The folder holding launcher.log, so a friend can send it over.
#[tauri::command]
fn open_launcher_logs(app: AppHandle) -> Result<(), String> {
    let dir = app.path().app_log_dir().map_err(|e| e.to_string())?;
    app.opener().open_path(dir.display().to_string(), None::<&str>).map_err(|e| e.to_string())
}

/// Takes the server mods and MelonLoader out of the game, then hands over to Hyland Link's own uninstaller
/// (which asks for confirmation itself) and closes the launcher.
#[tauri::command]
fn uninstall_launcher(app: AppHandle, shared: State<Shared>) -> Result<(), String> {
    let uninstaller = std::env::current_exe()
        .map_err(|e| e.to_string())?
        .parent()
        .map(|dir| dir.join("uninstall.exe"))
        .filter(|path| path.is_file())
        .ok_or("This copy of Hyland Link wasn't installed with the setup, so there's no uninstaller to run.")?;
    let manifest = manifest(&shared);
    let game = detect_game(&app, &manifest);
    if let Some(path) = &game.path {
        if game.running {
            return Err("Schedule I is running. Close the game first, then try again.".into());
        }
        install::uninstall(&PathBuf::from(path), &manifest, true)?;
        info!("Removed the server mods and MelonLoader before uninstalling");
    }
    info!("Starting the Hyland Link uninstaller");
    std::process::Command::new(&uninstaller).spawn().map_err(|e| format!("Couldn't start the uninstaller: {e}"))?;
    app.exit(0);
    Ok(())
}

/// New lines of MelonLoader's Latest.log since `offset`, for the in-app game log.
#[tauri::command]
async fn read_game_log(shared: State<'_, Shared>, run: Option<String>, offset: u64) -> Result<game_log::LogChunk, String> {
    let Some(dir) = shared.game.lock().expect("game lock").0.clone() else { return Ok(game_log::LogChunk::default()) };
    tauri::async_runtime::spawn_blocking(move || game_log::read(&PathBuf::from(dir), run.as_deref(), offset))
        .await
        .map_err(|e| e.to_string())?
}

/// Accepts a hand-picked folder only if it really holds the game.
#[tauri::command]
fn set_game_path(app: AppHandle, path: String) -> Result<(), String> {
    let dir = PathBuf::from(&path);
    if !game::is_game_dir(&dir) {
        return Err(format!("That folder doesn't have “{}” in it. Pick the folder the game is installed in.", game::EXE_NAME));
    }
    info!("Game folder picked by hand: {path}");
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
    if let Some(u) = &update {
        info!("Launcher update available: {}", u.version);
    }
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
    info!("Installing launcher {}", update.version);
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
    info!("Removing the server mods{}", if melonloader { " and MelonLoader" } else { "" });
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
        // %LOCALAPPDATA%\com.blazzer.hylandlink\logs\launcher.log: what the launcher did, for debugging a friend's PC.
        .plugin(
            tauri_plugin_log::Builder::new()
                .clear_targets()
                .target(Target::new(TargetKind::LogDir { file_name: Some("launcher".into()) }))
                .target(Target::new(TargetKind::Stdout))
                .level(log::LevelFilter::Info)
                .max_file_size(1_000_000)
                .rotation_strategy(RotationStrategy::KeepOne)
                .timezone_strategy(TimezoneStrategy::UseLocal)
                .build(),
        )
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_updater::Builder::new().build())
        .manage(Shared {
            manifest: Mutex::new((manifest::baked(), Source::BuiltIn { reason: "checking for updates".into() })),
            game: Mutex::new((None, false)),
        })
        .setup(|app| {
            info!("Hyland Link {} started", app.package_info().version);
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
            quit_game,
            read_game_log,
            open_launcher_logs,
            uninstall_launcher,
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
