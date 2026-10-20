using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules.Wordbound
{
    public static class OnlineJson
    {
        public const int MaximumBytes = 32768;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static JsonNode Parse(string json)
        {
            if (json == null || json.Length > MaximumBytes || Utf8.GetByteCount(json) > MaximumBytes)
                throw new FormatException("Online JSON exceeds its size limit.");
            int depth = 0;
            bool quoted = false, escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (c < 32) throw new FormatException("Unescaped JSON control character.");
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{' || c == '[')
                {
                    if (++depth > 12) throw new FormatException("Online JSON nesting exceeds its limit.");
                }
                else if (c == '}' || c == ']') depth--;
                else if (char.IsWhiteSpace(c) && " \r\n\t".IndexOf(c) < 0)
                    throw new FormatException("Invalid JSON whitespace.");
                else if (c == '-' || (c >= '0' && c <= '9'))
                {
                    int start = i;
                    while (i + 1 < json.Length && ",]} \t\r\n".IndexOf(json[i + 1]) < 0) i++;
                    string number = json.Substring(start, i - start + 1);
                    if (!long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ||
                        value < -9007199254740991L || value > 9007199254740991L || number != value.ToString(CultureInfo.InvariantCulture))
                        throw new FormatException("Online JSON requires exact bounded integers.");
                }
            }
            return Json.Parse(json, "online");
        }

        public static string Encode(object value)
        {
            if (value == null) return "null";
            if (value is string text) return Quote(text);
            if (value is bool flag) return flag ? "true" : "false";
            if (value is int || value is long)
            {
                long number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (number < -9007199254740991L || number > 9007199254740991L)
                    throw new ArgumentException("Serialize large counters as decimal strings.");
                return number.ToString(CultureInfo.InvariantCulture);
            }
            if (value is IDictionary<string, object> record)
                return "{" + string.Join(",", record.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => Quote(p.Key) + ":" + Encode(p.Value))) + "}";
            if (value is IEnumerable items)
                return "[" + string.Join(",", items.Cast<object>().Select(Encode)) + "]";
            throw new ArgumentException("Unsupported online JSON value.");
        }

        static string Quote(string text)
        {
            Utf8.GetByteCount(text);
            var result = new StringBuilder("\"");
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': result.Append("\\\""); break;
                    case '\\': result.Append("\\\\"); break;
                    case '\b': result.Append("\\b"); break;
                    case '\f': result.Append("\\f"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else result.Append(c);
                        break;
                }
            }
            return result.Append('"').ToString();
        }

        public static string Hash(object value)
        {
            using var hash = SHA256.Create();
            return string.Concat(hash.ComputeHash(Utf8.GetBytes(Encode(value))).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static byte[] DecodeBase64Url(string value, int maximumBytes)
        {
            if (value == null || value.Length > maximumBytes * 2 || !Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"))
                throw new FormatException("Invalid base64url value.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4)); }
            catch (FormatException) { throw new FormatException("Invalid base64url value."); }
            if (bytes.Length > maximumBytes || Base64Url(bytes) != value) throw new FormatException("Noncanonical base64url value.");
            return bytes;
        }

        public static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public static string DecodeUtf8(byte[] value)
        {
            try { return Utf8.GetString(value); }
            catch (DecoderFallbackException) { throw new FormatException("Invalid UTF-8."); }
        }

        internal static void Fields(JsonNode node, params string[] names)
        {
            if (!node.IsObject || node.Keys.Any(k => !names.Contains(k, StringComparer.Ordinal)) ||
                names.Any(k => !node.Has(k))) throw new FormatException("Unexpected or missing online fields.");
        }
        internal static string Text(JsonNode node, string name, string pattern)
        {
            string text = node[name].String();
            var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
            if (!match.Success || match.Length != text.Length) throw new FormatException("Invalid online " + name + ".");
            return text;
        }
        internal static long Integer(JsonNode node, string name, long minimum = 0, long maximum = 9007199254740991L)
        {
            double value = node[name].Number();
            if (double.IsNaN(value) || double.IsInfinity(value) || value != Math.Truncate(value) || value < minimum || value > maximum)
                throw new FormatException("Invalid online " + name + ".");
            return (long)value;
        }
        internal const string Uuid = "^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$";
        internal const string HashPattern = "^[a-f0-9]{64}$";
        internal const string Counter = "^(0|[1-9][0-9]{0,18})$";
        internal const string Identifier = "^[A-Za-z0-9_-]{1,24}$";
    }
}
