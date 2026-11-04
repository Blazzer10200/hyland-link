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
| HUD 0.2.0 (glass phone, Hyland app on key 8, rain level, the game's 7 apps redesigned like the board, the call screen) | **App-by-app pass against the board under way** (2026-11-04, Claude hands-off via the driver): Messages, Hyland app, Contacts, Deliveries done; paused at Dealers | `hud-phone` (a7eeee3), checked out at `.worktrees/hud-phone` |
| Phone design board | Published, every screen clicked through (all 7 game apps with sample data, the game's ✕ hide on chats, the Hyland app) | claude.ai artifact "Hyland phone" |
| Label-width fixes (TMP measures a never-active label as ~8 units; widths were measured only on text change) | **Clock pill** (`SLEEP NOW` drawn over `4:00 AM`): fixed and seen in the test copy (7cd4960). **Bank panel, hotbar name pill, Notify tracker count pills + deal chip**: same fix, builds clean, **not seen in game** (79fa71e). No version bumps yet | `main` |
| Linked navigation concept (compass + minimap share one target list, no duplicate icons) | Planning only, nothing in code. Boards: live demo (Today r11 vs Linked, focus, contexts, cluster layout) matched to the real 0.1.2 HUD from a test-copy screenshot, plus a rules sheet | "Hyland HUD" artifact, **Navigation** page (`Nav.dc.html`, `NavRules.dc.html`) |

**Unpushed (local commits only, 2026-11-04):** `main` has d2a9634, fc26000, 9387f2e, a6b9d01, 4d0f135 (the driver), 7cd4960, 79fa71e (the width fixes);
`hud-phone` has 6395e61, 9c706e8, 058b6d0, a7eeee3 (the app pass). `mod-release.ps1 -Publish` refuses an unpushed
tree, so push before the next mod release.

**Driver (`tools/driver`, test copy only):** `dev.ps1 drive "phone open; click Deliveries; wait 1.5; shot d1 /phone/AppsCanvas"`
opens, clicks, dumps (`tree <path> <depth>`) and screenshots the game without the mouse. Gotchas: `back` from an app's
top level closes the phone; `phone open` reopens the last app; an ambiguous match needs a longer `/path` or `#n`.

**Dev loop (new 2026-11-04, `docs/DEV-WORKFLOW.md`):** a dev server on this PC (`C:\Games\Schedule I (dev server)`,
S1DS 1.1.6, the 12:24 rolling world from live), and the test copy joining it with HotReload 1.2.0 in `Plugins\`.
Verified: server boots in ~22 s, the test copy joins by itself in ~15 s, a HUD rebuild reloads in the running game in
~70 ms (twice in a row, HUD drawn once, 0 errors), `server down` saves with the game's confirmation. The test copy now
runs exactly the live mod list (r11, byte for byte); `dev.ps1 status` shows it. `scripts/hud-testcopy.ps1` is
superseded by `dev.ps1` (Blazzer's call whether to delete it).

## Next steps
- **Linked navigation (planning, picked back up later).** Today one accepted deal shows in up to 5 places (Notify card,
  game compass icon, game map POI at the drop, our clock badge on the customer, our edge pin). Proposed: one target
  list, map shows what's in range, compass shows what isn't, only the focused objective in both; edge pins go away.
  Next: design options for (a) the focused objective's look and (b) telling a deal from a quest on the compass (both
  are the game's blue star disc). Open decision for Blazzer: fold the minimap into Hyland HUD (one scan, one settings
  section; needs `[HylandMinimap]` migration and the phone branch merged first) vs a shared `UserLibs/` library.
  Unverified on the board: the game's compass icon for a deal (assumed the same star disc).
- **Check the 79fa71e width fixes in game** when the dev loop is up: hide the HUD (phone open), `push hud`/`push notify`,
  close it, look at the bank panel, item name pill and tracker headers.
0. **Blazzer's own game was still on r10** at 12:22 on 2026-11-04 (no `HylandFixes.dll`): one Update click in the
   launcher before joining live.
1. **Watch for reports on r11** (death screen buttons, the notification cards, minimap markers in the right places). If
   something's off, fix it on `main` and ship the next version the same way: bump `src/AssemblyInfo.cs`, push,
   `mod-release.ps1 <mod> -Publish`, `mods.ps1 publish`.
2. **Phone app pass, resume at Dealers** (2026-11-04). Done and committed: Messages chat + header strip + compose bar
   + counter-offer and deal-time sheets; the Hyland app's blank rows (RectMask2D can't clip a 90°-turned screen, so a
   stencil Mask); scrollbars hidden everywhere; Contacts card; Deliveries (tabs on top: the game's container lays out
   in reverse; 2-column shops: two cells were 6 units wider than the 1099 grid; side card full height beside the
   header; − + at full size, they sit at 0.5 scale; Place order's label muted while it can't order).
   **Dealers, measured, not yet edited:** the column is 1048 tall and the 10 customer rows overflow it by ~100 units
   (10 × 55 + gaps). Plan: rows 55 → 48 units, row gap 6 → 4 board px, column spacing 10 → 8; ✕ as an ~18–20 px chip
   (rows are only ~23 px); the inventory entry "14x Granddaddy Smegma" shrinks under best-fit (cells 259×41, 2
   columns): set a sane minimum size or let a long entry take the full row. Then Journal (empty in the dev world),
   Products, Map, the Hyland app's SOUND section, home screen and call screen, the tearing hunt, a final error-log pass.
   Order receipts stay 215 tall on purpose (up to 8 items in 4 rows; a grid can't size per card).
   Older notes: (work in `.worktrees/hud-phone`; test with
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
- `scripts/hud-testcopy.ps1` is superseded by `dev.ps1` (header says so). Deleting it is Blazzer's call.
- `dist/hud-v0.1.1/` is a superseded local zip (gitignored, the release stays on GitHub). Deleting it is Blazzer's call.
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
- **TMP `preferredWidth` on a label that has never been active returns a few units.** Measure when
  `isActiveAndEnabled`, not when the text is set (7cd4960). Anything measured only on text change sticks wrong.
- **`dev.ps1 push` of a byte-identical DLL doesn't reload** (no "Reloaded" line in `Latest.log`): change something
  before an A/B push, or the "after" is still the old instance.
- **After a Schedule I update** the dev server and the test copy are stale copies of the old build: redo
  `docs/DEV-WORKFLOW.md` "Set up" step 1 for both, or joins fail on a version mismatch. Our mod projects compile
  against the real install, so they pick the update up by themselves.
- HotReload keeps each old build in memory (~the DLL's size per reload): restart the test copy after a long session.
- In `dev.ps1`, `$a, $b = $Rest` made `$b` a string with one argument (`$b[0]` = its first letter); `@($Rest |
  Select-Object -Skip 1)` keeps it an array. Watch for the same PowerShell destructuring trap in new scripts.
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
