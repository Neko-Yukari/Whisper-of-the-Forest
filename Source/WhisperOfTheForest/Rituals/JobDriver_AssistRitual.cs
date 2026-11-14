using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// A helper's half of a performance: stand on the assigned chalk cell until the rite ends. The
    /// helper reserves nothing - the host's job owns the altar and the offerings - and never touches
    /// mana. The job ends by itself when the altar stops running the rite, so a cancelled rite leaves
    /// no pawn standing on the chalk waiting for a chant that will not come.
    /// </summary>
    public class JobDriver_AssistRitual : JobDriver
    {
        private const TargetIndex AltarInd = TargetIndex.A;
        private const TargetIndex StationInd = TargetIndex.B;

        private CompRitualAltar Altar => job.targetA.Thing?.TryGetComp<CompRitualAltar>();

        // No reservation on the altar: the host already holds it with maxPawns = 1, so a second claim
        // fails CanReserve, and because TryTakeOrderedJob marks the job playerForced
        // (Pawn_JobTracker.cs:897) ReservationManager.Reserve then takes the reservation over by
        // ending the current holder's job (ReservationManager.cs:327-338) - the host's, which cancels
        // the very rite this helper is joining.
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompRitualAltar altar = Altar;
            if (altar == null || !altar.IsHelping(pawn))
            {
                yield break;
            }

            this.FailOnDespawnedOrNull(AltarInd);
            this.FailOn(delegate { return altar.IsActive && !altar.IsHelping(pawn); });

            yield return Toils_Goto.GotoCell(StationInd, PathEndMode.OnCell);
            yield return Stand(altar);
        }

        private Toil Stand(CompRitualAltar altar)
        {
            Toil toil = ToilMaker.MakeToil("RitualAssistStand");
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
                if (!altar.IsActive)
                {
                    ReadyForNextToil();
                }
            };
            return toil;
        }
    }
}
