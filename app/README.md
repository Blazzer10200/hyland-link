# Hyland Link

The friend launcher for Blazzer's Schedule I server. It walks a friend through four steps on one screen, with no
scrolling: **Find Schedule I → Install the server mod → Launch once to finish setup → Join the server**. Done steps
collapse to a check and a one-line summary. Only the current step opens up, and one big button at the bottom always
does the next thing.

## What it does

| Step | How |
|---|---|
| Find Schedule I | Steam's registry key + every library in `libraryfolders.vdf`; manual folder pick as fallback |
| Check the Steam branch | `BetaKey` in `appmanifest_3164500.acf` must be empty or `public` (the IL2CPP server needs the normal branch) |
| Install MelonLoader | `MelonLoader.x64.zip` extracted into the game folder; SHA-256 checked before anything is written |
| Install the server mod | Only `Mods/DedicatedServerMod_Il2cpp_Client.dll` from the release zip, checked by zip hash and DLL hash; removes the Mono client DLL |
| Install the server's extra mods | Every `mods[]` entry whose `side` isn't `server`: zip hash, then each listed file's hash, before anything is written; only into `Mods/ Plugins/ UserLibs/ UserData/`. Updates drop files an older version shipped; mods the server dropped are removed file by file from the marker |
| Follow the server | Re-reads the online manifest every 5 min, on window focus and right before Play; a change turns Play into Update |
| Add the server to Favorites | Edits the mod's own `UserData/DedicatedServerClientServers.json`, keeps every existing entry and field, and never rewrites a file it can't parse |
| Live status | The mod's status query: TCP to the game port, `DS_STATUS`, one JSON line back |
| Play | `steam://rungameid/3164500` |
| Update itself | `tauri-plugin-updater` on start: reads `latest.json` from the newest GitHub release, verifies the minisign signature against the pubkey in `tauri.conf.json`, runs the NSIS installer in passive mode, which closes and reopens the app. If that fails, a toast links to the Releases page |

What's installed is decided by `../manifest/manifest.json`. It's compiled in, and the online copy wins when its
`revision` is higher. The launcher records what it installed, file by file, in `UserData/HylandLink.json` inside the
game folder. Removing the mods from Settings (the gear) removes only those files and leaves the player's own mods and
`UserData` alone. "Uninstall Hyland Link" does that plus MelonLoader, then runs the launcher's own uninstaller.

## Develop

```bash
pnpm install
pnpm dev                      # UI in a browser with a fake backend: ?s=setup|update|mods|nomods|installing|closegame|firstlaunch|firstrun|ready|playing|logerrors|offline|branch|nogame|selfupdate
pnpm tauri dev                # the real app
pnpm check                    # svelte-check
cd src-tauri && cargo test    # unit tests; add `-- --ignored` for the real download + install into a temp folder
```

The browser mock (`src/lib/dev-mock.ts`) loads only under `vite dev` outside Tauri, and release builds drop it.

## Layout

- `src-tauri/src/manifest.rs`: the version file (built-in copy, online fetch, validation, schema 2)
- `src-tauri/src/game.rs`: Steam library search, beta branch, and whether the game is running (ToolHelp)
- `src-tauri/src/install.rs`: download, checksum, extract, state detection (incl. extra and stale mods), uninstall
- `src-tauri/src/favorites.rs`: the mod's saved-servers file
- `src-tauri/src/status.rs`: the `DS_STATUS` query
- `src-tauri/src/game_log.rs`: hides MelonLoader's console (`UserData/Loader.cfg`) and follows `MelonLoader/Latest.log`
- `src/lib/launcher.svelte.ts`: app state, the phase machine (noGame → branch → setup/update → installing →
  firstLaunch → ready → playing), which of the 4 steps is current, the manifest rechecks and the game log poll
- `src/lib/game-log.ts`: log line parsing and the game's stage (loading, main menu, connecting, on the server)
- `src/lib/components/`: TitleBar (with the settings gear), ServerCard (with SignalBars), Steps (the guided stepper),
  GameStatus (the live stage), ActionBar (the one button: Install / Play / Leave & close), Sheet with ModsSheet (the
  server's mod list), LogSheet (the game log) and SettingsSheet (folder, logs, removal, uninstall)

The launcher writes its own log to `%LOCALAPPDATA%\com.blazzer.hylandlink\logs\launcher.log` (1 MB, one file kept).
