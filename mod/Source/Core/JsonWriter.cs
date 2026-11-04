using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RimWorldMcp.Bridge.Core
{
    /// <summary>
    /// Minimal streaming JSON writer. The mod has no third-party dependencies, and the game
    /// ships no JSON library that works on every platform it supports.
    /// </summary>
    public sealed class JsonWriter
    {
        private readonly StringBuilder output = new StringBuilder(1024);
        private readonly Stack<bool> hasItems = new Stack<bool>();
        private bool afterKey;

        public JsonWriter BeginObject()
        {
            BeforeValue();
            output.Append('{');
            hasItems.Push(false);
            return this;
        }

        public JsonWriter EndObject()
        {
            hasItems.Pop();
            output.Append('}');
            return this;
        }

        public JsonWriter BeginArray()
        {
            BeforeValue();
            output.Append('[');
            hasItems.Push(false);
            return this;
        }

        public JsonWriter EndArray()
        {
            hasItems.Pop();
            output.Append(']');
            return this;
        }

        public JsonWriter Key(string name)
        {
            WriteSeparator();
            AppendString(name);
            output.Append(':');
            afterKey = true;
            return this;
        }

        public JsonWriter Value(string value)
        {
            BeforeValue();
            if (value == null)
            {
                output.Append("null");
            }
            else
            {
                AppendString(value);
            }
            return this;
        }

        public JsonWriter Value(bool value)
        {
            BeforeValue();
            output.Append(value ? "true" : "false");
            return this;
        }

        public JsonWriter Value(int value)
        {
            BeforeValue();
            output.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter Value(long value)
        {
            BeforeValue();
            output.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonWriter Value(double value)
        {
            BeforeValue();
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                // JSON has no representation for these, and some game stats can produce them.
                output.Append("null");
            }
            else
            {
                output.Append(Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture));
            }
            return this;
        }

        public JsonWriter Null()
        {
            BeforeValue();
            output.Append("null");
            return this;
        }

        public JsonWriter Prop(string name, string value) => Key(name).Value(value);

        public JsonWriter Prop(string name, bool value) => Key(name).Value(value);

        public JsonWriter Prop(string name, int value) => Key(name).Value(value);

        public JsonWriter Prop(string name, long value) => Key(name).Value(value);

        public JsonWriter Prop(string name, double value) => Key(name).Value(value);

        public override string ToString() => output.ToString();

        private void BeforeValue()
        {
            if (afterKey)
            {
                afterKey = false;
                return;
            }
            WriteSeparator();
        }

        private void WriteSeparator()
        {
            if (hasItems.Count == 0)
            {
                return;
            }
            if (hasItems.Peek())
            {
                output.Append(',');
            }
            else
            {
                hasItems.Pop();
                hasItems.Push(true);
            }
        }

        private void AppendString(string value)
        {
            output.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        output.Append("\\\"");
                        break;
                    case '\\':
                        output.Append("\\\\");
                        break;
                    case '\n':
                        output.Append("\\n");
                        break;
                    case '\r':
                        output.Append("\\r");
                        break;
                    case '\t':
                        output.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            output.Append(c);
                        }
                        break;
                }
            }
            output.Append('"');
        }
    }
}
