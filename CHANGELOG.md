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

## Shipped

### 2026-11-02, mod list revision 8
- **Hyland HUD 0.1.0** (new for everyone): the "Glass Rail" hotbar with rounded slots and a lift on the selected one,
  a compass strip with a clock pill, curfew and sleep warnings, friends on the compass with their distance, and a
  bank panel that counts up. It fades when you're idle and puts the game's own HUD back if anything goes wrong.
- **Hyland Minimap 1.2.2**: the server's own headless player no longer shows up as a ghost friend, and a crashing
  minimap waits longer between retries.
- **Launcher 0.1.4**: Play waits for the latest mod list, Update can't run while the game is starting, removing mods
  locks the buttons, a stale mod list from GitHub's cache can't roll you back, and closing mid-install asks twice.

### 2026-11-02, mod list revision 7
- **Launcher 0.1.3**: a slow or dropped connection no longer asks you to "update" back to old mods, a damaged
  saved-servers file can't block Play, Settings can't remove files during an install, and if the server's mods change
  while you play, the button closes the game for you.
- **Hyland Minimap 1.2.1**: a customer the game hasn't fully loaded no longer knocks out the whole minimap,
  PageUp/PageDown don't zoom while you type, and the rotating map can't get stuck after a bad frame.

### 2026-11-02, mod list revisions 5 and 6
- **Launcher 0.1.2**: Update is no longer blocked after the game quits (a closed game was mistaken for a running one).
- **Hyland Minimap 1.2.0**: the map turns with your view (on for everyone), turning is smoother, zoom-out stops at 0.6.
- **MelonLoader 0.7.3** for players and the server.

### 2026-11-02, mod list revisions 3 and 4
- **Hyland Minimap 1.1.0** replaces the stock Schedule I Minimap (revision 3): our fork of nacho's minimap (MIT) with
  a smooth bezel, sharp zoom, muted map colors, edge shading, a view cone, a north badge, edge pins for off-map deals
  and dealers, and outlined markers.

### 2026-11-02, launcher 0.1.1
- No more MelonLoader console window. The launcher shows what the game is doing (loading, main menu, joining) and its
  log. A gear menu holds the game folder, logs and Uninstall. While you play, the big button is "Leave server & close
  game", which saves and disconnects cleanly.

### 2026-11-02, launcher 0.1.0
- First release. It finds Schedule I, installs MelonLoader, the server mod and every mod the server runs, keeps them
  matched to the server, saves the server to your Favorites and updates itself.
