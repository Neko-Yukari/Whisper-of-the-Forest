using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Save/load handoff for the network: battery charge is saved on the battery, and the link graph is
    /// rebuilt from the spawned buildings after the load.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Mana_NetworkSurvivesReload : LabTest
    {
        private int nodeAId;
        private int nodeBId;
        private int batteryId;

        public override string Name => "mana.network-survives-reload";

        protected override void Run(TestContext t)
        {
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef furnaceDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaFurnace");
            if (nodeDef == null || furnaceDef == null)
            {
                t.Fail("mana building defs are not loaded");
                return;
            }

            Building nodeA = Spawn(nodeDef, t, 5, 5);
            CompManaBattery battery = Mana_TestFixture.AddBattery(t, nodeA);
            batteryId = battery.parent.thingIDNumber;
            Building nodeB = Spawn(nodeDef, t, 9, 5);
            Building furnace = Spawn(furnaceDef, t, 6, 5);
            if (nodeA == null || nodeB == null || furnace == null)
            {
                return;
            }

            nodeAId = nodeA.thingIDNumber;
            nodeBId = nodeB.thingIDNumber;
            // Production is covered without Reload; parked scenes must not advance a long charge cycle.
            battery.SetStored(battery.Capacity);
            t.Step(1);

            ManaNetwork network = nodeA.GetComp<CompManaNode>().Network;
            t.Expect(network != null, "the node has no network before the save");
            t.Expect(network.Stored > 0f, "the network has no charge before the save");
            t.Note("storedBeforeSave", network.Stored);
        }

        protected override void AfterReload(TestContext t)
        {
            var nodeA = t.Arena.Find(nodeAId) as Building;
            var nodeB = t.Arena.Find(nodeBId) as Building;
            t.Arena.Track(nodeA);
            t.Arena.Track(nodeB);

            if (nodeA == null || nodeB == null)
            {
                t.Fail("a mana node did not survive the save/load");
                return;
            }

            CompManaNode a = nodeA.GetComp<CompManaNode>();
            CompManaNode b = nodeB.GetComp<CompManaNode>();
            var battery = (Building)t.Arena.Find(batteryId);
            t.Arena.Track(battery);
            t.Note("storedAfterLoad", battery.GetComp<CompManaBattery>().Stored);
            t.Expect(battery.GetComp<CompManaBattery>().Stored > 0f, "the battery lost its charge during the save/load");
            t.Expect(a.Network != null && a.Network == b.Network,
                "the link graph was not rebuilt after the load");
        }

        private static Building Spawn(ThingDef def, TestContext t, int x, int z)
        {
            var building = (Building)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(building, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(building);
        }
    }
}
