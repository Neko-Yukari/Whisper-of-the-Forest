using System;
using System.Collections.Generic;
using RimLumen.Core.Api;
using RimLumen.Core.Contracts;
using UnityEngine;
using Verse;
using Verse.Sound;
using WhisperOfTheForest.Spells;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Full RimLumen Railgun VFX worker.
    ///   1. Charging: 2.5D depth-masked focal convergence + micro-arcs in front of the caster,
    ///      paired with RailgunCharge RimLumen L2 particles/PointLight and charge sustainer audio.
    ///      Waits explicitly for <c>Trigger(VfxStates.Release)</c> so an interrupted channel NEVER fires
    ///      a phantom beam.
    ///   2. Firing: selective Layer 15 map darkening (UI untouched), HDR core + quadratic heat linger
    ///      glow, 7 deterministic re-striking jagged bolts, 16 travelling elliptical shock/re-ionisation
    ///      rings, RailgunFire RimLumen corridor embers/GlowGrid lights and layered fire audio.
    /// All clocks read <c>Find.TickManager.TicksGame</c>, freezing cleanly when paused.
    /// </summary>
    public sealed class RailgunLumenVfxWorker : VfxWorker
    {
        private const float MinVisible = 0.001f;

        private RailgunLumenVfxDef def;
        private Map map;
        private Thing caster;
        private Vector3 aim;

        private int startTick;
        private int chargeTicks;
        private bool fired;
        private int fireTick;
        private int castSeed;

        private float aimDirX;
        private float aimDirZ;
        private Vector3 chargeFocalPos;

        private Vector3 beamOrigin;
        private float beamLength;

        private Color chargeColor;
        private Color chargeCoreColor;
        private Color coreColor;
        private Color glowColor;
        private Color afterglowColor;
        private Color arcColor;
        private Color ringPrimaryColor;
        private Color ringSecondaryColor;

        private bool[] ringHasFlash;
        private float[] boltSeeds;
        private float[] boltLengths;
        private int[] boltSegments;
        private int[] boltCycles;
        private List<Vector3>[] boltPointBuffers;

        private LumenRibbonMesh depthMesh;
        private LumenRibbonMesh overMesh;
        private ILumenEffectHandle chargeHandle;
        private ILumenEffectHandle fireHandle;
        private Sustainer chargeSustainer;

        private float ShockSpeedCellsPerSecond
        {
            get
            {
                float window = def.beamHoldSeconds * def.ringShockArrivalFraction;
                return beamLength > 0f && window > 0f ? beamLength / window : 0f;
            }
        }

        private float TotalFireSeconds
        {
            get
            {
                float beamEnd = def.beamHoldSeconds + def.beamFadeSeconds;
                float glowEnd = def.beamHoldSeconds + def.glowLingerSeconds + def.beamFadeSeconds;
                float ringLife = def.ringMainPulseSeconds + def.ringFlashDelaySeconds + def.ringFlashSeconds;
                float lastAcross = beamLength * def.ringCount / (def.ringCount + 1f);
                float shock = ShockSpeedCellsPerSecond;
                float ringEnd = (shock > 0f ? lastAcross / shock : 0f) + ringLife;
                return Mathf.Max(Mathf.Max(beamEnd, glowEnd), ringEnd);
            }
        }

        protected override void OnStart()
        {
            def = Def as RailgunLumenVfxDef;
            if (def == null)
            {
                throw new InvalidOperationException("RailgunLumenVfxWorker requires RailgunLumenVfxDef");
            }

            map = Request.map;
            caster = Request.follow;
            aim = Request.target;
            chargeTicks = Mathf.Max(0, Request.chargeTicks);
            startTick = Find.TickManager.TicksGame;
            castSeed = Gen.HashCombineInt(startTick, map.uniqueID);

            chargeColor = VfxColors.ParseUnity(def.chargeColor);
            chargeCoreColor = VfxColors.ParseUnity(def.chargeCoreColor);
            coreColor = VfxColors.ParseUnity(def.coreColor);
            glowColor = VfxColors.ParseUnity(def.glowColor);
            afterglowColor = VfxColors.ParseUnity(def.afterglowColor);
            arcColor = VfxColors.ParseUnity(def.arcColor);
            ringPrimaryColor = VfxColors.ParseUnity(def.ringColor);
            ringSecondaryColor = VfxColors.ParseUnity(def.ringSecondaryColor);

            depthMesh = new LumenRibbonMesh("WotF_Railgun_ChargeDepth");
            overMesh = new LumenRibbonMesh("WotF_Railgun_FireOver");

            RefreshChargeFocal();
            StartChargeLumen();
            StartChargeAudio();
        }

        public override void Update(float deltaSeconds)
        {
            if (Finished)
            {
                return;
            }

            if (!fired)
            {
                RefreshChargeFocal();
                float progress = chargeTicks <= 0
                    ? 1f
                    : Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)chargeTicks);

                if (chargeHandle != null && chargeHandle.IsAlive)
                {
                    chargeHandle.SetParam("scale", Mathf.Lerp(1.02f, 0.42f, progress));
                    chargeHandle.SetParam("intensity", Mathf.Lerp(0.75f, 1.20f, progress));
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

            if (SecondsSince(fireTick) >= TotalFireSeconds)
            {
                Finished = true;
            }
        }

        public override void Trigger(string state)
        {
            if (Finished || state != VfxStates.Release || fired)
            {
                return;
            }

            BeginFire();
        }

        public override void DrawWorld()
        {
            if (Finished || map != Find.CurrentMap)
            {
                return;
            }

            Camera camera = Find.Camera;
            if (camera == null)
            {
                return;
            }

            if (!fired)
            {
                DrawChargeGeometry();
                return;
            }

            DrawFireGeometry(camera, SecondsSince(fireTick));
        }

        protected override void Release()
        {
            EndChargeAudio();

            if (chargeHandle != null)
            {
                chargeHandle.Stop(allowFadeOut: false);
                chargeHandle = null;
            }

            if (fireHandle != null)
            {
                fireHandle.Stop(allowFadeOut: fired);
                fireHandle = null;
            }

            depthMesh?.Dispose();
            depthMesh = null;
            overMesh?.Dispose();
            overMesh = null;
        }

        private void BeginFire()
        {
            fired = true;
            fireTick = Find.TickManager.TicksGame;
            EndChargeAudio();

            if (chargeHandle != null)
            {
                chargeHandle.Stop(allowFadeOut: false);
                chargeHandle = null;
            }

            Vector3 start = caster != null && caster.Spawned && caster.Map == map
                ? caster.Position.ToVector3Shifted()
                : Request.origin;

            if (!RailgunGeometry.TryDirection(start.x, start.z, aim.x, aim.z, out aimDirX, out aimDirZ))
            {
                aimDirX = Request.direction.x;
                aimDirZ = Request.direction.z;
                float norm = Mathf.Sqrt(aimDirX * aimDirX + aimDirZ * aimDirZ);
                if (norm > 0.0001f)
                {
                    aimDirX /= norm;
                    aimDirZ /= norm;
                }
                else
                {
                    aimDirX = 1f;
                    aimDirZ = 0f;
                }
            }

            beamOrigin = new Vector3(
                start.x + aimDirX * def.chargeOffsetCells * 0.65f,
                def.beamAltitudeY,
                start.z + aimDirZ * def.chargeOffsetCells * 0.65f);

            float requestedMax = Request.length > 0f ? Request.length : 512f;
            beamLength = RailgunGeometry.TryLength(start.x, start.z, aimDirX, aimDirZ, requestedMax, map.Size.x, map.Size.z, out float clipped)
                ? clipped
                : requestedMax;

            InitBoltState();
            InitRingState();

            if (map == Find.CurrentMap && def.fireSound != null)
            {
                def.fireSound.PlayOneShot(new TargetInfo(start.ToIntVec3(), map));
            }

            StartFireLumen();

            VfxDiagnostics.Info("railgun lumen shot fired: length=" + beamLength.ToString("F1")
                + " bolts=" + def.arcCount + " rings=" + def.ringCount);
        }

        private void DrawChargeGeometry()
        {
            if (depthMesh == null || caster == null || !caster.Spawned || caster.Map != map)
            {
                return;
            }

            RefreshChargeFocal();
            depthMesh.Clear();

            float elapsed = SecondsSince(startTick);
            float progress = chargeTicks <= 0
                ? 1f
                : Mathf.Clamp01((Find.TickManager.TicksGame - startTick) / (float)chargeTicks);
            float squeeze = Mathf.Pow(progress, 1.25f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * Mathf.PI * 2f * def.chargePulseHz);

            float casterZ = caster.DrawPos.z;
            float altitudeAt(float _, float wz) => SpellCastVfxMath.EvaluatePawnMaskAltitude(
                wz, casterZ, def.zTransitionHalf, def.frontAltitudeY, def.backAltitudeY);

            float outerRadius = def.chargeRadiusCells * Mathf.Lerp(1.05f, 0.22f, squeeze) * (0.92f + 0.08f * pulse);
            float apexHeightY = def.chargeFunnelHeightCells * Mathf.Lerp(1f, 0.28f, squeeze);
            float sweep = Mathf.PI * 2.5f;
            float spin = elapsed * (5.0f + 4.2f * squeeze);
            float intensity = def.chargeIntensity * Mathf.Lerp(0.80f, 1.30f, progress);
            float ringW = def.chargeBaseRingThickness * Mathf.Lerp(1f, 0.65f, squeeze);

            // 1. Crisp closed outer containment base ring + soft bloom halo at h = 0 (60-degree Vertical3D pitch)
            depthMesh.AddTiltedEllipseRing(
                chargeFocalPos,
                Vector3.right,
                Vector3.forward,
                outerRadius,
                LumenRibbonMesh.Ortho60AspectZ,
                44,
                ringW * 2.5f,
                new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.28f),
                intensity * 0.85f,
                2.0f,
                altitudeAt);
            depthMesh.AddTiltedEllipseRing(
                chargeFocalPos,
                Vector3.right,
                Vector3.forward,
                outerRadius,
                LumenRibbonMesh.Ortho60AspectZ,
                44,
                ringW,
                new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.92f),
                intensity * 1.15f,
                1.35f,
                altitudeAt);

            // 2. Counter-rotating segmented capacitor arcs inside the base ring
            depthMesh.AddSegmentedEllipseArcs(
                chargeFocalPos,
                outerRadius * 0.82f,
                LumenRibbonMesh.Ortho60AspectZ,
                4,
                0.55f,
                -spin * 0.82f,
                12,
                ringW * 0.85f,
                new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.82f),
                intensity * 1.1f,
                1.4f,
                altitudeAt);

            // 3. True 3D helical plasma funnel arms spiraling inward AND upward into the suspended core
            for (int arm = 0; arm < def.chargeVortexArms; arm++)
            {
                float armStart = spin + (arm / (float)def.chargeVortexArms) * Mathf.PI * 2f;
                depthMesh.AddFunnelSpiralArc(
                    chargeFocalPos,
                    outerRadius,
                    0.045f,
                    0f,
                    apexHeightY,
                    LumenRibbonMesh.Ortho60AspectZ,
                    armStart,
                    sweep,
                    28,
                    0.095f * Mathf.Lerp(1f, 0.62f, squeeze),
                    0.032f,
                    new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.55f + 0.35f * progress),
                    new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.92f),
                    intensity,
                    def.ribbonEdgePower,
                    altitudeAt);
            }

            Vector3 apexScreenPos = new Vector3(
                chargeFocalPos.x,
                chargeFocalPos.y,
                chargeFocalPos.z + apexHeightY * LumenRibbonMesh.Ortho60HeightZ);

            float spindleH = def.chargeSpindleHalfHeight * Mathf.Lerp(1f, 0.35f, squeeze);
            depthMesh.AddVerticalSpindle(
                apexScreenPos,
                spindleH,
                Mathf.Lerp(0.14f, 0.07f, squeeze),
                16,
                new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.22f),
                new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.88f),
                intensity * 1.05f,
                def.ribbonEdgePower,
                altitudeAt);

            depthMesh.AddRadialGlowDisc(
                apexScreenPos,
                Mathf.Lerp(0.24f, 0.15f, squeeze) * (0.92f + 0.14f * pulse),
                20,
                new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.82f),
                new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0f),
                intensity * 1.1f,
                1.55f,
                altitudeAt);

            // Micro-lightning tendrils snapping from the base ring up into the suspended focal sphere
            int boltSteps = Mathf.FloorToInt(elapsed / 0.06f);
            for (int b = 0; b < def.chargeBoltCount; b++)
            {
                float seedVal = RingRoll01(castSeed + boltSteps * 31, b);
                float angle = seedVal * Mathf.PI * 2f;
                Vector3 outerPt = new Vector3(
                    chargeFocalPos.x + Mathf.Cos(angle) * outerRadius,
                    chargeFocalPos.y,
                    chargeFocalPos.z + Mathf.Sin(angle) * outerRadius * LumenRibbonMesh.Ortho60AspectZ);
                Vector3 midPt = Vector3.Lerp(outerPt, apexScreenPos, 0.52f)
                    + new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * ((seedVal - 0.5f) * 0.22f);
                depthMesh.AddTaperedSegment(
                    outerPt,
                    midPt,
                    0.022f,
                    0.045f,
                    new Color(chargeColor.r, chargeColor.g, chargeColor.b, 0.35f),
                    new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.82f),
                    intensity,
                    1.3f,
                    altitudeAt);
                depthMesh.AddTaperedSegment(
                    midPt,
                    apexScreenPos,
                    0.045f,
                    0.025f,
                    new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.82f),
                    new Color(chargeCoreColor.r, chargeCoreColor.g, chargeCoreColor.b, 0.65f),
                    intensity,
                    1.3f,
                    altitudeAt);
            }

            depthMesh.Upload();
            if (depthMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(depthMesh.Mesh, LumenShaders.RibbonDepth);
            }
        }

        private void DrawFireGeometry(Camera camera, float elapsed)
        {
            if (overMesh == null)
            {
                return;
            }

            overMesh.Clear();

            float beamAlpha = BeamAlpha(elapsed);
            float glowAlpha = GlowAlpha(elapsed);
            Vector3 beamEnd = beamOrigin + new Vector3(aimDirX, 0f, aimDirZ) * beamLength;

            if (glowAlpha > MinVisible)
            {
                float tHold = def.beamHoldSeconds;
                float heatMix = elapsed <= tHold ? 0f : Mathf.Clamp01((elapsed - tHold) / Mathf.Max(0.01f, def.glowLingerSeconds));
                Color heatColor = Color.Lerp(glowColor, afterglowColor, heatMix);
                float heatWidth = def.glowWidthCells * Mathf.Lerp(1f, 0.52f, heatMix) * (0.4f + 0.6f * glowAlpha);
                float heatIntensity = Mathf.Lerp(def.glowIntensity, def.afterglowIntensity, heatMix);

                overMesh.AddTaperedSegment(
                    beamOrigin,
                    beamEnd,
                    heatWidth,
                    heatWidth * 0.92f,
                    new Color(heatColor.r, heatColor.g, heatColor.b, glowAlpha),
                    new Color(heatColor.r, heatColor.g, heatColor.b, glowAlpha),
                    heatIntensity,
                    def.ribbonEdgePower,
                    null);
            }

            if (beamAlpha > MinVisible)
            {
                float coreW = def.coreWidthCells * (0.35f + 0.65f * beamAlpha);
                overMesh.AddTaperedSegment(
                    beamOrigin,
                    beamEnd,
                    coreW,
                    coreW * 0.95f,
                    new Color(coreColor.r, coreColor.g, coreColor.b, beamAlpha),
                    new Color(coreColor.r, coreColor.g, coreColor.b, beamAlpha),
                    def.coreIntensity,
                    Mathf.Max(0.9f, def.ribbonEdgePower * 0.75f),
                    null);

                overMesh.AddRadialGlowDisc(
                    beamOrigin,
                    def.muzzleFlareRadius * (0.45f + 0.55f * beamAlpha),
                    24,
                    new Color(coreColor.r, coreColor.g, coreColor.b, beamAlpha),
                    new Color(glowColor.r, glowColor.g, glowColor.b, 0f),
                    def.coreIntensity * 1.15f,
                    1.6f,
                    null);
            }

            AppendBolts(elapsed, beamAlpha);
            AppendShockRings(camera, elapsed);

            overMesh.Upload();
            if (overMesh.HasGeometry)
            {
                LumenShaders.DrawLumen(overMesh.Mesh, LumenShaders.RibbonOver);
            }
        }

        private void InitBoltState()
        {
            if (def.arcCount <= 0)
            {
                boltPointBuffers = null;
                return;
            }

            var random = new System.Random(castSeed);
            boltSeeds = new float[def.arcCount];
            boltLengths = new float[def.arcCount];
            boltSegments = new int[def.arcCount];
            boltCycles = new int[def.arcCount];
            boltPointBuffers = new List<Vector3>[def.arcCount];

            for (int i = 0; i < def.arcCount; i++)
            {
                float len = Mathf.Min(beamLength,
                    Mathf.Lerp(def.arcMinLengthCells, def.arcMaxLengthCells, (float)random.NextDouble()));
                int segs = Mathf.Clamp(Mathf.CeilToInt(len * def.arcSegmentsPerCell), 2, def.arcMaxSegments);
                boltLengths[i] = len;
                boltSegments[i] = segs;
                boltSeeds[i] = (float)random.NextDouble() * 1000f;
                boltCycles[i] = int.MinValue;
                boltPointBuffers[i] = new List<Vector3>(segs + 1);
            }
        }

        private void AppendBolts(float elapsed, float beamAlpha)
        {
            if (boltPointBuffers == null || beamAlpha <= MinVisible)
            {
                return;
            }

            float cycleSeconds = def.arcLifeSeconds + def.arcGapSeconds;
            for (int i = 0; i < boltPointBuffers.Length; i++)
            {
                float clock = elapsed + boltSeeds[i];
                int cycle = Mathf.FloorToInt(clock / cycleSeconds);
                float phase = clock - cycle * cycleSeconds;
                if (phase >= def.arcLifeSeconds)
                {
                    continue;
                }

                if (cycle != boltCycles[i])
                {
                    boltCycles[i] = cycle;
                    RebuildBoltPoints(i, cycle);
                }

                float life = phase / def.arcLifeSeconds;
                float alpha = beamAlpha * Mathf.Sin(life * Mathf.PI);
                if (alpha <= MinVisible)
                {
                    continue;
                }

                overMesh.AddPolyline(
                    boltPointBuffers[i],
                    boltSegments[i] + 1,
                    def.arcThicknessCells,
                    new Color(arcColor.r, arcColor.g, arcColor.b, alpha),
                    def.arcIntensity,
                    1.3f,
                    true,
                    null);
            }
        }

        private void RebuildBoltPoints(int index, int cycle)
        {
            var random = new System.Random(Gen.HashCombineInt(Gen.HashCombineInt(castSeed, index), cycle));
            float sideX = -aimDirZ;
            float sideZ = aimDirX;
            float length = boltLengths[index];
            float startAlong = (float)random.NextDouble() * Mathf.Max(0f, beamLength - length);
            float baseAngle = (float)(random.NextDouble() * Math.PI * 2.0);
            float jitterRad = def.arcAngleJitterDegrees * Mathf.Deg2Rad;
            int segments = boltSegments[index];

            List<Vector3> buffer = boltPointBuffers[index];
            buffer.Clear();
            for (int s = 0; s <= segments; s++)
            {
                float along = startAlong + length * s / segments;
                float angle = baseAngle + ((float)random.NextDouble() * 2f - 1f) * jitterRad;
                float radius = def.arcRadiusCells + ((float)random.NextDouble() * 2f - 1f) * def.arcJitterCells;
                float cos = Mathf.Cos(angle) * radius;
                float sin = Mathf.Sin(angle) * radius;

                buffer.Add(new Vector3(
                    beamOrigin.x + aimDirX * along + sideX * cos,
                    beamOrigin.y + Mathf.Clamp(sin * 0.1f, -0.15f, 0.15f),
                    beamOrigin.z + aimDirZ * along + sideZ * cos));
            }
        }

        private void InitRingState()
        {
            ringHasFlash = new bool[def.ringCount];
            for (int i = 0; i < def.ringCount; i++)
            {
                ringHasFlash[i] = RingRoll01(castSeed, i) < def.ringFlashChance;
            }
        }

        private void AppendShockRings(Camera camera, float elapsed)
        {
            if (ringHasFlash == null || def.ringCount <= 0)
            {
                return;
            }

            if (!TryComputeRingBasis(camera, out Vector3 minor, out Vector3 major))
            {
                return;
            }

            float ratio = def.ringMode == RailgunRingMode.Ellipse30 ? def.ringEllipseRatio : 1f;
            float speed = ShockSpeedCellsPerSecond;
            if (speed <= 0f)
            {
                return;
            }

            for (int i = 0; i < def.ringCount; i++)
            {
                float across = beamLength * ((i + 1f) / (def.ringCount + 1f));
                float age = elapsed - across / speed;
                if (age < 0f)
                {
                    continue;
                }

                float progress;
                float endRadius;
                float intensity;
                bool isFlash = false;
                if (age < def.ringMainPulseSeconds)
                {
                    progress = age / def.ringMainPulseSeconds;
                    endRadius = def.ringMainEndRadiusCells;
                    intensity = def.ringIntensity * def.ringMainIntensityScale;
                }
                else if (ringHasFlash[i] && age < def.ringMainPulseSeconds + def.ringFlashDelaySeconds + def.ringFlashSeconds)
                {
                    float sinceFlash = age - def.ringMainPulseSeconds - def.ringFlashDelaySeconds;
                    if (sinceFlash < 0f)
                    {
                        continue;
                    }

                    progress = sinceFlash / def.ringFlashSeconds;
                    endRadius = def.ringFlashRadiusCells;
                    intensity = def.ringIntensity * def.ringFlashIntensityScale;
                    isFlash = true;
                }
                else
                {
                    continue;
                }

                float alpha = PulseAlpha(progress);
                if (alpha <= MinVisible)
                {
                    continue;
                }

                float radius = Mathf.Lerp(def.ringStartRadiusCells, endRadius, progress);
                var center = new Vector3(
                    beamOrigin.x + aimDirX * across,
                    beamOrigin.y + 0.02f,
                    beamOrigin.z + aimDirZ * across);

                Color baseCol = (i % 2 == 0 || isFlash) ? ringPrimaryColor : ringSecondaryColor;
                overMesh.AddTiltedEllipseRing(
                    center,
                    major,
                    minor,
                    radius,
                    ratio,
                    def.ringSegments,
                    def.ringThicknessCells,
                    new Color(baseCol.r, baseCol.g, baseCol.b, alpha),
                    intensity,
                    1.35f);
            }
        }

        private bool TryComputeRingBasis(Camera camera, out Vector3 minor, out Vector3 major)
        {
            var axis = new Vector3(aimDirX, 0f, aimDirZ);
            if (def.ringMode == RailgunRingMode.WorldPerpendicular)
            {
                minor = Vector3.Cross(axis, Vector3.up).normalized;
                major = Vector3.Cross(axis, minor).normalized;
                return true;
            }

            if (camera == null)
            {
                minor = major = Vector3.zero;
                return false;
            }

            Vector3 view = camera.transform.forward;
            Vector3 projected = axis - view * Vector3.Dot(axis, view);
            if (projected.sqrMagnitude < 0.0001f)
            {
                projected = camera.transform.right;
            }

            minor = projected.normalized;
            major = Vector3.Cross(minor, view).normalized;
            return true;
        }

        private void RefreshChargeFocal()
        {
            if (caster == null || !caster.Spawned || caster.Map != map)
            {
                return;
            }

            Vector3 casterPos = caster.DrawPos;
            if (!RailgunGeometry.TryDirection(casterPos.x, casterPos.z, aim.x, aim.z, out aimDirX, out aimDirZ))
            {
                IntVec3 facing = caster.Rotation.FacingCell;
                aimDirX = facing.x;
                aimDirZ = facing.z;
            }

            float focalY = SpellCastVfxMath.EvaluatePawnMaskAltitude(
                casterPos.z + aimDirZ * def.chargeOffsetCells,
                casterPos.z,
                def.zTransitionHalf,
                def.frontAltitudeY,
                def.backAltitudeY);

            chargeFocalPos = SpellCastVfxMath.ClampFocalPointBeforeWall(
                map,
                casterPos,
                aimDirX,
                aimDirZ,
                def.chargeOffsetCells,
                def.wallClearanceMargin,
                focalY);
        }

        private void StartChargeLumen()
        {
            if (!RimLumenApi.IsReady || def.chargeLumenEffectId.NullOrEmpty())
            {
                return;
            }

            float depthRefZ = caster != null ? caster.DrawPos.z : chargeFocalPos.z;

            chargeHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = def.chargeLumenEffectId,
                MapIndex = map != null ? map.uniqueID : -1,
                OriginWorld = new System.Numerics.Vector3(chargeFocalPos.x, def.chargeOriginHeightCells, chargeFocalPos.z),
                DepthReferenceZ = depthRefZ,
                RotationDegrees = Mathf.Atan2(aimDirX, aimDirZ) * Mathf.Rad2Deg,
                Follow = FollowChargeFocal,
                RealTime = false
            });
        }

        private bool FollowChargeFocal(out System.Numerics.Vector3 worldPosition, out float rotationDegrees)
        {
            RefreshChargeFocal();
            worldPosition = new System.Numerics.Vector3(chargeFocalPos.x, def.chargeOriginHeightCells, chargeFocalPos.z);
            rotationDegrees = Mathf.Atan2(aimDirX, aimDirZ) * Mathf.Rad2Deg;
            return !Finished && !fired && caster != null && caster.Spawned && caster.Map == map;
        }

        private void StartFireLumen()
        {
            if (!RimLumenApi.IsReady || def.fireLumenEffectId.NullOrEmpty())
            {
                return;
            }

            fireHandle = RimLumenApi.Play(new LumenPlayRequest
            {
                EffectId = def.fireLumenEffectId,
                MapIndex = map != null ? map.uniqueID : -1,
                OriginWorld = new System.Numerics.Vector3(beamOrigin.x, def.fireOriginHeightCells, beamOrigin.z),
                TargetWorld = new System.Numerics.Vector3(
                    beamOrigin.x + aimDirX * Mathf.Max(2f, beamLength),
                    def.fireOriginHeightCells,
                    beamOrigin.z + aimDirZ * Mathf.Max(2f, beamLength)),
                Scale = 1f,
                RealTime = false
            });
        }

        private void StartChargeAudio()
        {
            if (map != Find.CurrentMap || caster == null || !caster.Spawned || def.chargeSound == null)
            {
                return;
            }

            chargeSustainer = def.chargeSound.TrySpawnSustainer(
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

            float k = 1f - fade / def.beamFadeSeconds;
            return k * k;
        }

        private float GlowAlpha(float elapsed)
        {
            float hold = def.beamHoldSeconds + def.glowLingerSeconds;
            if (elapsed <= hold)
            {
                return 1f;
            }

            float fade = elapsed - hold;
            if (fade >= def.beamFadeSeconds)
            {
                return 0f;
            }

            float k = 1f - fade / def.beamFadeSeconds;
            return k * k;
        }

        private float PulseAlpha(float progress)
        {
            float rise = Mathf.Clamp01(def.ringPulseRise);
            float fall = Mathf.Clamp01(def.ringPulseFall);
            float up = rise <= 0f ? 1f : SmoothStep(progress / rise);
            float down = fall <= 0f ? 1f : SmoothStep((1f - progress) / fall);
            return Mathf.Clamp01(Mathf.Min(up, down));
        }

        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float RingRoll01(int seed, int index)
        {
            uint x = unchecked((uint)seed) ^ unchecked((uint)index * 0x9E3779B9u);
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x >> 8) * (1f / 16777216f);
        }

        private static float SecondsSince(int tick)
        {
            long delta = (long)Find.TickManager.TicksGame - tick;
            return delta <= 0 ? 0f : delta / (float)GenTicks.TicksPerRealSecond;
        }
    }
}
