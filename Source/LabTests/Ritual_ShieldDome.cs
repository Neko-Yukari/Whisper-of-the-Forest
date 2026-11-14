using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The grove shield rite re-arms the altar's interceptor after the ring and performance complete.
    /// </summary>
    [LabTest]
    public sealed class Ritual_ShieldDome : LabTest
    {
        public override string Name => "ritual.shield-dome";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_ShieldDome");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar_LivingWood");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef greenChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Green");
            if (def == null || altarDef == null || nodeDef == null || greenChalk == null)
            {
                t.Fail("shield ritual defs are not loaded");
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
            CompProjectileInterceptor shield = altar.GetComp<CompProjectileInterceptor>();
            if (comp == null || nodeComp == null || shield == null)
            {
                t.Fail("ritual altar, mana node or shield component is missing");
                return;
            }

            SpawnRing(greenChalk, t, 6, 6, def.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            float before = nodeComp.Stored;
            t.Expect(!shield.Active, "the altar shield was already active before the rite");
            t.Expect(comp.TryStart(def, caster), "the shield rite did not start");
            t.Expect(t.StepUntil(() => !comp.IsActive, 2000), "the shield rite did not finish");
            t.Expect(shield.Active, "the altar shield did not activate");
            t.Expect(shield.currentHitPoints == shield.HitPointsMax, "the altar shield did not start at full charge");
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
            var thing = (T)ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.BlocksGranite : null);
            GenSpawn.Spawn(thing, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(thing);
        }
    }
}