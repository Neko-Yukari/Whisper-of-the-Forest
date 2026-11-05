using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    // StatRequest.Pawn is the "for pawn" of ability requests, not the thing being evaluated; the
    // bloodline pawn is req.Thing.

    /// <summary>Adds the level's mana cap to <c>WotF_ManaMax</c>, which the gene then reads.</summary>
    public class StatPart_ForestBloodlineMana : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene != null)
            {
                val += gene.LevelManaMax;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene == null)
            {
                return null;
            }

            return "WotF_Race.ManaFromLevel".Translate(gene.Level).Resolve() + ": " + gene.LevelManaMax.ToString("0.#");
        }
    }

    /// <summary>Adds the level's neural-heat capacity to the vanilla <c>PsychicEntropyMax</c>.</summary>
    public class StatPart_ForestBloodlineEntropy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene != null)
            {
                val += gene.LevelEntropyMaxBonus;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene == null)
            {
                return null;
            }

            return "WotF_Race.EntropyFromLevel".Translate(gene.Level).Resolve() + ": " + gene.LevelEntropyMaxBonus.ToString("0.#");
        }
    }

    /// <summary>Scales the vanilla <c>PsychicEntropyRecoveryRate</c> by the level's factor.</summary>
    public class StatPart_ForestBloodlineEntropyRecovery : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene != null)
            {
                val *= gene.LevelEntropyRecoveryFactor;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Thing as Pawn);
            if (gene == null)
            {
                return null;
            }

            return "WotF_Race.EntropyRecoveryFromLevel".Translate(gene.Level).Resolve() + ": x" + gene.LevelEntropyRecoveryFactor.ToString("0.###");
        }
    }

    /// <summary>
    /// <c>PsychicEntropyMax</c> and <c>PsychicEntropyRecoveryRate</c> are Core Defs, and a StatPart
    /// can only be attached by editing those Defs. The mod has no Patches folder in this phase, so the
    /// parts are appended once at startup. Both stats already own a StatPart (gear offset, pain), so
    /// their workers never cache them as immutable.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ForestBloodlineStats
    {
        static ForestBloodlineStats()
        {
            StatDef entropyMax = DefDatabase<StatDef>.GetNamed("PsychicEntropyMax");
            entropyMax.parts.Add(new StatPart_ForestBloodlineEntropy());
            StatDef entropyRecovery = DefDatabase<StatDef>.GetNamed("PsychicEntropyRecoveryRate");
            entropyRecovery.parts.Add(new StatPart_ForestBloodlineEntropyRecovery());
        }
    }
}
