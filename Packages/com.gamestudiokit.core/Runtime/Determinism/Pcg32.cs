// Game Studio Kit (MIT). PCG32 random number generator (PCG-XSH-RR 64/32, M. E. O'Neill, pcg-random.org), implemented
// from the published algorithm. Use named streams (seed, purpose, index) instead of UnityEngine.Random or System.Random:
// the same seed gives the same sequence on every platform and runtime, and separate purposes never share state.
// Engine-free (assembly GameStudioKit.Determinism, noEngineReferences).
using System;
using System.Collections.Generic;

namespace GameStudioKit.Determinism
{
    public struct Pcg32
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong _state;
        ulong _inc;

        /// <summary>The reference seeding: state and stream (sequence) selector.</summary>
        public Pcg32(ulong seed, ulong stream)
        {
            _state = 0UL;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        /// <summary>
        /// A named stream: the same (seed, purpose, index) always gives the same sequence, and different purposes or
        /// indices give independent sequences. Example: Pcg32.ForStream(runSeed, "loot", doorIndex).
        /// </summary>
        public static Pcg32 ForStream(ulong seed, string purpose, long index = 0)
        {
            ulong p = Hash64.Of(purpose);
            ulong s = Hash64.Combine(seed, p);
            ulong q = Hash64.Combine(p, unchecked((ulong)index));
            return new Pcg32(Hash64.Combine(s, unchecked((ulong)index)), q);
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        /// <summary>Uniform integer in [0, bound) without modulo bias (rejection sampling). bound must be &gt; 0.</summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound), "bound must be > 0");
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return r % bound;
            }
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = unchecked((uint)(maxExclusive - minInclusive));
            return unchecked(minInclusive + (int)NextUInt(span));
        }

        /// <summary>Uniform float in [0, 1) with 24 random bits.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform double in [0, 1) with 53 random bits.</summary>
        public double NextDouble()
        {
            ulong hi = NextUInt() >> 5;
            ulong lo = NextUInt() >> 6;
            return (hi * 67108864.0 + lo) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        public bool Chance(double probability) => NextDouble() < probability;

        /// <summary>Fisher–Yates shuffle in place.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)NextUInt((uint)(i + 1));
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public T Pick<T>(IList<T> list)
        {
            if (list == null || list.Count == 0) throw new ArgumentException("cannot pick from an empty list");
            return list[(int)NextUInt((uint)list.Count)];
        }

        /// <summary>Skips ahead by <paramref name="delta"/> steps in O(log delta) (the PCG jump-ahead).</summary>
        public void Advance(ulong delta)
        {
            ulong curMult = Multiplier, curPlus = _inc, accMult = 1UL, accPlus = 0UL;
            while (delta > 0)
            {
                if ((delta & 1UL) != 0)
                {
                    accMult = unchecked(accMult * curMult);
                    accPlus = unchecked(accPlus * curMult + curPlus);
                }
                curPlus = unchecked((curMult + 1UL) * curPlus);
                curMult = unchecked(curMult * curMult);
                delta >>= 1;
            }
            _state = unchecked(accMult * _state + accPlus);
        }
    }
}
