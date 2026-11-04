# The local dev loop for our mods (docs/DEV-WORKFLOW.md): a dev server on this PC, the HUD test copy joining it, and
# HotReload swapping new builds into the running game. Nothing here touches the live server, its world, or Blazzer's
# real game folder: every path below is one of the two dev copies.
#
#   pwsh scripts/dev.ps1 server up [<world>]    start the dev server (an empty dev world loads <world>, default: newest)
#   pwsh scripts/dev.ps1 server down            save (waits for the game to confirm it), then stop
#   pwsh scripts/dev.ps1 server status          running?, world loaded, who's on
#   pwsh scripts/dev.ps1 server log [<lines>]   tail of its MelonLoader log
#   pwsh scripts/dev.ps1 server cmd <command>   one console command (save, listplayers, help, ...)
#   pwsh scripts/dev.ps1 server world <file>    swap in another world backup (server down; the old world is moved aside)
#   pwsh scripts/dev.ps1 play                   start the test copy and join the dev server straight from launch
#   pwsh scripts/dev.ps1 push <mod> [<mod>...]  build and copy into the test copy; a running game reloads it (HotReload)
#   pwsh scripts/dev.ps1 watch <mod>            push again on every save under the mod's src/ (Ctrl+C stops)
#   pwsh scripts/dev.ps1 jointest [<dll>...]    one unattended join against the dev server (join-test.ps1)
#   pwsh scripts/dev.ps1 status                 dev server, and each mod in the test copy vs your build vs the live list
#   pwsh scripts/dev.ps1 log                    the test copy's log: our mods, HotReload, errors
#
# Mods: hud, phone (the parked phone, .worktrees/hud-phone), notify, minimap, fixes.
# World backups live in C:\Games\Schedule I dev worlds (copy one down from the server: schedule1-server's pull-world.sh).
param(
    [Parameter(Position = 0, Mandatory)] [ValidateSet("server", "play", "push", "watch", "jointest", "status", "log")]
    [string] $Command,
    [Parameter(Position = 1, ValueFromRemainingArguments)] [string[]] $Rest = @()
)
$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$Server = "C:\Games\Schedule I (dev server)"
$Test = "C:\Games\Schedule I (HUD test)"
$Worlds = "C:\Games\Schedule I dev worlds"
$Port = 38465
$ConsolePort = 4050
$ServerLog = "$Server\MelonLoader\Latest.log"
$TestLog = "$Test\MelonLoader\Latest.log"
$Save = "$Server\UserData\DedicatedServerSave"
$Mods = [ordered]@{
    hud     = @{ Dir = "$Root\mods\hud"; Dll = "HylandHud.dll" }
    phone   = @{ Dir = "$Root\.worktrees\hud-phone\mods\hud"; Dll = "HylandHud.dll" }
    notify  = @{ Dir = "$Root\mods\notify"; Dll = "HylandNotify.dll" }
    minimap = @{ Dir = "$Root\mods\minimap"; Dll = "HylandMinimap.dll" }
    fixes   = @{ Dir = "$Root\mods\fixes"; Dll = "HylandFixes.dll" }
}

function Get-Game([string] $folder) {
    Get-CimInstance Win32_Process -Filter "Name='Schedule I.exe'" |
        Where-Object { $_.ThreadCount -gt 0 -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith("$folder\", [StringComparison]::OrdinalIgnoreCase) }
}

# One line to the server's loopback TCP console; returns its reply without the banner.
function Send-Console([string] $line) {
    $tcp = [Net.Sockets.TcpClient]::new()
    try {
        $tcp.Connect("127.0.0.1", $ConsolePort)
        $stream = $tcp.GetStream()
        $bytes = [Text.Encoding]::UTF8.GetBytes("$line`r`n")
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.ReadTimeout = 1500
        $buffer = [byte[]]::new(65536)
        $text = ""
        $until = (Get-Date).AddSeconds(4)
        while ((Get-Date) -lt $until) {
            try { $n = $stream.Read($buffer, 0, $buffer.Length) } catch [IO.IOException] { break }
            if ($n -le 0) { break }
            $text += [Text.Encoding]::UTF8.GetString($buffer, 0, $n)
        }
        $text -split "`r?`n" | ForEach-Object { $_ -replace '^> ', '' } |
            Where-Object { $_ -and $_ -notmatch '^(ScheduleOne Dedicated Server Console|Type .help.|Command session|If your host panel|Connected at )' }
    } finally { $tcp.Dispose() }
}

function Get-LogFrom([string] $log, [long] $offset) {
    if (-not (Test-Path $log)) { return "" }
    $fs = [IO.File]::Open($log, "Open", "Read", "ReadWrite")
    try {
        if ($offset -gt $fs.Length) { $offset = 0 }
        $fs.Position = $offset
        [IO.StreamReader]::new($fs).ReadToEnd()
    } finally { $fs.Dispose() }
}

function Build-Mod([string] $name) {
    $m = $Mods[$name]
    if (-not $m) { throw "Unknown mod '$name'. One of: $($Mods.Keys -join ', ')." }
    if (-not (Test-Path $m.Dir)) { throw "No source at $($m.Dir)." }
    $out = & dotnet build $m.Dir -c Release -nologo -v:quiet 2>&1
    if ($LASTEXITCODE -ne 0) { $out | Where-Object { $_ -match 'error' } | Select-Object -Unique | Write-Host -ForegroundColor Red; throw "$name didn't build." }
    "$($m.Dir)\bin\Release\net6.0\$($m.Dll)"
}

function Push-Mod([string] $name) {
    $dll = Build-Mod $name
    $dest = "$Test\Mods\$(Split-Path $dll -Leaf)"
    Copy-Item $dll $dest -Force
    $v = (Get-Item $dest).VersionInfo.FileVersion
    $state = if (Get-Game $Test) { "the running game reloads it" } else { "loads on the next play" }
    "$(Get-Date -Format HH:mm:ss) $name $v -> test copy ($state)"
}

switch ($Command) {
    "server" {
        # @() keeps one argument an array: "$a, $b = $Rest" makes $b a string, and $b[0] its first letter.
        $sub = $Rest | Select-Object -First 1
        $args2 = @($Rest | Select-Object -Skip 1)
        $running = Get-Game $Server
        switch ($sub) {
            "up" {
                if ($running) { "Dev server already running (pid $($running.ProcessId))."; return }
                if (-not (Test-Path "$Server\Mods\DedicatedServerMod_Il2cpp_Server.dll")) { throw "No dev server at $Server (docs/DEV-WORKFLOW.md, 'Set up')." }
                if (-not (Test-Path $Save)) {
                    $world = if ($args2) { $args2[0] } else { Get-ChildItem $Worlds -Filter *.tar.zst -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName }
                    if ($world) {
                        if (-not (Test-Path $world)) { $world = Join-Path $Worlds $world }
                        & "$env:SystemRoot\System32\tar.exe" -xf $world -C "$Server\UserData"
                        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Save)) { throw "Couldn't unpack $world into $Server\UserData." }
                        "World: $(Split-Path $world -Leaf)"
                    } else { "World: a fresh one (no backups in $Worlds)" }
                }
                $a ="--batchmode --nographics --dedicated-server --server-name `"Hyland Dev`" " +
                     "--tcp-console --tcp-console-bind 127.0.0.1 --tcp-console-port $ConsolePort"
                $p = Start-Process -FilePath "$Server\Schedule I.exe" -WorkingDirectory $Server -ArgumentList $a -WindowStyle Minimized -PassThru
                "Starting the dev server (pid $($p.Id)); waiting for the world to load..."
                $deadline = (Get-Date).AddMinutes(5)
                while ($true) {
                    Start-Sleep 3
                    # Each run rewrites Latest.log from the start, so the whole file is this boot.
                    $text = Get-LogFrom $ServerLog 0
                    if ($text -match "Loaded save") { break }
                    if ($p.HasExited) { throw "The dev server exited during boot (exit $($p.ExitCode)). pwsh scripts/dev.ps1 server log" }
                    if ((Get-Date) -gt $deadline) { throw "No 'Loaded save' after 5 min. pwsh scripts/dev.ps1 server log" }
                }
                "Dev server up: 127.0.0.1:$Port. pwsh scripts/dev.ps1 play"
            }
            "down" {
                if (-not $running) { "Dev server isn't running."; return }
                $before = (Get-Item $ServerLog).Length
                Send-Console "save" | Out-Null
                $deadline = (Get-Date).AddSeconds(30)
                while (-not ((Get-LogFrom $ServerLog $before) -match "Save completed")) {
                    if ((Get-Date) -gt $deadline) { throw "The game didn't confirm a save in 30 s, so I left it running. pwsh scripts/dev.ps1 server log" }
                    Start-Sleep 1
                }
                Stop-Process -Id $running.ProcessId
                "Saved (the game confirmed it) and stopped."
            }
            "status" {
                if (-not $running) { "Dev server: not running."; return }
                "Dev server: running (pid $($running.ProcessId), since $($running.CreationDate)), 127.0.0.1:$Port"
                $log = Get-LogFrom $ServerLog 0
                "World: $(if ($log -match 'Loaded save') { 'loaded' } else { 'not loaded yet' })"
                Send-Console "listplayers"
            }
            "log" {
                $n = if ($args2) { [int]$args2[0] } else { 40 }
                Get-Content $ServerLog -Tail $n
            }
            "cmd" {
                if (-not $running) { throw "Dev server isn't running." }
                if (-not $args2) { throw "Which command? e.g. server cmd help" }
                Send-Console ($args2 -join " ")
            }
            "world" {
                if ($running) { throw "Stop the dev server first: pwsh scripts/dev.ps1 server down" }
                if (-not $args2) { throw "Which backup? One of: $((Get-ChildItem $Worlds -Filter *.tar.zst).Name -join ', ')" }
                $world = $args2[0]
                if (-not (Test-Path $world)) { $world = Join-Path $Worlds $world }
                if (-not (Test-Path $world)) { throw "No such backup: $($args2[0])" }
                if (Test-Path $Save) {
                    $aside = "$Worlds\replaced\DedicatedServerSave-$(Get-Date -Format yyyyMMdd-HHmmss)"
                    New-Item -ItemType Directory -Force "$Worlds\replaced" | Out-Null
                    Move-Item $Save $aside
                    "Old dev world moved to $aside"
                }
                & "$env:SystemRoot\System32\tar.exe" -xf $world -C "$Server\UserData"
                if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Save)) { throw "Couldn't unpack $world." }
                "Dev world is now $(Split-Path $world -Leaf). pwsh scripts/dev.ps1 server up"
            }
            default { throw "server up | down | status | log | cmd | world" }
        }
    }
    "play" {
        if (Get-Game $Test) { throw "The test copy is already running." }
        $other = Get-CimInstance Win32_Process -Filter "Name='Schedule I.exe'" | Where-Object { $_.ThreadCount -gt 0 -and -not $_.ExecutablePath.StartsWith("$Server\", [StringComparison]::OrdinalIgnoreCase) }
        if ($other) { throw "Another Schedule I is open ($($other.ExecutablePath)). Close it first: one Steam account, one game." }
        if (-not (Get-Game $Server)) { throw "The dev server isn't running: pwsh scripts/dev.ps1 server up" }
        Start-Process -FilePath "$Test\Schedule I.exe" -WorkingDirectory $Test -ArgumentList "--server-ip 127.0.0.1 --server-port $Port"
        "Started the test copy; it joins the dev server by itself. Then: pwsh scripts/dev.ps1 watch <mod>"
    }
    "push" {
        if (-not $Rest) { throw "Which mods? $($Mods.Keys -join ', ')" }
        if ($Rest -contains "hud" -and $Rest -contains "phone") { throw "hud and phone are the same DLL: pick one." }
        foreach ($name in $Rest) { Push-Mod $name }
    }
    "watch" {
        if ($Rest.Count -ne 1) { throw "Watch one mod: $($Mods.Keys -join ', ')" }
        $name = $Rest[0]
        Push-Mod $name
        $fsw = [IO.FileSystemWatcher]::new("$($Mods[$name].Dir)\src", "*.cs")
        $fsw.IncludeSubdirectories = $true
        "Watching $($Mods[$name].Dir)\src. Save a file to push; Ctrl+C stops."
        while ($true) {
            $change = $fsw.WaitForChanged([IO.WatcherChangeTypes]::All, 1000)
            if ($change.TimedOut) { continue }
            # Editors write a file in several steps: let the burst settle, then build once.
            while (-not $fsw.WaitForChanged([IO.WatcherChangeTypes]::All, 400).TimedOut) { }
            try { Push-Mod $name } catch { Write-Host "$(Get-Date -Format HH:mm:ss) $_" -ForegroundColor Red }
        }
    }
    "jointest" {
        if (-not (Get-Game $Server)) { throw "The dev server isn't running: pwsh scripts/dev.ps1 server up" }
        & "$PSScriptRoot\join-test.ps1" -TestHost 127.0.0.1 -Port $Port -Mod $Rest
        exit $LASTEXITCODE
    }
    "status" {
        $s = Get-Game $Server
        "Dev server: $(if ($s) { "running (pid $($s.ProcessId)), 127.0.0.1:$Port" } else { 'not running' })"
        "Test copy:  $(if (Get-Game $Test) { 'running' } else { 'closed' })$(if (Test-Path "$Test\Plugins\HotReload.dll") { ', HotReload installed' } else { ', NO HotReload' })"
        $live = (Get-Content "$Root\manifest\manifest.json" -Raw | ConvertFrom-Json).mods
        ""
        "{0,-8} {1,-16} {2,-12} {3}" -f "mod", "test copy", "your build", "live list"
        foreach ($name in $Mods.Keys) {
            if ($name -eq "phone") { continue }
            $m = $Mods[$name]
            $inTest = "$Test\Mods\$($m.Dll)"
            $built = "$($m.Dir)\bin\Release\net6.0\$($m.Dll)"
            $liveFile = $live.files | Where-Object { $_.path -eq "Mods/$($m.Dll)" }
            $liveMod = $live | Where-Object { $_.files.path -contains "Mods/$($m.Dll)" }
            $testHash = if (Test-Path $inTest) { (Get-FileHash $inTest).Hash.ToLower() }
            $testText = if (-not $testHash) { "-" } else { (Get-Item $inTest).VersionInfo.FileVersion + $(if ($liveFile -and $testHash -eq $liveFile.sha256) { " =live" } elseif ((Test-Path $built) -and $testHash -eq (Get-FileHash $built).Hash.ToLower()) { " =build" } else { "" }) }
            $builtText = if (Test-Path $built) { (Get-Item $built).VersionInfo.FileVersion } else { "-" }
            "{0,-8} {1,-16} {2,-12} {3}" -f $name, $testText, $builtText, $(if ($liveMod) { $liveMod.version } else { "-" })
        }
    }
    "log" {
        if (-not (Test-Path $TestLog)) { throw "No log yet: $TestLog" }
        $lines = Get-Content $TestLog
        "Log: $TestLog ($((Get-Item $TestLog).LastWriteTime))"
        $lines | Select-String "\[HotReload\]" | Select-Object -Last 15 | ForEach-Object { $_.Line }
        "--- our mods"
        $lines | Select-String "\[Hyland_" | Select-Object -Last 40 | ForEach-Object { $_.Line }
        $err = $lines | Select-String "\[ERROR\]|Exception"
        "--- errors: $($err.Count)"
        $err | Select-Object -Last 15 | ForEach-Object { $_.Line }
    }
}
