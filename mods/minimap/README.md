# Hyland Minimap

A HUD minimap for Schedule I (IL2CPP, MelonLoader 0.7.2+). It's a fork of nacho's
[Schedule I Minimap](https://thunderstore.io/c/schedule-i/p/nachorn/Schedule_I_Minimap/) 1.0.2 (MIT). The author
didn't publish source, so `src/` started as an ILSpy decompile of that DLL (commit `bdee77d`). Everything after that
commit is our change.

## What's different from the original

- **Smooth round edge.** Unity's `Mask` cuts the map with a hard stencil edge. Now a ring baked at its real on-screen
  pixel size (`Shapes.RimPixel`) is drawn over that edge, instead of the plain discs drawn behind it. The circle and
  arrow sprites use 4×4 supersampling and mipmaps.
- **Zoom follows the map art's resolution.** `Zoom = 1` means one pixel of the game's map per screen pixel, the
  sharpest it can look. The range is 0.6–4, and each PageUp/PageDown multiplies or divides the zoom by `ZoomStep`
  (1.25). The original default was 3.0 on the phone map's scale, which magnified the art until it blurred.
- **The Bezel look**, chosen in a design preview. Each part is a setting, and all are on by default:
  - `MapGrading`: the map art is slightly darker and cooler, so the markers stand out.
  - `Vignette`: the map shades toward the rim.
  - `ViewCone`: a soft wedge under the arrow shows where you're looking.
  - `CompassNorth`: an N badge on the border turns with the map.
  - `EdgePins`: active deals and hired dealers that are off the map show as arrows on the border, pointing at them.
  - Markers and the arrow have thin dark outlines, and portraits default to 20 instead of 24.
- **Its own identity.** It's named Hyland Minimap, the DLL is `HylandMinimap.dll`, and its settings live under
  `[HylandMinimap]` in `UserData/MelonPreferences.cfg`, so it never fights the original.

On first use it logs the map art's real size and what zoom 1 works out to, which helps with tuning.

## Build

```bash
dotnet build -c Release mods/minimap
```

It references the MelonLoader and IL2CPP assemblies in a Schedule I install. The game has to have run once with
MelonLoader. The default path is the Steam one; pass `-p:GamePath="D:\...\Schedule I"` for another. The output is
`bin/Release/net6.0/HylandMinimap.dll`.

To ship a change, bump `src/AssemblyInfo.cs`, then `pwsh scripts/mod-release.ps1 minimap -Publish` and
`pwsh scripts/mods.ps1 publish`.
