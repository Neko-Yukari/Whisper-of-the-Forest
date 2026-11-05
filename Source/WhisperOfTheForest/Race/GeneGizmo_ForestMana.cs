using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Race
{
    /// <summary>
    /// Only exists because <see cref="Gene_Resource"/> creates its gizmo from the gene Def and the
    /// base gizmo type is abstract. Colours come from the Def; this class adds no values.
    /// </summary>
    public class GeneGizmo_ForestMana : GeneGizmo_Resource
    {
        private static bool draggingBar;

        public GeneGizmo_ForestMana(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor)
            : base(gene, drainGenes, barColor, barHighlightColor)
        {
        }

        protected override bool DraggingBar
        {
            get { return draggingBar; }
            set { draggingBar = value; }
        }

        protected override string GetTooltip()
        {
            string text = gene.ResourceLabel.CapitalizeFirst() + ": " + gene.ValueForDisplay + " / " + gene.MaxForDisplay;
            if (!gene.def.resourceDescription.NullOrEmpty())
            {
                text += "\n\n" + gene.def.resourceDescription.Formatted(gene.pawn.Named("PAWN")).Resolve();
            }

            return text;
        }
    }
}
