# Hyland Notifications

Every on-screen notification in Schedule I, redone in Hyland HUD's glass. It's a separate mod from Hyland HUD (it shares
HUD's `Art.cs`, `Ui.cs` and `Sprites.cs` as linked files), so the two ship and update on their own. Design board:
the "Hyland Notifications" canvas artifact (boards Main, Deals, Popups, Quests, Police, Alerts, Screen).

## How it works

Two ways, depending on the piece:

- **Mirror** (everything that just shows up and goes away). The game's piece gets a nested Canvas that's switched off
  (`Muffle`), so it draws nothing but its scripts, timers and sounds keep running. Each frame we read the same state the
  game shows (its labels, its `IsOpen`/`IsPlaying`, its animation) and draw our own card. We never set a value the game
  reads back (`HintDisplay` checks its own `Group.alpha`, for one). Turning a piece off just lets the game draw again.
- **Restyle in place** (screens you click: ticket, arrest notice, hospital bill). Their buttons and exit keys must stay
  the game's, so the game's own objects change: paper sprite → glass, dark ink → light, the headings printed on the
  paper drawn as text, the close button in our blue. Every change is recorded and put back on teardown.

Each surface runs on its own with its own backoff (`Backoff`): one that throws gets put back to the game's and retried
later, without taking the others down.

## What it covers

| Surface | Game piece | Where |
| --- | --- | --- |
| `Toasts` | unread badge (`HUD.UnreadMessagesPrompt`) with the unread count | top left |
| `CallCard` | incoming call (`CallNotification`) | left column, top |
| `Toasts` | `NotificationsManager` toasts; same title + icon fold into one card with ×N | left column |
| `Tracker` | the quest list (`HUD.QuestEntryContainer`): deals (face, goods, place, pay, open/closing chip), cartel deals, quests (current steps) | left column |
| `WantedStatus` | police status (`CrimeStatusUI`): investigating, under arrest, wanted, dead or alive, body search | under the compass |
| `Popups` | deal done (standard and special), new customers, cartel influence, cartel status | top center, below the status |
| `Popups` | special customers' arrival (during their cutscene, HUD hidden, so it's on the overlay layer) | bottom left |
| `CornerStack` | "Saving…"/"Game saved" (`SaveIndicator`) and hints (`HintDisplay`) | under a top-right minimap, else top right |
| `ProgressBar` | `ProgressSlider` (being cuffed, every hold-to action) | where the game's slider sits |
| `PoliceScreens` | `OffenceNoticeUI`, `ArrestNoticeScreen`, `HospitalBillScreen` | in place |

The left column stops at 60% of the screen height (or above a bottom-left minimap); the tracker folds the rest into
"+N more". `ScreenLayout` reads Hyland Minimap's own settings (corner, size, margin, offsets) once a second, so nothing
lands on the map whichever corner a player picks.

Left alone on purpose: the sleep-time screens (rank up, region unlocked, daily summary, incoming special customers), the
world `!`/`?` markers over NPCs, the main menu's disconnect notice (the server mod's), and the curfew/sleep warnings and
`+$` money popups (Hyland HUD's).

## Settings

`[HylandNotify]` in `UserData/MelonPreferences.cfg`: `Enabled` plus one switch per surface (`Tracker`, `Toasts`, `Call`,
`Wanted`, `Hints`, `Saved`, `Progress`, `Popups`, `Screens`) and `TrackerRows` (2–10, default 5). A change applies live:
everything is put back and rebuilt.

## Build

```bash
dotnet build mods/notify -c Release
```

Output: `mods/notify/bin/Release/net6.0/HylandNotify.dll`. References come from the game folder, like Hyland HUD's.

## Testing in game

The local dev loop (`docs/DEV-WORKFLOW.md`), never Blazzer's own game folder or the live server.

```bash
pwsh scripts/dev.ps1 server up
pwsh scripts/dev.ps1 play
pwsh scripts/dev.ps1 watch notify   # every save rebuilds; the running game reloads it
pwsh scripts/dev.ps1 log
```

Never single player (the test copy shares the saves with the real game). Things to make happen: take a deal and let it
get close to its window's end, finish it (deal done), get texted twice by one person, get a call, get a hint, save, get
seen by police and lose them, get cuffed (progress bar and arrest notice), get a ticket, get knocked out (hospital bill).

## Shipping a change

Bump `src/AssemblyInfo.cs` (MelonInfo and the two assembly versions), then:

```bash
pwsh scripts/mod-release.ps1 notify            # build + stage + check, nothing leaves the PC
pwsh scripts/mod-release.ps1 notify -Publish   # GitHub release notify-v<version> (not latest) + mods.ps1 add
pwsh scripts/mods.ps1 publish                  # the server installs it first, then main is pushed
```
