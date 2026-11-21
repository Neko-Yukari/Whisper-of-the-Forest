using System.Collections.Generic;
using RimWorld;
using Verse;
using WhisperOfTheForest.Race;

namespace WhisperOfTheForest.Mana
{
    public class CompProperties_ManaVial : CompProperties_UseEffect
    {
        public float mana;

        public CompProperties_ManaVial()
        {
            compClass = typeof(CompUseEffect_ManaVial);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats(req))
            {
                yield return entry;
            }

            yield return new StatDrawEntry(
                StatCategoryDefOf.BasicsNonPawn,
                "WotF_Stat_ManaVialRestore".Translate(),
                mana.ToString("0.#"),
                "WotF_Stat_ManaVialRestore_Desc".Translate(),
                2500);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }

            if (mana <= 0f)
            {
                yield return parentDef.defName + " mana vial restores no mana.";
            }
        }
    }

    /// <summary>
    /// A bottled part of the network. The altar fills the vial by drawing more mana than the vial
    /// gives back, so bottling is a portable reserve rather than a way to create mana.
    /// </summary>
    public class CompUseEffect_ManaVial : CompUseEffect
    {
        public CompProperties_ManaVial Props => (CompProperties_ManaVial)props;

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(p);
            if (gene == null)
            {
                return "WotF_ManaVial_NoBloodline".Translate();
            }

            return gene.CurrentMana >= gene.MaxMana - 0.01f
                ? "WotF_ManaVial_Full".Translate()
                : AcceptanceReport.WasAccepted;
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Gene_ForestBloodline gene = Gene_ForestBloodline.Get(usedBy);
            if (gene != null)
            {
                gene.Value += Props.mana;
            }
        }
    }
}
