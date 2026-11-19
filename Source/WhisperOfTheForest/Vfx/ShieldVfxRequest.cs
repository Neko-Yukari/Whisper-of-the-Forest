using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhisperOfTheForest.Vfx
{
    public enum ShieldVfxShape
    {
        Dome,
        Planar
    }

    public readonly struct ShieldHitEvent
    {
        public readonly Vector3 HitWorldPos;
        public readonly Vector3 IncomingDirectionXZ;
        public readonly bool IsAirProjectile;
        public readonly int RemainingCharges;
        public readonly int MaxCharges;

        public ShieldHitEvent(
            Vector3 hitWorldPos,
            Vector3 incomingDirectionXZ,
            bool isAirProjectile,
            int remainingCharges,
            int maxCharges)
        {
            HitWorldPos = hitWorldPos;
            IncomingDirectionXZ = incomingDirectionXZ;
            IsAirProjectile = isAirProjectile;
            RemainingCharges = remainingCharges;
            MaxCharges = maxCharges;
        }
    }

    public readonly struct ShieldPassEvent
    {
        public readonly Vector3 PassWorldPos;
        public readonly Vector3 TravelDirectionXZ;
        public readonly bool IsAirProjectile;

        public ShieldPassEvent(Vector3 passWorldPos, Vector3 travelDirectionXZ, bool isAirProjectile)
        {
            PassWorldPos = passWorldPos;
            TravelDirectionXZ = travelDirectionXZ;
            IsAirProjectile = isAirProjectile;
        }
    }

    /// <summary>
    /// Gameplay-to-VFX contract for hit-count shields (both grove dome and planar spell barrier).
    /// Carries only geometry, charge counts and lifecycle flags; look and timing belong to <see cref="ShieldLumenVfxDef"/>.
    /// </summary>
    public sealed class ShieldVfxRequest : VfxRequest
    {
        public ShieldVfxShape shape;
        public float radius;
        public float spanLength;
        public int maxCharges;
        public int currentCharges;
        public int durationTicks;
        public int remainingTicks;
        public bool respawningAfterLoad;
    }

    public class ShieldLumenVfxDef : VfxDef
    {
        public string coreColor;
        public string primaryColor;
        public string secondaryColor;
        public string lowChargeColor;
        public float hdrIntensity;

        // 2.5D Pawn Z-depth occlusion altitudes (Pawn sits at Y ~ 8.415)
        public float backAltitudeY;
        public float frontAltitudeY;
        public float zTransitionHalf;

        // Ribbon & breath parameters
        public float coreWidthCells;
        public float glowWidthCells;
        public float edgePower;
        public float pulseHz;
        public float pulseMinAlpha;
        public float pulseMaxAlpha;
        public int ringSegments;

        // Lifecycle durations
        public float openSeconds;
        public float breakSeconds;
        public float expireSeconds;

        // Surface-propagating geodesic ripple & friendly pass-through parameters
        public float rippleDurationSeconds;
        public float passRippleDurationSeconds;
        public float rippleWaveSpeed;
        public float passWaveSpeed;
        public float rippleCrestSpacing;
        public int rippleCrestCount;

        // Planar 3D cambered raked barrier geometry (used when shape == Planar)
        public float wallHeightCells;
        public float camberCells;
        public float topRakeCells;
        public float curtainAlpha;

        // Grove Dome 3D spherical canopy geometry (used when shape == Dome)
        public float domeCanopyHeightCells;
        public int domeCanopyRibCount;
        public int domeRuneArcCount;
        public float domeSpinRadPerSec;

        // Native RimLumen EffectAssets & SoundDefs
        public string activateLumenEffectId;
        public string sustainLumenEffectId;
        public string hitLumenEffectId;
        public string airHitLumenEffectId;
        public string passLumenEffectId;
        public string breakLumenEffectId;

        public SoundDef activateSound;
        public SoundDef hitSound;
        public SoundDef passSound;
        public SoundDef breakSound;

        public override bool SupportsState(string state)
        {
            return state == VfxStates.Break || state == VfxStates.Expire;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (lifetime != VfxLifetime.FollowThing)
            {
                yield return "ShieldLumenVfxDef must use VfxLifetime.FollowThing";
            }

            if (!VfxColors.IsValid(coreColor) || !VfxColors.IsValid(primaryColor)
                || !VfxColors.IsValid(secondaryColor) || !VfxColors.IsValid(lowChargeColor))
            {
                yield return "coreColor, primaryColor, secondaryColor and lowChargeColor must be " + VfxColors.Format;
            }

            if (hdrIntensity <= 0f || coreWidthCells <= 0f || glowWidthCells <= 0f || edgePower <= 0f)
            {
                yield return "hdrIntensity, coreWidthCells, glowWidthCells and edgePower must be positive";
            }

            if (backAltitudeY <= 0f || frontAltitudeY <= backAltitudeY || zTransitionHalf <= 0f)
            {
                yield return "invalid 2.5D depth masking altitudes (require 0 < backAltitudeY < frontAltitudeY)";
            }

            if (ringSegments < 16)
            {
                yield return "ringSegments must be at least 16";
            }

            if (openSeconds <= 0f || breakSeconds <= 0f || expireSeconds <= 0f)
            {
                yield return "openSeconds, breakSeconds and expireSeconds must be positive";
            }

            if (rippleDurationSeconds <= 0f || passRippleDurationSeconds <= 0f
                || rippleWaveSpeed <= 0f || passWaveSpeed <= 0f
                || rippleCrestSpacing <= 0f || rippleCrestCount < 2)
            {
                yield return "surface ripple durations, speeds, spacing and crestCount (>=2) must be positive";
            }
        }
    }

    /// <summary>
    /// RimLumen 2.5D/3D shield worker for both the Grove Dome Rite and the Planar Shield Spell:
    ///   - Planar: true 3D cambered &amp; top-raked barrier projected through <c>(x, z + 0.5*y)</c>,
    ///     end anchor pylons, vertical structural ribs, per-slice 2.5D Pawn Z-depth occlusion,
    ///     elastic membrane flex, and boundary-clamped iso-geodesic surface-propagating wave ripples.
    ///   - Dome: ultra-clean breathable perimeter ward ring + counter-rotating elven rune arcs +
    ///     subtle 3D spherical canopy ribs + true 3D spherical geodesic wave rings climbing up from
    ///     rim impacts or gliding down the hemisphere from overhead mortar/bombardment impacts.
    ///   - Friendly Pass-Through: subtle phase-aperture surface shimmer + native RimLumen Ripple distortion.
    /// </summary>
    public sealed class ShieldLumenVfxWorker : VfxWorker
    {
        private enum Phase
        {
            Sustaining,
            Breaking,
            Expiring
        }

        private struct SurfaceWave
        {
            public Vector3 worldPos;
            public float sParam;
            public float vParam;
            public int birthTick;
            public float durationSeconds;
            public bool isAir;
            public bool isFriendly;
        }

        private struct BreakShard
        {
            public float sOrAngle;
            public float heightFrac;
            public Vector3 velocity;
            public float baseAngle;
            public float spinSpeed;
            public float size;
        }

        private const int MaxActiveWaves = 12;

        private ShieldLumenVfxDef def;
        private ShieldVfxRequest shieldReq;
        private Map map;
        private Phase phase = Phase.Sustaining;
        private int startTick;
        private int endPhaseTick = -1;
        private int currentCharges;
        private int maxCharges;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;
        private Color lowChargeColor;

        private Vector3 centerXZ;
        private Vector3 normalXZ;
        private Vector3 tangentXZ;
        private float domeRadius;
        private float spanLength;

        private readonly List<SurfaceWave> waves = new List<SurfaceWave>(MaxActiveWaves);
        private readonly List<Vector3> polylineBuf = new List<Vector3>(128);
        private readonly List<float> nearbyPawnZs = new List<float>(32);
        private readonly List<float> nearbyPawnXs = new List<float>(32);
        private BreakShard[] shards;

        private LumenRibbonMesh depthMesh;
        private LumenRibbonMesh overMesh;
        private ILumenEffectHandle sustainHandle;

        protected override void OnStart()
        {
            def = Def as ShieldLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("ShieldLumenVfxWorker requires ShieldLumenVfxDef");
            }

            shieldReq = Request as ShieldVfxRequest;
            if (shieldReq == null)
            {
                throw new InvalidOperationException("ShieldLumenVfxWorker requires ShieldVfxRequest");
            }

            map = shieldReq.map;
            startTick = Find.TickManager.TicksGame;
            maxCharges = Mathf.Max(1, shieldReq.maxCharges);
            currentCharges = Mathf.Clamp(shieldReq.currentCharges, 0, maxCharges);
            domeRadius = Mathf.Max(0.5f, shieldReq.radius);
            spanLength = Mathf.Max(0.5f, shieldReq.spanLength);

            coreColor = VfxColors.ParseUnity(def.coreColor);
            primaryColor = VfxColors.ParseUnity(def.primaryColor);
            secondaryColor = VfxColors.ParseUnity(def.secondaryColor);
            lowChargeColor = VfxColors.ParseUnity(def.lowChargeColor);
            if (shieldReq.theme != null)
            {
                coreColor = shieldReq.theme.CoreColorUnity;
                primaryColor = shieldReq.theme.PrimaryColorUnity;
                secondaryColor = shieldReq.theme.SecondaryColorUnity;
            }

            centerXZ = new Vector3(shieldReq.origin.x, def.frontAltitudeY, shieldReq.origin.z);
            var dir = new Vector3(shieldReq.direction.x, 0f, shieldReq.direction.z);
            normalXZ = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            tangentXZ = new Vector3(-normalXZ.z, 0f, normalXZ.x);

            depthMesh = new LumenRibbonMesh("WotF_ShieldDepth_" + def.defName);
            overMesh = new LumenRibbonMesh("WotF_ShieldOver_" + def.defName);
            StartSustainLumen();

            if (!shieldReq.respawningAfterLoad)
            {
                if (RimLumenApi.IsReady && !def.activateLumenEffectId.NullOrEmpty())
                {
                    RimLumenApi.Play(
                        def.activateLumenEffectId,
                        map,
                        new System.Numerics.Vector3(centerXZ.x, 0f, centerXZ.z),
                        1f,
                        Mathf.Atan2(normalXZ.x, normalXZ.z) * Mathf.Rad2Deg,
                        false);
                }

                if (map == Find.CurrentMap && def.activateSound != null)
                {
                    def.activateSound.PlayOneShot(new TargetInfo(centerXZ.ToIntVec3(), map));
                }
            }
        }

        public override void OnShieldHit(in ShieldHitEvent hit)
        {
            if (Finished)
            {
                return;
            }

            currentCharges = Mathf.Max(0, hit.RemainingCharges);
            maxCharges = Mathf.Max(1, hit.MaxCharges);

            float sParam = 0f;
            float vParam = 0.48f;
            float spawnHeightY = 0.55f;
            Vector3 spawnWorld = hit.HitWorldPos;
            Vector3 reboundNormal = normalXZ;

            if (shieldReq.shape == ShieldVfxShape.Planar)
            {
                float half = Mathf.Max(0.25f, spanLength * 0.5f);
                float along = (hit.HitWorldPos.x - centerXZ.x) * tangentXZ.x
                    + (hit.HitWorldPos.z - centerXZ.z) * tangentXZ.z;
                sParam = Mathf.Clamp(along / half, -0.96f, 0.96f);
                uint hHash = unchecked((uint)(Find.TickManager.TicksGame * 747796405 + waves.Count * 2891336453u));
                vParam = 0.24f + ((hHash & 0xFFFFu) / 65535f) * 0.52f;
                Vector3 pt3 = EvalPlanar3D(sParam, vParam, 1f, 1f, includeFlex: false, out _);
                spawnWorld = new Vector3(pt3.x, 0f, pt3.z);
                spawnHeightY = pt3.y;
                reboundNormal = normalXZ;
            }
            else
            {
                float dx = hit.HitWorldPos.x - centerXZ.x;
                float dz = hit.HitWorldPos.z - centerXZ.z;
                float r = Mathf.Sqrt(dx * dx + dz * dz);
                if (r > 0.01f)
                {
                    reboundNormal = new Vector3(dx / r, 0f, dz / r);
                }

                if (hit.IsAirProjectile)
                {
                    float u = Mathf.Clamp01(r / domeRadius);
                    spawnHeightY = Mathf.Max(0.4f, def.domeCanopyHeightCells * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)));
                }
                else
                {
                    spawnHeightY = 0.55f;
                }
            }

            PushWave(new SurfaceWave
            {
                worldPos = spawnWorld,
                sParam = sParam,
                vParam = vParam,
                birthTick = Find.TickManager.TicksGame,
                durationSeconds = def.rippleDurationSeconds,
                isAir = hit.IsAirProjectile,
                isFriendly = false
            });

            if (RimLumenApi.IsReady)
            {
                string effectId = hit.IsAirProjectile && !def.airHitLumenEffectId.NullOrEmpty()
                    ? def.airHitLumenEffectId
                    : def.hitLumenEffectId;
                if (!effectId.NullOrEmpty())
                {
                    float rot = Mathf.Atan2(reboundNormal.x, reboundNormal.z) * Mathf.Rad2Deg;
                    RimLumenApi.Play(
                        effectId,
                        map,
                        new System.Numerics.Vector3(spawnWorld.x, spawnHeightY, spawnWorld.z),
                        1f,
                        rot,
                        false);
                }
            }

            if (map == Find.CurrentMap && def.hitSound != null)
            {
                def.hitSound.PlayOneShot(new TargetInfo(hit.HitWorldPos.ToIntVec3(), map));
            }
        }

        public override void OnShieldPassThrough(in ShieldPassEvent pass)
        {
            if (Finished || phase != Phase.Sustaining)
            {
                return;
            }

            float sParam = 0f;
            float vParam = 0.46f;
            float spawnHeightY = 0.65f;
            Vector3 spawnWorld = pass.PassWorldPos;

            if (shieldReq.shape == ShieldVfxShape.Planar)
            {
                float half = Mathf.Max(0.25f, spanLength * 0.5f);
                float along = (pass.PassWorldPos.x - centerXZ.x) * tangentXZ.x
                    + (pass.PassWorldPos.z - centerXZ.z) * tangentXZ.z;
                sParam = Mathf.Clamp(along / half, -0.95f, 0.95f);
                Vector3 pt3 = EvalPlanar3D(sParam, vParam, 1f, 1f, includeFlex: false, out _);
                spawnWorld = new Vector3(pt3.x, 0f, pt3.z);
                spawnHeightY = pt3.y;
            }

            PushWave(new SurfaceWave
            {
                worldPos = spawnWorld,
                sParam = sParam,
                vParam = vParam,
                birthTick = Find.TickManager.TicksGame,
                durationSeconds = def.passRippleDurationSeconds,
                isAir = false,
                isFriendly = true
            });

            if (RimLumenApi.IsReady && !def.passLumenEffectId.NullOrEmpty())
            {
                float rot = Mathf.Atan2(pass.TravelDirectionXZ.x, pass.TravelDirectionXZ.z) * Mathf.Rad2Deg;
                RimLumenApi.Play(
                    def.passLumenEffectId,
                    map,
                    new System.Numerics.Vector3(spawnWorld.x, spawnHeightY, spawnWorld.z),
                    1f,
                    rot,
                    false);
            }

            if (map == Find.CurrentMap && def.passSound != null)
            {
                def.passSound.PlayOneShot(new TargetInfo(pass.PassWorldPos.ToIntVec3(), map));
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || phase != Phase.Sustaining)
            {
                return;
            }

            if (state == VfxStates.Break)
            {
                phase = Phase.Breaking;
                endPhaseTick = Find.TickManager.TicksGame;
                InitBreakShards();

                if (sustainHandle != null && sustainHandle.IsAlive)
                {
                    sustainHandle.Stop(false);
                }

                if (RimLumenApi.IsReady && !def.breakLumenEffectId.NullOrEmpty())
                {
                    RimLumenApi.Play(
                        def.breakLumenEffectId,
                        map,
                        new System.Numerics.Vector3(centerXZ.x, 0.1f, centerXZ.z),
                        1f,
                        0f,
                        false);
                }

                if (map == Find.CurrentMap && def.breakSound != null)
                {
                    def.breakSound.PlayOneShot(new TargetInfo(centerXZ.ToIntVec3(), map));
                }
            }
            else if (state == VfxStates.Expire)
            {
                phase = Phase.Expiring;
                endPhaseTick = Find.TickManager.TicksGame;
                if (sustainHandle != null && sustainHandle.IsAlive)
                {
                    sustainHandle.Stop(true);
                }
            }
        }

        public override void FollowLost()
        {
            if (!Finished && phase == Phase.Sustaining)
            {
                phase = Phase.Expiring;
                endPhaseTick = Find.TickManager.TicksGame;
                if (sustainHandle != null && sustainHandle.IsAlive)
                {
                    sustainHandle.Stop(true);
                }
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            for (int i = waves.Count - 1; i >= 0; i--)
            {
                if (SecondsSince(waves[i].birthTick) >= waves[i].durationSeconds)
                {
                    waves.RemoveAt(i);
                }
            }

            if (phase == Phase.Breaking && SecondsSince(endPhaseTick) >= def.breakSeconds)
            {
                Finished = true;
            }
            else if (phase == Phase.Expiring && SecondsSince(endPhaseTick) >= def.expireSeconds)
            {
                Finished = true;
            }
        }

        public override void DrawWorld()
        {
            if (Finished || map != Find.CurrentMap || depthMesh == null || overMesh == null)
            {
                return;
            }

            depthMesh.Clear();
            overMesh.Clear();

            float chargeRatio = Mathf.Clamp01(currentCharges / (float)maxCharges);
            float lowMix = chargeRatio < 0.45f ? (0.45f - chargeRatio) / 0.45f : 0f;
            Color curPrimary = Color.Lerp(primaryColor, lowChargeColor, lowMix * 0.72f);
            Color curSecondary = Color.Lerp(secondaryColor, lowChargeColor, lowMix * 0.52f);

            float elapsed = SecondsSince(startTick);
            float openU = shieldReq.respawningAfterLoad ? 1f : Mathf.Clamp01(elapsed / def.openSeconds);
            float openEase = 1f - (1f - openU) * (1f - openU) * (1f - openU);

            float masterAlpha = 1f;
            float heightScale = openEase;
            float spanScale = 0.35f + 0.65f * openEase;

            if (phase == Phase.Expiring)
            {
                float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.expireSeconds);
                masterAlpha = 1f - u;
                heightScale *= Mathf.Max(0.02f, 1f - u * u);
            }
            else if (phase == Phase.Breaking)
            {
                float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.breakSeconds);
                masterAlpha = Mathf.Max(0f, 1f - u * 2.4f);
            }

            float breath = chargeRatio < 0.25f
                ? (0.76f + 0.24f * Mathf.Sin(elapsed * 36f))
                : Mathf.Lerp(def.pulseMinAlpha, def.pulseMaxAlpha, 0.5f + 0.5f * Mathf.Sin(elapsed * def.pulseHz * Mathf.PI * 2f));
            float hdr = def.hdrIntensity * Mathf.Lerp(0.85f, 1.15f, chargeRatio);

            if (shieldReq.shape == ShieldVfxShape.Planar)
            {
                CollectNearbyPawnsForOcclusion();
                if (masterAlpha > 0.005f)
                {
                    DrawPlanar3D(curPrimary, curSecondary, breath * masterAlpha, hdr, heightScale, spanScale, chargeRatio);
                }

                if (phase == Phase.Breaking)
                {
                    DrawBreakShards(overMesh, curPrimary, hdr * 1.25f);
                }
            }
            else
            {
                if (masterAlpha > 0.005f)
                {
                    DrawDome3D(curPrimary, curSecondary, masterAlpha, breath, hdr, chargeRatio, elapsed);
                }

                if (phase == Phase.Breaking)
                {
                    DrawBreakShards(overMesh, curPrimary, hdr * 1.25f);
                }
            }

            depthMesh.Upload();
            if (depthMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(depthMesh.Mesh, LumenShaders.RibbonDepth);
            }

            overMesh.Upload();
            if (overMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(overMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            if (sustainHandle != null)
            {
                sustainHandle.Dispose();
                sustainHandle = null;
            }

            depthMesh?.Dispose();
            depthMesh = null;
            overMesh?.Dispose();
            overMesh = null;
        }

        // =========================================================================================
        // PLANAR SHIELD: 3D Cambered Raked Wall + Per-Slice 2.5D Pawn Occlusion + Surface Ripples
        // =========================================================================================

        private void CollectNearbyPawnsForOcclusion()
        {
            nearbyPawnXs.Clear();
            nearbyPawnZs.Clear();
            var pawns = map?.mapPawns?.AllPawnsSpawned;
            if (pawns == null)
            {
                return;
            }

            float maxDistSq = (spanLength * 0.5f + 2.5f) * (spanLength * 0.5f + 2.5f);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || !p.Spawned) continue;
                Vector3 dp = p.DrawPos;
                float dx = dp.x - centerXZ.x;
                float dz = dp.z - centerXZ.z;
                if (dx * dx + dz * dz <= maxDistSq)
                {
                    nearbyPawnXs.Add(dp.x);
                    nearbyPawnZs.Add(dp.z);
                }
            }
        }

        /// <summary>
        /// Evaluates RimWorld 2.5D Pawn depth masking altitude for a shield vertex whose ground root sits at
        /// <c>(rootX, rootZ)</c>: if any nearby pawn stands South (closer to camera, <c>pawnZ &lt; rootZ</c>)
        /// within sprite width, drops the vertex altitude to <c>backAltitudeY</c> so the pawn cleanly occludes
        /// the shield behind their torso; otherwise draws at <c>frontAltitudeY</c> in front of pawns behind the wall.
        /// </summary>
        private float EvaluatePlanarAltitude(float rootX, float rootZ)
        {
            float minAlt = def.frontAltitudeY;
            for (int i = 0; i < nearbyPawnZs.Count; i++)
            {
                float dx = Mathf.Abs(nearbyPawnXs[i] - rootX);
                if (dx > 0.72f) continue;
                float pawnZ = nearbyPawnZs[i];
                // Only mask when the pawn's sprite overlaps vertically and the pawn's feet are South of the wall root
                if (pawnZ < rootZ + def.zTransitionHalf && rootZ - pawnZ < def.wallHeightCells * 0.65f + 1.15f)
                {
                    float candidate = SpellCastVfxMath.EvaluatePawnMaskAltitude(
                        rootZ, pawnZ, def.zTransitionHalf, def.frontAltitudeY, def.backAltitudeY);
                    if (candidate < minAlt) minAlt = candidate;
                }
            }

            return minAlt;
        }

        private float EvalMembraneFlex(float s, float v)
        {
            if (waves.Count == 0) return 0f;
            float flex = 0f;
            float halfSpan = spanLength * 0.5f;
            for (int i = 0; i < waves.Count; i++)
            {
                SurfaceWave w = waves[i];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float u = age / w.durationSeconds;
                float du = (s - w.sParam) * halfSpan;
                float dh = (v - w.vParam) * def.wallHeightCells;
                float dist = Mathf.Sqrt(du * du + dh * dh);

                if (w.isFriendly)
                {
                    float front = age * def.passWaveSpeed;
                    float delta = (dist - front) / 0.55f;
                    flex += 0.10f * (1f - u) * Mathf.Exp(-delta * delta) * Mathf.Cos((dist - front) * 5.2f);
                }
                else
                {
                    float front = age * def.rippleWaveSpeed;
                    float delta = (dist - front) / 0.65f;
                    float dent = -0.17f * (1f - u) * (1f - u) * Mathf.Exp(-(dist * dist) / 0.45f) * Mathf.Cos(age * 28f);
                    float ring = -0.085f * (1f - u) * Mathf.Exp(-delta * delta) * Mathf.Cos((dist - front) * 6.5f);
                    flex += dent + ring;
                }
            }

            return flex;
        }

        private Vector3 EvalPlanar3D(float s, float v, float heightScale, float spanScale, bool includeFlex, out Vector2 groundRoot)
        {
            float bowScale = Mathf.Min(1.18f, spanLength / 6f);
            float bow = def.camberCells * bowScale * (1f - s * s);
            float half = spanLength * 0.5f * spanScale;
            float flex = includeFlex ? EvalMembraneFlex(s, v) : 0f;

            float rootX = centerXZ.x + tangentXZ.x * (half * s) + normalXZ.x * bow;
            float rootZ = centerXZ.z + tangentXZ.z * (half * s) + normalXZ.z * bow;
            groundRoot = new Vector2(rootX, rootZ);

            float wx = rootX + normalXZ.x * (flex - def.topRakeCells * v);
            float wz = rootZ + normalXZ.z * (flex - def.topRakeCells * v);
            float wy = v * def.wallHeightCells * heightScale * (0.93f + 0.07f * (1f - s * s));
            return new Vector3(wx, wy, wz);
        }

        private Vector3 ProjectPlanarScreen(float s, float v, float heightScale, float spanScale, bool includeFlex)
        {
            Vector3 p3 = EvalPlanar3D(s, v, heightScale, spanScale, includeFlex, out Vector2 root);
            float altY = EvaluatePlanarAltitude(root.x, root.y);
            return new Vector3(p3.x, altY, p3.z + p3.y * LumenRibbonMesh.Ortho60HeightZ);
        }

        private void DrawPlanar3D(
            Color curPrimary,
            Color curSecondary,
            float masterAlpha,
            float hdr,
            float heightScale,
            float spanScale,
            float chargeRatio)
        {
            int segs = Mathf.Max(24, def.ringSegments);
            float frontFactor = 0.82f + 0.24f * (-normalXZ.z);
            float halfSpan = spanLength * 0.5f * spanScale;

            // 1. Translucent 2-band 3D vertical energy curtain + rails + ribs
            for (int i = 0; i < segs; i++)
            {
                float s0 = -1f + (2f * i) / segs;
                float s1 = -1f + (2f * (i + 1)) / segs;
                float sm = 0.5f * (s0 + s1);

                Vector3 b0 = ProjectPlanarScreen(s0, 0f, heightScale, spanScale, true);
                Vector3 b1 = ProjectPlanarScreen(s1, 0f, heightScale, spanScale, true);
                Vector3 m0 = ProjectPlanarScreen(s0, 0.52f, heightScale, spanScale, true);
                Vector3 m1 = ProjectPlanarScreen(s1, 0.52f, heightScale, spanScale, true);
                Vector3 t0 = ProjectPlanarScreen(s0, 1f, heightScale, spanScale, true);
                Vector3 t1 = ProjectPlanarScreen(s1, 1f, heightScale, spanScale, true);

                float waveGlow = SampleSliceExcitation(sm, halfSpan);
                float cAlpha = def.curtainAlpha * masterAlpha * frontFactor;

                var colBot = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(cAlpha * (1.10f + 0.65f * waveGlow)));
                var colMid = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(cAlpha * (0.45f + 0.55f * waveGlow)));
                var colTop = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(cAlpha * (0.90f + 0.65f * waveGlow)));

                depthMesh.AddShadedQuad(b0, b1, m1, m0, colBot, colMid, hdr * 0.90f);
                depthMesh.AddShadedQuad(m0, m1, t1, t0, colMid, colTop, hdr * 0.95f);

                // Mid-waist runic seam
                var seamCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01((0.18f + 0.35f * waveGlow) * masterAlpha));
                depthMesh.AddTaperedSegment(m0, m1, def.coreWidthCells * 0.42f, def.coreWidthCells * 0.42f, seamCol, seamCol, hdr * 0.9f, def.edgePower, null);

                // Ground rail (outer glow + crisp core)
                var railGlow = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01((0.52f + 0.25f * waveGlow) * masterAlpha));
                var railCore = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.88f * masterAlpha));
                depthMesh.AddTaperedSegment(b0, b1, def.glowWidthCells * 0.72f, def.glowWidthCells * 0.72f, railGlow, railGlow, hdr * 0.9f, def.edgePower, null);
                depthMesh.AddTaperedSegment(b0, b1, def.coreWidthCells * 0.85f, def.coreWidthCells * 0.85f, railCore, railCore, hdr * 1.1f, def.edgePower, null);

                // Top elevated crest rail
                var topGlow = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01((0.48f + 0.25f * waveGlow) * masterAlpha));
                depthMesh.AddTaperedSegment(t0, t1, def.glowWidthCells * 0.65f, def.glowWidthCells * 0.65f, topGlow, topGlow, hdr * 0.95f, def.edgePower, null);
                depthMesh.AddTaperedSegment(t0, t1, def.coreWidthCells * 0.82f, def.coreWidthCells * 0.82f, railCore, railCore, hdr * 1.15f, def.edgePower, null);

                // Outward hostile-side chevron ticks
                if (i % 6 == 3)
                {
                    EvalPlanar3D(sm, 0f, heightScale, spanScale, false, out Vector2 tickRoot);
                    float alt = EvaluatePlanarAltitude(tickRoot.x, tickRoot.y);
                    var pRoot = new Vector3(tickRoot.x, alt, tickRoot.y);
                    var pTip = new Vector3(tickRoot.x + normalXZ.x * 0.26f, alt, tickRoot.y + normalXZ.z * 0.26f);
                    var tickCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(0.65f * masterAlpha));
                    depthMesh.AddTaperedSegment(pRoot, pTip, def.coreWidthCells * 0.65f, def.coreWidthCells * 0.25f, tickCol, tickCol, hdr, def.edgePower, null);
                }

                // Vertical structural energy ribs
                if (i % 6 == 0 && i > 0)
                {
                    var ribCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01((0.38f + 0.35f * waveGlow) * masterAlpha));
                    depthMesh.AddTaperedSegment(b0, t0, def.coreWidthCells * 0.65f, def.coreWidthCells * 0.55f, ribCol, ribCol, hdr, def.edgePower, null);
                }
            }

            // 2. End Anchor Pylons (ground rune socket + bevel end-cap + vertical column + diamond finial)
            for (int e = -1; e <= 1; e += 2)
            {
                EvalPlanar3D(e, 0f, heightScale, spanScale, false, out Vector2 outerFoot);
                Vector2 innerFoot = new Vector2(outerFoot.x - normalXZ.x * def.topRakeCells, outerFoot.y - normalXZ.z * def.topRakeCells);
                float alt = EvaluatePlanarAltitude(outerFoot.x, outerFoot.y);
                float pylonH = def.wallHeightCells * 1.08f * heightScale;

                var pOuter = new Vector3(outerFoot.x, alt, outerFoot.y);
                var pInner = new Vector3(innerFoot.x, alt, innerFoot.y);
                var pTop = new Vector3(innerFoot.x, alt, innerFoot.y + pylonH * LumenRibbonMesh.Ortho60HeightZ);

                depthMesh.AddTiltedEllipseRing(
                    pInner, Vector3.right, Vector3.forward, 0.25f, LumenRibbonMesh.Ortho60AspectZ,
                    18, def.coreWidthCells * 0.65f,
                    new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.62f * masterAlpha)),
                    hdr, def.edgePower, null);

                var bevelCol = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.22f * masterAlpha));
                depthMesh.AddShadedTriangle(pOuter, pInner, pTop, bevelCol, bevelCol,
                    new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.28f * masterAlpha)), hdr * 0.9f);

                var slopeCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(0.65f * masterAlpha));
                depthMesh.AddTaperedSegment(pOuter, pTop, def.coreWidthCells * 0.8f, def.coreWidthCells * 0.55f, slopeCol, slopeCol, hdr, def.edgePower, null);

                var colGlow = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.75f * masterAlpha));
                var colCore = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.95f * masterAlpha));
                depthMesh.AddTaperedSegment(pInner, pTop, def.glowWidthCells * 0.55f, def.glowWidthCells * 0.45f, colGlow, colGlow, hdr, def.edgePower, null);
                depthMesh.AddTaperedSegment(pInner, pTop, def.coreWidthCells * 1.05f, def.coreWidthCells * 0.85f, colCore, colCore, hdr * 1.2f, def.edgePower, null);

                // Floating diamond finial
                float rFin = 0.14f;
                depthMesh.AddShadedQuad(
                    pTop + new Vector3(0f, 0f, -rFin * 1.35f),
                    pTop + new Vector3(rFin * 0.85f, 0f, 0f),
                    pTop + new Vector3(0f, 0f, rFin * 1.35f),
                    pTop + new Vector3(-rFin * 0.85f, 0f, 0f),
                    colCore, colCore, hdr * 1.25f);
            }

            // 3. Silk-smooth boundary-clamped iso-geodesic surface-propagating ripples + rail guided pulses!
            DrawPlanarSurfaceRipples(curPrimary, curSecondary, masterAlpha, hdr, heightScale, spanScale);
        }

        private float SampleSliceExcitation(float s, float halfSpan)
        {
            float sum = 0f;
            for (int i = 0; i < waves.Count; i++)
            {
                SurfaceWave w = waves[i];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float u = age / w.durationSeconds;
                float du = Mathf.Abs(s - w.sParam) * halfSpan;
                float speed = w.isFriendly ? def.passWaveSpeed : def.rippleWaveSpeed;
                float delta = (du - age * speed) / 0.55f;
                sum += Mathf.Exp(-delta * delta) * (1f - u) * (w.isFriendly ? 0.35f : 0.85f);
            }

            return Mathf.Min(1.5f, sum);
        }

        private void DrawPlanarSurfaceRipples(
            Color curPrimary,
            Color curSecondary,
            float masterAlpha,
            float hdr,
            float heightScale,
            float spanScale)
        {
            float halfSpan = spanLength * 0.5f * spanScale;
            float H = def.wallHeightCells * heightScale;
            if (halfSpan <= 0.05f || H <= 0.05f || waves.Count == 0)
            {
                return;
            }

            const int Steps = 84;
            for (int wIdx = 0; wIdx < waves.Count; wIdx++)
            {
                SurfaceWave w = waves[wIdx];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float uLife = age / w.durationSeconds;
                float fade = Mathf.Pow(1f - uLife, 1.35f);

                float u0 = w.sParam * halfSpan;
                float h0 = Mathf.Clamp(w.vParam, 0.10f, 0.90f) * H;

                // Soft surface contact / phase-aperture glow disc
                Vector3 centerScreen = ProjectPlanarScreen(w.sParam, Mathf.Clamp(w.vParam, 0.10f, 0.90f), heightScale, spanScale, true);
                Color glowInner = w.isFriendly
                    ? new Color(0.75f, 1f, 0.94f, Mathf.Clamp01(0.42f * fade * masterAlpha))
                    : new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.78f * (1f - uLife) * (1f - uLife) * masterAlpha));
                depthMesh.AddRadialGlowDisc(
                    centerScreen,
                    (w.isFriendly ? 0.45f : 0.72f) * (0.6f + 0.45f * uLife),
                    18,
                    glowInner,
                    new Color(curPrimary.r, curPrimary.g, curPrimary.b, 0f),
                    hdr * (w.isFriendly ? 1.0f : 1.25f),
                    1.6f,
                    null);

                int crests = w.isFriendly ? Mathf.Min(3, def.rippleCrestCount) : def.rippleCrestCount;
                float speed = w.isFriendly ? def.passWaveSpeed : def.rippleWaveSpeed;
                float spacing = w.isFriendly ? def.rippleCrestSpacing * 0.68f : def.rippleCrestSpacing;

                for (int c = 0; c < crests; c++)
                {
                    float rWave = age * speed - c * spacing;
                    if (rWave <= 0.04f) continue;

                    float crestWeight = (1f - c / (float)crests) * fade * Mathf.Exp(-rWave * (w.isFriendly ? 0.65f : 0.22f));
                    if (crestWeight <= 0.015f) continue;

                    Color baseCol = w.isFriendly
                        ? (c == 0 ? new Color(0.76f, 1f, 0.95f) : curPrimary)
                        : (c == 0 ? coreColor : (c == 1 ? curSecondary : curPrimary));
                    var waveCol = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Clamp01((w.isFriendly ? 0.55f : 0.92f) * crestWeight * masterAlpha));
                    float width = (w.isFriendly ? def.coreWidthCells * 0.55f : def.coreWidthCells * 0.90f) * (1f - 0.18f * c);
                    float offN = w.isFriendly ? (c - 1) * 0.035f : 0f;

                    polylineBuf.Clear();
                    bool prevIn = false;
                    float prevU = 0f, prevH = 0f;

                    for (int k = 0; k <= Steps; k++)
                    {
                        float theta = (k / (float)Steps) * Mathf.PI * 2f;
                        float uCur = u0 + Mathf.Cos(theta) * rWave;
                        float hCur = h0 + Mathf.Sin(theta) * rWave;
                        bool inside = uCur >= -halfSpan && uCur <= halfSpan && hCur >= 0f && hCur <= H;

                        if (inside)
                        {
                            if (!prevIn && k > 0)
                            {
                                float uc = Mathf.Clamp(prevU, -halfSpan, halfSpan);
                                float hc = Mathf.Clamp(prevH, 0f, H);
                                Vector3 pe = ProjectPlanarScreen(uc / halfSpan, hc / H, heightScale, spanScale, true);
                                polylineBuf.Add(pe + new Vector3(normalXZ.x * offN, 0f, normalXZ.z * offN));
                            }

                            Vector3 pt = ProjectPlanarScreen(uCur / halfSpan, hCur / H, heightScale, spanScale, true);
                            polylineBuf.Add(pt + new Vector3(normalXZ.x * offN, 0f, normalXZ.z * offN));
                        }
                        else if (prevIn)
                        {
                            float uc = Mathf.Clamp(uCur, -halfSpan, halfSpan);
                            float hc = Mathf.Clamp(hCur, 0f, H);
                            Vector3 pe = ProjectPlanarScreen(uc / halfSpan, hc / H, heightScale, spanScale, true);
                            polylineBuf.Add(pe + new Vector3(normalXZ.x * offN, 0f, normalXZ.z * offN));
                            FlushWaveArc(depthMesh, width, waveCol, hdr * 1.15f);
                        }

                        prevIn = inside;
                        prevU = uCur;
                        prevH = hCur;
                    }

                    FlushWaveArc(depthMesh, width, waveCol, hdr * 1.15f);

                    // Top & Bottom Rail Guided Wave Pulses when the circular surface wave intersects h=0 or h=H
                    if (!w.isFriendly)
                    {
                        for (int rv = 0; rv <= 1; rv++)
                        {
                            float dyRail = Mathf.Abs(rv * H - h0);
                            if (rWave <= dyRail) continue;
                            float duHit = Mathf.Sqrt(rWave * rWave - dyRail * dyRail);
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                float uHit = u0 + sign * duHit;
                                if (Mathf.Abs(uHit) > halfSpan) continue;
                                float sHit = uHit / halfSpan;
                                Vector3 pA = ProjectPlanarScreen(Mathf.Clamp(sHit - 0.08f, -1f, 1f), rv, heightScale, spanScale, true);
                                Vector3 pB = ProjectPlanarScreen(Mathf.Clamp(sHit + 0.08f, -1f, 1f), rv, heightScale, spanScale, true);
                                var pulseCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.90f * crestWeight * masterAlpha));
                                depthMesh.AddTaperedSegment(pA, pB, def.coreWidthCells * 1.35f, def.coreWidthCells * 1.35f, pulseCol, pulseCol, hdr * 1.25f, def.edgePower, null);
                            }
                        }
                    }
                }
            }
        }

        // =========================================================================================
        // GROVE SHIELD DOME: Breathable Perimeter + Canopy Ribs + 3D Spherical Geodesic Ripples
        // =========================================================================================

        private void DrawDome3D(
            Color curPrimary,
            Color curSecondary,
            float masterAlpha,
            float breath,
            float hdr,
            float chargeRatio,
            float elapsed)
        {
            var domeOrigin = new Vector3(centerXZ.x, def.frontAltitudeY, centerXZ.z);

            // 1. Clean double perimeter ward ring (interior stays 100% transparent & readable)
            var glowCol = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.28f * breath * masterAlpha));
            var coreCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.78f * breath * masterAlpha));
            overMesh.AddTiltedEllipseRing(
                domeOrigin, Vector3.right, Vector3.forward, domeRadius, 1f,
                def.ringSegments, def.glowWidthCells, glowCol, hdr * 0.85f, def.edgePower, null);
            overMesh.AddTiltedEllipseRing(
                domeOrigin, Vector3.right, Vector3.forward, domeRadius, 1f,
                def.ringSegments, def.coreWidthCells, coreCol, hdr, def.edgePower, null);

            // 2. Counter-rotating Elven Rune Arcs (arc fill tracks remaining chargeRatio!)
            if (def.domeRuneArcCount > 0)
            {
                overMesh.AddSegmentedEllipseArcs(
                    domeOrigin,
                    Mathf.Max(0.5f, domeRadius - 0.24f),
                    1f,
                    def.domeRuneArcCount,
                    Mathf.Lerp(0.16f, 0.56f, chargeRatio),
                    elapsed * def.domeSpinRadPerSec,
                    16,
                    def.coreWidthCells * 0.85f,
                    new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(0.52f * masterAlpha)),
                    hdr * 0.95f,
                    def.edgePower,
                    null);
            }

            // 3. Subtle 3D Spherical Canopy Ribs (faint ~8% at rest; brighten on open & aerial mortar hits!)
            float airExcite = (!shieldReq.respawningAfterLoad && elapsed < def.openSeconds)
                ? (1f - elapsed / def.openSeconds) * 0.7f
                : 0f;
            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i].isAir)
                {
                    float age = SecondsSince(waves[i].birthTick);
                    if (age >= 0f && age < waves[i].durationSeconds)
                    {
                        airExcite = Mathf.Max(airExcite, (1f - age / waves[i].durationSeconds) * 0.78f);
                    }
                }
            }

            float ribAlpha = (0.075f + 0.26f * airExcite) * masterAlpha;
            if (def.domeCanopyRibCount > 0 && ribAlpha > 0.005f)
            {
                var ribCol = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(ribAlpha));
                const int RibSteps = 44;
                for (int rib = 0; rib < def.domeCanopyRibCount; rib++)
                {
                    float phi = (rib * Mathf.PI) / def.domeCanopyRibCount + elapsed * 0.04f;
                    float cosP = Mathf.Cos(phi), sinP = Mathf.Sin(phi);
                    polylineBuf.Clear();
                    for (int k = 0; k <= RibSteps; k++)
                    {
                        float u = -1f + (2f * k) / RibSteps;
                        float r = u * domeRadius;
                        float wy = def.domeCanopyHeightCells * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
                        polylineBuf.Add(new Vector3(
                            centerXZ.x + cosP * r,
                            def.frontAltitudeY,
                            centerXZ.z + sinP * r + wy * LumenRibbonMesh.Ortho60HeightZ));
                    }

                    overMesh.AddPolyline(polylineBuf, polylineBuf.Count, def.coreWidthCells * 0.58f, ribCol, hdr * 0.85f, def.edgePower, true, null);
                }
            }

            // 4. True 3D Spherical Geodesic Wave Rings on the Dome Hemisphere!
            DrawDomeGeodesicRipples(curPrimary, curSecondary, masterAlpha, hdr);
        }

        private void DrawDomeGeodesicRipples(Color curPrimary, Color curSecondary, float masterAlpha, float hdr)
        {
            if (waves.Count == 0)
            {
                return;
            }

            const int Steps = 84;
            for (int wIdx = 0; wIdx < waves.Count; wIdx++)
            {
                SurfaceWave w = waves[wIdx];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float uLife = age / w.durationSeconds;
                float fade = Mathf.Pow(1f - uLife, 1.35f);

                float ux = (w.worldPos.x - centerXZ.x) / domeRadius;
                float uz = (w.worldPos.z - centerXZ.z) / domeRadius;
                float rHoriz = Mathf.Sqrt(ux * ux + uz * uz);
                if (rHoriz > 1f)
                {
                    ux /= rHoriz;
                    uz /= rHoriz;
                }

                float uy = w.isAir ? Mathf.Sqrt(Mathf.Max(0.02f, 1f - ux * ux - uz * uz)) : 0.04f;
                var u0 = new Vector3(ux, uy, uz).normalized;

                var e1 = new Vector3(-u0.z, 0f, u0.x);
                if (e1.sqrMagnitude < 1e-5f) e1 = Vector3.right;
                else e1.Normalize();
                Vector3 e2 = Vector3.Cross(u0, e1).normalized;

                var hitScreen = new Vector3(
                    centerXZ.x + domeRadius * u0.x,
                    def.frontAltitudeY,
                    centerXZ.z + domeRadius * u0.z + def.domeCanopyHeightCells * u0.y * LumenRibbonMesh.Ortho60HeightZ);
                Color focalInner = w.isFriendly
                    ? new Color(0.76f, 1f, 0.92f, Mathf.Clamp01(0.42f * fade * masterAlpha))
                    : new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.75f * (1f - uLife) * (1f - uLife) * masterAlpha));
                overMesh.AddRadialGlowDisc(
                    hitScreen,
                    w.isFriendly ? 0.75f : (w.isAir ? 1.65f : 1.20f),
                    20,
                    focalInner,
                    new Color(curPrimary.r, curPrimary.g, curPrimary.b, 0f),
                    hdr * (w.isFriendly ? 1.0f : 1.25f),
                    1.6f,
                    null);

                int crests = w.isFriendly ? Mathf.Min(3, def.rippleCrestCount) : def.rippleCrestCount;
                float speed = w.isFriendly ? def.passWaveSpeed * 1.4f : def.rippleWaveSpeed * 1.85f;
                float spacing = w.isFriendly ? def.rippleCrestSpacing * 1.3f : def.rippleCrestSpacing * 2.1f;

                for (int c = 0; c < crests; c++)
                {
                    float arcDist = age * speed - c * spacing;
                    if (arcDist <= 0.05f) continue;
                    float gamma = arcDist / domeRadius;
                    if (gamma >= Mathf.PI * 0.65f) continue;

                    float crestWeight = (1f - c / (float)crests) * fade * Mathf.Exp(-arcDist * (w.isFriendly ? 0.35f : 0.14f));
                    if (crestWeight <= 0.015f) continue;

                    Color baseCol = w.isFriendly
                        ? new Color(0.75f, 1f, 0.91f)
                        : (c == 0 ? coreColor : (c == 1 ? curSecondary : curPrimary));
                    var waveCol = new Color(baseCol.r, baseCol.g, baseCol.b, Mathf.Clamp01((w.isFriendly ? 0.55f : 0.90f) * crestWeight * masterAlpha));
                    float width = (w.isFriendly ? def.coreWidthCells * 0.65f : def.coreWidthCells * 1.05f) * (1f - 0.18f * c);

                    float cosG = Mathf.Cos(gamma), sinG = Mathf.Sin(gamma);
                    polylineBuf.Clear();
                    for (int k = 0; k <= Steps; k++)
                    {
                        float phi = (k / (float)Steps) * Mathf.PI * 2f;
                        Vector3 q = u0 * cosG + (e1 * Mathf.Cos(phi) + e2 * Mathf.Sin(phi)) * sinG;
                        if (q.y < 0f)
                        {
                            FlushWaveArc(overMesh, width, waveCol, hdr * 1.15f);
                            continue;
                        }

                        polylineBuf.Add(new Vector3(
                            centerXZ.x + domeRadius * q.x,
                            def.frontAltitudeY,
                            centerXZ.z + domeRadius * q.z + def.domeCanopyHeightCells * q.y * LumenRibbonMesh.Ortho60HeightZ));
                    }

                    FlushWaveArc(overMesh, width, waveCol, hdr * 1.15f);

                    // Perimeter rim guided wave pulses where the spherical wave meets y = 0
                    if (u0.y < sinG && !w.isFriendly)
                    {
                        float hLen = Mathf.Max(0.05f, Mathf.Sqrt(u0.x * u0.x + u0.z * u0.z));
                        float baseAng = Mathf.Atan2(u0.z, u0.x);
                        float dAng = Mathf.Acos(Mathf.Clamp(cosG / hLen, -1f, 1f));
                        var pulseCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.88f * crestWeight * masterAlpha));

                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            float aMid = baseAng + sign * dAng;
                            var pA = new Vector3(centerXZ.x + Mathf.Cos(aMid - 0.045f) * domeRadius, def.frontAltitudeY, centerXZ.z + Mathf.Sin(aMid - 0.045f) * domeRadius);
                            var pB = new Vector3(centerXZ.x + Mathf.Cos(aMid + 0.045f) * domeRadius, def.frontAltitudeY, centerXZ.z + Mathf.Sin(aMid + 0.045f) * domeRadius);
                            overMesh.AddTaperedSegment(pA, pB, def.coreWidthCells * 1.75f, def.coreWidthCells * 1.75f, pulseCol, pulseCol, hdr * 1.25f, def.edgePower, null);
                        }
                    }
                }
            }
        }

        private void FlushWaveArc(LumenRibbonMesh target, float width, Color color, float hdr)
        {
            if (polylineBuf.Count >= 2)
            {
                target.AddPolyline(polylineBuf, polylineBuf.Count, width, color, hdr, def.edgePower, true, null);
            }

            polylineBuf.Clear();
        }

        private void PushWave(in SurfaceWave wave)
        {
            if (waves.Count >= MaxActiveWaves)
            {
                waves.RemoveAt(0);
            }

            waves.Add(wave);
        }

        private void InitBreakShards()
        {
            int count = shieldReq.shape == ShieldVfxShape.Dome ? 42 : 28;
            shards = new BreakShard[count];
            var rng = new System.Random(Gen.HashCombineInt(startTick, Find.TickManager.TicksGame));
            for (int i = 0; i < count; i++)
            {
                shards[i] = new BreakShard
                {
                    sOrAngle = shieldReq.shape == ShieldVfxShape.Dome
                        ? (i / (float)count) * Mathf.PI * 2f
                        : -1f + (2f * i) / Mathf.Max(1, count - 1),
                    heightFrac = 0.15f + (float)rng.NextDouble() * 0.78f,
                    velocity = new Vector3(
                        ((float)rng.NextDouble() - 0.5f) * 3.6f,
                        1.2f + (float)rng.NextDouble() * 2.5f,
                        ((float)rng.NextDouble() - 0.5f) * 3.6f),
                    baseAngle = (float)rng.NextDouble() * Mathf.PI * 2f,
                    spinSpeed = ((float)rng.NextDouble() - 0.5f) * 9f,
                    size = 0.14f + (float)rng.NextDouble() * 0.22f
                };
            }
        }

        private void DrawBreakShards(LumenRibbonMesh target, Color curPrimary, float hdr)
        {
            if (shards == null) return;
            float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.breakSeconds);
            float fade = (1f - u) * (1f - u);
            var colCore = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.88f * fade));
            var colEdge = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.65f * fade));

            for (int i = 0; i < shards.Length; i++)
            {
                BreakShard sh = shards[i];
                float wx, wy, wz;
                if (shieldReq.shape == ShieldVfxShape.Dome)
                {
                    wx = centerXZ.x + Mathf.Cos(sh.sOrAngle) * domeRadius + sh.velocity.x * u * 1.4f;
                    wz = centerXZ.z + Mathf.Sin(sh.sOrAngle) * domeRadius + sh.velocity.z * u * 1.4f;
                    wy = Mathf.Max(0f, sh.heightFrac * 1.6f + sh.velocity.y * u - 4.5f * u * u);
                }
                else
                {
                    Vector3 basePt = EvalPlanar3D(sh.sOrAngle, sh.heightFrac, 1f, 1f, false, out _);
                    wx = basePt.x + sh.velocity.x * u * 0.9f;
                    wz = basePt.z + sh.velocity.z * u * 0.9f;
                    wy = Mathf.Max(0f, basePt.y + sh.velocity.y * u * 0.6f - 4.8f * u * u);
                }

                var c = new Vector3(wx, def.frontAltitudeY, wz + wy * LumenRibbonMesh.Ortho60HeightZ);
                float r = sh.size * (1f - 0.55f * u);
                float ang = sh.baseAngle + sh.spinSpeed * u;
                var a1 = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                var a2 = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (r * 0.62f);
                target.AddShadedQuad(c - a1, c + a2, c + a1, c - a2, colEdge, colCore, hdr);
            }
        }

        private void StartSustainLumen()
        {
            if (!RimLumenApi.IsReady || def.sustainLumenEffectId.NullOrEmpty())
            {
                return;
            }

            float rot = Mathf.Atan2(normalXZ.x, normalXZ.z) * Mathf.Rad2Deg;
            float scale = shieldReq.shape == ShieldVfxShape.Dome ? domeRadius : spanLength;
            sustainHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = def.sustainLumenEffectId,
                MapIndex = map != null ? map.uniqueID : -1,
                OriginWorld = new System.Numerics.Vector3(centerXZ.x, 0f, centerXZ.z),
                Scale = Mathf.Max(0.1f, scale),
                RotationDegrees = rot,
                RealTime = false
            });
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
