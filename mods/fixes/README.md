# Hyland Fixes

Client-side workarounds for game bugs that hit our dedicated server. Id `hyland-fixes`, shipped like our other mods
(`pwsh scripts/mod-release.ps1 fixes -Publish`, then `pwsh scripts/mods.ps1 publish`). One file per fix in `src/`; each
one logs under `[Hyland Fixes]` when it steps in, so `MelonLoader/Latest.log` shows whether it mattered.

## Drying rack join hang (`DryingRackVisuals.cs`, 0.1.0)

**Symptom:** joining the server sticks on the loading screen ("Syncing NPCBehaviour" and similar) for everyone while any
drying rack is drying a custom mix (2026-11-03, Miracle Balls in the Bungalow).

**Cause:** on join the server sends each rack's drying operations (`DryingRack.PleaseReceiveOp`) before the player's game
knows the server's custom products, which S1DS replicates later through a rate-limited queue. `PleaseReceiveOp` calls
`RefreshHangingVisuals` → `DryingOperation.GetQualityItemInstance` → `Registry.GetItem(id)`, which is null for a product
the client hasn't received yet, so it throws. FishNet then drops the rest of that network batch, other objects never get
their configuration, `ConfigurationReplicator.ReceiveStringField` throws at load-complete, and the S1DS client's load
sequence dies.

**Fix:** a Harmony prefix skips `RefreshHangingVisuals` while any operation's product is unknown and retries once a
second until it's known (gives up after 10 min). The rack's operations list is untouched, so drying, saving and the
rack's UI behave as before; only the hanging buds appear a few seconds late. A finalizer logs and stops any other
exception in that purely visual method, so it can never take a join down again.

**Server side:** `s1ds world-check` (schedule1-server) flags racks drying a custom product, and `s1ds-diag fix-drying
--apply` moves them to the output slot. With this mod on every player that's a safety net, not a must.

**Testing:** `s1ds-sandbox up <backup>` (schedule1-server, on the server box) loads a world with such a rack on port 38466; then
`pwsh scripts/join-test.ps1 -Port 38466` (expect STUCK) and the same with
`-Mod mods/fixes/bin/Release/net6.0/HylandFixes.dll` (expect LOADED IN).
