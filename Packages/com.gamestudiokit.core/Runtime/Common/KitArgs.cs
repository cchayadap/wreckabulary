// Game Studio Kit (MIT). Engine-free player/editor command-line argument reader for the -qa-* contract (CONTRACTS §3.6).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameStudioKit.Common
{
    /// <summary>
    /// Reads "-name value" pairs and "-flag" switches. Names compare case-insensitively. A value may not start with '-'
    /// unless it parses as a number (so "-qa-seed -5" works).
    /// </summary>
    public sealed class KitArgs
    {
        readonly string[] _args;
        // gsk-lint-ignore static-without-reset: engine-free cache of the process command line, which cannot change within a process; tests restore it with OverrideForTests(null)
        static KitArgs _current;

        public KitArgs(string[] args) { _args = args ?? new string[0]; }

        /// <summary>The process command line (Environment.GetCommandLineArgs), cached.</summary>
        public static KitArgs Current
        {
            get
            {
                if (_current == null)
                {
                    string[] a;
                    try { a = Environment.GetCommandLineArgs(); }
                    catch (Exception) { a = new string[0]; }
                    _current = new KitArgs(a);
                }
                return _current;
            }
        }

        /// <summary>Replaces the cached command line (tests only).</summary>
        public static void OverrideForTests(string[] args) { _current = args == null ? null : new KitArgs(args); }

        public IReadOnlyList<string> All => _args;

        public bool Has(string name) => IndexOf(name) >= 0;

        public string Get(string name, string fallback = null)
        {
            int i = IndexOf(name);
            if (i < 0 || i + 1 >= _args.Length) return fallback;
            string v = _args[i + 1];
            if (v.StartsWith("-", StringComparison.Ordinal) && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return fallback;
            return v;
        }

        public int GetInt(string name, int fallback)
        {
            string v = Get(name);
            return v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : fallback;
        }

        public float GetFloat(string name, float fallback)
        {
            string v = Get(name);
            return v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : fallback;
        }

        int IndexOf(string name)
        {
            for (int i = 0; i < _args.Length; i++)
                if (string.Equals(_args[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
