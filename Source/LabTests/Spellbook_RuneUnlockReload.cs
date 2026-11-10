using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;
using WhisperOfTheForest.Testing;

namespace WhisperOfTheForest.LabTests
{
    [LabTest(Reload = true)]
    public sealed class Spellbook_RuneUnlockReload : LabTest
    {
        private const string RuneDefName = "WotF_Augment_BreakArmor";

        public override string Name => "spellbook.rune-unlock-reload";

        protected override void Run(TestContext t)
        {
            SpellGlyphDef rune = DefDatabase<SpellGlyphDef>.GetNamedSilentFail(RuneDefName);
            GameComponent_Spellbook book = GameComponent_Spellbook.Current;
            if (rune == null || book == null)
            {
                t.Fail("the rune or the spellbook component is missing");
                return;
            }

            t.Expect(!GameComponent_Spellbook.IsUnlocked(rune), "the rune was already unlocked before the test");
            t.Expect(book.Unlock(rune), "the rune could not be unlocked");
            t.Expect(GameComponent_Spellbook.IsUnlocked(rune), "the rune is not unlocked before the save");
        }

        protected override void AfterReload(TestContext t)
        {
            SpellGlyphDef rune = DefDatabase<SpellGlyphDef>.GetNamedSilentFail(RuneDefName);
            if (rune == null)
            {
                t.Fail("the rune def did not survive the save/load");
                return;
            }

            t.Expect(GameComponent_Spellbook.IsUnlocked(rune), "the unlocked rune was lost in the save/load");
        }
    }
}
