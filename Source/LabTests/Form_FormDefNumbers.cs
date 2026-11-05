using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Missiles;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// A spell dart flies with its own form Def's projectile block: the speed the form declares, as
    /// changed by Accelerate, and the form's own flight range. No shared tuning number is involved.
    /// </summary>
    [LabTest]
    public sealed class Form_ProjectileFromFormDef : LabTest
    {
        public override string Name => "form.projectile-from-form-def";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(14, 5);

            SpellGlyphDef form = TestContext.Glyph(SpellDefs.FormProjectile);
            SpellGlyphDef accelerate = TestContext.Glyph(SpellDefs.AugmentAccelerate);
            float formSpeed = form.projectile.speedCellsPerSecond;
            SpellRecipe recipe = TestContext.Recipe("fast-dart",
                SpellDefs.FormProjectile, SpellDefs.AugmentAccelerate, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            if (!t.StepUntil(() => t.LiveMissiles() >= 1, 200))
            {
                t.Fail("no dart spawned for a projectile cast");
                return;
            }

            IReadOnlyList<ArcaneMissileLabFrame> frames = t.Map.GetComponent<MapComponent_ArcaneMissiles>()?.GetLabFrames();
            if (frames == null || frames.Count == 0)
            {
                t.Fail("the live dart has no lab frame");
                return;
            }

            ArcaneMissileSettings flying = frames[0].Settings;
            float expectedSpeed = formSpeed * accelerate.factor;
            t.Note("speed", flying.speedCellsPerSecond);
            t.Note("speedExpected", expectedSpeed);
            t.Note("maxRange", flying.maxRangeCells);
            t.Note("maxRangeExpected", form.projectile.maxRangeCells);
            t.Expect(System.Math.Abs(flying.speedCellsPerSecond - expectedSpeed) < 0.01f,
                "dart speed " + flying.speedCellsPerSecond + ", expected the form's " + formSpeed
                + " x accelerate " + accelerate.factor);
            t.Expect(System.Math.Abs(flying.maxRangeCells - form.projectile.maxRangeCells) < 0.01f,
                "dart range " + flying.maxRangeCells + ", expected the form's " + form.projectile.maxRangeCells);
            t.Expect(flying.payload is SpellMissilePayload, "the dart must carry the spell plan as its payload");

            // The form Def itself is untouched by the augment: the plan changed a copy.
            t.Note("formSpeedAfterCast", form.projectile.speedCellsPerSecond);
            t.Expect(form.projectile.speedCellsPerSecond == formSpeed, "compiling a spell changed the form Def's own speed");
        }
    }

    /// <summary>
    /// A pierced dart hands its payload on: the missile layer only reports the hit, the spell layer
    /// relaunches the same dart past it with one pierce fewer, along the heading it hit with, and that
    /// dart resolves once more on the pawn standing behind. One Pierce means exactly two impacts.
    /// </summary>
    [LabTest]
    public sealed class Form_ProjectilePierce : LabTest
    {
        public override string Name => "form.projectile-pierce";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn front = t.SpawnTarget(8, 5);
            Pawn behind = t.SpawnTarget(12, 5);
            MapComponent_ArcaneMissiles missiles = t.Map.GetComponent<MapComponent_ArcaneMissiles>();
            int impactsBefore = missiles.TotalImpacts;

            SpellRecipe recipe = TestContext.Recipe("pierce-dart",
                SpellDefs.FormProjectile, SpellDefs.AugmentPierce, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, front))
            {
                return;
            }

            bool frontHit = t.StepUntil(() => t.InjurySeverity(front) > 0f, 600);
            bool relaunched = t.StepUntil(() => missiles.TotalImpacts - impactsBefore >= 2, 600);

            // Let anything that might still be flying land before counting.
            t.StepUntil(() => t.LiveMissiles() == 0, 600);
            int impacts = missiles.TotalImpacts - impactsBefore;
            t.Note("frontSeverity", t.InjurySeverity(front));
            t.Note("behindSeverity", t.InjurySeverity(behind));
            t.Note("impacts", impacts);
            t.Expect(frontHit, "the aimed target was not hit");
            t.Expect(relaunched, "the pierced dart was not relaunched past the first hit");
            t.Expect(t.InjurySeverity(behind) > 0f, "the pawn behind the aimed target was not hit by the pierced dart");
            t.Expect(impacts == 2, "one Pierce must resolve exactly two impacts, got " + impacts);
        }
    }

    /// <summary>An orbit cast places the form's orbs around the caster, and an orb that passes a pawn applies the effect.</summary>
    [LabTest]
    public sealed class Form_OrbitTouches : LabTest
    {
        public override string Name => "form.orbit-touches";

        protected override void Run(TestContext t)
        {
            SpellGlyphDef form = TestContext.Glyph(SpellDefs.FormOrbit);
            Pawn caster = t.SpawnCaster(4, 5);

            // Two cells east: the orbit passes within its hit radius of this cell's centre.
            Pawn target = t.SpawnTarget(6, 5);

            SpellRecipe recipe = TestContext.Recipe("orbit-harm", SpellDefs.FormOrbit, SpellDefs.EffectHarm);
            if (!t.CastAtSelf(caster, recipe))
            {
                return;
            }

            ThingDef orbDef = DefDatabase<ThingDef>.GetNamed(SpellEffectDefs.ThingSpellOrbit);
            bool placed = t.StepUntil(() => t.Map.listerThings.ThingsOfDef(orbDef).Count > 0, 200);
            int orbs = t.Map.listerThings.ThingsOfDef(orbDef).Count;
            t.Note("orbs", orbs);
            t.Expect(placed && orbs == form.orbit.count, "expected " + form.orbit.count + " orb(s), got " + orbs);

            float before = t.InjurySeverity(target);
            bool hit = t.StepUntil(() => t.InjurySeverity(target) > before, form.orbit.lifetimeTicks);
            t.Note("hitWithinLifetime", hit);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), before, "target injury severity after the orb passed");
            t.Expect(t.InjurySeverity(caster) <= 0f, "the orb must not wound its own caster");
        }
    }
}
