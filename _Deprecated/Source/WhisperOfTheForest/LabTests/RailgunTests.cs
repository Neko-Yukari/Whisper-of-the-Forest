using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// The composite form's shot is not stopped by the wall between the caster and the pawn: the
    /// same resolve reports a hit on the pawn behind it.
    ///
    /// The assertion reads the cast's own resolve list, not the pawn's condition, for two measured
    /// reasons: a lethal hit removes the pawn body within the same tick, and a thing destroyed mid
    /// sweep shifts the thing list so later entries (here: the wall) can be skipped — see the
    /// wallHpAfter measurement, which is sometimes 300 while the pawn is reported as hit. Wall
    /// damage on its own is covered by railgun.damages-wall, where nothing else shares the corridor.
    /// </summary>
    [LabTest]
    public sealed class Railgun_HitsBehindWall : LabTest
    {
        public override string Name => "railgun.hits-behind-wall";

        protected override void Run(TestContext t)
        {
            // The beam runs to the map edge. Anything destructible left in it is destroyed during the
            // same sweep, which is a scene prop the test did not ask for; clear the band first.
            t.ClearBandBeyondArena(1, 5, 1);

            Pawn caster = t.SpawnCaster(1, 5);
            Thing wall = t.SpawnWall(6, 5, ThingDefOf.Steel);
            Pawn target = t.SpawnTarget(12, 5);
            int targetId = target.thingIDNumber;

            int wallHpBefore = wall.HitPoints;
            float before = t.InjurySeverity(target);
            IntVec3 targetCell = t.Arena.Cell(12, 5);
            string cellBefore = CellContents.Of(t.Map, targetCell);

            SpellRecipe recipe = TestContext.Recipe("railgun", SpellDefs.FormRailgun);
            if (!t.CastAtCell(caster, recipe, t.Arena.Cell(30, 5)))
            {
                return;
            }

            SpellRailgunSpec spec = TestContext.Glyph(SpellDefs.FormRailgun)?.railgun;
            if (spec == null)
            {
                t.Fail("the railgun rune carries no numbers");
                return;
            }

            t.Step(spec.chargeTicks + 5);
            string resolve = RailgunCastService.LastResolve ?? string.Empty;
            Thing survivor = t.Arena.Find(targetId);
            Pawn alive = survivor as Pawn;
            string cellAfter = CellContents.Of(t.Map, targetCell);
            bool bled = cellAfter.Contains("Filth_Blood") && !cellBefore.Contains("Filth_Blood");
            bool reported = resolve.Contains(target.def.defName + "@(" + targetCell.x + ", 0, " + targetCell.z + ")");

            t.Note("chargeTicks", spec.chargeTicks);
            t.Note("wallHpBefore", wallHpBefore);
            t.Note("wallHpAfter", wall.HitPoints);
            t.Note("targetReportedAsHit", reported);
            t.Note("targetGone", survivor == null);
            t.Note("targetDead", alive != null && alive.Dead);
            t.Note("targetDowned", alive != null && alive.Downed);
            t.Note("targetSeverity", alive == null ? -1f : t.InjurySeverity(alive));
            t.Note("bled", bled);
            t.Measure("cellBefore", cellBefore);
            t.Measure("cellAfter", cellAfter);
            t.Measure("hits", resolve);

            t.Expect(reported, "the pawn behind the wall is not in the resolve list: " + resolve);
        }
    }

    /// <summary>
    /// The shot damages a wall in its corridor. Recorded here because an older acceptance note said
    /// wall hit points did not change; on 1.7.4998 a steel wall goes 300 -> 180 (120 damage), so the
    /// test asserts the measured behaviour and the stale note is gone.
    /// </summary>
    [LabTest]
    public sealed class Railgun_DamagesWall : LabTest
    {
        public override string Name => "railgun.damages-wall";

        protected override void Run(TestContext t)
        {
            // See Railgun_HitsBehindWall: a plant destroyed by an earlier target in the same sweep
            // shifts the thing list under the resolver, so the scene must not contain stray props.
            t.ClearBandBeyondArena(1, 5, 1);

            Pawn caster = t.SpawnCaster(1, 5);
            Thing wall = t.SpawnWall(6, 5, ThingDefOf.Steel);
            int before = wall.HitPoints;
            t.Measure("wallBeforeCast", (wall.Spawned ? "spawned" : "unspawned") + (wall.Destroyed ? "/destroyed" : "/alive")
                                        + "@" + wall.Position + " hp=" + wall.HitPoints);

            SpellRecipe recipe = TestContext.Recipe("railgun", SpellDefs.FormRailgun);
            if (!t.CastAtCell(caster, recipe, t.Arena.Cell(30, 5)))
            {
                return;
            }

            SpellRailgunSpec spec = TestContext.Glyph(SpellDefs.FormRailgun)?.railgun;
            if (spec == null)
            {
                t.Fail("the railgun rune carries no numbers");
                return;
            }

            t.Step(spec.chargeTicks + 5);
            t.Note("wallHpBefore", before);
            t.Note("wallHpAfter", wall.HitPoints);
            t.Note("wallMaxHp", wall.MaxHitPoints);
            t.Note("listerHasWall", t.Map.listerThings.AllThings.Contains(wall));
            t.Measure("hits", RailgunCastService.LastResolve);

            t.Expect(wall.HitPoints < before, "the wall in the corridor kept all " + before + " hit points");
        }
    }

    /// <summary>Scene helpers the railgun tests need; kept here because only they look behind the beam.</summary>
    internal static class PawnState
    {
        /// <summary>True when the pawn carries any injury at all, permanent scars and fractures included.</summary>
        public static bool HasAnyInjury(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && injury.Severity > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Def names of everything standing on one cell, e.g. "Human;Corpse_Human".</summary>
    internal static class CellContents
    {
        public static string Of(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map))
            {
                return "out-of-bounds";
            }

            var parts = new List<string>();
            List<Thing> things = map.thingGrid.ThingsListAt(cell);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i]?.def != null)
                {
                    parts.Add(things[i].def.defName);
                }
            }

            return parts.Count == 0 ? "empty" : string.Join(";", parts.ToArray());
        }
    }

    /// <summary>
    /// The composite form owns its own aim limit: with range 0 it accepts a cell far past the
    /// shared 24.9-cell carrier range, while the ordinary projectile form still refuses it.
    /// </summary>
    [LabTest]
    public sealed class Railgun_AimBeyondCarrierRange : LabTest
    {
        public override string Name => "railgun.aim-beyond-carrier-range";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(1, 5);
            float carrierRange = TestContext.Tuning.castRange;
            int distance = (int)System.Math.Ceiling(carrierRange) + 10;
            if (distance > t.Arena.Width - 3)
            {
                t.Fail("arena too small for " + distance + " cells");
                return;
            }

            var aim = new TargetInfo(t.Arena.Cell(distance, 5), t.Map);
            SpellCastPlan railgun = SpellCompiler.Compile(
                TestContext.Recipe("railgun", SpellDefs.FormRailgun), out string railgunReason);
            SpellCastPlan dart = SpellCompiler.Compile(
                TestContext.Recipe("dart", SpellDefs.FormProjectile, SpellDefs.EffectHarm), out string dartReason);
            if (railgun == null || dart == null)
            {
                t.Fail("compile refused: " + (railgunReason ?? dartReason));
                return;
            }

            bool railgunTargetable = SpellService.IsTargetable(caster, railgun, aim);
            bool dartTargetable = SpellService.IsTargetable(caster, dart, aim);
            t.Note("distance", distance);
            t.Note("carrierRange", carrierRange);
            t.Note("railgunTargetable", railgunTargetable);
            t.Note("dartTargetable", dartTargetable);

            t.Expect(railgunTargetable, "the railgun refused a cell " + distance + " cells away");
            t.Expect(!dartTargetable,
                "the projectile form accepted " + distance + " cells, past the " + carrierRange + "-cell carrier range");
        }
    }
}
