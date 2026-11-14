// Game Studio Kit (MIT). What a QA step sees: the seed, the run folder, the probe thresholds, and helpers to record
// facts, probe results, state hashes and captures. Editor and GSK_QA builds only.
#if GSK_QA || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Capture;
using GameStudioKit.Common;
using UnityEngine;

namespace GameStudioKit.Qa
{
    /// <summary>Thrown by scenario code to fail the current step with a message (other exceptions mark it ERROR).</summary>
    public sealed class QaFailException : Exception
    {
        public QaFailException(string message) : base(message) { }
    }

    public sealed class QaContext
    {
        readonly float _t0 = Time.realtimeSinceStartup;
        StoryCameraSet _cameras;
        bool _camerasLoaded;

        public string Scenario { get; internal set; }
        public string RunId { get; internal set; }
        public int Seed { get; internal set; }

        /// <summary>Absolute run folder (normally W/Docs/qa/runs/&lt;run_id&gt;).</summary>
        public string OutDir { get; internal set; }

        /// <summary>Absolute workspace root W, or null when it cannot be found.</summary>
        public string WorkspaceRoot { get; internal set; }

        public ProbeConfig Config { get; internal set; }

        /// <summary>Path of cameras.json (-qa-cameras in players; Docs/progress/cameras.json in the editor).</summary>
        public string CamerasPath { get; internal set; }

        internal ChecksDocument Doc;
        internal QaStepResult CurrentStep;

        /// <summary>Real seconds since the run started.</summary>
        public float ElapsedSeconds => Time.realtimeSinceStartup - _t0;

        public string CurrentStepName => CurrentStep?.Name;

        /// <summary>Records a world-space fact on the current step ("probes" in checks.json).</summary>
        public void Note(string key, object value)
        {
            if (CurrentStep == null || string.IsNullOrEmpty(key)) return;
            CurrentStep.Probes[key] = value;
        }

        /// <summary>Adds a failure to the current step without stopping it.</summary>
        public void Fail(string message)
        {
            CurrentStep?.AssertFailures.Add(message ?? "failed");
        }

        public void AddProbeResult(ProbeResult r)
        {
            if (r != null) Doc?.ProbeResults.Add(r);
        }

        public void AddProbeResults(IEnumerable<ProbeResult> results)
        {
            if (results == null) return;
            foreach (var r in results) AddProbeResult(r);
        }

        /// <summary>Stores a checkpoint hash (Hash64 of game state) as 16 hex digits. Host and client runs compare them.</summary>
        public void RecordStateHash(string checkpoint, ulong hash)
        {
            if (!KitIo.IsSafeName(checkpoint)) throw new ArgumentException("checkpoint names use only A-Z a-z 0-9 _ . -", nameof(checkpoint));
            if (Doc != null) Doc.StateHashes[checkpoint] = KitIo.Hex16(hash);
        }

        /// <summary>Absolute path for a file in the run folder.</summary>
        public string PathInRun(string fileName) => Path.Combine(OutDir, KitIo.SafeFileName(fileName));

        /// <summary>Workspace-relative POSIX path (checks.json convention), or the absolute POSIX path outside W.</summary>
        public string ToWorkspacePath(string absPath)
        {
            string rel = KitIo.RelativePosix(WorkspaceRoot, absPath);
            return rel ?? KitIo.ToPosix(Path.GetFullPath(absPath));
        }

        public void AddCapture(string name, string absPath, string kind = "png", string camera = null)
        {
            string wsPath = ToWorkspacePath(absPath);
            Doc?.Captures.Add(new QaCapture { Name = name, Path = wsPath, Kind = kind, Camera = camera, Utc = KitIo.UtcNowIso() });
            CurrentStep?.Captures.Add(wsPath);
        }

        public StoryCameraSet Cameras
        {
            get
            {
                if (_camerasLoaded) return _cameras;
                _camerasLoaded = true;
                if (!string.IsNullOrEmpty(CamerasPath) && File.Exists(CamerasPath))
                {
                    try { _cameras = StoryCameraSet.Load(CamerasPath); }
                    catch (Exception e) { KitLog.Warn("QA: cannot read cameras.json at " + CamerasPath + ": " + e.Message); }
                }
                return _cameras;
            }
        }

        /// <summary>Captures one story camera from cameras.json into the run folder and records it.</summary>
        public IEnumerator CaptureCamera(string cameraName)
        {
            var set = Cameras;
            var def = set?.Find(cameraName);
            if (def == null)
            {
                Fail("capture: camera '" + cameraName + "' is not in " + (CamerasPath ?? "cameras.json"));
                yield break;
            }
            string file = PathInRun(cameraName + "_" + (CurrentStepName ?? "step") + ".png");
            string error = null;
            yield return StoryCamera.Capture(def, file, set, e => error = e);
            if (error != null) Fail("capture " + cameraName + ": " + error);
            else AddCapture(cameraName, file, "png", cameraName);
        }

        /// <summary>Captures the whole screen at the end of the frame (includes overlay UI; not in batchmode).</summary>
        public IEnumerator CaptureScreen(string name)
        {
            string file = PathInRun(name + ".png");
            string error = null;
            yield return StoryCamera.CaptureScreen(file, e => error = e);
            if (error != null) Fail("screen capture " + name + ": " + error);
            else AddCapture(name, file, "png", "screen");
        }

        /// <summary>Waits until <paramref name="condition"/> holds; fails the step after <paramref name="timeoutSeconds"/>
        /// (real time).</summary>
        public IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) throw new QaFailException("timed out after " + timeoutSeconds + " s waiting for " + what);
                yield return null;
            }
        }

        public IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        /// <summary>Waits game time (affected by timeScale and captureDeltaTime).</summary>
        public IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }
    }
}
#endif
