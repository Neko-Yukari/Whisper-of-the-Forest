using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Ritual handoff: a complete chalk ring and a charged network let the altar take the cost and
    /// start the caster's job; finishing the job restores the caster's mana; a broken ring refuses
    /// the ritual before any mana is taken.
    /// </summary>
    [LabTest]
    public sealed class Ritual_ManaSpringHandoff : LabTest
    {
        public override string Name => "ritual.ring-and-mana";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_ManaSpring");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef chalkDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            if (def == null || altarDef == null || nodeDef == null || chalkDef == null)
            {
                t.Fail("ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(4, 4);
            caster.drafter.Drafted = false;
            Building altar = Spawn<Building>(altarDef, t, 6, 6);
            Building node = Spawn<Building>(nodeDef, t, 9, 6);
            if (altar == null || node == null)
            {
                return;
            }

            List<Thing> ring = SpawnRing(chalkDef, t, 6, 6, def.ringSize);
            if (ring.Count == 0)
            {
                t.Fail("no chalk mark was spawned around the altar");
                return;
            }

            t.Step(1);
            CompManaBattery nodeComp = Mana_TestFixture.AddBattery(t, node);
            CompRitualAltar comp = altar.GetComp<CompRitualAltar>();
            if (comp == null || nodeComp == null)
            {
                t.Fail("ritual altar or mana node component is missing");
                return;
            }

            nodeComp.SetStored(nodeComp.Capacity);
            ManaNetwork network = nodeComp.Network;
            if (network == null)
            {
                t.Fail("the mana node has no network after spawning");
                return;
            }

            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(caster);
            gene.OffsetMana(-gene.CurrentMana);

            t.Expect(comp.TryStart(def, caster), "the ritual did not start with a complete ring and a charged network");
            t.Expect(comp.IsActive, "the altar did not record an active ritual");
            t.Expect(network.Stored < nodeComp.Capacity, "the altar did not take its cost from the network");
            t.Measure("networkAfterStart", network.Stored.ToString("0.##"));
            t.Measure("manaAtStart", gene.CurrentMana.ToString("0.##"));

            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the ritual did not finish within the wait limit");
            t.Measure("manaAfterRitual", gene.CurrentMana.ToString("0.##"));
            t.Expect(gene.CurrentMana > 0f, "the completed ritual restored no mana");

            ring[0].Destroy(DestroyMode.Vanish);
            float storedBeforeRefusal = network.Stored;
            t.Expect(!comp.TryStart(def, caster), "the ritual started with a broken ring");
            t.Expect(Math.Abs(network.Stored - storedBeforeRefusal) < 0.001f,
                "a refused ritual still took mana from the network");
        }

        private static List<Thing> SpawnRing(ThingDef chalkDef, TestContext t, int centerX, int centerZ, int size)
        {
            var ring = new List<Thing>();
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                    {
                        continue;
                    }

                    ring.Add(Spawn<Thing>(chalkDef, t, centerX + dx, centerZ + dz));
                }
            }

            return ring;
        }

        private static T Spawn<T>(ThingDef def, TestContext t, int x, int z) where T : Thing
        {
            var thing = (T)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}
