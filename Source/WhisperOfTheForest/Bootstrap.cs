using HarmonyLib;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Single entry point for the experiment assembly. Patches Harmony exactly once so feature
    /// folders can add [HarmonyPatch] classes without each creating their own Harmony instance.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Bootstrap
    {
        public const string HarmonyId = "NekoYukari.WhisperOfTheForest.Experiments";

        private static bool initialized;

        static Bootstrap()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            new Harmony(HarmonyId).PatchAll();
            WotfLog.Info(WotfLog.ModuleBoot, "Harmony patched with id " + HarmonyId);
        }
    }
}
