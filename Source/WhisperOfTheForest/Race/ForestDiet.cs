using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Explicit exclusions defining which non-tree, non-harvestable living plants are NOT edible
    /// grasses or flowers for elves and forest-bloodline foraging (all entries live in ElfDiet.xml).
    /// </summary>
    public sealed class ForestPlantDietDef : Def
    {
        public List<string> excludedPlantDefNames;

        private HashSet<string> excludedSet;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            excludedSet = excludedPlantDefNames == null
                ? new HashSet<string>()
                : new HashSet<string>(excludedPlantDefNames);
        }

        public bool IsExcluded(string plantDefName)
        {
            if (plantDefName.NullOrEmpty())
            {
                return false;
            }

            if (excludedSet != null)
            {
                return excludedSet.Contains(plantDefName);
            }

            return excludedPlantDefNames != null && excludedPlantDefNames.Contains(plantDefName);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (excludedPlantDefNames == null)
            {
                yield return defName + " excludedPlantDefNames must be defined.";
            }
        }
    }

    public static class ForestDiet
    {
        private static ForestPlantDietDef cachedDiet;

        public static ForestPlantDietDef Diet =>
            cachedDiet ??= DefDatabase<ForestPlantDietDef>.GetNamed("WotF_ForestPlantDiet");

        public static bool UsesGrassFlowerDiet(Pawn pawn)
        {
            return pawn != null && (pawn.def.defName == "WotF_Elf" || Gene_ForestBloodline.Get(pawn) != null);
        }

        public static bool IsEdibleGrassOrFlower(ThingDef food)
        {
            return IsEdibleGrassOrFlower(food, Diet);
        }

        /// <summary>
        /// Narrows living plant edibility to grasses and flowers: excludes trees (<see cref="PlantProperties.IsTree"/>),
        /// harvestable crops/berries/mushrooms (<see cref="PlantProperties.Harvestable"/>), and non-flower bushes,
        /// thorns, cacti and giant fungi listed in <see cref="ForestPlantDietDef.excludedPlantDefNames"/>.
        /// Does not filter by pathCost/fillPercent so bush-based or modded flowers (e.g. Plant_Clivia) stay edible.
        /// </summary>
        public static bool IsEdibleGrassOrFlower(ThingDef food, ForestPlantDietDef diet)
        {
            if (food?.plant == null || food.ingestible == null || diet == null)
            {
                return false;
            }

            if ((food.ingestible.foodType & FoodTypeFlags.Plant) == 0)
            {
                return false;
            }

            if (food.plant.IsTree || food.plant.Harvestable)
            {
                return false;
            }

            return !diet.IsExcluded(food.defName);
        }
    }

    /// <summary>
    /// Restricts elf/bloodline living-plant suitability to grasses and flowers everywhere vanilla checks
    /// FoodUtility.WillEat / FoodIsSuitable (auto food search, food policies, binge, float menu).
    /// </summary>
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.FoodIsSuitable))]
    internal static class Patch_FoodUtility_FoodIsSuitable_GrassFlowerOnly
    {
        private static void Postfix(Pawn p, ThingDef food, ref bool __result)
        {
            if (!__result || food?.plant == null || !ForestDiet.UsesGrassFlowerDiet(p))
            {
                return;
            }

            if (!ForestDiet.IsEdibleGrassOrFlower(food))
            {
                __result = false;
            }
        }
    }

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
    /// An elf or bloodline pawn eating a living plant (foodType Plant) gets WotF_AtePlant. It is added where vanilla
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
            if (!ForestDiet.UsesGrassFlowerDiet(ingester)
                || ingester.needs?.mood == null
                || (foodDef.ingestible.foodType & FoodTypeFlags.Plant) == 0
                || ingester.story.traits.HasTrait(TraitDefOf.Ascetic))
            {
                return;
            }

            atePlant ??= DefDatabase<ThoughtDef>.GetNamed("WotF_AtePlant");
            __result.Add(new FoodUtility.ThoughtFromIngesting { thought = atePlant });
        }
    }
}
