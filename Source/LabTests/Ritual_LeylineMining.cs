using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Leyline handoff: the altar refuses a rite over barren ground, and a completed rite brings
    /// one portion of each deep resource up beside the altar.
    /// </summary>
    [LabTest]
    public sealed class Ritual_LeylineMining : LabTest
    {
        public override string Name => "ritual.leyline-mining";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_LeylineMining");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef starChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Star");
            if (def == null || altarDef == null || nodeDef == null || starChalk == null)
            {
                t.Fail("leyline ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(2, 2);
            caster.drafter.Drafted = false;
            Pawn helper = t.SpawnCaster(9, 6);
            helper.drafter.Drafted = false;
            helper.pather.StopDead();
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

            IntVec3 silverCell = t.Arena.Cell(7, 6);
            IntVec3 steelCell = t.Arena.Cell(8, 6);
            IntVec3 outsideCell = t.Arena.Cell(30, 6);
            t.Map.deepResourceGrid.SetAt(silverCell, ThingDefOf.Silver, 100);
            t.Map.deepResourceGrid.SetAt(steelCell, ThingDefOf.Steel, 500);
            t.Map.deepResourceGrid.SetAt(outsideCell, ThingDefOf.Jade, 500);

            int silverBefore = Count(t, ThingDefOf.Silver);
            int steelBefore = Count(t, ThingDefOf.Steel);
            SpawnRing(starChalk, t, 6, 6, def.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            float before = nodeComp.Stored;

            t.Expect(comp.TryStart(def, caster), "the leyline rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 3000), "the leyline rite did not finish");
            t.Expect(Count(t, ThingDefOf.Silver) - silverBefore == 70, "expected one silver portion, found " + (Count(t, ThingDefOf.Silver) - silverBefore));
            t.Expect(Count(t, ThingDefOf.Steel) - steelBefore == 45, "expected one steel portion, found " + (Count(t, ThingDefOf.Steel) - steelBefore));
            t.Expect(Count(t, ThingDefOf.Jade) == 0, "the rite mined outside its radius");
            t.Measure("manaBefore", before.ToString("0.#"));
            t.Measure("manaAfter", nodeComp.Stored.ToString("0.#"));

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(altar.Position, def.effectRadius, true))
            {
                if (cell.InBounds(t.Map))
                {
                    t.Map.deepResourceGrid.SetAt(cell, null, 0);
                }
            }

            float after = nodeComp.Stored;
            t.Expect(!comp.TryStart(def, caster), "the leyline rite started over barren ground");
            t.Expect(Math.Abs(nodeComp.Stored - after) < 0.01f, "the rejected rite still spent mana");
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