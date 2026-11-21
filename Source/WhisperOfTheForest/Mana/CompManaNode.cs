using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaNode : CompProperties
    {
        public float linkRadius;

        public CompProperties_ManaNode()
        {
            compClass = typeof(CompManaNode);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req)) yield return entry;
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
            if (linkRadius <= 0f) yield return "linkRadius must be positive";
        }
    }

    public class CompManaNode : ThingComp
    {
        private ManaNetwork network;

        public CompProperties_ManaNode Props => (CompProperties_ManaNode)props;

        public float LinkRadius => Props.linkRadius;

        public ManaNetwork Network
        {
            get => network;
            internal set => network = value;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            MapComponent_ManaNetwork.For(parent.Map)?.MarkDirty();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            MapComponent_ManaNetwork.For(previousMap)?.MarkDirty();
        }

        public override string CompInspectStringExtra()
        {
            int links = network == null ? 1 : network.nodes.Count;
            return string.Format("WotF_ManaNode_Inspect".Translate(), links);
        }
    }
}
