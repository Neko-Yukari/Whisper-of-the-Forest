using System.Collections.Generic;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// An effect authored in Unity and exported with the RimUnity exporter into this mod's
    /// <c>1.7/Data/RimUnity</c>. The look lives in the prefab; the Def only says which prefab, where
    /// above the ground and for how long. Authoring steps: <c>docs/rimunity-vfx-workflow.md</c>.
    /// </summary>
    public class PrefabVfxDef : VfxDef
    {
        /// <summary>Logical path the prefab was exported under, e.g. <c>Effects/Probe</c>.</summary>
        public string prefabPath;

        /// <summary>World height of the prefab root; above ~1 so map geometry does not clip it.</summary>
        public float height;

        /// <summary>Game ticks the effect lives. Required for a fixed-duration effect.</summary>
        public int durationTicks;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (string.IsNullOrEmpty(prefabPath))
            {
                yield return "a PrefabVfxDef needs a prefabPath";
            }

            if (lifetime == VfxLifetime.FixedDuration && durationTicks <= 0)
            {
                yield return "a fixed-duration PrefabVfxDef needs durationTicks > 0";
            }
        }
    }
}
