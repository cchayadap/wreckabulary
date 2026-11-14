using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Debug = UnityEngine.Debug;

namespace Wreckabulary.OnlineGameplayTests
{
    internal sealed class WordboundNativeTickBatches : IDisposable
    {
        readonly List<Observation> observations = new List<Observation>();
        readonly string testName = TestContext.CurrentContext.Test.FullName;
        readonly long started = Stopwatch.GetTimestamp();
        bool disposed;

        public static WordboundNativeTickBatches StartIfEnabled() =>
            Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_NATIVE") == "1" ? new WordboundNativeTickBatches() : null;

        public void Attach(NetworkRunner runner, int peerIndex, int creation)
        {
            var observation = new Observation(peerIndex, creation, started);
            observations.Add(observation);
            var adapter = runner.gameObject.AddComponent<WordboundNativeTickBatchAdapter>();
            NUnit.Framework.Assert.IsNotNull(adapter, "The native tick probe needs its editor-guarded runtime-assembly adapter.");
            adapter.BeforeTicks = observation.Begin;
            adapter.AfterTicks = observation.End;
            adapter.Updated = ticks => observation.UnityUpdate(runner, ticks);
            adapter.Destroyed = observation.Destroyed;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            long ended = Stopwatch.GetTimestamp();
            foreach (var observation in observations) observation.Stop();
            Debug.Log("WORDBOUND_NATIVE_TICK_WINDOW: case=" + testName + "; elapsedMs=" + Milliseconds(ended - started) +
                "; runners=" + observations.Count + "; clock=Stopwatch; thread=main; duration=wall-not-cpu" +
                "; callbackGaps=not-network-arrival-or-service-times; domainReloadRetention=false");
            foreach (var observation in observations)
            {
                try { observation.Report(); }
                catch (Exception error) { Debug.Log("WORDBOUND_NATIVE_TICK_RUNNER: status=unavailable; error=" + error.GetType().Name); }
            }
        }

        static string Milliseconds(long ticks) => (ticks * 1000d / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture);

        internal sealed class Observation
        {
            const int TailCapacity = 16;
            readonly int peerIndex, creation;
            readonly long started;
            readonly Totals forward = new Totals(), resimulated = new Totals();
            readonly Sample[] tail = new Sample[TailCapacity];
            readonly FrameDeltas frameDeltas = new FrameDeltas();
            long previousBegin, previousUnityUpdate, maxBeginGap, maxUnityUpdateGap, stoppedAt;
            long completed, unityUpdates;
            int tailNext, unmatchedBegins, unmatchedEnds, pairingMismatches, invalidTickCounts, maxTicksExecuted;
            bool active, stopped, destroyed, hostObserved, clientObserved;
            Sample current;

            internal Observation(int peerIndex, int creation, long started)
            { this.peerIndex = peerIndex; this.creation = creation; this.started = started; }

            internal void Begin(bool resimulation, int tickCount, bool host)
            {
                if (stopped) return;
                long now = Stopwatch.GetTimestamp();
                if (active) unmatchedBegins++;
                if (previousBegin != 0) maxBeginGap = Math.Max(maxBeginGap, now - previousBegin);
                previousBegin = now;
                hostObserved |= host;
                clientObserved |= !host;
                if (tickCount <= 0) invalidTickCounts++;
                current = new Sample { Begin = now, Frame = Time.frameCount, Resimulation = resimulation, Ticks = tickCount, Host = host };
                active = true;
            }

            internal void End(bool resimulation, int tickCount)
            {
                if (stopped) return;
                long now = Stopwatch.GetTimestamp();
                if (!active) { unmatchedEnds++; return; }
                active = false;
                if (current.Resimulation != resimulation || current.Ticks != tickCount)
                { pairingMismatches++; return; }
                current.End = now;
                current.EndFrame = Time.frameCount;
                if (tickCount <= 0) return;
                var total = resimulation ? resimulated : forward;
                total.Batches++;
                total.Ticks += tickCount;
                total.MaxTicks = Math.Max(total.MaxTicks, tickCount);
                total.WallTicks += now - current.Begin;
                if (now - current.Begin > total.MaxWallTicks)
                { total.MaxWallTicks = now - current.Begin; total.Worst = current; }
                tail[tailNext] = current;
                tailNext = (tailNext + 1) % TailCapacity;
                completed++;
            }

            internal void UnityUpdate(NetworkRunner runner, int ticksExecuted)
            {
                if (stopped) return;
                long now = Stopwatch.GetTimestamp();
                if (previousUnityUpdate != 0) maxUnityUpdateGap = Math.Max(maxUnityUpdateGap, now - previousUnityUpdate);
                previousUnityUpdate = now;
                unityUpdates++;
                maxTicksExecuted = Math.Max(maxTicksExecuted, ticksExecuted);
                try
                {
                    frameDeltas.Record(Time.unscaledDeltaTime, runner.Config.Simulation.SimulationUpdateTimeMode,
                        Time.frameCount, runner.Tick.Raw, runner.InputTick.Raw, runner.LatestServerTick.Raw, ticksExecuted);
                }
                catch (Exception error) { frameDeltas.Unavailable(error); }
            }

            internal void Destroyed() { destroyed = true; Stop(); }
            internal void Stop()
            {
                if (stopped) return;
                stoppedAt = Stopwatch.GetTimestamp();
                stopped = true;
                frameDeltas.Stop();
            }

            internal void Report()
            {
                string identity = "peer=" + peerIndex + "; creation=" + creation;
                bool partial = active || unmatchedBegins != 0 || unmatchedEnds != 0 || pairingMismatches != 0 || invalidTickCounts != 0;
                Debug.Log("WORDBOUND_NATIVE_TICK_RUNNER: " + identity + "; status=" + (completed == 0 ? "unavailable" : partial ? "observed-partial" : "observed") +
                    "; hostObserved=" + hostObserved + "; clientObserved=" + clientObserved +
                    "; completedBatches=" + completed + "; pendingBatch=" + active + "; destroyed=" + destroyed +
                    "; pendingBatchAgeAtStopMs=" + (active ? Milliseconds(stoppedAt - current.Begin) : "unavailable") +
                    "; unmatchedBegins=" + unmatchedBegins + "; unmatchedEnds=" + unmatchedEnds +
                    "; pairingMismatches=" + pairingMismatches + "; invalidTickCounts=" + invalidTickCounts +
                    "; maxBatchStartGapMs=" + Milliseconds(maxBeginGap) + "; unityUpdates=" + unityUpdates +
                    "; lastBatchAgeAtStopMs=" + (previousBegin != 0 ? Milliseconds(stoppedAt - previousBegin) : "unavailable") +
                    "; lastUnityUpdateAgeAtStopMs=" + (previousUnityUpdate != 0 ? Milliseconds(stoppedAt - previousUnityUpdate) : "unavailable") +
                    "; maxUnityUpdateGapMs=" + Milliseconds(maxUnityUpdateGap) + "; maxTicksExecutedAtUnityUpdate=" + maxTicksExecuted);
                ReportTotals(identity, "forward", forward);
                ReportTotals(identity, "resimulation", resimulated);
                int retained = (int)Math.Min(completed, TailCapacity);
                var text = new StringBuilder("WORDBOUND_NATIVE_TICK_TAIL: ").Append(identity)
                    .Append("; capacity=").Append(TailCapacity).Append("; retained=").Append(retained)
                    .Append("; overwritten=").Append(Math.Max(0, completed - retained))
                    .Append("; totalsIncludeOverwritten=true; samples=[");
                int first = completed >= TailCapacity ? tailNext : 0;
                for (int i = 0; i < retained; i++)
                {
                    var sample = tail[(first + i) % TailCapacity];
                    if (i > 0) text.Append('|');
                    text.Append(sample.Host ? "host" : "client").Append(',').Append(sample.Resimulation ? "resim" : "forward")
                        .Append(",ticks=").Append(sample.Ticks).Append(",frames=").Append(sample.Frame).Append('/').Append(sample.EndFrame)
                        .Append(",startMs=").Append(Milliseconds(sample.Begin - started))
                        .Append(",endMs=").Append(Milliseconds(sample.End - started))
                        .Append(",wallMs=").Append(Milliseconds(sample.End - sample.Begin));
                }
                Debug.Log(text.Append(']').ToString());
                Debug.Log("WORDBOUND_NATIVE_TICK_FRAME_DELTAS: " + identity + "; samples=" + frameDeltas.Samples +
                    "; unavailable=" + frameDeltas.UnavailableReads + "; lastUnavailableType=" + (frameDeltas.LastUnavailableType ?? "none") +
                    "; invalidDeltas=" + frameDeltas.InvalidDeltas + "; aboveOneSecond=" + frameDeltas.AboveOneSecond +
                    "; maxUnscaledDeltaMs=" + (frameDeltas.Samples > frameDeltas.InvalidDeltas ?
                        (frameDeltas.MaxUnscaledDelta * 1000d).ToString("F3", CultureInfo.InvariantCulture) : "unavailable") +
                    "; latestUpdateTimeMode=" + (frameDeltas.Samples > 0 ? frameDeltas.LatestMode.ToString() : "unavailable") +
                    "; phase=MonoBehaviour.Update-frame-observation-not-time-provider-trace; ticksExecuted=at-Unity-Update-not-assumed-same-phase" +
                    "; threshold=strict-unscaled-delta-greater-than-one-second; unscaledDeltaMatchesSdkArgumentOnlyInMode=UnscaledDeltaTime");
                var frameText = new StringBuilder("WORDBOUND_NATIVE_TICK_LONG_FRAMES: ").Append(identity)
                    .Append("; capacity=").Append(FrameDeltas.Capacity).Append("; retained=").Append(frameDeltas.Retained)
                    .Append("; overwritten=").Append(frameDeltas.AboveOneSecond - frameDeltas.Retained)
                    .Append("; totalsIncludeOverwritten=true; samples=[");
                for (int i = 0; i < frameDeltas.Retained; i++)
                {
                    var sample = frameDeltas.At(i);
                    if (i > 0) frameText.Append('|');
                    frameText.Append("frame=").Append(sample.Frame).Append(",mode=").Append(sample.Mode)
                        .Append(",unscaledDeltaMs=").Append((sample.UnscaledDelta * 1000d).ToString("F3", CultureInfo.InvariantCulture))
                        .Append(",sdkTick=").Append(sample.SdkTick).Append(",inputTick=").Append(sample.InputTick)
                        .Append(",latestServerTick=").Append(sample.LatestServerTick)
                        .Append(",ticksExecutedAtUnityUpdate=").Append(sample.TicksExecuted);
                }
                Debug.Log(frameText.Append(']').ToString());
            }

            void ReportTotals(string identity, string kind, Totals total) =>
                Debug.Log("WORDBOUND_NATIVE_TICK_BATCHES: " + identity + "; kind=" + kind +
                    "; status=" + (total.Batches > 0 ? "observed" : "unavailable") + "; batches=" + total.Batches +
                    "; ticks=" + total.Ticks + "; maxBatchTicks=" + total.MaxTicks +
                    "; totalBatchWallMs=" + Milliseconds(total.WallTicks) + "; maxBatchWallMs=" + Milliseconds(total.MaxWallTicks) +
                    "; maxWallBatchTicks=" + total.Worst.Ticks + "; maxWallBatchFrame=" + total.Worst.Frame +
                    "; maxWallBatchStartMs=" + (total.Batches > 0 ? Milliseconds(total.Worst.Begin - started) : "unavailable") +
                    "; maxWallBatchEndMs=" + (total.Batches > 0 ? Milliseconds(total.Worst.End - started) : "unavailable"));

            sealed class Totals
            {
                internal long Batches, Ticks, WallTicks, MaxWallTicks;
                internal int MaxTicks;
                internal Sample Worst;
            }
            struct Sample
            {
                internal long Begin, End;
                internal int Frame, EndFrame, Ticks;
                internal bool Resimulation, Host;
            }
        }

        internal sealed class FrameDeltas
        {
            internal const int Capacity = 16;
            readonly FrameDelta[] tail = new FrameDelta[Capacity];
            int next;
            bool stopped;
            internal long Samples { get; private set; }
            internal long UnavailableReads { get; private set; }
            internal long InvalidDeltas { get; private set; }
            internal long AboveOneSecond { get; private set; }
            internal double MaxUnscaledDelta { get; private set; }
            internal SimulationConfig.SimulationTimeMode LatestMode { get; private set; }
            internal string LastUnavailableType { get; private set; }
            internal int Retained => (int)Math.Min(AboveOneSecond, Capacity);

            internal void Record(float delta, SimulationConfig.SimulationTimeMode mode, int frame,
                int sdkTick, int inputTick, int latestServerTick, int ticksExecuted)
            {
                if (stopped) return;
                Samples++; LatestMode = mode;
                if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) { InvalidDeltas++; return; }
                MaxUnscaledDelta = Math.Max(MaxUnscaledDelta, delta);
                if (delta <= 1) return;
                AboveOneSecond++;
                tail[next] = new FrameDelta(delta, mode, frame, sdkTick, inputTick, latestServerTick, ticksExecuted);
                next = (next + 1) % Capacity;
            }

            internal void Unavailable(Exception error)
            {
                if (stopped) return;
                UnavailableReads++; LastUnavailableType = error.GetType().Name;
            }

            internal FrameDelta At(int index)
            {
                if (index < 0 || index >= Retained) throw new ArgumentOutOfRangeException(nameof(index));
                int first = AboveOneSecond >= Capacity ? next : 0;
                return tail[(first + index) % Capacity];
            }

            internal void Stop() => stopped = true;
        }

        internal readonly struct FrameDelta
        {
            internal readonly float UnscaledDelta;
            internal readonly SimulationConfig.SimulationTimeMode Mode;
            internal readonly int Frame, SdkTick, InputTick, LatestServerTick, TicksExecuted;

            internal FrameDelta(float delta, SimulationConfig.SimulationTimeMode mode, int frame,
                int sdkTick, int inputTick, int latestServerTick, int ticksExecuted)
            {
                UnscaledDelta = delta; Mode = mode; Frame = frame; SdkTick = sdkTick;
                InputTick = inputTick; LatestServerTick = latestServerTick; TicksExecuted = ticksExecuted;
            }
        }
    }
}
