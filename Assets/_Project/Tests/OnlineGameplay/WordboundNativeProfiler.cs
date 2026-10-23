using System;
using System.Globalization;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace Wreckabulary.OnlineGameplayTests
{
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
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
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
                    "; thread=main; instances=all-in-process; sampling=sum-per-frame" +
                    "; scopes=inclusive-not-additive; max=frame-total-not-single-call" +
                    "; finalPartialFrame=may-be-omitted; domainReloadRetention=false");
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
                "; frameSamples=" + samples.Length + "; invocations=" + invocations +
                "; capturedTotalCpuMs=" + (observed ? Format(total / 1000000d) : "unavailable") +
                "; capturedMaxFrameCpuMs=" + (observed ? Format(maximum / 1000000d) : "unavailable") +
                "; capacity=" + recorder.Capacity + "; wrapped=" + wrapped +
                "; droppedSamples=" + (wrapped ? "unknown-at-least-one" : "no-buffer-wrap-observed") +
                "; invalidSamples=" + invalid + "; error=" + (errors[index] ?? "none"));
        }

        static string Format(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
    }
}

