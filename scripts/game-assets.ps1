# Pulls Schedule I's own assets (UI prefabs and their layout, sprites, icons, fonts, sounds) out of the installed game
# with AssetRipper, so designs and mods are built against the real thing instead of screenshots. Everything lands in
# .gameref/ (gitignored: the game's assets are never public). The game folder is only read, never written.
#
#   pwsh scripts/game-assets.ps1 setup     # install the pinned AssetRipper into .gameref/tools/ (checksum-checked)
#   pwsh scripts/game-assets.ps1 export    # export the installed build to .gameref/assets/build-<buildid>/
#   pwsh scripts/game-assets.ps1 browse    # AssetRipper's own browser UI on the game, for poking around by hand
#
# One export per game build: after a game update, run export again and the new build gets its own folder. -Force
# replaces an existing export of the same build.
param(
    [Parameter(Position = 0, Mandatory)] [ValidateSet("setup", "export", "browse")] [string] $Command,
    [string] $GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Schedule I",
    [switch] $Force
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $PSScriptRoot
$GameRef = Join-Path $Root ".gameref"

# Bump all three together; the hash is the release asset's sha256 as GitHub lists it.
$Version = "2.0.0"
$Asset = "AssetRipper_win_x64.zip"
$Sha256 = "9a7ef0e7c5c3ea5b90b4e6d855e2d98d5f7ec8c3f9e26fccbc194c6a7b01baf7"

$ToolDir = Join-Path $GameRef "tools/assetripper-$Version"
$Exe = Join-Path $ToolDir "AssetRipper.GUI.Free.exe"
$Log = Join-Path $GameRef "tools/assetripper.log"

function Install-AssetRipper {
    if (Test-Path $Exe) {
        Write-Host "AssetRipper $Version is installed: $ToolDir"
        return
    }
    New-Item -ItemType Directory -Force (Split-Path $ToolDir) | Out-Null
    $zip = Join-Path (Split-Path $ToolDir) $Asset
    $url = "https://github.com/AssetRipper/AssetRipper/releases/download/$Version/$Asset"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $zip
    $got = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
    if ($got -ne $Sha256) {
        Remove-Item -LiteralPath $zip
        throw "Checksum mismatch for $Asset (got $got, expected $Sha256). Nothing was installed."
    }
    Expand-Archive -LiteralPath $zip -DestinationPath $ToolDir
    Remove-Item -LiteralPath $zip
    Write-Host "Installed AssetRipper $Version (sha256 ok): $ToolDir"
}

function Get-BuildId {
    $manifest = Join-Path $GamePath "..\..\appmanifest_3164500.acf"
    if (-not (Test-Path $manifest)) { throw "No Steam manifest at $manifest. Is -GamePath the Schedule I install folder?" }
    if ((Get-Content -Raw $manifest) -notmatch '"buildid"\s+"(\d+)"') { throw "No buildid in $manifest." }
    return $Matches[1]
}

function Stop-AssetRipper($Process) {
    if (-not $Process.HasExited) {
        Stop-Process -Id $Process.Id
        Wait-Process -Id $Process.Id -Timeout 15 -ErrorAction SilentlyContinue
    }
}

function Get-FreePort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}

# Starts AssetRipper on localhost and loads the game folder. The caller stops the returned process.
function Start-Loaded([bool] $Headless) {
    if (-not (Test-Path (Join-Path $GamePath "Schedule I_Data"))) { throw "No Schedule I_Data in $GamePath." }
    $port = Get-FreePort
    $base = "http://127.0.0.1:$port"
    $argList = "--port $port --log-path `"$Log`"" + $(if ($Headless) { " --headless" } else { "" })
    $proc = Start-Process -FilePath $Exe -ArgumentList $argList -WorkingDirectory $ToolDir -PassThru -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds(60)
    while ($true) {
        if ($proc.HasExited) { throw "AssetRipper exited during startup (code $($proc.ExitCode)). See $Log." }
        try { Invoke-WebRequest -Uri "$base/" -TimeoutSec 2 | Out-Null; break } catch { }
        if ((Get-Date) -gt $deadline) { Stop-AssetRipper $proc; throw "AssetRipper didn't answer on $base within 60 s." }
        Start-Sleep -Milliseconds 500
    }
    Write-Host "Loading $GamePath"
    try {
        Invoke-Form "$base/LoadFolder" @{ Path = $GamePath } 600
        if (-not (Select-String -Path $Log -SimpleMatch "Finished processing assets" -Quiet)) {
            throw "AssetRipper didn't finish loading the game. See $Log."
        }
    }
    catch {
        Stop-AssetRipper $proc
        throw
    }
    return @{ Process = $proc; Base = $base }
}

# AssetRipper's form endpoints redirect to its home page when done; following that lands on a 200.
function Invoke-Form([string] $Url, [hashtable] $Fields, [int] $TimeoutSec) {
    $r = Invoke-WebRequest -Uri $Url -Method Post -Body $Fields -TimeoutSec $TimeoutSec -SkipHttpErrorCheck
    if ($r.StatusCode -ne 200) { throw "$Url answered $($r.StatusCode)." }
}

switch ($Command) {
    "setup" {
        Install-AssetRipper
    }
    "export" {
        Install-AssetRipper
        $build = Get-BuildId
        $out = Join-Path $GameRef "assets/build-$build"
        if (Test-Path $out) {
            if (-not $Force) { throw "Build $build is already exported: $out. Use -Force to export it again." }
            Remove-Item -LiteralPath $out -Recurse -Force
        }
        $partial = "$out.partial"
        if (Test-Path $partial) { Remove-Item -LiteralPath $partial -Recurse -Force }
        New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
        if (Test-Path $Log) { Remove-Item -LiteralPath $Log }

        $started = Get-Date
        $ar = Start-Loaded $true
        try {
            Write-Host "Exporting build $build (about 2 minutes, 6.3 GB)"
            Invoke-Form "$($ar.Base)/Export/UnityProject" @{ Path = $partial } 7200
        }
        finally {
            Stop-AssetRipper $ar.Process
        }
        if (-not (Test-Path (Join-Path $partial "ExportedProject/Assets"))) { throw "The export has no ExportedProject/Assets. See $Log." }
        Rename-Item -LiteralPath $partial -NewName (Split-Path -Leaf $out)
        [ordered]@{
            game_build = $build; assetripper = $Version; exported_at = (Get-Date).ToString("o")
            minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1); game_path = $GamePath
        } | ConvertTo-Json | Set-Content (Join-Path $out "export.json")
        Write-Host "Exported build $build to $out"
    }
    "browse" {
        Install-AssetRipper
        $ar = Start-Loaded $false
        Write-Host "AssetRipper is browsing the game at $($ar.Base) (its window opened in your browser). Ctrl+C here stops it."
        try { Wait-Process -Id $ar.Process.Id }
        finally { Stop-AssetRipper $ar.Process }
    }
}
