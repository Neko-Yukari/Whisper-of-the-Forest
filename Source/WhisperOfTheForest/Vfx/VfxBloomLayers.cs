using System.Collections.Generic;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Shared claim on a RimUnity Bloom layer. Several effects can be on screen at once, and RimUnity's
    /// Bloom setting is a single bit per layer, so the layer is enabled when the first holder appears and
    /// released when the last one ends — never on every individual effect.
    /// </summary>
    internal static class VfxBloomLayers
    {
        private static readonly Dictionary<int, int> Holders = new Dictionary<int, int>();

        public static void Acquire(int layer)
        {
            Holders.TryGetValue(layer, out int count);
            Holders[layer] = count + 1;
            if (count == 0)
            {
                RimUnity.RimUnityMod.Bloom.SetLayerEnabled(layer, true);
            }
        }

        public static void Release(int layer)
        {
            if (!Holders.TryGetValue(layer, out int count) || count <= 0)
            {
                return;
            }

            if (count > 1)
            {
                Holders[layer] = count - 1;
                return;
            }

            Holders.Remove(layer);
            RimUnity.RimUnityMod.Bloom.ReleaseLayer(layer);
        }
    }
}
