# Hyland HUD

A restyled HUD for Schedule I (IL2CPP, MelonLoader 0.7.2+): the "Glass Rail" concept (`hud-prototype.html`, Blazzer's
design). It's our own code, not a fork.

**Shipped:** 0.1.0 (`hud-v0.1.0`, id `hyland-hud`, client side) is on the server's mod list from revision 8
(2026-11-02). Players get it through the launcher's Update.

## What it does

It restyles the game's own HUD pieces in place instead of drawing copies. The hotbar slots stay the game's: drag and drop,
item icons, quantities, number keys and controller support keep working, and quest markers stay on the compass.

| Concept | Game source | How |
|---|---|---|
| Glass rail, rounded slots | `HUD.SlotContainer`, `PlayerInventory.SlotUIs` | Glass panel behind the row; the slot backgrounds get a rounded sprite and the concept's colors (`ItemSlotUI.SetNormalColor` / `SetHighlightColor`) |
| Slot gap 6 | `PlayerInventory.RepositionUI` (gap 20) | Same layout with `SlotGap`, every frame |
| Selected slot lifts | `HotbarSlot.IsSelected` | +10 units, ×1.07, 0.18 s |
| Gold underline, name pill | `ItemInstance.Name` | Slide under/over the selected slot; the game's fading `selectedItemLabel` is hidden |
| Compass strip | `CompassManager` | Notches hidden, N/E/S/W strip (160 units per 90°) in the compass `Container` (not the notches' `NotchContainer`, whose RectMask2D cuts everything to a 30-unit strip); `CompassUIRange`/`AngleDivisor` set to match, markers re-placed on the smoothed heading and faded at the ends |
| Clock pill | `TimeManager.CurrentTime`, `IsNight`, `Get12HourTime` | Day / dusk (17:00–18:00) / night icon, crossfade 0.3 s |
| Bank panel | `MoneyManager.onlineBalance` | 0.4 s count-up, right of the rail where the game's bank slot (`OnlineSlot`, `HUD.onlineBalanceSlotUI`) was; that slot is hidden. Cash isn't repeated: the game's cash slot shows it |
| Idle fade | slot changes, mouse buttons, scroll, gamepad bumpers/triggers/d-pad | 55% after 4 s, back in 0.12 s; never while an inventory screen is open. Walking and looking around don't count |
| HUD hide | `HUD.canvas.enabled`, `CompassManager.SetVisible` | Inherited: everything lives in the game's own containers |

Beyond the concept:

| Feature | Game source | How |
|---|---|---|
| Curfew and sleep warnings | `CurfewManager.IsEnabled` / `IsCurrentlyActive` (curfew 21:00–05:00), `TimeManager.CurrentTime` (time stops at 04:00 until everyone sleeps) | A tag on the clock pill (`TimeWarnings.For`): "CURFEW 25M" amber from 20:00, "CURFEW" red, "SLEEP 45M" from 03:00, "SLEEP NOW" at 04:00. The game's own `HUD.CurfewPrompt` and `SleepPrompt` say the same thing, so they're switched off (their graphics, since the game toggles the objects every frame) while the pill is on screen. The game hides the compass in menus and on the phone; then its own prompts come back, so a warning is never lost |
| Friends on the compass | `Player.PlayerList`, `Player.PlayerName`, `UnitsUtility.FormatShortDistance` | A cyan dot (the minimap's co-op color) with "Name · 140m" under it per co-op player, clamped to the strip's ends when behind you; metres or feet follow the game's setting. The dedicated server's own headless player (still named the class default "Player", since it has no Steam login) is skipped |
| Respawn on the server | `DeathScreen.CanRespawn` (= `Lobby.IsInLobby`) | The game shows Respawn only in a Steam lobby; a server client joins by IP, so it got "Load last save", which exits to the menu and drops you off the server. A Harmony postfix on `DeathScreen.Open` (`DeathScreenFix`) shows Respawn and hides Load last save whenever you're a client of someone else's server. Single player and co-op hosts keep the game's choice. Not tied to `Enabled`: it's a fix, not a style |

The phone (0.2.0; the game's layout and our design are in `PHONE.md`):

| Feature | Game source | How |
|---|---|---|
| Glass phone | `HomeScreen` (`Background`, `InfoBar`, `AppIcons` grid, each `AppIcon`), `Phone.phoneModel` | `PhoneSkin`: restyled in place, like the HUD. Dark wallpaper with rings, a clock card above a 4-column grid of glass tiles with our glyphs (other mods' apps keep their own art in a tile), white badge pills the game's red tints, a 1–9 chip per tile in `HomeScreen.appIcons` order (what the number keys open), and the `phone_Case` material recolored on a copy (only your phone). Motion when the phone comes up: wake from black 0.38 s, card drop 0.42 s, tiles pop 0.6→1.06→1 staggered 40 ms, badges pop once; a pressed tile squishes. Off with `GlassPhone` or `Enabled` |
| Clock card | `TimeManager`, `EnvironmentManager.GetCurrentWeather`, `MoneyManager.onlineBalance`, `Player.PlayerList` | Time and day, a cloud with falling drops when the current weather id has "Rain", the bank, and initials plus a count of who's online (the server's headless player skipped, as on the compass). Four times a second |
| Hyland app | `App<T>`, `HomeScreen.GenerateAppIcon`, `Phone.closeApps`, `GameInput.RegisterExitListener` | `HylandApp`: built the way the game builds an app without being one. An icon from the game's icon prefab (key 8, controller-selectable), a 655×1201 screen in `AppsCanvas` turned 90° like Messages, and the game's open/close rules: Escape closes it first, so does clicking the phone's button, and the game can close it before opening another app. Zooms out of its icon 0.34 s. A ScrollRect holds the switches (`Enabled`…`Friends`, `GlassPhone`, `KeyHints`), the slot gap, rain volume with a meter, and "Much quieter indoors". Changes write the cfg 0.5 s after the last one. Stays up with the HUD off, so you can turn it back on |
| Rain level | `RainController._audioSources` (`Rain`, `RainRoof`), `AudioSourceController.VolumeMultiplier`, `EffectController._enclosureBlend` | `RainLevel`: the game rewrites each rain source's multiplier from its weather profile when you go in or out, so this keeps the game's last value and plays it × `RainVolume`, × a slide to 35% as the enclosure blend goes indoors (`QuieterIndoors`). The game's Weather slider still applies. Off puts the game's values back. Not tied to `Enabled` |

Left out on purpose: deal countdowns. The game's HUD quest list already shows "(Expires in N hrs)" on tracked deals, red under 2 hours (`Contract.UpdateTiming`), and replacing it would also drop the quest objectives that share that list.

Turning it off (`Enabled = false`) or any error puts the game's HUD back exactly (every change is recorded and restored),
then retries after 5, 10, 20, 40, then 60 s.

### Where it differs from the concept

- **No backdrop blur** (uGUI can't blur what's behind it without a custom shader); the glass is a little darker instead.
- **The game's font**, not Barlow: a TMP font needs a Unity-built asset bundle to load. Sizes and spacing follow the concept.
- **The cash slot stays in the rail** (key 9/0, drag target, needed to hand over cash), after the separator, showing the
  cash amount as the game draws it, so the panel next to it shows only the bank. `HUD.onlineBalanceContainer` is the
  whole `CashSlotContainer` (cash slot and bank slot), so only the bank slot inside it is hidden. `MoneyPanel = false`
  drops the panel and brings the game's bank slot back.
- **The "+$120" float is the game's own** popup (over the cash slot), not a second one.
- **Hide/show is instant**, as the game toggles its canvas; the concept's 200 ms slide can't be hooked without patching.
- Item icons are the game's item art, not the concept's line icons.

## Settings

The Hyland app on the phone, or `[HylandHud]` in `UserData/MelonPreferences.cfg`: `Enabled`, `Hotbar`, `SlotGap` (6),
`MoneyPanel`, `IdleFade`, `IdleSeconds` (4), `IdleOpacity` (0.55), `Compass`, `CompassSmoothing` (0.08 s), `Clock`,
`TimeWarnings`, `Friends`, `GlassPhone`, `KeyHints`, `RainVolume` (100), `QuieterIndoors` (on).
`Clock`, `TimeWarnings` and `Friends` live on the compass strip, so they need `Compass` on. The hotbar, the compass,
the glass phone, the Hyland app and the rain level fail and retry separately: a problem in one leaves the others up.
Phone settings rebuild only the phone, so the app stays open while you flip them.

## Build

```bash
dotnet build -c Release mods/hud
```

Same requirements as the minimap (a Schedule I install that has run MelonLoader once; `-p:GamePath=...` for another
path). Output: `bin/Release/net6.0/HylandHud.dll`.

Preview the generated sprites in the concept's layout, without the game (text is left out, it's the game's font).
It writes `hud-layout.png`, `hud-sprites.png` and `hud-phone.png` (the home screen and the Hyland app at the game's
655×1201):

```bash
dotnet run --project mods/hud/preview -c Release -- <output folder>
```

## Testing in game

Never in Blazzer's own game folder (it's the co-op copy). There's a test copy at `C:\Games\Schedule I (HUD test)`: the
whole game folder plus a `steam_appid.txt` (3164500), so its `Schedule I.exe` runs from there (Steam open) instead of
Steam starting the real install. Copy a fresh `bin/Release/net6.0/HylandHud.dll` into its `Mods/` (with the game closed),
launch the exe, and **join the server**: both copies share the save folder in `AppData\LocalLow`, so a single-player
test would write to Blazzer's real saves.

For the phone (0.2.0), check: open it (wake, card, tiles pop), 1–9 chips match what the keys open, badges on
Messages/Deliveries, key 8 opens the Hyland app, Escape and Back close it, the phone's button closes it, the wheel
scrolls the list, a switch flips the HUD live, "Glass phone" off gives the game's home screen back, and rain outside vs
inside with "Much quieter indoors" on and off. The phone logs a line per build too ("Phone: home …", "Phone: Hyland
app is icon N").

Each session the mod logs one line about the hotbar and one about the compass (slot size, parents, canvas scale, which
bank readout it hid) to that copy's `MelonLoader/Latest.log`. When a screenshot shows something out of place, log the
real prefab with `Diagnostics.Chain`/`Children` in the skin's constructor. That's how the first run's two bugs were
found: the compass's `RectMask2D` and the cash slot's shared container.

## Shipping a change

Bump `src/AssemblyInfo.cs` (MelonInfo and the two assembly versions), then:

```bash
pwsh scripts/mod-release.ps1 hud            # build + stage + check, nothing leaves the PC
pwsh scripts/mod-release.ps1 hud -Publish   # GitHub release hud-v<version> (not latest) + mods.ps1 add
pwsh scripts/mods.ps1 publish               # the server installs it first, then main is pushed
```
