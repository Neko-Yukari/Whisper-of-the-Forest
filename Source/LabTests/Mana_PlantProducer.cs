using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Plant producer handoff: mature trees inside the grove radius feed the nearest network,
    /// and removing them stops the feed without draining what is already stored.
    /// </summary>
    [LabTest]
    public sealed class Mana_PlantProducer : LabTest
    {
        public override string Name => "mana.plant-producer";

        protected override void Run(TestContext t)
        {
            Map map = t.Map;
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef groveDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaGrove");
            if (nodeDef == null || groveDef == null)
            {
                t.Fail("mana node or grove def is not loaded");
                return;
            }

            Building node = Spawn(nodeDef, t, 5, 5);
            Mana_TestFixture.AddBattery(t, node);
            Building grove = Spawn(groveDef, t, 6, 5);
            if (node == null || grove == null)
            {
                return;
            }

            float radius = grove.GetComp<CompManaPlantProducer>().Props.radius;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(grove.Position, radius, true))
            {
                if (cell.InBounds(map))
                {
                    cell.GetPlant(map)?.Destroy(DestroyMode.Vanish);
                }
            }

            MapComponent_ManaNetwork.For(map)?.MarkDirty();
            t.Step(60);

            List<Plant> trees = new List<Plant>();
            for (int i = 0; i < 10; i++)
            {
                Plant tree = (Plant)ThingMaker.MakeThing(ThingDefOf.Plant_TreeOak);
                GenSpawn.Spawn(tree, t.Arena.Cell(8 + i, 5), map, WipeMode.Vanish);
                tree.Growth = 1f;
                trees.Add(tree);
            }

            MapComponent_ManaNetwork component = MapComponent_ManaNetwork.For(map);
            if (component == null)
            {
                t.Fail("MapComponent_ManaNetwork is not present on the map");
                return;
            }

            if (component.Networks.Count == 0)
            {
                t.Fail("the mana network graph is empty");
                return;
            }
            ManaNetwork network = node.GetComp<CompManaNode>().Network;
            float before = network.Stored;
            t.Step(60);
            float withTrees = network.Stored;
            t.Measure("storedWithTrees", withTrees.ToString("0.##"));
            t.Expect(withTrees - before >= 0.4f,
                "ten mature trees should add about 0.5 mana, got " + (withTrees - before).ToString("0.##"));

            for (int i = 0; i < trees.Count; i++)
            {
                trees[i].Destroy(DestroyMode.Vanish);
            }

            float afterDestroy = network.Stored;
            t.Step(60);
            t.Measure("storedAfterTreesRemoved", network.Stored.ToString("0.##"));
            t.Expect(Mathf.Abs(network.Stored - afterDestroy) < 0.01f,
                "the grove kept feeding after its trees were removed");
        }

        private static Building Spawn(ThingDef def, TestContext t, int x, int z)
        {
            var building = (Building)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(building, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(building);
        }
    }
}
