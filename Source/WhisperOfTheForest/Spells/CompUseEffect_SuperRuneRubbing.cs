using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Spells
{
    public class CompProperties_SuperRuneRubbing : CompProperties_UseEffect
    {
        public SpellGlyphDef glyph;

        public CompProperties_SuperRuneRubbing()
        {
            compClass = typeof(CompUseEffect_SuperRuneRubbing);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;
            if (glyph == null || glyph.kind != SpellGlyphKind.Super || !glyph.requiresUnlock)
                yield return "a super rune rubbing requires a locked Super glyph";
        }
    }

    public class CompUseEffect_SuperRuneRubbing : CompUseEffect
    {
        private CompProperties_SuperRuneRubbing Props => (CompProperties_SuperRuneRubbing)props;

        public override AcceptanceReport CanBeUsedBy(Pawn pawn)
        {
            return GameComponent_Spellbook.IsUnlockedInData(Props.glyph)
                ? (AcceptanceReport)SpellText.T("WotF_Spells.Rubbing.AlreadyKnown")
                : AcceptanceReport.WasAccepted;
        }

        public override void DoEffect(Pawn usedBy)
        {
            if (GameComponent_Spellbook.Current.Unlock(Props.glyph))
            {
                parent.SplitOff(1).Destroy();
                Messages.Message(SpellText.TF("WotF_Spells.Rubbing.Unlocked", Props.glyph.LabelCap),
                    usedBy, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
