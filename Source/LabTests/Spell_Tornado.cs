using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Spell_Tornado : LabTest
    {
        public override string Name => "spell.tornado";

        protected override void Run(TestContext t)
        {
            SpellGlyphDef glyph = TestContext.Glyph("WotF_Super_Tornado");
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            book.Unlock(glyph);
            SpellRecipe recipe = TestContext.Recipe("flame-tornado", glyph.defName);
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out string reason);
            t.Expect(plan != null && plan.form == SpellFormKind.Special && plan.tornado != null,
                "Super tornado did not compile: " + reason);
            if (plan == null) return;
            Pawn caster = t.SpawnCaster(2, 5);
            BloodlineKit.Gene(t, caster).GainExperience(BloodlineKit.XpFromOneTo(BloodlineKit.Def.maxLevel));
            Pawn center = t.SpawnTarget(15, 5);
            Pawn edge = t.SpawnTarget(19, 5);
            Pawn leaver = t.SpawnTarget(15, 6);
            Thing wall = t.SpawnWall(16, 5, ThingDefOf.Plasteel);
            // Keep native anatomy from capping damage or killing the dummy before ten pulses.
            float healthScale = center.RaceProps.baseHealthScale;
            center.RaceProps.baseHealthScale = 100f;
            t.Note("fixtureHumanHealthScale", center.HealthScale);
            try
            {
                foreach (IntVec3 cell in GenRadial.RadialCellsAround(center.Position, 4f, true))
                    t.Arena.SetTerrain(cell, TerrainDefOf.WaterShallow);
                t.Expect(book.SaveSlot(caster, 0, recipe, out reason), "save tornado slot: " + reason);
                book.SetActiveSlot(caster, 0);
                Command_Target command = t.SpellAbility(caster).GetGizmos().OfType<Command_Target>().Single();
                bool accepted = command.targetingParams.CanTarget(new TargetInfo(center));
                t.Note("nativeTargetAccepted", accepted);
                t.Expect(!command.Disabled && accepted, "tornado gizmo refused: " + command.disabledReason);
                if (command.Disabled || !accepted) return;
                float centerBefore = Damage(center), edgeBefore = Damage(edge), leaveBefore = Damage(leaver);
                int wallBefore = wall.HitPoints;
                int start = t.Tick;
                command.action(new LocalTargetInfo(center));
                var field = t.Map.listerThings.AllThings.OfType<Thing_SpellTornado>().Single();
                t.Arena.Track(field);
                var outsidePulses = new List<string>();
                int removedAt = -1;
                float outsidePrevious = 0f;
                int lastOutsidePulse = 0;
                for (int tick = 1; tick <= 600; tick++)
                {
                    t.Step(1);
                    int elapsed = t.Tick - start;
                    float actualCenter = Damage(center) - centerBefore;
                    float actualEdge = Damage(edge) - edgeBefore;
                    t.Expect(BurnDamage(center) == 0f && BurnDamage(edge) == 0f, "inside DOT damage at tick=" + elapsed);
                    t.Expect(Math.Abs(actualCenter - (elapsed / 60) * 20) < 0.01f,
                        "inside tick=" + elapsed + " damage=" + actualCenter + " hediffs=" + TestContext.HediffDump(center));
                    t.Expect(Math.Abs(actualEdge - actualCenter) < 0.01f,
                        "edge falloff tick=" + elapsed + " center=" + actualCenter + " edge=" + actualEdge);
                    if (elapsed % 60 == 0)
                    {
                        int remaining = Scorch(center).TryGetComp<HediffComp_Disappears>().ticksToDisappear;
                        t.Measure("pulse" + elapsed, "center=" + actualCenter + " edge=" + actualEdge + " scorch=" + remaining);
                        t.Expect(remaining >= 285 && remaining <= 300, "scorch refresh remaining=" + remaining);
                    }
                    if (elapsed == 180)
                    {
                        outsidePrevious = Damage(leaver);
                        t.Note("leaveDamage", outsidePrevious - leaveBefore);
                        t.Place(leaver, 30, 5);
                    }
                    if (elapsed > 180)
                    {
                        float current = Damage(leaver);
                        if (current > outsidePrevious)
                        {
                            t.Expect(Math.Abs(current - outsidePrevious - 20f) < 0.01f, "DOT amount=" + (current - outsidePrevious));
                            if (lastOutsidePulse != 0)
                                t.Expect(elapsed - lastOutsidePulse == 60, "DOT cadence tick=" + elapsed);
                            lastOutsidePulse = elapsed;
                            t.Expect(elapsed <= 480, "DOT after 300-tick expiry at " + elapsed);
                            outsidePulses.Add((elapsed - 180) + ":" + (current - outsidePrevious));
                            outsidePrevious = current;
                        }
                        if (removedAt < 0 && Scorch(leaver) == null) removedAt = elapsed - 180;
                    }
                }
                t.Measure("outsidePulses", string.Join(",", outsidePulses));
                t.Note("insideDotDamage", BurnDamage(center) + BurnDamage(edge));
                t.Note("scorchRemovedAfterLeave", removedAt);
                t.Measure("gizmoWallHp", wallBefore + "->" + wall.HitPoints);
                t.Measure("insideDamageBeforeAfter", centerBefore + "->" + Damage(center));
                t.Expect(outsidePulses.Count > 0, "no scorch damage after leaving");
                t.Expect(removedAt > 0 && removedAt <= 300, "scorch did not expire within 300 ticks: " + removedAt);
                t.Expect(wallBefore - wall.HitPoints == 200, "wall damage=" + (wallBefore - wall.HitPoints));
                t.Expect(!field.Destroyed, "field disappeared before fade");
                t.Step(29);
                t.Expect(!field.Destroyed, "field disappeared before tick 630");
                t.Step(1);
                t.Expect(field.Destroyed, "field remained after tick 630");
                int wallAfter = wall.HitPoints;
                Pawn late = t.SpawnTarget(15, 4);
                float lateBefore = Damage(late);
                float leaveAfter = Damage(leaver);
                t.Step(360);
                t.Measure("postFieldWallHp", wallAfter + "->" + wall.HitPoints);
                t.Note("lateTargetDamage", Damage(late) - lateBefore);
                t.Expect(wall.HitPoints == wallAfter && Damage(late) == lateBefore, "field dealt damage after destruction");
                t.Expect(Damage(leaver) == leaveAfter, "expired scorch kept damaging leaver");
                t.Expect(Scorch(center) == null, "final scorch did not expire");
            }
            finally
            {
                center.RaceProps.baseHealthScale = healthScale;
            }
        }

        private static float BurnDamage(Pawn pawn) => pawn.health.hediffSet.hediffs
            .OfType<Hediff_Injury>().Where(h => h.def == DamageDefOf.Burn.hediff).Sum(h => h.Severity);

        private static float Damage(Pawn pawn) => pawn.records.GetValue(RecordDefOf.DamageTaken);
        private static Hediff Scorch(Pawn pawn) => pawn.health.hediffSet.GetFirstHediffOfDef(
            DefDatabase<HediffDef>.GetNamed("WotF_Hediff_Scorch"));
    }
}
