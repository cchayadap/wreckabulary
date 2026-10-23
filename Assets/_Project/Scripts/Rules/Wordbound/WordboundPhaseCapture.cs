#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WordboundPhase
    {
        RollbackCapture, Synchronize, RetireResources, FullValidation, MutableValidation,
        EconomyValidation, ObserveHost, Prepare, FrameChanged,
        ProgressRestore, ResourcesValidation, BodiesValidation, MechanismsValidation, BodyBudgetValidation
    }

    [Flags]
    public enum WordboundPhaseInvalid
    {
        None = 0, WrongThread = 1, Unfinished = 2, LateScope = 4,
        Unbalanced = 8, Clock = 16, Overflow = 32, InvalidPhase = 64
    }

    public readonly struct WordboundPhaseBoundary
    {
        public readonly long Epoch, Tick;
        public readonly int Bodies, Resources, Mechanisms;
        public WordboundPhaseBoundary(MatchSnapshot frame)
        {
            Epoch = frame?.Epoch ?? -1; Tick = frame?.Progress.Tick ?? -1;
            Bodies = frame?.Bodies.Count ?? -1; Resources = frame?.Resources.Count ?? -1;
            Mechanisms = frame?.Mechanisms.Count ?? -1;
        }
        public override string ToString() => FormattableString.Invariant(
            $"epoch={Epoch},tick={Tick},bodies={Bodies},resources={Resources},mechanisms={Mechanisms}");
    }

    public sealed class WordboundPhaseCapture : IDisposable
    {
        public const int PhaseCount = 14;
        [ThreadStatic] static WordboundPhaseCapture active;
        static long nextGeneration;
        readonly long[] counts = new long[PhaseCount], totals = new long[PhaseCount], maxima = new long[PhaseCount];
        readonly long[] scopes = new long[256];
        readonly int owner = Thread.CurrentThread.ManagedThreadId;
        readonly long started = Stopwatch.GetTimestamp();
        long ended, nextScope;
        int invalid, inFlight;
        bool stopped;

        public long Generation { get; } = Interlocked.Increment(ref nextGeneration);
        public WordboundPhaseInvalid Invalid => (WordboundPhaseInvalid)Volatile.Read(ref invalid);
        public int InFlight => inFlight;
        public bool Stopped => stopped;
        public long ElapsedTicks => stopped ? ended - started : -1;

        WordboundPhaseCapture() { }
        public static WordboundPhaseCapture TryStart()
        {
            if (active != null) return null;
            return active = new WordboundPhaseCapture();
        }

        public static Scope Measure(WordboundPhase phase)
        {
            var capture = active;
            if (capture == null) return default;
            int index = (int)phase;
            if (index < 0 || index >= PhaseCount) { capture.Invalidate(WordboundPhaseInvalid.InvalidPhase); return default; }
            if (capture.inFlight == capture.scopes.Length || capture.nextScope == long.MaxValue)
            { capture.Invalidate(WordboundPhaseInvalid.Overflow); return default; }
            int depth = capture.inFlight++;
            long token = capture.scopes[depth] = ++capture.nextScope;
            return new Scope(capture, index, depth, token, Stopwatch.GetTimestamp());
        }

        public readonly struct Scope : IDisposable
        {
            readonly WordboundPhaseCapture capture;
            readonly int phase, depth;
            readonly long token, started;
            internal Scope(WordboundPhaseCapture capture, int phase, int depth, long token, long started)
            { this.capture = capture; this.phase = phase; this.depth = depth; this.token = token; this.started = started; }
            public void Dispose() => capture?.End(phase, depth, token, started);
        }

        void End(int phase, int depth, long token, long begin)
        {
            if (Thread.CurrentThread.ManagedThreadId != owner) { Invalidate(WordboundPhaseInvalid.WrongThread); return; }
            if (stopped) { Invalidate(WordboundPhaseInvalid.LateScope); return; }
            if (!ReferenceEquals(active, this) || inFlight != depth + 1 || scopes[depth] != token)
            { Invalidate(WordboundPhaseInvalid.Unbalanced); return; }
            inFlight--;
            long duration = Stopwatch.GetTimestamp() - begin;
            if (duration < 0) { Invalidate(WordboundPhaseInvalid.Clock); return; }
            if (counts[phase] == long.MaxValue || totals[phase] > long.MaxValue - duration)
            { Invalidate(WordboundPhaseInvalid.Overflow); return; }
            counts[phase]++; totals[phase] += duration; maxima[phase] = Math.Max(maxima[phase], duration);
        }

        void Invalidate(WordboundPhaseInvalid reason)
        {
            int observed, replacement;
            do { observed = Volatile.Read(ref invalid); replacement = observed | (int)reason; }
            while (Interlocked.CompareExchange(ref invalid, replacement, observed) != observed);
        }

        public void Dispose()
        {
            if (Thread.CurrentThread.ManagedThreadId != owner) { Invalidate(WordboundPhaseInvalid.WrongThread); return; }
            if (stopped) return;
            ended = Stopwatch.GetTimestamp(); stopped = true;
            if (ended < started) Invalidate(WordboundPhaseInvalid.Clock);
            if (inFlight != 0) Invalidate(WordboundPhaseInvalid.Unfinished);
            if (ReferenceEquals(active, this)) active = null;
            else Invalidate(WordboundPhaseInvalid.Unbalanced);
        }

        public long Count(WordboundPhase phase) => counts[(int)phase];
        public long TotalTicks(WordboundPhase phase) => totals[(int)phase];
        public long MaximumTicks(WordboundPhase phase) => maxima[(int)phase];

        public IEnumerable<string> Report(string window, string outcome, WordboundPhaseBoundary first, WordboundPhaseBoundary last)
        {
            if (!stopped) throw new InvalidOperationException("Stop phase capture before reporting.");
            string status = Invalid == WordboundPhaseInvalid.None ? "observed" : "invalid";
            yield return "WORDBOUND_PHASE_WINDOW: window=" + window + "; outcome=" + outcome +
                "; generation=" + Generation + "; thread=" + owner + "; frequency=" + Stopwatch.Frequency +
                "; elapsedTicks=" + ElapsedTicks + "; invalid=" + Invalid + "; inFlight=" + inFlight +
                "; scope=inclusive-elapsed-not-cpu; instances=all-on-owner-thread; fullValidationContexts=combined" +
                "; first={" + first + "}; last={" + last + "}";
            for (int i = 0; i < PhaseCount; i++)
                yield return "WORDBOUND_PHASE: window=" + window + "; generation=" + Generation + "; phase=" + (WordboundPhase)i +
                    "; status=" + (counts[i] == 0 && status == "observed" ? "unavailable" : status) +
                    "; count=" + counts[i] + "; totalTicks=" + totals[i] + "; maxTicks=" + maxima[i] +
                    "; elapsedMs=" + (counts[i] > 0 && status == "observed" ?
                        (totals[i] * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) : "unavailable");
        }
    }
}
#endif
