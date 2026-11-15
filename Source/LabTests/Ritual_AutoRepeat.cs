using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Auto-repeat handoff: an idle altar with a production rite selected is taken up as work by a
    /// bloodline pawn once its inputs are inside, runs to completion, and stops when the inputs run out.
    /// </summary>
    [LabTest]
    public sealed class Ritual_AutoRepeat : LabTest
    {
        public override string Name => "ritual.auto-repeat";

        // A test wait, not a design number: long enough for an idle pawn's work scan to come round.
        private const int WorkPickupTicks = 600;

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
            nodeComp.SetStored(nodeComp.Capacity);

            comp.SetAutoRitual(def);
            t.Expect(comp.AutoRitual == def, "the altar did not accept the auto-repeat rite");

            // One cycle's inputs at a time: the altar drops whatever the current collection does not count.
            DeliverCycle(t, comp, def, inputChalk, herb);
            comp.CompTickRare();
            t.Expect(t.StepUntil(() => comp.IsActive, WorkPickupTicks), "no pawn took up the auto-repeat rite as work");
            t.Expect(t.StepUntil(() => !comp.IsActive, 1000), "the first auto-repeat rite did not finish");
            t.Expect(Count(t, output) == def.outputProducts.Single(p => p.thingDef == output).count,
                "first cycle output differs from ritual Def, found " + Count(t, output));

            DeliverCycle(t, comp, def, inputChalk, herb);
            comp.CompTickRare();
            t.Expect(t.StepUntil(() => comp.IsActive, WorkPickupTicks), "no pawn took up the second auto-repeat rite");
            t.Expect(t.StepUntil(() => !comp.IsActive, 1000), "the second auto-repeat rite did not finish");
            t.Expect(Count(t, output) == def.outputProducts.Single(p => p.thingDef == output).count * 2,
                "two-cycle output differs from ritual Def, found " + Count(t, output));

            comp.CompTickRare();
            t.Expect(!t.StepUntil(() => comp.IsActive, WorkPickupTicks), "the altar started a rite after its inputs ran out");
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

        private static void DeliverCycle(TestContext t, CompRitualAltar comp, RitualDef def, ThingDef inputChalk, ThingDef herb)
        {
            Ritual_TestFixture.Deliver(t, comp, inputChalk, def.inputCosts.Single(c => c.thingDef == inputChalk).count);
            Ritual_TestFixture.Deliver(t, comp, herb, def.inputCosts.Single(c => c.thingDef == herb).count);
        }

        private static void SpawnRing(ThingDef chalkDef, TestContext t, int centerX, int centerZ, int size)
        {
            int radius = (size - 1) / 2;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != radius)
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
