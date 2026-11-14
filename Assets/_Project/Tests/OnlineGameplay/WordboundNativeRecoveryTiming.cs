using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fusion;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Wreckabulary.OnlineGameplayTests
{
    public sealed partial class WordboundNativeGameplayTests
    {
        sealed class RecoveryTiming : IDisposable
        {
            sealed class Stamp
            {
                public readonly long Wall = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                public readonly long Monotonic = Stopwatch.GetTimestamp();
                public object Value() => new Dictionary<string, object> {
                    ["unixMilliseconds"] = Wall, ["stopwatchTicks"] = Monotonic.ToString(CultureInfo.InvariantCulture) };
            }
            sealed class Observation
            {
                public Peer Peer;
                public NetworkRunner Original;
                public long OldEpoch, OldTick, DetectedEpoch = -1, DetectedTick = -1;
                public long RestoredEpoch = -1, RestoredTick = -1;
                public Stamp Detected, Restored, Ready;
                public Action<string> OnStatus;
                public Action<MatchSnapshot> OnFrame;
                public Action OnReady;
            }
            readonly string stimulusKind;
            readonly Stamp stimulus = new Stamp();
            readonly List<Observation> observations = new List<Observation>();
            Stamp hostStop, readyObserved;

            public RecoveryTiming(IEnumerable<Peer> peers, long expectedEpoch, string stimulusKind)
            {
                this.stimulusKind = stimulusKind;
                foreach (var peer in peers)
                {
                    var value = new Observation { Peer = peer, Original = peer.Runner,
                        OldEpoch = peer.Controller.Frame.Epoch, OldTick = peer.Controller.Frame.Progress.Tick };
                    value.OnStatus = message => {
                        if (message == "" || value.Detected != null) return;
                        value.Detected = new Stamp();
                        var frame = peer.Controller.Frame;
                        if (frame != null) { value.DetectedEpoch = frame.Epoch; value.DetectedTick = frame.Progress.Tick; }
                    };
                    value.OnFrame = frame => {
                        if (value.Detected == null || value.Restored != null || peer.Runner == null ||
                            ReferenceEquals(peer.Runner, value.Original) || frame.Epoch != expectedEpoch) return;
                        value.Restored = new Stamp(); value.RestoredEpoch = frame.Epoch; value.RestoredTick = frame.Progress.Tick;
                    };
                    value.OnReady = () => {
                        if (value.Detected != null && value.Ready == null && peer.Runner != null &&
                            !ReferenceEquals(peer.Runner, value.Original)) value.Ready = new Stamp();
                    };
                    peer.Session.RecoveryStatusChanged += value.OnStatus;
                    peer.Controller.FrameChanged += value.OnFrame;
                    peer.Session.GameplayStarted += value.OnReady;
                    observations.Add(value);
                }
            }
            public void MarkHostStopRequested() => hostStop = new Stamp();
            public void MarkReadyObserved() => readyObserved = new Stamp();
            static object Duration(Stamp start, Stamp end) => start == null || end == null ? null :
                new Dictionary<string, object> { ["stopwatchTicks"] = end.Monotonic - start.Monotonic,
                    ["milliseconds"] = ((end.Monotonic - start.Monotonic) * 1000.0 / Stopwatch.Frequency)
                        .ToString("F3", CultureInfo.InvariantCulture) };
            public void Dispose()
            {
                foreach (var value in observations)
                {
                    value.Peer.Session.RecoveryStatusChanged -= value.OnStatus;
                    value.Peer.Controller.FrameChanged -= value.OnFrame;
                    value.Peer.Session.GameplayStarted -= value.OnReady;
                }
                var result = new Dictionary<string, object> {
                    ["schemaVersion"] = 1, ["stimulusKind"] = stimulusKind,
                    ["stopwatchFrequency"] = Stopwatch.Frequency, ["stimulus"] = stimulus.Value(),
                    ["hostStopRequested"] = hostStop?.Value(), ["allReadyObserved"] = readyObserved?.Value(),
                    ["stimulusToAllReady"] = Duration(stimulus, readyObserved),
                    ["hostStopToAllReady"] = Duration(hostStop, readyObserved),
                    ["survivors"] = observations.Select((value, index) => new Dictionary<string, object> {
                        ["survivorIndex"] = index, ["oldEpoch"] = value.OldEpoch, ["oldFrameTick"] = value.OldTick,
                        ["detected"] = value.Detected?.Value(), ["detectedFrameEpoch"] = value.DetectedEpoch,
                        ["detectedFrameTick"] = value.DetectedTick, ["firstRestored"] = value.Restored?.Value(),
                        ["firstRestoredEpoch"] = value.RestoredEpoch, ["firstRestoredTick"] = value.RestoredTick,
                        ["gameplayStarted"] = value.Ready?.Value(), ["detectedToGameplayStarted"] = Duration(value.Detected, value.Ready),
                        ["stimulusToGameplayStarted"] = Duration(stimulus, value.Ready),
                        ["oldToRestoredTickDifference"] = value.Restored == null ? (object)null : value.RestoredTick - value.OldTick
                    }).ToArray()
                };
                Debug.Log("WORDBOUND_RECOVERY_TIMING: " + OnlineJson.Encode(result));
            }
        }
    }
}
