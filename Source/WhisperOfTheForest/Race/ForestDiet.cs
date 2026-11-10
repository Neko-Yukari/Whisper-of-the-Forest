using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// The bloodline eats raw plants without the vanilla raw-food mood penalty. Raw meat keeps it.
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.ThoughtsFromIngesting))]
    internal static class Patch_FoodUtility_ThoughtsFromIngesting
    {
        private static void Postfix(
            Pawn ingester,
            ThingDef foodDef,
            List<FoodUtility.ThoughtFromIngesting> __result)
        {
            if (Gene_ForestBloodline.Get(ingester) == null
                || foodDef.thingCategories == null
                || !foodDef.thingCategories.Contains(ThingCategoryDefOf.PlantFoodRaw))
            {
                return;
            }

            __result.RemoveAll(entry => entry.thought == ThoughtDefOf.AteRawFood);
        }
    }
}
