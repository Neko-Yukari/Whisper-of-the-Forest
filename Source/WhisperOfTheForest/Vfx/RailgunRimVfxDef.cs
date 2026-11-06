using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>How a shock ring is drawn in the world.</summary>
    public enum RailgunRingMode
    {
        /// <summary>
        /// A real ring in space: its plane is perpendicular to the beam axis, and the camera projects it.
        /// Read from the side it collapses to a line, exactly as a physical ring would.
        /// </summary>
        WorldPerpendicular,

        /// <summary>
        /// The same ring pre-squashed along the beam's on-screen direction to a fixed minor/major ratio,
        /// so it always reads the way a ring seen at about thirty degrees does.
        /// </summary>
        Ellipse30
    }

    /// <summary>
    /// The railgun as drawn by RimUnity world geometry: a white-hot core, a warm glow, jagged arcs that
    /// wrap the beam and a train of shock rings, plus the map darkening that keeps the beam the brightest
    /// thing on screen.
    ///
    /// Every number here is written in XML and validated by <see cref="ConfigErrors"/>; this class holds
    /// no duplicate default. The spell still only sends a direction, a length and a charge time.
    /// </summary>
    public class RailgunRimVfxDef : VfxDef
    {
        // ---- assets -------------------------------------------------------------------------

        /// <summary>Exported logical path of the shared additive ribbon material.</summary>
        public string materialPath;

        /// <summary>Exported logical path of the luminance-selective darkening material.</summary>
        public string darkenMaterialPath;

        /// <summary>RimUnity Bloom layer the effect draws on; the worker takes and releases it.</summary>
        public int bloomLayer;

        // ---- beam ---------------------------------------------------------------------------

        /// <summary>World height of the beam axis above the ground.</summary>
        public float beamHeight;

        /// <summary>Width of the white-hot core, in cells.</summary>
        public float coreWidthCells;

        public string coreColor;

        /// <summary>Brightness multiplier applied to the core colour; above 1 pushes it into Bloom.</summary>
        public float coreIntensity;

        /// <summary>Width of the warm glow around the core, in cells.</summary>
        public float glowWidthCells;

        public string glowColor;

        public float glowIntensity;

        /// <summary>Seconds the beam holds at full strength after the shot.</summary>
        public float beamHoldSeconds;

        /// <summary>Seconds the beam takes to fade out.</summary>
        public float beamFadeSeconds;

        /// <summary>Extra seconds the glow lingers after the core starts fading.</summary>
        public float glowLingerSeconds;

        // ---- arcs ---------------------------------------------------------------------------

        public string arcColor;

        public float arcIntensity;

        /// <summary>How many independent bolts exist; each one is somewhere on the beam at any moment.</summary>
        public int arcCount;

        /// <summary>Shortest stretch of beam one bolt covers, in cells.</summary>
        public float arcMinLengthCells;

        /// <summary>Longest stretch of beam one bolt covers, in cells.</summary>
        public float arcMaxLengthCells;

        /// <summary>Bolt points per world cell of its own length; keeps a long shot as jagged as a short one.</summary>
        public float arcSegmentsPerCell;

        /// <summary>Ceiling on points per bolt.</summary>
        public int arcMaxSegments;

        /// <summary>Distance a bolt sits from the beam axis, in cells.</summary>
        public float arcRadiusCells;

        /// <summary>How far each point may wander off that distance, in cells. This is the zigzag.</summary>
        public float arcJitterCells;

        /// <summary>Per-point random swing around the axis, in degrees.</summary>
        public float arcAngleJitterDegrees;

        public float arcThicknessCells;

        /// <summary>Seconds one bolt stays visible.</summary>
        public float arcLifeSeconds;

        /// <summary>Seconds one bolt stays gone before it strikes somewhere else.</summary>
        public float arcGapSeconds;

        // ---- shock rings --------------------------------------------------------------------

        public RailgunRingMode ringMode;

        public int ringCount;

        /// <summary>Points per ring; the last point repeats the first to close it.</summary>
        public int ringSegments;

        /// <summary>
        /// Where the shock front must be at the end of the beam, as a fraction of beamHoldSeconds. The
        /// speed is derived from the beam length and this window, so even the longest map diagonal is
        /// reached before the beam starts to fade, and a short shot takes the same time as a long one.
        /// </summary>
        public float ringShockArrivalFraction;

        /// <summary>Seconds the main pulse of a ring lasts, expanding and decaying the whole time.</summary>
        public float ringMainPulseSeconds;

        /// <summary>Fraction of the main pulse spent fading in; the ring is dim while it is still inside the glow.</summary>
        public float ringPulseRise;

        /// <summary>Fraction of the main pulse spent fading out.</summary>
        public float ringPulseFall;

        public float ringStartRadiusCells;

        /// <summary>Radius the main pulse expands to.</summary>
        public float ringMainEndRadiusCells;

        /// <summary>Chance that a ring gets a short re-ionisation flash after its main pulse, 0 to 1.</summary>
        public float ringFlashChance;

        /// <summary>Seconds between the end of the main pulse and a re-ionisation flash.</summary>
        public float ringFlashDelaySeconds;

        /// <summary>Seconds a re-ionisation flash lasts.</summary>
        public float ringFlashSeconds;

        /// <summary>Radius a re-ionisation flash expands to.</summary>
        public float ringFlashRadiusCells;

        public float ringThicknessCells;

        public string ringColor;

        /// <summary>Base brightness of a ring; each pulse multiplies it by its own scale.</summary>
        public float ringIntensity;

        public float ringMainIntensityScale;

        public float ringFlashIntensityScale;

        /// <summary>Minor/major axis ratio used by <see cref="RailgunRingMode.Ellipse30"/>; sin 30 = 0.5.</summary>
        public float ringEllipseRatio;

        // ---- charge -------------------------------------------------------------------------

        public float chargeRadiusCells;

        public string chargeColor;

        public float chargeIntensity;

        /// <summary>Pulses per second of the gathering orb.</summary>
        public float chargePulseHz;

        // ---- darkening ----------------------------------------------------------------------

        /// <summary>Peak strength of the selective darkening; 0 disables the clip entirely.</summary>
        public float darkenStrength;

        public float darkenInSeconds;

        public float darkenHoldSeconds;

        public float darkenOutSeconds;

        /// <summary>Luminance kept at full brightness; the beam and its glow sit above this.</summary>
        public float darkenKeepLow;

        public float darkenKeepHigh;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (string.IsNullOrEmpty(materialPath))
            {
                yield return "a RailgunRimVfxDef needs a materialPath";
            }

            if (darkenStrength > 0f && string.IsNullOrEmpty(darkenMaterialPath))
            {
                yield return "a RailgunRimVfxDef with darkenStrength > 0 needs a darkenMaterialPath";
            }

            if (bloomLayer < 0 || bloomLayer > 31)
            {
                yield return "bloomLayer must be a layer number between 0 and 31";
            }

            if (beamHeight < 0f)
            {
                yield return "beamHeight must not be negative";
            }

            if (coreWidthCells <= 0f || glowWidthCells <= 0f)
            {
                yield return "core and glow widths must be positive";
            }

            if (coreIntensity <= 0f || glowIntensity <= 0f)
            {
                yield return "core and glow intensities must be positive";
            }

            if (beamHoldSeconds < 0f || glowLingerSeconds < 0f || beamFadeSeconds <= 0f)
            {
                yield return "beamFadeSeconds must be positive and the other beam durations must not be negative";
            }

            if (arcCount < 0 || arcMaxSegments < 2 || arcSegmentsPerCell <= 0f || arcThicknessCells <= 0f)
            {
                yield return "bolts need arcCount >= 0, arcMaxSegments >= 2, a positive arcSegmentsPerCell and a positive thickness";
            }

            if (arcMinLengthCells <= 0f || arcMaxLengthCells < arcMinLengthCells)
            {
                yield return "bolt lengths need 0 < arcMinLengthCells <= arcMaxLengthCells";
            }

            if (arcRadiusCells <= 0f || arcJitterCells < 0f || arcIntensity <= 0f)
            {
                yield return "bolt radius and intensity must be positive and the jitter must not be negative";
            }

            if (arcAngleJitterDegrees < 0f)
            {
                yield return "arcAngleJitterDegrees must not be negative";
            }

            if (arcLifeSeconds <= 0f || arcGapSeconds < 0f)
            {
                yield return "a bolt needs a positive arcLifeSeconds and a non-negative arcGapSeconds";
            }

            if (ringCount < 1 || ringSegments < 8)
            {
                yield return "rings need ringCount >= 1 and ringSegments >= 8";
            }

            if (ringShockArrivalFraction <= 0f || ringShockArrivalFraction > 1f)
            {
                yield return "ringShockArrivalFraction must be in (0, 1]";
            }

            if (ringPulseRise <= 0f || ringPulseRise > 1f || ringPulseFall <= 0f || ringPulseFall > 1f)
            {
                yield return "ringPulseRise and ringPulseFall must be in (0, 1]";
            }

            if (ringMainPulseSeconds <= 0f)
            {
                yield return "ringMainPulseSeconds must be positive";
            }

            if (ringFlashDelaySeconds < 0f || ringFlashSeconds <= 0f)
            {
                yield return "a re-ionisation flash needs a non-negative delay and a positive duration";
            }

            if (ringFlashChance < 0f || ringFlashChance > 1f)
            {
                yield return "ringFlashChance must be between 0 and 1";
            }

            if (ringStartRadiusCells <= 0f || ringMainEndRadiusCells <= 0f || ringFlashRadiusCells <= 0f
                || ringThicknessCells <= 0f)
            {
                yield return "ring radii and thickness must be positive";
            }

            if (ringIntensity <= 0f || ringMainIntensityScale <= 0f || ringFlashIntensityScale <= 0f)
            {
                yield return "ring intensity and both pulse scales must be positive";
            }

            if (ringMode == RailgunRingMode.Ellipse30 && (ringEllipseRatio <= 0f || ringEllipseRatio > 1f))
            {
                yield return "ringEllipseRatio must be in (0, 1] for Ellipse30";
            }

            if (chargeRadiusCells <= 0f || chargeIntensity <= 0f || chargePulseHz <= 0f)
            {
                yield return "charge radius, intensity and pulse must be positive";
            }

            if (darkenStrength < 0f || darkenStrength > 1f)
            {
                yield return "darkenStrength must be between 0 and 1";
            }

            if (darkenInSeconds < 0f || darkenHoldSeconds < 0f || darkenOutSeconds <= 0f)
            {
                yield return "darkening needs non-negative in/hold and a positive out duration";
            }

            if (darkenKeepHigh <= darkenKeepLow)
            {
                yield return "darkenKeepHigh must be greater than darkenKeepLow";
            }

            foreach (string error in ColorErrors())
            {
                yield return error;
            }
        }

        private IEnumerable<string> ColorErrors()
        {
            if (!VfxColors.IsValid(coreColor))
            {
                yield return "coreColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(glowColor))
            {
                yield return "glowColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(arcColor))
            {
                yield return "arcColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(ringColor))
            {
                yield return "ringColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(chargeColor))
            {
                yield return "chargeColor is not " + VfxColors.Format;
            }
        }
    }
}
