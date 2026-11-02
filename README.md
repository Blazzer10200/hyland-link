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
- **The server mod:** [`manifest/manifest.json`](manifest/manifest.json) says which MelonLoader and mod versions the
  server runs. When it changes, the launcher shows **Update** and installs the new files in one click.

## For Blazzer: shipping changes

Mod or server change (new mod version, new IP, a notice for everyone): edit `manifest/manifest.json`, raise its
`revision`, and push `main`. Every launcher reads it on its next start.

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
manifest/            the version file the launcher reads (built in, and fetched from main)
scripts/release.sh   build, sign and publish a release
```
