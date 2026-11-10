using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Walks to the altar and performs the active ritual for the Def's duration. The altar owns the
    /// active ritual and its paid cost; this driver only stops cleanly on any interruption.
    /// </summary>
    public class JobDriver_PerformRitual : JobDriver
    {
        private const TargetIndex AltarInd = TargetIndex.A;

        private CompRitualAltar Altar => job.targetA.Thing?.TryGetComp<CompRitualAltar>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            CompRitualAltar altar = Altar;
            if (altar == null || altar.ActiveDef == null)
            {
                yield break;
            }

            this.FailOnDespawnedOrNull(AltarInd);
            this.AddFinishAction(delegate { altar.CancelRitual(pawn); });

            yield return Toils_Goto.GotoThing(AltarInd, PathEndMode.InteractionCell)
                .FailOnDespawnedOrNull(AltarInd);

            yield return Toils_General.Wait(altar.ActiveDef.durationTicks, AltarInd)
                .WithProgressBarToilDelay(AltarInd)
                .FailOnDespawnedOrNull(AltarInd);

            yield return Toils_General.Do(delegate { altar.CompleteRitual(pawn); });
        }
    }
}
