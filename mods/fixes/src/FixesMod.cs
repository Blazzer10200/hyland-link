using MelonLoader;

namespace HylandFixes
{
    // Workarounds for game bugs that hit a dedicated server, one file per fix. MelonLoader applies every [HarmonyPatch]
    // in this assembly on load.
    public class FixesMod : MelonMod
    {
        internal static MelonLogger.Instance Log;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Log.Msg("Hyland Fixes loaded: drying rack join fix");
        }
    }
}
