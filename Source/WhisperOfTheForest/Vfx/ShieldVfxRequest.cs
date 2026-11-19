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
        public float hdrIntensity;
        public float altitudeY;
        public float coreWidthCells;
        public float glowWidthCells;
        public float edgePower;
        public float pulseHz;
        public float pulseMinAlpha;
        public float pulseMaxAlpha;
        public int ringSegments;
        public float hitFlashSeconds;
        public float hitFlashRadiusCells;
        public float breakSeconds;
        public float expireSeconds;

        public string sustainLumenEffectId;
        public string hitLumenEffectId;
        public SoundDef activateSound;
        public SoundDef hitSound;
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

            if (!VfxColors.IsValid(coreColor))
            {
                yield return "coreColor must be " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(primaryColor))
            {
                yield return "primaryColor must be " + VfxColors.Format;
            }

            if (hdrIntensity <= 0f)
            {
                yield return "hdrIntensity must be positive";
            }

            if (coreWidthCells <= 0f || glowWidthCells <= 0f)
            {
                yield return "coreWidthCells and glowWidthCells must be positive";
            }

            if (edgePower <= 0f)
            {
                yield return "edgePower must be positive";
            }

            if (ringSegments < 12)
            {
                yield return "ringSegments must be at least 12";
            }

            if (hitFlashSeconds <= 0f || hitFlashRadiusCells <= 0f)
            {
                yield return "hitFlashSeconds and hitFlashRadiusCells must be positive";
            }

            if (breakSeconds <= 0f || expireSeconds <= 0f)
            {
                yield return "breakSeconds and expireSeconds must be positive";
            }
        }
    }

    /// <summary>
    /// RimLumen Layer 15 + optional EffectAsset worker for both Dome and Planar shields.
    /// Detaches cleanly on <c>Trigger(Break)</c> or <c>Trigger(Expire)</c> so shatter/fade tails finish
    /// playing after the gameplay shield Thing destroys itself on the same tick.
    /// </summary>
    public sealed class ShieldLumenVfxWorker : VfxWorker
    {
        private enum Phase
        {
            Sustaining,
            Breaking,
            Expiring
        }

        private struct HitPulse
        {
            public Vector3 worldPos;
            public int tick;
        }

        private const int MaxActivePulses = 8;

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
        private Vector3 centerXZ;
        private Vector3 normalXZ;
        private Vector3 tangentXZ;
        private readonly Vector3[] linePoints = new Vector3[2];
        private readonly List<HitPulse> pulses = new List<HitPulse>(MaxActivePulses);

        private LumenRibbonMesh mesh;
        private ILumenEffectHandle lumenHandle;

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

            coreColor = VfxColors.ParseUnity(def.coreColor);
            primaryColor = VfxColors.ParseUnity(def.primaryColor);
            if (shieldReq.theme != null)
            {
                coreColor = shieldReq.theme.CoreColorUnity;
                primaryColor = shieldReq.theme.PrimaryColorUnity;
            }

            centerXZ = new Vector3(shieldReq.origin.x, def.altitudeY, shieldReq.origin.z);
            Vector3 dir = new Vector3(shieldReq.direction.x, 0f, shieldReq.direction.z);
            normalXZ = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            tangentXZ = new Vector3(-normalXZ.z, 0f, normalXZ.x);

            mesh = new LumenRibbonMesh("WotF_Shield_" + def.defName);
            StartSustainLumen();

            if (!shieldReq.respawningAfterLoad && map == Find.CurrentMap && def.activateSound != null)
            {
                def.activateSound.PlayOneShot(new TargetInfo(centerXZ.ToIntVec3(), map));
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

            if (pulses.Count >= MaxActivePulses)
            {
                pulses.RemoveAt(0);
            }

            pulses.Add(new HitPulse
            {
                worldPos = new Vector3(hit.HitWorldPos.x, def.altitudeY, hit.HitWorldPos.z),
                tick = Find.TickManager.TicksGame
            });

            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                lumenHandle.SetParam("chargeRatio", currentCharges / (float)maxCharges);
            }

            if (RimLumenApi.IsReady && !def.hitLumenEffectId.NullOrEmpty())
            {
                float rot = Mathf.Atan2(hit.IncomingDirectionXZ.x, hit.IncomingDirectionXZ.z) * Mathf.Rad2Deg;
                RimLumenApi.Play(
                    def.hitLumenEffectId,
                    map,
                    new System.Numerics.Vector3(hit.HitWorldPos.x, 0.35f, hit.HitWorldPos.z),
                    1f,
                    rot,
                    false);
            }

            if (map == Find.CurrentMap && def.hitSound != null)
            {
                def.hitSound.PlayOneShot(new TargetInfo(hit.HitWorldPos.ToIntVec3(), map));
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
                if (lumenHandle != null && lumenHandle.IsAlive)
                {
                    lumenHandle.Stop(false);
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
                if (lumenHandle != null && lumenHandle.IsAlive)
                {
                    lumenHandle.Stop(true);
                }
            }
        }

        public override void FollowLost()
        {
            if (Finished)
            {
                return;
            }

            if (phase == Phase.Sustaining)
            {
                phase = Phase.Expiring;
                endPhaseTick = Find.TickManager.TicksGame;
                if (lumenHandle != null && lumenHandle.IsAlive)
                {
                    lumenHandle.Stop(true);
                }
            }
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            float flashTicks = def.hitFlashSeconds * GenTicks.TicksPerRealSecond;
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                if (now - pulses[i].tick > flashTicks)
                {
                    pulses.RemoveAt(i);
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
            if (Finished || mesh == null)
            {
                return;
            }

            mesh.Clear();
            float chargeRatio = Mathf.Clamp01(currentCharges / (float)maxCharges);
            float elapsed = SecondsSince(startTick);
            float pulseWave = 0.5f + 0.5f * Mathf.Sin(elapsed * def.pulseHz * Mathf.PI * 2f);
            float baseAlpha = Mathf.Lerp(def.pulseMinAlpha, def.pulseMaxAlpha, pulseWave)
                * Mathf.Lerp(0.45f, 1f, chargeRatio);
            float hdr = def.hdrIntensity * Mathf.Lerp(0.75f, 1.15f, chargeRatio);

            if (phase == Phase.Breaking)
            {
                float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.breakSeconds);
                float fade = (1f - u) * (1f - u);
                DrawBody(fade * 0.9f, hdr * 1.45f, 1f + 0.08f * u);
            }
            else if (phase == Phase.Expiring)
            {
                float u = Mathf.Clamp01(SecondsSince(endPhaseTick) / def.expireSeconds);
                DrawBody(baseAlpha * (1f - u), hdr * (1f - 0.5f * u), 1f);
            }
            else
            {
                DrawBody(baseAlpha, hdr, 1f);
            }

            DrawPulses();
            mesh.Upload();
            if (mesh.HasGeometry)
            {
                LumenShaders.DrawLumen(mesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        protected override void Release()
        {
            if (lumenHandle != null)
            {
                lumenHandle.Dispose();
                lumenHandle = null;
            }

            if (mesh != null)
            {
                mesh.Dispose();
                mesh = null;
            }
        }

        private void DrawBody(float alpha, float hdr, float scaleFactor)
        {
            if (alpha <= 0.001f)
            {
                return;
            }

            var glowCol = new Color(primaryColor.r, primaryColor.g, primaryColor.b, Mathf.Clamp01(alpha * 0.65f));
            var coreCol = new Color(coreColor.r, coreColor.g, coreColor.b, Mathf.Clamp01(alpha));

            if (shieldReq.shape == ShieldVfxShape.Dome)
            {
                float r = shieldReq.radius * scaleFactor;
                mesh.AddTiltedEllipseRing(
                    centerXZ, Vector3.right, Vector3.forward, r, 1f,
                    def.ringSegments, def.glowWidthCells, glowCol, hdr * 0.85f, def.edgePower);
                mesh.AddTiltedEllipseRing(
                    centerXZ, Vector3.right, Vector3.forward, r, 1f,
                    def.ringSegments, def.coreWidthCells, coreCol, hdr, def.edgePower);
            }
            else
            {
                float halfLen = shieldReq.spanLength * 0.5f * scaleFactor;
                linePoints[0] = centerXZ - tangentXZ * halfLen;
                linePoints[1] = centerXZ + tangentXZ * halfLen;
                mesh.AddPolyline(linePoints, 2, def.glowWidthCells, glowCol, hdr * 0.85f, def.edgePower, true, null);
                mesh.AddPolyline(linePoints, 2, def.coreWidthCells, coreCol, hdr, def.edgePower, true, null);
            }
        }

        private void DrawPulses()
        {
            for (int i = 0; i < pulses.Count; i++)
            {
                float u = Mathf.Clamp01(SecondsSince(pulses[i].tick) / def.hitFlashSeconds);
                float fade = 1f - u;
                float radius = Mathf.Lerp(0.25f, def.hitFlashRadiusCells, Mathf.Sqrt(u));
                mesh.AddRadialGlowDisc(
                    pulses[i].worldPos,
                    radius,
                    16,
                    new Color(coreColor.r, coreColor.g, coreColor.b, 0.78f * fade),
                    new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                    def.hdrIntensity * 1.35f,
                    def.edgePower,
                    null);
            }
        }

        private void StartSustainLumen()
        {
            if (!RimLumenApi.IsReady || def.sustainLumenEffectId.NullOrEmpty())
            {
                return;
            }

            float rot = Mathf.Atan2(normalXZ.x, normalXZ.z) * Mathf.Rad2Deg;
            float scale = shieldReq.shape == ShieldVfxShape.Dome ? shieldReq.radius : shieldReq.spanLength;
            lumenHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = def.sustainLumenEffectId,
                MapIndex = map != null ? map.uniqueID : -1,
                OriginWorld = new System.Numerics.Vector3(centerXZ.x, 0.25f, centerXZ.z),
                Scale = Mathf.Max(0.1f, scale),
                RotationDegrees = rot,
                RealTime = false
            });
            lumenHandle.SetParam("chargeRatio", currentCharges / (float)maxCharges);
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
