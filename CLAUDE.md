# Hyland Link: project rules

Friend launcher for Blazzer's Schedule I server (server tooling lives in `projects/schedule1-server`, private).
This repo is **public** (`Blazzer10200/hyland-link`), because the updater and the manifest are fetched anonymously
from it. Released launchers are installed on friends' PCs and update themselves from `releases/latest`.

State and next steps: `HANDOFF.md`. Maintainer flow (shipping mod/launcher updates): `README.md`.

## Map
- `app/src-tauri/src/lib.rs`: every Tauri command (state, install, play, quit game, game log, status, updater,
  uninstall incl. running the NSIS `uninstall.exe` next to the exe) + plugin setup.
- `app/src-tauri/src/game.rs` `request_close`: "Leave & close" posts WM_CLOSE to the game's windows, the same as its X
  button, so the client mod saves and disconnects. It never kills the process.
- `app/src-tauri/src/{game,install,favorites,status,manifest}.rs`: Steam detection, install/uninstall (incl. extra
  mods, tracked per file in `UserData/HylandLink.json`), the mod's favorites file, the `DS_STATUS` query, the version
  file (built-in vs online by `revision`, schema 2).
- `app/src-tauri/src/game_log.rs`: Play sets `hide_console = true` in `UserData/Loader.cfg` (no MelonLoader console
  window); `read_game_log` follows `MelonLoader/Latest.log` by offset, and a new run id (its first line) means a relaunch.
- `app/src/lib/launcher.svelte.ts`: the whole UI state machine (phases, current step, self-update, mod-list rechecks
  every 5 min / on focus / before Play, the game log poll every 800 ms while the game runs or the log is open).
- `app/src/lib/game-log.ts`: parses log lines and reads the game's stage off them (starting → preparing → loading → menu
  → connecting → joined → closing). The client-mod strings it matches come from DedicatedServerClient 1.1.6: recheck on a mod update.
- `app/src/lib/components/`: `Steps` (the stepper), `ActionBar` (the one big button; two-click "Leave server & close
  game" while playing), `GameStatus` (live stage + newest log line), `Sheet` (shared bottom sheet, one open at a time)
  under `ModsSheet` (the server's mod list), `LogSheet` (the game log) and `SettingsSheet` (the gear: game folder,
  logs, two-click removals and "Uninstall Hyland Link"), `ServerCard`, `TitleBar` (gear, minimize, close).
- `app/src/routes/+page.svelte`: layout, toasts, the self-update overlay. `app/src/app.css`: design tokens.
- `app/src/lib/dev-mock.ts`: fake backend for the browser preview (`.claude/launch.json` → `hyland-link-ui`, port 1420,
  viewport 460x720). `?s=firstrun|playing|logerrors` stream a fake game log.
- Debugging a PC: `bash scripts/logs.sh [lines]` prints the launcher's own log
  (`%LOCALAPPDATA%\com.blazzer.hylandlink\logs\launcher.log`: game found/started/closed, mod list changes, installs,
  Play, game stages, every error shown to the player), the game's `Latest.log` with error counts, and `hide_console`.
- `mods/minimap/`: Hyland Minimap, our fork of nacho's Schedule I Minimap (MIT, decompiled; see its README). Players get
  it as `hyland-minimap` (client side) from a `minimap-v<version>` release. To ship a change: bump `src/AssemblyInfo.cs`,
  `dotnet build -c Release mods/minimap`, zip `Mods/HylandMinimap.dll` + LICENSE.md with **forward-slash** entry paths
  (pwsh `ZipFile.CreateFromDirectory`, not 5.1's Compress-Archive), then `gh release create minimap-v<version> ...
  --latest=false` (**never latest**: the launcher updater reads `releases/latest`), `mods.ps1 add <asset URL> -Id
  hyland-minimap ...` and `publish`.
- `mods/hud/`: Hyland HUD, our restyle of the game's hotbar, compass and money (Blazzer's "Glass Rail" concept). It
  restyles the game's own HUD objects in place and restores them on disable or error. Not shipped yet; ships like the
  minimap (`hud-v<version>`, id `hyland-hud`). `mods/hud/preview` renders its sprites to PNG without the game.
- `manifest/manifest.json`: what friends install and what the server runs. `scripts/mods.ps1`: the only way to change
  mods (server applies first, then push). `scripts/release.sh`: the only way to ship a launcher build.

## Never break
- **Updater key:** `C:/AI Workflow/.secrets/hyland-link-updater.key` (minisign, empty password). Its pubkey is in
  `app/src-tauri/tauri.conf.json` → `plugins.updater.pubkey`. Never regenerate or rotate casually: installed launchers
  only accept updates signed by this key.
- **Signing env trap:** `.secrets/env.sh` exports `TAURI_SIGNING_PRIVATE_KEY` for another app (rift). Always build
  releases through `scripts/release.sh`, which overrides it with this app's key. A release signed with the wrong key
  fails verification on every friend's PC.
- **Versions:** `app/package.json`, `app/src-tauri/Cargo.toml` and `tauri.conf.json` must match. The script refuses a
  mismatch.
- **One rule set, three places:** manifest validation (allowed hosts, `Mods/ Plugins/ UserLibs/ UserData/` paths,
  ids, hashes) is mirrored in `manifest.rs`, `scripts/mods.ps1` and schedule1-server's `s1ds-mods`. Change one,
  change all three, or the server and players can disagree about the same file.
- **Mods flow one way:** server first, then `main`. Never push a manifest whose mods the server doesn't run yet.
- **Released launchers read the online manifest:** v0.1.0 needs schema 2 and requires `game.version` and
  `launcher.version` even though nothing reads them now. Only add fields (with `#[serde(default)]`); never drop or
  rename one, and never bump `schema` without a launcher release that understands it first.
- **Public repo hygiene:** nothing from `schedule1-server` goes in here (the ntfy topic lives in its scripts). The
  server IP in `manifest/manifest.json` is public on purpose.
- **"Live" means a launcher read it:** raw.githubusercontent.com caches the manifest up to 5 min per edge server, so
  one curl seeing the new revision proves nothing about a player's edge. Before telling Blazzer a mod list is live, the
  launcher log (`bash scripts/logs.sh`) must show `Mod list: revision N from Online`, or 5 min must have passed since
  the push. A query string doesn't help: the CDN ignores it (random `?t=` still returns `X-Cache: HIT`).
- **Releases move only when published:** a draft doesn't update `releases/latest`. The asset is always named
  `Hyland-Link-Setup.exe`, so the friend download link never changes.
- Don't install the mod into Blazzer's real game folder to test. That copy is used for co-op. Use the browser mock
  (`pnpm --dir app dev`, `?s=<state>`) or `cargo test -- --ignored` (installs into a temp folder).

## Build
- `CARGO_TARGET_DIR=C:\cargo-targets` on this PC, so bundles land in `C:\cargo-targets\release\bundle\nsis\`.
- Checks: `pnpm --dir app check`, `cd app/src-tauri && cargo test`.
- See the real app (not the mock): start `C:\cargo-targets\release\hyland-link.exe` with
  `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`, then `node scripts/read-app.mjs 9333` prints
  its screen text. Read-only: it never clicks, so nothing gets installed.
