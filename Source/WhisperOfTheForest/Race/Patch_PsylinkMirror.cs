using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    // For a bloodline pawn the vanilla psylink is a mirror of the bloodline level. Every patch here
    // returns to vanilla behaviour for pawns without the gene.

    /// <summary>A neuroformer or ritual created the first psylink.</summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.PostAdd))]
    internal static class Patch_HediffPsylink_PostAdd
    {
        public static void Postfix(Hediff_Psylink __instance)
        {
            Gene_ForestBloodline.Get(__instance.pawn)?.Notify_VanillaPsylinkChanged();
        }
    }

    /// <summary>
    /// Neuroformer upgrades, the anima tree and title bestowing all end here
    /// (<c>ChangeLevel(int)</c> and <c>PawnUtility.ChangePsylinkLevel</c> forward to this overload).
    /// </summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.ChangeLevel), typeof(int), typeof(bool))]
    internal static class Patch_HediffPsylink_ChangeLevel
    {
        public static void Postfix(Hediff_Psylink __instance)
        {
            Gene_ForestBloodline.Get(__instance.pawn)?.Notify_VanillaPsylinkChanged();
        }
    }

    /// <summary>Mirror levels hand out no random psycast; psytrainers still teach them.</summary>
    [HarmonyPatch(typeof(Hediff_Psylink), nameof(Hediff_Psylink.TryGiveAbilityOfLevel))]
    internal static class Patch_HediffPsylink_TryGiveAbilityOfLevel
    {
        public static bool Prefix(Hediff_Psylink __instance)
        {
            return Gene_ForestBloodline.Get(__instance.pawn) == null;
        }
    }

    /// <summary>
    /// The psylink stages multiply neural-heat limit and recovery; a bloodline pawn takes both from
    /// its level curves instead, so its psylink reads a copy of the stage without those two factors.
    /// </summary>
    [HarmonyPatch(typeof(Hediff), nameof(Hediff.CurStage), MethodType.Getter)]
    internal static class Patch_Hediff_CurStage_BloodlinePsylink
    {
        private static readonly Dictionary<HediffStage, HediffStage> Stripped = new Dictionary<HediffStage, HediffStage>();
        private static readonly MethodInfo ObjectClone = AccessTools.Method(typeof(object), "MemberwiseClone");

        public static void Postfix(Hediff __instance, ref HediffStage __result)
        {
            if (!(__instance is Hediff_Psylink) || __result == null || Gene_ForestBloodline.Get(__instance.pawn) == null)
            {
                return;
            }

            if (!Stripped.TryGetValue(__result, out HediffStage stage))
            {
                stage = (HediffStage)ObjectClone.Invoke(__result, null);
                stage.statFactors = __result.statFactors?
                    .Where(m => m.stat != StatDefOf.PsychicEntropyMax && m.stat != StatDefOf.PsychicEntropyRecoveryRate)
                    .ToList();
                Stripped[__result] = stage;
            }

            __result = stage;
        }
    }
}
