using System;
using System.Globalization;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.OnlineGameplayTests
{
    internal sealed class WordboundNativePhases
    {
        WordboundPhaseCapture capture;
        WordboundPhaseBoundary first;
        string window = "startup";

        public static WordboundNativePhases StartIfEnabled() =>
            Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_PHASES") == "1" ? new WordboundNativePhases() : null;

        WordboundNativePhases()
        {
            first = new WordboundPhaseBoundary(null);
            capture = WordboundPhaseCapture.TryStart();
            if (capture == null) Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: capture-already-owned");
        }

        public void Ready(MatchSnapshot frame)
        {
            if (capture == null) return;
            var boundary = new WordboundPhaseBoundary(frame);
            Stop(boundary, "all-peers-ready");
            first = boundary; window = "ready";
            capture = WordboundPhaseCapture.TryStart();
            if (capture == null) Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: ready-capture-already-owned");
        }

        public void Finish(MatchSnapshot frame, string outcome) => Stop(new WordboundPhaseBoundary(frame), outcome);

        void Stop(WordboundPhaseBoundary last, string outcome)
        {
            var completed = capture; capture = null;
            if (completed == null) return;
            completed.Dispose();
            try
            {
                Debug.Log("WORDBOUND_PHASE_CASE: " + TestContext.CurrentContext.Test.FullName);
                foreach (string line in completed.Report(window, outcome, first, last)) Debug.Log(line);
            }
            catch (Exception error) { Debug.Log("WORDBOUND_PHASE_UNAVAILABLE: report-" + error.GetType().Name); }
        }
    }

    internal sealed class WordboundNativeProfiler : IDisposable
    {
        const int Capacity = 32768;
        static readonly string[] Names =
        {
            "WB.Match.Reconcile", "WB.Match.Capture", "WB.Match.Advance", "WB.Match.Validate",
            "WB.Match.Presentation", "WB.Match.PhysicsApply", "WB.Match.BuildWorld", "WB.Match.BeforePhysics"
        };
        readonly ProfilerRecorder[] recorders = new ProfilerRecorder[Names.Length];
        readonly string[] errors = new string[Names.Length];
        readonly string testName = TestContext.CurrentContext.Test.FullName;
        readonly int firstFrame = Time.frameCount;
        readonly double started = Time.realtimeSinceStartupAsDouble;
        bool disposed;

        public static WordboundNativeProfiler StartIfEnabled()
        {
            return Environment.GetEnvironmentVariable("WORDBOUND_PROFILE_NATIVE") == "1"
                ? new WordboundNativeProfiler() : null;
        }

        WordboundNativeProfiler()
        {
            const ProfilerRecorderOptions options = ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
            for (int i = 0; i < Names.Length; i++)
            {
                try
                {
                    var marker = new ProfilerMarker(ProfilerCategory.Scripts, Names[i]);
                    recorders[i] = ProfilerRecorder.StartNew(marker, Capacity, options);
                }
                catch (Exception exception) { errors[i] = "start:" + exception.GetType().Name; }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            int lastFrame = Time.frameCount;
            double ended = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < recorders.Length; i++)
            {
                try { if (recorders[i].Valid) recorders[i].Stop(); }
                catch (Exception exception) { errors[i] = "stop:" + exception.GetType().Name; }
            }
            try
            {
                Debug.Log("WORDBOUND_NATIVE_PROFILE_WINDOW: case=" + testName +
                    "; startFrame=" + firstFrame + "; endFrame=" + lastFrame +
                    "; elapsedSeconds=" + Format(ended - started) +
                    "; thread=main; instances=all-in-process; sampling=per-marker-invocation" +
                    "; scopes=inclusive-not-additive; max=sample-not-frame" +
                    "; inFlightScopes=not-yet-recorded; domainReloadRetention=false");
                for (int i = 0; i < recorders.Length; i++)
                {
                    try { Report(i); }
                    catch (Exception exception)
                    {
                        Debug.Log("WORDBOUND_NATIVE_PROFILE: marker=" + Names[i] +
                            "; status=unavailable; error=read:" + exception.GetType().Name);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < recorders.Length; i++)
                {
                    try { recorders[i].Dispose(); }
                    catch (Exception exception)
                    {
                        Debug.Log("WORDBOUND_NATIVE_PROFILE: marker=" + Names[i] +
                            "; status=unavailable; error=dispose:" + exception.GetType().Name);
                    }
                }
            }
        }

        void Report(int index)
        {
            var recorder = recorders[index];
            if (!recorder.Valid)
            {
                Debug.Log("WORDBOUND_NATIVE_PROFILE: marker=" + Names[index] +
                    "; valid=false; status=unavailable; error=" + (errors[index] ?? "invalid-recorder"));
                return;
            }
            var samples = recorder.ToArray();
            bool timed = recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
            bool wrapped = recorder.WrappedAround;
            long invocations = 0;
            double total = 0, maximum = 0;
            int invalid = 0;
            foreach (var sample in samples)
            {
                if (sample.Value < 0 || sample.Count < 0) { invalid++; continue; }
                invocations += sample.Count;
                total += sample.Value;
                maximum = Math.Max(maximum, sample.Value);
            }
            bool observed = timed && invocations > 0 && invalid == 0 && errors[index] == null;
            Debug.Log("WORDBOUND_NATIVE_PROFILE: marker=" + Names[index] +
                "; valid=true; unit=" + recorder.UnitType +
                "; status=" + (observed ? wrapped ? "retained-tail" : "observed" : "unavailable") +
                "; samples=" + samples.Length + "; invocations=" + invocations +
                "; capturedTotalCpuMs=" + (observed ? Format(total / 1000000d) : "unavailable") +
                "; capturedMaxSampleCpuMs=" + (observed ? Format(maximum / 1000000d) : "unavailable") +
                "; capacity=" + recorder.Capacity + "; wrapped=" + wrapped +
                "; droppedSamples=" + (wrapped ? "unknown-at-least-one" : "no-buffer-wrap-observed") +
                "; invalidSamples=" + invalid + "; error=" + (errors[index] ?? "none"));
        }

        static string Format(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    }
}

