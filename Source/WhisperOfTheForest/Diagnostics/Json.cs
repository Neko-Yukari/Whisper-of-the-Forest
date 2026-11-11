using System;
using System.Globalization;
using System.Text;
using Verse;

namespace WhisperOfTheForest
{
    /// <summary>
    /// Minimal JSON emitter. The lab protocol only needs flat objects, so this avoids a runtime
    /// dependency on System.Text.Json (which the game also ships as a platform assembly).
    /// </summary>
    internal static class Json
    {
        public static string Str(string value)
        {
            if (value == null)
            {
                return "null";
            }

            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        public static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Num(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string Num(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "JSON numbers must be finite.");
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        public static string Cell(IntVec3 cell)
        {
            return "[" + Num(cell.x) + "," + Num(cell.y) + "," + Num(cell.z) + "]";
        }

        public static string CellOrNull(bool valid, IntVec3 cell)
        {
            return valid ? Cell(cell) : "null";
        }
    }
}
