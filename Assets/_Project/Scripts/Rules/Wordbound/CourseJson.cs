using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseJson
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        static readonly Regex Number = new Regex(@"\A-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?\z");

        public static CourseImportResult ImportDocument(string json)
        {
            var result = new CourseImportResult();
            try
            {
                Preflight(json, CourseLimits.DocumentBytes);
                var node = Json.Parse(json, "course.json");
                if (node["format"].String() != CourseDocument.FormatId) throw node.Error("not a Wreckabulary course document");
                var document = (CourseDocument)Read(node, typeof(CourseDocument), true);
                result.Validation.AddRange(CourseValidator.ValidateStructure(document).Issues);
                if (result.Validation.Ok) result.Document = document;
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { result.Validation.Add("json.invalid", "$", "", e.Message); }
            return result;
        }

        public static string ExportDocument(CourseDocument document)
        {
            var valid = CourseValidator.ValidateStructure(document);
            if (!valid.Ok) throw new ArgumentException(string.Join("\n", valid.Issues));
            var value = (Dictionary<string, object>)Value(document);
            value.Add("format", CourseDocument.FormatId);
            string json = Encode(value);
            Preflight(json, CourseLimits.DocumentBytes);
            return json;
        }

        public static string ExportManifest(ResolvedCourse manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            string json = Encode(Value(manifest.Data));
            Preflight(json, CourseLimits.ManifestBytes);
            return json;
        }

        public static CourseManifestResult ImportManifest(string json, CourseCatalogue catalogue)
        {
            var result = new CourseManifestResult();
            try
            {
                Preflight(json, CourseLimits.ManifestBytes);
                var data = (CourseManifestData)Read(Json.Parse(json, "manifest.json"), typeof(CourseManifestData));
                CourseValidator.ValidateManifest(data, catalogue, result.Validation);
                if (result.Validation.Ok) result.Manifest = new ResolvedCourse(data, catalogue);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { result.Validation.Add("json.invalid", "$", "", e.Message); }
            return result;
        }

        internal static object Read(JsonNode node, Type type, bool documentRoot = false)
        {
            if (type == typeof(string))
            {
                string text = node.String();
                ValidateText(text);
                if (text.Length > 4096) throw node.Error("string exceeds 4096 characters");
                return text;
            }
            if (type == typeof(int))
            {
                double number = node.Number();
                if (double.IsNaN(number) || double.IsInfinity(number) || number < int.MinValue || number > int.MaxValue || Math.Truncate(number) != number)
                    throw node.Error("expected an in-range integer");
                return (int)number;
            }
            if (type == typeof(bool)) return node.Bool();
            if (type == typeof(CoursePoint))
            {
                if (!node.IsArray || node.Items.Count != 3) throw node.Error("expected [x,y,z] integer millimetres");
                var points = node.Items;
                var point = new CoursePoint((int)Read(points[0], typeof(int)), (int)Read(points[1], typeof(int)), (int)Read(points[2], typeof(int)));
                if (!CourseValidator.ValidPoint(point)) throw node.Error("coordinate exceeds course bounds");
                return point;
            }
            if (type == typeof(CourseBox))
            {
                Keys(node, new[] { "min", "max" });
                var box = new CourseBox((CoursePoint)Read(node["min"], typeof(CoursePoint)), (CoursePoint)Read(node["max"], typeof(CoursePoint)));
                if (!CourseValidator.ValidBox(box)) throw node.Error("invalid volume bounds");
                return box;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                if (!node.IsArray) throw node.Error("expected an array");
                var items = node.Items;
                if (items.Count > CourseLimits.Entities) throw node.Error("array exceeds entity bound");
                var list = (IList)Activator.CreateInstance(type);
                Type itemType = type.GetGenericArguments()[0];
                foreach (var item in items) list.Add(Read(item, itemType));
                return list;
            }
            if (!type.Name.StartsWith("Course", StringComparison.Ordinal) || type.Namespace != typeof(CourseJson).Namespace)
                throw new ArgumentException("Unsupported course data type.");
            var fields = Fields(type);
            Keys(node, fields.Select(f => Key(f.Name)).Concat(documentRoot ? new[] { "format" } : Array.Empty<string>()));
            var instance = Activator.CreateInstance(type);
            foreach (var field in fields) field.SetValue(instance, Read(node[Key(field.Name)], field.FieldType));
            return instance;
        }

        internal static object Value(object value)
        {
            if (value == null) throw new ArgumentException("Course data cannot contain null values.");
            if (value is string text) { ValidateText(text); return text; }
            if (value is int || value is bool) return value;
            if (value is CoursePoint p) return new object[] { p.X, p.Y, p.Z };
            if (value is CourseBox b) return new Dictionary<string, object> { ["min"] = Value(b.Min), ["max"] = Value(b.Max) };
            if (value is IDictionary<string, object>) return value;
            if (value is IEnumerable sequence)
            {
                var list = sequence.Cast<object>().ToList();
                if (list.Count > CourseLimits.Entities) throw new ArgumentException("Array exceeds entity bound.");
                if (list.Count > 0 && list[0] != null)
                {
                    var type = list[0].GetType();
                    var id = type.GetField("Id") ?? type.GetField("AssetId");
                    if (id != null) list = list.OrderBy(x => (string)id.GetValue(x), StringComparer.Ordinal).ToList();
                    else if (type == typeof(CourseLap)) list = list.OrderBy(x => ((CourseLap)x).Number).ToList();
                    else if (type == typeof(CourseLapSelection)) list = list.OrderBy(x => ((CourseLapSelection)x).Number).ToList();
                }
                return list.Select(Value).ToList();
            }
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var field in Fields(value.GetType()))
            {
                object fieldValue = field.GetValue(value);
                if (fieldValue is List<string> strings && field.Name != "ConnectionIds" && field.Name != "CheckpointIds" &&
                    field.Name != "SectionIds" && field.Name != "PuzzleIds" && field.Name != "AlternativeIds")
                    fieldValue = strings.OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (field.Name == "Yaws" && fieldValue is List<int> numbers) fieldValue = numbers.OrderBy(x => x).ToList();
                result.Add(Key(field.Name), Value(fieldValue));
            }
            return result;
        }
        static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Instance | BindingFlags.Public);
        static string Key(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);
        internal static void Keys(JsonNode node, IEnumerable<string> expected)
        {
            var keys = new HashSet<string>(expected, StringComparer.Ordinal);
            if (!node.IsObject || node.Keys.Any(k => !keys.Contains(k)) || keys.Any(k => !node.Has(k)))
                throw node.Error("missing or unknown fields; expected " + string.Join(", ", keys.OrderBy(k => k, StringComparer.Ordinal)));
        }
        internal static string Encode(object value)
        {
            var builder = new StringBuilder();
            Write(builder, value);
            return builder.ToString();
        }
        static void Write(StringBuilder b, object value)
        {
            if (value is string text) { Quote(b, text); return; }
            if (value is int number) { b.Append(number.ToString(CultureInfo.InvariantCulture)); return; }
            if (value is bool flag) { b.Append(flag ? "true" : "false"); return; }
            if (value is IDictionary<string, object> map)
            {
                b.Append('{'); bool first = true;
                foreach (var pair in map.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (!first) b.Append(','); first = false;
                    Quote(b, pair.Key); b.Append(':'); Write(b, pair.Value);
                }
                b.Append('}'); return;
            }
            if (value is IEnumerable list)
            {
                b.Append('['); bool first = true;
                foreach (var item in list) { if (!first) b.Append(','); first = false; Write(b, item); }
                b.Append(']'); return;
            }
            throw new ArgumentException("Unsupported canonical JSON value.");
        }
        static void Quote(StringBuilder b, string text)
        {
            ValidateText(text);
            b.Append('"');
            foreach (char c in text)
            {
                if (c == '"' || c == '\\') b.Append('\\').Append(c);
                else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else b.Append(c);
            }
            b.Append('"');
        }
        internal static void ValidateText(string text)
        {
            if (text == null) throw new FormatException("Null string.");
            for (int i = 0; i < text.Length; i++)
                if (char.IsSurrogate(text[i]))
                {
                    if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                        throw new FormatException("Unpaired Unicode surrogate.");
                    i++;
                }
        }
        internal static void Preflight(string text, int byteLimit)
        {
            if (text == null || text.Length > byteLimit) throw new FormatException("JSON exceeds the byte limit or is null.");
            ValidateText(text);
            if (Utf8.GetByteCount(text) > byteLimit) throw new FormatException("JSON exceeds the UTF-8 byte limit.");
            int depth = 0, tokens = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ':' || c == ',') continue;
                if (++tokens > 250000) throw new FormatException("JSON token limit exceeded.");
                if (c == '{' || c == '[') { if (++depth > CourseLimits.JsonDepth) throw new FormatException("JSON nesting limit exceeded."); continue; }
                if (c == '}' || c == ']') { if (--depth < 0) throw new FormatException("Unexpected closing delimiter."); continue; }
                if (c == '"')
                {
                    bool closed = false; int start = i;
                    while (++i < text.Length)
                    {
                        c = text[i];
                        if (i - start > 24578) throw new FormatException("JSON string token too long.");
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new FormatException("Unescaped control character.");
                        if (c != '\\') continue;
                        if (++i >= text.Length) break;
                        c = text[i];
                        if (c == 'u')
                        {
                            if (i + 4 >= text.Length) throw new FormatException("Incomplete Unicode escape.");
                            for (int n = 0; n < 4; n++)
                                if (!Uri.IsHexDigit(text[++i])) throw new FormatException("Invalid Unicode escape.");
                        }
                        else if ("\"\\/bfnrt".IndexOf(c) < 0) throw new FormatException("Invalid string escape.");
                    }
                    if (!closed) throw new FormatException("Unterminated string.");
                    continue;
                }
                int begin = i;
                while (i < text.Length && " \t\r\n,:]}".IndexOf(text[i]) < 0) i++;
                string token = text.Substring(begin, i - begin); i--;
                if (token == "true" || token == "false" || token == "null") continue;
                if (token.Length > 32 || !Number.IsMatch(token)) throw new FormatException("Invalid JSON token.");
            }
            if (depth != 0) throw new FormatException("Unclosed JSON delimiter.");
        }
        public static string Sha256(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Utf8.GetBytes(text)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
