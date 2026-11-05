using System;
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

#if WOTF_LAB
            // Command-line configuration must not depend on a loaded game. The lab is not part of
            // a release build, so neither is this call.
            LabHost.ConfigureFromCommandLine();
#endif

            try
            {
                new Harmony(HarmonyId).PatchAll();
#if WOTF_LAB
                LabHost.MarkHarmonyPatched();
#endif
                WotfLog.Info(WotfLog.ModuleBoot, "Harmony patched with id " + HarmonyId);
            }
            catch (Exception exception)
            {
                // A failed patch must stay visible in the log and in `status`, but must not break startup.
                WotfLog.Error(WotfLog.ModuleBoot, "Harmony PatchAll failed: " + exception);
            }
        }
    }

#if WOTF_LAB
    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    internal static class Patch_RootPlay_Update
    {
        // Postfix runs once per Unity frame on the game's main thread.
        private static void Postfix()
        {
            LabHost.OnMainThreadUpdate();
        }
    }
#endif
}
