using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WhisperOfTheForest.Testing
{
    /// <summary>
    /// Tiny JSON writer for the test run report. The lab protocol only needs nested objects, arrays,
    /// strings, numbers and booleans, so this avoids a System.Text.Json dependency in the game.
    /// </summary>
    internal sealed class TestJson
    {
        private readonly StringBuilder builder = new StringBuilder(1024);
        private readonly Stack<bool> needsComma = new Stack<bool>();
        private bool pendingKey;

        internal TestJson StartObject()
        {
            Separate();
            builder.Append('{');
            needsComma.Push(false);
            return this;
        }

        internal TestJson EndObject()
        {
            builder.Append('}');
            needsComma.Pop();
            pendingKey = false;
            return this;
        }

        internal TestJson StartArray()
        {
            Separate();
            builder.Append('[');
            needsComma.Push(false);
            return this;
        }

        internal TestJson EndArray()
        {
            builder.Append(']');
            needsComma.Pop();
            pendingKey = false;
            return this;
        }

        internal TestJson Key(string key)
        {
            Separate();
            AppendString(key);
            builder.Append(':');
            pendingKey = true;
            return this;
        }

        internal TestJson Value(string value)
        {
            Separate();
            if (value == null)
            {
                builder.Append("null");
            }
            else
            {
                AppendString(value);
            }

            return this;
        }

        internal TestJson Value(bool value)
        {
            Separate();
            builder.Append(value ? "true" : "false");
            return this;
        }

        internal TestJson Value(int value)
        {
            Separate();
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        internal TestJson Value(long value)
        {
            Separate();
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        internal TestJson Value(float value)
        {
            Separate();
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                builder.Append("null");
            }
            else
            {
                builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            }

            return this;
        }

        internal TestJson Property(string key, string value)
        {
            return Key(key).Value(value);
        }

        internal TestJson Property(string key, bool value)
        {
            return Key(key).Value(value);
        }

        internal TestJson Property(string key, int value)
        {
            return Key(key).Value(value);
        }

        internal TestJson Property(string key, long value)
        {
            return Key(key).Value(value);
        }

        internal TestJson Property(string key, float value)
        {
            return Key(key).Value(value);
        }

        public override string ToString()
        {
            return builder.ToString();
        }

        private void Separate()
        {
            if (pendingKey)
            {
                pendingKey = false;
                return;
            }

            if (needsComma.Count == 0)
            {
                return;
            }

            if (needsComma.Peek())
            {
                builder.Append(',');
            }
            else
            {
                needsComma.Pop();
                needsComma.Push(true);
            }
        }

        private void AppendString(string value)
        {
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
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
