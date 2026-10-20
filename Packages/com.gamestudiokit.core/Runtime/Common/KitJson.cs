// Game Studio Kit (MIT). Engine-free JSON writer and reader shared by every kit file format (checks.json, perf.json,
// packed_assets.json, probes.config.json, cameras.json, exec results). No UnityEngine types, so the kit's node tests can
// compile and run this file under plain .NET to prove the output matches the JSON schemas.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameStudioKit.Common
{
    /// <summary>Implemented by kit documents that write themselves as one JSON value.</summary>
    public interface IKitJson
    {
        void WriteJson(KitJsonWriter w);
    }

    /// <summary>
    /// Streaming JSON writer. Numbers are written with the invariant culture, so a German or French OS never produces
    /// "7,9". Doubles are rounded to 6 decimals; NaN and infinities become null because JSON has no such values.
    /// </summary>
    public sealed class KitJsonWriter
    {
        readonly StringBuilder _sb;
        readonly bool _pretty;
        readonly Stack<bool> _hasItems = new Stack<bool>();
        bool _afterName;

        public KitJsonWriter(bool pretty = true, int capacity = 4096)
        {
            _sb = new StringBuilder(capacity);
            _pretty = pretty;
        }

        public override string ToString() => _sb.ToString();

        public KitJsonWriter BeginObject() { BeforeValue(); _sb.Append('{'); _hasItems.Push(false); return this; }
        public KitJsonWriter EndObject() { Close('}'); return this; }
        public KitJsonWriter BeginArray() { BeforeValue(); _sb.Append('['); _hasItems.Push(false); return this; }
        public KitJsonWriter EndArray() { Close(']'); return this; }

        public KitJsonWriter Name(string name)
        {
            if (_hasItems.Count == 0) throw new InvalidOperationException("KitJsonWriter: Name outside an object");
            if (_hasItems.Peek()) _sb.Append(',');
            _hasItems.Pop();
            _hasItems.Push(true);
            NewLine();
            WriteString(name);
            _sb.Append(_pretty ? ": " : ":");
            _afterName = true;
            return this;
        }

        public KitJsonWriter Null() { BeforeValue(); _sb.Append("null"); return this; }
        public KitJsonWriter Value(bool v) { BeforeValue(); _sb.Append(v ? "true" : "false"); return this; }
        public KitJsonWriter Value(int v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public KitJsonWriter Value(long v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        public KitJsonWriter Value(ulong v) { BeforeValue(); _sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
        /// <summary>Floats go through decimal (7 significant digits), so 7.9f is written as 7.9, not 7.900000095.</summary>
        public KitJsonWriter Value(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return Null();
            return Value(Math.Abs(v) < 1e15f ? (double)(decimal)v : v);
        }

        public KitJsonWriter Value(double v)
        {
            BeforeValue();
            _sb.Append(FormatNumber(v));
            return this;
        }

        public KitJsonWriter Value(string v)
        {
            BeforeValue();
            if (v == null) _sb.Append("null");
            else WriteString(v);
            return this;
        }

        /// <summary>Writes any supported value: null, string, bool, integer and floating types, IKitJson,
        /// IDictionary (string keys, in enumeration order) and IEnumerable.</summary>
        public KitJsonWriter Any(object v)
        {
            switch (v)
            {
                case null: return Null();
                case string s: return Value(s);
                case bool b: return Value(b);
                case int i: return Value(i);
                case long l: return Value(l);
                case uint ui: return Value((long)ui);
                case ulong ul: return Value(ul);
                case short sh: return Value((int)sh);
                case byte by: return Value((int)by);
                case float f: return Value(f);
                case double d: return Value(d);
                case decimal m: return Value((double)m);
                case IKitJson j:
                    j.WriteJson(this);
                    return this;
                case IDictionary dict:
                    BeginObject();
                    foreach (DictionaryEntry e in dict) { Name(Convert.ToString(e.Key, CultureInfo.InvariantCulture)); Any(e.Value); }
                    return EndObject();
                case IEnumerable seq:
                    BeginArray();
                    foreach (var item in seq) Any(item);
                    return EndArray();
                default:
                    return Value(Convert.ToString(v, CultureInfo.InvariantCulture));
            }
        }

        // Convenience property writers.
        public KitJsonWriter Prop(string name, string v) => Name(name).Value(v);
        public KitJsonWriter Prop(string name, bool v) => Name(name).Value(v);
        public KitJsonWriter Prop(string name, int v) => Name(name).Value(v);
        public KitJsonWriter Prop(string name, long v) => Name(name).Value(v);
        public KitJsonWriter Prop(string name, double v) => Name(name).Value(v);
        public KitJsonWriter PropAny(string name, object v) => Name(name).Any(v);

        /// <summary>Invariant, round-to-6-decimals number text; NaN and infinities become "null".</summary>
        public static string FormatNumber(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            if (Math.Abs(v) < 1e15)
            {
                double r = Math.Round(v, 6, MidpointRounding.AwayFromZero);
                if (r == 0) return "0";
                return r.ToString("0.######", CultureInfo.InvariantCulture);
            }
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        void BeforeValue()
        {
            if (_afterName) { _afterName = false; return; }
            if (_hasItems.Count == 0) return;
            if (_hasItems.Peek()) _sb.Append(',');
            _hasItems.Pop();
            _hasItems.Push(true);
            NewLine();
        }

        void Close(char c)
        {
            if (_hasItems.Count == 0) throw new InvalidOperationException("KitJsonWriter: unbalanced close");
            bool had = _hasItems.Pop();
            if (had) NewLine();
            _sb.Append(c);
        }

        void NewLine()
        {
            if (!_pretty) return;
            _sb.Append('\n');
            _sb.Append(' ', _hasItems.Count * 2);
        }

        void WriteString(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    case '\b': _sb.Append("\\b"); break;
                    case '\f': _sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029)
                            _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }

    /// <summary>JSON parse and serialize helpers. Parse returns Dictionary&lt;string, object&gt; (insertion order),
    /// List&lt;object&gt;, string, long (integers that fit), double, bool or null.</summary>
    public static class KitJson
    {
        public static string Serialize(object value, bool pretty = true)
        {
            var w = new KitJsonWriter(pretty);
            w.Any(value);
            return w.ToString();
        }

        /// <summary>Parses one JSON document. Throws FormatException with the character offset on bad input.</summary>
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Parser(text);
            p.SkipWs();
            var v = p.ReadValue(0);
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("trailing characters");
            return v;
        }

        public static bool TryParse(string text, out object value, out string error)
        {
            try { value = Parse(text); error = null; return true; }
            catch (FormatException e) { value = null; error = e.Message; return false; }
        }

        public static Dictionary<string, object> AsObject(object v) => v as Dictionary<string, object>;
        public static List<object> AsList(object v) => v as List<object>;

        public static object Get(Dictionary<string, object> o, string key)
        {
            if (o == null || key == null) return null;
            return o.TryGetValue(key, out var v) ? v : null;
        }

        public static string GetString(Dictionary<string, object> o, string key, string fallback = null) =>
            Get(o, key) is string s ? s : fallback;

        public static double GetDouble(Dictionary<string, object> o, string key, double fallback = 0) =>
            ToDouble(Get(o, key), fallback);

        public static int GetInt(Dictionary<string, object> o, string key, int fallback = 0)
        {
            var v = Get(o, key);
            if (v is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
            if (v is double d && !double.IsNaN(d) && Math.Abs(d) < int.MaxValue) return (int)Math.Round(d);
            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> o, string key, bool fallback = false) =>
            Get(o, key) is bool b ? b : fallback;

        public static Dictionary<string, object> GetObject(Dictionary<string, object> o, string key) =>
            Get(o, key) as Dictionary<string, object>;

        public static List<object> GetList(Dictionary<string, object> o, string key) => Get(o, key) as List<object>;

        public static double ToDouble(object v, double fallback = 0)
        {
            switch (v)
            {
                case long l: return l;
                case double d: return d;
                case int i: return i;
                case float f: return f;
                default: return fallback;
            }
        }

        public static bool IsNumber(object v) => v is long || v is double || v is int || v is float;

        public static List<string> GetStringList(Dictionary<string, object> o, string key)
        {
            var result = new List<string>();
            var list = GetList(o, key);
            if (list == null) return result;
            foreach (var item in list) if (item is string s) result.Add(s);
            return result;
        }

        sealed class Parser
        {
            const int MaxDepth = 256;
            readonly string _s;
            int _i;

            public Parser(string s)
            {
                _s = s;
                _i = s.Length > 0 && s[0] == (char)0xFEFF ? 1 : 0;
            }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string what) =>
                new FormatException("KitJson: " + what + " at offset " + _i.ToString(CultureInfo.InvariantCulture));

            public void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') _i++;
                    else break;
                }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth) throw Error("nesting too deep");
                if (AtEnd) throw Error("unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error("unexpected character '" + c + "'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error("expected " + word);
                _i += word.Length;
            }

            Dictionary<string, object> ReadObject(int depth)
            {
                var o = new Dictionary<string, object>();
                _i++;
                SkipWs();
                if (!AtEnd && _s[_i] == '}') { _i++; return o; }
                while (true)
                {
                    SkipWs();
                    if (AtEnd || _s[_i] != '"') throw Error("expected a string key");
                    string key = ReadString();
                    SkipWs();
                    if (AtEnd || _s[_i] != ':') throw Error("expected ':'");
                    _i++;
                    SkipWs();
                    o[key] = ReadValue(depth + 1);
                    SkipWs();
                    if (AtEnd) throw Error("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return o; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> ReadArray(int depth)
            {
                var a = new List<object>();
                _i++;
                SkipWs();
                if (!AtEnd && _s[_i] == ']') { _i++; return a; }
                while (true)
                {
                    SkipWs();
                    a.Add(ReadValue(depth + 1));
                    SkipWs();
                    if (AtEnd) throw Error("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return a; }
                    throw Error("expected ',' or ']'");
                }
            }

            string ReadString()
            {
                _i++;
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c < 0x20) throw Error("control character in string");
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("bad escape");
                    char e = _s[_i++];
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
                            if (_i + 4 > _s.Length) throw Error("bad unicode escape");
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw Error("bad unicode escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Error("bad escape");
                    }
                }
            }

            object ReadNumber()
            {
                int start = _i;
                if (_s[_i] == '-') _i++;
                if (AtEnd || !char.IsDigit(_s[_i])) throw Error("bad number");
                if (_s[_i] == '0') _i++;
                else while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                bool isInt = true;
                if (!AtEnd && _s[_i] == '.')
                {
                    isInt = false;
                    _i++;
                    if (AtEnd || !char.IsDigit(_s[_i])) throw Error("bad fraction");
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                if (!AtEnd && (_s[_i] == 'e' || _s[_i] == 'E'))
                {
                    isInt = false;
                    _i++;
                    if (!AtEnd && (_s[_i] == '+' || _s[_i] == '-')) _i++;
                    if (AtEnd || !char.IsDigit(_s[_i])) throw Error("bad exponent");
                    while (!AtEnd && char.IsDigit(_s[_i])) _i++;
                }
                string text = _s.Substring(start, _i - start);
                if (isInt && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
