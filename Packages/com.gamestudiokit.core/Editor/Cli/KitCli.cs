// Game Studio Kit: the exec door between `game_gate exec` and C# entry points (CONTRACTS §7.9).
// One entry point, three doors: warm `unity command eval` calls Run(...), cold batchmode calls
// `-executeMethod GameStudioKit.Editor.KitCli.RunFromEnv` (GSK_EXEC_METHOD / GSK_EXEC_ARGS / GSK_EXEC_OUT), and the
// optional Pipeline command `kit_exec` (Editor.Pipeline/Cli/KitCliCommands.cs) forwards to Run(...).
// An entry point is `public static void Method()` in a project or kit assembly (Library/ScriptAssemblies): it reads
// KitCli.Args (a JSON string) and reports with KitCli.WriteResult(object). Engine, editor, runtime and library code
// (UnityEngine.*, UnityEditor.*, Unity.*, System.*, ...) is never an entry point, so exec cannot reach e.g.
// EditorPrefs.DeleteAll. KitCli writes the envelope {ok, method, utc, result, error[, truncated]} to OutPath; `ok` is
// false when the method is refused, missing or throws. The gate FAILs on a missing file, ok != true, or
// result.verdict FAIL/ERROR.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

namespace GameStudioKit.Editor
{
    public static class KitCli
    {
        /// <summary>The JSON arguments of the running exec ("{}" when none were given).</summary>
        public static string Args { get; private set; } = "{}";

        /// <summary>Absolute path of the result envelope for the running exec.</summary>
        public static string OutPath { get; private set; }

        /// <summary>Fully qualified name of the running entry point.</summary>
        public static string Method { get; private set; }

        /// <summary>Namespaces that never hold an entry point (the gate refuses them too).</summary>
        static readonly string[] s_BlockedPrefixes =
        {
            "UnityEngine.", "UnityEditor.", "Unity.", "System.", "Microsoft.", "Mono.", "Newtonsoft.", "NUnit.", "JetBrains.",
        };

        const int k_MaxDepth = 16;
        const int k_NodeBudget = 10000;

        static object s_Result;
        static bool s_HasResult;
        static string s_ScriptAssembliesDir;

        /// <summary>Record the entry point's result (the last call wins); it is written inside the envelope.</summary>
        public static void WriteResult(object result)
        {
            s_Result = result;
            s_HasResult = true;
        }

        /// <summary>Cold door: batchmode `-executeMethod GameStudioKit.Editor.KitCli.RunFromEnv`.</summary>
        public static void RunFromEnv()
        {
            var method = Environment.GetEnvironmentVariable("GSK_EXEC_METHOD");
            var args = Environment.GetEnvironmentVariable("GSK_EXEC_ARGS");
            var outPath = Environment.GetEnvironmentVariable("GSK_EXEC_OUT");
            Run(method, args, outPath);
        }

        /// <summary>Warm door (and the shared implementation): run method with argsJson, write the envelope to outPath.</summary>
        /// <returns>The envelope JSON that was written.</returns>
        public static string Run(string method, string argsJson, string outPath)
        {
            Method = method ?? "";
            Args = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson;
            OutPath = string.IsNullOrWhiteSpace(outPath) ? DefaultOutPath() : outPath;
            s_Result = null;
            s_HasResult = false;
            bool ok = false;
            string error = null;
            try
            {
                var target = Resolve(Method, out error);
                if (target != null)
                {
                    target.Invoke(null, null);
                    ok = true;
                }
            }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException ?? e;
                error = inner.GetType().Name + ": " + inner.Message;
                Debug.LogError("[GSK] exec " + Method + " threw exception: " + inner);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                Debug.LogError("[GSK] exec " + Method + " threw exception: " + e);
            }
            var truncated = false;
            var resultJson = "null";
            if (s_HasResult)
            {
                try
                {
                    resultJson = ToJson(s_Result, out truncated);
                }
                catch (Exception e)
                {
                    ok = false;
                    error = (error == null ? "" : error + "; ") + "the result could not be serialized: " + e.GetType().Name + ": " + e.Message;
                }
            }
            var envelope = new StringBuilder();
            envelope.Append('{');
            envelope.Append("\"ok\":").Append(ok ? "true" : "false");
            envelope.Append(",\"method\":").Append(Quote(Method));
            envelope.Append(",\"utc\":").Append(Quote(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
            envelope.Append(",\"result\":").Append(resultJson);
            envelope.Append(",\"error\":").Append(error == null ? "null" : Quote(error));
            if (truncated) envelope.Append(",\"truncated\":true");
            envelope.Append('}');
            var json = envelope.ToString();
            WriteAtomic(OutPath, json);
            Debug.Log("[GSK] exec " + Method + (ok ? " ok" : " failed: " + error) + " -> " + OutPath);
            return json;
        }

        static MethodInfo Resolve(string qualified, out string error)
        {
            error = null;
            var dot = qualified.LastIndexOf('.');
            if (dot <= 0 || dot == qualified.Length - 1)
            {
                error = "expected Namespace.Class.Method, got '" + qualified + "'";
                return null;
            }
            foreach (var prefix in s_BlockedPrefixes)
            {
                if (qualified.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    error = qualified + " is engine, editor, runtime or library code: exec runs the project's own entry points only";
                    return null;
                }
            }
            var typeName = qualified.Substring(0, dot);
            var methodName = qualified.Substring(dot + 1);
            if (typeName == typeof(KitCli).FullName)
            {
                error = "KitCli itself is not an entry point";
                return null;
            }
            Type type = null;
            string refused = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try
                {
                    t = asm.GetType(typeName, false) ?? asm.GetType(NestedName(typeName), false);
                }
                catch (Exception)
                {
                    continue;
                }
                if (t == null) continue;
                if (!IsProjectAssembly(asm))
                {
                    refused = "type " + typeName + " is in " + asm.GetName().Name + ", which is not a project or kit assembly (Library/ScriptAssemblies)";
                    continue;
                }
                type = t;
                break;
            }
            if (type == null)
            {
                error = refused ?? "type " + typeName + " was not found in any loaded assembly (is it compiled, and in an Editor assembly?)";
                return null;
            }
            // public static void Method() only (CONTRACTS §7.9): no private helpers, no generic or value-returning methods.
            var m = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (m == null || m.ReturnType != typeof(void) || m.ContainsGenericParameters)
            {
                error = "type " + typeName + " has no public static void " + methodName + "() entry point";
                return null;
            }
            return m;
        }

        /// <summary>An assembly Unity compiled for this project (its own asmdefs, Assembly-CSharp*, the kit package).</summary>
        static bool IsProjectAssembly(Assembly asm)
        {
            if (asm.IsDynamic) return false;
            string location = null;
            try
            {
                location = asm.Location;
            }
            catch (Exception)
            {
                location = null;
            }
            if (!string.IsNullOrEmpty(location))
            {
                string full;
                try
                {
                    full = Path.GetFullPath(location);
                }
                catch (Exception)
                {
                    return false;
                }
                return full.StartsWith(ScriptAssembliesDir(), StringComparison.OrdinalIgnoreCase);
            }
            // Loaded from bytes: fall back to the assemblies the compilation pipeline builds for this project.
            var name = asm.GetName().Name;
            foreach (var a in global::UnityEditor.Compilation.CompilationPipeline.GetAssemblies(global::UnityEditor.Compilation.AssembliesType.Editor))
            {
                if (string.Equals(a.name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static string ScriptAssembliesDir()
        {
            if (s_ScriptAssembliesDir == null)
            {
                var root = Path.GetDirectoryName(Application.dataPath) ?? ".";
                var dir = Path.GetFullPath(Path.Combine(root, "Library", "ScriptAssemblies"));
                s_ScriptAssembliesDir = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            return s_ScriptAssembliesDir;
        }

        // "A.B.Outer.Inner" may be the nested type A.B.Outer+Inner.
        static string NestedName(string typeName)
        {
            var i = typeName.LastIndexOf('.');
            return i > 0 ? typeName.Substring(0, i) + "+" + typeName.Substring(i + 1) : typeName;
        }

        static string DefaultOutPath()
        {
            var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "exec");
            return Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".json");
        }

        static void WriteAtomic(string path, string text)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            File.WriteAllText(tmp, text + "\n", new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Serialize a value to JSON (see <see cref="ToJson(object, out bool)"/>).</summary>
        public static string ToJson(object value)
        {
            return ToJson(value, out _);
        }

        /// <summary>Serialize a value to JSON: null, bool, numbers, strings, enums (as names), dictionaries, lists, and
        /// objects. Value types (Vector3, Color, Matrix4x4, ...) write their public fields only, as JsonUtility does (their
        /// self-typed properties such as Color.linear or Matrix4x4.inverse would expand without end); a struct without
        /// public fields writes its readable properties. Classes write public fields and readable properties, skipping
        /// properties of their own type and reference cycles. Unity objects become {name, type}. Depth is capped at 16 and
        /// the whole value at 10,000 nodes; past either, values become null and <paramref name="truncated"/> is true.</summary>
        public static string ToJson(object value, out bool truncated)
        {
            var sb = new StringBuilder();
            var state = new JsonState();
            Write(sb, value, 0, state);
            truncated = state.Truncated;
            return sb.ToString();
        }

        sealed class JsonState
        {
            public int Nodes;
            public bool Truncated;
            public readonly HashSet<object> Open = new HashSet<object>(ReferenceComparer.Instance);
            public bool Exhausted => Nodes > k_NodeBudget;
        }

        sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => RuntimeHelpers.GetHashCode(o);
        }

        static void Write(StringBuilder sb, object v, int depth, JsonState st)
        {
            if (v == null) { sb.Append("null"); return; }
            st.Nodes++;
            if (depth > k_MaxDepth || st.Exhausted)
            {
                st.Truncated = true;
                sb.Append("null");
                return;
            }
            switch (v)
            {
                case string s: sb.Append(Quote(s)); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case char c: sb.Append(Quote(c.ToString())); return;
                case Enum e: sb.Append(Quote(e.ToString())); return;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case int _: case long _: case short _: case byte _: case sbyte _: case uint _: case ulong _: case ushort _:
                    sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return;
                case DateTime dt: sb.Append(Quote(dt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))); return;
                case DateTimeOffset dto: sb.Append(Quote(dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))); return;
                case TimeSpan ts: sb.Append(ts.TotalSeconds.ToString("R", CultureInfo.InvariantCulture)); return;
                case Guid g: sb.Append(Quote(g.ToString())); return;
                case Type t: sb.Append(Quote(t.FullName)); return;
                case UnityEngine.Object uo:
                    sb.Append("{\"name\":").Append(Quote(uo ? uo.name : null)).Append(",\"type\":").Append(Quote(uo ? uo.GetType().Name : null)).Append('}');
                    return;
            }
            var type = v.GetType();
            if (type.IsPointer || v is Delegate || v is MemberInfo || v is Assembly || v is IntPtr || v is UIntPtr)
            {
                sb.Append(Quote(type.Name));
                return;
            }
            var isRef = !type.IsValueType;
            if (isRef && !st.Open.Add(v))
            {
                sb.Append("null"); // a reference cycle
                return;
            }
            try
            {
                if (v is IDictionary dict)
                {
                    sb.Append('{');
                    var first = true;
                    foreach (DictionaryEntry kv in dict)
                    {
                        if (st.Exhausted) { st.Truncated = true; break; }
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append(Quote(Convert.ToString(kv.Key, CultureInfo.InvariantCulture))).Append(':');
                        Write(sb, kv.Value, depth + 1, st);
                    }
                    sb.Append('}');
                    return;
                }
                if (v is IEnumerable list)
                {
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (st.Exhausted) { st.Truncated = true; break; }
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, item, depth + 1, st);
                    }
                    sb.Append(']');
                    return;
                }
                sb.Append('{');
                var firstMember = true;
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (st.Exhausted) { st.Truncated = true; break; }
                    object fv;
                    try { fv = field.GetValue(v); }
                    catch (Exception) { continue; }
                    if (!firstMember) sb.Append(',');
                    firstMember = false;
                    sb.Append(Quote(field.Name)).Append(':');
                    Write(sb, fv, depth + 1, st);
                }
                // Value types with public fields are complete (JsonUtility semantics); others add readable properties.
                if (!type.IsValueType || fields.Length == 0)
                {
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (st.Exhausted) { st.Truncated = true; break; }
                        if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
                        if (prop.PropertyType == type || prop.PropertyType.IsPointer) continue; // Color.linear, Matrix4x4.inverse, ...
                        object pv;
                        try { pv = prop.GetValue(v, null); }
                        catch (Exception) { continue; }
                        if (!firstMember) sb.Append(',');
                        firstMember = false;
                        sb.Append(Quote(prop.Name)).Append(':');
                        Write(sb, pv, depth + 1, st);
                    }
                }
                sb.Append('}');
            }
            finally
            {
                if (isRef) st.Open.Remove(v);
            }
        }

        /// <summary>A JSON string literal (also a valid C# literal for the gate's eval call).</summary>
        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
