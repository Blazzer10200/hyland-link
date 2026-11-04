# JoinDiag (test copy only, never shipped)

A throwaway MelonLoader mod that logs around the client-side calls that can kill a server join: grid item init,
FishNet start callbacks, spray surfaces, `DryingRack.PleaseReceiveOp` and `ConfigurationReplicator.ReceiveStringField`
(the one that throws when an object never got its configuration). It names the object as `type @ parent/path`, so the
first `start` without an `ok` is the object whose init threw. It found the drying rack bug (see `mods/fixes/README.md`).

Build (references the test copy): `dotnet build -c Release tools/joindiag`, then
`pwsh scripts/join-test.ps1 -Port 38466 -Mod tools/joindiag/bin/Release/net6.0/JoinDiag.dll` and read
`[JoinDiag]` lines in the test copy's `MelonLoader/Latest.log`. Add a `[HarmonyPatch]` for whatever new suspect a
log points at; RPC bodies are `RpcLogic___<Name>_<hash>` in `.gameref/src`.

Careful: a Harmony patch on an Il2Cpp RPC method changes the bug. The interop trampoline catches the exception, so a
join that hangs without JoinDiag can get through with it. Use it to name the object, then confirm with a clean client.
