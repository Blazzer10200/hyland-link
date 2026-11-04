# One unattended test join from the HUD test copy (C:\Games\Schedule I (HUD test)), using the S1DS client's built-in
# reconnect test. Prints whether the player got all the way in, and the lines our mods logged. For "stuck on the loading
# screen" bugs: reproduce against a sandbox server (s1ds-sandbox up <backup> on the server box, port 38466), then test a fix.
#
#   pwsh scripts/join-test.ps1                                   # live server
#   pwsh scripts/join-test.ps1 -Port 38466                       # the sandbox
#   pwsh scripts/join-test.ps1 -Port 38466 -Mod mods/fixes/bin/Release/net6.0/HylandFixes.dll
#
# -Mod copies DLLs into the test copy's Mods for this run only and takes them out again. The S1DS "PASS" means connected,
# not loaded: a load that dies shows as "Unhandled exception in coroutine" with no DWELL_OK, so that's what this reads.
# Join the server, never single-player: the test copy shares saves with the real game.
param(
    # The server's LAN address (schedule1-server README). Kept out of this public repo: pass it or set S1_TEST_HOST.
    [string] $TestHost = $env:S1_TEST_HOST,
    [int] $Port = 38465,
    [string[]] $Mod = @(),
    [int] $TimeoutSeconds = 180
)
$ErrorActionPreference = "Stop"
if (-not $TestHost) { throw "Which server? Pass -TestHost <LAN IP> or set S1_TEST_HOST (the server's LAN address)." }
$T ="C:\Games\Schedule I (HUD test)"
$exe = Join-Path $T "Schedule I.exe"
if (-not (Test-Path $exe)) { throw "No test copy at $T (see mods/hud/README.md, 'Testing in game')." }

# Only the test copy's game: Blazzer's real game may be running.
function Stop-TestCopy {
    Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($T, [StringComparison]::OrdinalIgnoreCase) } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
Stop-TestCopy
Start-Sleep 2

$added = @()
foreach ($m in $Mod) {
    $src = Resolve-Path $m
    $dst = Join-Path $T "Mods\$(Split-Path -Leaf $src)"
    if (Test-Path $dst) { throw "$dst is already in the test copy; -Mod is for DLLs that aren't." }
    Copy-Item $src $dst
    $added += $dst
}
try {
    $a = "--s1ds-reconnect-test --s1ds-reconnect-test-host $TestHost --s1ds-reconnect-test-port $Port " +
         "--s1ds-reconnect-test-cycles 1 --s1ds-reconnect-test-dwell-seconds 10 --s1ds-reconnect-test-start-delay-seconds 5 " +
         "--s1ds-reconnect-test-join-timeout-seconds $TimeoutSeconds --s1ds-reconnect-test-quit"
    Write-Host "Joining ${TestHost}:$Port with$(if ($added) { ' ' + (($added | Split-Path -Leaf) -join ', ') } else { ' the test copy as it is' })"
    $p = Start-Process -FilePath $exe -WorkingDirectory $T -ArgumentList $a -PassThru
    $log = Join-Path $T "MelonLoader\Latest.log"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds + 30)
    Start-Sleep 20
    $outcome = "TIMEOUT: no outcome in $($TimeoutSeconds + 30) s"
    while ((Get-Date) -lt $deadline) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match "Unhandled exception in coroutine") { $outcome = "STUCK: the load sequence crashed (loading screen never closes)"; break }
        if ($text -match "CYCLE 1/1 DWELL_OK") { $outcome = "LOADED IN"; break }
        if ($p.HasExited) { $outcome = "GAME EXITED before an outcome (exit $($p.ExitCode))"; break }
        Start-Sleep 3
    }
    Start-Sleep 3
} finally {
    Stop-TestCopy
    Start-Sleep 2
    foreach ($d in $added) { Remove-Item -LiteralPath $d -Force }
}
Write-Host "OUTCOME: $outcome"
Get-Content $log | Select-String -Pattern "\[Hyland|Unhandled exception in coroutine|NullReferenceException|DWELL_OK" |
    Select-Object -First 25 | ForEach-Object { "  $($_.Line)" }
Write-Host "Full log: $log"
if ($outcome -ne "LOADED IN") { exit 1 }
