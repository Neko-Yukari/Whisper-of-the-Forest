using System;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Transmutation handoff: the altar must reserve the Def's input materials when the rite starts,
    /// take them only when the chant completes, and leave the Def's products beside it.
    /// </summary>
    [LabTest]
    public sealed class Ritual_Transmute : LabTest
    {
        public override string Name => "ritual.transmute";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_TransmuteGreenChalk");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar_LivingWood");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef chalkDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            ThingDef inputChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_Chalk");
            ThingDef herb = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
            ThingDef output = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkGreen");
            if (def == null || altarDef == null || nodeDef == null || chalkDef == null
                || inputChalk == null || herb == null || output == null)
            {
                t.Fail("transmutation defs are not loaded");
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

            CompManaBattery nodeComp = Mana_TestFixture.AddBattery(t, node);
            CompRitualAltar comp = altar.GetComp<CompRitualAltar>();
            if (comp == null || nodeComp == null)
            {
                t.Fail("ritual altar or mana node component is missing");
                return;
            }

            SpawnRing(chalkDef, t, 6, 6, def.ringSize);
            SpawnStack(inputChalk, t, 6, 8, def.inputCosts.Single(c => c.thingDef == inputChalk).count);
            SpawnStack(herb, t, 7, 8, def.inputCosts.Single(c => c.thingDef == herb).count);
            nodeComp.SetStored(nodeComp.Capacity);
            t.Expect(comp.TryStart(def, caster), "the transmutation did not start");
            t.Expect(Count(t, inputChalk) > 0 && Count(t, herb) > 0,
                "the altar consumed its inputs before the chant completed");
            t.Expect(t.StepUntil(() => !comp.IsActive, 1000), "the transmutation did not finish");
            t.Expect(Count(t, inputChalk) == 0 && Count(t, herb) == 0,
                "the altar did not consume its inputs when the chant completed");
            t.Expect(Count(t, output) == def.outputProducts.Single(p => p.thingDef == output).count,
                "output differs from ritual Def, found " + Count(t, output));
        }

        private static int Count(TestContext t, ThingDef def)
        {
            int total = 0;
            foreach (Thing thing in t.Map.listerThings.ThingsOfDef(def))
            {
                if (thing.Spawned && !thing.Destroyed)
                {
                    total += thing.stackCount;
                }
            }

            return total;
        }

        private static void SpawnStack(ThingDef def, TestContext t, int x, int z, int count)
        {
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = count;
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            t.Arena.Track(thing);
        }

        private static void SpawnRing(ThingDef chalkDef, TestContext t, int centerX, int centerZ, int size)
        {
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != radius)
                    {
                        continue;
                    }

                    Spawn<Thing>(chalkDef, t, centerX + dx, centerZ + dz);
                }
            }
        }

        private static T Spawn<T>(ThingDef def, TestContext t, int x, int z) where T : Thing
        {
            var thing = (T)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}
