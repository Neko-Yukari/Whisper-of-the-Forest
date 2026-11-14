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
            using var casting = new ResourceTestUtil.FreeCastingScope();
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(10, 5);
            t.Note("liveAtStart", t.LiveMissiles());
            float casterBefore = t.InjurySeverity(caster);

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

            int partsBefore = target.health.hediffSet.GetMissingPartsCommonAncestors().Count;
            float before = t.InjurySeverity(target);
            bool hit = t.StepUntil(() => t.InjurySeverity(target) > before
                || target.health.hediffSet.GetMissingPartsCommonAncestors().Count > partsBefore, 600);
            t.Note("hitWithin600Ticks", hit);
            t.Note("targetSeverity", t.InjurySeverity(target));
            // Enough of the scene to tell a miss from a hit that left no injury severity.
            t.Measure("targetHediffs", DescribeHediffs(target));
            t.Note("missingParts", target.health.hediffSet.GetMissingPartsCommonAncestors().Count);
            t.Note("liveAtEnd", t.LiveMissiles());
            t.Measure("arenaPawns", DescribeArenaPawns(t, caster, target));
            bool wounded = t.InjurySeverity(target) > before
                           || target.health.hediffSet.GetMissingPartsCommonAncestors().Count > partsBefore;
            t.Expect(wounded, "the dart left no injury and no destroyed part on the target");
            t.Measure("casterHediffs", DescribeHediffs(caster));
            t.Expect(t.InjurySeverity(caster) <= casterBefore, "the dart must not wound its own caster");
        }

        private static string DescribeHediffs(Pawn pawn)
        {
            var text = new System.Text.StringBuilder();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (text.Length > 0)
                {
                    text.Append(';');
                }

                text.Append(hediff.def.defName).Append('=').Append(hediff.Severity.ToString("0.#"));
            }

            return text.Length == 0 ? "none" : text.ToString();
        }

        private static string DescribeArenaPawns(TestContext t, Pawn caster, Pawn target)
        {
            var text = new System.Text.StringBuilder();
            foreach (Pawn pawn in t.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == caster || pawn == target || pawn.Dead || !t.Arena.ContainsCell(pawn.Position))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(';');
                }

                text.Append(pawn.LabelShort).Append('@').Append(pawn.Position)
                    .Append(" d=").Append(pawn.Position.DistanceTo(target.Position).ToString("0.#"));
            }

            return text.Length == 0 ? "none" : text.ToString();
        }
    }
}
