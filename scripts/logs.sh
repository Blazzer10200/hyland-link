#!/usr/bin/env bash
# Everything needed to debug Hyland Link on this PC in one go: the launcher's own log, the game's MelonLoader log,
# and the settings the launcher writes into the game folder. Read-only.
#
#   scripts/logs.sh        last 60 lines of each log
#   scripts/logs.sh 200    last 200 lines of each

set -u
lines="${1:-60}"

unix() { cygpath -u "$1" 2>/dev/null || printf '%s\n' "$1"; }
section() { printf '\n=== %s ===\n' "$1"; }

launcher_log="$(unix "${LOCALAPPDATA:-$HOME/AppData/Local}")/com.blazzer.hylandlink/logs/launcher.log"

section "Launcher log ($launcher_log)"
if [ -f "$launcher_log" ]; then
    tail -n "$lines" "$launcher_log"
else
    echo "(none yet: the launcher writes it from v0.1.1 on)"
fi

# The launcher logs every game folder it finds; fall back to Steam's default library.
game=""
[ -f "$launcher_log" ] && game="$(grep -o 'Game folder: [A-Za-z]:.*' "$launcher_log" | tail -n 1 | sed 's/^Game folder: //; s/\r$//')"
[ -n "$game" ] || game='C:\Program Files (x86)\Steam\steamapps\common\Schedule I'
game="$(unix "$game")"

section "Game folder: $game"
if [ ! -d "$game" ]; then
    echo "(not found)"
    exit 0
fi

marker="$game/UserData/HylandLink.json"
if [ -f "$marker" ]; then
    echo "Installed by Hyland Link: $(tr -d '\r\n ' < "$marker")"
else
    echo "Installed by Hyland Link: nothing (no UserData/HylandLink.json)"
fi
loader_cfg="$game/UserData/Loader.cfg"
if [ -f "$loader_cfg" ]; then
    console_setting="$(grep -m1 -E '^\s*hide_console' "$loader_cfg" | tr -d '\r')"
    echo "Console window: ${console_setting:-hide_console not set}"
else
    echo "Console window: Loader.cfg missing"
fi

game_log="$game/MelonLoader/Latest.log"
section "Game log ($game_log)"
if [ -f "$game_log" ]; then
    # Counted like the launcher's log sheet: tagged lines only (an exception's stack belongs to its [ERROR] line).
    echo "Errors: $(grep -ciE '\[(error|fatal)\]' "$game_log")  Warnings: $(grep -ciE '\[warn(ing)?\]' "$game_log")"
    tail -n "$lines" "$game_log"
else
    echo "(none: the game hasn't run with MelonLoader yet)"
fi
