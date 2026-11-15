using System;
using System.Collections.Generic;
using WhisperOfTheForest.Spells;
using static WhisperOfTheForest.Tests.Glyphs;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: SpellCompiler.CanAppend is the one rule the book page greys runes with, the save path
    /// validates with and the cast compiles with (AGENTS.md coding rule 9).
    /// </summary>
    internal static class SpellGrammarTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("grammar: empty bar takes a form", () => Appends(true, null, Projectile)),
            ("grammar: empty bar takes a super rune", () => Appends(true, null, Super)),
            ("grammar: empty bar refuses an effect", () => Appends(false, "WotF_Spells.Reason.FormMustBeFirst", Harm)),
            ("grammar: one form per chain", () => Appends(false, "WotF_Spells.Reason.OneForm", Touch, Projectile)),
            ("grammar: a super rune takes no effect", () => Appends(false, "WotF_Spells.Reason.CompleteFormNoEffect", Harm, Super)),
            ("grammar: augment binds to the form that lists it", () => Appends(true, null, FormAugment, Projectile)),
            ("grammar: augment binds to the nearest effect, not the form", () => Appends(false, "WotF_Spells.Reason.AugmentNotAccepted", FormAugment, Projectile, Harm)),
            ("grammar: exclusion declared by the new augment", () => Appends(false, "WotF_Spells.Reason.AugmentExcluded", Amplify, Projectile, Harm, Dampen)),
            ("grammar: exclusion declared by the bound augment", () => Appends(false, "WotF_Spells.Reason.AugmentExcluded", Dampen, Projectile, Harm, Amplify)),
            ("grammar: chain length is capped by tuning", () => Appends(false, "WotF_Spells.Reason.TooLong", Harm, Projectile, Harm, Harm, Harm)),
            ("grammar: locked rune refused without an unlock", () => Appends(false, "WotF_Spells.Reason.GlyphLocked", Locked, Projectile)),
            ("validate: a form alone has no effect", () => Validates(false, "WotF_Spells.Reason.NoEffect", Projectile)),
            ("validate: a timing rune needs a payload", () => Validates(false, "WotF_Spells.Reason.TimingNeedsEffect", Projectile, Harm, Wait)),
            ("validate: a timing rune with a payload compiles", () => Validates(true, null, Projectile, Wait, Harm)),
            ("validate: a super rune is a whole spell", () => Validates(true, null, Super)),
        };

        private static void Appends(bool expected, string reasonKey, string glyph, params string[] prefix)
        {
            bool accepted = SpellCompiler.CanAppend(prefix, Get(glyph), out string reason);
            Check.Equal(expected, accepted, "append " + glyph + " after [" + string.Join(",", prefix) + "] reason=" + reason);
            Check.Equal(reasonKey, reason, "reason");
        }

        private static void Validates(bool expected, string reasonKey, params string[] glyphs)
        {
            bool valid = SpellCompiler.TryValidate(Recipe(glyphs), out string reason);
            Check.Equal(expected, valid, "validate [" + string.Join(",", glyphs) + "] reason=" + reason);
            Check.Equal(reasonKey, reason, "reason");
        }
    }
}
