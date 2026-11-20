using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Race;

namespace WhisperOfTheForest.Tests
{
    internal static class ForestDietTests
    {
        internal static IEnumerable<(string Name, Action Run)> All
        {
            get
            {
                yield return ("forest-diet: grass, tall grass and flowers are edible", GrassAndFlowersEdible);
                yield return ("forest-diet: trees, harvestable crops, bushes and excluded thorns are rejected", NonGrassPlantsRejected);
            }
        }

        private static ForestPlantDietDef SampleDiet()
        {
            var diet = new ForestPlantDietDef
            {
                defName = "Test_ForestPlantDiet",
                maxPathCost = 14,
                maxFillPercent = 0.19f,
                excludedPlantDefNames = new List<string> { "Test_Brambles" }
            };
            diet.ResolveReferences();
            return diet;
        }

        private static ThingDef Plant(
            string defName,
            FoodTypeFlags foodType = FoodTypeFlags.Plant,
            int pathCost = 0,
            float fillPercent = 0f,
            float harvestYield = 0f,
            string harvestTag = null,
            bool forceIsTree = false)
        {
            return new ThingDef
            {
                defName = defName,
                pathCost = pathCost,
                fillPercent = fillPercent,
                ingestible = foodType == FoodTypeFlags.None ? null : new IngestibleProperties { foodType = foodType },
                plant = new PlantProperties
                {
                    harvestYield = harvestYield,
                    harvestTag = harvestTag,
                    forceIsTree = forceIsTree
                }
            };
        }

        private static void GrassAndFlowersEdible()
        {
            ForestPlantDietDef diet = SampleDiet();
            Check.True(ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Grass"), diet), "ordinary grass should be edible");
            Check.True(ForestDiet.IsEdibleGrassOrFlower(Plant("Test_TallGrass", pathCost: 14), diet), "tall grass at pathCost threshold should be edible");
            Check.True(ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Flower"), diet), "decorative flower should be edible");
        }

        private static void NonGrassPlantsRejected()
        {
            ForestPlantDietDef diet = SampleDiet();
            Check.True(!ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Oak", foodType: FoodTypeFlags.Tree, harvestTag: "Wood", harvestYield: 40f), diet),
                "tree with Tree foodType must be rejected");
            Check.True(!ForestDiet.IsEdibleGrassOrFlower(Plant("Test_ModTree", foodType: FoodTypeFlags.Plant, forceIsTree: true), diet),
                "modded tree marked Plant with forceIsTree must be rejected");
            Check.True(!ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Rice", harvestYield: 6f, pathCost: 14), diet),
                "harvestable crop must be rejected");
            Check.True(!ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Bush", pathCost: 30, fillPercent: 0.20f), diet),
                "bush exceeding pathCost/fillPercent must be rejected");
            Check.True(!ForestDiet.IsEdibleGrassOrFlower(Plant("Test_Brambles", pathCost: 14), diet),
                "explicitly excluded thorny shrub must be rejected");
        }
    }
}
