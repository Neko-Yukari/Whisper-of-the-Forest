using System.Collections.Generic;
using Verse;

namespace WhisperOfTheForest.Experimental.Vfx
{
    /// <summary>
    /// Everything that makes the railgun look like a railgun, and nothing a spell can read: beam and
    /// core widths, the two colour layers, the arcs, the afterglow and the full-screen darkening.
    ///
    /// These numbers are the effect's own. The spell only says where the shot goes, how wide the
    /// damage corridor is and how long the charge lasts. Every value is written in XML; this class
    /// declares no duplicate default, and an omitted value fails in <see cref="ConfigErrors"/>.
    /// </summary>
    public class RailgunVfxDef : VfxDef
    {
        // ---- beam ---------------------------------------------------------------------------

        /// <summary>White-hot core width in cells; the brightest part of the beam.</summary>
        public float coreWidthCells;

        public string coreColor;

        /// <summary>Outer glow width in cells; wider than the core so the two layers read separately.</summary>
        public float glowWidthCells;

        public string glowColor;

        /// <summary>Height above the ground the beam is drawn at, in world units.</summary>
        public float beamHeight;

        // ---- timing -------------------------------------------------------------------------

        /// <summary>Seconds the beam stays at full strength after the shot.</summary>
        public float beamHoldSeconds;

        /// <summary>Seconds the beam takes to fade out.</summary>
        public float beamFadeSeconds;

        // ---- afterglow ----------------------------------------------------------------------

        /// <summary>Seconds the residual band keeps shrinking after the beam has faded.</summary>
        public float afterglowSeconds;

        /// <summary>Width of the residual band at the moment the beam has fully faded, in cells.</summary>
        public float afterglowWidthCells;

        public string afterglowColor;

        // ---- arcs ---------------------------------------------------------------------------

        public string arcColor;

        /// <summary>How many jittered arcs run along the beam at once.</summary>
        public int arcCount;

        public float arcWidthCells;

        /// <summary>Maximum sideways wander of an arc, in cells.</summary>
        public float arcAmplitudeCells;

        /// <summary>How often the arcs are re-rolled, in hertz.</summary>
        public float arcFps;

        // ---- full-screen darkening ----------------------------------------------------------

        /// <summary>Peak opacity of the darkening pass; 0 disables it.</summary>
        public float darkenStrength;

        public string darkenTint;

        public float darkenInSeconds;

        public float darkenHoldSeconds;

        public float darkenOutSeconds;

        // ---- charge -------------------------------------------------------------------------

        /// <summary>Radius of the charging orb at its brightest, in cells.</summary>
        public float chargeRadiusCells;

        public string chargeColor;

        /// <summary>Pulses per second of the charging orb.</summary>
        public float chargePulseHz;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (coreWidthCells <= 0f || glowWidthCells <= 0f || afterglowWidthCells <= 0f)
            {
                yield return "core, glow and afterglow widths must be positive";
            }

            if (beamHeight < 0f)
            {
                yield return "beamHeight must not be negative";
            }

            if (beamHoldSeconds < 0f || beamFadeSeconds < 0f || afterglowSeconds < 0f)
            {
                yield return "beam and afterglow durations must not be negative";
            }

            if (darkenStrength < 0f || darkenStrength > 1f)
            {
                yield return "darkenStrength must be between 0 and 1";
            }

            if (darkenInSeconds < 0f || darkenHoldSeconds < 0f || darkenOutSeconds < 0f)
            {
                yield return "darkening durations must not be negative";
            }

            if (arcCount < 0 || arcWidthCells < 0f || arcAmplitudeCells < 0f)
            {
                yield return "arc count, width and amplitude must not be negative";
            }

            if (arcFps <= 0f)
            {
                yield return "arcFps must be positive";
            }

            if (chargeRadiusCells <= 0f)
            {
                yield return "chargeRadiusCells must be positive";
            }

            if (chargePulseHz <= 0f)
            {
                yield return "chargePulseHz must be positive";
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

            if (!VfxColors.IsValid(afterglowColor))
            {
                yield return "afterglowColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(arcColor))
            {
                yield return "arcColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(darkenTint))
            {
                yield return "darkenTint is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(chargeColor))
            {
                yield return "chargeColor is not " + VfxColors.Format;
            }
        }
    }
}
