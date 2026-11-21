using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Marks an apparel Def as a spellbook. Wearing any spellbook opens all ten of the pawn's own
    /// spell bookmarks; <see cref="maxGlyphs"/> limits how many runes a single spell chain may hold
    /// (e.g. 4 / 7 / 10 across book tiers). Spells live with the pawn, not in the book, so swapping
    /// to a smaller book never deletes longer spells: they remain visible in their bookmarks and
    /// become castable again once a higher-tier book is worn.
    /// </summary>
    public class SpellbookExtension : DefModExtension
    {
        public int maxGlyphs;
        public bool allowsSuperGlyphs;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            int ceiling = SpellDefs.Tuning != null ? SpellDefs.Tuning.maxGlyphs : 10;
            if (maxGlyphs < 1 || maxGlyphs > ceiling)
            {
                yield return "maxGlyphs must be 1.." + ceiling + ", is " + maxGlyphs;
            }
        }
    }
}
