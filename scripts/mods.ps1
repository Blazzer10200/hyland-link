# The server's mod list lives in manifest/manifest.json. This edits it and publishes it: the server applies it first,
# and main is pushed only after the server is back up on it, so players' launchers never get ahead of the server.
#
#   pwsh scripts/mods.ps1 list
#   pwsh scripts/mods.ps1 add <Thunderstore or GitHub zip URL> [-Side both|client|server] [-Skip "*Mono*"]
#                             [-Id x] [-Name "..."] [-Version 1.2.3] [-Blurb "..."]
#   pwsh scripts/mods.ps1 remove <id>
#   pwsh scripts/mods.ps1 server-mod <S1DS tag, e.g. 1.1.7>
#   pwsh scripts/mods.ps1 publish [-Now]
#
# add and remove only edit the file (rerun add with the new URL to update a mod). publish bumps `revision`, checks the
# file with the launcher's own rules (cargo test), runs `hl s1 mods apply` on the server over SSH, then commits and
# pushes. The server waits until nobody's online unless you pass -Now.
#Requires -Version 7
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet("list", "add", "remove", "server-mod", "publish")]
    [string] $Command,
    [Parameter(Position = 1)] [string] $Target,
    [ValidateSet("both", "client", "server")] [string] $Side,
    [string[]] $Skip = @(),
    [string] $Id,
    [string] $Name,
    [string] $Version,
    [string] $Blurb,
    [switch] $Now
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
# Inside a function $PSBoundParameters is that function's own, so read the script's here: an explicit -Blurb "" clears it.
$BlurbGiven = $PSBoundParameters.ContainsKey("Blurb")
Add-Type -AssemblyName System.IO.Compression

$Root = Split-Path -Parent $PSScriptRoot
$ManifestPath = Join-Path $Root "manifest/manifest.json"
$SshHost = "blazzer-labs"
# The hosts and folders of app/src-tauri/src/manifest.rs and schedule1-server's s1ds-mods. add/remove check only
# these; publish runs the launcher's full rules (cargo test) before anything reaches the server.
$ModDirs = "Mods", "Plugins", "UserLibs", "UserData"
$Hosts = "https://github.com/", "https://thunderstore.io/"

function Read-Manifest { Get-Content -Raw $ManifestPath | ConvertFrom-Json -Depth 20 }

function Write-Manifest($manifest) {
    # ConvertTo-Json writes CRLF on Windows; the repo keeps LF.
    $json = ($manifest | ConvertTo-Json -Depth 20) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($ManifestPath, "$json`n", [Text.UTF8Encoding]::new($false))
}

function Get-Sha256([byte[]] $bytes) {
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-Download([string] $url) {
    if (-not ($Hosts | Where-Object { $url.StartsWith($_) })) { throw "Only GitHub and Thunderstore downloads work in the launcher: $url" }
    Write-Host "Downloading $url"
    $client = [Net.Http.HttpClient]::new()
    try { $client.GetByteArrayAsync($url).GetAwaiter().GetResult() } finally { $client.Dispose() }
}

function Open-Zip([byte[]] $bytes) {
    [IO.Compression.ZipArchive]::new([IO.MemoryStream]::new($bytes), [IO.Compression.ZipArchiveMode]::Read)
}

function Read-Entry($entry) {
    $stream = $entry.Open()
    $buffer = [IO.MemoryStream]::new()
    try { $stream.CopyTo($buffer) } finally { $stream.Dispose() }
    , $buffer.ToArray()
}

# Where a zip entry goes in the game folder: kept under Mods/Plugins/UserLibs/UserData, a loose .dll goes to Mods/,
# everything else (readme, icon, Thunderstore's manifest.json) is skipped.
# $wrapper: the one folder a whole GitHub zip sits in ("MyMod-1.0/Mods/x.dll"), looked through; see Get-Wrapper.
function Get-GamePath([string] $entryName, [string] $wrapper) {
    $parts = $entryName.Replace('\', '/').Split('/')
    if ($wrapper -and $parts.Count -gt 1 -and $parts[0] -eq $wrapper) { $parts = $parts[1..($parts.Count - 1)] }
    $dir = $ModDirs | Where-Object { $_ -ieq $parts[0] } | Select-Object -First 1
    if ($dir -and $parts.Count -gt 1) { return (@($dir) + $parts[1..($parts.Count - 1)]) -join '/' }
    if ($entryName.EndsWith(".dll", [StringComparison]::OrdinalIgnoreCase)) { return "Mods/$($parts[-1])" }
    $null
}

# Only a zip whose every entry sits in one folder (that isn't itself Mods/Plugins/...) has a wrapper. A Thunderstore
# package has files at its root, so a "BepInEx/plugins" folder in one is never mistaken for one.
function Get-Wrapper($zip) {
    $names = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $roots = @($names | ForEach-Object { $_.Split('/')[0] } | Sort-Object -Unique)
    if ($roots.Count -eq 1 -and -not ($ModDirs -icontains $roots[0]) -and -not ($names | Where-Object { -not $_.Contains('/') })) {
        return $roots[0]
    }
    $null
}

function Show-List {
    $manifest = Read-Manifest
    Write-Host "Revision $($manifest.revision), MelonLoader $($manifest.melonloader.version), server mod $($manifest.mod.version)"
    if (-not $manifest.mods) { Write-Host "No extra mods."; return }
    foreach ($mod in $manifest.mods) {
        Write-Host ("  {0,-24} {1,-10} {2,-7} {3}" -f $mod.id, $mod.version, $mod.side, ($mod.files.path -join ", "))
    }
}

function Add-Mod {
    if (-not $Target) { throw "usage: mods.ps1 add <zip URL> [-Side both|client|server] [-Skip pattern] [-Id] [-Name] [-Version] [-Blurb]" }
    $manifest = Read-Manifest
    $bytes = Get-Download $Target
    $zip = Open-Zip $bytes
    try {
        # Thunderstore packages describe themselves; GitHub zips need -Name and -Version.
        $info = $null
        $infoEntry = $zip.GetEntry("manifest.json")
        if ($infoEntry) { $info = [Text.Encoding]::UTF8.GetString((Read-Entry $infoEntry)).TrimStart([char]0xFEFF) | ConvertFrom-Json }
        $wrapper = Get-Wrapper $zip
        $files = @(foreach ($entry in $zip.Entries) {
                if ($entry.FullName.EndsWith("/")) { continue }
                if ($Skip | Where-Object { $entry.FullName -like $_ }) { Write-Host "  skipping $($entry.FullName)"; continue }
                $path = Get-GamePath $entry.FullName $wrapper
                if (-not $path) { continue }
                [ordered]@{ source = $entry.FullName; path = $path; sha256 = Get-Sha256 (Read-Entry $entry) }
            })
    }
    finally { $zip.Dispose() }

    if ($files.Count -eq 0) { throw "Nothing to install in that zip (no .dll and no $($ModDirs -join '/') folders)." }
    # StrictMode throws on a missing property, and a GitHub zip's manifest.json may not be a Thunderstore one.
    $field = { param($n) if ($info -and $info.PSObject.Properties[$n]) { $info.$n } }
    $modName = if ($Name) { $Name } elseif (& $field name) { (& $field name) -replace '_', ' ' } else { throw "No Thunderstore manifest.json in the zip: pass -Name and -Version." }
    $modVersion = if ($Version) { $Version } elseif (& $field version_number) { & $field version_number } else { throw "Pass -Version for a GitHub zip." }
    $modId = if ($Id) { $Id } else { ($modName.ToLowerInvariant() -replace '[^a-z0-9_-]+', '-').Trim('-') }
    $existing = @($manifest.mods | Where-Object { $_.id -eq $modId })
    $modSide = if ($Side) { $Side } elseif ($existing) { $existing[0].side } else { "both" }
    $modBlurb = if ($BlurbGiven) { $Blurb } elseif ($existing) { $existing[0].blurb } elseif (& $field description) {
        $d = [string](& $field description); if ($d.Length -gt 80) { $d.Substring(0, 77) + "..." } else { $d }
    } else { "" }

    $entry = [ordered]@{
        id = $modId; name = $modName; version = $modVersion; blurb = $modBlurb; side = $modSide
        url = $Target; sha256 = Get-Sha256 $bytes; files = $files
    }
    # Replace in place to keep the display order; new mods go last.
    $mods = @($manifest.mods)
    $index = [array]::FindIndex([object[]]$mods, [Predicate[object]] { param($m) $m.id -eq $modId })
    if ($index -ge 0) { $mods[$index] = $entry } else { $mods += $entry }
    $manifest.mods = @($mods)
    Write-Manifest $manifest

    Write-Host "$(if ($index -ge 0) { 'Updated' } else { 'Added' }) $modId ($modName $modVersion, side: $modSide):"
    $files | ForEach-Object { Write-Host "  $($_.source) -> $($_.path)" }
    if ($files | Where-Object { $_.source -match 'mono' }) {
        Write-Warning "Something here looks like a Mono build. Players and the server run IL2CPP: rerun with -Skip '*Mono*'."
    }
    Write-Host "Not live yet. Run: pwsh scripts/mods.ps1 publish"
}

function Remove-Mod {
    if (-not $Target) { throw "usage: mods.ps1 remove <id>" }
    $manifest = Read-Manifest
    $kept = @($manifest.mods | Where-Object { $_.id -ne $Target })
    if ($kept.Count -eq @($manifest.mods).Count) { throw "No mod with id '$Target'. See: pwsh scripts/mods.ps1 list" }
    $manifest.mods = $kept
    Write-Manifest $manifest
    Write-Host "Removed $Target from the list. Not live yet. Run: pwsh scripts/mods.ps1 publish"
}

function Set-ServerMod {
    if (-not $Target) { throw "usage: mods.ps1 server-mod <S1DS tag, e.g. 1.1.7>" }
    $tag = $Target.TrimStart("v")
    $manifest = Read-Manifest
    $url = "https://github.com/ifBars/S1DedicatedServers/releases/download/v$tag/Il2cpp_Client.zip"
    $bytes = Get-Download $url
    $zip = Open-Zip $bytes
    try {
        $dll = $zip.GetEntry($manifest.mod.dll)
        if (-not $dll) { throw "Il2cpp_Client.zip v$tag has no $($manifest.mod.dll); the mod's layout changed, update manifest.json by hand." }
        $dllSha = Get-Sha256 (Read-Entry $dll)
    }
    finally { $zip.Dispose() }
    $manifest.mod.version = $tag
    $manifest.mod.url = $url
    $manifest.mod.sha256 = Get-Sha256 $bytes
    $manifest.mod.dllSha256 = $dllSha
    Write-Manifest $manifest
    Write-Host "Server mod -> $tag (players' client DLL and the server image ghcr.io/ifbars/s1dedicatedservers:$tag)."
    Write-Host "Publishing checks the server boots on MelonLoader $($manifest.melonloader.version). Run: pwsh scripts/mods.ps1 publish"
}

# Launchers read the raw URL, which every GitHub CDN edge server may keep for 5 minutes (max-age=300; query strings
# don't bypass it). One edge serving the new revision proves nothing about the player's edge: "live" is 5 minutes
# after the push, once every cached copy has expired.
function Wait-RawManifest([int] $revision) {
    $url = "https://raw.githubusercontent.com/Blazzer10200/hyland-link/main/manifest/manifest.json"
    $pushed = Get-Date
    $deadline = $pushed.AddMinutes(7)
    Write-Host "Waiting 5 minutes for every GitHub cache copy of the old revision to expire..."
    while ($true) {
        $served = try { [int](Invoke-RestMethod $url -TimeoutSec 10).revision } catch { -1 }
        if ($served -eq $revision -and (Get-Date) -ge $pushed.AddSeconds(310)) {
            Write-Host "Revision $revision is live: launchers get it on their next check (every 5 min, on focus, or at start)."
            return
        }
        if ((Get-Date) -gt $deadline) {
            Write-Warning "After 7 minutes the launchers' URL still serves revision $served, not $revision. The server and main are on $revision; check $url"
            return
        }
        Start-Sleep -Seconds 15
    }
}

function Publish-Mods {
    Push-Location $Root
    try {
        $other = @(git status --porcelain | Where-Object { $_ -notmatch ' manifest/manifest\.json$' })
        if ($other) { throw "Commit or stash other changes first; publish commits only manifest/manifest.json:`n$($other -join "`n")" }
        $branch = git rev-parse --abbrev-ref HEAD
        if ($branch -ne "main") { throw "Publish from main (you're on $branch)." }
        # Checked before the server changes: the push at the end must send only the manifest commit, and must not be
        # rejected for being behind after the server already runs the new list.
        git fetch -q origin main
        if ($LASTEXITCODE) { throw "git fetch failed; publish needs to know main matches GitHub first. Nothing changed." }
        $ahead = [int](git rev-list --count origin/main..main)
        $behind = [int](git rev-list --count main..origin/main)
        if ($ahead -or $behind) { throw "main is $ahead ahead and $behind behind origin/main. Push or pull first. Nothing changed." }

        $original = Get-Content -Raw $ManifestPath
        $manifest = Read-Manifest
        $revision = [int]$manifest.revision + 1
        $manifest.revision = $revision
        Write-Manifest $manifest
        $applied = $false
        try {
            # The launcher's own validation, so players never fall back to the built-in list over a typo.
            cargo test --manifest-path app/src-tauri/Cargo.toml --lib manifest::
            if ($LASTEXITCODE) { throw "The launcher would reject this manifest (test output above). Nothing changed on the server." }

            $sshArgs = @("-t", $SshHost, "hl", "s1", "mods", "apply", "--b64", [Convert]::ToBase64String([IO.File]::ReadAllBytes($ManifestPath)))
            if ($Now) { $sshArgs += "--now" }
            ssh @sshArgs
            if ($LASTEXITCODE) { throw "The server didn't finish applying revision $revision (output above). Nothing was pushed." }
            $applied = $true
        }
        finally {
            # Also on Ctrl+C (which skips catch): the file goes back unless the server took the new revision.
            if (-not $applied) { [IO.File]::WriteAllText($ManifestPath, $original, [Text.UTF8Encoding]::new($false)) }
        }

        git add manifest/manifest.json
        git commit -m "Mods: revision $revision"
        if ($LASTEXITCODE) { throw "git commit failed. The server already runs revision ${revision}: commit and push main by hand." }
        git push origin main
        if ($LASTEXITCODE) { throw "The server runs revision $revision but the push failed. Push main by hand now so players match." }
        Wait-RawManifest $revision
    }
    finally { Pop-Location }
}

switch ($Command) {
    "list" { Show-List }
    "add" { Add-Mod }
    "remove" { Remove-Mod }
    "server-mod" { Set-ServerMod }
    "publish" { Publish-Mods }
}
