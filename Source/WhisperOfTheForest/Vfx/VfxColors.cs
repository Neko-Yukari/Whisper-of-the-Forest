using System;
using System.Globalization;
using RimUnity;

namespace WhisperOfTheForest.Experimental.Vfx
{
    /// <summary>
    /// The visual layer's only colour parser: "#RRGGBB" or "#RRGGBBAA" (leading hash optional).
    /// It never falls back to another colour. A Def validates its own strings with <see cref="IsValid"/>
    /// in ConfigErrors, so a bad value fails at load instead of being silently replaced on screen.
    /// </summary>
    public static class VfxColors
    {
        public const string Format = "\"#RRGGBB\" or \"#RRGGBBAA\" (6 or 8 hex digits)";

        public static bool IsValid(string hex)
        {
            return TryParse(hex, out RimUnity.Color unused);
        }

        public static RimUnity.Color Parse(string hex)
        {
            if (!TryParse(hex, out RimUnity.Color color))
            {
                throw new FormatException("colour \"" + (hex ?? "null") + "\" is not " + Format);
            }

            return color;
        }

        private static bool TryParse(string hex, out RimUnity.Color color)
        {
            color = default(RimUnity.Color);
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            string digits = hex.Trim().TrimStart('#');
            if (digits.Length != 6 && digits.Length != 8)
            {
                return false;
            }

            if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                return false;
            }

            if (digits.Length == 6)
            {
                color = new RimUnity.Color(
                    ((value >> 16) & 0xFF) / 255f,
                    ((value >> 8) & 0xFF) / 255f,
                    (value & 0xFF) / 255f,
                    1f);
                return true;
            }

            color = new RimUnity.Color(
                ((value >> 24) & 0xFF) / 255f,
                ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f,
                (value & 0xFF) / 255f);
            return true;
        }
    }
}
