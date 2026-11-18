using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    public enum RailgunRingMode
    {
        WorldPerpendicular,
        Ellipse30
    }

    /// <summary>
    /// Full RimLumen specification for the Railgun Super spell: front-of-caster charge implosion with
    /// 2.5D Pawn depth masking, selective Layer 15 map darkening, HDR beam + quadratic heat linger,
    /// 7 re-striking jagged bolts, 16 travelling elliptical shock/re-ionisation rings, corridor particle
    /// embers/lights and bespoke charge/fire SoundDefs.
    /// </summary>
    public sealed class RailgunLumenVfxDef : VfxDef
    {
        // ---- charge (front-of-caster focal convergence with 2.5D depth mask) ----------------
        public float chargeOffsetCells;
        public float wallClearanceMargin;
        public float backAltitudeY;
        public float frontAltitudeY;
        public float zTransitionHalf;
        public float chargeOriginHeightCells;
        public float chargeRadiusCells;
        public float chargeSpindleHalfHeight;
        public float chargeBaseRingThickness;
        public float chargeFunnelHeightCells;
        public int chargeVortexArms;
        public int chargeBoltCount;
        public string chargeColor;
        public string chargeCoreColor;
        public float chargeIntensity;
        public float chargePulseHz;
        public string chargeLumenEffectId;
        public SoundDef chargeSound;

        // ---- beam & heat linger (Layer 15 RibbonOver) ----------------------------------------
        public float beamAltitudeY;
        public float coreWidthCells;
        public string coreColor;
        public float coreIntensity;
        public float glowWidthCells;
        public string glowColor;
        public float glowIntensity;
        public string afterglowColor;
        public float afterglowIntensity;
        public float beamHoldSeconds;
        public float beamFadeSeconds;
        public float glowLingerSeconds;
        public float ribbonEdgePower;
        public float muzzleFlareRadius;

        // ---- 7 re-striking jagged bolts ------------------------------------------------------
        public string arcColor;
        public float arcIntensity;
        public int arcCount;
        public float arcMinLengthCells;
        public float arcMaxLengthCells;
        public float arcSegmentsPerCell;
        public int arcMaxSegments;
        public float arcRadiusCells;
        public float arcJitterCells;
        public float arcAngleJitterDegrees;
        public float arcThicknessCells;
        public float arcLifeSeconds;
        public float arcGapSeconds;

        // ---- 16 travelling elliptical shock & re-ionisation rings ----------------------------
        public RailgunRingMode ringMode;
        public int ringCount;
        public int ringSegments;
        public float ringShockArrivalFraction;
        public float ringMainPulseSeconds;
        public float ringPulseRise;
        public float ringPulseFall;
        public float ringStartRadiusCells;
        public float ringMainEndRadiusCells;
        public float ringFlashChance;
        public float ringFlashDelaySeconds;
        public float ringFlashSeconds;
        public float ringFlashRadiusCells;
        public float ringThicknessCells;
        public string ringColor;
        public string ringSecondaryColor;
        public float ringIntensity;
        public float ringMainIntensityScale;
        public float ringFlashIntensityScale;
        public float ringEllipseRatio;

        // ---- RimLumen fire particles/screenDim/lights & sound --------------------------------
        public float fireOriginHeightCells;
        public string fireLumenEffectId;
        public SoundDef fireSound;

        public override bool SupportsState(string state) => state == VfxStates.Release;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (chargeOffsetCells < 0f || wallClearanceMargin < 0f || backAltitudeY <= 0f || frontAltitudeY <= backAltitudeY
                || zTransitionHalf <= 0f || chargeOriginHeightCells < 0f || fireOriginHeightCells < 0f)
            {
                yield return "invalid charge offset, RimLumen ground-height origins or 2.5D depth masking altitudes";
            }

            if (chargeRadiusCells <= 0f || chargeSpindleHalfHeight <= 0f || chargeVortexArms < 1 || chargeBoltCount < 0
                || chargeBaseRingThickness <= 0f || chargeFunnelHeightCells <= 0f
                || chargeIntensity <= 0f || chargePulseHz <= 0f)
            {
                yield return "charge geometry, intensity and pulseHz must be positive";
            }

            if (chargeLumenEffectId.NullOrEmpty() || fireLumenEffectId.NullOrEmpty())
            {
                yield return "chargeLumenEffectId and fireLumenEffectId are required";
            }

            if (chargeSound == null || fireSound == null)
            {
                yield return "chargeSound and fireSound are required";
            }

            if (beamAltitudeY <= 0f || coreWidthCells <= 0f || glowWidthCells <= 0f || coreIntensity <= 0f
                || glowIntensity <= 0f || afterglowIntensity <= 0f || ribbonEdgePower <= 0f || muzzleFlareRadius <= 0f)
            {
                yield return "beam geometry and HDR intensities must be positive";
            }

            if (beamHoldSeconds < 0f || glowLingerSeconds < 0f || beamFadeSeconds <= 0f)
            {
                yield return "beamFadeSeconds must be positive and hold/linger must not be negative";
            }

            if (arcCount < 0 || arcMaxSegments < 2 || arcSegmentsPerCell <= 0f || arcThicknessCells <= 0f
                || arcMinLengthCells <= 0f || arcMaxLengthCells < arcMinLengthCells
                || arcRadiusCells <= 0f || arcJitterCells < 0f || arcAngleJitterDegrees < 0f
                || arcIntensity <= 0f || arcLifeSeconds <= 0f || arcGapSeconds < 0f)
            {
                yield return "invalid arc parameters";
            }

            if (ringCount < 1 || ringSegments < 8 || ringShockArrivalFraction <= 0f || ringShockArrivalFraction > 1f
                || ringMainPulseSeconds <= 0f || ringPulseRise <= 0f || ringPulseRise > 1f
                || ringPulseFall <= 0f || ringPulseFall > 1f || ringStartRadiusCells <= 0f
                || ringMainEndRadiusCells <= 0f || ringFlashRadiusCells <= 0f || ringThicknessCells <= 0f
                || ringFlashChance < 0f || ringFlashChance > 1f || ringFlashDelaySeconds < 0f || ringFlashSeconds <= 0f
                || ringIntensity <= 0f || ringMainIntensityScale <= 0f || ringFlashIntensityScale <= 0f
                || (ringMode == RailgunRingMode.Ellipse30 && (ringEllipseRatio <= 0f || ringEllipseRatio > 1f)))
            {
                yield return "invalid shock ring parameters";
            }

            foreach (string hexError in ValidateColors())
            {
                yield return hexError;
            }
        }

        private IEnumerable<string> ValidateColors()
        {
            if (!VfxColors.IsValid(chargeColor)) yield return "chargeColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(chargeCoreColor)) yield return "chargeCoreColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(coreColor)) yield return "coreColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(glowColor)) yield return "glowColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(afterglowColor)) yield return "afterglowColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(arcColor)) yield return "arcColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(ringColor)) yield return "ringColor is not " + VfxColors.Format;
            if (!VfxColors.IsValid(ringSecondaryColor)) yield return "ringSecondaryColor is not " + VfxColors.Format;
        }
    }
}
