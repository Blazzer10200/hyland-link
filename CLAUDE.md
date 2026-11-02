# Hyland Link: project rules

Friend launcher for Blazzer's Schedule I server (server tooling lives in `projects/schedule1-server`, private).
This repo is meant to be **public** (`Blazzer10200/hyland-link`), because the updater and the manifest are fetched
anonymously from it.

State and next steps: `HANDOFF.md`. Maintainer flow (shipping mod/launcher updates): `README.md`.

## Map
- `app/src-tauri/src/lib.rs`: every Tauri command (state, install, play, status, updater) + plugin setup.
- `app/src-tauri/src/{game,install,favorites,status,manifest}.rs`: Steam detection, install/uninstall, the mod's
  favorites file, the `DS_STATUS` query, the version file (built-in vs online by `revision`).
- `app/src/lib/launcher.svelte.ts`: the whole UI state machine (phases, current step, self-update).
- `app/src/lib/components/`: `Steps` (the stepper), `ActionBar` (big button + More menu), `ServerCard`, `TitleBar`.
- `app/src/routes/+page.svelte`: layout, toasts, the self-update overlay. `app/src/app.css`: design tokens.
- `app/src/lib/dev-mock.ts`: fake backend for the browser preview (`.claude/launch.json` → `hyland-link-ui`, port 1420,
  viewport 460x720).
- `manifest/manifest.json`: what friends install. `scripts/release.sh`: the only way to ship a launcher build.

## Never break
- **Updater key:** `C:/AI Workflow/.secrets/hyland-link-updater.key` (minisign, empty password). Its pubkey is in
  `app/src-tauri/tauri.conf.json` → `plugins.updater.pubkey`. Never regenerate or rotate casually: installed launchers
  only accept updates signed by this key.
- **Signing env trap:** `.secrets/env.sh` exports `TAURI_SIGNING_PRIVATE_KEY` for another app (rift). Always build
  releases through `scripts/release.sh`, which overrides it with this app's key. A release signed with the wrong key
  fails verification on every friend's PC.
- **Versions:** `app/package.json`, `app/src-tauri/Cargo.toml` and `tauri.conf.json` must match. The script refuses a
  mismatch.
- **Public repo hygiene:** nothing from `schedule1-server` goes in here (the ntfy topic lives in its scripts). The
  server IP in `manifest/manifest.json` is public on purpose.
- **Releases move only when published:** a draft doesn't update `releases/latest`. The asset is always named
  `Hyland-Link-Setup.exe`, so the friend download link never changes.
- Don't install the mod into Blazzer's real game folder to test. That copy is used for co-op. Use the browser mock
  (`pnpm --dir app dev`, `?s=<state>`) or `cargo test -- --ignored` (installs into a temp folder).

## Build
- `CARGO_TARGET_DIR=C:\cargo-targets` on this PC, so bundles land in `C:\cargo-targets\release\bundle\nsis\`.
- Checks: `pnpm --dir app check`, `cd app/src-tauri && cargo test`.
