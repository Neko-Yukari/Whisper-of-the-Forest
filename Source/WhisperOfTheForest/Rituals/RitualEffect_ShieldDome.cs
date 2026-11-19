using RimWorld;
using Verse;
using WhisperOfTheForest.Shields;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Opens (or refreshes) a hit-count dome shield anchored to the altar. Ritual strength scales the
    /// charge count; radius, duration and VFX Def come from <see cref="RitualDef"/>.
    /// </summary>
    public sealed class RitualEffect_ShieldDome : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            int charges = context.Def.ShieldChargesFor(context.RitualStrength);
            ShieldInterception.SpawnOrReplaceDome(
                context.Altar,
                context.EffectRadius,
                charges,
                context.Def.shieldDurationTicks,
                context.Def.shieldVfxDef);
            Messages.Message(
                string.Format("WotF_Ritual_ShieldDome_Done".Translate(), charges),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
