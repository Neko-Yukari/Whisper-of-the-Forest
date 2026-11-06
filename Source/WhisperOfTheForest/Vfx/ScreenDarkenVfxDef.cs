using System.Collections.Generic;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>Parameters for a luminance-selective full-screen darkening clip.</summary>
    public class ScreenDarkenVfxDef : VfxDef
    {
        public string materialPath;
        public float strength;
        public float keepLow;
        public float keepHigh;
        public int durationTicks;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (string.IsNullOrEmpty(materialPath))
            {
                yield return "a ScreenDarkenVfxDef needs a materialPath";
            }

            if (strength < 0f || strength > 1f)
            {
                yield return "strength must be in [0, 1]";
            }

            if (keepLow < 0f || keepHigh <= keepLow)
            {
                yield return "keepHigh must be greater than keepLow >= 0";
            }

            if (durationTicks <= 0)
            {
                yield return "durationTicks must be positive";
            }
        }
    }
}
