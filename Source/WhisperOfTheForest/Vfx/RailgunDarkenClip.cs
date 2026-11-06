using System;
using RimUnity;
using RimUnity.ScreenEfx;
using Mathf = UnityEngine.Mathf;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// One screen-space darkening pass whose strength is read every frame, so the ramp follows the
    /// effect's own tick clock instead of being frozen at construction.
    ///
    /// The clip owns a private material copy and destroys it in <see cref="OnRelease"/>; RimUnity keeps
    /// rendering queued frames after <c>Stop</c>, so the copy must outlive the handle.
    /// </summary>
    internal sealed class RailgunDarkenClip : EfxClip
    {
        private readonly Material material;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly Func<float> strength;
        private readonly float keepLow;
        private readonly float keepHigh;

        public RailgunDarkenClip(Material template, Func<float> strength, float keepLow, float keepHigh)
        {
            material = new Material(template);
            this.strength = strength;
            this.keepLow = keepLow;
            this.keepHigh = keepHigh;
        }

        public override void BuildFrame(EfxFrameBuilder frame)
        {
            float current = Mathf.Clamp01(strength());
            if (current <= 0.001f)
            {
                return;
            }

            properties.SetFloat("_Strength", current);
            properties.SetFloat("_KeepLow", keepLow);
            properties.SetFloat("_KeepHigh", keepHigh);
            frame.AddPass(material, 0, properties);
        }

        protected override void OnRelease()
        {
            RimUnity.Object.Destroy(material);
        }
    }
}
