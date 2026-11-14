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

            int levelBefore = gene.Level;
            float entropyBefore = elf.GetStatValue(StatDefOf.PsychicEntropyMax);
            int written = (int)HediffDefOf.PsychicAmplifier.maxSeverity;

            Hediff_Psylink psylink = elf.GetMainPsylinkSource();
            psylink.level = written;
            psylink.Severity = written;

            t.Step(300);
            t.Measure("levelBefore", levelBefore.ToString());
            t.Measure("levelAfter", gene.Level.ToString());
            t.Measure("psylinkAfter", elf.GetPsylinkLevel().ToString());
            t.Measure("entropyBefore", entropyBefore.ToString("0.#"));
            t.Measure("entropyAfter", elf.GetStatValue(StatDefOf.PsychicEntropyMax).ToString("0.#"));
            t.Expect(gene.Level > levelBefore, "a psylink written straight into the hediff did not raise the bloodline level");
            t.Expect(elf.GetPsylinkLevel() == written, "reconcile changed the psylink level");
            t.Expect(elf.GetStatValue(StatDefOf.PsychicEntropyMax) > entropyBefore,
                "neural heat limit did not rise with the reconciled level");
        }
    }
}