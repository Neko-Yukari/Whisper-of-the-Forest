using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Cast fire -> movement handoff: a pawn that walks during the windup keeps walking, keeps the
    /// cast, and the dart still leaves. The cast never takes over the pawn's job, which is the whole
    /// reason the windup may move.
    /// </summary>
    [LabTest]
    public sealed class Casting_WindupMovement : LabTest
    {
        public override string Name => "casting.windup-movement";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnTarget(10, 5);

            SpellRecipe recipe = TestContext.Recipe("walk-cast", SpellDefs.FormProjectile, SpellDefs.EffectHarm);
            if (!t.CastAt(caster, recipe, target))
            {
                return;
            }

            t.Expect(SpellCastingService.IsCasting(caster), "the cast is not in progress right after the order");

            IntVec3 start = caster.Position;
            Job walk = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(t.Arena.Cell(2, 9)));
            caster.jobs.TryTakeOrderedJob(walk, null, false);

            bool moved = t.StepUntil(() => caster.Position != start, 60);
            t.Note("movedWhileCasting", moved);
            t.Note("castingAfterMove", SpellCastingService.IsCasting(caster));
            t.Expect(moved, "the caster could not move while winding up");
            t.Expect(SpellCastingService.IsCasting(caster), "walking dropped the cast");

            bool launched = t.StepUntil(() => t.LiveMissiles() >= 1, 200);
            t.Note("launchedAfterMove", launched);
            t.Expect(launched, "the dart never left the caster after it walked");
        }
    }
}
