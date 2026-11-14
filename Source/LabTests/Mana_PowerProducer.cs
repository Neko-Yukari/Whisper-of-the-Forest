using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Power producer handoff: while the converter is powered it feeds the nearest network,
    /// and switching the power off stops the feed.
    /// </summary>
    [LabTest]
    public sealed class Mana_PowerProducer : LabTest
    {
        public override string Name => "mana.power-producer";

        protected override void Run(TestContext t)
        {
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef converterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaConverter");
            if (nodeDef == null || converterDef == null)
            {
                t.Fail("mana node or converter def is not loaded");
                return;
            }

            Building node = Spawn(nodeDef, t, 5, 5);
            Mana_TestFixture.AddBattery(t, node);
            Building converter = Spawn(converterDef, t, 6, 5);
            if (node == null || converter == null)
            {
                return;
            }

            CompPowerTrader power = converter.GetComp<CompPowerTrader>();
            CompManaPowerProducer producer = converter.GetComp<CompManaPowerProducer>();
            if (power == null || producer == null)
            {
                t.Fail("the converter is missing its power or mana comp");
                return;
            }

            t.Measure("powerOutput", power.PowerOutput.ToString("0"));
            t.Expect(power.PowerOutput < 0f, "the converter does not draw power: " + power.PowerOutput);

            MapComponent_ManaNetwork component = MapComponent_ManaNetwork.For(t.Map);
            if (component == null || component.Networks.Count == 0)
            {
                t.Fail("the mana network graph is empty");
                return;
            }

            ManaNetwork network = node.GetComp<CompManaNode>().Network;
            float before = network.Stored;
            power.PowerOn = true;
            for (int i = 0; i < 60; i++)
            {
                producer.CompTick();
            }

            float powered = network.Stored;
            t.Measure("storedPowered", powered.ToString("0.##"));
            t.Expect(powered > before,
                "a powered converter added no mana: " + before + " -> " + powered);

            power.PowerOn = false;
            for (int i = 0; i < 60; i++)
            {
                producer.CompTick();
            }

            t.Measure("storedUnpowered", network.Stored.ToString("0.##"));
            t.Expect(Mathf.Abs(network.Stored - powered) < 0.01f,
                "the converter kept feeding after power was cut");
        }

        private static Building Spawn(ThingDef def, TestContext t, int x, int z)
        {
            var building = (Building)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(building, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(building);
        }
    }
}
