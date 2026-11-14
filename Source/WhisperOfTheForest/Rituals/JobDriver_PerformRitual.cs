using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The host's half of a performance: walk onto the assigned chalk cell, wait there until every
    /// participant is seated, chant for the Def's duration, then settle the cost and apply the effect.
    ///
    /// Waiting for the whole group follows vanilla: the gather toil completes only when every
    /// controlled pawn has arrived (PsychicRitualToil_Goto.Tick returns finalGoto.AllPawnsDone,
    /// PsychicRitualToil_Goto.cs:167) and each early arrival is parked on
    /// DutyDefOf.WaitForRitualParticipants (:201), so nobody chants while walking.
    ///
    /// It owns no numbers: the altar holds the cost, the duration and the reserved offerings. Ending
    /// this job before the last toil is what "interrupted" means, and it costs nothing.
    /// </summary>
    public class JobDriver_PerformRitual : JobDriver
    {
        private const TargetIndex AltarInd = TargetIndex.A;
        private const TargetIndex StationInd = TargetIndex.B;

        private CompRitualAltar Altar => job.targetA.Thing?.TryGetComp<CompRitualAltar>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            CompRitualAltar altar = Altar;
            if (altar == null)
            {
                return false;
            }

            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
                && altar.ReserveOfferings(pawn, job, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompRitualAltar altar = Altar;
            if (altar == null || !altar.IsHost(pawn))
            {
                yield break;
            }

            this.FailOnDespawnedOrNull(AltarInd);
            this.FailOn(delegate { return !altar.IsHost(pawn); });
            this.AddFinishAction(delegate { altar.NotifyHostJobEnded(pawn); });

            yield return Toils_Goto.GotoCell(StationInd, PathEndMode.OnCell);
            yield return Gather(altar);
            yield return Toils_General.Wait(altar.ActiveDurationTicks, AltarInd)
                .WithProgressBarToilDelay(AltarInd)
                .FailOnDespawnedOrNull(AltarInd);
            yield return Toils_General.Do(delegate { altar.CompleteRitual(pawn); });
        }

        /// <summary>Stand on the cell until the altar says every participant is in place.</summary>
        private Toil Gather(CompRitualAltar altar)
        {
            Toil toil = ToilMaker.MakeToil("RitualGather");
            toil.initAction = delegate
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(job.targetA);
            };
            toil.handlingFacing = true;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (altar.TryBeginChanting())
                {
                    ReadyForNextToil();
                }
            };
            return toil;
        }
    }
}
