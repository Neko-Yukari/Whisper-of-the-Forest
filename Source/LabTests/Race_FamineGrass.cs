using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Famine foraging handoff: a hungry bloodline pawn asks the food giver for a wild plant only
    /// outside a growing zone, and eating that plant leaves the mood penalty and the log entry.
    /// </summary>
    [LabTest]
    public sealed class Race_FamineGrass : LabTest
    {
        public override string Name => "race.famine-grass";

        protected override void Run(TestContext t)
        {
            ThingDef elfDef = DefDatabase<ThingDef>.GetNamedSilentFail("WotF_Elf");
            GeneDef bloodline = DefDatabase<GeneDef>.GetNamedSilentFail("WotF_ForestBloodline");
            ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WotF_AtePlant");
            TaleDef tale = DefDatabase<TaleDef>.GetNamedSilentFail("WotF_Tale_AteWildGrass");
            if (elfDef == null || bloodline == null || thought == null || tale == null)
            {
                t.Fail("famine-grass defs are not loaded");
                return;
            }

            t.Expect((elfDef.race.foodType & FoodTypeFlags.Plant) != 0, "the elf cannot eat living plants");
            t.Expect((elfDef.race.foodType & FoodTypeFlags.Tree) != 0, "the elf cannot eat trees");
            t.Expect(bloodline.dontMindRawFood, "the bloodline gene does not remove the raw-food penalty");

            Pawn elf = t.SpawnCaster(4, 4);
            elf.drafter.Drafted = false;
            elf.needs.food.CurLevel = 0.2f;

            t.SetTerrain(8, 4, TerrainDefOf.Soil);
            Plant wild = t.SpawnPlant(8, 4, 1f);
            Job forage = GiveFoodJob(elf);
            t.Expect(forage != null && forage.def.defName == "WotF_FamineEat" && forage.targetA.Thing == wild,
                "the hungry elf did not ask to eat the wild plant");
            t.Measure("forageTarget", forage?.targetA.Thing?.def.defName ?? "none");
            if (forage == null)
            {
                return;
            }

            wild.sown = true;
            Job sownJob = GiveFoodJob(elf);
            t.Expect(sownJob == null || sownJob.targetA.Thing != wild, "the elf tried to eat a player-grown plant");
            wild.sown = false;

            Pawn human = t.SpawnTarget(12, 4);
            human.drafter.Drafted = false;
            human.needs.food.CurLevel = 0.2f;
            Job humanJob = GiveFoodJob(human);
            t.Expect(humanJob == null || humanJob.targetA.Thing != wild, "a pawn without the bloodline foraged wild grass");

            elf.needs.food.CurLevel = 0.8f;
            Job fedJob = GiveFoodJob(elf);
            t.Expect(fedJob == null || fedJob.targetA.Thing != wild, "a fed elf foraged wild grass");

            elf.needs.food.CurLevel = 0.2f;
            elf.jobs.TryTakeOrderedJob(forage);
            t.Expect(t.StepUntil(() => wild.Destroyed, 600), "the elf did not finish eating the wild plant");
            t.Expect(elf.needs.mood.thoughts.memories.GetFirstMemoryOfDef(thought) != null,
                "eating wild grass left no mood penalty");
            t.Expect(Find.TaleManager.AllTalesListForReading.Any(recorded => recorded.def == tale),
                "eating wild grass left no log entry");
            t.Measure("thoughtMood", thought.stages[0].baseMoodEffect.ToString());
        }

        private static Job GiveFoodJob(Pawn pawn)
        {
            MethodInfo method = typeof(JobGiver_GetFood).GetMethod(
                "TryGiveJob", BindingFlags.Instance | BindingFlags.NonPublic);
            return (Job)method.Invoke(new JobGiver_GetFood(), new object[] { pawn });
        }
    }
}
