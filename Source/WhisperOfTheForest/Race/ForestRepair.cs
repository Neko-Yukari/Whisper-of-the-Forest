using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// A forest craft pawn fixing a broken-down building gets a clumsy repair effect instead of the
    /// vanilla welding sparks. Only JobDriver_FixBrokenDownBuilding is touched; ordinary repairs
    /// (JobDriver_Repair) keep their own effect.
    /// </summary>
    internal static class ForestRepair
    {
        public static EffecterDef EffecterFor(Pawn pawn)
        {
            Gene_ForestCraft gene = Gene_ForestCraft.Get(pawn);
            return gene == null ? null : ((WotF_ForestCraftGeneDef)gene.def).elfRepairEffect;
        }
    }

    [HarmonyPatch(typeof(JobDriver_FixBrokenDownBuilding), "MakeNewToils")]
    internal static class Patch_JobDriver_FixBrokenDownBuilding
    {
        private static void Postfix(JobDriver_FixBrokenDownBuilding __instance, ref IEnumerable<Toil> __result)
        {
            EffecterDef effecter = ForestRepair.EffecterFor(__instance.pawn);
            if (effecter == null)
            {
                return;
            }

            __result = ReplaceWaitEffect(__result, effecter);
        }

        private static IEnumerable<Toil> ReplaceWaitEffect(IEnumerable<Toil> toils, EffecterDef effecter)
        {
            foreach (Toil toil in toils)
            {
                if (toil.defaultCompleteMode == ToilCompleteMode.Delay && toil.defaultDuration > 0)
                {
                    // The vanilla wait toil has one pre-tick action and two finish actions:
                    // ToilEffects.WithEffect (the welding effecter) and WithProgressBarToilDelay.
                    // Drop only the effecter's pair, keep the progress bar's cleanup, then attach ours.
                    if (toil.preTickActions != null && toil.preTickActions.Count > 0)
                    {
                        toil.preTickActions.RemoveAt(0);
                    }
                    if (toil.finishActions != null && toil.finishActions.Count > 0)
                    {
                        toil.finishActions.RemoveAt(0);
                    }

                    toil.WithEffect(effecter, TargetIndex.A);
                }

                yield return toil;
            }
        }
    }
}
