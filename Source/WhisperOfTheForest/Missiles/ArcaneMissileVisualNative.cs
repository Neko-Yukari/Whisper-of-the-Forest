using RimWorld;
using Verse;

namespace WhisperOfTheForest.Experimental.Missiles
{
    /// <summary>
    /// Native backend: no GameObject at all. It drops vanilla glow flecks along the authoritative
    /// path and a vanilla explosion flash on impact, so it exercises RimWorld's own mote/fleck
    /// pipeline and needs no lifetime bookkeeping (flecks expire themselves).
    /// </summary>
    public class ArcaneMissileVisualNative : ArcaneMissileVisualSource
    {
        /// <summary>One fleck every N game ticks keeps the trail readable without flooding the fleck list.</summary>
        private const int FleckIntervalTicks = 3;

        private const float TrailFleckSize = 0.45f;
        private const float ImpactFlashScale = 0.9f;

        private Map map;
        private int firstTick;
        private bool stopped;

        public override void Spawn(ArcaneMissileVisualFrame frame)
        {
            map = frame.Map;
            firstTick = frame.AgeTicks;
            stopped = false;
            EmitTrail(frame);
        }

        public override void Update(ArcaneMissileVisualFrame frame)
        {
            if (stopped || frame.AgeTicks - firstTick < FleckIntervalTicks
                || (frame.AgeTicks - firstTick) % FleckIntervalTicks != 0)
            {
                return;
            }

            EmitTrail(frame);
        }

        public override void Impact(ArcaneMissileImpactVisualContext context)
        {
            if (context.Map == null)
            {
                return;
            }

            var point = new UnityEngine.Vector3(context.X, context.Y, context.Z);
            float scale = context.Settings != null ? ImpactFlashScale * context.Settings.missileSize : ImpactFlashScale;
            FleckMaker.Static(point, context.Map, FleckDefOf.ExplosionFlash, scale);
            FleckMaker.ThrowLightningGlow(point, context.Map, scale);
        }

        public override void Probe(ArcaneMissileImpactVisualContext context)
        {
            if (context.Map == null)
            {
                return;
            }

            var point = new UnityEngine.Vector3(context.X, context.Y, context.Z);
            FleckMaker.Static(point, context.Map, FleckDefOf.ExplosionFlash, ImpactFlashScale);
            FleckMaker.ThrowLightningGlow(point, context.Map, ImpactFlashScale * 1.5f);
        }

        public override void Dispose()
        {
            stopped = true;
            map = null;
        }

        private void EmitTrail(ArcaneMissileVisualFrame frame)
        {
            if (stopped || map == null)
            {
                return;
            }

            VisualPosition(frame, out float x, out float y, out float z);
            float size = frame.Settings != null ? TrailFleckSize * frame.Settings.missileSize : TrailFleckSize;
            FleckMaker.ThrowLightningGlow(new UnityEngine.Vector3(x, y, z), map, size);
        }
    }
}
