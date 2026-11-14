using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// Marks an apparel Def as a spellbook. Wearing one is what lets a forest-blooded pawn cast; its
    /// slot count says how many of the pawn's own spell slots are open. Spells live with the pawn,
    /// not in the book, so taking a book off or swapping it never deletes a spell: slots beyond the
    /// worn book's count are only sealed until a larger book is worn.
    /// </summary>
    public class SpellbookExtension : DefModExtension
    {
        public int slotCount;
        public bool allowsSuperGlyphs;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (slotCount < 1 || slotCount > GameComponent_Spellbook.SlotCount)
            {
                yield return "slotCount must be 1.." + GameComponent_Spellbook.SlotCount + ", is " + slotCount;
            }
        }
    }
}
