# The HUD test copy (C:\Games\Schedule I (HUD test), see mods/hud/README.md "Testing in game"). Never touches the real
# Steam install. Swapping needs the test copy's game closed (MelonLoader has no hot reload); launch needs Steam open and
# then: join the server, never single player (both copies share the saves).
#
#   pwsh scripts/hud-testcopy.ps1 use main     # main's HUD build (mods/hud/bin/Release)
#   pwsh scripts/hud-testcopy.ps1 use phone    # the parked phone's build (.worktrees/hud-phone, branch hud-phone)
#   pwsh scripts/hud-testcopy.ps1 launch       # start the test copy's own exe
#   pwsh scripts/hud-testcopy.ps1 log          # what the HUD said in the test copy's last run, plus errors
#
# Build first: dotnet build -c Release in mods/hud (or .worktrees/hud-phone/mods/hud).
param(
    [Parameter(Position = 0, Mandatory)] [ValidateSet("use", "launch", "log")] [string] $Command,
    [Parameter(Position = 1)] [ValidateSet("main", "phone")] [string] $Build
)
$ErrorActionPreference = "Stop"
$Test = "C:\Games\Schedule I (HUD test)"
$Root = Split-Path $PSScriptRoot -Parent
$Builds = @{
    main  = "$Root\mods\hud\bin\Release\net6.0\HylandHud.dll"
    phone = "$Root\.worktrees\hud-phone\mods\hud\bin\Release\net6.0\HylandHud.dll"
}
$running = Get-CimInstance Win32_Process -Filter "Name='Schedule I.exe'" | Where-Object { $_.ExecutablePath -like "$Test\*" }

switch ($Command) {
    "use" {
        if (-not $Build) { throw "Which build: use main or use phone." }
        if ($running) { throw "The test copy's game is running (pid $($running.ProcessId)). Close it first." }
        if (-not (Test-Path $Builds[$Build])) { throw "No build at $($Builds[$Build]). Run dotnet build -c Release first." }
        Copy-Item $Builds[$Build] "$Test\Mods\HylandHud.dll" -Force
        $hash = (Get-FileHash "$Test\Mods\HylandHud.dll").Hash.Substring(0, 12)
        "Test copy HUD is now $Build, $((Get-Item "$Test\Mods\HylandHud.dll").VersionInfo.FileVersion) ($hash)."
    }
    "launch" {
        if ($running) { throw "The test copy is already running (pid $($running.ProcessId))." }
        $real = Get-CimInstance Win32_Process -Filter "Name='Schedule I.exe'"
        if ($real) { throw "Another Schedule I is running ($($real.ExecutablePath)). Close it first." }
        Start-Process -FilePath "$Test\Schedule I.exe" -WorkingDirectory $Test
        "Started the test copy. Join the server, not single player."
    }
    "log" {
        $log = "$Test\MelonLoader\Latest.log"
        if (-not (Test-Path $log)) { throw "No log yet: $log" }
        $lines = Get-Content $log
        "Log: $log ($((Get-Item $log).LastWriteTime))"
        $lines | Select-String -SimpleMatch "Hyland HUD v", "Hyland Minimap v" | ForEach-Object { $_.Line }
        "--- HUD lines"
        $lines | Select-String "\[Hyland_HUD\]" | Select-Object -Last 60 | ForEach-Object { $_.Line }
        $err = $lines | Select-String "\[ERROR\]|Exception|\[WARNING\]"
        "--- errors and warnings: $($err.Count)"
        $err | Select-Object -Last 15 | ForEach-Object { $_.Line }
    }
}
