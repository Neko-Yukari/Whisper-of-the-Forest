using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// A weather rite: the finished performance hands the map's weather over to the Def's weather.
    /// The weather decider keeps it until its own next natural transition, so the rite changes the
    /// sky now rather than pinning a permanent condition.
    /// </summary>
    public sealed class RitualEffect_Weather : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            WeatherDef weather = context.Def.weatherDef;
            context.Map.weatherManager.TransitionTo(weather);
            Messages.Message(
                "WotF_Ritual_Weather_Done".Translate(context.Def.LabelCap, weather.LabelCap),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
