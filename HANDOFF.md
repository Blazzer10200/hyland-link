---
title: Hyland Link handoff
tags: [project, handoff]
status: active
updated: 2026-11-04
---

# Hyland Link: handoff

One page of where things stand. Older session logs (sessions 1–10, 2026-11-02) are in git history before this
commit. What changed for players: `CHANGELOG.md`. Server-side state: the schedule1-server HANDOFF (private).

## Live right now
- **Launcher 0.1.5** is `releases/latest` (2026-11-03: mod hashes cached by size + mtime, one progress event per
  percent, Latest.log head only). Installed launchers update themselves to it on their next start.
- **Mod list revision 11** (2026-11-04, overnight, nobody on): MelonLoader 0.7.3, the server mod
  (DedicatedServerClient 1.1.6), Hyland Minimap 1.2.3, **Hyland HUD 0.1.2** (0.1.1's Respawn swap reverted: it froze
  the death screen), Hyland Notifications 0.1.1, **Hyland Fixes 0.1.0** (new: drying racks drying a custom mix no
  longer hang joins; `mods/fixes/README.md`). Players get it with one Update click in the launcher.
  This exact set was tested with `scripts/join-test.ps1` against a sandbox server holding tonight's broken world
  (schedule1-server `s1ds-sandbox`): LOADED IN, no errors; the plain client gets STUCK on the same world. Not seen in
  game by eye: Minimap 1.2.3's projection rewrite, the Notifications cards beyond the tracker/badge/deal-done/police.
- **The server's save before the r9 restart failed** ("save command failed (console unreachable?)"; it stopped anyway;
  the r10 save worked). Anything between its last autosave and 19:15 may be lost; the backup taken after the stop is
  `/opt/s1ds/backups/s1-world-20261103-191532-pre-mods-r9.tar.zst`. Likely cause: the server froze ~19:10 (out of
  memory) and was killed from the host, last save 19:01 (schedule1-server's Daily note for 2026-11-03).
- The server is live with players on it. **No mod publishes or server restarts without Blazzer's go-ahead** (r11 went
  out on his "ship what you need, no questions" for the overnight run of 2026-11-04).

## Built, not shipped
| What | State | Where |
|---|---|---|
| HUD 0.2.0 (glass phone, Hyland app on key 8, rain level, the game's 7 apps redesigned like the board, the call screen) | **Back-burnered on branch `hud-phone`, needs a lot more work** (Blazzer, end of 2026-11-03). Runs in the test copy with no errors | `hud-phone` (88d7c4d), checked out at `.worktrees/hud-phone` |
| Phone design board | Published, every screen clicked through (all 7 game apps with sample data, the game's ✕ hide on chats, the Hyland app) | claude.ai artifact "Hyland phone" |

`main` and `hud-phone` are pushed. `mod-release.ps1 -Publish` refuses an unpushed tree, so push before the next mod
release.

**Dev loop (new 2026-11-04, `docs/DEV-WORKFLOW.md`):** a dev server on this PC (`C:\Games\Schedule I (dev server)`,
S1DS 1.1.6, the 12:24 rolling world from live), and the test copy joining it with HotReload 1.2.0 in `Plugins\`.
Verified: server boots in ~22 s, the test copy joins by itself in ~15 s, a HUD rebuild reloads in the running game in
~70 ms (twice in a row, HUD drawn once, 0 errors), `server down` saves with the game's confirmation. The test copy now
runs exactly the live mod list (r11, byte for byte); `dev.ps1 status` shows it. `scripts/hud-testcopy.ps1` is
superseded by `dev.ps1` (Blazzer's call whether to delete it).

## Next steps
1. **Watch for reports on r11** (death screen buttons, the notification cards, minimap markers in the right places). If
   something's off, fix it on `main` and ship the next version the same way: bump `src/AssemblyInfo.cs`, push,
   `mod-release.ps1 <mod> -Publish`, `mods.ps1 publish`.
2. **Phone: back-burnered, still needs a lot of work** (work in `.worktrees/hud-phone`; test with
   `dev.ps1 server up`, `play`, `watch phone`: saves reload into the running game). On the branch now: the home screen, the Hyland app, and the game's 7
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
3. Bringing the phone back to `main`: `git revert a25f0de` (the park) on `main`, then `git merge hud-phone` (the
   branch has 21 commits past where it was parked; it only changed `mods/hud/` and `PHONE.md`, so main's HANDOFF,
   CHANGELOG and CLAUDE.md win the merge cleanly). Keep the version at 0.2.0 if 0.1.1 went out, test with the
   checklist in `mods/hud/README.md`, ship the same way.

## Open items
- **This file is public** (tracked and pushed). Sessions 1–10 held server and network details; they're out of the
  current file but stay in history. Untracking it is Blazzer's call.
- Server-side items (the `mods.ps1 publish` ssh drop, the unseen watchdog crash path, the console's `bring`) moved to
  schedule1-server's HANDOFF "Open" on 2026-11-04. That repo is now private on GitHub (`Blazzer10200/schedule1-server`).
- Two small gaps in the three-place manifest rules: a trailing dot, space or `~` in a path segment (Windows strips
  them, which gets around the reserved-path check), and Unicode vs ASCII case folding (Python vs Rust). Change all
  three places at once, plus a server deploy.
- Back up `C:/AI Workflow/.secrets/hyland-link-updater.key` off this PC (Blazzer). Losing it means every friend
  reinstalls by hand.
- HUD 0.1.1's Respawn swap froze the death screen in game; 0.1.2 (r11) reverts it. A working Respawn needs a fix seen
  in the test copy first. Everything else in the HUD (rail, compass, clock, friends, bank-only panel) passed in game
  on 2026-11-03.
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
- A closed game can linger in the process list with no threads while Steam holds its handle. `dev.ps1` (and
  `hud-testcopy.ps1` since e150ef9) ignores those; before that launch refused with "Another Schedule I is running ()".
- Screen grabs from a non-DPI-aware PowerShell come out at 2048×1152 on this 2560×1440 screen (125% scaling).
