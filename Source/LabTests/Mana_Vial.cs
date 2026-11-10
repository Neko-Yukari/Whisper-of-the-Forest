using System;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Bottling handoff: the altar spends network mana and materials to leave a vial, and drinking
    /// that vial moves the stored amount into the bloodline gene. A full or non-bloodline pawn is
    /// refused before the vial is spent.
    /// </summary>
    [LabTest]
    public sealed class Mana_Vial : LabTest
    {
        public override string Name => "mana.vial";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_BottleMana");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef chalkDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_White");
            ThingDef vialDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaVial");
            ThingDef steel = DefDatabase<ThingDef>.GetNamedSilentFail("Steel");
            ThingDef plasteel = DefDatabase<ThingDef>.GetNamedSilentFail("Plasteel");
            if (def == null || altarDef == null || nodeDef == null || chalkDef == null
                || vialDef == null || steel == null || plasteel == null)
            {
                t.Fail("mana vial defs are not loaded");
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

            t.Step(1);
            CompManaBattery nodeComp = Mana_TestFixture.AddBattery(t, node);
            CompRitualAltar comp = altar.GetComp<CompRitualAltar>();
            if (comp == null || nodeComp == null)
            {
                t.Fail("ritual altar or mana node component is missing");
                return;
            }

            SpawnRing(chalkDef, t, 6, 6, def.ringSize);
            SpawnStack(steel, t, 6, 8, 10);
            SpawnStack(plasteel, t, 7, 8, 2);
            nodeComp.SetStored(nodeComp.Capacity);
            ManaNetwork network = nodeComp.Network;
            if (network == null)
            {
                t.Fail("the mana node has no network after spawning");
                return;
            }

            t.Expect(comp.TryStart(def, caster), "the bottling rite did not start");
            t.Expect(network.Stored < nodeComp.Capacity, "the rite did not draw mana from the network");
            t.Measure("networkAfterStart", network.Stored.ToString("0.##"));
            t.Expect(t.StepUntil(() => !comp.IsActive, 1000), "the bottling rite did not finish");
            t.Expect(Count(t, vialDef) == 1, "expected one mana vial beside the altar, found " + Count(t, vialDef));

            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(caster);
            gene.OffsetMana(-gene.CurrentMana);
            gene.Value = 10f;

            Thing vial = First(t, vialDef);
            CompUsable usable = vial?.TryGetComp<CompUsable>();
            if (usable == null)
            {
                t.Fail("the mana vial has no CompUsable");
                return;
            }

            t.Expect(usable.CanBeUsedBy(caster).Accepted, "a bloodline pawn at partial mana was refused the vial");
            usable.UsedBy(caster);
            t.Measure("manaAfterDrink", gene.CurrentMana.ToString("0.##"));
            t.Expect(Math.Abs(gene.CurrentMana - 60f) < 0.01f,
                "expected 10 + 50 mana after drinking, found " + gene.CurrentMana);
            t.Expect(Count(t, vialDef) == 0, "drinking the vial did not consume it");

            Thing second = ThingMaker.MakeThing(vialDef);
            GenSpawn.Spawn(second, t.Arena.Cell(6, 7), t.Map, WipeMode.Vanish);
            t.Arena.Track(second);
            gene.Value = gene.MaxMana;
            t.Expect(!second.TryGetComp<CompUsable>().CanBeUsedBy(caster).Accepted,
                "a full-mana pawn was allowed to drink a vial");

            Pawn human = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, canGeneratePawnRelations: false));
            GenSpawn.Spawn(human, t.Arena.Cell(5, 5), t.Map, WipeMode.Vanish);
            t.Arena.Track(human);
            t.Expect(!second.TryGetComp<CompUsable>().CanBeUsedBy(human).Accepted,
                "a pawn without the forest bloodline was allowed to drink a vial");
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

        private static Thing First(TestContext t, ThingDef def)
        {
            foreach (Thing thing in t.Map.listerThings.ThingsOfDef(def))
            {
                if (thing.Spawned && !thing.Destroyed)
                {
                    return thing;
                }
            }

            return null;
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
            var thing = (T)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}