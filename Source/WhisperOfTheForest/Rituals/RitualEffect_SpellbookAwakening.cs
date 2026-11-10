using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The rite of spellbook awakening: a book-bearer calls one basic spellbook out of the old
    /// world's remnants and leaves it beside the altar. The book is an ordinary wearable item; any
    /// forest-blooded pawn can pick it up and wear it, which is what opens casting for them.
    /// </summary>
    public sealed class RitualEffect_SpellbookAwakening : RitualEffectWorker
    {
        private const string SpellbookDefName = "WotF_Spellbook_Basic";

        public override void Apply(RitualContext context)
        {
            Thing book = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(SpellbookDefName));
            if (!GenPlace.TryPlaceThing(book, context.Altar.Position, context.Map, ThingPlaceMode.Near))
            {
                book.Destroy(DestroyMode.Vanish);
                Messages.Message(
                    "WotF_Ritual_SpellbookAwakening_NoRoom".Translate(),
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            Messages.Message(
                "WotF_Ritual_SpellbookAwakening_Done".Translate(),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
