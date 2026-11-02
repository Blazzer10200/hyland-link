# Hyland HUD

A restyled HUD for Schedule I (IL2CPP, MelonLoader 0.7.2+): the "Glass Rail" concept (`hud-prototype.html`, Blazzer's
design). It's our own code, not a fork.

**Not shipped yet.** Built and previewed, never run in game. See "Before the first release" below.

## What it does

It restyles the game's own HUD pieces in place instead of drawing copies. The hotbar slots stay the game's: drag and drop,
item icons, quantities, number keys and controller support keep working, and quest markers stay on the compass.

| Concept | Game source | How |
|---|---|---|
| Glass rail, rounded slots | `HUD.SlotContainer`, `PlayerInventory.SlotUIs` | Glass panel behind the row; the slot backgrounds get a rounded sprite and the concept's colors (`ItemSlotUI.SetNormalColor` / `SetHighlightColor`) |
| Slot gap 6 | `PlayerInventory.RepositionUI` (gap 20) | Same layout with `SlotGap`, every frame |
| Selected slot lifts | `HotbarSlot.IsSelected` | +10 units, ×1.07, 0.18 s |
| Gold underline, name pill | `ItemInstance.Name` | Slide under/over the selected slot; the game's fading `selectedItemLabel` is hidden |
| Compass strip | `CompassManager` | Notches hidden, N/E/S/W strip (160 units per 90°) in the same container; `CompassUIRange`/`AngleDivisor` set to match, markers re-placed on the smoothed heading and faded at the ends |
| Clock pill | `TimeManager.CurrentTime`, `IsNight`, `Get12HourTime` | Day / dusk (17:00–18:00) / night icon, crossfade 0.3 s |
| Cash + bank panel | `MoneyManager.cashBalance`, `onlineBalance` | 0.4 s count-up; the game's bank readout is hidden |
| Idle fade | keyboard, mouse buttons, scroll | 55% after 4 s, back in 0.12 s; never while an inventory screen is open |
| HUD hide | `HUD.canvas.enabled`, `CompassManager.SetVisible` | Inherited: everything lives in the game's own containers |

Turning it off (`Enabled = false`) or any error puts the game's HUD back exactly (every change is recorded and restored),
then retries after 5, 10, 20, 40, then 60 s.

### Where it differs from the concept

- **No backdrop blur** (uGUI can't blur what's behind it without a custom shader); the glass is a little darker instead.
- **The game's font**, not Barlow: a TMP font needs a Unity-built asset bundle to load. Sizes and spacing follow the concept.
- **The cash slot stays in the rail** (key 9/0, drag target, needed to hand over cash), after the separator, so cash
  also shows in the money panel. `MoneyPanel = false` drops the panel and brings the game's bank readout back.
- **The "+$120" float is the game's own** popup (over the cash slot), not a second one.
- **Hide/show is instant**, as the game toggles its canvas; the concept's 200 ms slide can't be hooked without patching.
- Item icons are the game's item art, not the concept's line icons.

## Settings

`[HylandHud]` in `UserData/MelonPreferences.cfg`: `Enabled`, `Hotbar`, `SlotGap` (6), `MoneyPanel`, `IdleFade`,
`IdleSeconds` (4), `IdleOpacity` (0.55), `Compass`, `CompassSmoothing` (0.08 s), `Clock`.

## Build

```bash
dotnet build -c Release mods/hud
```

Same requirements as the minimap (a Schedule I install that has run MelonLoader once; `-p:GamePath=...` for another
path). Output: `bin/Release/net6.0/HylandHud.dll`.

Preview the generated sprites in the concept's layout, without the game (text is left out, it's the game's font):

```bash
dotnet run --project mods/hud/preview -c Release -- <output folder>
```

## Before the first release

1. Run it once in a test copy of the game (not Blazzer's co-op folder). Each session it logs one line about the real
   hotbar and compass layout (slot size, parents, canvas scale): check the rail wraps the slots, the cash slot sits
   inside it, and the strip lines up with quest markers.
2. Ship like the minimap: bump `src/AssemblyInfo.cs`, build, zip `Mods/HylandHud.dll` with forward-slash entry paths,
   `gh release create hud-v<version> ... --latest=false`, `mods.ps1 add <asset URL> -Id hyland-hud ...`, `publish`.
