using System;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// The railgun effect: a charging orb at the caster, then one full-length beam with a bright core,
    /// an outer glow, jittered arcs, a residual band, and a full-screen darkening that makes the beam
    /// read as the only light on the map.
    ///
    /// The whole effect runs on the game's tick count, not on a shader or animation clock, so the
    /// visual and the damage cannot drift apart, pausing the game freezes every layer of it (orb,
    /// beam, arcs, afterglow, darkening) and a faster game speed runs it faster. Every look number
    /// comes from <see cref="RailgunVfxDef"/>.
    /// </summary>
    public class RailgunVfxWorker : VfxWorker
    {
        private const int ArcSegments = 14;

        /// <summary>RimWorld advances sixty game ticks per game second at normal speed.</summary>
        private const float TicksPerSecond = 60f;

        private RailgunVfxDef def;
        private Map map;
        private Thing follow;
        private Vector3 aim;
        private int startTick;
        private int chargeTicks;

        private bool fired;
        private int fireTick;
        private Vector3 beamOrigin;
        private float beamDirX;
        private float beamDirZ;
        private float beamLength;
        private int arcSeed;

        private RimUnity.Color coreColor;
        private RimUnity.Color glowColor;
        private RimUnity.Color afterglowColor;
        private RimUnity.Color arcColor;
        private RimUnity.Color chargeColor;

        private float BeamSeconds => def.beamHoldSeconds + def.beamFadeSeconds;

        /// <summary>
        /// Game seconds since the effect started. <c>TicksGame</c> stops advancing while the game is
        /// paused, so this value freezes with it, and it advances faster at a faster game speed; a
        /// rewound counter yields zero instead of a negative time.
        /// </summary>
        private float ElapsedGameSeconds => SecondsSince(startTick);

        protected override void OnStart()
        {
            def = Def as RailgunVfxDef;
            if (def == null)
            {
                throw new System.InvalidOperationException(
                    "RailgunVfxWorker needs a RailgunVfxDef, got " + (Def != null ? Def.defName : "no def"));
            }

            map = Request.map;
            follow = Request.follow;
            aim = Request.target;
            chargeTicks = Mathf.Max(0, Request.chargeTicks);
            startTick = Find.TickManager.TicksGame;
            arcSeed = Gen.HashCombineInt(startTick, map.uniqueID);

            coreColor = VfxColors.Parse(def.coreColor);
            glowColor = VfxColors.Parse(def.glowColor);
            afterglowColor = VfxColors.Parse(def.afterglowColor);
            arcColor = VfxColors.Parse(def.arcColor);
            chargeColor = VfxColors.Parse(def.chargeColor);

            if (chargeTicks <= 0)
            {
                Fire();
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (!fired)
            {
                AdvanceCharge();
                return;
            }

            float elapsed = SecondsSince(fireTick);
            if (elapsed >= BeamSeconds + def.afterglowSeconds)
            {
                Finished = true;
            }
        }

        /// <summary>
        /// The charge is counted in game ticks, so pausing the game pauses the shot. A loaded save or a
        /// rewound tick counter can make the difference negative; that only delays the shot.
        /// </summary>
        private void AdvanceCharge()
        {
            if (Find.TickManager.TicksGame - startTick >= chargeTicks)
            {
                Fire();
            }
        }

        /// <summary>
        /// Locks in the geometry of the shot. The start is where the caster stands <em>now</em>; the aim
        /// point is the one locked at cast time, so a caster that walked during the charge shoots from
        /// its new position towards the same point.
        /// </summary>
        private void Fire()
        {
            fired = true;
            fireTick = Find.TickManager.TicksGame;

            Vector3 start = follow != null && follow.Spawned ? follow.DrawPos : Request.origin;
            float dx = aim.x - start.x;
            float dz = aim.z - start.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance > 0.0001f)
            {
                beamDirX = dx / distance;
                beamDirZ = dz / distance;
            }
            else
            {
                beamDirX = Request.direction.x;
                beamDirZ = Request.direction.z;
                float fallback = Mathf.Sqrt(beamDirX * beamDirX + beamDirZ * beamDirZ);
                if (fallback > 0.0001f)
                {
                    beamDirX /= fallback;
                    beamDirZ /= fallback;
                }
                else
                {
                    beamDirX = 1f;
                    beamDirZ = 0f;
                }
            }

            beamOrigin = new Vector3(start.x, def.beamHeight, start.z);
            beamLength = RailgunVfxDraw.TryClipToMap(map, start.x, start.z, beamDirX, beamDirZ, Request.length,
                out float clipped)
                ? clipped
                : Request.length;
        }

        public override void DrawOverlay()
        {
            if (map != Find.CurrentMap)
            {
                return;
            }

            if (!fired)
            {
                DrawCharge();
                return;
            }

            float elapsed = SecondsSince(fireTick);
            DrawDim(elapsed);
            RimUnity.Material material = RailgunVfxDraw.BeamMaterial();
            float beamAlpha = BeamAlpha(elapsed);
            if (beamAlpha > 0f)
            {
                DrawBeam(material, def.glowWidthCells * 0.5f, glowColor, beamAlpha * 0.75f);
                DrawBeam(material, def.coreWidthCells * 0.5f, coreColor, beamAlpha);
                DrawArcs(material, beamAlpha);
            }

            DrawAfterglow(material, elapsed);
        }

        private float BeamAlpha(float elapsed)
        {
            if (elapsed <= def.beamHoldSeconds)
            {
                return 1f;
            }

            float fade = elapsed - def.beamHoldSeconds;
            if (fade >= def.beamFadeSeconds)
            {
                return 0f;
            }

            float k = 1f - fade / Mathf.Max(def.beamFadeSeconds, 0.0001f);
            return k * k;
        }

        private void DrawCharge()
        {
            RimUnity.Material material = RailgunVfxDraw.OrbMaterial();
            Vector3 position = follow != null && follow.Spawned ? follow.DrawPos : Request.origin;
            if (material == null || !RailgunVfxDraw.TryProject(position, out RimUnity.Vector2 gui))
            {
                return;
            }

            float progress = chargeTicks <= 0
                ? 1f
                : Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)chargeTicks);
            float pulse = 0.5f + 0.5f * Mathf.Sin(ElapsedGameSeconds * Mathf.PI * 2f * def.chargePulseHz);
            float cell = RailgunVfxDraw.CellSize();
            float radius = def.chargeRadiusCells * (0.35f + 0.65f * progress) * cell;

            RailgunVfxDraw.DrawSquare(material, gui, radius * 1.7f,
                RailgunVfxDraw.WithAlpha(glowColor, 0.18f + 0.25f * progress));
            RailgunVfxDraw.DrawSquare(material, gui, radius * (0.75f + 0.2f * pulse),
                RailgunVfxDraw.WithAlpha(chargeColor, 0.3f + 0.5f * progress * pulse));
            RailgunVfxDraw.DrawSquare(material, gui, radius * 0.35f,
                RailgunVfxDraw.WithAlpha(coreColor, 0.5f + 0.5f * progress));
        }

        private void DrawBeam(RimUnity.Material material, float halfWidthCells, RimUnity.Color color, float alpha)
        {
            if (material == null || alpha <= 0f)
            {
                return;
            }

            var start = new Vector3(beamOrigin.x, beamOrigin.y, beamOrigin.z);
            var end = new Vector3(beamOrigin.x + beamDirX * beamLength, beamOrigin.y,
                beamOrigin.z + beamDirZ * beamLength);
            if (!RailgunVfxDraw.TryProject(start, out RimUnity.Vector2 fromGui)
                || !RailgunVfxDraw.TryProject(end, out RimUnity.Vector2 toGui))
            {
                return;
            }

            float halfWidthPixels = Mathf.Max(1f, halfWidthCells * RailgunVfxDraw.CellSize());
            RailgunVfxDraw.DrawBand(material, fromGui, toGui, halfWidthPixels, RailgunVfxDraw.WithAlpha(color, alpha));
        }

        /// <summary>Jittered arcs along the beam, re-rolled a fixed number of times per second.</summary>
        private void DrawArcs(RimUnity.Material material, float alpha)
        {
            if (material == null || def.arcCount <= 0 || alpha <= 0f || beamLength <= 0.5f)
            {
                return;
            }

            int roll = (int)(ElapsedGameSeconds * Mathf.Max(def.arcFps, 0.1f));
            var random = new System.Random(Gen.HashCombineInt(arcSeed, roll));
            float cell = RailgunVfxDraw.CellSize();
            float perpendicularX = -beamDirZ;
            float perpendicularZ = beamDirX;
            float halfWidth = Mathf.Max(0.5f, def.arcWidthCells * cell * 0.5f);

            for (int arc = 0; arc < def.arcCount; arc++)
            {
                bool hasPrevious = false;
                RimUnity.Vector2 previous = default(RimUnity.Vector2);
                for (int step = 0; step <= ArcSegments; step++)
                {
                    float along = step / (float)ArcSegments;
                    float taper = Mathf.Sin(along * Mathf.PI);
                    float lateral = ((float)random.NextDouble() * 2f - 1f) * def.arcAmplitudeCells * taper;

                    var world = new Vector3(
                        beamOrigin.x + beamDirX * beamLength * along + perpendicularX * lateral,
                        beamOrigin.y,
                        beamOrigin.z + beamDirZ * beamLength * along + perpendicularZ * lateral);

                    if (!RailgunVfxDraw.TryProject(world, out RimUnity.Vector2 gui))
                    {
                        hasPrevious = false;
                        continue;
                    }

                    var point = new RimUnity.Vector2(gui.x, gui.y);
                    if (hasPrevious)
                    {
                        RailgunVfxDraw.DrawBand(material, previous, point, halfWidth,
                            RailgunVfxDraw.WithAlpha(arcColor, alpha * 0.9f));
                    }

                    hasPrevious = true;
                    previous = point;
                }
            }
        }

        /// <summary>The band left behind after the beam has faded: it narrows as it dissolves.</summary>
        private void DrawAfterglow(RimUnity.Material material, float elapsed)
        {
            if (def.afterglowSeconds <= 0f || elapsed < BeamSeconds || material == null)
            {
                return;
            }

            float k = (elapsed - BeamSeconds) / Mathf.Max(def.afterglowSeconds, 0.0001f);
            if (k >= 1f)
            {
                return;
            }

            float alpha = (1f - k) * (1f - k) * 0.55f;
            DrawBeam(material, def.afterglowWidthCells * 0.5f * (0.3f + 0.7f * (1f - k)), afterglowColor, alpha);
        }

        private void DrawDim(float elapsed)
        {
            if (def.darkenStrength <= 0f)
            {
                return;
            }

            float total = def.darkenInSeconds + def.darkenHoldSeconds + def.darkenOutSeconds;
            float strength;
            if (elapsed <= def.darkenInSeconds)
            {
                strength = def.darkenInSeconds <= 0f ? 1f : elapsed / def.darkenInSeconds;
            }
            else if (elapsed <= def.darkenInSeconds + def.darkenHoldSeconds)
            {
                strength = 1f;
            }
            else if (elapsed < total)
            {
                strength = 1f - (elapsed - def.darkenInSeconds - def.darkenHoldSeconds)
                               / Mathf.Max(def.darkenOutSeconds, 0.0001f);
            }
            else
            {
                return;
            }

            RimUnity.Color tint = VfxColors.Parse(def.darkenTint);
            RailgunVfxDraw.DrawScreenTint(new Color(tint.r, tint.g, tint.b,
                Mathf.Clamp01(strength) * def.darkenStrength));
        }

        /// <summary>Game seconds between two tick counts; zero when paused, never negative.</summary>
        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / TicksPerSecond;
        }
    }
}
