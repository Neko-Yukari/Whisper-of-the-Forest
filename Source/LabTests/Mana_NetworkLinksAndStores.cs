using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Network handoff: nearby nodes form one pool, a distant node stays separate, a fuelled
    /// producer fills the pool, and destroying a node re-derives the graph without losing stored mana.
    /// </summary>
    [LabTest]
    public sealed class Mana_NetworkLinksAndStores : LabTest
    {
        public override string Name => "mana.network-links-and-stores";

        protected override void Run(TestContext t)
        {
            Map map = t.Map;
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef furnaceDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaFurnace");
            if (nodeDef == null || furnaceDef == null)
            {
                t.Fail("mana building defs are not loaded");
                return;
            }

            Building nearA = Spawn(nodeDef, t, 5, 5);
            Mana_TestFixture.AddBattery(t, nearA);
            Building nearB = Spawn(nodeDef, t, 10, 5);
            Building far = Spawn(nodeDef, t, 30, 20);
            Building furnace = Spawn(furnaceDef, t, 6, 5);
            if (nearA == null || nearB == null || far == null || furnace == null)
            {
                return;
            }

            MapComponent_ManaNetwork component = MapComponent_ManaNetwork.For(map);
            if (component == null)
            {
                t.Fail("MapComponent_ManaNetwork is not present on the map");
                return;
            }

            t.Expect(component.Networks.Count == 2,
                "expected two networks (two near nodes and one far node), got " + component.Networks.Count);
            t.Expect(nearA.GetComp<CompManaNode>().Network == nearB.GetComp<CompManaNode>().Network,
                "nearby nodes are not on the same network");
            t.Expect(nearA.GetComp<CompManaNode>().Network != far.GetComp<CompManaNode>().Network,
                "the distant node joined the near network");

            CompRefuelable fuel = furnace.GetComp<CompRefuelable>();
            fuel.Refuel(fuel.Props.fuelCapacity);
            t.Step(180);

            ManaNetwork nearNetwork = nearA.GetComp<CompManaNode>().Network;
            t.Measure("nearStored", nearNetwork.Stored.ToString("0.##"));
            t.Measure("nearCapacity", nearNetwork.Capacity.ToString("0.##"));
            t.Expect(nearNetwork.Stored > 0f, "the fuelled producer added no mana to the near network");

            float storedBeforeDestroy = nearNetwork.Stored;
            nearB.Destroy(DestroyMode.Vanish);
            t.Step(1);

            t.Expect(component.Networks.Count == 2,
                "after destroying one near node the graph should keep two networks, got " + component.Networks.Count);
            t.Expect(nearA.GetComp<CompManaNode>().Network.Stored > 0f,
                "stored mana disappeared when the neighbouring node was destroyed");
            t.Measure("storedBeforeDestroy", storedBeforeDestroy.ToString("0.##"));
            t.Measure("storedAfterDestroy", nearA.GetComp<CompManaNode>().Network.Stored.ToString("0.##"));
        }

        private static Building Spawn(ThingDef def, TestContext t, int x, int z)
        {
            var building = (Building)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(building, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(building);
        }
    }
}
