using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Debug = UnityEngine.Debug;

namespace Wreckabulary.OnlineGameplayTests
{
    internal sealed class WordboundNativeCheckpoints : IDisposable
    {
        readonly Probe[] peers = new Probe[8];
        bool stopped;
        internal static WordboundNativeCheckpoints StartIfEnabled() =>
            Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_CHECKPOINTS") == "1" ? new WordboundNativeCheckpoints() : null;
        internal Probe ForPeer(int index) => peers[index] ?? (peers[index] = new Probe(index));
        public void Dispose()
        {
            if (stopped) return;
            stopped = true;
            foreach (var peer in peers)
            {
                if (peer == null) continue;
                try { peer.Finish(); }
                catch (Exception error) { Debug.Log("WORDBOUND_CHECKPOINT_UNAVAILABLE: " + error.GetType().Name); }
            }
        }

        internal enum Stage { Publish, Validate, Acknowledge, Begin, Chunk, Finalize, Head, Renew, Read, Claim, Open }
        internal enum TickState { NotReady, Migration, Result, DurableGapObserved, OtherPaused, Eligible }
        internal struct Total
        {
            internal long Count, Failed, Elapsed, Maximum, InFlight, MaximumInFlight;
        }
        internal sealed class Probe
        {
            readonly object sync = new object();
            readonly int peerIndex;
            readonly long started = Stopwatch.GetTimestamp();
            readonly Total[] stages = new Total[11];
            readonly long[] samples = new long[6], intervals = new long[6];
            OnlineSnapshotMark pending, observedCommitted;
            long pendingStarted, committedObservedAt, accepted, unmatchedAccepted, superseded, commitTotal, commitMaximum;
            long firstTick = -1, lastTick = -1, firstSdkTick = -1, lastSdkTick = -1, maximumGap, unavailable, staleSamples;
            long previousSample, maximumSampleGap;
            int creation;
            TickState previousState;
            bool stopped;
            WordboundNativeTickBatchAdapter adapter;
            Action<NetworkRunner> callback;

            internal Probe(int peerIndex) { this.peerIndex = peerIndex; }
            internal IWordboundFusionAuthority Wrap(IWordboundFusionAuthority source) => new Authority(this, source);
            internal IOnlineHttpTransport Wrap(IOnlineHttpTransport source) => new Transport(this, source);
            internal long Begin(Stage stage)
            {
                long now = Stopwatch.GetTimestamp();
                lock (sync)
                {
                    if (!stopped)
                    {
                        ref var total = ref stages[(int)stage];
                        total.InFlight++; total.MaximumInFlight = Math.Max(total.MaximumInFlight, total.InFlight);
                    }
                }
                return now;
            }
            internal void End(Stage stage, long start, bool success)
            {
                long elapsed = Stopwatch.GetTimestamp() - start;
                lock (sync)
                {
                    if (stopped) return;
                    ref var total = ref stages[(int)stage];
                    total.InFlight--; total.Count++; if (!success) total.Failed++;
                    total.Elapsed += elapsed; total.Maximum = Math.Max(total.Maximum, elapsed);
                }
            }
            internal void Published(OnlineSnapshotMark mark, long start)
            {
                lock (sync)
                {
                    if (stopped) return;
                    if (pending != null) superseded++;
                    pending = mark; pendingStarted = start;
                }
            }
            internal void Acknowledged(OnlineSnapshotMark mark)
            {
                long now = Stopwatch.GetTimestamp();
                lock (sync)
                {
                    if (stopped) return;
                    accepted++;
                    if (pending == null || !pending.Same(mark)) { unmatchedAccepted++; return; }
                    long elapsed = now - pendingStarted;
                    commitTotal += elapsed; commitMaximum = Math.Max(commitMaximum, elapsed); pending = null;
                }
            }
            internal void Attach(NetworkRunner runner, int runnerCreation, Func<NetworkRunner> current,
                WordboundGameplayController controller, Func<WordboundFusionSession> session)
            {
                Detach();
                creation = runnerCreation; previousSample = 0;
                adapter = runner.GetComponent<WordboundNativeTickBatchAdapter>() ?? runner.gameObject.AddComponent<WordboundNativeTickBatchAdapter>();
                callback = observed => {
                    try
                    {
                        if (!ReferenceEquals(observed, current())) { lock (sync) { if (!stopped) staleSamples++; } return; }
                        if (!observed.IsServer || !observed.IsForward) return;
                        var snapshot = controller.Frame;
                        var active = session();
                        Sample(observed.Tick.Raw, snapshot?.Progress.Tick ?? -1, snapshot?.Epoch ?? -1,
                            active?.Committed, active?.GameplayReady ?? false, snapshot?.Progress.Migrating ?? false,
                            snapshot?.Progress.Result != null, controller.IsSimulationPaused, controller.Authority.Match.IsPaused);
                    }
                    catch { lock (sync) { if (!stopped) unavailable++; } }
                };
                adapter.TickStarting += callback;
            }
            void Detach()
            {
                if (adapter != null && callback != null) adapter.TickStarting -= callback;
                adapter = null; callback = null;
            }
            static bool DurableGap(long tick, long epoch, OnlineSnapshotMark committed) => tick >= 0 && committed != null &&
                committed.Epoch == epoch && (tick + 1 < committed.Tick || tick + 1 - committed.Tick >
                    ModeDefinition.ProvisionalWindowSeconds * ModeDefinition.TickRate);
            internal void Sample(long sdkTick, long tick, long epoch, OnlineSnapshotMark committed, bool ready,
                bool migrating, bool result, bool paused, bool authorityPaused)
            {
                long now = Stopwatch.GetTimestamp();
                lock (sync)
                {
                    if (stopped) return;
                    ObserveCommitted(committed, now);
                    var state = !ready || tick < 0 ? TickState.NotReady : migrating ? TickState.Migration :
                        result ? TickState.Result : paused || authorityPaused ?
                        DurableGap(tick, epoch, committed) ? TickState.DurableGapObserved : TickState.OtherPaused : TickState.Eligible;
                    if (previousSample != 0)
                    {
                        long gap = now - previousSample;
                        intervals[(int)previousState] += gap; maximumSampleGap = Math.Max(maximumSampleGap, gap);
                    }
                    previousSample = now; previousState = state; samples[(int)state]++;
                    if (firstTick < 0 && tick >= 0) firstTick = tick;
                    if (firstSdkTick < 0) firstSdkTick = sdkTick;
                    lastTick = tick; lastSdkTick = sdkTick;
                    if (committed != null && committed.Epoch == epoch) maximumGap = Math.Max(maximumGap, tick - committed.Tick);
                }
            }
            void ObserveCommitted(OnlineSnapshotMark mark, long now)
            {
                if (mark == null || observedCommitted != null && observedCommitted.Same(mark)) return;
                observedCommitted = mark; committedObservedAt = now;
            }
            internal string Facts(WordboundGameplayController controller, WordboundFusionSession session)
            {
                try
                {
                    var frame = controller.Frame; var mark = session.Committed;
                    long now = Stopwatch.GetTimestamp();
                    lock (sync)
                    {
                        if (!stopped) ObserveCommitted(mark, now);
                        string age = mark == null ? "unavailable" : Ms(now - committedObservedAt);
                        string pendingAge = pending == null ? "none" : Ms(now - pendingStarted);
                        return $",checkpointCanonicalTick={frame?.Progress.Tick},committedTick={mark?.Tick},committedEpoch={mark?.Epoch},committedObservedAgeMs={age},migration={frame?.Progress.Migrating},result={frame?.Progress.Result != null},authorityPaused={controller.Authority.Match.IsPaused},checkpointPendingAgeMs={pendingAge}";
                    }
                }
                catch { return ",checkpointFactsUnavailable=true"; }
            }
            internal Total StageTotal(Stage stage) { lock (sync) return stages[(int)stage]; }
            internal (long Accepted, long Unmatched, long Superseded, bool Pending) PublicationCounts()
            { lock (sync) return (accepted, unmatchedAccepted, superseded, pending != null); }
            internal long Samples(TickState state) { lock (sync) return samples[(int)state]; }
            internal void Finish()
            {
                Detach();
                lock (sync)
                {
                    if (stopped) return;
                    stopped = true;
                    long now = Stopwatch.GetTimestamp();
                    Debug.Log($"WORDBOUND_CHECKPOINT_WINDOW: peer={peerIndex}; runnerCreations={creation}; elapsedMs={Ms(now - started)}; canonicalFirst={firstTick}; canonicalLast={lastTick}; sdkFirst={firstSdkTick}; sdkLast={lastSdkTick}; maxCommittedGapTicks={maximumGap}; maxSampleGapMs={Ms(maximumSampleGap)}; staleSamples={staleSamples}; unavailable={unavailable}; accepted={accepted}; unmatchedAccepted={unmatchedAccepted}; superseded={superseded}; pending={pending != null}; matchedPublicationToAckTotalMs={Ms(commitTotal)}; matchedPublicationToAckMaxMs={Ms(commitMaximum)}; clock=Stopwatch; duration=inclusive-continuation-observed-wall-not-cpu-or-http-rtt; end=accepted-authority-ack-before-session-and-controller-scheduling; prepareCompression=unisolated; stages=nested-and-parallel-not-additive; sampleIntervals=previous-label-between-observations-not-exact-pause-time; finalInterval=omitted");
                    for (int i = 0; i < stages.Length; i++)
                    {
                        var total = stages[i];
                        Debug.Log($"WORDBOUND_CHECKPOINT_STAGE: peer={peerIndex}; stage={(Stage)i}; completed={total.Count}; failed={total.Failed}; inFlight={total.InFlight}; maxInFlight={total.MaximumInFlight}; totalMs={Ms(total.Elapsed)}; maxMs={Ms(total.Maximum)}");
                    }
                    for (int i = 0; i < samples.Length; i++)
                        Debug.Log($"WORDBOUND_CHECKPOINT_TICKS: peer={peerIndex}; state={(TickState)i}; samples={samples[i]}; observedIntervalsMs={Ms(intervals[i])}");
                }
            }
        }
        static string Ms(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);

        sealed class Authority : IWordboundFusionAuthority
        {
            readonly Probe probe;
            readonly IWordboundFusionAuthority inner;
            internal Authority(Probe probe, IWordboundFusionAuthority inner) { this.probe = probe; this.inner = inner; }
            public string IdentityHash => inner.IdentityHash;
            public long Epoch => inner.Epoch;
            public void AcceptCommand(string actor, JsonNode command) => inner.AcceptCommand(actor, command);
            public OnlineSnapshotMark Publish(out string json)
            {
                long start = probe.Begin(Stage.Publish); bool success = false;
                try { var mark = inner.Publish(out json); probe.Published(mark, start); success = true; return mark; }
                finally { probe.End(Stage.Publish, start, success); }
            }
            public void ValidateSnapshot(string json, OnlineSnapshotMark mark)
            {
                long start = probe.Begin(Stage.Validate); bool success = false;
                try { inner.ValidateSnapshot(json, mark); success = true; }
                finally { probe.End(Stage.Validate, start, success); }
            }
            public bool AcknowledgeDurable(OnlineSnapshotMark mark)
            {
                long start = probe.Begin(Stage.Acknowledge); bool success = false;
                try { bool accepted = inner.AcknowledgeDurable(mark); if (accepted) probe.Acknowledged(mark); success = true; return accepted; }
                finally { probe.End(Stage.Acknowledge, start, success); }
            }
            public void PauseForMigration() => inner.PauseForMigration();
            public void RestoreCommitted(string json, OnlineSnapshotMark mark, long epoch, long wallClock) => inner.RestoreCommitted(json, mark, epoch, wallClock);
            public IReadOnlyList<OnlineSeatPresence> Presence() => inner.Presence();
            public void SetTransportReady(bool ready, bool host) => inner.SetTransportReady(ready, host);
            public void Disconnect(string actor, long wallClock) => inner.Disconnect(actor, wallClock);
            public bool Reconnect(string actor, long wallClock) => inner.Reconnect(actor, wallClock);
            public void Return(string actor, long wallClock) => inner.Return(actor, wallClock);
        }

        sealed class Transport : IOnlineHttpTransport
        {
            readonly Probe probe;
            readonly IOnlineHttpTransport inner;
            internal Transport(Probe probe, IOnlineHttpTransport inner) { this.probe = probe; this.inner = inner; }
            public Task<OnlineResponse> Send(OnlineRequest request, CancellationToken ct)
            {
                Stage? stage = Route(request.Path);
                return stage.HasValue ? Observe(request, ct, stage.Value) : inner.Send(request, ct);
            }
            async Task<OnlineResponse> Observe(OnlineRequest request, CancellationToken ct, Stage stage)
            {
                long start = probe.Begin(stage); bool success = false;
                try
                {
                    var response = await inner.Send(request, ct).ConfigureAwait(false);
                    success = response.Status >= 200 && response.Status < 300; return response;
                }
                finally { probe.End(stage, start, success); }
            }
        }
        internal static Stage? Route(string path)
        {
            if (path == null) return null;
            int start = path.LastIndexOf('/');
            string action = start < 0 ? path : path.Substring(start + 1);
            switch (action)
            {
                case "checkpoint-begin": return Stage.Begin;
                case "checkpoint-chunk": return Stage.Chunk;
                case "checkpoint-finalize": return Stage.Finalize;
                case "checkpoint-head": return Stage.Head;
                case "checkpoint-renew": return Stage.Renew;
                case "checkpoint-read": return Stage.Read;
                case "checkpoint-claim": return Stage.Claim;
                case "checkpoint-open": return Stage.Open;
                default: return null;
            }
        }
    }
}
