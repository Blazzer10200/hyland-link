using System;
using HarmonyLib;
using MelonLoader;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Management;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

[assembly: MelonInfo(typeof(JoinDiag.Mod), "JoinDiag", "0.1.0", "Blazzer")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace JoinDiag
{
    // Test copy only. Names the object whose client-side init throws during a server join, and the configuration
    // replicator whose name sync finds no configuration.
    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Log;
        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Log.Msg("JoinDiag loaded");
        }

        internal static string Describe(Component c)
        {
            if (c == null) return "(null)";
            string path = c.gameObject.name;
            Transform t = c.transform.parent;
            for (int i = 0; t != null && i < 6; i++) { path = t.name + "/" + path; t = t.parent; }
            return c.GetIl2CppType().FullName + " @ " + path;
        }
    }

    [HarmonyPatch(typeof(GridItem), "RpcLogic___InitializeGridItem_Client_1883577149")]
    internal static class GridInitPatch
    {
        static void Prefix(GridItem __instance, string GUID) =>
            Mod.Log.Msg("[GRIDINIT start] " + Mod.Describe(__instance) + " guid=" + GUID);
        static void Postfix(GridItem __instance) =>
            Mod.Log.Msg("[GRIDINIT ok] " + Mod.Describe(__instance));
    }

    [HarmonyPatch(typeof(Il2CppFishNet.Object.NetworkObject), "InitializeCallbacks")]
    internal static class StartCallbacksPatch
    {
        static void Prefix(Il2CppFishNet.Object.NetworkObject __instance, bool asServer)
        {
            if (!asServer) Mod.Log.Msg("[CBSTART start] " + Mod.Describe(__instance) + " id=" + __instance.ObjectId);
        }
        static void Postfix(Il2CppFishNet.Object.NetworkObject __instance, bool asServer)
        {
            if (!asServer) Mod.Log.Msg("[CBSTART ok] id=" + __instance.ObjectId);
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Graffiti.WorldSpraySurface), "RpcLogic___Set_3759704962")]
    internal static class WorldSprayPatch
    {
        static void Prefix(Il2CppScheduleOne.Graffiti.WorldSpraySurface __instance, Il2CppReferenceArray<Il2CppScheduleOne.Graffiti.SprayStroke> strokes, bool hasBeenFinalized, bool isCartelGraffiti) =>
            Mod.Log.Msg("[SPRAY start] " + Mod.Describe(__instance) + " strokes=" + (strokes == null ? -1 : strokes.Length) + " finalized=" + hasBeenFinalized + " cartel=" + isCartelGraffiti);
        static void Postfix(Il2CppScheduleOne.Graffiti.WorldSpraySurface __instance) =>
            Mod.Log.Msg("[SPRAY ok] " + Mod.Describe(__instance));
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Graffiti.SpraySurface), "RpcLogic___Set_4105842735")]
    internal static class SprayPatch
    {
        static void Prefix(Il2CppScheduleOne.Graffiti.SpraySurface __instance, Il2CppReferenceArray<Il2CppScheduleOne.Graffiti.SprayStroke> strokes) =>
            Mod.Log.Msg("[SPRAYBASE start] " + Mod.Describe(__instance) + " strokes=" + (strokes == null ? -1 : strokes.Length));
        static void Postfix(Il2CppScheduleOne.Graffiti.SpraySurface __instance) =>
            Mod.Log.Msg("[SPRAYBASE ok] " + Mod.Describe(__instance));
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.ObjectScripts.DryingRack), "RpcLogic___PleaseReceiveOp_1575047616")]
    internal static class DryingOpPatch
    {
        static void Prefix(Il2CppScheduleOne.ObjectScripts.DryingRack __instance) =>
            Mod.Log.Msg("[DRYOP start] " + Mod.Describe(__instance) + " guid=" + __instance.GUID + " cfgNull=" + (__instance.Configuration == null));
        static void Postfix(Il2CppScheduleOne.ObjectScripts.DryingRack __instance) =>
            Mod.Log.Msg("[DRYOP ok] " + __instance.GUID);
    }

    [HarmonyPatch(typeof(ConfigurationReplicator), "RpcLogic___ReceiveStringField_2801973956")]
    internal static class StringFieldPatch
    {
        static void Prefix(ConfigurationReplicator __instance, int fieldIndex, string value)
        {
            bool nullCfg = __instance.Configuration == null;
            if (nullCfg)
                Mod.Log.Warning("[STRINGFIELD null configuration] " + Mod.Describe(__instance) + " field=" + fieldIndex + " value=" + value);
        }
    }
}
