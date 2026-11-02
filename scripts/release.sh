#!/usr/bin/env bash
# Builds a signed Hyland Link release into dist/v<version>/ and, with --publish, puts it on GitHub.
#
#   scripts/release.sh --notes "What changed"             build + sign only
#   scripts/release.sh --notes "What changed" --publish   also create the GitHub release
#
# Bump the version in app/package.json, app/src-tauri/Cargo.toml and app/src-tauri/tauri.conf.json first.
# Every installed launcher updates itself from the newest published (not draft) release.
set -euo pipefail

REPO="Blazzer10200/hyland-link"
KEY="C:/AI Workflow/.secrets/hyland-link-updater.key"
cd "$(dirname "$0")/.."

notes="Bug fixes and improvements."
publish=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --notes) notes="$2"; shift 2 ;;
    --publish) publish=true; shift ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

# The three version fields must agree, or the updater compares against the wrong number.
ver=$(node -p 'require("./app/src-tauri/tauri.conf.json").version')
pkg=$(node -p 'require("./app/package.json").version')
crate=$(sed -n 's/^version = "\(.*\)"/\1/p' app/src-tauri/Cargo.toml | head -1)
if [[ "$ver" != "$pkg" || "$ver" != "$crate" ]]; then
  echo "Version mismatch: tauri.conf.json $ver, package.json $pkg, Cargo.toml $crate" >&2
  exit 1
fi

if $publish && gh release view "v$ver" --repo "$REPO" >/dev/null 2>&1; then
  echo "v$ver is already on GitHub. Bump the version first." >&2
  exit 1
fi

[[ -f "$KEY" ]] || { echo "Signing key missing: $KEY" >&2; exit 1; }
# env.sh exports another app's key globally; Hyland Link must always sign with its own, or every update fails.
TAURI_SIGNING_PRIVATE_KEY="$(cat "$KEY")"
export TAURI_SIGNING_PRIVATE_KEY
export TAURI_SIGNING_PRIVATE_KEY_PASSWORD=""

pnpm --dir app tauri build

bundle="$(cygpath -u "${CARGO_TARGET_DIR:-app/src-tauri/target}")/release/bundle/nsis"
setup="$bundle/Hyland Link_${ver}_x64-setup.exe"
[[ -f "$setup" && -f "$setup.sig" ]] || { echo "Build output missing: $setup (.sig)" >&2; exit 1; }

# Same asset name every release, so releases/latest/download/Hyland-Link-Setup.exe is a permanent friend link.
out="dist/v$ver"
mkdir -p "$out"
cp "$setup" "$out/Hyland-Link-Setup.exe"
SIG="$(cat "$setup.sig")" \
  URL="https://github.com/$REPO/releases/download/v$ver/Hyland-Link-Setup.exe" \
  VER="$ver" NOTES="$notes" \
  node -e '
    const e = process.env;
    const feed = {
      version: e.VER,
      notes: e.NOTES,
      pub_date: new Date().toISOString().replace(/\.\d+Z$/, "Z"),
      platforms: { "windows-x86_64": { signature: e.SIG, url: e.URL } },
    };
    console.log(JSON.stringify(feed, null, 2));
  ' >"$out/latest.json"

echo
echo "Built Hyland Link $ver -> $out/"
ls -la "$out"

if ! $publish; then
  echo
  echo "Not published. Push main first (the manifest lives there), then rerun with --publish."
  exit 0
fi

gh release create "v$ver" "$out/Hyland-Link-Setup.exe" "$out/latest.json" \
  --repo "$REPO" --title "Hyland Link $ver" --notes "$notes"
echo "Published. Friends' launchers update on their next start."
echo "Download link: https://github.com/$REPO/releases/latest/download/Hyland-Link-Setup.exe"
