using RimWorld;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// The grove shield rite re-arms the altar's built-in interceptor. The interceptor owns the
    /// radius, hit points and active duration; the rite only restores its charge and starts it.
    /// </summary>
    public sealed class RitualEffect_ShieldDome : RitualEffectWorker
    {
        public override void Apply(RitualContext context)
        {
            CompProjectileInterceptor shield = context.Altar.GetComp<CompProjectileInterceptor>();
            shield.currentHitPoints = shield.HitPointsMax;
            shield.Activate();
            Messages.Message(
                "WotF_Ritual_ShieldDome_Done".Translate(),
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }
}
