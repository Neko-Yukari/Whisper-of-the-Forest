using System;
using System.Globalization;
using UnityColor = UnityEngine.Color;

namespace WhisperOfTheForest.Vfx
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
            return TryParseComponents(hex, out _, out _, out _, out _);
        }

        public static UnityColor ParseUnity(string hex)
        {
            if (!TryParseComponents(hex, out float r, out float g, out float b, out float a))
            {
                throw new FormatException("colour \"" + (hex ?? "null") + "\" is not " + Format);
            }

            return new UnityColor(r, g, b, a);
        }

        private static bool TryParseComponents(string hex, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
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
                r = ((value >> 16) & 0xFF) / 255f;
                g = ((value >> 8) & 0xFF) / 255f;
                b = (value & 0xFF) / 255f;
                a = 1f;
                return true;
            }

            r = ((value >> 24) & 0xFF) / 255f;
            g = ((value >> 16) & 0xFF) / 255f;
            b = ((value >> 8) & 0xFF) / 255f;
            a = (value & 0xFF) / 255f;
            return true;
        }
    }
}
