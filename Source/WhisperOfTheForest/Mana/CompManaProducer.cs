using RimWorld;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaProducer : CompProperties
    {
        public float manaPerSecond = 1.5f;
        public float linkRadius = 12.9f;

        public CompProperties_ManaProducer()
        {
            compClass = typeof(CompManaProducer);
        }
    }

    /// <summary>
    /// A device that feeds the nearest mana network while it has fuel. The refuelable comp owns the
    /// fuel and its consumption; this comp only turns a full second of burning into mana.
    /// </summary>
    public class CompManaProducer : ThingComp
    {
        private CompRefuelable fuel;
        private int ticks;

        public CompProperties_ManaProducer Props => (CompProperties_ManaProducer)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            fuel = parent.GetComp<CompRefuelable>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (fuel == null || !fuel.HasFuel)
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
            ManaNetwork network = MapComponent_ManaNetwork.For(parent.Map)
                ?.NetworkNear(parent.Position, Props.linkRadius);
            return network == null
                ? (string)"WotF_ManaProducer_Unlinked".Translate()
                : (string)"WotF_ManaProducer_Linked".Translate(network.nodes.Count);
        }
    }
}
