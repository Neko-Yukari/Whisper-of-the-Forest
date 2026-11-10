using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Mana;
using WhisperOfTheForest.Rituals;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Beast-call handoff: the rite spends its focus and mana, leaves one animal beside the altar,
    /// and a failed taming attempt sends that animal away instead of leaving it wild.
    /// </summary>
    [LabTest]
    public sealed class Ritual_BeastCall : LabTest
    {
        public override string Name => "ritual.beast-call";

        protected override void Run(TestContext t)
        {
            RitualDef def = DefDatabase<RitualDef>.GetNamedSilentFail("WotF_Ritual_BeastCall");
            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_RitualAltar");
            ThingDef nodeDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ManaNode");
            ThingDef greenChalk = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_ChalkMark_Green");
            PawnKindDef horse = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
            if (def == null || altarDef == null || nodeDef == null || greenChalk == null || horse == null)
            {
                t.Fail("beast-call ritual defs are not loaded");
                return;
            }

            Pawn caster = t.SpawnCaster(2, 2);
            caster.drafter.Drafted = false;
            caster.skills.GetSkill(SkillDefOf.Animals).Level = 20;
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

            Thing hay = ThingMaker.MakeThing(ThingDefOf.Hay);
            hay.stackCount = 50;
            GenSpawn.Spawn(hay, t.Arena.Cell(7, 6), t.Map, WipeMode.Vanish);
            t.Arena.Track(hay);
            int hayBefore = Count(t, ThingDefOf.Hay);

            SpawnRing(greenChalk, t, 6, 6, def.ringSize);
            nodeComp.SetStored(nodeComp.Capacity);
            int horsesBefore = CountHorses(t, horse);
            List<int> horsesBeforeIds = HorseIds(t, horse);
            float before = nodeComp.Stored;

            t.Expect(comp.TryStart(def, caster), "the beast-call rite did not start");
            t.Measure("jobAtStart", caster.CurJobDef == null ? "null" : caster.CurJobDef.defName);
            var trace = new System.Text.StringBuilder();
            string[] last = { caster.CurJobDef == null ? "null" : caster.CurJobDef.defName };
            t.Expect(t.StepUntil(() => TraceRitualJob(caster, comp, trace, last), 3000),
                "the beast-call rite did not finish");
            t.Measure("casterJobs", trace.Length == 0 ? "none" : trace.ToString());
            t.Measure("casterState", "downed=" + caster.Downed + " injury=" + t.InjurySeverity(caster).ToString("0.#"));
            t.Expect(Count(t, ThingDefOf.Hay) == hayBefore - 50, "the rite did not consume its hay focus");
            t.Expect(Math.Abs(nodeComp.Stored - (before - def.manaCost)) < 0.01f, "the rite spent the wrong amount of mana");

            t.Note("horsesBefore", horsesBefore);
            t.Note("horsesAfter", CountHorses(t, horse));
            Pawn summoned = null;
            Pawn tamed = null;
            foreach (Pawn pawn in t.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.kindDef != horse || !pawn.Position.InHorDistOf(altar.Position, 5f)
                    || horsesBeforeIds.Contains(pawn.thingIDNumber))
                {
                    continue;
                }

                summoned = pawn;
                if (pawn.Faction == Faction.OfPlayer)
                {
                    tamed = pawn;
                    break;
                }
            }

            if (summoned != null)
            {
                // The animal the rite left belongs to this test's scene. Untracked, a tamed one stays in
                // the colony and every later pass starts with it already standing beside the altar.
                t.Arena.Track(summoned);
            }

            t.Expect(tamed != null, "no tamed horse appeared beside the altar");
            t.Measure("horses", DescribeHorses(t, altar, horse));
            t.Note("spawnRoom", CellFinder.TryRandomClosewalkCellNear(altar.Position, t.Map, 2, out IntVec3 roomCell));
            Pawn probe = PawnGenerator.GeneratePawn(new PawnGenerationRequest(horse, null, PawnGenerationContext.NonPlayer, canGeneratePawnRelations: false));
            t.Note("probeCanTame", TameUtility.CanTame(probe));
            t.Note("probeScaria", probe.health.hediffSet.HasHediff(HediffDefOf.Scaria));
            probe.Destroy(DestroyMode.Vanish);
            t.Measure("manaBefore", before.ToString("0.#"));
            t.Measure("manaAfter", nodeComp.Stored.ToString("0.#"));

            float after = nodeComp.Stored;
            t.Expect(!comp.TryStart(def, caster), "the rite started without its hay focus");
            t.Expect(Math.Abs(nodeComp.Stored - after) < 0.01f, "the rejected rite still spent mana");

            Pawn wild = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                horse,
                null,
                PawnGenerationContext.NonPlayer,
                canGeneratePawnRelations: false));
            GenSpawn.Spawn(wild, t.Arena.Cell(15, 15), t.Map, WipeMode.Vanish);
            t.Arena.Track(wild);
            t.Expect(!RitualEffect_BeastCall.TryTame(caster, wild, 0f), "a zero-chance taming attempt succeeded");
            t.Expect(!wild.Spawned, "the animal did not leave after a failed taming attempt");
        }

        private static bool TraceRitualJob(Pawn caster, CompRitualAltar comp, System.Text.StringBuilder trace, string[] last)
        {
            string now = caster.CurJobDef == null ? "null" : caster.CurJobDef.defName;
            if (now != last[0])
            {
                trace.Append(last[0]).Append("->").Append(now).Append(';');
                last[0] = now;
            }

            return !comp.IsActive;
        }

        private static List<int> HorseIds(TestContext t, PawnKindDef horse)
        {
            var ids = new List<int>();
            foreach (Pawn pawn in t.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.kindDef == horse)
                {
                    ids.Add(pawn.thingIDNumber);
                }
            }

            return ids;
        }

        private static int CountHorses(TestContext t, PawnKindDef horse)
        {
            int total = 0;
            foreach (Pawn pawn in t.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.kindDef == horse)
                {
                    total++;
                }
            }

            return total;
        }

        private static string DescribeHorses(TestContext t, Building altar, PawnKindDef horse)
        {
            var text = new System.Text.StringBuilder();
            foreach (Pawn pawn in t.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.kindDef != horse || pawn.Position.DistanceTo(altar.Position) > 15f)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(';');
                }

                text.Append(pawn.LabelShort).Append('@').Append(pawn.Position)
                    .Append(" faction=").Append(pawn.Faction == null ? "none" : pawn.Faction.Name)
                    .Append(" d=").Append(pawn.Position.DistanceTo(altar.Position).ToString("0.#"))
                    .Append(" wild=").Append(pawn.GetStatValue(StatDefOf.Wildness).ToString("0.##"));
            }

            return text.Length == 0 ? "none" : text.ToString();
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
