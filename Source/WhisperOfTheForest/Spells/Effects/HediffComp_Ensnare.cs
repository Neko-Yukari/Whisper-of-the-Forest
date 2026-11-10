using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Spells
{
    public sealed class HediffComp_Ensnare : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo) => InterruptWalking();

        public override void CompPostMerged(Hediff other) => InterruptWalking();

        private void InterruptWalking()
        {
            Job job = Pawn.CurJob;
            if (job == null)
                return;
            Type driver = job.def.driverClass;
            // Exact vanilla drivers: do not include subclasses that add work, attacks or flight.
            if (driver != typeof(JobDriver_Goto) && driver != typeof(JobDriver_GotoMindControlled)
                && driver != typeof(JobDriver_GotoPatrolDest) && driver != typeof(JobDriver_Flee)
                && driver != typeof(JobDriver_Follow) && driver != typeof(JobDriver_FollowClose)
                && driver != typeof(JobDriver_FollowRoper))
                return;

            if (Pawn.mindState.forcedGotoPosition == job.targetA.Cell)
                Pawn.mindState.forcedGotoPosition = IntVec3.Invalid;
            Pawn.pather.StopDead();
            Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }
    }
}
