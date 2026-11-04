# Hyland Driver (test copy only, never shipped)

Lets Claude Code work the test copy while Blazzer plays something else: no real mouse, keyboard or window focus. It
reads commands from the test copy's `UserData\Driver\cmd.txt`, does them inside the game the way the game's own
input would (the phone key's `GameplayMenu` path, Escape's exit listeners, pointer events on the UI), and answers in
`out.txt`. Screenshots are the game's own frame (`ScreenCapture`), so nothing else on the screen ends up in them.

It also keeps the test copy cheap and out of the way: windowed at 1600x900, off screen (it still renders, so shots
work; `window show` brings it on screen), muted, 20 fps, running while unfocused. All of that is set at runtime and
never written to the game's settings file. Unity may remember the window size in the registry, which the real game
shares, but the real game applies its own display settings on start. A `res` size is kept in `UserData\Driver\res.txt`
across restarts and enforced every 2 s, so put it back with `res 1600 900` after a big-screenshot session.

## Use

```
pwsh scripts/dev.ps1 push driver        # build + copy into the test copy (restart the test copy to load it)
pwsh scripts/dev.ps1 server up
pwsh scripts/dev.ps1 play               # with the driver in: starts minimized, then off screen, no focus steal
pwsh scripts/dev.ps1 drive "state"
pwsh scripts/dev.ps1 drive "phone open; click Messages; wait 0.5; shot messages Phone"
```

`drive` prints the driver's answer plus any new errors in the game's log, and exits 1 on either. A failing command
stops the rest of its batch.

## Commands

| Command | Does |
|---|---|
| `state` | window size, fps, focus; phone open?, which app, menu screen |
| `phone open` / `phone close` | the phone key: `GameplayMenu.SetScreen(Phone)` + `Open()` / `Close()` |
| `back` | Escape: the newest exit listener that takes it (closes an app, a popup, then the phone) |
| `find <q>` | lists active UI objects matching `<q>`, with path, text, screen rect and `[clickable]` |
| `click <q>` | pointer enter/down/up/click/exit on the first click handler at or above the match |
| `scroll <q> <0..1>` | the ScrollRect at, above or under the match (1 = top) |
| `type <q> = <text>` | sets the text of the input field at or above the match |
| `tree <q> [depth]` | the match's subtree: rects, screen rects, images (sprite, color), texts (size, color), components |
| `shot <name> [<q>]` | `UserData\Driver\shots\<name>.png`, cropped to the match's screen rect when `<q>` is given |
| `wait <seconds>` | real time |
| `res <w> <h>` | another window size (kept until the game restarts) |
| `window back` / `hide` / `show` | behind the others / off screen (still renders) / back on screen, never activated |
| `hands on` / `hands off` | Blazzer plays it: the screen's refresh rate, sound on, window in front with focus / back to 20 fps, muted, off screen (not kept across a driver reload) |

Queries: a name or a visible text, case ignored, exact beats contains (`Messages`). `/A/B` matches the end of a
hierarchy path. Several equally good hits fail and list them; add ` #n` to pick one (`click Accept #2`).

The dev world is ours, so accepting deals, buying and sending messages are fine there. Never on the live server: the
driver only goes into the test copy, which only joins the dev server.
