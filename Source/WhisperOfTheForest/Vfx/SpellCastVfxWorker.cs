using System;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Presents a Dragon's Dogma 2 inspired swirling vortex + vertical core spindle ahead of the caster,
    /// masked cleanly against the caster's 2.5D depth silhouette via <c>WotF_Shader_LumenRibbonDepth</c>
    /// and augmented by RimLumen L2 particles and dynamic PointLight.
    ///
    /// Timing follows <c>Find.TickManager.TicksGame</c>: pause freezes everything cleanly.
    /// Cancelling or interrupting ends charge VFX and sustainer immediately with no release flash.
    /// </summary>
    public sealed class SpellCastVfxWorker : VfxWorker
    {
        private enum Phase
        {
            Charging,
            Releasing
        }

        private SpellCastVfxDef def;
        private SpellThemeDef theme;
        private Map map;
        private Thing caster;

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

        private LumenRibbonMesh ribbonMesh;
        private ILumenEffectHandle lumenHandle;
        private Sustainer chargeSustainer;

        protected override void OnStart()
        {
            def = Def as SpellCastVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("SpellCastVfxWorker requires SpellCastVfxDef");
            }

            theme = Request.theme;
            if (theme == null)
            {
                throw new InvalidOperationException("SpellCastVfxWorker requires Request.theme");
            }

            map = Request.map;
            caster = Request.follow;
            startTick = Find.TickManager.TicksGame;
            totalPrepSeconds = Mathf.Max(0.05f, Request.chargeTicks / (float)GenTicks.TicksPerRealSecond);

            coreColor = theme.CoreColorUnity;
            primaryColor = theme.PrimaryColorUnity;
            secondaryColor = theme.SecondaryColorUnity;

            ribbonMesh = new LumenRibbonMesh("WotF_CastVortex_" + theme.defName);
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
                float u = SpellCastVfxMath.ComputeConvergence(elapsed, totalPrepSeconds, def.leadCollapseSeconds);

                if (lumenHandle != null && lumenHandle.IsAlive)
                {
                    lumenHandle.SetParam("scale", Mathf.Lerp(1f, 0.38f, u));
                    lumenHandle.SetParam("intensity", Mathf.Lerp(0.75f, 1.15f, u));
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
            if (releaseElapsed >= def.releaseFlashDurationSeconds)
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

            if (lumenHandle != null && lumenHandle.IsAlive)
            {
                lumenHandle.SetParam("scale", 1f);
                lumenHandle.SetParam("intensity", def.releaseFlashHdrMultiplier);
                lumenHandle.GoTo(VfxStates.Release);
            }
        }

        public override void DrawWorld()
        {
            if (Finished || ribbonMesh == null || caster == null || !caster.Spawned || caster.Map != map)
            {
                return;
            }

            Camera cam = Find.Camera;
            if (cam == null)
            {
                return;
            }

            RefreshFocalPoint();
            ribbonMesh.Clear();

            float casterZ = caster.DrawPos.z;
            float altitudeAt(float _, float wz) => SpellCastVfxMath.EvaluatePawnMaskAltitude(
                wz, casterZ, def.zTransitionHalf, def.frontAltitudeY, def.backAltitudeY);

            if (phase == Phase.Charging)
            {
                BuildChargingGeometry(altitudeAt);
            }
            else
            {
                BuildReleaseFlashGeometry(altitudeAt);
            }

            ribbonMesh.Upload();
            if (ribbonMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(ribbonMesh.Mesh, LumenShaders.RibbonDepth);
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

                lumenHandle = null;
            }

            if (ribbonMesh != null)
            {
                ribbonMesh.Dispose();
                ribbonMesh = null;
            }
        }

        private void BuildChargingGeometry(Func<float, float, float> altitudeAt)
        {
            float elapsed = SecondsSince(startTick);
            float u = SpellCastVfxMath.ComputeConvergence(elapsed, totalPrepSeconds, def.leadCollapseSeconds);
            float squeeze = Mathf.Pow(u, 1.35f);

            float currentRadius = Mathf.Lerp(def.vortexStartRadius, def.vortexMinRadius, squeeze);
            float innerRadius = currentRadius * 0.16f;
            float apexHeightY = Mathf.Lerp(def.funnelHeightCells, def.funnelHeightCells * 0.25f, squeeze);
            float spinAngle = elapsed * def.vortexSpinRadPerSec * (1f + 0.65f * squeeze);
            float hdrVortex = theme.hdrIntensity * def.hdrVortexMultiplier * Mathf.Lerp(0.80f, 1.20f, u);
            float hdrCore = theme.hdrIntensity * def.hdrCoreMultiplier * Mathf.Lerp(0.85f, 1.35f, u);
            float fadeIn = Mathf.Clamp01(elapsed / 0.12f);

            // 1. Crisp closed bottom magic ring + soft outer bloom halo on the ground plane (h = 0)
            float ringW = def.baseRingThickness * Mathf.Lerp(1f, 0.62f, squeeze);
            ribbonMesh.AddTiltedEllipseRing(
                lastFocalPos,
                Vector3.right,
                Vector3.forward,
                currentRadius,
                def.vortexEllipseAspectZ,
                40,
                ringW * 2.4f,
                new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.26f * fadeIn),
                hdrVortex * 0.85f,
                2.0f,
                altitudeAt);
            ribbonMesh.AddTiltedEllipseRing(
                lastFocalPos,
                Vector3.right,
                Vector3.forward,
                currentRadius,
                def.vortexEllipseAspectZ,
                40,
                ringW,
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.90f * fadeIn),
                hdrVortex * 1.15f,
                1.35f,
                altitudeAt);

            // 2. Counter-rotating runic arc segments inside the base ring so angular spin is immediately readable
            ribbonMesh.AddSegmentedEllipseArcs(
                lastFocalPos,
                currentRadius * 0.82f,
                def.vortexEllipseAspectZ,
                4,
                0.56f,
                -spinAngle * 0.78f,
                12,
                ringW * 0.82f,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.78f * fadeIn),
                hdrVortex * 1.1f,
                1.4f,
                altitudeAt);

            // 3. True 3D helical funnel arms spiraling inward AND upward (matching Vertical3D 60-degree pitch)
            float sweep = def.vortexTwistTurns * Mathf.PI * 2f;
            for (int arm = 0; arm < def.vortexArmCount; arm++)
            {
                float armOffset = (arm / (float)def.vortexArmCount) * Mathf.PI * 2f;
                float startAngle = spinAngle + armOffset;
                float startW = def.vortexRibbonWidth * Mathf.Lerp(1f, 0.58f, squeeze);
                float endW = startW * 0.36f;

                Color outerCol = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.62f * fadeIn);
                Color innerCol = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.92f * fadeIn);

                ribbonMesh.AddFunnelSpiralArc(
                    lastFocalPos,
                    currentRadius,
                    innerRadius,
                    0f,
                    apexHeightY,
                    def.vortexEllipseAspectZ,
                    startAngle,
                    sweep,
                    def.vortexSegmentsPerArm,
                    startW,
                    endW,
                    outerCol,
                    innerCol,
                    hdrVortex,
                    def.ribbonEdgePower,
                    altitudeAt);
            }

            // 4. Secondary counter-sweeping mid-funnel filaments
            for (int ring = 0; ring < def.outerVortexRingCount; ring++)
            {
                float ringOffset = ring * Mathf.PI + spinAngle * -0.6f;
                float ringRadius = currentRadius * (0.64f + 0.16f * ring);
                Color fStart = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0.38f * fadeIn);
                Color fEnd = new Color(coreColor.r, coreColor.g, coreColor.b, 0.72f * fadeIn);

                ribbonMesh.AddFunnelSpiralArc(
                    lastFocalPos,
                    ringRadius,
                    innerRadius * 0.85f,
                    apexHeightY * 0.15f,
                    apexHeightY,
                    def.vortexEllipseAspectZ,
                    ringOffset,
                    -sweep * 0.7f,
                    Mathf.Max(12, def.vortexSegmentsPerArm - 6),
                    def.vortexRibbonWidth * 0.50f,
                    def.vortexRibbonWidth * 0.22f,
                    fStart,
                    fEnd,
                    hdrVortex * 0.9f,
                    def.ribbonEdgePower,
                    altitudeAt);
            }

            // 5. Vertical vortex-core spindle & suspended focal halo at the 3D funnel apex
            Vector3 apexScreenPos = new Vector3(
                lastFocalPos.x,
                lastFocalPos.y,
                lastFocalPos.z + apexHeightY * LumenRibbonMesh.Ortho60HeightZ);

            float halfH = Mathf.Lerp(def.spindleHalfHeightStart, def.spindleHalfHeightMin, squeeze);
            float widthX = Mathf.Lerp(def.spindleWidthStart, def.spindleWidthMin, squeeze);
            Color tipCol = new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.22f * fadeIn);
            Color coreCol = new Color(coreColor.r, coreColor.g, coreColor.b, 0.88f * fadeIn);

            ribbonMesh.AddVerticalSpindle(
                apexScreenPos,
                halfH,
                widthX,
                def.spindleSegments,
                tipCol,
                coreCol,
                hdrCore,
                def.ribbonEdgePower,
                altitudeAt);

            float discRadius = Mathf.Lerp(def.coreDiscRadiusStart, def.coreDiscRadiusMin, squeeze) * (0.92f + 0.10f * Mathf.Sin(elapsed * 16f));
            ribbonMesh.AddRadialGlowDisc(
                apexScreenPos,
                discRadius,
                20,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.78f * fadeIn),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdrCore,
                1.6f,
                altitudeAt);
        }

        private void BuildReleaseFlashGeometry(Func<float, float, float> altitudeAt)
        {
            float progress = Mathf.Clamp01(SecondsSince(releaseTick) / def.releaseFlashDurationSeconds);
            float easeOut = 1f - (1f - progress) * (1f - progress);
            float fade = (1f - progress) * (1f - progress);
            float hdr = theme.hdrIntensity * def.releaseFlashHdrMultiplier * fade;

            // Soft translucent bloom disc (never harsh opaque whiteout)
            float flashRadius = Mathf.Lerp(def.coreDiscRadiusMin, def.releaseFlashRadius, easeOut);
            ribbonMesh.AddRadialGlowDisc(
                lastFocalPos,
                flashRadius,
                24,
                new Color(coreColor.r, coreColor.g, coreColor.b, 0.68f * fade),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0f),
                hdr,
                1.8f,
                altitudeAt);

            // Delicate expanding shock halo ring
            var major = Vector3.right;
            var minor = Vector3.forward;
            float ringRadius = Mathf.Lerp(def.vortexMinRadius, def.releaseFlashRadius * 1.15f, easeOut);
            ribbonMesh.AddTiltedEllipseRing(
                lastFocalPos,
                major,
                minor,
                ringRadius,
                def.vortexEllipseAspectZ,
                28,
                def.vortexRibbonWidth * 0.7f * (1f - 0.5f * progress),
                new Color(primaryColor.r, primaryColor.g, primaryColor.b, 0.55f * fade),
                hdr * 0.9f,
                1.5f,
                altitudeAt);
        }

        private void RefreshFocalPoint()
        {
            if (caster == null || !caster.Spawned || caster.Map != map)
            {
                return;
            }

            Vector3 casterPos = caster.DrawPos;
            Vector3 aimPos = casterPos + caster.Rotation.FacingCell.ToVector3();
            Thing targetThing = Request.aimThing;
            if (targetThing != null && targetThing.Spawned && targetThing.Map == map && targetThing != caster)
            {
                aimPos = targetThing.DrawPos;
            }
            else
            {
                float rdx = Request.target.x - Request.origin.x;
                float rdz = Request.target.z - Request.origin.z;
                if (rdx * rdx + rdz * rdz > 0.0025f)
                {
                    aimPos = Request.target;
                }
            }

            float dx = aimPos.x - casterPos.x;
            float dz = aimPos.z - casterPos.z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len > 0.0001f)
            {
                dirX = dx / len;
                dirZ = dz / len;
            }
            else
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                dirX = facing.x;
                dirZ = facing.z;
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
            if (!RimLumenApi.IsReady || theme.castLumenEffectId.NullOrEmpty())
            {
                return;
            }

            var origin = new System.Numerics.Vector3(lastFocalPos.x, def.lumenOriginHeightCells, lastFocalPos.z);
            float facing = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            float depthRefZ = caster != null ? caster.DrawPos.z : lastFocalPos.z;

            lumenHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = theme.castLumenEffectId,
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
            RefreshFocalPoint();
            worldPosition = new System.Numerics.Vector3(lastFocalPos.x, def.lumenOriginHeightCells, lastFocalPos.z);
            rotationDegrees = Mathf.Atan2(dirX, dirZ) * Mathf.Rad2Deg;
            return !Finished && caster != null && caster.Spawned && caster.Map == map;
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

        private float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
