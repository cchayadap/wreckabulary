// Game Studio Kit (MIT). Stable 64-bit hashing for persisted values, seeds and game-state checkpoints. Never use
// object.GetHashCode or string.GetHashCode for anything saved or compared across runs: they differ between runtimes and
// processes. Engine-free (assembly GameStudioKit.Determinism, noEngineReferences), so Core code can use it.
// FNV-1a 64 over UTF-8 for text, and a SplitMix64 finaliser for mixing.
using System;

namespace GameStudioKit.Determinism
{
    public static class Hash64
    {
        public const ulong FnvOffset = 14695981039346656037UL;
        public const ulong FnvPrime = 1099511628211UL;

        /// <summary>FNV-1a 64 of the string's UTF-8 bytes (encoded without allocation). Null hashes like "".</summary>
        public static ulong Of(string s)
        {
            ulong h = FnvOffset;
            if (s == null) return h;
            for (int i = 0; i < s.Length; i++)
            {
                int cp = s[i];
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    cp = char.ConvertToUtf32(s[i], s[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(s[i])) cp = 0xFFFD;

                if (cp < 0x80) h = Step(h, (byte)cp);
                else if (cp < 0x800)
                {
                    h = Step(h, (byte)(0xC0 | (cp >> 6)));
                    h = Step(h, (byte)(0x80 | (cp & 0x3F)));
                }
                else if (cp < 0x10000)
                {
                    h = Step(h, (byte)(0xE0 | (cp >> 12)));
                    h = Step(h, (byte)(0x80 | ((cp >> 6) & 0x3F)));
                    h = Step(h, (byte)(0x80 | (cp & 0x3F)));
                }
                else
                {
                    h = Step(h, (byte)(0xF0 | (cp >> 18)));
                    h = Step(h, (byte)(0x80 | ((cp >> 12) & 0x3F)));
                    h = Step(h, (byte)(0x80 | ((cp >> 6) & 0x3F)));
                    h = Step(h, (byte)(0x80 | (cp & 0x3F)));
                }
            }
            return h;
        }

        /// <summary>FNV-1a 64 of raw bytes.</summary>
        public static ulong Of(byte[] data)
        {
            ulong h = FnvOffset;
            if (data == null) return h;
            foreach (byte b in data) h = Step(h, b);
            return h;
        }

        static ulong Step(ulong h, byte b) => (h ^ b) * FnvPrime;

        /// <summary>SplitMix64 finaliser: a bijective avalanche of 64 bits.</summary>
        public static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Order-dependent combination of two hashes.</summary>
        public static ulong Combine(ulong a, ulong b) => Mix(a ^ Mix(b + 0x9E3779B97F4A7C15UL));

        /// <summary>16 lower-case hex digits (the checks.json state_hashes format).</summary>
        public static string ToHex(ulong h) => h.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Incremental state hash: add fields in a fixed order, read Value. A class, so every Add changes the same builder:
        /// <c>b.Add(seed).Add(tick); return b.Value;</c> includes both fields (a mutable struct would silently drop every
        /// field after the first when chained on a variable).
        /// </summary>
        public sealed class Builder
        {
            ulong _h;
            bool _started;

            public ulong Value => _started ? _h : FnvOffset;

            public Builder Add(ulong v)
            {
                _h = _started ? Combine(_h, v) : Combine(FnvOffset, v);
                _started = true;
                return this;
            }

            public Builder Add(long v) => Add(unchecked((ulong)v));
            public Builder Add(int v) => Add(unchecked((ulong)(long)v));
            public Builder Add(uint v) => Add((ulong)v);
            public Builder Add(bool v) => Add(v ? 1UL : 0UL);
            public Builder Add(string s) => Add(Of(s));

            /// <summary>Floats hash by bit pattern, with -0 folded into +0 and every NaN into one value.</summary>
            public Builder Add(float v)
            {
                if (v == 0f) v = 0f;
                if (float.IsNaN(v)) v = float.NaN;
                return Add(unchecked((ulong)(uint)BitConverter.SingleToInt32Bits(v)));
            }

            public Builder Add(double v)
            {
                if (v == 0d) v = 0d;
                if (double.IsNaN(v)) v = double.NaN;
                return Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(v)));
            }

            /// <summary>Quantises to a grid first, so tiny float noise does not change the hash.</summary>
            public Builder AddQuantized(double v, double step) => Add((long)Math.Round(v / step));
        }
    }
}
