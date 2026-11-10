using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest]
    public sealed class Effect_SnareMovement : LabTest
    {
        public override string Name => "effect.snare-movement";

        protected override void Run(TestContext t)
        {
            Pawn caster = t.SpawnCaster(2, 5);
            Pawn target = t.SpawnCaster(10, 5);
            HediffDef snare = DefDatabase<HediffDef>.GetNamed(SpellEffectDefs.HediffEnsnare);
            if (!t.CastAt(caster, TestContext.Recipe("snare", SpellDefs.FormProjectile, "WotF_Effect_Snare"), target))
                return;
            bool hit = t.StepUntil(() => target.health.hediffSet.HasHediff(snare), 600);
            t.Note("snareApplied", hit);
            t.Expect(hit, "the projectile did not apply snare");
            if (!hit) return;
            t.Note("downedOnHit", target.Downed);
            t.Expect(!target.Downed, "snare downed the target instead of rooting it");
            IntVec3 start = target.Position;
            target.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, t.Arena.Cell(15, 5)), JobTag.Misc);
            t.Step(60);
            t.Measure("rootedMove", start + " -> " + target.Position + ", job=" + target.CurJobDef?.defName);
            t.Expect(target.health.hediffSet.HasHediff(snare) && target.Position == start,
                "the ordered move advanced while snared");
            t.Expect(!target.Downed, "snared target became downed during the move order");
            Pawn victim = t.SpawnTarget(14, 8);
            target.jobs.EndCurrentJob(JobCondition.InterruptForced);
            float healthBefore = victim.health.summaryHealth.SummaryHealthPercent;
            t.Expect(t.CastAt(target, TestContext.Recipe("rooted-cast", SpellDefs.FormProjectile, SpellDefs.EffectHarm), victim),
                "snared pawn could not cast");
            bool castHit = t.StepUntil(() => victim.health.summaryHealth.SummaryHealthPercent < healthBefore, 180);
            t.Note("castHitWhileSnared", castHit && target.health.hediffSet.HasHediff(snare));
            t.Expect(castHit && target.health.hediffSet.HasHediff(snare), "snared pawn's spell did not hit before expiry");
            t.Expect(t.StepUntil(() => !target.health.hediffSet.HasHediff(snare), 1200), "snare did not expire");
            // Reissue the order because vanilla may replace a blocked Goto with a wait job.
            target.drafter.Drafted = true;
            target.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, t.Arena.Cell(15, 5)), JobTag.Misc);
            bool resumed = t.StepUntil(() => target.Position != start, 180);
            t.Note("movedAfterExpiry", resumed);
            t.Expect(resumed, "target could not move after snare expired");
        }
    }
}
