using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Vfx
{
    /// <summary>
    /// Data-driven visual and audio theme bound to spell Effect/Super glyphs. Adding a new magic
    /// school only requires declaring a new SpellThemeDef and binding glyphs to it in XML.
    /// </summary>
    public class SpellThemeDef : Def
    {
        public string coreColor;
        public string primaryColor;
        public string secondaryColor;
        public string lightColor;
        public float hdrIntensity;
        public string castLumenEffectId;
        public string quickCastLumenEffectId;
        public SoundDef castChargeSound;
        public SoundDef castReleaseSound;

        public Color CoreColorUnity => VfxColors.ParseUnity(coreColor);
        public Color PrimaryColorUnity => VfxColors.ParseUnity(primaryColor);
        public Color SecondaryColorUnity => VfxColors.ParseUnity(secondaryColor);
        public Color LightColorUnity => VfxColors.ParseUnity(lightColor);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (!VfxColors.IsValid(coreColor))
            {
                yield return "coreColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(primaryColor))
            {
                yield return "primaryColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(secondaryColor))
            {
                yield return "secondaryColor is not " + VfxColors.Format;
            }

            if (!VfxColors.IsValid(lightColor))
            {
                yield return "lightColor is not " + VfxColors.Format;
            }

            if (hdrIntensity <= 0f)
            {
                yield return "hdrIntensity must be positive";
            }

            if (castLumenEffectId.NullOrEmpty())
            {
                yield return "castLumenEffectId is required";
            }

            if (quickCastLumenEffectId.NullOrEmpty())
            {
                yield return "quickCastLumenEffectId is required";
            }

            if (castChargeSound == null)
            {
                yield return "castChargeSound is required";
            }

            if (castReleaseSound == null)
            {
                yield return "castReleaseSound is required";
            }
        }
    }
}
