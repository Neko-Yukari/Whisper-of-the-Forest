using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Spells
{
    /// <summary>
    /// The three phases of a cast in ticks: windup (the caster gathers the spell and may keep
    /// walking), channel (the spell forms; movement is slowed) and recovery (the caster is rooted
    /// while the cast settles). Release, meaning the damage / dart / corridor, happens when the
    /// channel ends.
    ///
    /// A recipe freezes its numbers when the book creates the spell (<see cref="GameComponent_Spellbook.SaveSlot"/>),
    /// and a cast copies them again, so editing a Def afterwards cannot rewrite a saved spell or a
    /// cast that is already running. The chain decides the value: every rune contributes its own
    /// three numbers and the sum is the spell's.
    ///
    /// A phase of zero ticks is over the moment it starts. That is the whole "instant cast" rule:
    /// there is no separate instant path, a spell whose runes add up to zero just walks through its
    /// phases in the same call.
    /// </summary>
    public class SpellTiming : IExposable
    {
        public int windupTicks;
        public int channelTicks;
        public int recoveryTicks;

        /// <summary>
        /// Sums the numbers the runes declare. An unknown rune contributes nothing; validation
        /// refuses that chain before a timing is ever derived from it.
        /// </summary>
        public static SpellTiming Derive(IReadOnlyList<string> glyphs)
        {
            var timing = new SpellTiming();
            if (glyphs == null)
            {
                return timing;
            }

            for (int i = 0; i < glyphs.Count; i++)
            {
                SpellGlyphDef glyph = SpellDefs.Glyph(glyphs[i]);
                if (glyph == null)
                {
                    continue;
                }

                timing.windupTicks += glyph.windupTicks;
                timing.channelTicks += glyph.channelTicks;
                timing.recoveryTicks += glyph.recoveryTicks;
            }

            return timing;
        }

        public SpellTiming Copy()
        {
            return new SpellTiming
            {
                windupTicks = windupTicks,
                channelTicks = channelTicks,
                recoveryTicks = recoveryTicks
            };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref windupTicks, "windupTicks", 0);
            Scribe_Values.Look(ref channelTicks, "channelTicks", 0);
            Scribe_Values.Look(ref recoveryTicks, "recoveryTicks", 0);
        }

        public override string ToString()
        {
            return windupTicks + "/" + channelTicks + "/" + recoveryTicks;
        }
    }
}
