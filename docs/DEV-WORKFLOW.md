---
title: Hyland Link dev workflow
tags: [project, guide]
status: active
updated: 2026-11-04
---

# Dev workflow

How to work on our mods without touching the live server or Blazzer's real game. One script drives it:
`pwsh scripts/dev.ps1` (its header lists every command).

## The three places

```
DEV, on this PC (the default)        SANDBOX, CT 121 (rare)           LIVE, CT 121
C:\Games\Schedule I (dev server)     s1ds-sandbox, port 38466         port 38465, friends on it
  a copy of a live world               only to reproduce a bug that     changed only by
C:\Games\Schedule I (HUD test)         needs the Linux/Wine server      mod-release.ps1 + mods.ps1 publish
  + HotReload, joins 127.0.0.1
```

- **Dev server:** the game installed a second time with the S1DS 1.1.6 server mod (the same version live runs, from
  the manifest's release) and MelonLoader 0.7.3. Its world lives in its own `UserData\DedicatedServerSave`, never in
  the game's `AppData\LocalLow` saves. Listens on 127.0.0.1:38465, console on 127.0.0.1:4050, named "Hyland Dev".
- **Test copy:** the game client with the server's client mod, our mods, and **HotReload** in `Plugins\` (dev only,
  never shipped, never in the real game). It's the only copy we put test builds into.
- **World backups:** `C:\Games\Schedule I dev worlds\`. Copy one down from the server with schedule1-server's
  `bash schedule1-admin/pull-world.sh` (read-only on the server). Worlds a swap replaced go to `replaced\` there.
- **Blazzer's real game** (`steamapps\common\Schedule I`) is for playing. Nothing here writes to it. Our mod projects
  only compile against its `MelonLoader\Il2CppAssemblies`.

## The loops

| Changing | Do | Restart? |
|---|---|---|
| HUD, Notifications, Minimap (anything drawn in game) | `dev.ps1 watch hud` and save files | No: HotReload swaps it in ~0.1 s |
| Something that runs during a join (Hyland Fixes) | `dev.ps1 push fixes`, then rejoin, or `dev.ps1 jointest` | Rejoin only |
| A server-side mod or server setting | edit, `dev.ps1 server down`, `server up` | The dev server only |
| A bug that needs the real world's state | `pull-world.sh <backup>`, `dev.ps1 server world <backup>` | The dev server only |
| Shipping | the usual: bump `AssemblyInfo.cs`, push, `mod-release.ps1 <mod> -Publish`, `mods.ps1 publish` | Live, once |

A session:

```
pwsh scripts/dev.ps1 server up      # loads the newest world in the worlds folder the first time
pwsh scripts/dev.ps1 play           # the test copy joins the dev server by itself
pwsh scripts/dev.ps1 watch hud      # save a file -> it builds and the game reloads it
pwsh scripts/dev.ps1 log            # what HotReload and our mods said, plus errors
pwsh scripts/dev.ps1 server down    # saves (confirmed in the log), then stops
```

`dev.ps1 status` shows each mod in the test copy against your build and the live mod list, so you can tell at a
glance whether the test copy runs what friends run.

Measured 2026-11-04: the dev server boots and loads the live world in ~22 s, the test copy joins by itself ~15 s after
launch, and a HUD rebuild lands in the running game in ~70 ms. The server can start before or after the test copy.
Only one client at a time: close Blazzer's real game before `play` (one Steam account, one game). If Windows asks
whether `Schedule I.exe` may use the network, Cancel is fine: the dev server only needs 127.0.0.1.

## Writing mods that reload

HotReload unloads the old build (Harmony patches, MelonLoader callbacks, preference categories, objects it made)
and starts the new one as if the game had just launched: `OnInitializeMelon`, then `OnSceneWasLoaded` replayed for
the open scenes. Ours already fit:
- `OnDeinitializeMelon` must put the game back the way it was (HUD and Notifications call their `Teardown()`).
- Build lazily from `OnLateUpdate` (with the retry helpers), not from one-time game events that won't fire again.
- No injected Il2Cpp classes (`ClassInjector`): keep it that way, they're the hard case for reloading.
- Code that only runs while joining needs a rejoin to run again.

Each reload keeps the old build in memory (about the DLL's size), so restart the test copy now and then.

## Set up (done 2026-11-04; redo after a game update)

1. Game update: copy the new install over the dev server and the test copy, skipping `Mods`, `Plugins`,
   `UserData` (robocopy `/E /XD Mods Plugins UserData`).
2. Dev server: `Mods\DedicatedServerMod_Il2cpp_Server.dll` from S1DS `Il2cpp_Server.zip` (the version in
   `manifest.json` → `mod.version`; check the zip's sha256 against the GitHub release), `steam_appid.txt` = `3164500`.
3. HotReload: source pinned at `.gameref/tools/hotreload` (github.com/Vergir/MelonLoader-HotReload, tag v1.2.0,
   reviewed 2026-11-04: no network, writes only under the game folder). Build with
   `dotnet build HotReload.csproj -c Release -p:DeployToGame=false` and copy `bin\Release\HotReload.dll` + `.pdb`
   into the test copy's `Plugins\`. Settings: the test copy's `UserData\HotReload.toml`.
