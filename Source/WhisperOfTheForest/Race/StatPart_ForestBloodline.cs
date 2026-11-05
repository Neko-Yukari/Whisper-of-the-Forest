using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Race
{
    /// <summary>Adds the level's mana cap to <c>WotF_ManaMax</c>, which the gene then reads.</summary>
    public class StatPart_ForestBloodlineMana : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Pawn);
            if (gene != null)
            {
                val += gene.LevelData.manaMax;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Pawn);
            if (gene == null)
            {
                return null;
            }

            return "WotF_Race.ManaFromLevel".Translate(gene.Level).Resolve() + ": " + gene.LevelData.manaMax.ToString("0.#");
        }
    }

    /// <summary>Adds the level's neural-heat capacity to the vanilla <c>PsychicEntropyMax</c>.</summary>
    public class StatPart_ForestBloodlineEntropy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Pawn);
            if (gene != null)
            {
                val += gene.LevelData.entropyMaxBonus;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(req.Pawn);
            if (gene == null)
            {
                return null;
            }

            return "WotF_Race.EntropyFromLevel".Translate(gene.Level).Resolve() + ": " + gene.LevelData.entropyMaxBonus.ToString("0.#");
        }
    }

    /// <summary>
    /// <c>PsychicEntropyMax</c> is a Core Def, and a StatPart can only be attached by editing that
    /// Def. The mod has no Patches folder in this phase, so the part is appended once at startup.
    /// The stat already owns a StatPart (the gear offset), so its worker never caches it as immutable.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class ForestBloodlineStats
    {
        static ForestBloodlineStats()
        {
            StatDef entropyMax = DefDatabase<StatDef>.GetNamed("PsychicEntropyMax");
            entropyMax.parts.Add(new StatPart_ForestBloodlineEntropy());
        }
    }
}
