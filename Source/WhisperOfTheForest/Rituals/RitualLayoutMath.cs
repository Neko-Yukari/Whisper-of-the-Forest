using UnityEngine;
using Verse;

namespace WhisperOfTheForest.Rituals
{
    /// <summary>
    /// Pure, asset-free layout math shared by the ritual host UI and offline seam tests:
    /// adaptive offering/mana chip sizing (R1) and North-up chalk diagram coordinates (R3).
    /// </summary>
    public static class RitualLayoutMath
    {
        public const float ChipPadLeft = 6f;
        public const float ChipIconSize = 24f;
        public const float ChipIconGap = 6f;
        public const float ChipLabelCountGap = 6f;
        public const float ChipPadRight = 8f;
        public const float ChipMinWidth = 86f;
        public const float TextSafetyMargin = 2f;

        /// <summary>
        /// Computes chip width from measured single-line text widths using the exact same
        /// padding, icon, and gap constants used when drawing the chip chrome.
        /// </summary>
        public static float MeasureChipWidth(float rawLabelW, float rawCountW, bool hasCount, float maxWidth)
        {
            float labelW = rawLabelW > 0f ? Mathf.Ceil(rawLabelW) + TextSafetyMargin : 0f;
            float countW = hasCount && rawCountW > 0f ? Mathf.Ceil(rawCountW) + TextSafetyMargin : 0f;
            float w = ChipPadLeft + ChipIconSize
                + (labelW > 0f ? ChipIconGap + labelW : 0f)
                + (countW > 0f ? ChipLabelCountGap + countW : 0f)
                + ChipPadRight;
            float safeMax = Mathf.Max(1f, maxWidth);
            float minW = Mathf.Min(ChipMinWidth, safeMax);
            return Mathf.Clamp(w, minW, safeMax);
        }

        /// <summary>
        /// Computes exact label and count rectangles inside a chip of width <paramref name="chipW"/>.
        /// Sets <paramref name="needsEllipsis"/> strictly when the needed label width exceeds
        /// <paramref name="availLabelW"/>, so fitting labels never hit vanilla LabelEllipses's -13f clamp.
        /// </summary>
        public static void ComputeChipTextLayout(
            float chipX,
            float chipW,
            float rawLabelW,
            float rawCountW,
            bool hasLabel,
            bool hasCount,
            out float labelX,
            out float availLabelW,
            out bool needsEllipsis,
            out float countX,
            out float countBoxW)
        {
            float contentLeft = chipX + ChipPadLeft + ChipIconSize + ChipIconGap;
            float maxCountBoxW = Mathf.Max(0f, chipX + chipW - ChipPadRight - contentLeft);
            float rawCountBoxW = hasCount && rawCountW > 0f ? Mathf.Ceil(rawCountW) + TextSafetyMargin : 0f;
            countBoxW = Mathf.Min(rawCountBoxW, maxCountBoxW);
            float reservedRight = countBoxW > 0f
                ? ChipLabelCountGap + countBoxW + ChipPadRight
                : ChipPadRight;

            labelX = contentLeft;
            availLabelW = Mathf.Max(0f, chipX + chipW - contentLeft - reservedRight);

            float neededLabelW = hasLabel && rawLabelW > 0f ? Mathf.Ceil(rawLabelW) + TextSafetyMargin : 0f;
            needsEllipsis = hasLabel && neededLabelW > availLabelW + 0.01f;

            countX = Mathf.Max(contentLeft, chipX + chipW - ChipPadRight - countBoxW);
        }

        /// <summary>
        /// Maps IMGUI diagram grid coordinates (row 0 = top, col 0 = left) to RimWorld map
        /// offsets around the altar: North (+Z) at top (row = 0), West (-X) at left (col = 0).
        /// </summary>
        public static IntVec3 DiagramOffset(int row, int col, int radius)
        {
            int dx = col - radius;
            int dz = radius - row;
            return new IntVec3(dx, 0, dz);
        }
    }
}
