using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Famine foraging: a bloodline pawn below the urgent-hunger line may eat a living wild plant
    /// outside a growing zone when the normal food search finds nothing. Eating one leaves a log entry.
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
            return IsWildPlant(plant) && plant.IngestibleNow && ForestDiet.IsEdibleGrassOrFlower(plant.def);
        }

        internal static bool IsWildPlant(Plant plant)
        {
            return !plant.sown
                && !(plant.Map?.zoneManager.ZoneAt(plant.Position) is Zone_Growing);
        }

        /// <summary>
        /// A race whose foodType includes Plant/Tree lets vanilla's own food search pick a living plant
        /// (FoodUtility.cs:439), and a tool user's ingest job then picks the food up
        /// (JobDriver_Ingest.cs:140-154). A carried plant still runs Plant.TickLong, which needs a map and
        /// throws. Such a job is swapped for eating the plant where it stands.
        /// </summary>
        internal static Job EatPlantsInPlace(Pawn pawn, Job job)
        {
            if (job != null
                && job.def == JobDefOf.Ingest
                && job.targetA.Thing is Plant plant
                && plant.Spawned
                && pawn.RaceProps.ToolUser)
            {
                return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WotF_FamineEat"), plant);
            }

            return job;
        }

        internal static void NotifyAteWildPlant(Pawn pawn)
        {
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
            if (__result != null)
            {
                __result = FamineGrass.EatPlantsInPlace(pawn, __result);
                return;
            }

            if (pawn?.needs?.food == null)
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

    /// <summary>The food binge mental state searches food the same way and hands out the same ingest job.</summary>
    [HarmonyPatch(typeof(JobGiver_Binge), "TryGiveJob")]
    internal static class Patch_JobGiver_Binge_EatPlantsInPlace
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            __result = FamineGrass.EatPlantsInPlace(pawn, __result);
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
            if (__result == null || !(clickedThing is Plant plant))
            {
                return;
            }

            Pawn pawn = context.FirstSelectedPawn;
            if (!ForestDiet.UsesGrassFlowerDiet(pawn))
            {
                return;
            }

            if (!ForestDiet.IsEdibleGrassOrFlower(plant.def))
            {
                __result = null;
                return;
            }

            if (__result.action == null)
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
