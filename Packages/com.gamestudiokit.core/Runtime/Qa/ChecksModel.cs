// Game Studio Kit (MIT). The checks.json v1 document (checks.schema.json, CONTRACTS §7.3) as plain data. Engine-free:
// QaRunner fills it in Unity; the node tests fill it under plain .NET and validate the output against the schema.
using System;
using System.Collections.Generic;
using GameStudioKit.Common;

namespace GameStudioKit.Qa
{
    public static class QaStatus
    {
        public const string Pass = "PASS";
        public const string Fail = "FAIL";
        public const string Skip = "SKIP";
        public const string Error = "ERROR";
    }

    public sealed class QaStepResult : IKitJson
    {
        public string Name;
        public string Status = QaStatus.Pass;

        /// <summary>Seconds since the run started when the step ended.</summary>
        public double TSeconds;

        /// <summary>World-space facts the step measured (name to value).</summary>
        public readonly Dictionary<string, object> Probes = new Dictionary<string, object>(StringComparer.Ordinal);
        public readonly List<string> AssertFailures = new List<string>();

        /// <summary>Workspace-relative POSIX paths of the step's captures.</summary>
        public readonly List<string> Captures = new List<string>();

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("name", string.IsNullOrEmpty(Name) ? "unnamed" : Name);
            w.Prop("status", Status);
            w.Prop("t_s", Math.Max(0, TSeconds));
            w.Name("probes").Any(Probes);
            w.Name("assert_failures").Any(AssertFailures);
            if (Captures.Count > 0) w.Name("captures").Any(Captures);
            w.EndObject();
        }
    }

    public sealed class QaCapture : IKitJson
    {
        public string Name;
        public string Path;
        public string Kind = "png";
        public string Camera;
        public string Utc;

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("name", Name ?? "capture");
            w.Prop("path", Path ?? "");
            w.Prop("kind", Kind ?? "png");
            if (!string.IsNullOrEmpty(Camera)) w.Prop("camera", Camera);
            if (!string.IsNullOrEmpty(Utc)) w.Prop("utc", Utc);
            w.EndObject();
        }
    }

    public sealed class QaConsoleSummary : IKitJson
    {
        public int Errors;
        public int Exceptions;
        public int Warnings;
        public string FirstException;

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("errors", Math.Max(0, Errors));
            w.Prop("exceptions", Math.Max(0, Exceptions));
            w.Prop("warnings", Math.Max(0, Warnings));
            w.Prop("first_exception", FirstException);
            w.EndObject();
        }
    }

    /// <summary>The "perf" block inside checks.json (all fields optional).</summary>
    public sealed class QaPerfSummary : IKitJson
    {
        public double? FrameMsP50;
        public double? FrameMsP95;
        public double? WorstFrameMs;
        public double? FirstVisitHitchMs;
        public double? GcKbFrameP95;
        public double? SysMemMbMax;

        /// <summary>System memory growth after warm-up, scaled to 30 minutes (soak_memory_growth_pct_per_30min); may be
        /// negative. Additive field.</summary>
        public double? SysMemGrowthPctPer30Min;

        /// <summary>Seconds between the first and last post-warm-up memory sample. Additive field.</summary>
        public double? MeasuredSeconds;

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            Opt(w, "frame_ms_p50", FrameMsP50);
            Opt(w, "frame_ms_p95", FrameMsP95);
            Opt(w, "worst_frame_ms", WorstFrameMs);
            Opt(w, "first_visit_hitch_ms", FirstVisitHitchMs);
            Opt(w, "gc_kb_frame_p95", GcKbFrameP95);
            Opt(w, "sys_mem_mb_max", SysMemMbMax);
            OptSigned(w, "sys_mem_growth_pct_per_30min", SysMemGrowthPctPer30Min);
            Opt(w, "measured_s", MeasuredSeconds);
            w.EndObject();
        }

        internal static void Opt(KitJsonWriter w, string name, double? v)
        {
            if (v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value)) w.Prop(name, Math.Max(0, v.Value));
        }

        internal static void OptSigned(KitJsonWriter w, string name, double? v)
        {
            if (v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value)) w.Prop(name, v.Value);
        }
    }

    public sealed class ChecksDocument : IKitJson
    {
        public string RunId;
        public string Commit;
        public string Tree;
        public string BuildId;
        public string Scenario;
        public int Seed;

        /// <summary>"editor" or "player".</summary>
        public string Platform = "player";
        public bool? DevelopmentBuild;
        public string UnityVersion;
        public string StartedUtc;
        public string EndedUtc;

        /// <summary>PASS, FAIL or ERROR; see <see cref="ComputeResult"/>.</summary>
        public string Result = QaStatus.Error;

        public readonly List<QaStepResult> Steps = new List<QaStepResult>();

        /// <summary>Checkpoint name to 16 lower-case hex digits (Hash64 of game state).</summary>
        public readonly Dictionary<string, string> StateHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        public QaPerfSummary Perf;
        public QaConsoleSummary Console = new QaConsoleSummary();
        public readonly List<ProbeResult> ProbeResults = new List<ProbeResult>();
        public readonly List<QaCapture> Captures = new List<QaCapture>();

        /// <summary>
        /// ERROR when the runner itself broke; FAIL when any step failed or errored, no step passed (a run that ran
        /// nothing, or skipped everything, proves nothing), the console logged an error or an exception, or any probe
        /// result is FAIL (ROUTE never fails); PASS otherwise.
        /// </summary>
        public string ComputeResult(bool runnerError)
        {
            if (runnerError) return QaStatus.Error;
            bool anyPass = false;
            foreach (var s in Steps)
            {
                if (s.Status == QaStatus.Fail || s.Status == QaStatus.Error) return QaStatus.Fail;
                if (s.Status == QaStatus.Pass) anyPass = true;
            }
            if (!anyPass) return QaStatus.Fail;
            if (Console != null && (Console.Errors > 0 || Console.Exceptions > 0)) return QaStatus.Fail;
            foreach (var p in ProbeResults)
                if (p.Verdict == ProbeVerdict.FAIL) return QaStatus.Fail;
            return QaStatus.Pass;
        }

        /// <summary>True when there are steps and every one of them was skipped.</summary>
        public bool AllStepsSkipped()
        {
            if (Steps.Count == 0) return false;
            foreach (var s in Steps) if (s.Status != QaStatus.Skip) return false;
            return true;
        }

        /// <summary>Process exit code for the result: 0 PASS, 1 FAIL, 2 ERROR (CONTRACTS §3.6).</summary>
        public static int ExitCodeFor(string result) =>
            result == QaStatus.Pass ? 0 : result == QaStatus.Fail ? 1 : 2;

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("schema", 1);
            w.Prop("run_id", RunId);
            w.Prop("commit", Commit);
            w.Prop("tree", Tree);
            w.Prop("build_id", BuildId);
            w.Prop("scenario", Scenario);
            w.Prop("seed", Seed);
            w.Prop("platform", Platform);
            if (DevelopmentBuild.HasValue) w.Prop("development_build", DevelopmentBuild.Value);
            if (!string.IsNullOrEmpty(UnityVersion)) w.Prop("unity_version", UnityVersion);
            w.Prop("started_utc", StartedUtc);
            w.Prop("ended_utc", EndedUtc);
            w.Prop("result", Result);
            w.Name("steps").BeginArray();
            foreach (var s in Steps) s.WriteJson(w);
            w.EndArray();
            if (StateHashes.Count > 0) w.Name("state_hashes").Any(StateHashes);
            if (Perf != null) w.Name("perf").Any(Perf);
            w.Name("console").Any(Console ?? new QaConsoleSummary());
            if (ProbeResults.Count > 0)
            {
                w.Name("probe_results").BeginArray();
                foreach (var p in ProbeResults) p.WriteJson(w);
                w.EndArray();
            }
            if (Captures.Count > 0)
            {
                w.Name("captures").BeginArray();
                foreach (var c in Captures) c.WriteJson(w);
                w.EndArray();
            }
            w.EndObject();
        }

        public string ToJson() => KitJson.Serialize(this, true) + "\n";

        /// <summary>Atomic write (tmp + rename) of checks.json.</summary>
        public void WriteTo(string path) => KitIo.WriteAllTextAtomic(path, ToJson());
    }
}
