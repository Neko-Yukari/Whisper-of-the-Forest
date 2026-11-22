using RimWorld;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaProducer : CompProperties
    {
        public float manaPerSecond;
        public float linkRadius;

        public CompProperties_ManaProducer()
        {
            compClass = typeof(CompManaProducer);
        }

        public override System.Collections.Generic.IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req)) yield return entry;
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaOutput".Translate(),
                string.Format("WotF_Stat_Unit_ManaPerSec".Translate(), manaPerSecond.ToString("0.##")),
                "WotF_Stat_ManaOutput_Desc".Translate(),
                2610);
            yield return new StatDrawEntry(
                StatCategoryDefOf.Building,
                "WotF_Stat_ManaLinkRadius".Translate(),
                string.Format("WotF_Stat_Unit_Cells".Translate(), linkRadius.ToString("0.#")),
                "WotF_Stat_ManaLinkRadius_Desc".Translate(),
                2600);
        }

        // Ensures: the rates the producer reads and the fuel it burns exist before play.
        public override System.Collections.Generic.IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (manaPerSecond <= 0f) yield return "manaPerSecond must be positive";
            if (linkRadius <= 0f) yield return "linkRadius must be positive";
            CompProperties_Refuelable fuel = parentDef.GetCompProperties<CompProperties_Refuelable>();
            if (fuel == null || fuel.fuelConsumptionRate <= 0f || !fuel.consumeFuelOnlyWhenUsed)
                yield return "mana furnace requires refuelable with positive consumption and consumeFuelOnlyWhenUsed";
        }
    }

    public class CompManaProducer : ThingComp
    {
        private CompRefuelable fuel;

        public CompProperties_ManaProducer Props => (CompProperties_ManaProducer)props;

        private ManaNetwork Network => MapComponent_ManaNetwork.For(parent.Map)
            ?.NetworkNear(parent.Position, Props.linkRadius);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            fuel = parent.GetComp<CompRefuelable>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!ManaSwitchUtility.IsSwitchedOn(parent)) return;
            if (!fuel.HasFuel) return;
            ManaNetwork network = Network;
            if (network == null || network.Stored >= network.Capacity) return;

            float added = network.Add(Props.manaPerSecond / 60f);
            // Refuelable's rate is per game day; only burn the fraction actually stored.
            fuel.ConsumeFuel(fuel.Props.fuelConsumptionRate / 60000f
                * (added / (Props.manaPerSecond / 60f)));
        }

        public override string CompInspectStringExtra()
        {
            if (!ManaSwitchUtility.IsSwitchedOn(parent)) return "WotF_ManaSwitch_Off".Translate();
            ManaNetwork network = Network;
            if (network == null) return "WotF_ManaProducer_Unlinked".Translate();
            string text = string.Format("WotF_ManaProducer_Linked".Translate(), network.nodes.Count);
            return network.Stored >= network.Capacity
                ? text + "\n" + "WotF_ManaFurnace_Full".Translate()
                : text;
        }
    }
}
