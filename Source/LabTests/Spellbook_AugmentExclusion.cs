using System.Collections.Generic;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    /// <summary>
    /// Pierce and Split may not sit on the same projectile, in either order: the book refuses the
    /// second one with a reason, and a finished chain holding both does not validate. Each alone
    /// is still accepted.
    /// </summary>
    [LabTest]
    public sealed class Spellbook_PierceExcludesSplit : LabTest
    {
        public override string Name => "spellbook.pierce-excludes-split";

        protected override void Run(TestContext t)
        {
            SpellGlyphDef pierce = TestContext.Glyph(SpellDefs.AugmentPierce);
            SpellGlyphDef split = TestContext.Glyph(SpellDefs.AugmentSplit);

            var afterPierce = new List<string> { SpellDefs.FormProjectile, SpellDefs.AugmentPierce };
            bool splitAccepted = SpellCompiler.CanAppend(afterPierce, split, out string splitReason);
            t.Measure("splitAfterPierce", splitReason ?? "accepted");
            t.Expect(!splitAccepted, "split was accepted after pierce on the same projectile");
            t.Expect(!splitReason.NullOrEmpty(), "refusing split gave no reason");

            var afterSplit = new List<string> { SpellDefs.FormProjectile, SpellDefs.AugmentSplit };
            bool pierceAccepted = SpellCompiler.CanAppend(afterSplit, pierce, out string pierceReason);
            t.Measure("pierceAfterSplit", pierceReason ?? "accepted");
            t.Expect(!pierceAccepted, "pierce was accepted after split on the same projectile");

            var formOnly = new List<string> { SpellDefs.FormProjectile };
            t.Expect(SpellCompiler.CanAppend(formOnly, pierce, out string pierceAlone), "pierce alone refused: " + pierceAlone);
            t.Expect(SpellCompiler.CanAppend(formOnly, split, out string splitAlone), "split alone refused: " + splitAlone);

            SpellRecipe both = TestContext.Recipe("pierce-split",
                SpellDefs.FormProjectile, SpellDefs.AugmentSplit, SpellDefs.AugmentPierce, SpellDefs.EffectHarm);
            t.Expect(!SpellCompiler.TryValidate(both, out _), "a finished chain with both split and pierce validated");
        }
    }
}
