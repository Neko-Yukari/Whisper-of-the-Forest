using System.Collections.Generic;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// XML tuning parameters for the DD2-inspired spell-casting vortex, vertical core spindle,
    /// 2.5D pawn depth masking and release flash.
    /// </summary>
    public sealed class SpellCastVfxDef : VfxDef
    {
        public float forwardOffsetCells;
        public float wallClearanceMargin;
        public float backAltitudeY;
        public float frontAltitudeY;
        public float zTransitionHalf;
        public float lumenOriginHeightCells;

        public int vortexArmCount;
        public int vortexSegmentsPerArm;
        public float vortexStartRadius;
        public float vortexMinRadius;
        public float vortexEllipseAspectZ;
        public float vortexRibbonWidth;
        public float vortexSpinRadPerSec;
        public float vortexTwistTurns;
        public int outerVortexRingCount;
        public float baseRingThickness;
        public float funnelHeightCells;

        public float spindleHalfHeightStart;
        public float spindleHalfHeightMin;
        public float spindleWidthStart;
        public float spindleWidthMin;
        public int spindleSegments;

        public float leadCollapseSeconds;
        public float releaseFlashDurationSeconds;
        public float releaseFlashRadius;
        public float hdrCoreMultiplier;
        public float hdrVortexMultiplier;
        public float releaseFlashHdrMultiplier;
        public float ribbonEdgePower;
        public float coreDiscRadiusStart;
        public float coreDiscRadiusMin;

        public override bool SupportsState(string state) => state == VfxStates.Release;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (forwardOffsetCells < 0f || wallClearanceMargin < 0f)
            {
                yield return "forwardOffsetCells and wallClearanceMargin must not be negative";
            }

            if (backAltitudeY <= 0f || frontAltitudeY <= backAltitudeY || zTransitionHalf <= 0f || lumenOriginHeightCells < 0f)
            {
                yield return "depth masking altitudes require 0 < backAltitudeY < frontAltitudeY, positive zTransitionHalf and non-negative lumenOriginHeightCells";
            }

            if (vortexArmCount < 1 || vortexSegmentsPerArm < 4 || vortexStartRadius <= 0f || vortexMinRadius <= 0f
                || vortexEllipseAspectZ <= 0f || vortexEllipseAspectZ > 1.5f || vortexRibbonWidth <= 0f
                || vortexTwistTurns <= 0f || outerVortexRingCount < 0
                || baseRingThickness <= 0f || funnelHeightCells <= 0f)
            {
                yield return "vortex geometry parameters must be valid positive values";
            }

            if (coreDiscRadiusStart <= 0f || coreDiscRadiusMin <= 0f)
            {
                yield return "coreDiscRadiusStart and coreDiscRadiusMin must be positive";
            }

            if (spindleHalfHeightStart <= 0f || spindleHalfHeightMin <= 0f || spindleWidthStart <= 0f
                || spindleWidthMin <= 0f || spindleSegments < 4)
            {
                yield return "spindle geometry parameters must be positive";
            }

            if (leadCollapseSeconds < 0f || releaseFlashDurationSeconds <= 0f || releaseFlashRadius <= 0f)
            {
                yield return "collapse and release flash timings/radius must be valid positive values";
            }

            if (hdrCoreMultiplier <= 0f || hdrVortexMultiplier <= 0f || releaseFlashHdrMultiplier <= 0f || ribbonEdgePower <= 0f)
            {
                yield return "HDR multipliers and ribbonEdgePower must be positive";
            }
        }
    }
}
