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
    /// The fertile-land rite is a terrain effect, not a pawn effect: it must turn natural soil
    /// within its radius into rich soil and leave soil outside the radius alone.
    /// </summary>
    [LabTest]
    public sealed class Ritual_FertileSoil : LabTest
    {
        public override string Name => "ritual.fertile-soil";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_FertileSoil");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef greenChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Green");
            if (def == null || altarDef == null || nodeDef == null || greenChalk == null)
            {
                t.Fail("fertile-soil ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(2, 2);
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

            IntVec3 insideNear = t.Arena.Cell(7, 6);
            IntVec3 insideFar = t.Arena.Cell(10, 6);
            IntVec3 outside = t.Arena.Cell(30, 6);
            t.Arena.SetTerrain(insideNear, TerrainDefOf.Soil);
            t.Arena.SetTerrain(insideFar, TerrainDefOf.Soil);
            t.Arena.SetTerrain(outside, TerrainDefOf.Soil);

            ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Potato") ?? ThingDefOf.Plant_Potato;
            Plant plant = (Plant)ThingMaker.MakeThing(plantDef);
            GenSpawn.Spawn(plant, insideNear, t.Map, WipeMode.Vanish);

            SpawnRing(greenChalk, t, 6, 6, def.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            float before = nodeComp.Stored;
            t.Expect(comp.TryStart(def, caster), "the fertile-land rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the fertile-land rite did not finish");

            t.Expect(t.Map.terrainGrid.TerrainAt(insideNear) == TerrainDefOf.SoilRich,
                "the rite left near soil unimproved");
            t.Expect(insideNear.GetPlant(t.Map) == plant && !plant.Destroyed,
                "the rite destroyed the plant on improved soil");
            t.Expect(t.Map.terrainGrid.TerrainAt(insideFar) == TerrainDefOf.SoilRich,
                "the rite left far-but-inside soil unimproved");
            t.Expect(t.Map.terrainGrid.TerrainAt(outside) == TerrainDefOf.Soil,
                "the rite improved soil outside its radius");
            t.Measure("manaBefore", before.ToString("0.#"));
            t.Measure("manaAfter", nodeComp.Stored.ToString("0.#"));
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
