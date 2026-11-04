---
title: Hyland Link handoff
tags: [project, handoff]
status: active
updated: 2026-11-03
---

# Hyland Link: handoff

One page of where things stand. Older session logs (sessions 1–10, 2026-11-02) are in git history before this
commit. What changed for players: `CHANGELOG.md`. Server-side state: the schedule1-server HANDOFF (private).

## Live right now
- **Launcher 0.1.4** is `releases/latest`. Installed launchers update themselves to it.
- **Mod list revision 8**: MelonLoader 0.7.3, the server mod (DedicatedServerClient 1.1.6), Hyland Minimap 1.2.2,
  Hyland HUD 0.1.0.
- The server is live with players on it. **No mod publishes or server restarts without Blazzer's go-ahead.**

## Built, not shipped
| What | State | Where |
|---|---|---|
| HUD 0.1.1 (Respawn instead of "Load last save" on the server) | On `main`, staged, **not seen in game**, not published | `dist/hud-v0.1.1/HylandHud.zip` |
| HUD 0.2.0 (glass phone, Hyland app on key 8, rain level, the game's 7 apps redesigned like the board, the call screen) | **Back-burnered on branch `hud-phone`, needs a lot more work** (Blazzer, end of 2026-11-03). Runs in the test copy with no errors | `hud-phone` (9bcdf6b), checked out at `.worktrees/hud-phone` |
| Phone design board | Published, every screen clicked through (all 7 game apps with sample data, the game's ✕ hide on chats, the Hyland app) | claude.ai artifact "Hyland phone" |

`main` is ahead of `origin/main` (nothing pushed since revision 8). `mod-release.ps1 -Publish` refuses an unpushed
tree, so pushing comes first when a HUD ships.

## Next steps
1. **See 0.1.1 work once in the test copy** (join the server, never single player). The test copy has the phone build
   right now: `pwsh scripts/hud-testcopy.ps1 use main` with the game closed, `launch`, die once, and the death screen
   should offer Respawn, not "Load last save".
2. Ship 0.1.1 on Blazzer's go-ahead: push `main`, `pwsh scripts/mod-release.ps1 hud -Publish`,
   `pwsh scripts/mods.ps1 publish` (the server applies it first, waiting until nobody's online unless `-Now`). Live =
   the launcher log says `Mod list: revision 9 from Online`. Then `dist/hud-v0.1.0` can go (Blazzer's OK).
3. **Phone: back-burnered, still needs a lot of work** (work in `.worktrees/hud-phone`; test with
   `hud-testcopy.ps1 use phone`, `launch`, `log`). On the branch now: the home screen, the Hyland app, and the game's 7
   apps redesigned the HUD way, one skin per app (`GameAppSkin` + `MessagesSkin`, `DeliveriesSkin`, `DealersSkin`,
   `JournalSkin`, `ProductsSkin`, `ContactsSkin`; `mods/hud/PHONE.md` "What we built"), with zoom in/out of the icon.
   Seen and accepted in game on 2026-11-03: the Messages list (rows, previews, the ✕ hiding a chat) and the home
   screen's icon names and number keys. **Not yet checked against the board in game:** the Messages chat and its
   popups, Deliveries, Dealers, Journal, Products, Contacts, Map, the Hyland app, and the frame "tearing" Blazzer saw
   (never pinned down). Known gaps: the chat strip is empty for dealers, bubbles keep the game's big gaps, Products'
   detail toggles are plain.
   **How to run the next pass:** turn on computer use in the Claude desktop app (Settings → This computer → Computer
   use; tools load at session start, so start a fresh session after). Then drive the test copy yourself: open every
   app, screen and popup, screenshot each one, compare with the board (`Phone.dc.html` on the "Hyland HUD" artifact),
   fix, repeat. Navigate only: no deals accepted, nothing bought or sent (the save is shared with the server). The
   last few rounds of "Blazzer screenshots, I guess" were slow; checking the board and the game's code yourself first
   finds most of it (e.g. the cramped icon names were the game's label box sitting 3 px inside the tile).
4. Bringing the phone back to `main`: `git revert a25f0de` (the park) on `main`, then `git merge hud-phone` (the
   branch has 21 commits past where it was parked; it only changed `mods/hud/` and `PHONE.md`, so main's HANDOFF,
   CHANGELOG and CLAUDE.md win the merge cleanly). Keep the version at 0.2.0 if 0.1.1 went out, test with the
   checklist in `mods/hud/README.md`, ship the same way.

## Open items
- **This file is public** (tracked and pushed). Sessions 1–10 held server and network details; they're out of the
  current file but stay in history. Untracking it is Blazzer's call.
- `mods.ps1 publish` runs `ssh -t` for the server's whole wait-until-empty step, so a dropped connection can kill
  `s1ds mods apply` midway. The fix belongs in schedule1-server (run the apply detached, poll for the result).
- Two small gaps in the three-place manifest rules: a trailing dot, space or `~` in a path segment (Windows strips
  them, which gets around the reserved-path check), and Unicode vs ASCII case folding (Python vs Rust). Change all
  three places at once, plus a server deploy.
- Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (Blazzer). Losing it means every friend
  reinstalls by hand.
- The server watchdog fix (schedule1-server) is live, but its crash path hasn't been seen firing yet.
- The server console's `bring <player> <player>` answers "Brought …" but the player doesn't move: the game teleports
  only the local player (`PlayerMovement.Teleport`, no RPC), so the server moves its own copy. A server mod gap, not
  ours. Names with non-ASCII (BLAZZER™) don't resolve over the console either: use the Steam ID from `listops`.
- HUD 0.1.1's Respawn hasn't been seen yet; everything else in it (rail, compass, clock, friends, bank-only panel)
  passed in game on 2026-11-03.
- A hotbar missing in a test screenshot was the game, not the HUD: at the 4 AM pass-out, on arrest and on death,
  `Player.ExitAll` / `OnDied` call `SetInventoryEnabled(false)`, which hides `HotbarContainer` and the cash slot with it.
## Gotchas worth keeping
- **"Live" means a launcher read it:** the raw manifest is cached up to 5 min per CDN edge and query strings don't
  bypass it. Check `bash scripts/logs.sh` or wait 5 min after the push.
- **Changing a setting's default doesn't reach existing players:** MelonPreferences keeps saved values. Flip a
  default with a one-time migration (a `SettingsVersion` entry, as the minimap does), not by editing the default.
- Never edit `UserData/MelonPreferences.cfg` while the game runs: MelonLoader rewrites it on exit.
- Testing the real launcher exe while the installed one runs: start it with
  `WEBVIEW2_USER_DATA_FOLDER=%TEMP%\hl-test-webview` too, or the DevTools flag is ignored (shared WebView2 profile).
- `game-log.ts` stage detection matches DedicatedServerClient 1.1.6 log strings: recheck them on a server mod update.
- `minimap-v1.2.0`'s tag points at the pre-1.2.0 source (released before the push); the zip is right.
- MelonLoader #1181 (no load on Win 11 25H2 in compat mode): rename `version.dll` to `winhttp.dll`.
- Il2CppInterop: `GetComponentInParent<T>(true)` throws a VerificationException; walk the parents by hand.
- The game's legacy `Text` drops any line that doesn't fit its rect (OpenSans needs ~1.36× the font size per line),
  so restyled labels that grow need `verticalOverflow = Overflow`.
- A closed game can linger in the process list with no threads while Steam holds its handle. `hud-testcopy.ps1
  launch` now ignores it (e150ef9); before that it refused with "Another Schedule I is running ()".
- Screen grabs from a non-DPI-aware PowerShell come out at 2048×1152 on this 2560×1440 screen (125% scaling).
