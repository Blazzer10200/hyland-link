# Changelog

What changed for players. Shipped versions are on the Releases page; "Unreleased" is fixed here but not sent to
anyone yet.

## Unreleased

### Hyland HUD 0.2.0 (built, not tested in game, not staged)
Includes 0.1.1's respawn fix.
- **A glass phone.** The home screen gets glass app tiles in four columns, a clock card (time, day, a rain cloud when
  it's raining, your bank, who's on the server) and a dark case. It wakes from black, the card drops in and the apps
  pop in one by one. Number keys 1–9 show on the tiles. Your apps, badges, keys and controller work as before.
  "Glass phone" in the Hyland app turns it off.
- **The Hyland app (key 8).** All the HUD's switches and the slot gap, plus the phone's own, without editing a config
  file. Changes save right away.
- **Rain you can turn down.** Rain volume (0–100%) on top of the game's Weather slider, and "Much quieter indoors",
  which takes rain inside down to about a third of the game's level. Indoors used to be only a little quieter than
  outside.

### Hyland HUD 0.1.1 (staged in `dist/hud-v0.1.1`, not published)
- **Dying on the server no longer kicks you out.** The game's death screen shows "Respawn" only inside a Steam lobby.
  On the server you join by IP, so it offered only "Load last save", which quits to the main menu and drops you
  from the server. You now get Respawn (hospital, or your property during curfew), like co-op. Single player and
  co-op hosts keep the game's normal screen.
