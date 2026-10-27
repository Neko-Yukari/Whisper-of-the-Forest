using System;
using HarmonyLib;
using Verse;

namespace WhisperOfTheForest.Experimental
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

            // Command-line configuration must not depend on a loaded game.
            LabHost.ConfigureFromCommandLine();

            try
            {
                new Harmony(HarmonyId).PatchAll();
                LabHost.MarkHarmonyPatched();
                Log.Message("[WotF-Lab] Harmony patched with id " + HarmonyId);
            }
            catch (Exception exception)
            {
                // A failed patch must stay visible in the log and in `status`, but must not break startup.
                Log.Error("[WotF-Lab] Harmony PatchAll failed: " + exception);
            }
        }
    }

    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    internal static class Patch_RootPlay_Update
    {
        // Postfix runs once per Unity frame on the game's main thread.
        private static void Postfix()
        {
            LabHost.OnMainThreadUpdate();
        }
    }
}
