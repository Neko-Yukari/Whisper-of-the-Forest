using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Right-click eat handoff: the vanilla "eat" float menu option on a living plant must start
    /// the in-place famine eating job, not vanilla's ingest job that unforbids and carries the plant.
    /// </summary>
    [LabTest]
    public sealed class Race_FamineGrassOrder : LabTest
    {
        public override string Name => "race.famine-grass-order";

        protected override void Run(TestContext t)
        {
            // The vanilla option only exists for a race whose foodType contains Plant, so a human
            // caster never sees it; the reported defect is the elf race clicking that option.
            Pawn elf = t.SpawnElf(4, 4);
            elf.needs.food.CurLevel = 0.2f;

            t.SetTerrain(6, 4, TerrainDefOf.Soil);
            Plant wild = t.SpawnPlant(6, 4, 1f);

            string eat = "ConsumeThing".Translate(wild.LabelShort, wild);
            FloatMenuOption option = t.RightClick(elf, wild).FirstOrDefault(o => o.Label.StartsWith(eat));
            t.Expect(option?.action != null, "the elf has no usable eat option on the plant");
            if (option?.action == null)
            {
                return;
            }

            option.action();
            t.Step(1);
            t.Measure("orderedJob", elf.CurJobDef?.defName ?? "none");
            t.Expect(elf.CurJobDef?.defName == "WotF_FamineEat", "the eat order did not start the in-place eating job");
            t.Expect(t.StepUntil(() => wild.Destroyed, 600), "the elf did not finish eating the ordered plant");
        }
    }
}
