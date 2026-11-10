using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaBattery : CompProperties
    {
        public float capacity;
        public float linkRadius;

        public CompProperties_ManaBattery()
        {
            compClass = typeof(CompManaBattery);
        }

        // Ensures: one error per field that is not positive; values live only in the Def.
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (capacity <= 0f) yield return "capacity must be positive";
            if (linkRadius <= 0f) yield return "linkRadius must be positive";
        }
    }

    /// <summary>
    /// The only place mana is stored. A battery joins the network of the nearest node within its own
    /// link radius; it never links batteries or devices to each other, that is the node's job.
    /// </summary>
    public class CompManaBattery : ThingComp
    {
        private float stored;
        private ManaNetwork network;

        public CompProperties_ManaBattery Props => (CompProperties_ManaBattery)props;

        public float Stored => stored;
        public float Capacity => Props.capacity;
        public float LinkRadius => Props.linkRadius;

        /// <summary>Null while no node is in reach; the charge stays in the battery meanwhile.</summary>
        public ManaNetwork Network
        {
            get => network;
            internal set => network = value;
        }

        // Ensures: the map's network graph is marked dirty so this battery is attached on the next rebuild.
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            MapComponent_ManaNetwork.For(parent.Map).MarkDirty();
        }

        // Ensures: the previous map's graph is marked dirty; the stored charge is lost with the building.
        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            stored = 0f;
            network = null;
            MapComponent_ManaNetwork.For(previousMap)?.MarkDirty();
        }

        // Ensures: stored is saved under "manaStored".
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref stored, "manaStored", 0f);
        }

        // Ensures: stored is clamped to [0, Capacity].
        internal void SetStored(float value)
        {
            stored = value < 0f ? 0f : value > Capacity ? Capacity : value;
        }

        // Ensures: shows stored/capacity and whether the battery is linked to a network.
        public override string CompInspectStringExtra()
        {
            ManaNetwork linked = parent.Spawned
                ? MapComponent_ManaNetwork.For(parent.Map).NetworkNear(parent.Position, LinkRadius) : null;
            return string.Format("WotF_ManaBattery_Inspect".Translate(), Stored.ToString("0"), Capacity.ToString("0"))
                + "\n" + (linked == null ? "WotF_ManaProducer_Unlinked".Translate().ToString()
                    : string.Format("WotF_ManaProducer_Linked".Translate(), linked.nodes.Count));
        }
    }
}
