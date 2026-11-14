using System;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Wordbound
{
    public struct WordboundStream
    {
        const ulong Multiplier = 6364136223846793005UL;

        ulong state;
        ulong increment;

        public WordboundStream(ulong seed, ulong stream)
        {
            state = 0UL;
            increment = (stream << 1) | 1UL;
            NextUInt();
            state = unchecked(state + seed);
            NextUInt();
        }

        public ulong State => state;
        public ulong Increment => increment;

        public static WordboundStream ForStream(ulong seed, string purpose, long index = 0)
        {
            ulong p = Hash64.Of(purpose);
            ulong s = Hash64.Combine(seed, p);
            ulong q = Hash64.Combine(p, unchecked((ulong)index));
            return new WordboundStream(Hash64.Combine(s, unchecked((ulong)index)), q);
        }

        public static WordboundStream FromState(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0) throw new ArgumentException("A PCG increment must be odd.", nameof(increment));
            return new WordboundStream { state = state, increment = increment };
        }

        public static WordboundStream Restore(MatchRandomState saved)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            return FromState(saved.State, saved.Increment);
        }

        public MatchRandomState Capture(string streamId) => new MatchRandomState(streamId, state, increment);

        public uint NextUInt()
        {
            ulong old = state;
            state = unchecked(old * Multiplier + increment);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

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

        public int Below(int bound)
        {
            if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound), "bound must be > 0");
            return (int)NextUInt((uint)bound);
        }
    }
}
