using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Race
{
    public class WotF_ForestCraftGeneDef : GeneDef
    {
        public float poweredWorkTableSpeedFactor;
        public int handcraftQualityBonus;
        public EffecterDef elfRepairEffect;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;
            if (geneClass != typeof(Gene_ForestCraft))
                yield return "geneClass must be Gene_ForestCraft";
            if (poweredWorkTableSpeedFactor <= 0f || poweredWorkTableSpeedFactor > 1f)
                yield return "poweredWorkTableSpeedFactor must be in (0, 1]";
            if (handcraftQualityBonus < 0)
                yield return "handcraftQualityBonus must not be negative";
            if (elfRepairEffect == null)
                yield return "elfRepairEffect is null";
        }
    }

    public class Gene_ForestCraft : Gene
    {
        public static Gene_ForestCraft Get(Pawn pawn)
        {
            Gene_ForestCraft gene = pawn?.genes?.GetFirstGeneOfType<Gene_ForestCraft>();
            return gene != null && gene.Active ? gene : null;
        }
    }
}
