using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest
{
    /// <summary>
    /// The spell bar: the glyph sequence being composed for the current slot, plus the name typed
    /// next to it. Like Ars Nouveau there is no saved/draft pair here - clicking a glyph appends it
    /// and clicking a bead removes it, so the bar is what the player sees, and only "create" writes
    /// it into the slot. Switching slots or closing the book drops the uncreated edits.
    ///
    /// <see cref="IsModified"/> exists so the lab can still ask whether the bar holds edits no slot
    /// has; nothing in the window blocks on it any more.
    /// </summary>
    internal sealed class SpellbookChain
    {
        public string name = "";

        public readonly List<string> glyphs = new List<string>();

        private string savedName = "";
        private readonly List<string> savedGlyphs = new List<string>();

        /// <summary>True when the bar differs from the slot content it was loaded from.</summary>
        public bool IsModified
        {
            get
            {
                if (name != savedName || glyphs.Count != savedGlyphs.Count)
                {
                    return true;
                }

                for (int i = 0; i < glyphs.Count; i++)
                {
                    if (glyphs[i] != savedGlyphs[i])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasGlyphs
        {
            get { return glyphs.Count > 0; }
        }

        /// <summary>Fills the bar from a saved slot; null clears it.</summary>
        public void LoadFrom(SpellRecipe recipe)
        {
            name = recipe?.name ?? "";
            glyphs.Clear();
            savedGlyphs.Clear();
            savedName = name;

            List<string> source = recipe?.glyphs;
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].NullOrEmpty())
                {
                    continue;
                }

                glyphs.Add(source[i]);
                savedGlyphs.Add(source[i]);
            }
        }

        /// <summary>Empties the bar only; the name stays so it can be retyped.</summary>
        public void ClearBar()
        {
            glyphs.Clear();
        }

        /// <summary>
        /// Appends one rune. Whether that rune is legal is not decided here: the catalogue asks
        /// <see cref="SpellCompiler.CanAppend"/> first, and that is the only copy of the rule.
        /// </summary>
        public void Add(string glyphDefName)
        {
            if (glyphDefName.NullOrEmpty())
            {
                return;
            }

            glyphs.Add(glyphDefName);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= glyphs.Count)
            {
                return;
            }

            glyphs.RemoveAt(index);
        }

        /// <summary>Independent recipe handed to the shared service; never shares list state.</summary>
        public SpellRecipe ToRecipe()
        {
            return new SpellRecipe
            {
                name = name,
                glyphs = new List<string>(glyphs)
            };
        }
    }
}
