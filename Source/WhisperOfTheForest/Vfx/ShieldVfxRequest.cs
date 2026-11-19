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

        // 1-2 wide, far-propagating underdamped vibration wave & strictly shield-confined refraction parameters
        public float rippleDurationSeconds;
        public float passRippleDurationSeconds;
        public float rippleWaveSpeed;
        public float passWaveSpeed;
        public float rippleCrestSpacing;
        public int rippleCrestCount;
        public float rippleBandHalfWidth;
        public float rippleSpreadScale;
        public float hitDistortionCells;
        public float passDistortionCells;

        // Planar 3D cambered raked barrier geometry (used when shape == Planar)
        public float wallHeightCells;
        public float camberCells;
        public float topRakeCells;
        public float curtainAlpha;
        public float planarPylonRiseFraction;
        public float planarMiddleStartFraction;

        // Grove Dome 3D canopy geometry (used when shape == Dome)
        public float domeCanopyHeightCells;
        public int domeCanopyRibCount;
        public int domeRuneArcCount;
        public float domeSpinRadPerSec;
        public float domeHexSizeCells;
        public int domeHexRings;

        // Native RimLumen EffectAssets & SoundDefs
        public string activateLumenEffectId;
        public string sustainLumenEffectId;
        public string hitLumenEffectId;
        public string airHitLumenEffectId;
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
                || rippleCrestSpacing <= 0f || rippleCrestCount < 1
                || rippleBandHalfWidth <= 0f || rippleSpreadScale <= 0f
                || hitDistortionCells < 0f || passDistortionCells < 0f)
            {
                yield return "surface ripple durations, speeds, spacing, bandHalfWidth, spreadScale and distortion must be valid";
            }

            if (domeHexRings < 0 || (domeHexRings > 0 && domeHexSizeCells <= 0f))
            {
                yield return "domeHexSizeCells must be positive when domeHexRings > 0";
            }

            if (wallHeightCells > 0f && (planarPylonRiseFraction <= 0f || planarPylonRiseFraction > 1f
                || planarMiddleStartFraction < 0f || planarMiddleStartFraction >= 1f))
            {
                yield return "Planar shield requires 0 < planarPylonRiseFraction <= 1 and 0 <= planarMiddleStartFraction < 1";
            }
        }
    }

    /// <summary>
    /// RimLumen 2.5D/3D shield worker for both the Grove Dome Rite and the Planar Shield Spell:
    ///   - Planar: true 3D cambered &amp; top-raked barrier (<c>wallHeightCells = 2.20</c>), end anchor pylons,
    ///     vertical structural ribs, per-slice 2.5D Pawn Z-depth occlusion, elastic membrane flex, and 1-2 wide
    ///     underdamped vibration wave bands + Layer 14 screen-space refraction analytically clamped to the wall.
    ///   - Dome: breathable perimeter ward ring + counter-rotating elven rune arcs + lenticular 3D canopy ribs +
    ///     1-2 far-sweeping 3D spherical geodesic wave bands &amp; Layer 14 refraction strictly inside <c>R = domeRadius</c>.
    ///   - Friendly Pass-Through: 1 gentle phase-slip wave ring + subtle shield-confined Layer 14 refraction.
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
        private readonly Vector3[] hexUnitQ = new Vector3[6];
        private readonly Vector3[] hexCorners = new Vector3[6];
        private BreakShard[] shards;

        private LumenRibbonMesh depthMesh;
        private LumenRibbonMesh overMesh;
        private LumenRibbonMesh distortionMesh;
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
            distortionMesh = new LumenRibbonMesh("WotF_ShieldDistortion_" + def.defName);
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
                    spawnHeightY = Mathf.Max(0.4f, def.domeCanopyHeightCells * Mathf.Max(0f, 1f - u * u));
                }
                else
                {
                    // Vanilla flat-shot Projectile draws at ExactPosition on ground XZ (0 Vertical3D Y offset).
                    spawnHeightY = 0f;
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
            Vector3 spawnWorld = pass.PassWorldPos;

            if (shieldReq.shape == ShieldVfxShape.Planar)
            {
                float half = Mathf.Max(0.25f, spanLength * 0.5f);
                float along = (pass.PassWorldPos.x - centerXZ.x) * tangentXZ.x
                    + (pass.PassWorldPos.z - centerXZ.z) * tangentXZ.z;
                sParam = Mathf.Clamp(along / half, -0.95f, 0.95f);
                Vector3 pt3 = EvalPlanar3D(sParam, vParam, 1f, 1f, includeFlex: false, out _);
                spawnWorld = new Vector3(pt3.x, 0f, pt3.z);
            }

            // Friendly pass-through spawns ONLY the subtle 1-ring surface ripple (+ shield-confined refraction), zero particles.
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
                    float breakScale = shieldReq.shape == ShieldVfxShape.Dome ? domeRadius : 1f;
                    RimLumenApi.Play(
                        def.breakLumenEffectId,
                        map,
                        new System.Numerics.Vector3(centerXZ.x, 0.1f, centerXZ.z),
                        breakScale,
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
            if (Finished || map != Find.CurrentMap || depthMesh == null || overMesh == null || distortionMesh == null)
            {
                return;
            }

            depthMesh.Clear();
            overMesh.Clear();
            distortionMesh.Clear();

            float chargeRatio = Mathf.Clamp01(currentCharges / (float)maxCharges);
            float lowMix = chargeRatio < 0.45f ? (0.45f - chargeRatio) / 0.45f : 0f;
            Color curPrimary = Color.Lerp(primaryColor, lowChargeColor, lowMix * 0.72f);
            Color curSecondary = Color.Lerp(secondaryColor, lowChargeColor, lowMix * 0.52f);

            float elapsed = SecondsSince(startTick);
            float openU = shieldReq.respawningAfterLoad ? 1f : Mathf.Clamp01(elapsed / def.openSeconds);

            float masterAlpha = 1f;
            float expireHeightScale = 1f;

            if (phase == Phase.Expiring)
            {
                float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.expireSeconds);
                masterAlpha = 1f - u;
                expireHeightScale = Mathf.Max(0.02f, 1f - u * u);
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
                    DrawPlanar3D(curPrimary, curSecondary, breath * masterAlpha, hdr, openU, expireHeightScale);
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

            distortionMesh.Upload();
            if (distortionMesh.HasGeometry)
            {
                LumenShaders.DrawDistortion(distortionMesh.Mesh);
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
            distortionMesh?.Dispose();
            distortionMesh = null;
        }

        // =========================================================================================
        // UNDERDAMPED VIBRATION ENVELOPE (2D Wave Geometric Spreading + Viscous Damping + Ringing)
        // =========================================================================================

        private static float EvaluateVibrationEnvelope(float age, float duration, float dist, float spreadScale)
        {
            float u = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
            float geomDecay = 1f / Mathf.Sqrt(1f + Mathf.Max(0f, dist) / Mathf.Max(0.25f, spreadScale));
            float timeDecay = Mathf.Exp(-1.95f * u) * (1f - u * u);
            float vibRing = 0.84f + 0.16f * Mathf.Cos(age * 24f) * Mathf.Exp(-2.8f * age);
            return geomDecay * timeDecay * vibRing;
        }

        // =========================================================================================
        // PLANAR SHIELD: 3D Cambered Raked Wall + Per-Slice 2.5D Pawn Occlusion + Clamped Ripples
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

        private float EvaluatePlanarAltitude(float rootX, float rootZ)
        {
            float minAlt = def.frontAltitudeY;
            for (int i = 0; i < nearbyPawnZs.Count; i++)
            {
                float dx = Mathf.Abs(nearbyPawnXs[i] - rootX);
                if (dx > 0.72f) continue;
                float pawnZ = nearbyPawnZs[i];
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
                float du = (s - w.sParam) * halfSpan;
                float dh = (v - w.vParam) * def.wallHeightCells;
                float dist = Mathf.Sqrt(du * du + dh * dh);

                if (w.isFriendly)
                {
                    float front = age * def.passWaveSpeed;
                    float env = EvaluateVibrationEnvelope(age, w.durationSeconds, front, def.rippleSpreadScale);
                    float delta = (dist - front) / 0.55f;
                    flex += 0.085f * env * Mathf.Exp(-delta * delta);
                }
                else
                {
                    float front = age * def.rippleWaveSpeed;
                    float env = EvaluateVibrationEnvelope(age, w.durationSeconds, front, def.rippleSpreadScale);
                    float standDent = -0.18f * Mathf.Exp(-age * 4.2f) * Mathf.Exp(-(dist * dist) / 0.65f) * Mathf.Cos(age * 24f);
                    float delta = (dist - front) / 0.62f;
                    float waveFlex = -0.09f * env * Mathf.Exp(-delta * delta);
                    flex += standDent + waveFlex;
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

        private Vector3 ProjectPlanarScreen(float s, float v, float heightScale, float spanScale, bool includeFlex, out float frontPawnMask)
        {
            Vector3 p3 = EvalPlanar3D(s, v, heightScale, spanScale, includeFlex, out Vector2 root);
            float altY = EvaluatePlanarAltitude(root.x, root.y);
            frontPawnMask = Mathf.Clamp01((altY - def.backAltitudeY) / Mathf.Max(0.01f, def.frontAltitudeY - def.backAltitudeY));
            return new Vector3(p3.x, altY, p3.z + p3.y * LumenRibbonMesh.Ortho60HeightZ);
        }

        private void DrawPlanar3D(
            Color curPrimary,
            Color curSecondary,
            float masterAlpha,
            float hdr,
            float openU,
            float expireHeightScale)
        {
            const float spanScale = 1f;

            // Stage 1: Left & Right anchor pylons erect vertically at full span!
            float pylonU = Mathf.Clamp01(openU / def.planarPylonRiseFraction);
            float pylonHeightScale = (1f - (1f - pylonU) * (1f - pylonU) * (1f - pylonU)) * expireHeightScale;
            float pylonAlpha = masterAlpha * Mathf.Clamp01(openU / 0.16f);

            // Stage 2: Entire full-height middle curtain & rails gradually materialize in-place!
            float midU = Mathf.Clamp01((openU - def.planarMiddleStartFraction) / Mathf.Max(0.01f, 1f - def.planarMiddleStartFraction));
            float midAppearAlpha = midU * midU * (3f - 2f * midU);
            float middleAlpha = masterAlpha * midAppearAlpha;
            float midCondenseBoost = Mathf.Sin(midU * Mathf.PI) * 0.24f;
            float heightScale = expireHeightScale;

            int segs = Mathf.Max(24, def.ringSegments);
            float frontFactor = 0.82f + 0.24f * (-normalXZ.z);
            float halfSpan = spanLength * 0.5f * spanScale;

            // 1. Translucent 2-band 3D vertical energy curtain + rails + ribs (materializes in Stage 2!)
            if (middleAlpha > 0.004f)
            {
                for (int i = 0; i < segs; i++)
                {
                    float s0 = -1f + (2f * i) / segs;
                    float s1 = -1f + (2f * (i + 1)) / segs;
                    float sm = 0.5f * (s0 + s1);

                    Vector3 b0 = ProjectPlanarScreen(s0, 0f, heightScale, spanScale, true, out _);
                    Vector3 b1 = ProjectPlanarScreen(s1, 0f, heightScale, spanScale, true, out _);
                    Vector3 m0 = ProjectPlanarScreen(s0, 0.52f, heightScale, spanScale, true, out _);
                    Vector3 m1 = ProjectPlanarScreen(s1, 0.52f, heightScale, spanScale, true, out _);
                    Vector3 t0 = ProjectPlanarScreen(s0, 1f, heightScale, spanScale, true, out _);
                    Vector3 t1 = ProjectPlanarScreen(s1, 1f, heightScale, spanScale, true, out _);

                    float waveGlow = SampleSliceExcitation(sm, halfSpan) + midCondenseBoost;
                    float cAlpha = def.curtainAlpha * middleAlpha * frontFactor;

                    var colBot = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(cAlpha * (1.10f + 0.55f * waveGlow)));
                    var colMid = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(cAlpha * (0.45f + 0.50f * waveGlow)));
                    var colTop = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(cAlpha * (0.90f + 0.55f * waveGlow)));

                    depthMesh.AddShadedQuad(b0, b1, m1, m0, colBot, colMid, hdr * 0.90f);
                    depthMesh.AddShadedQuad(m0, m1, t1, t0, colMid, colTop, hdr * 0.95f);

                    var seamCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01((0.16f + 0.30f * waveGlow) * middleAlpha));
                    depthMesh.AddTaperedSegment(m0, m1, def.coreWidthCells * 0.42f, def.coreWidthCells * 0.42f, seamCol, seamCol, hdr * 0.9f, def.edgePower, null);

                    var railGlow = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01((0.52f + 0.25f * waveGlow) * middleAlpha));
                    var railCore = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.88f * middleAlpha));
                    depthMesh.AddTaperedSegment(b0, b1, def.glowWidthCells * 0.72f, def.glowWidthCells * 0.72f, railGlow, railGlow, hdr * 0.9f, def.edgePower, null);
                    depthMesh.AddTaperedSegment(b0, b1, def.coreWidthCells * 0.85f, def.coreWidthCells * 0.85f, railCore, railCore, hdr * 1.1f, def.edgePower, null);

                    var topGlow = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01((0.48f + 0.25f * waveGlow) * middleAlpha));
                    depthMesh.AddTaperedSegment(t0, t1, def.glowWidthCells * 0.65f, def.glowWidthCells * 0.65f, topGlow, topGlow, hdr * 0.95f, def.edgePower, null);
                    depthMesh.AddTaperedSegment(t0, t1, def.coreWidthCells * 0.82f, def.coreWidthCells * 0.82f, railCore, railCore, hdr * 1.15f, def.edgePower, null);

                    if (i % 6 == 3)
                    {
                        EvalPlanar3D(sm, 0f, heightScale, spanScale, false, out Vector2 tickRoot);
                        float alt = EvaluatePlanarAltitude(tickRoot.x, tickRoot.y);
                        var pRoot = new Vector3(tickRoot.x, alt, tickRoot.y);
                        var pTip = new Vector3(tickRoot.x + normalXZ.x * 0.24f, alt, tickRoot.y + normalXZ.z * 0.24f);
                        var tickCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(0.62f * middleAlpha));
                        depthMesh.AddTaperedSegment(pRoot, pTip, def.coreWidthCells * 0.62f, def.coreWidthCells * 0.24f, tickCol, tickCol, hdr, def.edgePower, null);
                    }

                    if (i % 6 == 0 && i > 0)
                    {
                        var ribCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01((0.38f + 0.32f * waveGlow) * middleAlpha));
                        depthMesh.AddTaperedSegment(b0, t0, def.coreWidthCells * 0.62f, def.coreWidthCells * 0.52f, ribCol, ribCol, hdr, def.edgePower, null);
                    }
                }
            }

            // 2. Left & Right End Anchor Pylons (erect vertically first in Stage 1!)
            for (int e = -1; e <= 1; e += 2)
            {
                EvalPlanar3D(e, 0f, 1f, spanScale, false, out Vector2 outerFoot);
                var innerFoot = new Vector2(outerFoot.x - normalXZ.x * def.topRakeCells, outerFoot.y - normalXZ.z * def.topRakeCells);
                float alt = EvaluatePlanarAltitude(outerFoot.x, outerFoot.y);
                float pylonH = def.wallHeightCells * 1.08f * pylonHeightScale;

                var pOuter = new Vector3(outerFoot.x, alt, outerFoot.y);
                var pInner = new Vector3(innerFoot.x, alt, innerFoot.y);
                var pTop = new Vector3(innerFoot.x, alt, innerFoot.y + pylonH * LumenRibbonMesh.Ortho60HeightZ);

                depthMesh.AddTiltedEllipseRing(
                    pInner, Vector3.right, Vector3.forward, 0.25f, LumenRibbonMesh.Ortho60AspectZ,
                    18, def.coreWidthCells * 0.65f,
                    new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.66f * pylonAlpha)),
                    hdr, def.edgePower, null);

                if (middleAlpha > 0.004f)
                {
                    var bevelCol = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.22f * middleAlpha));
                    depthMesh.AddShadedTriangle(pOuter, pInner, pTop, bevelCol, bevelCol,
                        new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.28f * middleAlpha)), hdr * 0.9f);

                    var slopeCol = new Color(curSecondary.r, curSecondary.g, curSecondary.b, Mathf.Clamp01(0.65f * middleAlpha));
                    depthMesh.AddTaperedSegment(pOuter, pTop, def.coreWidthCells * 0.8f, def.coreWidthCells * 0.55f, slopeCol, slopeCol, hdr, def.edgePower, null);
                }

                if (pylonHeightScale > 0.01f)
                {
                    var colGlow = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.80f * pylonAlpha));
                    var colCore = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.98f * pylonAlpha));
                    depthMesh.AddTaperedSegment(pInner, pTop, def.glowWidthCells * 0.55f, def.glowWidthCells * 0.45f, colGlow, colGlow, hdr, def.edgePower, null);
                    depthMesh.AddTaperedSegment(pInner, pTop, def.coreWidthCells * 1.05f, def.coreWidthCells * 0.85f, colCore, colCore, hdr * 1.25f, def.edgePower, null);

                    float rFin = 0.14f * (1f + 0.26f * Mathf.Sin(pylonU * Mathf.PI));
                    depthMesh.AddShadedQuad(
                        pTop + new Vector3(0f, 0f, -rFin * 1.35f),
                        pTop + new Vector3(rFin * 0.85f, 0f, 0f),
                        pTop + new Vector3(0f, 0f, rFin * 1.35f),
                        pTop + new Vector3(-rFin * 0.85f, 0f, 0f),
                        colCore, colCore, hdr * 1.35f);
                }
            }

            // 3. Strictly shield-surface-clamped 1-2 thin vibration wave filaments + Layer 14 refraction
            if (middleAlpha > 0.004f)
            {
                DrawPlanarSurfaceRipples(curPrimary, curSecondary, middleAlpha, hdr, heightScale, spanScale);
            }
        }

        private float SampleSliceExcitation(float s, float halfSpan)
        {
            float sum = 0f;
            for (int i = 0; i < waves.Count; i++)
            {
                SurfaceWave w = waves[i];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float du = Mathf.Abs(s - w.sParam) * halfSpan;
                float speed = w.isFriendly ? def.passWaveSpeed : def.rippleWaveSpeed;
                float front = age * speed;
                float env = EvaluateVibrationEnvelope(age, w.durationSeconds, front, def.rippleSpreadScale);
                float delta = (du - front) / 0.72f;
                sum += Mathf.Exp(-delta * delta) * env * (w.isFriendly ? 0.35f : 0.78f);
            }

            return Mathf.Min(1.4f, sum);
        }

        private static float PlanarRayExitDistance(float u0, float h0, float dx, float dy, float halfSpan, float H)
        {
            float tx = dx > 1e-5f ? (halfSpan - u0) / dx : (dx < -1e-5f ? (-halfSpan - u0) / dx : 1e6f);
            float ty = dy > 1e-5f ? (H - h0) / dy : (dy < -1e-5f ? -h0 / dy : 1e6f);
            return Mathf.Max(0f, Mathf.Min(tx, ty));
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

            const int GlowSteps = 28;
            const int WaveSteps = 96;

            for (int wIdx = 0; wIdx < waves.Count; wIdx++)
            {
                SurfaceWave w = waves[wIdx];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float uLife = age / w.durationSeconds;

                float u0 = Mathf.Clamp(w.sParam * halfSpan, -halfSpan + 0.02f, halfSpan - 0.02f);
                float h0 = Mathf.Clamp(w.vParam, 0.08f, 0.92f) * H;
                Vector3 centerScreen = ProjectPlanarScreen(u0 / halfSpan, h0 / H, heightScale, spanScale, true, out _);

                // 1. Standing elastic membrane glow strictly clipped inside [-halfSpan, +halfSpan] x [0, H] (hostile impacts only; friendly has ripple only)
                float standEnv = Mathf.Exp(-age * 3.8f) * (0.76f + 0.24f * Mathf.Cos(age * 22f));
                if (!w.isFriendly && standEnv > 0.015f)
                {
                    const float glowR = 0.95f;
                    var innerCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.76f * standEnv * masterAlpha));
                    var outerZero = new Color(curPrimary.r, curPrimary.g, curPrimary.b, 0f);

                    for (int k = 0; k < GlowSteps; k++)
                    {
                        float a0 = (k / (float)GlowSteps) * Mathf.PI * 2f;
                        float a1 = ((k + 1) / (float)GlowSteps) * Mathf.PI * 2f;
                        float dx0 = Mathf.Cos(a0), dy0 = Mathf.Sin(a0);
                        float dx1 = Mathf.Cos(a1), dy1 = Mathf.Sin(a1);
                        float r0 = Mathf.Min(glowR, PlanarRayExitDistance(u0, h0, dx0, dy0, halfSpan, H));
                        float r1 = Mathf.Min(glowR, PlanarRayExitDistance(u0, h0, dx1, dy1, halfSpan, H));
                        if (r0 <= 0.002f && r1 <= 0.002f) continue;

                        Vector3 rim0 = ProjectPlanarScreen((u0 + dx0 * r0) / halfSpan, (h0 + dy0 * r0) / H, heightScale, spanScale, true, out _);
                        Vector3 rim1 = ProjectPlanarScreen((u0 + dx1 * r1) / halfSpan, (h0 + dy1 * r1) / H, heightScale, spanScale, true, out _);
                        depthMesh.AddClippedGlowSector(
                            centerScreen, rim0, rim1,
                            r0 / glowR, r1 / glowR,
                            innerCol, outerZero,
                            hdr * (w.isFriendly ? 0.95f : 1.20f), 1.5f);
                    }
                }

                // 2. Uncrowded 1-2 thin, crisp vibration wave filaments + Layer 14 refraction strictly clamped to the Planar Shield
                int crests = w.isFriendly ? 1 : def.rippleCrestCount;
                float speed = w.isFriendly ? def.passWaveSpeed : def.rippleWaveSpeed;
                float baseDistort = w.isFriendly ? def.passDistortionCells : def.hitDistortionCells;
                const float HorizStretch = 1.32f;

                for (int c = 0; c < crests; c++)
                {
                    float rWave = age * speed - c * def.rippleCrestSpacing;
                    if (rWave <= 0.03f) continue;

                    float env = EvaluateVibrationEnvelope(age, w.durationSeconds, rWave, def.rippleSpreadScale)
                        * (c == 0 ? 1f : 0.62f);
                    if (env <= 0.012f) continue;

                    float bw = def.rippleBandHalfWidth * (w.isFriendly ? 0.85f : (c == 0 ? 1.0f : 1.15f));
                    float rBandStart = rWave - bw;
                    float rIn = Mathf.Max(0f, rBandStart);
                    float rOut = rWave + bw;
                    float invSpan = 1f / Mathf.Max(0.001f, 2f * bw);

                    float bwDist = Mathf.Max(0.18f, bw * 2.1f);
                    float rDistStart = rWave - bwDist;
                    float rDistIn = Mathf.Max(0f, rDistStart);
                    float rDistOut = rWave + bwDist;
                    float invDistSpan = 1f / Mathf.Max(0.001f, 2f * bwDist);

                    Color wakeRgb = w.isFriendly ? curPrimary : (c == 0 ? curPrimary : curSecondary);
                    Color crestRgb = w.isFriendly ? new Color(0.76f, 1f, 0.93f) : (c == 0 ? coreColor : curSecondary);
                    var wakeCol = new Color(wakeRgb.r, wakeRgb.g, wakeRgb.b, Mathf.Clamp01((w.isFriendly ? 0.36f : 0.42f) * env * masterAlpha));
                    var crestCol = new Color(crestRgb.r, crestRgb.g, crestRgb.b, Mathf.Clamp01((w.isFriendly ? 0.78f : 0.96f) * env * masterAlpha));
                    float distortAmp = baseDistort * env * masterAlpha;

                    float bwSpine = bw * 0.34f;
                    float rSpineStart = rWave - bwSpine;
                    float rSpineIn = Mathf.Max(0f, rSpineStart);
                    float rSpineOut = rWave + bwSpine;
                    float invSpineSpan = 1f / Mathf.Max(0.001f, 2f * bwSpine);

                    for (int k = 0; k < WaveSteps; k++)
                    {
                        float a0 = (k / (float)WaveSteps) * Mathf.PI * 2f;
                        float a1 = ((k + 1) / (float)WaveSteps) * Mathf.PI * 2f;
                        float dx0 = Mathf.Cos(a0) * HorizStretch, dy0 = Mathf.Sin(a0);
                        float dx1 = Mathf.Cos(a1) * HorizStretch, dy1 = Mathf.Sin(a1);

                        float rb0 = PlanarRayExitDistance(u0, h0, dx0, dy0, halfSpan, H);
                        float rb1 = PlanarRayExitDistance(u0, h0, dx1, dy1, halfSpan, H);

                        if (distortAmp > 0.0005f && (rDistIn < rb0 || rDistIn < rb1))
                        {
                            float da0 = Mathf.Min(rDistIn, rb0), db0 = Mathf.Min(rDistOut, rb0);
                            float da1 = Mathf.Min(rDistIn, rb1), db1 = Mathf.Min(rDistOut, rb1);
                            if (db0 - da0 > 0.002f || db1 - da1 > 0.002f)
                            {
                                Vector3 din0 = ProjectPlanarScreen((u0 + dx0 * da0) / halfSpan, (h0 + dy0 * da0) / H, heightScale, spanScale, true, out float maskIn0);
                                Vector3 dout0 = ProjectPlanarScreen((u0 + dx0 * db0) / halfSpan, (h0 + dy0 * db0) / H, heightScale, spanScale, true, out float maskOut0);
                                Vector3 din1 = ProjectPlanarScreen((u0 + dx1 * da1) / halfSpan, (h0 + dy1 * da1) / H, heightScale, spanScale, true, out float maskIn1);
                                Vector3 dout1 = ProjectPlanarScreen((u0 + dx1 * db1) / halfSpan, (h0 + dy1 * db1) / H, heightScale, spanScale, true, out float maskOut1);

                                Vector2 dir0 = SafeOutwardXZ(din0, dout0);
                                Vector2 dir1 = SafeOutwardXZ(din1, dout1);
                                distortionMesh.AddDistortionBandQuad(
                                    din0, dout0, dout1, din1,
                                    Mathf.Clamp01((da0 - rDistStart) * invDistSpan),
                                    Mathf.Clamp01((db0 - rDistStart) * invDistSpan),
                                    Mathf.Clamp01((db1 - rDistStart) * invDistSpan),
                                    Mathf.Clamp01((da1 - rDistStart) * invDistSpan),
                                    -dir0 * distortAmp, -dir1 * distortAmp,
                                    0.5f * (maskIn0 + maskOut0), 0.5f * (maskIn1 + maskOut1));
                            }
                        }

                        if (rIn >= rb0 && rIn >= rb1) continue;

                        float ra0 = Mathf.Min(rIn, rb0), rbOut0 = Mathf.Min(rOut, rb0);
                        float ra1 = Mathf.Min(rIn, rb1), rbOut1 = Mathf.Min(rOut, rb1);
                        if (rbOut0 - ra0 <= 0.002f && rbOut1 - ra1 <= 0.002f) continue;

                        Vector3 in0 = ProjectPlanarScreen((u0 + dx0 * ra0) / halfSpan, (h0 + dy0 * ra0) / H, heightScale, spanScale, true, out _);
                        Vector3 out0 = ProjectPlanarScreen((u0 + dx0 * rbOut0) / halfSpan, (h0 + dy0 * rbOut0) / H, heightScale, spanScale, true, out _);
                        Vector3 in1 = ProjectPlanarScreen((u0 + dx1 * ra1) / halfSpan, (h0 + dy1 * ra1) / H, heightScale, spanScale, true, out _);
                        Vector3 out1 = ProjectPlanarScreen((u0 + dx1 * rbOut1) / halfSpan, (h0 + dy1 * rbOut1) / H, heightScale, spanScale, true, out _);

                        depthMesh.AddWaveBandQuad(
                            in0, out0, out1, in1,
                            Mathf.Clamp01((ra0 - rBandStart) * invSpan),
                            Mathf.Clamp01((rbOut0 - rBandStart) * invSpan),
                            Mathf.Clamp01((rbOut1 - rBandStart) * invSpan),
                            Mathf.Clamp01((ra1 - rBandStart) * invSpan),
                            wakeCol, hdr * (w.isFriendly ? 1.0f : 1.15f), 1.45f);

                        if (rSpineIn < rb0 || rSpineIn < rb1)
                        {
                            float sa0 = Mathf.Min(rSpineIn, rb0), sb0 = Mathf.Min(rSpineOut, rb0);
                            float sa1 = Mathf.Min(rSpineIn, rb1), sb1 = Mathf.Min(rSpineOut, rb1);
                            if (sb0 - sa0 > 0.0015f || sb1 - sa1 > 0.0015f)
                            {
                                Vector3 sin0 = ProjectPlanarScreen((u0 + dx0 * sa0) / halfSpan, (h0 + dy0 * sa0) / H, heightScale, spanScale, true, out _);
                                Vector3 sout0 = ProjectPlanarScreen((u0 + dx0 * sb0) / halfSpan, (h0 + dy0 * sb0) / H, heightScale, spanScale, true, out _);
                                Vector3 sin1 = ProjectPlanarScreen((u0 + dx1 * sa1) / halfSpan, (h0 + dy1 * sa1) / H, heightScale, spanScale, true, out _);
                                Vector3 sout1 = ProjectPlanarScreen((u0 + dx1 * sb1) / halfSpan, (h0 + dy1 * sb1) / H, heightScale, spanScale, true, out _);

                                depthMesh.AddWaveBandQuad(
                                    sin0, sout0, sout1, sin1,
                                    Mathf.Clamp01((sa0 - rSpineStart) * invSpineSpan),
                                    Mathf.Clamp01((sb0 - rSpineStart) * invSpineSpan),
                                    Mathf.Clamp01((sb1 - rSpineStart) * invSpineSpan),
                                    Mathf.Clamp01((sa1 - rSpineStart) * invSpineSpan),
                                    crestCol, hdr * 1.40f, 1.9f);
                            }
                        }
                    }

                    // Top & Bottom Rail Guided Shock Pulses (strictly clamped to [-halfSpan, +halfSpan])
                    if (!w.isFriendly && c == 0)
                    {
                        for (int rv = 0; rv <= 1; rv++)
                        {
                            float dyRail = Mathf.Abs(rv * H - h0);
                            if (rWave <= dyRail) continue;
                            float duHit = Mathf.Sqrt(rWave * rWave - dyRail * dyRail) * HorizStretch;
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                float uHit = u0 + sign * duHit;
                                if (Mathf.Abs(uHit) > halfSpan) continue;
                                float sHit = uHit / halfSpan;
                                Vector3 pA = ProjectPlanarScreen(Mathf.Clamp(sHit - 0.09f, -1f, 1f), rv, heightScale, spanScale, true, out _);
                                Vector3 pB = ProjectPlanarScreen(Mathf.Clamp(sHit + 0.09f, -1f, 1f), rv, heightScale, spanScale, true, out _);
                                var pulseCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.94f * env * masterAlpha));
                                depthMesh.AddTaperedSegment(pA, pB, def.coreWidthCells * 1.35f, def.coreWidthCells * 1.35f, pulseCol, pulseCol, hdr * 1.30f, def.edgePower, null);
                            }
                        }
                    }
                }
            }
        }

        // =========================================================================================
        // GROVE SHIELD DOME: Breathable Perimeter + Lenticular Canopy + Far-Sweeping 3D Ripples
        // =========================================================================================

        private Vector3 ProjectDomeHemisphere(Vector3 q)
        {
            // Lenticular canopy height H * q.y^2 (with H < domeRadius) guarantees every point on the upper
            // hemisphere projects strictly inside the R = domeRadius perimeter circle, even on the North shoulder.
            float qyClamped = Mathf.Max(0f, q.y);
            float wy = def.domeCanopyHeightCells * qyClamped * qyClamped;
            return new Vector3(
                centerXZ.x + domeRadius * q.x,
                def.frontAltitudeY,
                centerXZ.z + domeRadius * q.z + wy * LumenRibbonMesh.Ortho60HeightZ);
        }

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
            float innerSafeRadius = Mathf.Max(0.5f, domeRadius - def.glowWidthCells * 0.5f);

            // 0. Dome Opening Wave (strictly clamped <= domeRadius on both Layer 15 HDR and Layer 14 Distortion!)
            if (!shieldReq.respawningAfterLoad && elapsed < def.openSeconds)
            {
                float openU = Mathf.Clamp01(elapsed / def.openSeconds);
                float openFade = 1f - openU;
                float rOpen = domeRadius * (1f - (1f - openU) * (1f - openU));
                float bwOpen = 0.65f;
                float rIn = Mathf.Max(0f, rOpen - bwOpen);
                float rOut = Mathf.Min(domeRadius, rOpen + bwOpen);
                if (rOut - rIn > 0.01f)
                {
                    float uIn = Mathf.Clamp01((rIn - (rOpen - bwOpen)) / (2f * bwOpen));
                    float uOut = Mathf.Clamp01((rOut - (rOpen - bwOpen)) / (2f * bwOpen));
                    var openCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.55f * openFade * masterAlpha));
                    float openDistort = def.hitDistortionCells * 0.75f * openFade * masterAlpha;
                    int segs = def.ringSegments;
                    for (int i = 0; i < segs; i++)
                    {
                        float a0 = (i / (float)segs) * Mathf.PI * 2f;
                        float a1 = ((i + 1) / (float)segs) * Mathf.PI * 2f;
                        var dir0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                        var dir1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                        var pIn0 = new Vector3(centerXZ.x + dir0.x * rIn, def.frontAltitudeY, centerXZ.z + dir0.y * rIn);
                        var pOut0 = new Vector3(centerXZ.x + dir0.x * rOut, def.frontAltitudeY, centerXZ.z + dir0.y * rOut);
                        var pIn1 = new Vector3(centerXZ.x + dir1.x * rIn, def.frontAltitudeY, centerXZ.z + dir1.y * rIn);
                        var pOut1 = new Vector3(centerXZ.x + dir1.x * rOut, def.frontAltitudeY, centerXZ.z + dir1.y * rOut);

                        overMesh.AddWaveBandQuad(pIn0, pOut0, pOut1, pIn1, uIn, uOut, uOut, uIn, openCol, hdr, 1.6f);
                        distortionMesh.AddDistortionBandQuad(
                            pIn0, pOut0, pOut1, pIn1,
                            uIn, uOut, uOut, uIn,
                            -dir0 * openDistort, -dir1 * openDistort,
                            1f, 1f);
                    }
                }
            }

            // 1. Clean double perimeter ward ring (outer edge flush at domeRadius so glow stays inside R = domeRadius)
            var glowCol = new Color(curPrimary.r, curPrimary.g, curPrimary.b, Mathf.Clamp01(0.28f * breath * masterAlpha));
            var coreCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.78f * breath * masterAlpha));
            overMesh.AddTiltedEllipseRing(
                domeOrigin, Vector3.right, Vector3.forward, innerSafeRadius, 1f,
                def.ringSegments, def.glowWidthCells, glowCol, hdr * 0.85f, def.edgePower, null);
            overMesh.AddTiltedEllipseRing(
                domeOrigin, Vector3.right, Vector3.forward, domeRadius - def.coreWidthCells * 0.5f, 1f,
                def.ringSegments, def.coreWidthCells, coreCol, hdr, def.edgePower, null);

            // 2. Counter-rotating Elven Rune Arcs
            if (def.domeRuneArcCount > 0)
            {
                overMesh.AddSegmentedEllipseArcs(
                    domeOrigin,
                    Mathf.Max(0.5f, domeRadius - 0.28f),
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

            // 3. Subtle 3D Canopy Ribs
            float airExcite = (!shieldReq.respawningAfterLoad && elapsed < def.openSeconds)
                ? (1f - elapsed / def.openSeconds) * 0.7f
                : 0f;
            for (int i = 0; i < waves.Count; i++)
            {
                float age = SecondsSince(waves[i].birthTick);
                if (age >= 0f && age < waves[i].durationSeconds)
                {
                    float weight = waves[i].isAir ? 0.72f : 0.32f;
                    airExcite = Mathf.Max(airExcite, (1f - age / waves[i].durationSeconds) * weight);
                }
            }

            float ribAlpha = (0.075f + 0.24f * airExcite) * masterAlpha;
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
                        float wy = def.domeCanopyHeightCells * Mathf.Max(0f, 1f - u * u);
                        float r = u * (domeRadius - 0.08f);
                        polylineBuf.Add(new Vector3(
                            centerXZ.x + cosP * r,
                            def.frontAltitudeY,
                            centerXZ.z + sinP * r + wy * LumenRibbonMesh.Ortho60HeightZ));
                    }

                    overMesh.AddPolyline(polylineBuf, polylineBuf.Count, def.coreWidthCells * 0.56f, ribCol, hdr * 0.85f, def.edgePower, true, null);
                }
            }

            // 4. 1-2 Wide, Far-Sweeping 3D Spherical Geodesic Ripples + Strictly Dome-Confined Layer 14 Distortion!
            DrawDomeGeodesicRipples(curPrimary, curSecondary, masterAlpha, hdr);
        }

        private static float SphericalHorizonGamma(Vector3 u0, Vector3 tDir)
        {
            return Mathf.Clamp(Mathf.Atan2(u0.y, -tDir.y), 0f, Mathf.PI * 0.94f);
        }

        private void DrawDomeGeodesicRipples(Color curPrimary, Color curSecondary, float masterAlpha, float hdr)
        {
            if (waves.Count == 0)
            {
                return;
            }

            const int GlowSteps = 28;
            const int WaveSteps = 112;

            for (int wIdx = 0; wIdx < waves.Count; wIdx++)
            {
                SurfaceWave w = waves[wIdx];
                float age = SecondsSince(w.birthTick);
                if (age < 0f || age >= w.durationSeconds) continue;
                float uLife = age / w.durationSeconds;

                float ux = (w.worldPos.x - centerXZ.x) / domeRadius;
                float uz = (w.worldPos.z - centerXZ.z) / domeRadius;
                float rHoriz = Mathf.Sqrt(ux * ux + uz * uz);
                if (rHoriz > 1f)
                {
                    ux /= rHoriz;
                    uz /= rHoriz;
                }

                // Flat-shot rim hits sit directly at ground rim (uy ~ 0.004) so VFX aligns 1:1 with Projectile.ExactPosition!
                float uy = w.isAir ? Mathf.Sqrt(Mathf.Max(0.04f, 1f - ux * ux - uz * uz)) : 0.004f;
                Vector3 u0 = new Vector3(ux, uy, uz).normalized;

                var e1 = new Vector3(-u0.z, 0f, u0.x);
                if (e1.sqrMagnitude < 1e-5f) e1 = Vector3.right;
                else e1.Normalize();
                Vector3 e2 = Vector3.Cross(u0, e1).normalized;

                Vector3 hitScreen = ProjectDomeHemisphere(u0);

                // 1. Standing vibration bloom analytically horizon-clamped inside R = domeRadius (hostile impacts only)
                float standEnv = Mathf.Exp(-age * 3.8f) * (0.76f + 0.24f * Mathf.Cos(age * 22f));
                if (!w.isFriendly && standEnv > 0.015f)
                {
                    float glowArc = w.isAir ? 2.25f : 1.55f;
                    float gammaGlow = glowArc / domeRadius;
                    var innerCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.76f * standEnv * masterAlpha));
                    var outerZero = new Color(curPrimary.r, curPrimary.g, curPrimary.b, 0f);

                    for (int k = 0; k < GlowSteps; k++)
                    {
                        float p0 = (k / (float)GlowSteps) * Mathf.PI * 2f;
                        float p1 = ((k + 1) / (float)GlowSteps) * Mathf.PI * 2f;
                        Vector3 t0 = e1 * Mathf.Cos(p0) + e2 * Mathf.Sin(p0);
                        Vector3 t1 = e1 * Mathf.Cos(p1) + e2 * Mathf.Sin(p1);
                        float g0 = Mathf.Min(gammaGlow, SphericalHorizonGamma(u0, t0));
                        float g1 = Mathf.Min(gammaGlow, SphericalHorizonGamma(u0, t1));
                        if (g0 <= 0.001f && g1 <= 0.001f) continue;

                        Vector3 rim0 = ProjectDomeHemisphere(u0 * Mathf.Cos(g0) + t0 * Mathf.Sin(g0));
                        Vector3 rim1 = ProjectDomeHemisphere(u0 * Mathf.Cos(g1) + t1 * Mathf.Sin(g1));
                        overMesh.AddClippedGlowSector(
                            hitScreen, rim0, rim1,
                            g0 / gammaGlow, g1 / gammaGlow,
                            innerCol, outerZero,
                            hdr * 1.20f, 1.5f);
                    }
                }

                // 1B. Localized 3D Spherical Hexagonal Honeycomb Ward Lattice (hostile impacts only; anchored at u0!)
                if (!w.isFriendly && def.domeHexRings > 0)
                {
                    DrawDomeHexLattice(w, u0, e1, e2, age, curPrimary, curSecondary, masterAlpha, hdr);
                }

                // 2. Uncrowded 1-2 Thin, Crisp Spherical Wave Filaments + Layer 14 Refraction Strictly Inside R = domeRadius
                int crests = w.isFriendly ? 1 : def.rippleCrestCount;
                float speed = w.isFriendly ? def.passWaveSpeed : def.rippleWaveSpeed;
                float baseDistort = w.isFriendly ? def.passDistortionCells : def.hitDistortionCells;

                for (int c = 0; c < crests; c++)
                {
                    float arcDist = age * speed - c * def.rippleCrestSpacing;
                    if (arcDist <= 0.08f) continue;
                    float gammaWave = arcDist / domeRadius;
                    if (gammaWave >= Mathf.PI * 0.88f) continue;

                    float env = EvaluateVibrationEnvelope(age, w.durationSeconds, arcDist, def.rippleSpreadScale)
                        * (c == 0 ? 1f : 0.56f);
                    if (env <= 0.012f) continue;

                    float bwCells = def.rippleBandHalfWidth * (w.isFriendly ? 0.85f : (c == 0 ? 1.0f : 1.18f));
                    float dGamma = bwCells / domeRadius;
                    float gBandStart = gammaWave - dGamma;
                    float gIn = Mathf.Max(0f, gBandStart);
                    float gOut = gammaWave + dGamma;
                    float invGSpan = 1f / Mathf.Max(1e-4f, 2f * dGamma);

                    float dGammaDist = Mathf.Max(0.26f, bwCells * 1.9f) / domeRadius;
                    float gDistStart = gammaWave - dGammaDist;
                    float gDistIn = Mathf.Max(0f, gDistStart);
                    float gDistOut = gammaWave + dGammaDist;
                    float invGDistSpan = 1f / Mathf.Max(1e-4f, 2f * dGammaDist);

                    Color wakeRgb = w.isFriendly ? curPrimary : (c == 0 ? curPrimary : curSecondary);
                    Color crestRgb = w.isFriendly ? new Color(0.76f, 1f, 0.92f) : (c == 0 ? coreColor : curSecondary);
                    var wakeCol = new Color(wakeRgb.r, wakeRgb.g, wakeRgb.b, Mathf.Clamp01((w.isFriendly ? 0.34f : 0.38f) * env * masterAlpha));
                    var crestCol = new Color(crestRgb.r, crestRgb.g, crestRgb.b, Mathf.Clamp01((w.isFriendly ? 0.76f : 0.94f) * env * masterAlpha));
                    float distortAmp = baseDistort * env * masterAlpha;

                    float dGammaSpine = dGamma * 0.34f;
                    float gSpineStart = gammaWave - dGammaSpine;
                    float gSpineIn = Mathf.Max(0f, gSpineStart);
                    float gSpineOut = gammaWave + dGammaSpine;
                    float invGSpineSpan = 1f / Mathf.Max(1e-4f, 2f * dGammaSpine);

                    for (int k = 0; k < WaveSteps; k++)
                    {
                        float p0 = (k / (float)WaveSteps) * Mathf.PI * 2f;
                        float p1 = ((k + 1) / (float)WaveSteps) * Mathf.PI * 2f;
                        Vector3 t0 = e1 * Mathf.Cos(p0) + e2 * Mathf.Sin(p0);
                        Vector3 t1 = e1 * Mathf.Cos(p1) + e2 * Mathf.Sin(p1);

                        float gb0 = SphericalHorizonGamma(u0, t0);
                        float gb1 = SphericalHorizonGamma(u0, t1);

                        if (distortAmp > 0.0005f && (gDistIn < gb0 || gDistIn < gb1))
                        {
                            float da0 = Mathf.Min(gDistIn, gb0), db0 = Mathf.Min(gDistOut, gb0);
                            float da1 = Mathf.Min(gDistIn, gb1), db1 = Mathf.Min(gDistOut, gb1);
                            if (db0 - da0 > 0.0004f || db1 - da1 > 0.0004f)
                            {
                                Vector3 din0 = ProjectDomeHemisphere(u0 * Mathf.Cos(da0) + t0 * Mathf.Sin(da0));
                                Vector3 dout0 = ProjectDomeHemisphere(u0 * Mathf.Cos(db0) + t0 * Mathf.Sin(db0));
                                Vector3 din1 = ProjectDomeHemisphere(u0 * Mathf.Cos(da1) + t1 * Mathf.Sin(da1));
                                Vector3 dout1 = ProjectDomeHemisphere(u0 * Mathf.Cos(db1) + t1 * Mathf.Sin(db1));

                                Vector2 dir0 = SafeOutwardXZ(din0, dout0);
                                Vector2 dir1 = SafeOutwardXZ(din1, dout1);
                                distortionMesh.AddDistortionBandQuad(
                                    din0, dout0, dout1, din1,
                                    Mathf.Clamp01((da0 - gDistStart) * invGDistSpan),
                                    Mathf.Clamp01((db0 - gDistStart) * invGDistSpan),
                                    Mathf.Clamp01((db1 - gDistStart) * invGDistSpan),
                                    Mathf.Clamp01((da1 - gDistStart) * invGDistSpan),
                                    -dir0 * distortAmp, -dir1 * distortAmp,
                                    1f, 1f);
                            }
                        }

                        if (gIn >= gb0 && gIn >= gb1) continue;

                        float ga0 = Mathf.Min(gIn, gb0), gbOut0 = Mathf.Min(gOut, gb0);
                        float ga1 = Mathf.Min(gIn, gb1), gbOut1 = Mathf.Min(gOut, gb1);
                        if (gbOut0 - ga0 <= 0.0004f && gbOut1 - ga1 <= 0.0004f) continue;

                        Vector3 in0 = ProjectDomeHemisphere(u0 * Mathf.Cos(ga0) + t0 * Mathf.Sin(ga0));
                        Vector3 out0 = ProjectDomeHemisphere(u0 * Mathf.Cos(gbOut0) + t0 * Mathf.Sin(gbOut0));
                        Vector3 in1 = ProjectDomeHemisphere(u0 * Mathf.Cos(ga1) + t1 * Mathf.Sin(ga1));
                        Vector3 out1 = ProjectDomeHemisphere(u0 * Mathf.Cos(gbOut1) + t1 * Mathf.Sin(gbOut1));

                        overMesh.AddWaveBandQuad(
                            in0, out0, out1, in1,
                            Mathf.Clamp01((ga0 - gBandStart) * invGSpan),
                            Mathf.Clamp01((gbOut0 - gBandStart) * invGSpan),
                            Mathf.Clamp01((gbOut1 - gBandStart) * invGSpan),
                            Mathf.Clamp01((ga1 - gBandStart) * invGSpan),
                            wakeCol, hdr * (w.isFriendly ? 1.0f : 1.12f), 1.45f);

                        if (gSpineIn < gb0 || gSpineIn < gb1)
                        {
                            float sa0 = Mathf.Min(gSpineIn, gb0), sb0 = Mathf.Min(gSpineOut, gb0);
                            float sa1 = Mathf.Min(gSpineIn, gb1), sb1 = Mathf.Min(gSpineOut, gb1);
                            if (sb0 - sa0 > 0.0003f || sb1 - sa1 > 0.0003f)
                            {
                                Vector3 sin0 = ProjectDomeHemisphere(u0 * Mathf.Cos(sa0) + t0 * Mathf.Sin(sa0));
                                Vector3 sout0 = ProjectDomeHemisphere(u0 * Mathf.Cos(sb0) + t0 * Mathf.Sin(sb0));
                                Vector3 sin1 = ProjectDomeHemisphere(u0 * Mathf.Cos(sa1) + t1 * Mathf.Sin(sa1));
                                Vector3 sout1 = ProjectDomeHemisphere(u0 * Mathf.Cos(sb1) + t1 * Mathf.Sin(sb1));

                                overMesh.AddWaveBandQuad(
                                    sin0, sout0, sout1, sin1,
                                    Mathf.Clamp01((sa0 - gSpineStart) * invGSpineSpan),
                                    Mathf.Clamp01((sb0 - gSpineStart) * invGSpineSpan),
                                    Mathf.Clamp01((sb1 - gSpineStart) * invGSpineSpan),
                                    Mathf.Clamp01((sa1 - gSpineStart) * invGSpineSpan),
                                    crestCol, hdr * 1.38f, 1.9f);
                            }
                        }
                    }

                    // Perimeter Rim Guided Shock Pulses originating right from the rim contact point!
                    float sinG = Mathf.Sin(gammaWave);
                    if (u0.y < sinG && !w.isFriendly && c == 0)
                    {
                        float hLen = Mathf.Max(0.05f, Mathf.Sqrt(u0.x * u0.x + u0.z * u0.z));
                        float baseAng = Mathf.Atan2(u0.z, u0.x);
                        float dAng = Mathf.Acos(Mathf.Clamp(Mathf.Cos(gammaWave) / hLen, -1f, 1f));
                        var pulseCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(0.92f * env * masterAlpha));
                        float rimR = domeRadius - def.coreWidthCells * 0.5f;

                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            float aMid = baseAng + sign * dAng;
                            var pA = new Vector3(centerXZ.x + Mathf.Cos(aMid - 0.055f) * rimR, def.frontAltitudeY, centerXZ.z + Mathf.Sin(aMid - 0.055f) * rimR);
                            var pB = new Vector3(centerXZ.x + Mathf.Cos(aMid + 0.055f) * rimR, def.frontAltitudeY, centerXZ.z + Mathf.Sin(aMid + 0.055f) * rimR);
                            overMesh.AddTaperedSegment(pA, pB, def.coreWidthCells * 1.65f, def.coreWidthCells * 1.65f, pulseCol, pulseCol, hdr * 1.25f, def.edgePower, null);
                        }
                    }
                }
            }
        }

        private void DrawDomeHexLattice(
            in SurfaceWave w,
            Vector3 hU0,
            Vector3 he1,
            Vector3 he2,
            float age,
            Color curPrimary,
            Color curSecondary,
            float masterAlpha,
            float hdr)
        {
            float rWave0 = age * def.rippleWaveSpeed;
            float hexSize = def.domeHexSizeCells;
            float rCorner = hexSize * 0.92f;
            int hexRange = def.domeHexRings;

            for (int hq = -hexRange; hq <= hexRange; hq++)
            {
                for (int hr = -hexRange; hr <= hexRange; hr++)
                {
                    if (Mathf.Abs(hq + hr) > hexRange) continue;

                    float cx = hexSize * 1.7320508f * (hq + hr * 0.5f);
                    float cy = hexSize * 1.5f * hr;
                    float hDist = Mathf.Sqrt(cx * cx + cy * cy);

                    float impactExcite = Mathf.Exp(-age * 2.8f) * Mathf.Exp(-(hDist * hDist) / (w.isAir ? 5.2f : 4.2f));
                    float delta = (hDist - rWave0) / 0.58f;
                    float waveExcite = EvaluateVibrationEnvelope(age, w.durationSeconds, rWave0, def.rippleSpreadScale)
                        * Mathf.Exp(-delta * delta)
                        * Mathf.Exp(-hDist * (w.isAir ? 0.18f : 0.22f));
                    float hexAlpha = Mathf.Min(0.94f, impactExcite * 0.90f + waveExcite * 0.78f) * masterAlpha;
                    if (hexAlpha < 0.025f) continue;

                    bool anyInside = false;
                    bool allInside = true;
                    for (int v = 0; v < 6; v++)
                    {
                        float ang = (v * Mathf.PI) / 3f + Mathf.PI / 6f;
                        float vx = cx + Mathf.Cos(ang) * rCorner;
                        float vy = cy + Mathf.Sin(ang) * rCorner;
                        float d = Mathf.Sqrt(vx * vx + vy * vy);
                        Vector3 q;
                        if (d > 1e-4f)
                        {
                            float gAng = d / domeRadius;
                            Vector3 tDir = (he1 * vx + he2 * vy) / d;
                            q = hU0 * Mathf.Cos(gAng) + tDir * Mathf.Sin(gAng);
                        }
                        else
                        {
                            q = hU0;
                        }

                        hexUnitQ[v] = q;
                        if (q.y >= 0.002f) anyInside = true;
                        if (q.y < 0f) allInside = false;
                        else hexCorners[v] = ProjectDomeHemisphere(q);
                    }

                    if (!anyInside) continue;

                    bool isCenter = hq == 0 && hr == 0;
                    if (allInside && hexAlpha > 0.18f)
                    {
                        Vector3 hCenter = (hexCorners[0] + hexCorners[3]) * 0.5f;
                        Color fillRgb = isCenter ? coreColor : curPrimary;
                        var fillCol = new Color(fillRgb.r, fillRgb.g, fillRgb.b, Mathf.Clamp01(hexAlpha * 0.14f));
                        for (int v = 0; v < 6; v++)
                        {
                            overMesh.AddShadedTriangle(hCenter, hexCorners[v], hexCorners[(v + 1) % 6], fillCol, fillCol, fillCol, hdr * 0.95f);
                        }
                    }

                    Color lineRgb = isCenter ? coreColor : curSecondary;
                    var lineCol = new Color(lineRgb.r, lineRgb.g, lineRgb.b, Mathf.Clamp01(hexAlpha));
                    float lineW = def.coreWidthCells * (isCenter ? 0.48f : 0.36f);
                    for (int v = 0; v < 6; v++)
                    {
                        Vector3 qA = hexUnitQ[v];
                        Vector3 qB = hexUnitQ[(v + 1) % 6];
                        if (qA.y < 0f && qB.y < 0f) continue;

                        Vector3 pA = qA.y >= 0f ? hexCorners[v] : ClipToDomeRim(qB, qA);
                        Vector3 pB = qB.y >= 0f ? hexCorners[(v + 1) % 6] : ClipToDomeRim(qA, qB);
                        overMesh.AddTaperedSegment(pA, pB, lineW, lineW, lineCol, lineCol, hdr * 1.18f, def.edgePower, null);
                    }
                }
            }
        }

        private Vector3 ClipToDomeRim(Vector3 insideQ, Vector3 outsideQ)
        {
            float denom = insideQ.y - outsideQ.y;
            float t = denom > 1e-6f ? Mathf.Clamp01(insideQ.y / denom) : 0f;
            float rx = insideQ.x + (outsideQ.x - insideQ.x) * t;
            float rz = insideQ.z + (outsideQ.z - insideQ.z) * t;
            float len = Mathf.Sqrt(rx * rx + rz * rz);
            if (len > 1e-5f)
            {
                rx /= len;
                rz /= len;
            }

            return new Vector3(centerXZ.x + domeRadius * rx, def.frontAltitudeY, centerXZ.z + domeRadius * rz);
        }

        private static Vector2 SafeOutwardXZ(Vector3 innerPt, Vector3 outerPt)
        {
            float dx = outerPt.x - innerPt.x;
            float dz = outerPt.z - innerPt.z;
            float lenSq = dx * dx + dz * dz;
            if (lenSq <= 1e-8f)
            {
                return Vector2.zero;
            }

            float invLen = 1f / Mathf.Sqrt(lenSq);
            return new Vector2(dx * invLen, dz * invLen);
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
