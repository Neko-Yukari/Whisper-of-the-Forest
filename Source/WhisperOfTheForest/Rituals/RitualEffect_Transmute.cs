using System;
using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// A production rite: the altar has already taken the input materials from the ground, and this
    /// worker leaves the Def's products beside it. Stack limits are respected so a large product
    /// count becomes several stacks instead of one oversized one.
    /// </summary>
    public sealed class RitualEffect_Transmute : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            foreach (ThingDefCountClass product in context.Def.outputProducts)
            {
                int remaining = product.count;
                while (remaining > 0)
                {
                    int stack = Math.Min(remaining, product.thingDef.stackLimit);
                    Thing thing = ThingMaker.MakeThing(product.thingDef);
                    thing.stackCount = stack;
                    if (!GenPlace.TryPlaceThing(thing, context.Altar.Position, context.Map, ThingPlaceMode.Near))
                    {
                        thing.Destroy(DestroyMode.Vanish);
                        Messages.Message(
                            "WotF_Ritual_Transmute_NoRoom".Translate(),
                            MessageTypeDefOf.RejectInput,
                            false);
                        return;
                    }

                    remaining -= stack;
                }
            }

            Messages.Message(
                "WotF_Ritual_Transmute_Done".Translate(),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
