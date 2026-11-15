using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Haulers fill an altar that is collecting offerings the way vanilla fills a subcore scanner
    /// (WorkGiver_HaulToSubcoreScanner): take the nearest stack the altar still lacks, carry at most the
    /// missing count, and let the vanilla HaulToContainer job put it into the altar's container.
    /// One hauler at a time, because that job reserves the container (JobDriver_HaulToContainer).
    /// </summary>
    public class WorkGiver_HaulRitualOfferings : WorkGiver_Scanner
    {
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Building> buildings = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count; i++)
            {
                CompRitualAltar altar = buildings[i].TryGetComp<CompRitualAltar>();
                if (altar != null && altar.NeedsOfferings)
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
            if (altar == null || !altar.NeedsOfferings || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            return FindOffering(pawn, altar).Thing != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompRitualAltar altar = t.TryGetComp<CompRitualAltar>();
            if (altar == null || !altar.NeedsOfferings)
            {
                return null;
            }

            ThingCount offering = FindOffering(pawn, altar);
            if (offering.Thing == null)
            {
                return null;
            }

            Job job = HaulAIUtility.HaulToContainerJob(pawn, offering.Thing, t);
            job.count = Mathf.Min(job.count, offering.Count);
            return job;
        }

        private static ThingCount FindOffering(Pawn pawn, CompRitualAltar altar)
        {
            Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), 9999f, Validator);
            return thing == null
                ? default(ThingCount)
                : new ThingCount(thing, Mathf.Min(thing.stackCount, altar.RequiredCountOf(thing)));

            bool Validator(Thing x)
            {
                return !x.IsForbidden(pawn) && pawn.CanReserve(x) && altar.RequiredCountOf(x) > 0;
            }
        }
    }
}
