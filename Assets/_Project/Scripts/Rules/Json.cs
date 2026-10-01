using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wreckabulary.Rules
{
    /// <summary>
    /// A small JSON reader for the config files in Data/Config. The rules have no engine
    /// references, so they can't use JsonUtility, and netstandard2.1 has no System.Text.Json.
    /// Objects become <see cref="JsonNode"/>s; arrays, strings, numbers, true/false and null
    /// are supported.
    /// </summary>
    public static class Json
    {
        public static JsonNode Parse(string text, string source = "json")
        {
            var reader = new Reader(text, source);
            reader.SkipSpace();
            var value = reader.ReadValue("$");
            reader.SkipSpace();
            if (!reader.AtEnd) throw reader.Error("unexpected text after the end of the document");
            return new JsonNode(value, "$", source);
        }

        sealed class Reader
        {
            readonly string text;
            readonly string source;
            int pos;

            public Reader(string text, string source)
            {
                this.text = text ?? throw new ArgumentNullException(nameof(text));
                this.source = source;
            }

            public bool AtEnd => pos >= text.Length;

            public FormatException Error(string message)
            {
                int line = 1, col = 1;
                for (int i = 0; i < pos && i < text.Length; i++)
                {
                    if (text[i] == '\n') { line++; col = 1; }
                    else col++;
                }
                return new FormatException($"{source}:{line}:{col}: {message}");
            }

            public void SkipSpace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            }

            public object ReadValue(string path)
            {
                if (AtEnd) throw Error("unexpected end of document");
                char c = text[pos];
                switch (c)
                {
                    case '{': return ReadObject(path);
                    case '[': return ReadArray(path);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0) throw Error($"expected {word}");
                pos += word.Length;
            }

            Dictionary<string, object> ReadObject(string path)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                pos++; // {
                SkipSpace();
                if (!AtEnd && text[pos] == '}') { pos++; return result; }
                while (true)
                {
                    SkipSpace();
                    if (AtEnd || text[pos] != '"') throw Error("expected a quoted key");
                    string key = ReadString();
                    SkipSpace();
                    if (AtEnd || text[pos] != ':') throw Error("expected ':'");
                    pos++;
                    SkipSpace();
                    if (result.ContainsKey(key)) throw Error($"duplicate key \"{key}\"");
                    result[key] = ReadValue(path + "." + key);
                    SkipSpace();
                    if (AtEnd) throw Error("unterminated object");
                    if (text[pos] == ',') { pos++; continue; }
                    if (text[pos] == '}') { pos++; return result; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> ReadArray(string path)
            {
                var result = new List<object>();
                pos++; // [
                SkipSpace();
                if (!AtEnd && text[pos] == ']') { pos++; return result; }
                while (true)
                {
                    SkipSpace();
                    result.Add(ReadValue($"{path}[{result.Count}]"));
                    SkipSpace();
                    if (AtEnd) throw Error("unterminated array");
                    if (text[pos] == ',') { pos++; continue; }
                    if (text[pos] == ']') { pos++; return result; }
                    throw Error("expected ',' or ']'");
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                pos++; // opening quote
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = text[pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            double ReadNumber()
            {
                int start = pos;
                if (text[pos] == '-') pos++;
                while (pos < text.Length && "0123456789.eE+-".IndexOf(text[pos]) >= 0) pos++;
                string s = text.Substring(start, pos - start);
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error($"bad number '{s}'");
                return d;
            }
        }
    }

    /// <summary>
    /// A read-only view of one parsed JSON value that knows its path, so config errors say
    /// exactly which field is wrong ("items.json: $.items[3].melee.damage must be a number").
    /// </summary>
    public sealed class JsonNode
    {
        readonly object value;
        public string Path { get; }
        readonly string source;

        internal JsonNode(object value, string path, string source)
        {
            this.value = value;
            Path = path;
            this.source = source;
        }

        public bool IsNull => value == null;
        public bool IsObject => value is Dictionary<string, object>;
        public bool IsArray => value is List<object>;

        FormatException Wrong(string what) => new FormatException($"{source}: {Path} must be {what}");

        /// <summary>An error that names the file and this node's place in it.</summary>
        public FormatException Error(string message) => new FormatException($"{source}: {Path}: {message}");

        public bool Has(string key) => value is Dictionary<string, object> d && d.ContainsKey(key) && d[key] != null;

        public JsonNode this[string key]
        {
            get
            {
                if (!(value is Dictionary<string, object> d)) throw Wrong("an object");
                d.TryGetValue(key, out object child);
                return new JsonNode(child, Path + "." + key, source);
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                if (!(value is Dictionary<string, object> d)) throw Wrong("an object");
                return d.Keys;
            }
        }

        public IReadOnlyList<JsonNode> Items
        {
            get
            {
                if (IsNull) return Array.Empty<JsonNode>();
                if (!(value is List<object> list)) throw Wrong("an array");
                var nodes = new JsonNode[list.Count];
                for (int i = 0; i < list.Count; i++) nodes[i] = new JsonNode(list[i], $"{Path}[{i}]", source);
                return nodes;
            }
        }

        public string String()
        {
            if (value is string s) return s;
            throw Wrong("a string");
        }

        public string String(string fallback) => IsNull ? fallback : String();

        public double Number()
        {
            if (value is double d) return d;
            throw Wrong("a number");
        }

        public double Number(double fallback) => IsNull ? fallback : Number();

        public float Float(float fallback) => IsNull ? fallback : (float)Number();

        public float Float() => (float)Number();

        public int Int()
        {
            double d = Number();
            if (Math.Abs(d - Math.Round(d)) > 1e-9) throw Wrong("a whole number");
            return (int)Math.Round(d);
        }

        public int Int(int fallback) => IsNull ? fallback : Int();

        public bool Bool()
        {
            if (value is bool b) return b;
            throw Wrong("true or false");
        }

        public bool Bool(bool fallback) => IsNull ? fallback : Bool();

        public float[] Floats(int expectedLength)
        {
            var items = Items;
            if (items.Count != expectedLength) throw Wrong($"an array of {expectedLength} numbers");
            var result = new float[expectedLength];
            for (int i = 0; i < expectedLength; i++) result[i] = items[i].Float();
            return result;
        }

        public List<string> Strings()
        {
            var result = new List<string>();
            foreach (var item in Items) result.Add(item.String());
            return result;
        }
    }
}
