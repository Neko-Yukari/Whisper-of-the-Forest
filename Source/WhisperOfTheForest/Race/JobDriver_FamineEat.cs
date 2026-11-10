using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Eats a living wild plant in place. Vanilla's ingest driver picks the plant up first, and a
    /// carried plant is not a valid holder for the game's long-tick path.
    /// </summary>
    public class JobDriver_FamineEat : JobDriver
    {
        private Thing Food => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Food == null || Food.Destroyed || !Food.IngestibleNow);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Ingest.ChewIngestible(pawn, 1f, TargetIndex.A);
            yield return Toils_Ingest.FinalizeIngest(pawn, TargetIndex.A);
        }
    }
}
