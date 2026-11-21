using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaPlantProducer : CompProperties
    {
        public float manaPerSecondPerGrowth;
        public float maxManaPerSecond;
        public float radius;
        public float minGrowth;

        public CompProperties_ManaPlantProducer()
        {
            compClass = typeof(CompManaPlantProducer);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req)) yield return entry;
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaOutputCap".Translate(),
                string.Format("WotF_Stat_Unit_ManaPerSec".Translate(), maxManaPerSecond.ToString("0.##")),
                string.Format("WotF_Stat_ManaOutputCap_Desc".Translate(), manaPerSecondPerGrowth.ToString("0.###")),
                2610);
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaPlantRadius".Translate(),
                string.Format("WotF_Stat_Unit_Cells".Translate(), radius.ToString("0.#")),
                "WotF_Stat_ManaPlantRadius_Desc".Translate(),
                2600);
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaPlantMinGrowth".Translate(),
                minGrowth.ToStringPercent(),
                "WotF_Stat_ManaPlantMinGrowth_Desc".Translate(),
                2595);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (manaPerSecondPerGrowth <= 0f)
            {
                yield return parentDef.defName + " produces no mana per unit of tree growth.";
            }

            if (maxManaPerSecond <= 0f)
            {
                yield return parentDef.defName + " has no mana output cap.";
            }

            if (radius <= 0f)
            {
                yield return parentDef.defName + " has no radius to read trees from.";
            }

            if (minGrowth < 0f || minGrowth > 1f)
            {
                yield return parentDef.defName + " has a tree growth threshold outside 0..1.";
            }
        }
    }

    /// <summary>
    /// Draws on the standing trees around the building: mature trees inside the radius add up to
    /// the output cap. Nothing is consumed, so the grove is a slow passive source that needs land
    /// and a living forest rather than fuel.
    /// </summary>
    public class CompManaPlantProducer : ThingComp
    {
        private int ticks;
        private int trees;
        private float manaPerSecond;

        public CompProperties_ManaPlantProducer Props => (CompProperties_ManaPlantProducer)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            manaPerSecond = Measure();
        }

        public override void CompTick()
        {
            base.CompTick();
            ticks++;
            if (ticks < 60)
            {
                return;
            }

            ticks = 0;
            manaPerSecond = Measure();
            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.radius);
            network?.Add(manaPerSecond);
        }

        private float Measure()
        {
            float growth = 0f;
            int count = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(parent.Position, Props.radius, true))
            {
                Plant plant = cell.GetPlant(parent.Map);
                if (plant == null || !plant.def.plant.IsTree || plant.Growth < Props.minGrowth)
                {
                    continue;
                }

                count++;
                growth += Mathf.Min(plant.Growth, 1f);
            }

            trees = count;
            return Mathf.Min(Props.maxManaPerSecond, growth * Props.manaPerSecondPerGrowth);
        }

        public override string CompInspectStringExtra()
        {
            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.radius);
            string output = "WotF_ManaGrove_Inspect".Translate(trees, manaPerSecond.ToString("0.##"));
            return network == null
                ? output + "\n" + "WotF_ManaProducer_Unlinked".Translate()
                : output + "\n" + "WotF_ManaProducer_Linked".Translate(network.nodes.Count);
        }
    }
}
