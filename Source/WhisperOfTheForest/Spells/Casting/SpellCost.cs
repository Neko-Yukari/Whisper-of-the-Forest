using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// What one cast of a spell costs and pays back: mana and neural heat spent at release, and the
    /// bloodline experience the caster gains for it. Like <see cref="SpellTiming"/>, every rune adds
    /// its own numbers and the book freezes the sum when it creates the spell, so a later Def edit
    /// cannot reprice a spell that is already in a slot.
    ///
    /// The cast checks the cost when it begins and spends it when the spell is released; an
    /// interrupted cast spends nothing.
    /// </summary>
    public class SpellCost : IExposable
    {
        public float mana;
        public float entropy;
        public float experience;

        public bool IsFree => mana <= 0f && entropy <= 0f;

        /// <summary>
        /// Sums the numbers the runes declare. An unknown rune contributes nothing; validation
        /// refuses that chain before a cost is ever derived from it.
        /// </summary>
        public static SpellCost Derive(IReadOnlyList<string> glyphs)
        {
            var cost = new SpellCost();
            if (glyphs == null)
            {
                return cost;
            }

            for (int i = 0; i < glyphs.Count; i++)
            {
                SpellGlyphDef glyph = SpellDefs.Glyph(glyphs[i]);
                if (glyph == null)
                {
                    continue;
                }

                cost.mana += glyph.manaCost;
                cost.entropy += glyph.entropyCost;
                cost.experience += glyph.experience;
            }

            return cost;
        }

        public SpellCost Copy()
        {
            return new SpellCost
            {
                mana = mana,
                entropy = entropy,
                experience = experience
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mana, "mana", 0f);
            Scribe_Values.Look(ref entropy, "entropy", 0f);
            Scribe_Values.Look(ref experience, "experience", 0f);
        }

        public override string ToString()
        {
            return "mana " + mana.ToString("0.#") + " / entropy " + entropy.ToString("0.#")
                   + " / xp " + experience.ToString("0.#");
        }
    }
}
