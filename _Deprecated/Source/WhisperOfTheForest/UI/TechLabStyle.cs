using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Shared palette, metrics and icon lookup for the tech lab UI.
    /// The static ctor only builds colors; the texture itself is resolved lazily on the UI thread.
    /// The attribute is required because the type holds a static Texture2D field
    /// (Verse.StaticConstructorOnStartupUtility warns otherwise).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class TechLabStyle
    {
        public const float EntrySize = 44f;
        public const float EntryHideButtonSize = 16f;
        public const float EntryDragThreshold = 5f;

        public static readonly Color EntryFill = new Color(0.07f, 0.13f, 0.11f, 0.94f);
        public static readonly Color EntryFillHover = new Color(0.11f, 0.21f, 0.17f, 0.97f);
        public static readonly Color HideFill = new Color(0.16f, 0.16f, 0.16f, 0.80f);
        public static readonly Color HideFillHover = new Color(0.47f, 0.16f, 0.15f, 0.96f);
        public static readonly Color Accent = new Color(0.62f, 0.85f, 0.68f, 1f);
        public static readonly Color AccentDim = new Color(0.42f, 0.62f, 0.48f, 0.80f);
        public static readonly Color CardFill = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color Hint = new Color(0.75f, 0.77f, 0.73f, 1f);
        public static readonly Color ErrorText = new Color(0.95f, 0.55f, 0.48f, 1f);

        private static Texture2D icon;
        private static bool iconResolved;

        /// <summary>Entry/main-button icon. Null when the texture is missing, callers then fall back to a glyph.</summary>
        public static Texture2D Icon
        {
            get
            {
                if (!iconResolved)
                {
                    iconResolved = true;
                    icon = ContentFinder<Texture2D>.Get("Experiments/UI/TechLabIcon", false);
                }

                return icon;
            }
        }

        public static string Glyph => Translator.Translate("WotF_UI.EntryGlyph");
    }
}
