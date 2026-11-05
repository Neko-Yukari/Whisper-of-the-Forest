using HarmonyLib;
using Verse;

namespace WhisperOfTheForest.Experimental.Race
{
    /// <summary>
    /// A bloodline pawn that gains a vanilla psylink (any level) only counts as bloodline level 1.
    /// The patch never reads the psylink level; the ladder itself is the source of truth.
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.PostAdd))]
    internal static class Patch_HediffPsylink_Bloodline
    {
        public static void Postfix(Hediff_Psylink __instance)
        {
            Gene_ForestBloodline.Get(__instance.pawn)?.ApplyLegacyPsylinkLevel();
        }
    }
}
