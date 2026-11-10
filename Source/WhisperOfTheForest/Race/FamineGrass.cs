using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Famine foraging: a bloodline pawn below the urgent-hunger line may eat a living wild plant
    /// outside a growing zone when the normal food search finds nothing. Eating one leaves a small
    /// mood penalty and a log entry.
    /// </summary>
    internal static class FamineGrass
    {
        private const float ForageRadius = 40f;

        internal static Plant FindWildPlant(Pawn pawn)
        {
            Map map = pawn.Map;
            if (map == null)
            {
                return null;
            }

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                map,
                ThingRequest.ForGroup(ThingRequestGroup.Plant),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                ForageRadius,
                thing => thing is Plant plant && IsWildForage(plant) && pawn.CanReserve(plant)) as Plant;
        }

        internal static bool IsWildForage(Plant plant)
        {
            return IsWildPlant(plant) && plant.IngestibleNow;
        }

        internal static bool IsWildPlant(Plant plant)
        {
            return !plant.sown
                && !(plant.Map?.zoneManager.ZoneAt(plant.Position) is Zone_Growing);
        }

        internal static void NotifyAteWildPlant(Pawn pawn)
        {
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WotF_AteWildGrass");
            if (thought != null && pawn.needs?.mood != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            }

            TaleDef tale = DefDatabase<TaleDef>.GetNamedSilentFail("WotF_Tale_AteWildGrass");
            if (tale != null)
            {
                TaleRecorder.RecordTale(tale, pawn);
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    internal static class Patch_JobGiver_GetFood_FamineGrass
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || pawn?.needs?.food == null)
            {
                return;
            }

            if (pawn.needs.food.CurLevelPercentage >= 0.3f
                || Gene_ForestBloodline.Get(pawn) == null)
            {
                return;
            }

            Plant plant = FamineGrass.FindWildPlant(pawn);
            if (plant != null)
            {
                __result = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WotF_FamineEat"), plant);
            }
        }
    }

    /// <summary>
    /// Vanilla's "eat" option on a living plant unforbids the target and then hands out an ingest job
    /// that carries the plant in the inventory. A plant has no CompForbiddable and cannot be carried,
    /// so the bloodline eats it in place instead; the option's text, disabled state and precondition
    /// checks all stay vanilla, and only a usable option's action is replaced.
    /// </summary>
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Ingest), "GetSingleOptionFor", typeof(Thing), typeof(FloatMenuContext))]
    internal static class Patch_FloatMenuOptionProvider_Ingest_FamineGrass
    {
        private static void Postfix(Thing clickedThing, FloatMenuContext context, ref FloatMenuOption __result)
        {
            if (__result?.action == null || !(clickedThing is Plant plant))
            {
                return;
            }

            Pawn pawn = context.FirstSelectedPawn;
            if (Gene_ForestBloodline.Get(pawn) == null)
            {
                return;
            }

            __result.action = delegate
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WotF_FamineEat"), plant);
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class Patch_Thing_Ingested_WildGrass
    {
        private static void Postfix(Thing __instance, Pawn ingester)
        {
            if (__instance is Plant plant
                && FamineGrass.IsWildPlant(plant)
                && Gene_ForestBloodline.Get(ingester) != null)
            {
                FamineGrass.NotifyAteWildPlant(ingester);
            }
        }
    }
}
