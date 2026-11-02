---
title: Hyland Link handoff
tags: [project, handoff]
status: released
updated: 2026-11-02
---

# Hyland Link: handoff

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
