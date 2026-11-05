using RimWorld;
using Verse;
using WhisperOfTheForest.Experimental.Spells;
using WhisperOfTheForest.Experimental.Testing;

namespace WhisperOfTheForest.Experimental.LabTests
{
    /// <summary>
    /// The compiler is the single gate for the book, the save path and the cast: [Projectile][Harm]
    /// [AOE] is refused because Harm does not list AOE, while the compatible [Projectile][Harm]
    /// [Amplify] is accepted. Both are checked through the public service the UI calls.
    /// </summary>
    [LabTest]
    public sealed class Compile_RejectsIncompatibleChain : LabTest
    {
        public override string Name => "compile.rejects-incompatible";

        protected override void Run(TestContext t)
        {
            SpellRecipe rejected = TestContext.Recipe("bad-chain", SpellDefs.FormProjectile,
                SpellDefs.EffectHarm, SpellDefs.AugmentAoe);
            SpellRecipe accepted = TestContext.Recipe("good-chain", SpellDefs.FormProjectile,
                SpellDefs.EffectHarm, SpellDefs.AugmentAmplify);

            bool rejectedOk = SpellService.Validate(rejected, out string rejectedReason);
            bool acceptedOk = SpellService.Validate(accepted, out string acceptedReason);
            t.Note("incompatibleValid", rejectedOk);
            t.Measure("incompatibleReason", rejectedReason ?? "accepted");
            t.Note("compatibleValid", acceptedOk);
            t.Measure("compatibleReason", acceptedReason ?? "accepted");

            t.Expect(!rejectedOk, "[Projectile][Harm][AOE] was accepted by the compiler");
            t.Expect(!string.IsNullOrEmpty(rejectedReason), "the refusal has no player-facing reason");
            t.Expect(acceptedOk, "[Projectile][Harm][Amplify] was refused: " + acceptedReason);

            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (book == null)
            {
                t.Fail("GameComponent_Spellbook is not present in this game");
                return;
            }

            book.ClearSlot(0);
            bool saved = book.SaveSlot(0, rejected, out string saveReason);
            t.Measure("saveReason", saveReason ?? "saved");
            t.Note("saved", saved);
            t.Expect(!saved, "the book saved an incompatible chain");
            t.Expect(book.GetSlot(0) == null, "a refused save still wrote the slot");
        }
    }
}
