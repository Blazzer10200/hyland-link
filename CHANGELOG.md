# Changelog

What changed for players. Shipped versions are on the Releases page; "Unreleased" is fixed here but not sent to
anyone yet.

## Unreleased

### Hyland Notifications, next version (on `main`, not released)
- **Lighter on the game.** The deal tracker, the unread bubble and the police status no longer rewrite their text every
  frame, only when it changes.

### Hyland HUD 0.2.0, the phone (back-burnered on branch `hud-phone`, needs a lot more work: see HANDOFF)
- **A new phone.** A slim dark body in place of the game's chunky 3D model, sharp at any screen size. The home screen
  has a card with the time, the day and the weather, your bank balance and who's on the server, and glass app tiles
  with new icons. Each tile shows its number key (1–8), and the bar at the bottom works as the home button.
- **The Hyland app** (key 8): every HUD setting on the phone itself, with no config file to edit.
- **Rain you can turn down**, and much quieter indoors (the game drops it only a little under a roof).
- **The game's apps match the phone.** Messages, Journal, Products, Contacts, Map, Dealers and Deliveries go dark like
  the home screen, with a back button and the app's name at the top. Colors that mean something stay: message
  bubbles, prices, relationship rings, faces and the map itself. Deliveries' tabs move to the top, and the map sits
  in a rounded card. Titles no longer get cut off by the screen's rounded corners. Apps zoom out of their icon when
  you open them, and the back button shrinks them back in.
- **Messages reads at a glance.** Each chat shows its latest message on one line, a dealer's "what they got" list
  included, and a customer's waiting offer shows its timer as a thin bar along the row.
- **Incoming calls** use the same dark style, with Answer and Decline still green and red.

## Shipped

### 2026-11-03, mod list revision 9
- **Hyland HUD 0.1.1: dying on the server no longer kicks you out.** The game's death screen shows "Respawn" only
  inside a Steam lobby. On the server you join by IP, so it offered only "Load last save", which quits to the main
  menu and drops you from the server. You now get Respawn (hospital, or your property during curfew), like co-op.
- **Hyland Notifications 0.1.0** (new for everyone): every on-screen notification in the HUD's glass, with smooth
  slide-ins and fades, each shown exactly as long as the game's own would be.
  - **Deal tracker** (top left): each deal shows the customer's face, the goods, the place and the pay, with a chip
    for when the window opens and how long is left. Under 2 hours it turns orange and the face pulses. Cartel deals
    and quests sit under it with their current steps; past 5 rows (your pick, 2–10) the rest fold into "+N more".
  - **Toasts and messages:** texts and app notices as glass cards. Several texts from one person fold into one card
    with a count. The unread messages bubble shows how many chats are waiting.
  - **Incoming calls:** the caller's face and name, with a ringing pulse.
  - **Police status** under the compass: investigating, under arrest, wanted, wanted dead or alive, or a body search,
    each with its own icon and color. Wanted pulses an alarm ring, and once they lose sight of you it counts down how
    long they'll keep searching.
  - **Big moments** at the top center: deal done (pay counting up, satisfaction bar, relationship, bonuses), new
    customers with their faces, cartel influence and cartel status changes. The special customers' arrival card shows
    bottom left.
  - **Hints and "Game saved"** sit under the minimap instead of on top of it. Nothing of ours lands on the minimap,
    whichever corner you put it in.
  - **Progress bar** (being cuffed, every hold-to action) in glass, in the same spot as before.
  - **Ticket, arrest notice and hospital bill** go from paper to dark glass. Their buttons and keys work as before.

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
