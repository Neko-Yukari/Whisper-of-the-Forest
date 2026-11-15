using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Rune awakening handoff: with the chosen rune's materials delivered into the altar, the rite keeps
    /// them and the network's mana while it chants, takes them when the chant completes, and unlocks the
    /// rune colony-wide only then; a rune whose materials are missing refuses before any mana is taken.
    /// </summary>
    [LabTest]
    public sealed class Ritual_RuneAwakeningHandoff : LabTest
    {
        public override string Name => "ritual.rune-awakening";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_RuneAwakening");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar_Starlight");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef chalkDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Star");
            SpellGlyphDef jadeRune = DefDatabase<SpellGlyphDef>.GetNamedSilentFail("WotF_Effect_Blink");
            SpellGlyphDef steelRune = DefDatabase<SpellGlyphDef>.GetNamedSilentFail("WotF_Effect_Lightning");
            if (def == null || altarDef == null || nodeDef == null || chalkDef == null
                || jadeRune == null || steelRune == null)
            {
                t.Fail("ritual or rune defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(4, 4);
            caster.drafter.Drafted = false;
            Building altar = Spawn<Building>(altarDef, t, 6, 6);
            Building node = Spawn<Building>(nodeDef, t, 10, 6);
            if (altar == null || node == null)
            {
                return;
            }

            SpawnRing(DefDatabase<ThingDef>.GetNamed("WotF_ChalkMark_White"), t, 6, 6,
                def.RuneTierFor(1).ringSize);
            SpawnRing(DefDatabase<ThingDef>.GetNamed("WotF_ChalkMark_Green"), t, 6, 6,
                def.RuneTierFor(2).ringSize);
            List<Thing> ring = SpawnRing(chalkDef, t, 6, 6, def.RuneTierFor(jadeRune.tier).ringSize);
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

            comp.Prepare(def, jadeRune);
            Thing jade = Ritual_TestFixture.Deliver(t, comp, ThingDefOf.Jade,
                jadeRune.unlockCost.Find(cost => cost.thingDef == ThingDefOf.Jade).count);
            foreach (SpellGlyphCost cost in jadeRune.unlockCost)
            {
                if (cost.thingDef == ThingDefOf.Jade) continue;
                Ritual_TestFixture.Deliver(t, comp, cost.thingDef, cost.count);
            }

            nodeComp.SetStored(nodeComp.Capacity);
            ManaNetwork network = nodeComp.Network;
            if (network == null)
            {
                t.Fail("the mana node has no network after spawning");
                return;
            }

            float storedBefore = network.Stored;
            t.Expect(!GameComponent_Spellbook.IsUnlockedInData(jadeRune), "the rune was unlocked before the rite started");
            t.Expect(comp.TryStart(def, jadeRune, caster), "the rune awakening did not start with a complete ring and materials");
            t.Expect(!jade.Destroyed && jade.stackCount > 0,
                "the altar consumed the rune's jade before the chant completed");
            t.Expect(Math.Abs(network.Stored - storedBefore) < 0.001f,
                "the altar took its mana cost before the chant completed");
            t.Expect(!GameComponent_Spellbook.IsUnlockedInData(jadeRune), "the rune unlocked before the rite finished");
            t.Measure("networkAfterStart", network.Stored.ToString("0.##"));

            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the rite did not finish within the wait limit");
            t.Expect(jade.Destroyed || jade.stackCount == 0, "the altar did not consume the rune's jade");
            t.Expect(network.Stored < storedBefore, "the altar did not take its mana cost from the network");
            t.Expect(GameComponent_Spellbook.IsUnlockedInData(jadeRune), "the finished rite did not unlock the rune");

            float storedBeforeRefusal = network.Stored;
            comp.Prepare(def, steelRune);
            t.Expect(!comp.TryStart(def, steelRune, caster), "a rune with missing materials still started");
            t.Expect(Math.Abs(network.Stored - storedBeforeRefusal) < 0.001f,
                "a refused rune awakening still took mana from the network");
            t.Expect(!GameComponent_Spellbook.IsUnlockedInData(steelRune), "a refused rune awakening still unlocked its rune");
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
            var thing = (T)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}
