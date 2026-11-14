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
    /// Participants handoff: a bloodline pawn standing on the chalk ring widens the effect radius,
    /// and a rite that declares a minimum refuses to start without one.
    /// </summary>
    [LabTest]
    public sealed class Ritual_Participants : LabTest
    {
        public override string Name => "ritual.participants";

        protected override void Run(TestContext t)
        {
            RitualDef spring = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_ManaSpring");
            RitualDef leyline = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_LeylineMining");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar_Starlight");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef whiteChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            ThingDef starChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Star");
            if (spring == null || leyline == null || altarDef == null || nodeDef == null
                || whiteChalk == null || starChalk == null)
            {
                t.Fail("participant ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(4, 4);
            caster.drafter.Drafted = false;
            Pawn patient = t.SpawnCaster(20, 6);
            Building altar = Spawn<Building>(altarDef, t, 6, 6);
            Building node = Spawn<Building>(nodeDef, t, 10, 6);
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

            Gene_ForestBloodline patientGene = Gene_ForestBloodline.Get(patient);
            if (patientGene == null)
            {
                t.Fail("the patient has no bloodline gene");
                return;
            }

            patientGene.OffsetMana(-patientGene.CurrentMana);
            SpawnRing(whiteChalk, t, 6, 6, spring.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);

            t.Expect(comp.TryStart(spring, caster), "the rite did not start with a complete ring");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the rite did not finish");
            float withoutHelper = patientGene.CurrentMana;
            t.Measure("patientManaWithoutHelper", withoutHelper.ToString("0.#"));
            t.Expect(withoutHelper < spring.manaRestore * 0.5f, "the patient was restored without a helper");

            Pawn helper = t.SpawnCaster(7, 6);
            Gene_ForestBloodline.Get(helper).level = 10;
            helper.drafter.Drafted = false;
            helper.pather.StopDead();
            patientGene.OffsetMana(-patientGene.CurrentMana);
            nodeComp.SetStored(nodeComp.Capacity);

            t.Expect(comp.TryStart(spring, null, caster, new List<Pawn> { helper }), "the rite did not start with a helper");
            t.Expect(comp.ActiveParticipants == 1, "the altar did not count the helper on the ring");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the rite with a helper did not finish");
            float withHelper = patientGene.CurrentMana;
            t.Measure("patientManaWithHelper", withHelper.ToString("0.#"));
            t.Expect(withHelper - withoutHelper >= spring.manaRestore * 0.5f, "the helper did not widen the effect radius");

            SpawnRing(starChalk, t, 6, 6, leyline.ringSize);
            t.Map.deepResourceGrid.SetAt(t.Arena.Cell(7, 6), ThingDefOf.Silver, 100);
            t.Place(helper, 7, 6);
            nodeComp.SetStored(nodeComp.Capacity);
            float beforeRefusal = nodeComp.Stored;
            t.Expect(!comp.TryStart(leyline, caster), "the leyline rite started without a participant");
            t.Expect(Math.Abs(nodeComp.Stored - beforeRefusal) < 0.01f, "the refused rite still spent mana");

            t.Place(helper, 9, 6);
            t.Expect(comp.TryStart(leyline, null, caster, new List<Pawn> { helper }), "the leyline rite did not start with a participant");
            t.Expect(comp.ActiveParticipants == 1, "the leyline rite did not count its participant");
            comp.CancelRitual(caster);
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
