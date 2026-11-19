using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Hosting as work, for the auto-repeat rite only: an idle altar whose auto rite would pass Assess
    /// (its offerings all inside) is a work target, and a pawn takes it up by its own work priorities
    /// and schedule. The Def is not directOrderable: a right-click offers the altar's selected rite
    /// through FloatMenuOptionProvider_HostRitual instead. The job only carries the altar;
    /// JobDriver_PerformRitual commits the rite when it starts.
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
                if (altar != null && altar.AutoRitual != null && altar.AutoRitual.IsResearchFinished && !altar.IsActive)
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
            if (pawn.Drafted)
            {
                return false;
            }

            CompRitualAltar altar = t.TryGetComp<CompRitualAltar>();
            if (altar == null || altar.AutoRitual == null || !altar.AutoRitual.IsResearchFinished || altar.IsActive)
            {
                return false;
            }

            // Cheap checks first: this runs for every pawn's work scan.
            if (!altar.HasOfferingsFor(altar.AutoRitual, null) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return altar.Assess(altar.DirectRequest(altar.AutoRitual, pawn)).CanStart;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WotF_PerformRitual"), t);
        }
    }
}
