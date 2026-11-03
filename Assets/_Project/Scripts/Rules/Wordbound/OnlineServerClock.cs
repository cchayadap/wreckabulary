using System;
using System.Diagnostics;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class OnlineServerClock
    {
        public const long MaximumRoundTripMilliseconds = 20000;
        public const long MaximumResolutionMilliseconds = 1000;
        public const long ConfirmedJumpMilliseconds = 3000;
        readonly Func<long> monotonic;
        readonly object gate = new object();
        long offset, uncertainty, sampledAt, last = long.MinValue;
        long candidate, candidateError, candidateAt;
        bool synchronized, hasCandidate;

        public OnlineServerClock(Func<long> wallClock = null, Func<long> monotonicClock = null)
        {
            monotonic = monotonicClock ?? (() => (long)(Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency)));
            long wall = (wallClock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))();
            offset = wall - monotonic();
        }

        public bool Synchronized { get { lock (gate) return synchronized; } }
        public long UncertaintyMilliseconds { get { lock (gate) return synchronized ? uncertainty : long.MaxValue; } }
        public long Local() => monotonic();

        public long Now()
        {
            lock (gate)
            {
                long value = monotonic() + offset;
                if (value < last) value = last;
                return last = value;
            }
        }

        public bool Observe(long serverMilliseconds, long sentLocal, long receivedLocal, long resolutionMilliseconds = 0)
        {
            long roundTrip = receivedLocal - sentLocal;
            if (serverMilliseconds <= 0 || sentLocal < 0 || roundTrip < 0 || roundTrip > MaximumRoundTripMilliseconds ||
                resolutionMilliseconds < 0 || resolutionMilliseconds > MaximumResolutionMilliseconds) return false;
            long estimate = serverMilliseconds + resolutionMilliseconds / 2 - (sentLocal + roundTrip / 2);
            long error = (roundTrip + 1) / 2 + (resolutionMilliseconds + 1) / 2;
            lock (gate)
            {
                if (synchronized)
                {
                    long aged = uncertainty + Math.Max(0, receivedLocal - sampledAt) / 1000;
                    long shift = Math.Abs(estimate - offset);
                    if (shift > ConfirmedJumpMilliseconds + error + aged)
                    {
                        bool confirmed = hasCandidate && Math.Abs(estimate - candidate) <=
                            error + candidateError + Math.Max(0, receivedLocal - candidateAt) / 1000;
                        candidate = estimate; candidateError = error; candidateAt = receivedLocal; hasCandidate = true;
                        if (!confirmed) return false;
                    }
                    else if (error > aged && shift <= error + aged) return false;
                }
                offset = estimate; uncertainty = error; sampledAt = receivedLocal; synchronized = true; hasCandidate = false;
                return true;
            }
        }
    }
}
