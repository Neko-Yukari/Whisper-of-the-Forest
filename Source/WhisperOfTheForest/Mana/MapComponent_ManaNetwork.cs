using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Mana
{
    /// <summary>
    /// Rebuilds the mana link graph after a node is built, destroyed or loaded. The graph is not
    /// saved: node membership is derived from buildings, and the stored mana itself lives on the
    /// batteries. A rebuild is cheap and only happens when something changes.
    /// </summary>
    public class MapComponent_ManaNetwork : MapComponent
    {
        private readonly List<ManaNetwork> networks = new List<ManaNetwork>();
        private bool dirty = true;

        public MapComponent_ManaNetwork(Map map) : base(map)
        {
        }

        public IReadOnlyList<ManaNetwork> Networks
        {
            get
            {
                EnsureBuilt();
                return networks;
            }
        }

        public static MapComponent_ManaNetwork For(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_ManaNetwork>();
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            dirty = true;
            EnsureBuilt();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            EnsureBuilt();
        }

        /// <summary>The nearest connected node's network within <paramref name="radius"/> cells.</summary>
        public ManaNetwork NetworkNear(IntVec3 cell, float radius)
        {
            return NodeNear(cell, radius)?.Network;
        }

        internal CompManaNode NodeNear(IntVec3 cell, float radius)
        {
            EnsureBuilt();
            CompManaNode best = null;
            float bestDistance = radius * radius;
            for (int i = 0; i < networks.Count; i++)
            {
                ManaNetwork network = networks[i];
                for (int j = 0; j < network.nodes.Count; j++)
                {
                    CompManaNode node = network.nodes[j];
                    float distance = (node.parent.Position - cell).LengthHorizontalSquared;
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        best = node;
                    }
                }
            }

            return best;
        }

        private void EnsureBuilt()
        {
            if (!dirty)
            {
                return;
            }

            dirty = false;
            networks.Clear();

            List<CompManaNode> nodes = new List<CompManaNode>();
            Collect(nodes, map.listerBuildings.allBuildingsColonist);
            Collect(nodes, map.listerBuildings.allBuildingsNonColonist);

            var positions = new List<IntVec3>(nodes.Count);
            var radii = new List<float>(nodes.Count);
            foreach (CompManaNode node in nodes)
            {
                positions.Add(node.parent.Position);
                radii.Add(node.LinkRadius);
            }

            int[] group = ManaTopology.Group(positions, radii);
            for (int i = 0; i < nodes.Count; i++)
            {
                if (group[i] == networks.Count)
                {
                    networks.Add(new ManaNetwork());
                }

                ManaNetwork network = networks[group[i]];
                network.nodes.Add(nodes[i]);
                nodes[i].Network = network;
            }

            var batteries = new List<CompManaBattery>();
            Collect(batteries, map.listerBuildings.allBuildingsColonist);
            Collect(batteries, map.listerBuildings.allBuildingsNonColonist);
            foreach (CompManaBattery battery in batteries)
            {
                battery.Network = NetworkNear(battery.parent.Position, battery.LinkRadius);
                battery.Network?.batteries.Add(battery);
            }
        }

        private static void Collect<T>(List<T> nodes, List<Building> buildings) where T : ThingComp
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                T node = buildings[i].TryGetComp<T>();
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
        }
    }
}
