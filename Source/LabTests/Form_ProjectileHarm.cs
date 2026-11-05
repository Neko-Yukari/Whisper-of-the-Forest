using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Spell cast to missile impact: a projectile dart flies, the missile is in the air, and the
    /// harm effect resolves on the pawn it reaches. This is the cast -> projectile -> impact
    /// handoff, not a check of the dart's numbers.
    /// </summary>
    [LabTest]
    public sealed class Form_ProjectileHarm : LabTest
    {
        public override string Name => "form.projectile-harm";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(10, 5);

            SpellRecipe recipe = TestContext.Recipe("dart-harm", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            // The cast has phases now: the dart leaves when the channel ends, not inside the call.
            bool launched = t.StepUntil(() => t.LiveMissiles() >= 1, 200);
            t.Note("launchedWithin200Ticks", launched);
            t.Note("liveAtLaunch", t.LiveMissiles());
            t.Expect(launched, "no dart spawned for a projectile cast");

            float before = t.InjurySeverity(target);
            bool hit = t.StepUntil(() => t.InjurySeverity(target) > before, 600);
            t.Note("hitWithin600Ticks", hit);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), before, "target injury severity after the dart landed");
            t.Expect(t.InjurySeverity(caster) <= 0f, "the dart must not wound its own caster");
        }
    }
}
