using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// The bloodline's workbench handoff: a powered bench slows the pawn's work, and a bench
    /// without a power comp raises the quality of the product. Both use the pawn's current bill.
    /// </summary>
    [LabTest]
    public sealed class Race_WorkbenchTraits : LabTest
    {
        public override string Name => "race.workbench-traits";

        protected override void Run(TestContext t)
        {
            WotF_ForestCraftGeneDef def =
                (WotF_ForestCraftGeneDef)DefDatabase<GeneDef>.GetNamed("WotF_ForestCraft");
            t.Expect(def.poweredWorkTableSpeedFactor > 0f && def.poweredWorkTableSpeedFactor < 1f,
                "the powered workbench speed factor is not a slowdown");
            t.Expect(def.handcraftQualityBonus > 0,
                "the handcraft quality bonus is not positive");

            Pawn elf = t.SpawnCaster(5, 5);
            elf.genes.AddGene(def, false);
            elf.skills.GetSkill(SkillDefOf.Crafting).Level = 10;

            Building_WorkTable powered = SpawnTable(t, "TableMachining", 8, 5);
            Building_WorkTable manual = SpawnTable(t, "TableSculpting", 12, 5);
            if (powered == null || manual == null)
            {
                return;
            }

            Bill poweredBill = AddBill(powered);
            Bill manualBill = AddBill(manual);
            Gene_ForestCraft gene = Gene_ForestCraft.Get(elf);
            if (gene == null)
            {
                t.Fail("the caster has no forest bloodline gene");
                return;
            }

            float withGene = Spend(elf, powered, poweredBill, 60);
            QualityCategory qualityWithGene = Quality(elf, manual, manualBill);

            elf.genes.RemoveGene(gene);
            float withoutGene = Spend(elf, powered, poweredBill, 60);
            QualityCategory qualityWithoutGene = Quality(elf, manual, manualBill);

            t.Measure("poweredSpendWithGene", withGene.ToString("0.##"));
            t.Measure("poweredSpendWithoutGene", withoutGene.ToString("0.##"));
            t.Expect(withoutGene > 0f
                && Mathf.Abs(withGene / withoutGene - def.poweredWorkTableSpeedFactor) < 0.05f,
                "the powered workbench speed factor is not applied");

            t.Measure("qualityWithGene", qualityWithGene.ToString());
            t.Measure("qualityWithoutGene", qualityWithoutGene.ToString());
            QualityCategory expected = (QualityCategory)Math.Min(
                (int)qualityWithoutGene + def.handcraftQualityBonus,
                (int)QualityCategory.Legendary);
            t.Expect(qualityWithGene == expected,
                "the handcraft quality bonus is not applied");
        }
        private static Building_WorkTable SpawnTable(TestContext t, string defName, int x, int z)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                t.Fail("missing workbench def " + defName);
                return null;
            }

            ThingDef stuff = def.MadeFromStuff ? ThingDefOf.Steel : null;
            var table = (Building_WorkTable)ThingMaker.MakeThing(def, stuff);
            GenSpawn.Spawn(table, t.Arena.Cell(x, z), t.Map, WipeMode.Vanish);
            return t.Arena.Track(table);
        }

        private static Bill AddBill(Building_WorkTable table)
        {
            var bill = new Bill_Production(DefDatabase<RecipeDef>.GetNamed("Make_StoneBlocksAny"));
            table.billStack.AddBill(bill);
            return bill;
        }

        private static float Spend(Pawn pawn, Building_WorkTable table, Bill bill, int delta)
        {
            Job job = JobMaker.MakeJob(JobDefOf.DoBill, new LocalTargetInfo(table));
            job.bill = bill;
            pawn.jobs.curJob = job;
            var driver = (JobDriver_DoBill)job.MakeDriver(pawn);
            pawn.jobs.curDriver = driver;
            driver.workLeft = 10000f;

            Toil toil = Toils_Recipe.DoRecipeWork();
            toil.actor = pawn;
            float before = driver.workLeft;
            toil.tickIntervalAction(delta);
            return before - driver.workLeft;
        }

        private static QualityCategory Quality(Pawn pawn, Building_WorkTable table, Bill bill)
        {
            Job job = JobMaker.MakeJob(JobDefOf.DoBill, new LocalTargetInfo(table));
            job.bill = bill;
            pawn.jobs.curJob = job;

            Rand.PushState(13579);
            try
            {
                return QualityUtility.GenerateQualityCreatedByPawn(pawn, SkillDefOf.Crafting, false);
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
