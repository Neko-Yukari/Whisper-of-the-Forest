using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Mana
{
    public sealed class CompProperties_ManaWorkbench : CompProperties
    {
        public float manaPerSecond;
        public float linkRadius;

        public CompProperties_ManaWorkbench()
        {
            compClass = typeof(CompManaWorkbench);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req)) yield return entry;
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaDraw".Translate(),
                string.Format("WotF_Stat_Unit_ManaPerSec".Translate(), manaPerSecond.ToString("0.##")),
                "WotF_Stat_ManaDraw_Desc".Translate(),
                2610);
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaLinkRadius".Translate(),
                string.Format("WotF_Stat_Unit_Cells".Translate(), linkRadius.ToString("0.#")),
                "WotF_Stat_ManaLinkRadius_Desc".Translate(),
                2600);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (manaPerSecond <= 0f) yield return "manaPerSecond must be positive";
            if (linkRadius <= 0f) yield return "linkRadius must be positive";
            if (!typeof(Building_WorkTable).IsAssignableFrom(parentDef.thingClass))
                yield return "mana workbench requires Building_WorkTable";
            if (parentDef.CompDefForAssignableFrom<CompPowerTrader>() != null)
                yield return "mana workbench must not have CompPowerTrader";
        }
    }

    public sealed class CompManaWorkbench : ThingComp
    {
        public CompProperties_ManaWorkbench Props => (CompProperties_ManaWorkbench)props;
        private ManaNetwork Network => MapComponent_ManaNetwork.For(parent.Map)
            .NetworkNear(parent.Position, Props.linkRadius);
        public bool CanWork => parent.Spawned && ManaSwitchUtility.IsSwitchedOn(parent)
            && Network != null && Network.Stored >= Props.manaPerSecond / 60f;

        public bool TryPayForWork(int ticks)
        {
            return ManaSwitchUtility.IsSwitchedOn(parent) && Network?.TryDraw(Props.manaPerSecond * ticks / 60f) == true;
        }

        public override string CompInspectStringExtra()
        {
            if (!ManaSwitchUtility.IsSwitchedOn(parent))
                return "WotF_ManaSwitch_Off".Translate().ToString();

            return CanWork ? string.Format("WotF_ManaWorkbench_Rate".Translate(), Props.manaPerSecond.ToString("0.##"))
                : "WotF_ManaWorkbench_NoMana".Translate().ToString();
        }
    }

    [HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.UsableForBillsAfterFueling))]
    internal static class Patch_ManaWorkbenchUsable
    {
        private static void Postfix(Building_WorkTable __instance, ref bool __result)
        {
            if (__result && __instance.GetComp<CompManaWorkbench>() is CompManaWorkbench mana)
                __result = mana.CanWork;
        }
    }

    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.DoRecipeWork))]
    internal static class Patch_ManaWorkbenchWork
    {
        private static void Postfix(Toil __result)
        {
            Action<int> original = __result.tickIntervalAction;
            __result.tickIntervalAction = delta =>
            {
                Pawn pawn = __result.actor;
                Thing giver = pawn.CurJob.GetTarget(TargetIndex.A).Thing;
                CompManaWorkbench mana = giver?.TryGetComp<CompManaWorkbench>();
                if (mana != null && !mana.TryPayForWork(delta))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                original(delta);
            };
        }
    }
}
