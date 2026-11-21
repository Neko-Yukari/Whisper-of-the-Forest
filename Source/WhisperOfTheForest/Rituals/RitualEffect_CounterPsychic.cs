using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Tier-2 grove counter-rite: cancels every active hostile psychic ritual on the map and stuns
    /// all assigned participants for a duration scaled by ritual strength.
    /// </summary>
    public sealed class RitualEffect_CounterPsychic : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            int stunTicks = context.Def.CounterStunTicksFor(context.RitualStrength);
            PsychicRitualDisruption.DisruptHostileRituals(
                context.Map,
                context.Caster,
                stunTicks,
                out int canceledCount,
                out int stunnedCount);

            float seconds = stunTicks / (float)GenTicks.TicksPerRealSecond;
            if (canceledCount > 0)
            {
                Messages.Message(
                    string.Format(
                        "WotF_Ritual_CounterPsychic_Done".Translate(),
                        canceledCount,
                        stunnedCount,
                        seconds.ToString("0.#")),
                    context.Altar,
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }
            else
            {
                Messages.Message(
                    "WotF_Ritual_CounterPsychic_NoneActive".Translate(),
                    context.Altar,
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }

            WotfLog.Info(WotfLog.ModuleRitual,
                "counter-psychic disrupted " + canceledCount + " rituals, stunned "
                + stunnedCount + " pawns for " + stunTicks + " ticks");
        }
    }
}
