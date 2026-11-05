using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// A dart that is still flying when the player saves must be in the air after the load, hit the
    /// same target, and resolve exactly once. The runner performs the real save/load between
    /// <see cref="Run"/> and <see cref="AfterReload"/>; the projectile is found again by its
    /// thingIDNumber, which survives the save.
    /// </summary>
    [LabTest(Reload = true)]
    public sealed class Reload_ProjectileMidflight : LabTest
    {
        private int casterId;
        private int targetId;
        private float severityBefore;
        private int impactsBefore;

        public override string Name => "reload.projectile-midflight";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(17, 5);
            casterId = caster.thingIDNumber;
            targetId = target.thingIDNumber;
            severityBefore = t.InjurySeverity(target);
            impactsBefore = t.MissileImpacts();

            SpellRecipe recipe = TestContext.Recipe("dart-harm", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            t.Expect(t.StepUntil(() => t.LiveMissiles() >= 1, 200),
                "the dart never left the caster; the cast did not reach its release");
            t.Step(3);
            int live = t.LiveMissiles();
            t.Note("liveBeforeSave", live);
            t.Note("impactsBeforeSave", t.MissileImpacts() - impactsBefore);
            t.Expect(live >= 1, "the dart was not in flight when the save was taken");
            t.Expect(t.MissileImpacts() == impactsBefore, "the dart already landed; it must be mid-flight at the save");
        }

        protected override void AfterReload(TestContext t)
        {
            Thing casterThing = t.Arena.Find(casterId);
            Thing targetThing = t.Arena.Find(targetId);
            t.Arena.Track(casterThing);
            t.Arena.Track(targetThing);

            if (!(targetThing is Pawn target))
            {
                t.Fail("the target pawn did not survive the save/load");
                return;
            }

            int live = t.LiveMissiles();
            t.Note("liveAfterLoad", live);
            t.Expect(live >= 1, "the dart disappeared during the save/load");

            bool hit = t.StepUntil(() => t.InjurySeverity(target) > severityBefore, 600);
            t.Step(5);
            int impacts = t.MissileImpacts();
            t.Note("hitWithin600Ticks", hit);
            t.Note("impactsAfterLoad", impacts);
            t.Note("targetSeverity", t.InjurySeverity(target));

            t.Expect(hit, "the restored dart never hit its target");
            t.Expect(impacts == 1, "expected exactly one impact after the load, got " + impacts);
        }
    }
}
