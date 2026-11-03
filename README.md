# Hyland Link

The launcher for **Blazzer's Schedule I server**. It finds your game, installs the server mod, saves the server to your
Favorites and tells you when the server is up. It keeps itself up to date.

## Get it

**[Download Hyland Link](https://github.com/Blazzer10200/hyland-link/releases/latest/download/Hyland-Link-Setup.exe)**,
run it, and follow the four steps on screen.

Windows may say "Windows protected your PC" the first time, because the app isn't code-signed. Click **More info → Run
anyway**. It installs just for you, with no admin prompt, and only ever changes files inside your Schedule I folder.

You need Schedule I on Steam, on the normal version (not a beta branch).

## How updates reach you

- **The launcher:** on every start it checks this repo's newest release. If there's a newer one, it downloads it,
  checks the signature, installs it and reopens. You don't click anything.
- **The server's mods:** [`manifest/manifest.json`](manifest/manifest.json) lists the MelonLoader version, the server
  mod and every extra mod the server runs. The launcher re-reads it every few minutes; when it changes, the button
  turns into **Update** and installs exactly what the server has, in one click. It only removes mods it installed
  itself, never your own.

## For Blazzer: shipping changes

Mods (new S1DS version, extra mods), all from this folder:

```bash
pwsh scripts/mods.ps1 add <Thunderstore or GitHub zip URL>   # or: remove <id> · server-mod <tag> · list
pwsh scripts/mods.ps1 publish                                # server applies it first, then main is pushed
```

`publish` checks the file with the launcher's own rules, applies it on the server (waiting until nobody's online
unless `-Now`), confirms the server booted on the same MelonLoader players get, and only then pushes `main`. Each mod
has a `side`: `both` (default), `server` (never reaches players) or `client` (players only).

Our own client mods (`mods/hud`, `mods/minimap`): bump the mod's `src/AssemblyInfo.cs`, commit and push `main`, then

```bash
pwsh scripts/mod-release.ps1 hud -Publish   # GitHub release hud-v<version> (never "latest") + mods.ps1 add
pwsh scripts/mods.ps1 publish               # as above
```

Without `-Publish` it only builds and stages the zip in `dist/<tag>/`.

Other manifest changes (new IP, a notice for everyone): edit `manifest/manifest.json`, raise its `revision`, run
`cargo test --manifest-path app/src-tauri/Cargo.toml --lib manifest::` (a file the launcher rejects sends every player
back to the built-in list), and push `main`.

Launcher change:

1. Bump the version in `app/package.json`, `app/src-tauri/Cargo.toml` and `app/src-tauri/tauri.conf.json`.
2. Commit and push `main`.
3. `scripts/release.sh --notes "What changed" --publish`

The script builds, signs with `C:/AI Workflow/.secrets/hyland-link-updater.key`, writes `latest.json` and creates the
GitHub release. Without `--publish` it only builds into `dist/v<version>/`. **Never lose that key.** Without it,
installed launchers can't verify another update, and everyone has to reinstall once by hand.

## Layout

```
app/                 the Tauri 2 + Svelte 5 app (see app/README.md)
manifest/            the version file the launcher and the server both follow (built in, and fetched from main)
mods/minimap/        Hyland Minimap, our fork of nacho's Schedule I Minimap (MIT)
mods/hud/            Hyland HUD, our restyle of the game's hotbar, compass, money and phone
scripts/mods.ps1     edit the server's mod list and publish it (server first, then main)
scripts/mod-release.ps1  build, stage and release one of our own mods (hud, minimap)
scripts/release.sh   build, sign and publish a launcher release
scripts/read-app.mjs print the real app's screen text (dev check, see CLAUDE.md)
scripts/logs.sh      print the launcher log, the game log and the console setting on this PC
scripts/game-assets.ps1  export the live game's prefabs and art into .gameref/ (local only, never committed)
scripts/prefab-tree.mjs  print one UI subtree of that export (rects, images, texts, layout)
```
