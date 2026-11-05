using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// Deprecated 2026-11-05: asserts a design parameter (which form may target which distance),
    /// which changes with the design and only produces false failures. Kept for the record; not
    /// compiled and not part of the L2 suite.
    /// </summary>
    [LabTest]
    public sealed class Form_SelfHarm : LabTest
    {
        public override string Name => "form.self-harm";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn bystander = t.SpawnTarget(9, 5);

            SpellRecipe recipe = TestContext.Recipe("self-harm", SpellDefs.FormSelf, SpellDefs.EffectHarm);
            bool cast = t.CastAtSelf(caster, recipe);
            t.Note("cast", cast);

            float casterSeverity = t.InjurySeverity(caster);
            t.Note("casterSeverity", casterSeverity);
            t.Note("bystanderSeverity", t.InjurySeverity(bystander));

            t.ExpectGreater(casterSeverity, 0f, "caster injury severity after a self cast");
            t.Expect(t.InjurySeverity(bystander) <= 0f, "a self cast must not touch another pawn");
        }
    }

    /// <summary>Deprecated 2026-11-05: design-parameter check of the touch form.</summary>
    [LabTest]
    public sealed class Form_TouchHarm : LabTest
    {
        public override string Name => "form.touch-harm";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(3, 5);

            SpellRecipe recipe = TestContext.Recipe("touch-harm", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            bool cast = t.CastAt(caster, recipe, target);
            t.Note("cast", cast);
            t.Note("targetSeverity", t.InjurySeverity(target));
            t.ExpectGreater(t.InjurySeverity(target), 0f, "adjacent target injury severity after a touch cast");
            t.Expect(t.InjurySeverity(caster) <= 0f, "a touch cast must not wound the caster");
        }
    }

    /// <summary>Deprecated 2026-11-05: asserts the touch form's reach, a design parameter.</summary>
    [LabTest]
    public sealed class Form_TouchOutOfReach : LabTest
    {
        public override string Name => "form.touch-out-of-reach";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn far = t.SpawnTarget(6, 5);

            SpellRecipe recipe = TestContext.Recipe("touch-harm", SpellDefs.FormTouch, SpellDefs.EffectHarm);
            SpellCastPlan plan = SpellCompiler.Compile(recipe, out string planReason);
            if (plan == null)
            {
                t.Fail("compile refused: " + planReason);
                return;
            }

            bool targetable = SpellService.IsTargetable(caster, plan, new TargetInfo(far.Position, far.Map));
            t.Note("targetableAt4Cells", targetable);
            t.Expect(!targetable, "touch accepted a pawn 4 cells away");

            bool cast = SpellService.TryCast(caster, recipe, new LocalTargetInfo(far), out string reason);
            t.Note("cast", cast);
            t.Measure("castReason", reason ?? "accepted");
            t.Expect(!cast, "touch cast was accepted at 4 cells");
            t.Expect(t.InjurySeverity(far) <= 0f, "a refused touch cast still wounded the target");
        }
    }
}
