using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The bloodline's raw-food rule: raw plant food leaves no AteRawFood thought, while raw
    /// meat still does and a pawn without the bloodline keeps the penalty for both.
    /// </summary>
    [LabTest]
    public sealed class Race_Vegetarian : LabTest
    {
        public override string Name => "race.vegetarian";

        protected override void Run(TestContext t)
        {
            ThingDef plantDef = DefDatabase<ThingDef>.GetNamedSilentFail("RawPotatoes");
            if (plantDef == null)
            {
                t.Fail("RawPotatoes is not loaded");
                return;
            }

            Thing plant = ThingMaker.MakeThing(plantDef);
            Thing meat = ThingMaker.MakeThing(ThingDefOf.Meat_Human);
            Pawn elf = t.SpawnCaster(4, 4);
            Pawn human = t.SpawnTarget(8, 4);
            RemoveAscetic(elf);
            RemoveAscetic(human);

            bool humanPlant = HasRawFoodThought(human, plant);
            bool elfPlant = HasRawFoodThought(elf, plant);
            bool elfMeat = HasRawFoodThought(elf, meat);

            t.Measure("humanPlant", humanPlant.ToString());
            t.Measure("elfPlant", elfPlant.ToString());
            t.Measure("elfMeat", elfMeat.ToString());

            t.Expect(humanPlant, "a pawn without the bloodline lost the raw plant penalty");
            t.Expect(!elfPlant, "the bloodline still gets the raw plant penalty");
            t.Expect(elfMeat, "the bloodline lost the raw meat penalty");
        }

        private static void RemoveAscetic(Pawn pawn)
        {
            foreach (Trait trait in pawn.story.traits.allTraits.ToList())
            {
                if (trait.def == TraitDefOf.Ascetic)
                {
                    pawn.story.traits.RemoveTrait(trait);
                }
            }
        }

        private static bool HasRawFoodThought(Pawn pawn, Thing food)
        {
            return FoodUtility.ThoughtsFromIngesting(pawn, food, food.def)
                .Any(entry => entry.thought == ThoughtDefOf.AteRawFood);
        }
    }
}
