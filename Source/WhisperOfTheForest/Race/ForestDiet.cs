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

    /// <summary>
    /// An elf or bloodline pawn eating a living plant (foodType Plant or Tree) gets WotF_AtePlant. It is added where vanilla
    /// builds ingest thoughts, so both Thing.Ingested (Verse/Thing.cs:2020) and the food search's
    /// FoodOptimality (RimWorld/FoodUtility.cs:596) see it, and the search ranks plants below real food.
    /// Ascetics skip it like they skip a taste thought (FoodUtility.cs:885).
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.ThoughtsFromIngesting))]
    internal static class Patch_FoodUtility_ThoughtsFromIngesting_AtePlant
    {
        private static ThoughtDef atePlant;

        private static void Postfix(
            Pawn ingester,
            ThingDef foodDef,
            List<FoodUtility.ThoughtFromIngesting> __result)
        {
            if ((ingester.def.defName != "WotF_Elf" && Gene_ForestBloodline.Get(ingester) == null)
                || ingester.needs?.mood == null
                || (foodDef.ingestible.foodType & (FoodTypeFlags.Plant | FoodTypeFlags.Tree)) == 0
                || ingester.story.traits.HasTrait(TraitDefOf.Ascetic))
            {
                return;
            }

            atePlant ??= DefDatabase<ThoughtDef>.GetNamed("WotF_AtePlant");
            __result.Add(new FoodUtility.ThoughtFromIngesting { thought = atePlant });
        }
    }
}
