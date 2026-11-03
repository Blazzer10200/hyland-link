# The game's phone: what the redesign has to cover

Read off the live build (25796331) with `scripts/prefab-tree.mjs GameObject/Player.prefab Phone` on an AssetRipper
export (`scripts/game-assets.ps1 export`), plus the phone code in `.gameref/src`. Sizes are canvas units. The design
lives on the "Hyland HUD" canvas, board "Phone: home screen + Hyland app".

## Shell

- `Phone` (`Player.prefab`): a 3D model `phone` with meshes `Case`, `Indent`, `Button`, `Camera`, `Camera.001`,
  `Screen`. The case is the `phone_Case` material (gray 0.64, metallic 0.2, smoothness 0.2), so recoloring the body is
  one material color; a new shape would need our own model.
- Two world-space canvases on the model, both with a CanvasScaler (reference 800×600):
  - `HomeScreen` 655×1201, upright.
  - `AppsCanvas` 1201×655, sideways. Upright apps sit in it rotated 90°.
- Opening and closing are the game's own (`Phone.SetIsOpen`, a 0.1 s rotation to landscape, the look-offset slide).
  The home canvas hides 0.25 s after the phone closes.
- The phone has a flashlight with its own key (`Phone.ToggleFlashlight`). No screen UI.

## Home screen (`HomeScreen`)

| Object | What it is |
|---|---|
| `Background` | Full screen minus the 40-unit bar, `#676767`, masks its child |
| `Background/Image` | `Guilloche` sprite (the line pattern), `#535353`, 5000×751, keep aspect |
| `InfoBar` | 40 tall across the top: `Image` `#323232`, a `Vertical gradient` shadow 30 tall under it (`#000000` at 40%) |
| `InfoBar/Time` | Text, OpenSans-SemiBold 28, white, middle-left, 300 wide. Set every in-game minute to `Get12HourTime(CurrentTime) + " " + CurrentDay`, e.g. "3:14 PM Wednesday" |
| `InfoBar/Data` | Text "5G", OpenSans-SemiBold 28, white, middle-right. Static |
| `AppIcons` | GridLayoutGroup: cells 190×190, spacing 15 × 60, 3 columns, upper-left |
| `CallPrompt` | Incoming call, see below. Inactive until a call |

Behavior (`HomeScreen.cs`):
- Icons are made at runtime from the `AppIcon` prefab, one per app, in the order the apps start.
- **Keys 1–9 open the first nine apps** in grid order, while the phone is open and taking input.
- Keyboard and controller selection go through `UIScreen` / `UIContentPanel` / `UISelectable`: an icon can be selected
  without the mouse, and its `Outline` shows it (button colors below).

## App icon (`AppIcon.prefab`), 200×200

| Object | What it is |
|---|---|
| root | Button, color transition on `Outline`: normal clear, hover/selected `#F5F5F5`, press clear, no fade. `UISelectable` |
| `Shade` | `Rect shade` sliced, white at 61%, 30 bigger than the icon (the drop shadow) |
| `Outline` | `Rectangle_RoundedEdges` sliced, white: the hover/selected ring |
| `Mask/Image` | The app's icon sprite (`appicon_*`, 512×512 PNG), masked to the rounded rect, 20 in from the edge |
| `Notifications` | 60×60 red `#FF4949` circle, top right; `Text` OpenSans-Bold 36 white. Shown when the count > 0 |
| `Label` | OpenSans-SemiBold 32, best fit 3–32, white, upper-center, under the icon |

## Apps

| # | Object | Icon label | Title (`AppName`) | Orientation | Badge |
|---|---|---|---|---|---|
| 1 | `Messages` | Messages | Messages | **upright** | unread conversations |
| 2 | `Journal` | Journal | Journal | sideways | |
| 3 | `ProductManagerApp` | Products | My Products | sideways | |
| 4 | `ContactsApp` | Contacts | Contacts | sideways | |
| 5 | `MapApp` | Map | Map | sideways | |
| 6 | `DealerManagement` | Dealers | Dealer Management | **upright** | |
| 7 | `DeliveryApp` | Deliveries | Deliveries | sideways | active deliveries |

Order and numbering follow the screenshot and the grid; the number keys use the same order. Only Messages and
Deliveries ever set a badge (`SetNotificationCount`). Products, Map, Dealers and Deliveries are hidden in the
tutorial (`AvailableInTutorial = 0`). Sideways apps rotate the phone (`Phone.SetIsHorizontal`, 0.1 s).

Hyland is app 8: upright like Messages, key 8.

## Incoming call (`HomeScreen/CallPrompt`)

- Full screen `#3C3C3C`, caller name OpenSans-SemiBold 56 white, 150 from the top.
- A 250 round avatar: white ring, a `#B4B4B4` circle mask, the caller's picture (`Blank_Avatar_Icon` by default).
- Decline (left) and Answer (right): 160 circles, `#E56359` and `#7AE559`, a phone glyph, labels "Decline"/"Answer"
  OpenSans-SemiBold 32 under them.
- Ring: `cell_phone_dial_03` twice (pitch 1.1 and 0.8), UI audio, base volume 0.5.

## Rain (for the Sound section of the Hyland app)

- One `RainController` with two sources, both of audio type **Weather** (6), so the game's own **Settings → Audio →
  Weather** slider already scales them:
  - `Rain`: `HeavyRainSFX` / `LightRainSFX`, base 0.85.
  - `RainRoof`: `RainOnRoofHeavy`.
- The weather profile switches levels by where you are:

  | Setting | Volume | Low-pass |
  |---|---|---|
  | `RainOutside` | 0.4 | none (22 kHz) |
  | `RainInside` | 0.25 | 1,777 Hz |
  | `RainRoofInside` | 0.05 | 3,507 Hz |
  | `RainRoofOutside` | 0.001 | 10 Hz |

  Indoors is only about 4 dB quieter than outdoors, which is why rain sounds loud inside.
- Our fix: a rain level of our own, plus a much bigger indoor drop, both through `AudioSourceController.VolumeMultiplier` on the
  two rain sources. The Weather slider keeps working on top.
