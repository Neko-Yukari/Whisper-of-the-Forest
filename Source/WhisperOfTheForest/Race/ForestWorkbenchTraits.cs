using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// The bloodline's relation to workbenches: an electric bench slows the work down, a bench
    /// without a power comp raises the quality of what the pawn makes. Both numbers live on the
    /// bloodline gene Def.
    /// </summary>
    internal static class ForestWorkbench
    {
        public static bool IsBloodlineAtWorkTable(Pawn pawn, out Building_WorkTable table)
        {
            table = null;
            if (Gene_ForestBloodline.Get(pawn) == null)
            {
                return false;
            }

            table = pawn.CurJob?.bill?.billStack?.billGiver as Building_WorkTable;
            return table != null;
        }

        public static bool IsPowered(Building_WorkTable table)
        {
            return table.GetComp<CompPowerTrader>() != null;
        }

        public static WotF_ForestBloodlineGeneDef DefOf(Pawn pawn)
        {
            return (WotF_ForestBloodlineGeneDef)Gene_ForestBloodline.Get(pawn).def;
        }
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))]
    internal static class Patch_ToilsRecipe_DoRecipeWork
    {
        private static void Postfix(Toil __result)
        {
            Action<int> original = __result.tickIntervalAction;
            if (original == null)
            {
                return;
            }

            __result.tickIntervalAction = delta =>
            {
                Pawn pawn = __result.actor;
                if (!ForestWorkbench.IsBloodlineAtWorkTable(pawn, out Building_WorkTable table)
                    || !ForestWorkbench.IsPowered(table))
                {
                    original(delta);
                    return;
                }

                JobDriver_DoBill driver = pawn.jobs.curDriver as JobDriver_DoBill;
                if (driver == null)
                {
                    original(delta);
                    return;
                }

                float before = driver.workLeft;
                original(delta);
                if (pawn.jobs.curDriver != driver || driver.workLeft <= 0f)
                {
                    return;
                }

                float spent = before - driver.workLeft;
                if (spent <= 0f)
                {
                    return;
                }

                float factor = ForestWorkbench.DefOf(pawn).poweredWorkTableSpeedFactor;
                driver.workLeft += spent * (1f - factor);
            };
        }
    }

    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn),
        new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) })]
    internal static class Patch_QualityUtility_GenerateQualityCreatedByPawn
    {
        private static void Postfix(Pawn pawn, ref QualityCategory __result)
        {
            if (!ForestWorkbench.IsBloodlineAtWorkTable(pawn, out Building_WorkTable table)
                || ForestWorkbench.IsPowered(table))
            {
                return;
            }

            int bonus = ForestWorkbench.DefOf(pawn).handcraftQualityBonus;
            if (bonus <= 0)
            {
                return;
            }

            __result = (QualityCategory)Math.Min((int)__result + bonus, (int)QualityCategory.Legendary);
        }
    }
}
