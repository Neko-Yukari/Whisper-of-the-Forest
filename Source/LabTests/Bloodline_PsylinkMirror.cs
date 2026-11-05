using System.Linq;
using RimWorld;
using Verse;
using WhisperOfTheForest.Race;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    // Bloodline module: level curves and the vanilla psylink mirror. Run alone with -Filter bloodline.

    internal static class BloodlineKit
    {
        internal static WotF_ForestBloodlineGeneDef Def =>
            (WotF_ForestBloodlineGeneDef)DefDatabase<GeneDef>.GetNamed("WotF_ForestBloodline");

        /// <summary>
        /// The base value of a stat for this pawn before any StatPart: the default times the stat's own
        /// statFactors (psychic sensitivity for neural heat). Test pawns are randomly generated, so a
        /// psychically dull colonist must not read as a failure.
        /// </summary>
        internal static float FactoredBase(Pawn pawn, StatDef stat)
        {
            float value = stat.defaultBaseValue;
            if (stat.statFactors != null)
            {
                for (int i = 0; i < stat.statFactors.Count; i++)
                {
                    value *= pawn.GetStatValue(stat.statFactors[i]);
                }
            }

            return value;
        }

        internal static Gene_ForestBloodline Gene(TestContext t, Pawn pawn)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(pawn);
            if (gene == null)
            {
                t.Fail(pawn.LabelShort + " has no forest bloodline gene");
            }

            return gene;
        }

        /// <summary>Experience that takes a fresh level-1 gene to <paramref name="level"/>.</summary>
        internal static float XpFromOneTo(int level)
        {
            float total = 0f;
            for (int l = 1; l < level; l++)
            {
                total += Def.xpToNextLevelCurve.Evaluate(l);
            }

            return total;
        }

        /// <summary>The vanilla neuroformer's own install effect, as the use job applies it.</summary>
        internal static void UseNeuroformer(Pawn pawn)
        {
            var item = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PsychicAmplifier"));
            item.GetComp<CompUseEffect_InstallImplant>().DoEffect(pawn);
        }

        internal static int Psycasts(Pawn pawn)
        {
            return pawn.abilities.abilities.Count(a => a.def.IsPsycast);
        }
    }

    /// <summary>A new bloodline pawn mirrors level 1; levelling by experience raises the mirror without psycasts.</summary>
    [LabTest]
    public sealed class Bloodline_MirrorFollowsLevel : LabTest
    {
        public override string Name => "bloodline.mirror-follows-level";

        protected override void Run(TestContext t)
        {
            Pawn elf = t.SpawnCaster(1, 5);
            Gene_ForestBloodline gene = BloodlineKit.Gene(t, elf);
            if (gene == null)
            {
                return;
            }

            t.Note("levelAtSpawn", gene.Level);
            t.Note("psylinkAtSpawn", elf.GetPsylinkLevel());
            t.Expect(gene.Level == 1, "a new bloodline pawn should be level 1, is " + gene.Level);
            t.Expect(elf.GetPsylinkLevel() == 1, "level 1 should mirror psylink 1, got " + elf.GetPsylinkLevel());

            gene.GainExperience(BloodlineKit.XpFromOneTo(3));
            t.Note("levelAfterXp", gene.Level);
            t.Note("psylinkAfterXp", elf.GetPsylinkLevel());
            t.Note("psycasts", BloodlineKit.Psycasts(elf));
            t.Expect(gene.Level == 3, "experience should reach level 3, got " + gene.Level);
            t.Expect(elf.GetPsylinkLevel() == 2, "level 3 should mirror psylink 2, got " + elf.GetPsylinkLevel());
            t.Expect(BloodlineKit.Psycasts(elf) == 0, "mirror levels must not grant psycasts, got " + BloodlineKit.Psycasts(elf));
        }
    }

    /// <summary>Each neuroformer raises the link by one and the bloodline jumps to the level that mirrors it.</summary>
    [LabTest]
    public sealed class Bloodline_NeuroformerRaisesLevel : LabTest
    {
        public override string Name => "bloodline.neuroformer-raises-level";

        protected override void Run(TestContext t)
        {
            Pawn elf = t.SpawnCaster(1, 5);
            Gene_ForestBloodline gene = BloodlineKit.Gene(t, elf);
            if (gene == null)
            {
                return;
            }

            BloodlineKit.UseNeuroformer(elf);
            t.Note("psylinkAfterFirst", elf.GetPsylinkLevel());
            t.Note("levelAfterFirst", gene.Level);
            t.Expect(elf.GetPsylinkLevel() == 2, "first neuroformer: psylink should be 2, got " + elf.GetPsylinkLevel());
            t.Expect(gene.Level == BloodlineKit.Def.psylinkUnlockLevels[1],
                "first neuroformer: level should be " + BloodlineKit.Def.psylinkUnlockLevels[1] + ", got " + gene.Level);

            BloodlineKit.UseNeuroformer(elf);
            t.Note("psylinkAfterSecond", elf.GetPsylinkLevel());
            t.Note("levelAfterSecond", gene.Level);
            t.Expect(elf.GetPsylinkLevel() == 3, "second neuroformer: psylink should be 3, got " + elf.GetPsylinkLevel());
            t.Expect(gene.Level == BloodlineKit.Def.psylinkUnlockLevels[2],
                "second neuroformer: level should be " + BloodlineKit.Def.psylinkUnlockLevels[2] + ", got " + gene.Level);

            t.Note("psycasts", BloodlineKit.Psycasts(elf));
            t.Expect(BloodlineKit.Psycasts(elf) == 0, "neuroformers on a bloodline pawn must not grant psycasts");
        }
    }

    /// <summary>A pawn that already has psylink 3 starts its bloodline at the level that mirrors 3.</summary>
    [LabTest]
    public sealed class Bloodline_GainWithPsylink : LabTest
    {
        public override string Name => "bloodline.gain-with-psylink";

        protected override void Run(TestContext t)
        {
            Pawn pawn = t.SpawnTarget(1, 5);
            for (int i = 0; i < 3; i++)
            {
                pawn.ChangePsylinkLevel(1, sendLetter: false);
            }

            t.Expect(pawn.GetPsylinkLevel() == 3, "setup: psylink should be 3, got " + pawn.GetPsylinkLevel());

            pawn.genes.AddGene(BloodlineKit.Def, false);
            Gene_ForestBloodline gene = BloodlineKit.Gene(t, pawn);
            if (gene == null)
            {
                return;
            }

            t.Note("level", gene.Level);
            t.Note("psylink", pawn.GetPsylinkLevel());
            t.Expect(gene.Level == BloodlineKit.Def.psylinkUnlockLevels[2],
                "level should start at " + BloodlineKit.Def.psylinkUnlockLevels[2] + ", got " + gene.Level);
            t.Expect(pawn.GetPsylinkLevel() == 3, "gaining the gene must keep psylink 3, got " + pawn.GetPsylinkLevel());
        }
    }

    /// <summary>
    /// A bloodline pawn's mana cap and neural heat come from the level curves, without the psylink
    /// stage factors.
    /// </summary>
    [LabTest]
    public sealed class Bloodline_StatsFromCurves : LabTest
    {
        public override string Name => "bloodline.stats-from-curves";

        protected override void Run(TestContext t)
        {
            Pawn elf = t.SpawnCaster(1, 5);
            Gene_ForestBloodline gene = BloodlineKit.Gene(t, elf);
            if (gene == null)
            {
                return;
            }

            gene.GainExperience(BloodlineKit.XpFromOneTo(3));
            t.Expect(elf.GetPsylinkLevel() == 2, "setup: level 3 should mirror psylink 2, got " + elf.GetPsylinkLevel());

            float expectedMax = BloodlineKit.FactoredBase(elf, StatDefOf.PsychicEntropyMax)
                                + BloodlineKit.Def.entropyMaxCurve.Evaluate(gene.Level);
            float expectedRecovery = BloodlineKit.FactoredBase(elf, StatDefOf.PsychicEntropyRecoveryRate)
                                     * BloodlineKit.Def.entropyRecoveryFactorCurve.Evaluate(gene.Level);
            float max = elf.GetStatValue(StatDefOf.PsychicEntropyMax);
            float recovery = elf.GetStatValue(StatDefOf.PsychicEntropyRecoveryRate);

            // The test caster wears the basic spellbook, whose offset adds to the level's cap.
            float bookMana = SpellService.WornSpellbook(elf).def.equippedStatOffsets
                .GetStatOffsetFromList(BloodlineKit.Def.manaMaxStat);
            float expectedMana = BloodlineKit.Def.manaMaxCurve.Evaluate(gene.Level) + bookMana;
            float manaStat = elf.GetStatValue(BloodlineKit.Def.manaMaxStat);
            gene.RefreshMaxFromStats();

            t.Note("level", gene.Level);
            t.Note("manaMaxStat", manaStat);
            t.Note("manaBarMax", gene.MaxMana);
            t.Note("manaMaxExpected", expectedMana);
            t.Expect(System.Math.Abs(manaStat - expectedMana) < 0.01f, "mana cap stat " + manaStat + ", expected " + expectedMana);
            t.Expect(System.Math.Abs(gene.MaxMana - expectedMana) < 0.01f, "mana bar max " + gene.MaxMana + ", expected " + expectedMana);
            t.Note("entropyMax", max);
            t.Note("entropyMaxExpected", expectedMax);
            t.Note("entropyRecovery", recovery);
            t.Note("entropyRecoveryExpected", expectedRecovery);
            t.Expect(System.Math.Abs(max - expectedMax) < 0.01f, "PsychicEntropyMax " + max + ", expected " + expectedMax);
            t.Expect(System.Math.Abs(recovery - expectedRecovery) < 0.001f,
                "PsychicEntropyRecoveryRate " + recovery + ", expected " + expectedRecovery);
        }
    }

    /// <summary>A pawn without the gene keeps vanilla psylinks: psycasts on use and the stage factor on heat.</summary>
    [LabTest]
    public sealed class Bloodline_HumanStaysVanilla : LabTest
    {
        public override string Name => "bloodline.human-stays-vanilla";

        protected override void Run(TestContext t)
        {
            Pawn human = t.SpawnTarget(1, 5);
            BloodlineKit.UseNeuroformer(human);
            BloodlineKit.UseNeuroformer(human);
            t.Step(300);

            float factor = HediffDefOf.PsychicAmplifier.stages[1].statFactors.GetStatFactorFromList(StatDefOf.PsychicEntropyMax);
            float expectedMax = BloodlineKit.FactoredBase(human, StatDefOf.PsychicEntropyMax) * factor;
            t.Note("psychicSensitivity", human.GetStatValue(StatDefOf.PsychicSensitivity));
            float max = human.GetStatValue(StatDefOf.PsychicEntropyMax);

            t.Note("psylink", human.GetPsylinkLevel());
            t.Note("psycasts", BloodlineKit.Psycasts(human));
            t.Note("entropyMax", max);
            t.Note("entropyMaxExpected", expectedMax);
            t.Expect(Gene_ForestBloodline.Get(human) == null, "setup: the human must not carry the gene");
            t.Expect(human.GetPsylinkLevel() == 2, "two neuroformers should give psylink 2, got " + human.GetPsylinkLevel());
            t.Expect(BloodlineKit.Psycasts(human) >= 2, "vanilla grants one psycast per level, got " + BloodlineKit.Psycasts(human));
            t.Expect(System.Math.Abs(max - expectedMax) < 0.01f, "PsychicEntropyMax " + max + ", expected " + expectedMax);
        }
    }
}
