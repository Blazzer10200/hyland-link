//! Finding the Schedule I install through Steam, its beta branch, and whether it's running.

use std::fs;
use std::path::{Path, PathBuf};

use serde::Serialize;
use winreg::RegKey;
use winreg::enums::HKEY_CURRENT_USER;

pub const EXE_NAME: &str = "Schedule I.exe";
const INSTALL_DIR_NAME: &str = "Schedule I";

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GameInfo {
    pub found: bool,
    pub path: Option<String>,
    /// Steam beta branch; `None` = the normal branch.
    pub branch: Option<String>,
    pub running: bool,
}

pub fn is_game_dir(dir: &Path) -> bool {
    dir.join(EXE_NAME).is_file()
}

fn steam_path() -> Option<PathBuf> {
    let key = RegKey::predef(HKEY_CURRENT_USER).open_subkey(r"Software\Valve\Steam").ok()?;
    let path: String = key.get_value("SteamPath").ok()?;
    // Steam stores "c:/program files (x86)/steam"; show it the way Explorer does.
    let mut path = path.replace('/', "\\");
    if path.as_bytes().get(1) == Some(&b':') {
        path[..1].make_ascii_uppercase();
    }
    Some(PathBuf::from(path))
}

/// Every Steam library folder listed in libraryfolders.vdf, plus Steam's own folder.
fn library_folders(steam: &Path) -> Vec<PathBuf> {
    let mut libraries = vec![steam.to_path_buf()];
    if let Ok(vdf) = fs::read_to_string(steam.join(r"steamapps\libraryfolders.vdf")) {
        for line in vdf.lines() {
            let mut parts = line.split('"').filter(|p| !p.trim().is_empty());
            if parts.next() == Some("path")
                && let Some(value) = parts.next()
            {
                libraries.push(PathBuf::from(value.replace(r"\\", r"\")));
            }
        }
    }
    libraries.dedup();
    libraries
}

/// Game folder and its Steam app manifest (.acf), searching every Steam library.
fn find_through_steam(app_id: u32) -> Option<(PathBuf, PathBuf)> {
    let steam = steam_path()?;
    library_folders(&steam).into_iter().find_map(|library| {
        let game = library.join("steamapps").join("common").join(INSTALL_DIR_NAME);
        is_game_dir(&game).then(|| (game, library.join("steamapps").join(format!("appmanifest_{app_id}.acf"))))
    })
}

/// The `BetaKey` Steam records for the install, if any. Empty or "public" means the normal branch.
fn beta_branch(acf: &Path) -> Option<String> {
    let text = fs::read_to_string(acf).ok()?;
    text.lines().find_map(|line| {
        let parts: Vec<&str> = line.split('"').filter(|p| !p.trim().is_empty()).collect();
        match parts.as_slice() {
            ["BetaKey", value] if !value.is_empty() && *value != "public" => Some(value.to_string()),
            _ => None,
        }
    })
}

pub fn detect(app_id: u32, manual: Option<&Path>) -> GameInfo {
    let running = is_running();
    if let Some(dir) = manual.filter(|d| is_game_dir(d)) {
        // A hand-picked folder may still be a Steam library; look for its manifest two levels up.
        let acf = dir.parent().and_then(Path::parent).map(|steamapps| steamapps.join(format!("appmanifest_{app_id}.acf")));
        return GameInfo {
            found: true,
            path: Some(dir.display().to_string()),
            branch: acf.as_deref().and_then(beta_branch),
            running,
        };
    }
    match find_through_steam(app_id) {
        Some((dir, acf)) => GameInfo {
            found: true,
            path: Some(dir.display().to_string()),
            branch: beta_branch(&acf),
            running,
        },
        None => GameInfo { found: false, path: None, branch: None, running },
    }
}

/// Process ids of every "Schedule I.exe" (ToolHelp snapshot; no console window, no extra crates).
fn game_pids() -> Vec<u32> {
    use windows_sys::Win32::Foundation::{CloseHandle, INVALID_HANDLE_VALUE};
    use windows_sys::Win32::System::Diagnostics::ToolHelp::{
        CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
    };

    let target = EXE_NAME.to_lowercase();
    let mut pids = Vec::new();
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snapshot == INVALID_HANDLE_VALUE {
            return pids;
        }
        let mut entry: PROCESSENTRY32W = std::mem::zeroed();
        entry.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut ok = Process32FirstW(snapshot, &mut entry) != 0;
        while ok {
            let len = entry.szExeFile.iter().position(|&c| c == 0).unwrap_or(entry.szExeFile.len());
            if String::from_utf16_lossy(&entry.szExeFile[..len]).to_lowercase() == target && is_alive(entry.th32ProcessID) {
                pids.push(entry.th32ProcessID);
            }
            ok = Process32NextW(snapshot, &mut entry) != 0;
        }
        CloseHandle(snapshot);
    }
    pids
}

/// A game that has exited stays in the snapshot (no threads, no memory) while anything still holds a handle to it,
/// e.g. Steam, so only count one that hasn't signaled its exit. If Windows won't let us look, assume it's alive.
fn is_alive(pid: u32) -> bool {
    use windows_sys::Win32::Foundation::{CloseHandle, WAIT_OBJECT_0};
    use windows_sys::Win32::System::Threading::{OpenProcess, PROCESS_SYNCHRONIZE, WaitForSingleObject};

    unsafe {
        let process = OpenProcess(PROCESS_SYNCHRONIZE, 0, pid);
        if process.is_null() {
            return true;
        }
        let exited = WaitForSingleObject(process, 0) == WAIT_OBJECT_0;
        CloseHandle(process);
        !exited
    }
}

pub fn is_running() -> bool {
    !game_pids().is_empty()
}

/// Asks the game to quit the way the window's X button does (WM_CLOSE to its Unity window), so Unity runs its normal
/// shutdown and the client mod saves and disconnects. Never kills the process. Returns the windows asked.
pub fn request_close() -> Result<usize, String> {
    use windows_sys::Win32::Foundation::{HWND, LPARAM};
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        EnumWindows, GetClassNameW, GetWindowThreadProcessId, PostMessageW, WM_CLOSE,
    };

    struct Search {
        pids: Vec<u32>,
        windows: Vec<HWND>,
    }
    // Only Unity's own window: closing a MelonLoader console window would end the game without its shutdown.
    unsafe extern "system" fn visit(hwnd: HWND, data: LPARAM) -> i32 {
        let search = unsafe { &mut *(data as *mut Search) };
        let mut pid = 0u32;
        unsafe { GetWindowThreadProcessId(hwnd, &mut pid) };
        if search.pids.contains(&pid) {
            let mut class = [0u16; 64];
            let len = unsafe { GetClassNameW(hwnd, class.as_mut_ptr(), class.len() as i32) };
            if String::from_utf16_lossy(&class[..len.max(0) as usize]) == "UnityWndClass" {
                search.windows.push(hwnd);
            }
        }
        1
    }

    let pids = game_pids();
    if pids.is_empty() {
        return Err("Schedule I isn't running.".into());
    }
    let mut search = Search { pids, windows: Vec::new() };
    unsafe { EnumWindows(Some(visit), (&raw mut search) as LPARAM) };
    if search.windows.is_empty() {
        return Err("I couldn't find the game's window. Quit from the game's own menu.".into());
    }
    let asked = search.windows.iter().filter(|&&hwnd| unsafe { PostMessageW(hwnd, WM_CLOSE, 0, 0) } != 0).count();
    if asked == 0 {
        return Err("Windows didn't let me close the game (is it running as administrator?). Quit from its menu.".into());
    }
    Ok(asked)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_dir(name: &str) -> PathBuf {
        let dir = std::env::temp_dir().join(format!("hyland-link-test-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&dir);
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn reads_beta_branch_from_app_manifest() {
        let dir = temp_dir("acf");
        let acf = dir.join("appmanifest_3164500.acf");
        fs::write(&acf, "\"AppState\"\n{\n\t\"UserConfig\"\n\t{\n\t\t\"BetaKey\"\t\t\"alternate\"\n\t}\n}\n").unwrap();
        assert_eq!(beta_branch(&acf).as_deref(), Some("alternate"));
        fs::write(&acf, "\"AppState\"\n{\n\t\"buildid\"\t\t\"25796331\"\n}\n").unwrap();
        assert_eq!(beta_branch(&acf), None);
        fs::write(&acf, "\t\t\"BetaKey\"\t\t\"public\"\n").unwrap();
        assert_eq!(beta_branch(&acf), None);
    }

    #[test]
    fn lists_every_steam_library() {
        let steam = temp_dir("vdf");
        fs::create_dir_all(steam.join("steamapps")).unwrap();
        fs::write(
            steam.join(r"steamapps\libraryfolders.vdf"),
            "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\Program Files (x86)\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\SteamLibrary\"\n\t}\n}\n",
        )
        .unwrap();
        let libraries = library_folders(&steam);
        assert!(libraries.contains(&PathBuf::from(r"C:\Program Files (x86)\Steam")));
        assert!(libraries.contains(&PathBuf::from(r"D:\SteamLibrary")));
    }

    #[test]
    fn an_exited_process_still_held_open_is_not_alive() {
        // The Child keeps its handle after wait(), the same husk Steam leaves behind after the game quits.
        let mut done = std::process::Command::new("cmd").args(["/c", "exit 0"]).spawn().unwrap();
        done.wait().unwrap();
        assert!(!is_alive(done.id()));

        let mut busy = std::process::Command::new("ping").args(["-n", "30", "127.0.0.1"]).stdout(std::process::Stdio::null()).spawn().unwrap();
        let alive = is_alive(busy.id());
        busy.kill().unwrap();
        busy.wait().unwrap();
        assert!(alive);
    }
}
