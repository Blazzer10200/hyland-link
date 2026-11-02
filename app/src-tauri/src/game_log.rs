//! MelonLoader's console window and its log. Play hides the console; the launcher shows Latest.log instead.

use std::fs::{self, File};
use std::io::{ErrorKind, Read, Seek, SeekFrom};
use std::path::Path;

use serde::Serialize;

/// A first read of a long log starts this far from its end.
const MAX_BACKLOG: u64 = 256 * 1024;

/// Sets `hide_console = true` in MelonLoader's UserData/Loader.cfg and leaves every other setting alone.
/// MelonLoader fills in the rest itself when the file is new. Returns whether the file changed.
pub fn hide_console(game: &Path) -> Result<bool, String> {
    let path = game.join("UserData").join("Loader.cfg");
    let current = match fs::read_to_string(&path) {
        Ok(text) => text,
        Err(e) if e.kind() == ErrorKind::NotFound => String::new(),
        Err(e) => return Err(format!("Couldn't read MelonLoader's settings: {e}")),
    };
    let Some(updated) = with_console_hidden(&current) else { return Ok(false) };
    fs::create_dir_all(path.parent().expect("Loader.cfg has a parent")).map_err(|e| e.to_string())?;
    fs::write(&path, updated).map_err(|e| format!("Couldn't update MelonLoader's settings: {e}"))?;
    Ok(true)
}

fn key_value(line: &str) -> Option<(&str, &str)> {
    let (key, value) = line.split_once('=')?;
    Some((key.trim(), value.trim()))
}

/// The config with the console hidden, or None when it already is.
fn with_console_hidden(cfg: &str) -> Option<String> {
    const SETTING: &str = "hide_console = true";
    let newline = if cfg.contains("\r\n") { "\r\n" } else { "\n" };
    let mut lines: Vec<String> = Vec::new();
    let mut section = "";
    let mut header: Option<usize> = None;
    let mut found = false;
    for line in cfg.lines() {
        let trimmed = line.trim();
        if trimmed.starts_with('[') {
            section = trimmed;
            if section == "[console]" {
                header = Some(lines.len());
            }
        } else if section == "[console]" && key_value(trimmed).is_some_and(|(key, _)| key == "hide_console") {
            if key_value(trimmed).is_some_and(|(_, value)| value.starts_with("true")) {
                return None;
            }
            found = true;
            lines.push(SETTING.into());
            continue;
        }
        lines.push(line.into());
    }
    if !found {
        match header {
            Some(i) => lines.insert(i + 1, SETTING.into()),
            None => {
                if lines.last().is_some_and(|l| !l.trim().is_empty()) {
                    lines.push(String::new());
                }
                lines.extend(["[console]".into(), SETTING.into()]);
            }
        }
    }
    Some(lines.join(newline) + newline)
}

#[derive(Debug, Default, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LogChunk {
    /// The log's first line. It carries the launch time, so a new launch reads as a new log.
    pub run: Option<String>,
    /// Where the next read picks up.
    pub offset: u64,
    /// These lines start a different log than the one asked about: drop what you had.
    pub fresh: bool,
    pub lines: Vec<String>,
}

/// The complete lines of MelonLoader/Latest.log after `offset`, as long as `run` is still the log on disk.
/// A half-written last line waits for the next read.
pub fn read(game: &Path, run: Option<&str>, offset: u64) -> Result<LogChunk, String> {
    let failed = |e: std::io::Error| format!("Couldn't read the game log: {e}");
    let mut file = match File::open(game.join("MelonLoader").join("Latest.log")) {
        Ok(file) => file,
        Err(e) if e.kind() == ErrorKind::NotFound => return Ok(LogChunk { fresh: run.is_some(), ..LogChunk::default() }),
        Err(e) => return Err(failed(e)),
    };
    let len = file.metadata().map_err(failed)?.len();

    let mut head = Vec::new();
    file.by_ref().take(512).read_to_end(&mut head).map_err(failed)?;
    let current = String::from_utf8_lossy(&head).lines().map(str::trim).find(|l| !l.is_empty()).map(String::from);

    let fresh = current.as_deref() != run || len < offset;
    let start = match (fresh, len > MAX_BACKLOG) {
        (true, true) => len - MAX_BACKLOG,
        (true, false) => 0,
        (false, _) => offset,
    };
    file.seek(SeekFrom::Start(start)).map_err(failed)?;
    let mut bytes = Vec::new();
    file.take(len - start).read_to_end(&mut bytes).map_err(failed)?;

    let complete = bytes.iter().rposition(|&b| b == b'\n').map_or(0, |i| i + 1);
    let mut text = &bytes[..complete];
    // Starting mid-file lands mid-line; skip to the next whole one.
    if start > 0 && fresh {
        text = text.iter().position(|&b| b == b'\n').map_or(&[][..], |i| &text[i + 1..]);
    }
    let lines = String::from_utf8_lossy(text).lines().map(str::trim_end).filter(|l| !l.is_empty()).map(String::from).collect();
    Ok(LogChunk { run: current, offset: start + complete as u64, fresh, lines })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    #[test]
    fn hides_the_console_and_keeps_everything_else() {
        let shipped = "[loader]\ndisable = false\n\n[console]\nhide_warnings = false\nhide_console = false\nconsole_on_top = false\n";
        let hidden = with_console_hidden(shipped).unwrap();
        assert_eq!(hidden, shipped.replace("hide_console = false", "hide_console = true"));
        assert_eq!(with_console_hidden(&hidden), None, "already hidden: nothing to write");

        assert_eq!(with_console_hidden("").unwrap(), "[console]\nhide_console = true\n");
        assert_eq!(with_console_hidden("[loader]\ndisable = false\n").unwrap(), "[loader]\ndisable = false\n\n[console]\nhide_console = true\n");
        assert_eq!(with_console_hidden("[console]\nconsole_on_top = false\n").unwrap(), "[console]\nhide_console = true\nconsole_on_top = false\n");
        // Only the [console] key counts, and Windows line endings survive.
        let other = "[loader]\r\nhide_console = true\r\n[console]\r\nhide_console = false\r\n";
        assert_eq!(with_console_hidden(other).unwrap(), "[loader]\r\nhide_console = true\r\n[console]\r\nhide_console = true\r\n");
    }

    #[test]
    fn follows_the_log_across_writes_and_launches() {
        let game = std::env::temp_dir().join(format!("hyland-link-log-{}", std::process::id()));
        let _ = fs::remove_dir_all(&game);
        fs::create_dir_all(game.join("MelonLoader")).unwrap();
        let log = game.join("MelonLoader").join("Latest.log");

        assert!(!read(&game, None, 0).unwrap().fresh, "no log yet, nothing to drop");

        fs::write(&log, "\n[02:34:53.352] ---\n[02:34:53.355] MelonLoader v0.7.2\n\n[02:34:54.662] Loading Mo").unwrap();
        let first = read(&game, None, 0).unwrap();
        assert!(first.fresh);
        assert_eq!(first.run.as_deref(), Some("[02:34:53.352] ---"));
        assert_eq!(first.lines, ["[02:34:53.352] ---", "[02:34:53.355] MelonLoader v0.7.2"], "the half line waits");

        fs::OpenOptions::new().append(true).open(&log).unwrap().write_all(b"ds...\r\n").unwrap();
        let next = read(&game, first.run.as_deref(), first.offset).unwrap();
        assert!(!next.fresh);
        assert_eq!(next.lines, ["[02:34:54.662] Loading Mods..."]);
        assert!(read(&game, next.run.as_deref(), next.offset).unwrap().lines.is_empty());

        fs::write(&log, "\n[02:40:00.001] ---\n[02:40:00.002] MelonLoader v0.7.2\n").unwrap();
        let relaunch = read(&game, next.run.as_deref(), next.offset).unwrap();
        assert!(relaunch.fresh, "a new launch is a new log");
        assert_eq!(relaunch.lines.len(), 2);
        fs::remove_dir_all(&game).unwrap();
    }
}
