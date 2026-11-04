using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ObjectScripts;
using MelonLoader;
using UnityEngine;

namespace HylandFixes
{
    // The join hang of 2026-11-03 ("Syncing NPCBehaviour" forever). When a player joins, the server sends each drying
    // rack's operations (PleaseReceiveOp) before the player's game knows the server's custom products: those arrive
    // later through S1DS's rate-limited replication queue. PleaseReceiveOp then calls RefreshHangingVisuals, which does
    // Registry.GetItem(op.ItemID).GetDefaultInstance(), and GetItem is null for a mix the client hasn't heard of yet.
    // That NullReferenceException makes FishNet drop the rest of the network batch, other objects never get their
    // configuration, and the S1DS client's load sequence dies on the loading screen.
    //
    // The prefix skips the refresh while any op's product is still unknown and retries once a second until it's known,
    // so the rack's state (its DryingOperations list) is untouched and only the hanging buds show up a little later.
    // The finalizer stops anything else in this purely visual method from taking a join down, and logs it.
    [HarmonyPatch(typeof(DryingRack), nameof(DryingRack.RefreshHangingVisuals))]
    internal static class DryingRackVisuals
    {
        const float RetrySeconds = 1f;
        const float GiveUpSeconds = 600f;
        static readonly HashSet<IntPtr> Waiting = new HashSet<IntPtr>();

        static bool Prefix(DryingRack __instance)
        {
            string missing = MissingItem(__instance);
            if (missing == null) return true;
            if (Waiting.Add(__instance.Pointer))
            {
                FixesMod.Log.Msg($"Drying rack {Name(__instance)}: product '{missing}' isn't known here yet, so its hanging " +
                                 "visuals wait for it (this used to hang the join)");
                MelonCoroutines.Start(RetryWhenKnown(__instance, __instance.Pointer));
            }
            return false;
        }

        static Exception Finalizer(DryingRack __instance, Exception __exception)
        {
            if (__exception != null)
                FixesMod.Log.Warning($"Drying rack {Name(__instance)}: its visuals failed to refresh, kept going so the " +
                                     $"game doesn't drop the rest of the network batch: {__exception}");
            return null;
        }

        static IEnumerator RetryWhenKnown(DryingRack rack, IntPtr key)
        {
            float start = Time.realtimeSinceStartup, next = start + RetrySeconds;
            try
            {
                while (true)
                {
                    yield return null;
                    if (Time.realtimeSinceStartup < next) continue;
                    next = Time.realtimeSinceStartup + RetrySeconds;
                    if (rack == null) yield break; // destroyed (left the server, rack picked up)
                    string missing = MissingItem(rack);
                    if (missing == null)
                    {
                        Waiting.Remove(key); // before the call, so the prefix may queue a new wait if it changed again
                        rack.RefreshHangingVisuals();
                        FixesMod.Log.Msg($"Drying rack {Name(rack)}: product known now, visuals drawn " +
                                         $"({Time.realtimeSinceStartup - start:0}s later)");
                        yield break;
                    }
                    if (Time.realtimeSinceStartup - start > GiveUpSeconds)
                    {
                        FixesMod.Log.Warning($"Drying rack {Name(rack)}: product '{missing}' still unknown after " +
                                             $"{GiveUpSeconds:0}s; its hanging visuals stay empty until it changes");
                        yield break;
                    }
                }
            }
            finally
            {
                Waiting.Remove(key);
            }
        }

        // The first drying op whose product this game doesn't know, or null when all are known.
        static string MissingItem(DryingRack rack)
        {
            var ops = rack.DryingOperations;
            if (ops == null) return null;
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op != null && !string.IsNullOrEmpty(op.ItemID) && !Registry.ItemExists(op.ItemID)) return op.ItemID;
            }
            return null;
        }

        static string Name(DryingRack rack)
        {
            try { return rack == null ? "(gone)" : rack.GUID.ToString().Substring(0, 8); }
            catch { return "(unknown)"; }
        }
    }
}
