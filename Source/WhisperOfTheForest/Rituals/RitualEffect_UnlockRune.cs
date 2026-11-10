using RimWorld;
using Verse;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Opens one rune for the whole colony. The altar has already taken the materials and the mana
    /// and the rune is carried in the context; this worker only records the unlock.
    /// </summary>
    public sealed class RitualEffect_UnlockRune : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            bool unlocked = GameComponent_Spellbook.Current.Unlock(context.Rune);
            Messages.Message(
                (unlocked ? "WotF_Ritual_RuneAwakening_Done" : "WotF_Ritual_RuneAlreadyUnlocked")
                .Translate(context.Rune.LabelCap),
                unlocked ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                false);
        }
    }
}
