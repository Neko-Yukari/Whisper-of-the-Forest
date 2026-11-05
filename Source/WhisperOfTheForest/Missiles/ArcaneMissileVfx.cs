using System;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Vfx;

namespace WhisperOfTheForest.Missiles
{
    /// <summary>
    /// A visual request for one missile effect. The look of a volley lives in the settings that were
    /// frozen when it was fired (colour, size, trail, wobble), so the worker reads them from here
    /// instead of from the Def. Everything else is the shared <see cref="VfxRequest"/> contract.
    /// </summary>
    public class ArcaneMissileVfxRequest : VfxRequest
    {
        public ArcaneMissileSettings settings;

        /// <summary>Debug probe only: seconds the effect should stay alive; 0 uses the Def's own lifetime.</summary>
        public float seconds;
    }

    /// <summary>
    /// The cosmetic weave and height bob drawn on top of the authoritative flight position. Shared by
    /// every missile backend, so switching visuals never changes gameplay or saved state.
    /// </summary>
    internal static class ArcaneMissileVfxPosition
    {
        public static void Of(ArcaneMissile missile, out float x, out float y, out float z)
        {
            ArcaneMissileSettings settings = missile.settings;
            float phase = missile.ageTicks * settings.wobbleFrequency / GenTicks.TicksPerRealSecond * MathF.PI * 2f
                + missile.wobblePhase;
            float wobble = MathF.Sin(phase) * settings.wobbleAmplitude;
            float bob = MathF.Cos(phase * 0.7f) * settings.wobbleAmplitude * 0.35f;

            x = missile.posX + missile.headingZ * wobble;
            z = missile.posZ - missile.headingX * wobble;
            y = MathF.Max(0.1f, settings.flightHeight + bob);
        }
    }
}
