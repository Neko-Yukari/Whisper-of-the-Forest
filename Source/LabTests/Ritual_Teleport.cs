using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Ritual_Teleport : LabTest
    {
        public override string Name => "ritual.teleport";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_Teleport");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef teleportDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_TeleportNode");
            ThingDef greenChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Green");
            if (def == null || altarDef == null || nodeDef == null || teleportDef == null || greenChalk == null)
            {
                t.Fail("teleport ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(2, 2);
            Pawn companion = t.SpawnCaster(7, 7);
            Pawn outsider = t.SpawnCaster(20, 2);
            caster.drafter.Drafted = false;
            companion.drafter.Drafted = true;
            outsider.drafter.Drafted = true;

            Building altar = Spawn<Building>(altarDef, t, 6, 6);
            Building manaNode = Spawn<Building>(nodeDef, t, 10, 6);
            Building target = Spawn<Building>(teleportDef, t, 14, 6);
            if (altar == null || manaNode == null || target == null)
            {
                return;
            }

            CompManaBattery mana = Mana_TestFixture.AddBattery(t, manaNode);
            CompRitualAltar comp = altar.GetComp<CompRitualAltar>();
            if (mana == null || comp == null)
            {
                t.Fail("teleport altar or mana node component is missing");
                return;
            }

            SpawnRing(greenChalk, t, 6, 6, def.ringSize);
            mana.SetStored(mana.Capacity);
            float before = mana.Stored;

            t.Expect(!comp.TryStart(def, caster), "teleport started without a target node");
            t.Expect(Math.Abs(mana.Stored - before) < 0.01f, "rejected teleport spent mana");
            t.Expect(!comp.TryStart(def, manaNode), "teleport accepted a building without a node comp");
            t.Expect(Math.Abs(mana.Stored - before) < 0.01f, "invalid target spent mana");

            t.Expect(comp.TryStart(def, target), "teleport rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 3000), "teleport rite did not finish");
            t.Expect(Math.Abs(mana.Stored - (before - def.manaCost)) < 0.01f, "teleport spent the wrong amount of mana");
            t.Expect(caster.Position.InHorDistOf(target.Position, 5f), "caster did not arrive at the node");
            t.Expect(companion.Position.InHorDistOf(target.Position, 5f), "companion did not arrive at the node");
            t.Expect(!outsider.Position.InHorDistOf(target.Position, 5f), "outsider outside the radius was teleported");
            t.Measure("casterDist", caster.Position.DistanceTo(target.Position).ToString("0.##"));
            t.Measure("companionDist", companion.Position.DistanceTo(target.Position).ToString("0.##"));
            t.Measure("outsiderDist", outsider.Position.DistanceTo(target.Position).ToString("0.##"));
            t.Measure("manaAfter", mana.Stored.ToString("0.#"));
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
