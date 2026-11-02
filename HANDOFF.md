---
title: Hyland Link handoff
tags: [project, handoff]
status: released
updated: 2026-11-02
---

# Hyland Link: handoff

## Session 7 — 2026-11-02 06:30 (full review: launcher 0.1.3 + minimap 1.2.1 live, server hardened)

### Completed
- **Game code access:** the Mono branch (`alternate`, build 25796351) decompiled to `.gameref/src/` (gitignored,
  2,331 files, never public). IL2CPP interop DLLs give names/signatures only; `.gameref` has the logic.
- **Full review** (5 fresh-eyes agents: backend, UI, scripts, minimap, server); every finding re-checked in code.
- **Launcher 0.1.3 released** (`v0.1.3`, latest; feed verified anonymously, key id `c565f1e2370aae54`, download =
  build). Real app run before publishing: v0.1.3, "Mod list: revision 7 from Online", server online, 1 update shown.
  Fixes: a failed recheck keeps the online list (0.1.2 fell back to built-in r5 = MelonLoader 0.7.2 and asked to
  "update" back); extra mods re-checked after removals and files a current mod ships never deleted; download timeouts
  (15 s connect / 30 s first byte / 15 min); damaged Favorites file kept as `.unreadable-<time>`, Play re-adds the
  favorite, step 2 no longer needs it; install/uninstall busy lock, uninstall off the main thread; manifest paths
  stricter (server-mod paths, launcher-owned files reserved, same rules in s1ds-mods); UI: removals locked while
  installing, "Close Schedule I to update" button, notices clear after Update, per-text notice dismissal, stalled
  self-update → manual link, error count from tagged lines only, offline update check logged not shown.
- **Minimap 1.2.1** (`minimap-v1.2.1`, not latest; mod list **r7**, published 06:12, server applied, 5 min passed):
  skips customers without NPC data (one could take the whole map down), no zoom while `GameInput.IsTyping`, NaN-safe
  heading, retry backoff 5→60 s, version from `MelonInfo`.
- Scripts: `release.sh --publish` refuses a dirty tree, HEAD ≠ origin/main, or a version below the latest release;
  `mods.ps1 publish` refuses unsynced main before touching the server and restores the manifest on Ctrl+C;
  `logs.sh` fallbacks print. Checks: cargo test 18/18 incl. the real-download install, clippy 0, svelte-check 0/0,
  minimap build 0 warnings.

### Not done / not verified
- Minimap 1.2.1 compiled against the game's interop but **not run in game** (rule: no installs into Blazzer's game).
- Friends' self-update to 0.1.3 happens on their next launcher start; not observed yet.
- Deliberately skipped minimap perf nits (full re-apply of marker looks every 1 s, full `Apply` on each zoom key,
  per-frame Il2Cpp wrapper allocations): reworking working code for small gains.
- HUD redesign parked by Blazzer (canvas "Hyland HUD", main board published; compass/hotbar boards not written).

### Next Steps
1. Blazzer: open the launcher → it self-updates to 0.1.3 → Update (minimap 1.2.1) → play; `bash scripts/logs.sh`.
2. Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (Blazzer).
3. Leftovers to OK deleting: `%TEMP%\hlmm-1.2.0`, `%TEMP%\hlmm-1.2.1`, `%TEMP%\hl-verify-013`,
   `%TEMP%\hl-test-webview`; server ones in the schedule1-server HANDOFF.

## Session 6 — 2026-11-02 05:40 (Minimap 1.2.0, launcher 0.1.2, MelonLoader 0.7.3 everywhere; all verified on Blazzer's PC)

### Completed
- **Hyland Minimap 1.2.0** (`minimap-v1.2.0`, not latest, mod list r5): rotate with view on for everyone (hidden
  `SettingsVersion` entry flips `Rotate` once, `Settings.cs` `Migrate`), heading eased with `MapMath.SmoothAngle`
  (wraps ±180, frame-rate independent, `MinimapHud.cs` `HeadingSmoothing = 14`), `MinimumZoom` 0.25 → 0.6.
- **Launcher 0.1.2 released**: `game.rs` `is_alive` (WaitForSingleObject on SYNCHRONIZE) so an exited game that
  something still holds open no longer counts as running (0.1.1 showed "close the game" and refused Update). Test
  `an_exited_process_still_held_open_is_not_alive`. Feed 0.1.2, key id `c565f1e2370aae54`, download = build.
- **MelonLoader 0.7.3 everywhere** (mod list r6). Players: manifest only (released launchers upgrade when the version
  differs; 0.7.3 zip = 0.7.2 files + `net472/`, so overlay leaves nothing stale). Server: see schedule1-server HANDOFF
  (manifest's ML is now mounted over the image's baked 0.7.2). Install test covers upgrading over 0.7.2 (16/16 with
  `--include-ignored`).
- **Verified on Blazzer's PC, not just in theory:** 05:08 self-update 0.1.1 → 0.1.2 + minimap 1.2.0 installed; 05:38
  launcher read r6, installed ML 0.7.3; game log `MelonLoader v0.7.3`, Hyland Minimap 1.2.0 + client mod loaded,
  0 errors, connected. Server log 0.7.3, 0 errors.

### Failed / Don't Retry
- **I called r5 and r6 "live" while Blazzer's launcher still got the old revision** (twice). raw.githubusercontent
  caches per edge for 300 s, and **query strings don't bypass it** (random `?t=` → `X-Cache: HIT`). One curl proves
  nothing. Fixed: `mods.ps1` `Wait-RawManifest` says live only 5 min after the push; CLAUDE.md rule.
- The GitHub contents API would skip the cache but allows 60 req/h per IP: rejected for the launcher.

### Hooks & Gotchas
- **Blazzer was angry about stalling and premature "done".** Prove done with his launcher log
  (`Mod list: revision N from Online`, `Install finished`) and game log, then say it once. Don't re-ask approved things.
- `minimap-v1.2.0`'s tag points at `6058095` (pre-1.2.0 source): the release was created before the push. Zip is right.
- MelonLoader #1181 (no load on Win 11 25H2): compat mode loads the system `version.dll`; fix = rename to
  `winhttp.dll`. Not 0.7.3-specific. First launch after an ML bump regenerates Il2CppAssemblies (slow).
- My guard hook blocks `rm` on named paths and any `> .env` write, even in scratch dirs: use `--env-file` / `cp`.

### Next Steps
1. Push `main`: `2ae9cb3` (publish wait + CLAUDE.md rule) is local only.
2. Ask Blazzer how rotate-with-view feels in game (smoothing 14/s is a guess).
3. Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (Blazzer).
4. Leftovers for Blazzer to OK deleting: `%TEMP%\hlmm-1.2.0`, plus the server-side temp folders listed in the
   schedule1-server HANDOFF.

### Files Modified
- `mods/minimap/src/{Settings,MapMath,MinimapHud,MinimapMod,AssemblyInfo}.cs`, `mods/minimap/README.md`
- `app/src-tauri/src/{game,install}.rs`, `app/src-tauri/Cargo.toml` (Win32_System_Threading, 0.1.2), versions
- `manifest/manifest.json` (r5, r6), `scripts/mods.ps1` (`Wait-RawManifest`), `CLAUDE.md` ("Live" rule)

---

## Session 5 — 2026-11-02 04:55 (Hyland Minimap live as mod list r4; next: rotate with view)

### Completed
- Server world = Blazzer's co-op save **PTO RETIRED** (details in schedule1-server HANDOFF). Fixed `s1ds import` first:
  it merged instead of replacing.
- **Hyland Minimap** (`mods/minimap/`): fork of nacho's Schedule I Minimap 1.0.2 (MIT, ILSpy decompile, baseline commit
  `bdee77d`). Bezel design picked from in-chat previews: smooth rim over the Mask's stencil edge, supersampled +
  mipmapped sprites, zoom by map-art pixels, map grading, vignette, view cone, N badge, edge pins (active deals,
  dealers), marker outlines. Released as `minimap-v1.1.0` (**not latest**), shipped as `hyland-minimap` (client) in
  **mod list revision 4**. Blazzer: "looks really, really good" in game; log clean, 0 errors.
- First-launch numbers (Blazzer's PC): map art **2048×2048, Bilinear, no mipmaps**, drawn at 2048 units, HUD scale
  1.33 → zoom 1 = 0.75× the phone map. nachorn's default 3.0 was 4× the art: the blur.
- Bugs fixed on the way: `mods.ps1 -Blurb` ignored (function-scoped `$PSBoundParameters`); `s1ds mods apply` exited 1
  after success (EXIT trap read a function-local under `set -u`); 5.1 `Compress-Archive` writes `Mods\x.dll`.

### Key Decisions
- Fork, not wrap: no upstream source; MIT allows it. Own DLL/Melon name/prefs category, so it never collides.
- Mod releases live on hyland-link with `--latest=false`; checked after: `releases/latest` = v0.1.1, feed = 0.1.1.
- Design is previewed in chat first (`show_widget`), then built: Blazzer wants to see a design before code.

### Hooks & Gotchas
- **Changing a setting's default doesn't reach existing players**: MelonPreferences keeps saved values, and every
  player who launched r4 already has `[HylandMinimap] Rotate = false` saved. Flip a default with a new key or a
  one-time migration (a `SettingsVersion` entry), not by editing the default.
- Never edit `UserData/MelonPreferences.cfg` while the game runs: MelonLoader rewrites it on exit.
- Mod zips need forward-slash entries (pwsh `ZipFile.CreateFromDirectory`). Recipe is in CLAUDE.md.
- Preview renderer for the generated shapes: links `src/Shapes.cs` into a net8 console that writes PNGs (it lived
  in the session scratchpad; recreate under `mods/minimap/preview/` if the shapes change again).

### Next Steps
1. **Rotate with view, on for everyone** (Blazzer's ask: map turns as he turns). Already works:
   `Settings.cs:123` `Rotate`, used at `MinimapHud.cs:399-402` (map layer turns by `num2`, arrow/cone stay up, N badge
   follows via `MapMath.EdgePoint`). To ship it as the default: add a `SettingsVersion` entry; when it's below 2, set
   `Rotate = true` once and save. Then smooth the heading (lerp the angle a little each frame, wrapping at ±180°) so
   fast mouse turns don't snap. Bump to 1.2.0 and ship with the CLAUDE.md recipe (release `minimap-v1.2.0`, not
   latest; `mods.ps1 add … -Id hyland-minimap`; `publish`).
   Quick try without a build: with the game closed, set `Rotate = true` under `[HylandMinimap]` in
   `C:\Program Files (x86)\Steam\steamapps\common\Schedule I\UserData\MelonPreferences.cfg`.
2. Raise `MapMath.MinimumZoom` (`MapMath.cs:8`, 0.25) to about 0.6: the map art has no mipmaps, so it shimmers when
   zoomed out further.
3. Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (Blazzer).
4. Optional server housekeeping: see the schedule1-server HANDOFF.

### Files Modified
- `mods/minimap/**` (new), `manifest/manifest.json` (r3 nachorn → r4 hyland-minimap), `scripts/mods.ps1`,
  `app/src-tauri/src/install.rs` (test), CLAUDE.md, README.md; schedule1-server `s1ds` (import --delete, trap fix)

---

## Session 4 — 2026-11-02 04:01 (v0.1.1 released, Leave & close verified on the server)

### Completed
- **v0.1.1 released** (session 3's console hide, live game status + log, Settings gear, Leave & close, launcher log).
  Feed serves 0.1.1 anonymously, signature key id `c565f1e2370aae54` = pubkey, download bytes = build.
- **Self-update proven on this PC:** installed 0.1.0 started → updated → reopened as 0.1.1 (registry + launcher log),
  server online at 9 ms, mod list revision 2 from Online.
- Cleanup from a dead-code sweep: dropped the unused `ServerStatus.description` (Rust, `api.ts`, state, mock) and the
  unused npm `@tauri-apps/plugin-opener` (the Rust crate is used); docs synced (`closing` stage, step 2 title).
  The rest of the sweep came back clean: commands ↔ `api.ts` 1:1, no unused deps/capabilities/CSS/debug output.
- Router item settled: 38465 TCP (status ping) and UDP (Blazzer's join via NAT loopback) both pass the forward.
  The server's LAN address is static (details in the schedule1-server HANDOFF), so a router reservation is optional.
- Deleted `%TEMP%\hl-test-webview`. Checks: cargo test 14 passed, clippy 0, svelte-check 0/0.

### Key Decisions
- Kept in-file-only type exports and module-wide `pub` as they are: churn, not cleanup.
- Manifest `launcher.version` stays `0.1.0`: nothing reads it, and bumping means a revision push for no effect.

- **Leave & close while joined: passed** (03:59). Game had one `UnityWndClass` window and no console; WM_CLOSE →
  mod "Notifying client mods of shutdown" → "Preferences Saved!" → process gone in 2 s; server dropped to 0 players
  and kept running (same uptime).
- Blazzer's PC hard-froze ~1 min into an earlier join (Kernel-Power 41, bugcheck 0, no power button): hardware/driver
  class, not the launcher. A `hyland-link.exe` 0.1.0 `c0000409` at 03:18:01 predates the launcher log's first line
  (03:18:32), so it was a pre-log session-3 dev build; nothing ties it to shipped code. `request_close`'s callback
  can't panic.

- **First extra mod live: Schedule I Minimap 1.0.2** (nachorn, Thunderstore, `side: client`), mod list revision 3.
  It's compiled against MelonLoader 0.7.3 but has no `VerifyLoaderVersion`; all 32 MelonLoader APIs it calls exist in
  0.7.2 (checked by metadata against the game folder's `MelonLoader.dll`), and 0.7.2's resolver binds by name
  (`OverrideBaseAssembly`/`Fallback`). Server: no server-side extras, booted on 0.7.2, PTO RETIRED still loaded.
  Not yet seen in game. Runner-up if it misbehaves: CherryMods SmallCornerMap (ML 0.7.0, a year old).
- Fixed `mods.ps1 -Blurb` being ignored (function-scoped `$PSBoundParameters`). The real-download install test now
  also asserts every client-side extra mod lands and is removed by uninstall (15 passed).
- First publish failed **after** the server applied r3: `s1ds mods apply`'s EXIT trap read a function-local `$work`
  under `set -u`. Fixed in schedule1-server, redeployed, rerun clean → r3 pushed.

### Next Steps
1. Blazzer: launcher → Update → Play → minimap in the corner (PageUp/PageDown zoom). If it doesn't show,
   `bash scripts/logs.sh` (MelonLoader errors for ScheduleIMinimap).
2. Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (USB or password manager): Blazzer's call.
3. Optional: router DHCP reservation for .180 so nothing else gets handed that address.
   (The server world is now PTO RETIRED: see schedule1-server HANDOFF.)

### Files Modified
- `app/src-tauri/src/status.rs`, `app/src/lib/{api.ts,dev-mock.ts,launcher.svelte.ts}`, `app/package.json`,
  `app/pnpm-lock.yaml`, versions in `app/package.json` / `Cargo.toml` / `Cargo.lock` / `tauri.conf.json`, CLAUDE.md,
  app/README.md

---

## Session 3 — 2026-11-02 03:25 (built + real-app verified, not released)

### Completed
- Log review of Blazzer's first real join/leave: clean on client and server (join, 3 saves, clean leave). The game
  joined through the public IP, so the router passed UDP 38465 back in (NAT loopback).
- **Console gone:** Play sets `hide_console = true` in `UserData/Loader.cfg` (`game_log.rs`); a write failure only
  warns. **Live game log** (`read_game_log`, offset tail with run id = first line), `GameStatus` card (starting →
  preparing → loading → menu → connecting → joined → closing, from DedicatedServerClient 1.1.6 strings), `LogSheet`.
- **Gear → `SettingsSheet`** replaces the More menu: folder, game log, launcher log folder, two-click removals,
  "Uninstall Hyland Link" (strips mods + MelonLoader, runs `uninstall.exe` next to the exe, exits).
- **Leave server & close game** (two clicks while playing): WM_CLOSE to `UnityWndClass` windows only, never kills.
  Play locks to "Starting Schedule I…" until the game shows up (45 s cap).
- **Launcher log** (tauri-plugin-log): `%LOCALAPPDATA%\com.blazzer.hylandlink\logs\launcher.log`, `[app]/[ui]`
  lines; `bash scripts/logs.sh` prints it plus the game log, error counts and `hide_console`.
- Code review (fresh agent) → fixed: Loader.cfg failure blocked Play, stage lost after 600 lines, WM_CLOSE could hit
  the console, `get_state` hashing on the UI thread, double log parsing, unkeyed log rows, toasts under sheets, sheet
  focus, quit timer leak, double detect, duplicate `shortPath`, old clippy warning.
- Checks: `cargo test` 14 passed, clippy 0 warnings, svelte-check 0/0, `pnpm build` clean. **Real app**
  (`pnpm tauri build --no-bundle`): console hidden (no MelonLoader window), status card live, Leave & close → mod's
  normal shutdown lines and exit, Uninstall without `uninstall.exe` refuses and touches nothing.

### Key Decisions
- Hide the console via Loader.cfg, not by reparenting the window: Play goes through `steam://`, so no launch args,
  and Windows Terminal can't be embedded.
- Settings replaces More (one place for folder/logs/removal). Big button while playing = Leave & close, not "In game".
- No force-kill anywhere: a stuck game gets a notice after 30 s, not TerminateProcess.

### Hooks & Gotchas
- Testing the real exe while the installed launcher runs: both share the WebView2 profile, so the DevTools flag is
  ignored. Start the test exe with `WEBVIEW2_USER_DATA_FOLDER=%TEMP%\hl-test-webview` too.
- `app/node_modules/.modules.yaml` pointed at the pre-split `schedule1-server/hyland-link` store; repointed it by
  hand (pnpm add failed with ERR_PNPM_UNEXPECTED_VIRTUAL_STORE).
- Stage detection matches client-mod log strings (`game-log.ts` MARKERS): recheck them on every S1DS mod update.

### Next Steps
1. Blazzer OKs v0.1.1: bump the 3 versions, `scripts/release.sh --notes "…" --publish`. Installed launchers pick it up.
2. Push `main` (2 commits ahead of origin). Safe for friends: no manifest change.
3. Test Leave & close while joined to the server (only tested at the main menu).
4. Open from earlier: router TCP/UDP + .180 reservation check, back up the updater key, first extra mod.
5. Leftover to delete if wanted: `%TEMP%\hl-test-webview` (test WebView2 profile).

### Files Modified
- New: `app/src-tauri/src/game_log.rs`, `app/src/lib/game-log.ts`, `components/{Sheet,LogSheet,GameStatus,
  SettingsSheet}.svelte`, `scripts/logs.sh`
- Changed: `lib.rs`, `game.rs`, `Cargo.toml` (tauri-plugin-log, log, WindowsAndMessaging), `capabilities/default.json`,
  `launcher.svelte.ts`, `api.ts`, `dev-mock.ts`, `ActionBar`, `ModsSheet`, `Steps`, `TitleBar`, `+page.svelte`,
  `app/package.json`, CLAUDE.md, README.md, app/README.md

---

## Session 2 — 2026-11-02 02:45 (live: released, server online, self-running)

### Completed
- **Server-driven mods:** manifest schema 2 with `mods[]` (`side`: both/client/server). Launcher installs, updates,
  removes them hash-checked, tracks its files in `UserData/HylandLink.json`, re-reads the list every 5 min / on focus /
  before Play. Step 2: one "Server's mods" row + `ModsSheet`. Mock: `?s=mods`, `?s=nomods`.
- `scripts/mods.ps1` (list/add/remove/server-mod/publish): publish = cargo test validation → `hl s1 mods apply` over
  SSH → commit + push. Verified: server-mod reproduced the exact hashes; the gate caught a planted clash.
- Public repo `Blazzer10200/hyland-link`; raw manifest serves 200 anonymously. **v0.1.0 released**: key id
  54ae0a37e2f165c5 = pubkey, download link serves the built bytes.
- Server live (see schedule1-server HANDOFF): game installed, router forwards in, `69.50.245.12:38465` answers.
  The released launcher on this PC shows **"Online · 9 ms", 0 / 8** (router NAT loopback works).
- Cleanup: removed dead `GameInfo.picked_manually` (set, never read) and the unused template `app/static/favicon.png`;
  docs synced; added `scripts/read-app.mjs` (reads the real app's screen over WebView2 DevTools).
- Checks: `cargo test` 12 passed; svelte-check 0/0; `pnpm --dir app build` clean.

### Key Decisions
- Server is the source of truth; mods flow server → `main` → launchers, never the other way — no drift, no GitHub
  token on the internet-facing server.
- S1DS mod updates stay one command (ntfy says when), not automatic: they change friends' client mod and need a push.

### Failed / Don't Retry
- `kill <pid> <ppid>` on an orphan with PPID 0 sends `kill 0` to your own process group. Check PPID first.

### Load-Bearing Invariants
- `game.version` / `launcher.version` in the manifest look dead but v0.1.0 needs them to parse (see CLAUDE.md).
- `ModsSheet` lives inside `<main>` so the Update button stays clickable with the list open.

### Next Steps
1. A friend joins for real: proves UDP 38465 and the whole install flow on a clean PC.
2. Blazzer: confirm 38465 is TCP/UDP in the router and the .180 address reservation is saved.
3. First extra mod: `pwsh scripts/mods.ps1 add <url>` → `publish`.
4. Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC.

### Files Modified
- `app/src-tauri/src/{manifest,install,game}.rs`, `app/src/lib/{api.ts,launcher.svelte.ts,dev-mock.ts}`,
  `app/src/lib/components/{Steps,ActionBar,ModsSheet}.svelte`, `app/src/routes/+page.svelte`, `manifest/manifest.json`,
  `scripts/{mods.ps1,read-app.mjs}`, README.md, app/README.md, CLAUDE.md

---

## Session 1 — 2026-11-02 (built, verified locally, not published)

### Completed
- Built the launcher (Tauri 2 + Svelte 5): 4-step stepper on one 460x720 screen with no scrolling (find game → install
  MelonLoader 0.7.2 + IL2CPP client mod 1.1.6 + Favorite → first launch → join), live `DS_STATUS` ping, More menu
  (open/choose folder, remove mod). All 12 mock states previewed (`pnpm --dir app dev`, `?s=<state>`).
- Split out of `projects/schedule1-server` into this repo (`app/`, `manifest/`, `scripts/`), meant to be public.
- Self-update: `tauri-plugin-updater`, backend commands `check_update` / `install_update` (`app/src-tauri/src/lib.rs`),
  auto-runs on start with the "Updating Hyland Link" overlay (`app/src/routes/+page.svelte`). On failure: error toast
  plus a "Get it" toast that opens the Releases page.
- **Update loop verified end-to-end on this PC:** installed test 0.1.0 → localhost feed → it downloaded 0.1.1, verified
  the signature, installed, reopened as 0.1.1, then made no second update. The test build was uninstalled afterwards.
- `scripts/release.sh` builds, signs, writes `latest.json`, and with `--publish` creates the GitHub release.
  Built `dist/v0.1.0/` with it; its signature key id matches the pubkey (54ae0a37e2f165c5).
- Checks: `pnpm --dir app check` 0 errors; `cargo test` 7 passed, 1 ignored (network install test).

### Key Decisions
- Tauri updater over Velopack: it reuses the NSIS installer we already build, and exfil-v2 runs the same pattern.
- Local release script, not GitHub Actions: the signing key never leaves this PC, and there are no repo secrets to set.
- Same asset name every release (`Hyland-Link-Setup.exe`), so the friend link
  `github.com/Blazzer10200/hyland-link/releases/latest/download/Hyland-Link-Setup.exe` never changes.
- No bare-exe distribution anymore: a portable exe can't update itself. Always send the setup.

### Hooks & Gotchas
- `.secrets/env.sh` exports rift's `TAURI_SIGNING_PRIVATE_KEY`; `release.sh` overrides it. Never `pnpm tauri build` a
  release by hand.
- Long paths (the session scratchpad) break `Start-Process` (0xfffffffe) and GDI+. Use short `%TEMP%` paths.
- `python` on this PC is the Store stub (exit 126). Use node for throwaway servers.
- Until the GitHub repo exists, the launcher shows "Couldn't check for a Hyland Link update: …" on start. Expected; it
  goes away once v0.1.0 is published.

### Load-Bearing Invariants
- `plugins.updater.pubkey` in `tauri.conf.json` ↔ `C:/AI Workflow/.secrets/hyland-link-updater.key`. Back the key up
  off this PC.
- `manifest.rs` `REMOTE_URL` and `include_str!("../../../manifest/manifest.json")`: the folder layout is part of the
  build.

### Next Steps
1. Blazzer OKs creating the **public** repo `Blazzer10200/hyland-link`; then `gh repo create` + push `main`.
2. `scripts/release.sh --notes "First release…" --publish` → v0.1.0 goes live; send friends the permanent link.
3. Server side first, though (see `projects/schedule1-server/HANDOFF.md`): Steam login in CT 121 + port forwards,
   or friends will see the server offline.
4. Back up the updater key off this PC.

### Files Modified
- Everything under `app/`, `manifest/manifest.json`, `scripts/release.sh`, `README.md`, `CLAUDE.md`, `.claude/launch.json`

---
