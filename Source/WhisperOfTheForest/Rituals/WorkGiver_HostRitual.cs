using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Hosting as work. An altar whose auto-repeat rite has all its offerings inside is a work target:
    /// a pawn takes it up by its own work priorities and schedule and becomes the host, and nobody is
    /// pulled off their work. A rite prepared by hand is never taken up on its own; a right-click
    /// (forced) on the altar hosts it, through vanilla's prioritize menu (FloatMenuOptionProvider_WorkGivers),
    /// which shows the altar's first problem as the reason when this pawn cannot host it.
    /// The job only carries the altar; JobDriver_PerformRitual commits the rite when it starts.
    /// </summary>
    public class WorkGiver_HostRitual : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Building> buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                CompRitualAltar altar = buildings[i].TryGetComp<CompRitualAltar>();
                if (altar != null && altar.CollectDef != null && !altar.IsActive)
                {
                    yield return buildings[i];
                }
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            foreach (Thing _ in PotentialWorkThingsGlobal(pawn))
            {
                return false;
            }

            return true;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompRitualAltar altar = t.TryGetComp<CompRitualAltar>();
            if (altar == null || altar.CollectDef == null || altar.IsActive)
            {
                return false;
            }

            // Cheap checks first: this runs for every pawn's work scan.
            if (!forced && (altar.AutoRitual == null || !altar.IsPreparedFor(altar.AutoRitual, null) || altar.NeedsOfferings))
            {
                return false;
            }

            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            RitualAssessment assessment = altar.Assess(altar.WorkRequest(pawn));
            if (!assessment.CanStart)
            {
                JobFailReason.Is(assessment.FirstProblem);
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WotF_PerformRitual"), t);
        }

        public override string PostProcessedGerund(Job job)
        {
            CompRitualAltar altar = job.targetA.Thing.TryGetComp<CompRitualAltar>();
            return "WotF_Ritual_HostGerund".Translate(altar.CollectLabel);
        }
    }
}
