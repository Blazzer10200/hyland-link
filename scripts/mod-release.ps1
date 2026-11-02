# Ships one of our own client mods (mods/hud, mods/minimap) to players. Without -Publish nothing leaves this PC: it
# builds, stages dist/<tag>/<Mod>.zip with forward-slash entries and checks it. With -Publish it also creates the GitHub
# release (never "latest": the launcher's updater reads releases/latest) and adds the mod to manifest/manifest.json.
# Going live stays a separate step, so the server always applies the mod list first:
#
#   pwsh scripts/mod-release.ps1 hud              # build + stage + check
#   pwsh scripts/mod-release.ps1 hud -Publish     # + GitHub release + mods.ps1 add
#   pwsh scripts/mods.ps1 publish                 # server applies it, then main is pushed (players get it)
#
# The version comes from the mod's src/AssemblyInfo.cs (MelonInfo and AssemblyVersion must agree); bump it there first.
param(
    [Parameter(Position = 0, Mandatory)] [ValidateSet("hud", "minimap")] [string] $Mod,
    [switch] $Publish
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$Root = Split-Path -Parent $PSScriptRoot
$Repo = "Blazzer10200/hyland-link"
$Mods = @{
    hud     = @{ Dir = "mods/hud"; Dll = "HylandHud.dll"; Zip = "HylandHud.zip"; Tag = "hud"; Id = "hyland-hud"; Name = "Hyland HUD"
                 Extra = @(); Blurb = "Glass hotbar, compass with friends, clock with curfew and sleep warnings, cash and bank." }
    minimap = @{ Dir = "mods/minimap"; Dll = "HylandMinimap.dll"; Zip = "HylandMinimap.zip"; Tag = "minimap"; Id = "hyland-minimap"
                 Name = "Hyland Minimap"; Extra = @("LICENSE.md"); Blurb = "Minimap with customers, dealers, vehicles and players. PageUp/Down to zoom." }
}
$m = $Mods[$Mod]

$info = Get-Content -Raw (Join-Path $Root "$($m.Dir)/src/AssemblyInfo.cs")
if ($info -notmatch 'MelonInfo\([^,]+,\s*"[^"]+",\s*"(\d+\.\d+\.\d+)"') { throw "No version in the MelonInfo of $($m.Dir)/src/AssemblyInfo.cs." }
$version = $Matches[1]
if ($info -notmatch ('AssemblyVersion\("' + [regex]::Escape($version) + '\.0"\)')) {
    throw "AssemblyVersion in $($m.Dir)/src/AssemblyInfo.cs doesn't match MelonInfo's $version."
}
$tag = "$($m.Tag)-v$version"

Write-Host "Building $($m.Name) $version"
dotnet build -c Release (Join-Path $Root $m.Dir) --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
$dll = Join-Path $Root "$($m.Dir)/bin/Release/net6.0/$($m.Dll)"

# Forward-slash entry paths: the launcher (and Windows' own Compress-Archive on 5.1) would otherwise see "Mods\x.dll".
$stage = Join-Path $Root "dist/$tag"
New-Item -ItemType Directory -Force $stage | Out-Null
$zipPath = Join-Path $stage $m.Zip
if (Test-Path $zipPath) { [IO.File]::Delete($zipPath) }
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, "Mods/$($m.Dll)", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    foreach ($extra in $m.Extra) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $Root "$($m.Dir)/$extra"), $extra, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {
    $zip.Dispose()
}
$check = [IO.Compression.ZipFile]::OpenRead($zipPath)
try { $entries = @($check.Entries | ForEach-Object FullName) } finally { $check.Dispose() }
if ($entries -notcontains "Mods/$($m.Dll)" -or ($entries | Where-Object { $_.Contains("\") })) {
    throw "The zip's entries are wrong: $($entries -join ', ')"
}
$sha = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Staged $zipPath"
Write-Host "  entries: $($entries -join ', ')"
Write-Host "  sha256:  $sha"

if (-not $Publish) {
    Write-Host "Not published. To ship: pwsh scripts/mod-release.ps1 $Mod -Publish, then pwsh scripts/mods.ps1 publish."
    exit 0
}

# The release must match what's on main, and a tag is never reused (players' launchers cache by hash, not tag).
if (git -C $Root status --porcelain) { throw "The working tree isn't clean. Commit first, so the release matches main." }
git -C $Root fetch -q origin main
if ((git -C $Root rev-parse HEAD) -ne (git -C $Root rev-parse origin/main)) { throw "HEAD isn't origin/main. Push main first." }
gh release view $tag --repo $Repo *> $null
if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists. Bump the version in $($m.Dir)/src/AssemblyInfo.cs." }

gh release create $tag $zipPath --repo $Repo --latest=false --title "$($m.Name) $version" `
    --notes "$($m.Name) $version for the Hyland server. Players get it through the Hyland Link launcher; it's not meant to be installed by hand."
if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }
$latest = gh release view --repo $Repo --json tagName --jq .tagName
if (-not $latest.StartsWith("v")) { throw "releases/latest is now $latest, not a launcher release. Fix it on GitHub before anything else: installed launchers update from it." }

$url = "https://github.com/$Repo/releases/download/$tag/$($m.Zip)"
pwsh -NoProfile -File (Join-Path $PSScriptRoot "mods.ps1") add $url -Id $m.Id -Side client -Name $m.Name -Version $version -Blurb $m.Blurb
if ($LASTEXITCODE -ne 0) { throw "mods.ps1 add failed; the release $tag exists but the mod list wasn't changed." }
Write-Host "Released $tag and added it to the mod list. Next: pwsh scripts/mods.ps1 publish (the server applies it first, then main is pushed)."
