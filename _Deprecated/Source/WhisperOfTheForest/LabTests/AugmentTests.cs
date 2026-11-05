using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// Amplify multiplies the damage of the effect on its left. Walls are used instead of pawns so
    /// the delta is exact: a pawn splits random damage over random body parts, a building just loses
    /// hit points.
    /// </summary>
    [LabTest]
    public sealed class Augment_Amplify : LabTest
    {
        public override string Name => "augment.amplify";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Thing wall = t.SpawnWall(3, 5, ThingDefOf.WoodLog);
            int before = wall.HitPoints;

            SpellRecipe plain = TestContext.Recipe("touch-harm", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            SpellRecipe amplified = TestContext.Recipe("touch-harm-amplified", SpellDefs.FormTouch,
                SpellDefs.EffectHarm, SpellDefs.AugmentAmplify);

            if (!t.CastAt(caster, plain, wall))
            {
                return;
            }

            int afterPlain = wall.HitPoints;
            int plainDamage = before - afterPlain;
            t.Note("hpBefore", before);
            t.Note("plainDamage", plainDamage);
            t.ExpectGreater(plainDamage, 0, "plain touch harm damage");

            t.ClearCooldown(caster);
            if (!t.CastAt(caster, amplified, wall))
            {
                return;
            }

            int amplifiedDamage = afterPlain - wall.HitPoints;
            t.Note("amplifiedDamage", amplifiedDamage);
            t.Note("hpAfterAll", wall.HitPoints);

            // Harm is 9 and amplify 1.5x, both read from the Defs by the compiler.
            t.Expect(amplifiedDamage > plainDamage * 1.2f,
                "amplify did not increase the damage: plain=" + plainDamage + " amplified=" + amplifiedDamage);
        }
    }

    /// <summary>
    /// AOE widens Grow to a radius built from the Def numbers. The far plant proves the radius is
    /// finite; the middle plant proves it reaches past the base radius.
    /// </summary>
    [LabTest]
    public sealed class Augment_Area : LabTest
    {
        public override string Name => "augment.area";

        protected override void Run(TestContext t)
        {
            SpellTuningDef tuning = TestContext.Tuning;
            SpellGlyphDef aoe = TestContext.Glyph(SpellDefs.AugmentAoe);
            float radius = tuning.aoeBaseRadius + aoe.radius;

            int middleDistance = (int)System.Math.Floor(tuning.aoeBaseRadius) + 1;
            int farDistance = (int)System.Math.Ceiling(radius) + 2;
            t.Note("expectedRadius", radius);
            t.Note("middleDistance", middleDistance);
            t.Note("farDistance", farDistance);
            if (middleDistance >= farDistance || farDistance > t.Arena.Width - 2)
            {
                t.Fail("arena does not fit the radius test: middle=" + middleDistance + " far=" + farDistance);
                return;
            }

            Pawn caster = t.SpawnCaster(1, 5);
            for (int x = 1; x <= farDistance + 1; x++)
            {
                t.SetTerrain(x, 5, TerrainDefOf.Soil);
            }

            Plant near = t.SpawnPlant(2, 5, 0.1f);
            Plant middle = t.SpawnPlant(1 + middleDistance, 5, 0.1f);
            Plant far = t.SpawnPlant(1 + farDistance, 5, 0.1f);

            SpellRecipe recipe = TestContext.Recipe("grow-area", SpellDefs.FormSelf, SpellDefs.EffectGrow,
                SpellDefs.AugmentAoe);
            if (!t.CastAtSelf(caster, recipe))
            {
                return;
            }

            t.Note("nearGrowth", near.Growth);
            t.Note("middleGrowth", middle.Growth);
            t.Note("farGrowth", far.Growth);
            t.Expect(near.Growth >= 0.999f, "the plant next to the caster did not grow");
            t.Expect(middle.Growth >= 0.999f,
                "the plant at distance " + middleDistance + " did not grow although the AOE radius is " + radius);
            t.Expect(far.Growth < 0.999f,
                "the plant at distance " + farDistance + " grew although the radius is only " + radius);
        }
    }

    /// <summary>
    /// Accelerate raises the launch speed of the dart it follows (read from the live missile's own
    /// settings against the Def numbers) and the same flight then lands sooner.
    /// </summary>
    [LabTest]
    public sealed class Augment_Accelerate : LabTest
    {
        public override string Name => "augment.accelerate";

        protected override void Run(TestContext t)
        {
            SpellTuningDef tuning = TestContext.Tuning;
            SpellGlyphDef glyph = TestContext.Glyph(SpellDefs.AugmentAccelerate);
            float expectedFast = Mathf.Clamp(tuning.missileSpeed * glyph.factor, 2f, 60f);

            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(20, 5);

            int plainTicks = Flight(t, caster, target,
                TestContext.Recipe("dart-harm", SpellDefs.FormProjectile, SpellDefs.EffectHarm), out float plainSpeed);

            t.ClearCooldown(caster);
            int fastTicks = Flight(t, caster, target,
                TestContext.Recipe("dart-fast", SpellDefs.FormProjectile, SpellDefs.AugmentAccelerate, SpellDefs.EffectHarm),
                out float fastSpeed);

            t.Note("plainSpeed", plainSpeed);
            t.Note("acceleratedSpeed", fastSpeed);
            t.Note("expectedAcceleratedSpeed", expectedFast);
            t.Note("plainTicks", plainTicks);
            t.Note("acceleratedTicks", fastTicks);
            if (plainTicks > 0 && fastTicks > 0)
            {
                t.Note("ratio", fastTicks / (float)plainTicks);
            }

            t.Expect(Mathf.Abs(plainSpeed - tuning.missileSpeed) < 0.5f,
                "plain dart speed: expected " + tuning.missileSpeed + ", got " + plainSpeed);
            t.Expect(Mathf.Abs(fastSpeed - expectedFast) < 0.5f,
                "accelerated dart speed: expected " + expectedFast + ", got " + fastSpeed);
            t.Expect(plainTicks > 0 && fastTicks > 0, "a dart never landed: plain=" + plainTicks + " fast=" + fastTicks);
            t.Expect(fastTicks < plainTicks,
                "accelerate did not shorten the flight: plain=" + plainTicks + " fast=" + fastTicks);
        }

        private static int Flight(TestContext t, Pawn caster, Pawn target, SpellRecipe recipe, out float speed)
        {
            speed = -1f;
            float before = t.InjurySeverity(target);
            int start = t.Tick;
            if (!t.CastAt(caster, recipe, target))
            {
                return -1;
            }

            speed = t.LiveMissileSpeed();
            return t.StepUntil(() => t.InjurySeverity(target) > before, 600) ? t.Tick - start : -1;
        }
    }

    /// <summary>Decelerate lowers the launch speed of the dart it follows and the flight lands later.</summary>
    [LabTest]
    public sealed class Augment_Decelerate : LabTest
    {
        public override string Name => "augment.decelerate";

        protected override void Run(TestContext t)
        {
            SpellTuningDef tuning = TestContext.Tuning;
            SpellGlyphDef glyph = TestContext.Glyph(SpellDefs.AugmentDecelerate);
            float expectedSlow = Mathf.Clamp(tuning.missileSpeed * glyph.factor, 2f, 60f);

            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(20, 5);

            int plainTicks = Flight(t, caster, target,
                TestContext.Recipe("dart-harm", SpellDefs.FormProjectile, SpellDefs.EffectHarm), out float plainSpeed);

            t.ClearCooldown(caster);
            int slowTicks = Flight(t, caster, target,
                TestContext.Recipe("dart-slow", SpellDefs.FormProjectile, SpellDefs.AugmentDecelerate, SpellDefs.EffectHarm),
                out float slowSpeed);

            t.Note("plainSpeed", plainSpeed);
            t.Note("deceleratedSpeed", slowSpeed);
            t.Note("expectedDeceleratedSpeed", expectedSlow);
            t.Note("plainTicks", plainTicks);
            t.Note("deceleratedTicks", slowTicks);
            if (plainTicks > 0 && slowTicks > 0)
            {
                t.Note("ratio", slowTicks / (float)plainTicks);
            }

            t.Expect(Mathf.Abs(slowSpeed - expectedSlow) < 0.5f,
                "decelerated dart speed: expected " + expectedSlow + ", got " + slowSpeed);
            t.Expect(plainTicks > 0 && slowTicks > 0, "a dart never landed: plain=" + plainTicks + " slow=" + slowTicks);
            t.Expect(slowTicks > plainTicks,
                "decelerate did not lengthen the flight: plain=" + plainTicks + " slow=" + slowTicks);
        }

        private static int Flight(TestContext t, Pawn caster, Pawn target, SpellRecipe recipe, out float speed)
        {
            speed = -1f;
            float before = t.InjurySeverity(target);
            int start = t.Tick;
            if (!t.CastAt(caster, recipe, target))
            {
                return -1;
            }

            speed = t.LiveMissileSpeed();
            return t.StepUntil(() => t.InjurySeverity(target) > before, 600) ? t.Tick - start : -1;
        }
    }
    /// <summary>Split adds the Def's missile count to the volley of the form on its left.</summary>
    [LabTest]
    public sealed class Augment_Split : LabTest
    {
        public override string Name => "augment.split";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(14, 5);

            SpellTuningDef tuning = TestContext.Tuning;
            SpellGlyphDef split = TestContext.Glyph(SpellDefs.AugmentSplit);
            int expected = tuning.missileCount + split.missiles;
            t.Note("expectedMissiles", expected);

            SpellRecipe recipe = TestContext.Recipe("dart-split", SpellDefs.FormProjectile, SpellDefs.AugmentSplit,
                SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            int live = t.LiveMissiles();
            t.Note("liveAtLaunch", live);
            t.Expect(live == expected, "split volley: expected " + expected + " darts, got " + live);

            t.StepUntil(() => t.LiveMissiles() == 0, 600);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), 0f, "target severity after a split volley");
        }
    }
}
