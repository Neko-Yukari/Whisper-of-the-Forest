using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaPowerProducer : CompProperties
    {
        public float manaPerSecond;
        public float linkRadius;

        public CompProperties_ManaPowerProducer()
        {
            compClass = typeof(CompManaPowerProducer);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
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

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (manaPerSecond <= 0f)
            {
                yield return parentDef.defName + " converts power into no mana.";
            }

            if (linkRadius <= 0f)
            {
                yield return parentDef.defName + " has no radius to link to a mana network.";
            }

            if (parentDef.CompDefForAssignableFrom<CompPowerTrader>() == null)
            {
                yield return parentDef.defName + " needs a CompPowerTrader to run the mana converter.";
            }
        }
    }

    /// <summary>
    /// Turns the colony's power grid into mana while the building is powered. The power trader owns
    /// the watt draw; this comp only converts a full second of powered operation into mana.
    /// </summary>
    public class CompManaPowerProducer : ThingComp
    {
        private CompPowerTrader power;
        private int ticks;

        public CompProperties_ManaPowerProducer Props => (CompProperties_ManaPowerProducer)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            power = parent.GetComp<CompPowerTrader>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!ManaSwitchUtility.IsSwitchedOn(parent) || !power.PowerOn)
            {
                ticks = 0;
                return;
            }

            ticks++;
            if (ticks < 60)
            {
                return;
            }

            ticks = 0;
            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.linkRadius);
            network?.Add(Props.manaPerSecond);
        }

        public override string CompInspectStringExtra()
        {
            if (!ManaSwitchUtility.IsSwitchedOn(parent))
            {
                return "WotF_ManaSwitch_Off".Translate();
            }

            string state = power.PowerOn
                ? (string)"WotF_ManaConverter_On".Translate(Props.manaPerSecond.ToString("0.##"))
                : (string)"WotF_ManaConverter_Off".Translate();
            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.linkRadius);
            return network == null
                ? state + "\n" + "WotF_ManaProducer_Unlinked".Translate()
                : state + "\n" + "WotF_ManaProducer_Linked".Translate(network.nodes.Count);
        }
    }
}
