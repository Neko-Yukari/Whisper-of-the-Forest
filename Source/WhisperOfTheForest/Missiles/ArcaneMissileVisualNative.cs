using System;
using RimWorld;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// Native backend for one flying missile: no GameObject at all. It drops vanilla glow flecks along
    /// the authoritative path, so it exercises RimWorld's own mote/fleck pipeline. Flecks expire on
    /// their own, so the worker owns nothing to release.
    /// </summary>
    public class ArcaneMissileNativeVfxWorker : VfxWorker
    {
        /// <summary>One fleck every N game ticks keeps the trail readable without flooding the fleck list.</summary>
        private const int FleckIntervalTicks = 3;

        private const float TrailFleckSize = 0.45f;

        private ArcaneMissileSettings settings;
        private Projectile_ArcaneMissiles projectile;
        private Map map;
        private int firstTick;

        protected override void OnStart()
        {
            ArcaneMissileVfxRequest request = Request as ArcaneMissileVfxRequest;
            projectile = request != null ? request.follow as Projectile_ArcaneMissiles : null;
            if (projectile?.State == null)
            {
                throw new ArgumentException("the native missile effect needs the projectile as follow, with flight state");
            }

            settings = request.settings;
            map = request.map;
            firstTick = projectile.State.ageTicks;
            EmitTrail();
        }

        public override void Update(float deltaSeconds)
        {
            if (map != Find.CurrentMap || !projectile.Spawned)
            {
                return;
            }

            ArcaneMissile missile = projectile.State;
            if (missile.ageTicks - firstTick < FleckIntervalTicks
                || (missile.ageTicks - firstTick) % FleckIntervalTicks != 0)
            {
                return;
            }

            EmitTrail();
        }

        private void EmitTrail()
        {
            ArcaneMissile missile = projectile.State;
            if (missile == null || map != Find.CurrentMap)
            {
                return;
            }

            ArcaneMissileVfxPosition.Of(missile, out float x, out float y, out float z);
            FleckMaker.ThrowLightningGlow(new UnityEngine.Vector3(x, y, z), map, TrailFleckSize * settings.missileSize);
        }
    }

    /// <summary>
    /// The native detonation flash. Flecks carry their own lifetime in game ticks, so the worker only
    /// hands them to the map and finishes: nothing here needs a per-frame update.
    /// </summary>
    public class ArcaneMissileNativeImpactVfxWorker : VfxWorker
    {
        private const float ImpactFlashScale = 0.9f;

        protected override void OnStart()
        {
            ArcaneMissileVfxRequest request = Request as ArcaneMissileVfxRequest;
            if (request == null || request.settings == null)
            {
                throw new ArgumentException("the native missile impact needs an ArcaneMissileVfxRequest with settings");
            }

            var point = new UnityEngine.Vector3(request.origin.x, request.origin.y, request.origin.z);
            float scale = ImpactFlashScale * request.settings.missileSize;
            FleckMaker.Static(point, request.map, FleckDefOf.ExplosionFlash, scale);
            FleckMaker.ThrowLightningGlow(point, request.map, scale);
            Finished = true;
        }

        public override void Update(float deltaSeconds)
        {
        }
    }
}
