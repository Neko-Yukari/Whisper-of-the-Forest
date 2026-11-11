using RimWorld;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// A psylink level written straight into the hediff - a character editor or another mod - never
    /// passes through ChangeLevel, so the psylink patches never fire. The periodic reconcile must
    /// still raise the bloodline level and with it the neural heat limit.
    /// </summary>
    [LabTest]
    public sealed class Bloodline_ExternalPsylinkWrite : LabTest
    {
        public override string Name => "bloodline.external-psylink-write";

        protected override void Run(TestContext t)
        {
            Pawn elf = t.SpawnCaster(1, 5);
            Gene_ForestBloodline gene = BloodlineKit.Gene(t, elf);
            if (gene == null)
            {
                return;
            }

            float entropyBefore = elf.GetStatValue(StatDefOf.PsychicEntropyMax);
            t.Expect(gene.Level == 1, "a new bloodline pawn should be level 1, is " + gene.Level);

            Hediff_Psylink psylink = elf.GetMainPsylinkSource();
            psylink.level = 4;
            psylink.Severity = 4;
            t.Expect(gene.Level == 1, "a direct hediff write must not move the level by itself");

            t.Step(300);
            t.Measure("levelAfterReconcile", gene.Level.ToString());
            t.Measure("psylinkAfterReconcile", elf.GetPsylinkLevel().ToString());
            t.Measure("entropyBefore", entropyBefore.ToString("0.#"));
            t.Measure("entropyAfter", elf.GetStatValue(StatDefOf.PsychicEntropyMax).ToString("0.#"));
            t.Expect(gene.Level == 7, "psylink 4 should reconcile to bloodline level 7, got " + gene.Level);
            t.Expect(elf.GetPsylinkLevel() == 4, "reconcile must not lower the psylink, got " + elf.GetPsylinkLevel());
            t.Expect(elf.GetStatValue(StatDefOf.PsychicEntropyMax) > entropyBefore + 1f,
                "neural heat limit did not rise with the reconciled level");
        }
    }
}