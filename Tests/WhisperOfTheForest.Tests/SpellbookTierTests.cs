using System;
using System.Collections.Generic;
using System.Linq;
using WhisperOfTheForest.Spells;
using static WhisperOfTheForest.Tests.Glyphs;

namespace WhisperOfTheForest.Tests
{
    /// <summary>
    /// Seam: book tiers cap single-spell rune chain length (4 / 7 / 10) while sharing the same
    /// compiler gate (<see cref="SpellCompiler.CanAppend"/>) and Def config validation
    /// (<see cref="SpellbookExtension.ConfigErrors"/>).
    /// </summary>
    internal static class SpellbookTierTests
    {
        internal static IEnumerable<(string, Action)> All => new (string, Action)[]
        {
            ("tier: apprentice book (4) accepts 4th glyph and refuses 5th with BookTooLong", () => WithTuningMax(10, () =>
            {
                CheckAppend(true, null, 4, Harm, ChainOf(3));
                CheckAppend(false, "WotF_Spells.Reason.BookTooLong", 4, Harm, ChainOf(4));
            })),
            ("tier: adept book (7) accepts 7th glyph and refuses 8th with BookTooLong", () => WithTuningMax(10, () =>
            {
                CheckAppend(true, null, 7, Harm, ChainOf(6));
                CheckAppend(false, "WotF_Spells.Reason.BookTooLong", 7, Harm, ChainOf(7));
            })),
            ("tier: archmage codex (10) accepts 10th glyph and hits tuning cap with TooLong", () => WithTuningMax(10, () =>
            {
                CheckAppend(true, null, 10, Harm, ChainOf(9));
                CheckAppend(false, "WotF_Spells.Reason.TooLong", 10, Harm, ChainOf(10));
            })),
            ("tier: override above tuning cap still stops at tuning cap with TooLong", () => WithTuningMax(10, () =>
            {
                CheckAppend(false, "WotF_Spells.Reason.TooLong", 12, Harm, ChainOf(10));
            })),
            ("tier: SpellbookExtension.ConfigErrors accepts 4/7/10 and rejects out-of-range", () => WithTuningMax(10, () =>
            {
                Check.Equal(0, new SpellbookExtension { maxGlyphs = 4 }.ConfigErrors().Count(), "maxGlyphs=4");
                Check.Equal(0, new SpellbookExtension { maxGlyphs = 7 }.ConfigErrors().Count(), "maxGlyphs=7");
                Check.Equal(0, new SpellbookExtension { maxGlyphs = 10 }.ConfigErrors().Count(), "maxGlyphs=10");
                Check.True(new SpellbookExtension { maxGlyphs = 0 }.ConfigErrors().Any(), "maxGlyphs=0 should fail");
                Check.True(new SpellbookExtension { maxGlyphs = -2 }.ConfigErrors().Any(), "maxGlyphs=-2 should fail");
                Check.True(new SpellbookExtension { maxGlyphs = 11 }.ConfigErrors().Any(), "maxGlyphs=11 should fail");
            })),
            ("tier: SpellbookExtension.ConfigErrors respects current SpellTuningDef.maxGlyphs", () =>
            {
                Check.Equal(0, new SpellbookExtension { maxGlyphs = Glyphs.MaxGlyphs }.ConfigErrors().Count(), "at tuning ceiling");
                Check.True(new SpellbookExtension { maxGlyphs = Glyphs.MaxGlyphs + 1 }.ConfigErrors().Any(), "above tuning ceiling");
            }),
            ("plan summary: compiled nested delay payload produces indented lines", () =>
            {
                SpellCastPlan plan = SpellCompiler.Compile(Recipe(Projectile, Wait, Harm), out string reason);
                Check.True(plan != null, "compile failed: " + reason);
                List<string> lines = plan.BuildSummaryLines();
                Check.Equal(3, lines.Count, "delivery + delay + nested harm lines");
                Check.True(lines[2].StartsWith("  ", StringComparison.Ordinal), "nested harm step should be indented");
            }),
        };

        private static string[] ChainOf(int length)
        {
            var list = new string[length];
            list[0] = Projectile;
            for (int i = 1; i < length; i++)
            {
                list[i] = Harm;
            }

            return list;
        }

        private static void CheckAppend(bool expected, string reasonKey, int maxGlyphsOverride, string glyph, string[] prefix)
        {
            bool accepted = SpellCompiler.CanAppend(prefix, Get(glyph), out string reason, maxGlyphsOverride);
            Check.Equal(expected, accepted, $"append {glyph} len={prefix.Length} cap={maxGlyphsOverride} reason={reason}");
            Check.Equal(reasonKey, reason, "reason");
        }

        private static void WithTuningMax(int maxGlyphs, Action body)
        {
            SpellTuningDef tuning = SpellDefs.Tuning;
            int previous = tuning.maxGlyphs;
            tuning.maxGlyphs = maxGlyphs;
            try
            {
                body();
            }
            finally
            {
                tuning.maxGlyphs = previous;
            }
        }
    }
}
