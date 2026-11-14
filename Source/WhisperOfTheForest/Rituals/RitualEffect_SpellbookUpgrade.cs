using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    public sealed class RitualEffect_SpellbookUpgrade : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            // Slots belong to the pawn (GameComponent_Spellbook), not the consumed apparel.
            Thing book = ThingMaker.MakeThing(context.Def.outputProducts[0].thingDef);
            if (!GenPlace.TryPlaceThing(book, context.Altar.Position, context.Map, ThingPlaceMode.Near))
            {
                book.Destroy(DestroyMode.Vanish);
                Messages.Message("WotF_Ritual_SpellbookUpgrade_NoRoom".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            Messages.Message(string.Format("WotF_Ritual_SpellbookUpgrade_Done".Translate(), book.LabelCap),
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
