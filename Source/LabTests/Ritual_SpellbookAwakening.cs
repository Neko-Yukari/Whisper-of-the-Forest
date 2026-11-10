using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Spellbook awakening handoff: the rite must leave a wearable book beside the altar, and
    /// wearing that book must turn a bloodline pawn without one into an eligible caster.
    /// </summary>
    [LabTest]
    public sealed class Ritual_SpellbookAwakening : LabTest
    {
        public override string Name => "ritual.spellbook-awakening";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_SpellbookAwakening");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef chalkDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            ThingDef bookDef = DefDatabase<ThingDef>.GetNamedSilentFail(TestContext.SpellbookDefName);
            GeneDef bloodline = DefDatabase<GeneDef>.GetNamedSilentFail("WotF_ForestBloodline");
            if (def == null || altarDef == null || nodeDef == null || chalkDef == null
                || bookDef == null || bloodline == null)
            {
                t.Fail("spellbook awakening defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(4, 4);
            caster.drafter.Drafted = false;
            Pawn learner = t.SpawnTarget(10, 10);
            learner.genes.AddGene(bloodline, false);
            learner.drafter.Drafted = false;
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
            t.Expect(!SpellService.IsEligibleCaster(learner, out _),
                "a bloodline pawn without a spellbook was already an eligible caster");
            t.Expect(comp.TryStart(def, caster), "the spellbook awakening did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the spellbook awakening did not finish");
            Apparel book = t.Map.listerThings.ThingsOfDef(bookDef)
                .OfType<Apparel>()
                .FirstOrDefault(b => b.ParentHolder is Map);
            t.Expect(book != null, "the rite left no spellbook on the ground beside the altar");
            if (book == null)
            {
                return;
            }

            book.DeSpawn();
            learner.apparel.Wear(book, false, true);
            t.Expect(SpellService.IsEligibleCaster(learner, out string reason),
                "wearing the awakened spellbook did not make the pawn eligible: " + reason);
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
            var thing = (T)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}
