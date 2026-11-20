using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhisperOfTheForest.Vfx
{
    public sealed class SpellCastVfxRequest : VfxRequest
    {
        public bool radialRelease;
    }

    public sealed class SpellQuickCastVfxDef : VfxDef
    {
        public float forwardOffsetCells;
        public float wallClearanceMargin;
        public float backAltitudeY;
        public float frontAltitudeY;
        public float zTransitionHalf;
        public float lumenOriginHeightCells;

        public float cloudAuraRadius;
        public float burnoutFlashDurationSeconds;
        public float burnoutPeakRadius;
        public float releaseHoldDurationSeconds;

        public float hdrChargeCoreMultiplier;
        public float hdrBurnoutMultiplier;
        public float ribbonEdgePower;

        public float directionalPushBackOffsetCells;
        public float directionalPushRadius;
        public float directionalPushStrength;
        public float directionalPushDurationSeconds;

        public float radialPushRadius;
        public float radialPushStrength;
        public float radialPushDurationSeconds;

        public override bool SupportsState(string state) => state == VfxStates.Release;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (forwardOffsetCells <= 0f)
            {
                yield return "forwardOffsetCells must be positive";
            }

            if (wallClearanceMargin < 0f)
            {
                yield return "wallClearanceMargin must not be negative";
            }

            if (frontAltitudeY <= backAltitudeY)
            {
                yield return "frontAltitudeY must exceed backAltitudeY for 2.5D pawn depth masking";
            }

            if (zTransitionHalf <= 0f)
            {
                yield return "zTransitionHalf must be positive";
            }

            if (cloudAuraRadius <= 0f || burnoutPeakRadius <= 0f)
            {
                yield return "cloudAuraRadius and burnoutPeakRadius must be positive";
            }

            if (burnoutFlashDurationSeconds <= 0f || releaseHoldDurationSeconds < burnoutFlashDurationSeconds)
            {
                yield return "burnoutFlashDurationSeconds must be positive and at most releaseHoldDurationSeconds";
            }

            if (hdrChargeCoreMultiplier <= 0f || hdrBurnoutMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "hdrChargeCoreMultiplier, hdrBurnoutMultiplier and ribbonEdgePower must be positive";
            }

            if (directionalPushBackOffsetCells <= 0f || directionalPushRadius <= 0f
                || directionalPushStrength <= 0f || directionalPushDurationSeconds <= 0f)
            {
                yield return "directionalPush parameters must all be positive";
            }

            if (radialPushRadius <= 0f || radialPushStrength <= 0f || radialPushDurationSeconds <= 0f)
            {
                yield return "radialPush parameters must all be positive";
            }
        }
    }

    /// <summary>
    /// Short-windup (&lt; 60 ticks) spell-casting visual: a compact surging theme-colored particle cloud
    /// with no ground magic circle or hard funnel. On release, shoves the live cloud forward along cast
    /// direction (or gently outward in 360 degrees for Self casts) while the core flashes bright and
    /// collapses to zero; pushed particles brake rapidly and linger as soft drifting motes.
    /// </summary>
    public sealed class SpellQuickCastVfxWorker : VfxWorker
    {
        private const string StateReleaseDir = "ReleaseDir";
        private const string StateReleaseSelf = "ReleaseSelf";

        private enum Phase
        {
            Charging,
            Releasing
        }

        private SpellQuickCastVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private Thing caster;
        private bool radialRelease;

        private Phase phase = Phase.Charging;
        private int startTick;
        private int releaseTick;
        private float totalPrepSeconds;
        private float dirX;
        private float dirZ;
        private Vector3 lastFocalPos;

        private Color coreColor;
        private Color primaryColor;
        private Color secondaryColor;

        private LumenRibbonMesh auraMesh;
        private ILumenEffectHandle lumenHandle;
        private Sustainer chargeSustainer;

        public static bool IsRadialCastRelease(Spells.SpellFormKind form)
        {
            return form == Spells.SpellFormKind.Self;
        }

        protected override void OnStart()
        {
            def = Def as SpellQuickCastVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("SpellQuickCastVfxWorker requires SpellQuickCastVfxDef");
            }

            theme = Request.theme;
            if (theme == null)
            {
                throw new InvalidOperationException("SpellQuickCastVfxWorker requires Request.theme");
            }

            map = Request.map;
            caster = Request.follow;
            radialRelease = Request is SpellCastVfxRequest castReq && castReq.radialRelease;
            startTick = Find.TickManager.TicksGame;
            totalPrepSeconds = Mathf.Max(0.05f, Request.chargeTicks / (float)GenTicks.TicksPerRealSecond);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            auraMesh = new LumenRibbonMesh("WotF_CastQuick_" + theme.defName);
            RefreshFocalPoint();
            StartRimLumenEffect();
            StartChargeAudio();
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (phase == Phase.Charging)
            {
                RefreshFocalPoint();
                float elapsed = SecondsSince(startTick);
                float u = Mathf.Clamp01(elapsed / totalPrepSeconds);

                if (lumenHandle != null && lumenHandle.IsAlive)
                {
                    lumenHandle.SetParam("scale", Mathf.Lerp(0.92f, 1.08f, u));
                    lumenHandle.SetParam("intensity", Mathf.Lerp(0.85f, 1.22f, u));
                }

                if (map == Find.CurrentMap)
                {
                    chargeSustainer?.Maintain();
                }
                else
                {
                    EndChargeAudio();
                }

                return;
            }

            float releaseElapsed = SecondsSince(releaseTick);
            if (releaseElapsed >= def.releaseHoldDurationSeconds
                && (lumenHandle == null || !lumenHandle.IsAlive))
            {
                Finished = true;
            }
            else if (releaseElapsed >= def.releaseHoldDurationSeconds + 0.35f)
            {
                Finished = true;
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Release || phase == Phase.Releasing)
            {
                return;
            }

            phase = Phase.Releasing;
            releaseTick = Find.TickManager.TicksGame;
            RefreshFocalPoint();
            EndChargeAudio();

            if (map == Find.CurrentMap && theme.castReleaseSound != null)
            {
                theme.castReleaseSound.PlayOneShot(new TargetInfo(lastFocalPos.ToIntVec3(), map));
            }

            EmitCloudPushImpulse();

            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                lumenHandle.SetParam("scale", 1f);
                lumenHandle.SetParam("intensity", def.hdrBurnoutMultiplier);
                lumenHandle.GoTo(radialRelease ? StateReleaseSelf : StateReleaseDir);
            }
        }

        public override void FollowLost()
        {
            if (phase == Phase.Releasing)
            {
                return;
            }

            Stop();
        }

        public override void DrawWorld()
        {
            if (Finished || auraMesh == null || map == null)
            {
                return;
            }

            if (phase == Phase.Charging && (caster == null || !caster.Spawned || caster.Map != map))
            {
                return;
            }

            Camera cam = Find.Camera;
            if (cam == null)
            {
                return;
            }

            if (phase == Phase.Charging)
            {
                RefreshFocalPoint();
            }

            auraMesh.Clear();
            float casterZ = caster != null && caster.Spawned ? caster.DrawPos.z : lastFocalPos.z;
            float altitudeAt(float _, float wz) => SpellCastVfxMath.EvaluatePawnMaskAltitude(
                wz, casterZ, def.zTransitionHalf, def.frontAltitudeY, def.backAltitudeY);

            if (phase == Phase.Charging)
            {
                BuildChargingAura(altitudeAt);
            }
            else
            {
                BuildBurnoutFlash(altitudeAt);
            }

            auraMesh.Upload();
            if (auraMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(auraMesh.Mesh, LumenShaders.RibbonDepth);
            }
        }

        protected override void Release()
        {
            EndChargeAudio();

            if (lumenHandle != null)
            {
                if (phase != Phase.Releasing)
                {
                    lumenHandle.Stop(allowFadeOut: true);
                }
                else
                {
                    lumenHandle.Stop(allowFadeOut: false);
                }

                lumenHandle = null;
            }

            if (auraMesh != null)
            {
                auraMesh.Dispose();
                auraMesh = null;
            }
        }

        private void BuildChargingAura(Func<float, float, float> altitudeAt)
        {
            float elapsed = SecondsSince(startTick);
            float u = Mathf.Clamp01(elapsed / totalPrepSeconds);
            float fadeIn = Mathf.Clamp01(elapsed / 0.08f);
            float pulse = 1f + 0.09f * Mathf.Sin(elapsed * 15f);
            float radius = def.cloudAuraRadius * Mathf.Lerp(0.88f, 1.06f, u) * pulse;
            float hdr = theme.hdrIntensity * def.hdrChargeCoreMultiplier * Mathf.Lerp(0.85f, 1.25f, u);

            auraMesh.AddRadialGlowDisc(
                lastFocalPos,
                radius * 1.45f,
                20,
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.24f * fadeIn),
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                hdr * 0.8f,
                def.ribbonEdgePower * 1.15f,
                altitudeAt);
            auraMesh.AddRadialGlowDisc(
                lastFocalPos,
                radius * 0.72f,
                18,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.56f * fadeIn),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdr * 1.1f,
                def.ribbonEdgePower,
                altitudeAt);
        }

        private void BuildBurnoutFlash(Func<float, float, float> altitudeAt)
        {
            float elapsed = SecondsSince(releaseTick);
            if (elapsed >= def.burnoutFlashDurationSeconds)
            {
                return;
            }

            float t = Mathf.Clamp01(elapsed / def.burnoutFlashDurationSeconds);
            // Swells sharply to peak at t ~ 0.26, then collapses cleanly to zero ("核爆闪灭").
            float envelope = t < 0.26f
                ? Mathf.Lerp(0.65f, 1f, t / 0.26f)
                : Mathf.Pow(1f - (t - 0.26f) / 0.74f, 1.85f);
            float radius = def.burnoutPeakRadius * envelope;
            float alpha = Mathf.Pow(1f - t, 1.35f);
            float hdr = theme.hdrIntensity * def.hdrBurnoutMultiplier * Mathf.Lerp(1.35f, 0.45f, t);

            auraMesh.AddRadialGlowDisc(
                lastFocalPos,
                radius * 1.4f,
                22,
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.48f * alpha),
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f),
                hdr * 0.9f,
                def.ribbonEdgePower,
                altitudeAt);
            auraMesh.AddRadialGlowDisc(
                lastFocalPos,
                radius * 0.68f,
                20,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.92f * alpha),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdr * 1.45f,
                def.ribbonEdgePower * 0.9f,
                altitudeAt);
        }

        private void EmitCloudPushImpulse()
        {
            if (!RimLumenApi.IsReady || map == null || map.uniqueID < 0)
            {
                return;
            }

            if (radialRelease)
            {
                RimLumenApi.EmitEnvironmentPulse(new LumenEnvironmentPulseRequest
                {
                    MapIndex = map.uniqueID,
                    Channel = LumenEnvironmentChannel.Explosion,
                    Shape = LumenEnvironmentForceShape.HorizontalRadial,
                    CenterWorld = new System.Numerics.Vector3(lastFocalPos.x, def.lumenOriginHeightCells, lastFocalPos.z),
                    Radius = def.radialPushRadius,
                    Strength = def.radialPushStrength,
                    Duration = def.radialPushDurationSeconds,
                    RealTime = false
                });
                return;
            }

            // Place the radial impulse slightly behind the cloud so all live charge particles are shoved forward along dir.
            float pushOriginX = lastFocalPos.x - dirX * def.directionalPushBackOffsetCells;
            float pushOriginZ = lastFocalPos.z - dirZ * def.directionalPushBackOffsetCells;
            RimLumenApi.EmitEnvironmentPulse(new LumenEnvironmentPulseRequest
            {
                MapIndex = map.uniqueID,
                Channel = LumenEnvironmentChannel.Explosion,
                Shape = LumenEnvironmentForceShape.HorizontalRadial,
                CenterWorld = new System.Numerics.Vector3(pushOriginX, def.lumenOriginHeightCells, pushOriginZ),
                Radius = def.directionalPushRadius,
                Strength = def.directionalPushStrength,
                Duration = def.directionalPushDurationSeconds,
                RealTime = false
            });
        }

        private void RefreshFocalPoint()
        {
            Vector3 casterPos = caster != null && caster.Spawned ? caster.DrawPos : Request.origin;
            if (radialRelease)
            {
                dirX = 0f;
                dirZ = 1f;
                float selfY = SpellCastVfxMath.EvaluatePawnMaskAltitude(
                    casterPos.z, casterPos.z, def.zTransitionHalf, def.frontAltitudeY, def.backAltitudeY);
                lastFocalPos = new Vector3(casterPos.x, selfY, casterPos.z);
                return;
            }

            Vector3 aimPos = Request.aimThing != null && Request.aimThing.Spawned && Request.aimThing.Map == map
                ? Request.aimThing.DrawPos
                : Request.target;

            float dx = aimPos.x - casterPos.x;
            float dz = aimPos.z - casterPos.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len > 0.001f)
            {
                dirX = dx / len;
                dirZ = dz / len;
            }
            else if (caster != null)
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                dirX = facing.x;
                dirZ = facing.z;
                if (dirX == 0f && dirZ == 0f)
                {
                    dirZ = -1f;
                }
            }
            else if (dirX == 0f && dirZ == 0f)
            {
                dirZ = -1f;
            }

            float initialY = SpellCastVfxMath.EvaluatePawnMaskAltitude(
                casterPos.z + dirZ * def.forwardOffsetCells,
                casterPos.z,
                def.zTransitionHalf,
                def.frontAltitudeY,
                def.backAltitudeY);

            lastFocalPos = SpellCastVfxMath.ClampFocalPointBeforeWall(
                map,
                casterPos,
                dirX,
                dirZ,
                def.forwardOffsetCells,
                def.wallClearanceMargin,
                initialY);
        }

        private void StartRimLumenEffect()
        {
            if (!RimLumenApi.IsReady || theme.quickCastLumenEffectId.NullOrEmpty())
            {
                return;
            }

            var origin = new System.Numerics.Vector3(lastFocalPos.x, def.lumenOriginHeightCells, lastFocalPos.z);
            float facing = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            float depthRefZ = caster != null ? caster.DrawPos.z : lastFocalPos.z;

            lumenHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = theme.quickCastLumenEffectId,
                MapIndex = map != null ? map.uniqueID : -1,
                OriginWorld = origin,
                DepthReferenceZ = depthRefZ,
                Scale = 1f,
                RotationDegrees = facing,
                Follow = FollowFocalPoint,
                RealTime = false
            });
        }

        private bool FollowFocalPoint(out System.Numerics.Vector3 worldPosition, out float rotationDegrees)
        {
            if (phase == Phase.Charging)
            {
                RefreshFocalPoint();
            }

            worldPosition = new System.Numerics.Vector3(lastFocalPos.x, def.lumenOriginHeightCells, lastFocalPos.z);
            rotationDegrees = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            return !Finished && phase == Phase.Charging && caster != null && caster.Spawned && caster.Map == map;
        }

        private void StartChargeAudio()
        {
            if (map != Find.CurrentMap || caster == null || !caster.Spawned || theme.castChargeSound == null)
            {
                return;
            }

            chargeSustainer = theme.castChargeSound.TrySpawnSustainer(
                SoundInfo.InMap(caster, MaintenanceType.PerFrame));
        }

        private void EndChargeAudio()
        {
            if (chargeSustainer != null)
            {
                if (!chargeSustainer.Ended)
                {
                    chargeSustainer.End();
                }

                chargeSustainer = null;
            }
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
