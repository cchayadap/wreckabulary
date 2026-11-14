// Game Studio Kit (MIT). The in-game QA driver (ARCHITECTURE §3.12, CONTRACTS §3.6).
//  - Player: started by -qa-scenario <name> [-qa-seed N] [-qa-out <abs dir>] [-qa-cameras <abs cameras.json>]
//    [-qa-probes-config <abs>] [-qa-timeout <s>]; writes <qa-out>/checks.json atomically, then quits with 0 PASS,
//    1 FAIL, 2 ERROR. With -qa-perf it also writes perf.json.
//  - Editor / PlayMode tests: yield QaRunner.Run(scenario, options, result => ...).
// Compiled only in the editor and in GSK_QA builds (Release-QA and dev profiles); shipped release players do not
// contain it, so -qa-scenario cannot autoplay a shipped game (OWNER_DEFAULTS C-16, CONTRACTS §3.6). That compile guard
// is the only lock: the interim -qa-token of ARCHITECTURE §4.4 is dropped (CONTRACTS §3.6, §12).
// A run that runs nothing proves nothing: no steps is an ERROR, and all steps skipped is a FAIL. In players a watchdog
// ends a run that outlives -qa-timeout by more than WatchdogGraceSeconds (a step stuck on a yield instruction the step
// deadline cannot poll) and still writes checks.json.
#if GSK_QA || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Audio;
using GameStudioKit.Common;
using UnityEngine;

namespace GameStudioKit.Qa
{
    public sealed class QaRunOptions
    {
        public string Scenario;
        public int Seed = 1;

        /// <summary>Absolute run folder. Defaults to W/Docs/qa/runs/&lt;run_id&gt; (or Temp/qa/&lt;run_id&gt; without a workspace).</summary>
        public string OutDir;
        public string RunId;
        public string CamerasPath;
        public string ProbesConfigPath;
        public string Commit;
        public string Tree;
        public string BuildId;
        public float RunTimeoutSeconds = 900f;
        public int PerfWarmupFrames = PerfProbe.DefaultWarmupFrames;

        /// <summary>Shortest post-warm-up window (s) for which soak_memory_growth_pct_per_30min is evaluated.</summary>
        public double MemoryGrowthMinSeconds = FrameStats.DefaultMemoryGrowthMinSeconds;

        /// <summary>Also write perf.json next to checks.json.</summary>
        public bool WritePerfFile;

        /// <summary>Player mode only: quit the application with the result's exit code when done.</summary>
        public bool QuitWhenDone;

        public static QaRunOptions FromArgs(KitArgs args) => new QaRunOptions
        {
            Scenario = args.Get("-qa-scenario"),
            Seed = args.GetInt("-qa-seed", 1),
            OutDir = args.Get("-qa-out"),
            CamerasPath = args.Get("-qa-cameras"),
            ProbesConfigPath = args.Get("-qa-probes-config"),
            Commit = args.Get("-qa-commit"),
            BuildId = args.Get("-qa-build-id"),
            RunTimeoutSeconds = args.GetFloat("-qa-timeout", 900f),
            PerfWarmupFrames = args.GetInt("-qa-perf-warmup", PerfProbe.DefaultWarmupFrames),
            MemoryGrowthMinSeconds = args.GetFloat("-qa-mem-min-seconds", (float)FrameStats.DefaultMemoryGrowthMinSeconds),
            WritePerfFile = args.Has("-qa-perf"),
        };
    }

    public sealed class QaRunResult
    {
        public ChecksDocument Doc;
        public string ChecksPath;
        public string Result => Doc?.Result ?? QaStatus.Error;
        public int ExitCode => ChecksDocument.ExitCodeFor(Result);
        public bool Passed => Result == QaStatus.Pass;

        /// <summary>One line per failed step, console problem and failed probe, for test assertion messages.</summary>
        public string Summary()
        {
            if (Doc == null) return "no checks document";
            var lines = new List<string> { $"{Doc.Scenario} seed {Doc.Seed}: {Doc.Result} ({ChecksPath})" };
            foreach (var s in Doc.Steps)
                if (s.Status != QaStatus.Pass && s.Status != QaStatus.Skip)
                    lines.Add($"  step {s.Name}: {s.Status} {string.Join("; ", s.AssertFailures)}");
            if (Doc.Console.Errors > 0 || Doc.Console.Exceptions > 0)
                lines.Add($"  console: {Doc.Console.Errors} errors, {Doc.Console.Exceptions} exceptions; first: {Doc.Console.FirstException}");
            foreach (var p in Doc.ProbeResults)
                if (p.Verdict == ProbeVerdict.FAIL) lines.Add("  probe " + p);
            return string.Join("\n", lines);
        }
    }

    [AddComponentMenu("")]
    public sealed class QaRunner : MonoBehaviour
    {
        /// <summary>Seconds past the -qa-timeout deadline after which the player watchdog ends a stuck run.</summary>
        public const float WatchdogGraceSeconds = 10f;

        /// <summary>True while any QA run is in progress.</summary>
        public static bool IsRunning { get; private set; }

        static RunState s_Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsRunning = false;
            s_Active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return;
            var args = KitArgs.Current;
            if (!args.Has("-qa-scenario")) return;
            var go = new GameObject("GSK QaRunner");
            DontDestroyOnLoad(go);
            var runner = go.AddComponent<QaRunner>();
            var options = QaRunOptions.FromArgs(args);
            options.QuitWhenDone = true;
            runner.StartCoroutine(Run(null, options, null));
        }

        /// <summary>Everything a run needs to finish, from its coroutine or from the watchdog.</summary>
        sealed class RunState
        {
            public QaRunOptions Options;
            public QaRunResult Result;
            public ChecksDocument Doc;
            public ConsoleCounter Console;
            public QaContext Ctx;
            public PerfProbe Perf;
            public bool RunnerError;
            public float RunDeadline = float.PositiveInfinity;
            public Action<QaRunResult> Done;
            public bool Finished;
        }

        /// <summary>
        /// Runs a scenario (or options.Scenario when <paramref name="scenario"/> is null) and writes checks.json.
        /// Never throws: runner faults produce an ERROR document.
        /// </summary>
        public static IEnumerator Run(IQaScenario scenario, QaRunOptions options, Action<QaRunResult> done)
        {
            options = options ?? new QaRunOptions();
            var doc = new ChecksDocument();
            var st = new RunState { Options = options, Doc = doc, Result = new QaRunResult { Doc = doc }, Console = new ConsoleCounter(), Done = done };
            s_Active = st;
            IsRunning = true;

            string name = scenario?.Name ?? options.Scenario;
            doc.Scenario = KitIo.IsSafeName(name) ? name : KitIo.SafeFileName(name ?? "unknown");
            doc.Seed = options.Seed;
            doc.Platform = Application.isEditor ? "editor" : "player";
            doc.DevelopmentBuild = Application.isEditor ? (bool?)null : Debug.isDebugBuild;
            doc.UnityVersion = Application.unityVersion;
            doc.Commit = options.Commit;
            doc.Tree = options.Tree;
            doc.BuildId = options.BuildId ?? PerfProbe.BuildId();
            doc.StartedUtc = KitIo.UtcNowIso();

            try
            {
                ResolvePaths(options, doc);
                st.Ctx = new QaContext
                {
                    Scenario = doc.Scenario,
                    RunId = doc.RunId,
                    Seed = options.Seed,
                    OutDir = options.OutDir,
                    WorkspaceRoot = FindWorkspace(options.OutDir),
                    CamerasPath = options.CamerasPath,
                    Doc = doc,
                };
                var ctx = st.Ctx;
                if (string.IsNullOrEmpty(ctx.CamerasPath) && ctx.WorkspaceRoot != null)
                    ctx.CamerasPath = Path.Combine(ctx.WorkspaceRoot, "Docs", "progress", "cameras.json");
                string cfgPath = options.ProbesConfigPath;
                if (string.IsNullOrEmpty(cfgPath) && ctx.WorkspaceRoot != null)
                    cfgPath = Path.Combine(ctx.WorkspaceRoot, "Docs", "qa", "probes.config.json");
                ctx.Config = ProbeConfig.LoadOrDefaults(cfgPath);
                foreach (var w in ctx.Config.Warnings) KitLog.Warn("probes.config: " + w);
                Directory.CreateDirectory(options.OutDir);

                if (scenario == null) scenario = QaScenarios.Create(options.Scenario);
                st.Console.Start();
                // Every run starts with an empty audio ledger: fired counts and expected ids never leak between runs of
                // one Play Mode session. Scenarios call AudioEventProbe.Expect from BuildSteps or a step.
                AudioEventProbe.Reset();
                AudioEventProbe.Enabled = true;
                st.Perf = PerfProbe.GetOrCreate();
                st.Perf.OwnedByRunner = true;
                st.Perf.MemoryGrowthMinSeconds = options.MemoryGrowthMinSeconds;
                if (!st.Perf.IsRunning) st.Perf.Begin(doc.Scenario, options.PerfWarmupFrames);
            }
            catch (Exception e)
            {
                st.RunnerError = true;
                AddStep(doc, "setup", QaStatus.Error, "runner setup failed: " + e.Message);
            }

            if (!st.RunnerError && scenario == null)
            {
                st.RunnerError = true;
                AddStep(doc, "resolve_scenario", QaStatus.Error,
                    "unknown scenario '" + options.Scenario + "'; known: " + string.Join(", ", QaScenarios.KnownNames()));
            }

            if (!st.RunnerError)
            {
                List<QaStep> steps = null;
                try
                {
                    var built = scenario.BuildSteps(st.Ctx);
                    steps = built == null ? new List<QaStep>() : new List<QaStep>(built);
                }
                catch (Exception e)
                {
                    st.RunnerError = true;
                    AddStep(doc, "build_steps", QaStatus.Error, "BuildSteps threw " + e.GetType().Name + ": " + e.Message);
                }

                if (steps != null && steps.Count == 0)
                {
                    st.RunnerError = true;
                    AddStep(doc, "no_steps", QaStatus.Error, "scenario '" + doc.Scenario + "' built no steps");
                    steps = null;
                }

                if (steps != null)
                {
                    st.RunDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, options.RunTimeoutSeconds);
                    var ctx = st.Ctx;
                    foreach (var step in steps)
                    {
                        var res = new QaStepResult { Name = step?.Name ?? "unnamed" };
                        doc.Steps.Add(res);
                        if (step == null)
                        {
                            res.Status = QaStatus.Error;
                            res.AssertFailures.Add("null step");
                            continue;
                        }
                        if (Time.realtimeSinceStartup > st.RunDeadline)
                        {
                            res.Status = QaStatus.Fail;
                            res.AssertFailures.Add("not run: the run exceeded -qa-timeout " + options.RunTimeoutSeconds + " s");
                            res.TSeconds = ctx.ElapsedSeconds;
                            continue;
                        }
                        ctx.CurrentStep = res;
                        yield return RunStep(step, ctx, res, Mathf.Min(Time.realtimeSinceStartup + Mathf.Max(0.1f, step.TimeoutSeconds), st.RunDeadline));
                        res.TSeconds = ctx.ElapsedSeconds;
                        ctx.CurrentStep = null;
                    }
                    if (doc.AllStepsSkipped())
                        AddStep(doc, "all_skipped", QaStatus.Fail, "every step of '" + doc.Scenario + "' was skipped: the run proved nothing");
                }
            }

            Finish(st);

            if (options.QuitWhenDone && !Application.isEditor)
            {
                yield return null;
                Application.Quit(st.Result.ExitCode);
            }
        }

        /// <summary>Console, perf, audio, result and checks.json. Runs once, from the run or from the watchdog.</summary>
        static void Finish(RunState st)
        {
            if (st.Finished) return;
            st.Finished = true;
            var doc = st.Doc;
            var options = st.Options;
            st.Console.Stop();
            doc.Console = st.Console.Snapshot();
            if (st.Perf != null)
            {
                doc.Perf = st.Perf.Summary();
                double? growth = st.Perf.MemoryGrowthPctPer30Min();
                if (growth.HasValue && st.Ctx?.Config != null)
                    st.Ctx.AddProbeResult(st.Ctx.Config.Evaluate("PerfProbe", "soak_memory_growth_pct_per_30min", growth.Value));
                try
                {
                    st.Perf.Finish(options.WritePerfFile, options.OutDir);
                }
                catch (Exception e)
                {
                    KitLog.Warn("PerfProbe finish failed: " + e.Message);
                }
                st.Perf.OwnedByRunner = false;
            }
            if (st.Ctx != null && AudioEventProbe.HasActivity) st.Ctx.AddProbeResults(AudioEventProbe.Evaluate(st.Ctx.Config));
            AudioEventProbe.Enabled = false;

            doc.EndedUtc = KitIo.UtcNowIso();
            doc.Result = doc.ComputeResult(st.RunnerError);
            st.Result.ChecksPath = string.IsNullOrEmpty(options.OutDir) ? null : Path.Combine(options.OutDir, ChecksWriter.FileName);
            try
            {
                st.Result.ChecksPath = ChecksWriter.Write(options.OutDir, doc);
                KitLog.Info("QA " + doc.Scenario + " " + doc.Result + " → " + st.Result.ChecksPath);
            }
            catch (Exception e)
            {
                KitLog.Error("QA: could not write checks.json: " + e.Message);
                doc.Result = QaStatus.Error;
            }
            IsRunning = false;
            if (s_Active == st) s_Active = null;

            try { st.Done?.Invoke(st.Result); }
            catch (Exception e) { KitLog.Warn("QA done callback threw: " + e.Message); }
        }

        /// <summary>Player watchdog: a run stuck past its deadline (a Coroutine or AsyncOperation that never ends) is
        /// stopped, the current step fails, checks.json is written and the player quits.</summary>
        void Update()
        {
            var st = s_Active;
            if (st == null || st.Finished || Time.realtimeSinceStartup < st.RunDeadline + WatchdogGraceSeconds) return;
            StopAllCoroutines();
            string msg = "watchdog: exceeded -qa-timeout " + st.Options.RunTimeoutSeconds + " s by " + WatchdogGraceSeconds + " s";
            var step = st.Ctx?.CurrentStep;
            if (step != null)
            {
                step.Status = QaStatus.Fail;
                step.AssertFailures.Add(msg + " while this step was running");
                step.TSeconds = st.Ctx.ElapsedSeconds;
                st.Ctx.CurrentStep = null;
            }
            else AddStep(st.Doc, "watchdog", QaStatus.Fail, msg);
            KitLog.Error("QA " + st.Doc.Scenario + ": " + msg);
            Finish(st);
            if (st.Options.QuitWhenDone && !Application.isEditor) Application.Quit(st.Result.ExitCode);
        }

        static void AddStep(ChecksDocument doc, string name, string status, string message)
        {
            var s = new QaStepResult { Name = name, Status = status };
            s.AssertFailures.Add(message);
            doc.Steps.Add(s);
        }

        static void ResolvePaths(QaRunOptions options, ChecksDocument doc)
        {
            string leaf = string.IsNullOrEmpty(options.OutDir) ? null : Path.GetFileName(Path.GetFullPath(options.OutDir).TrimEnd('/', '\\'));
            if (!string.IsNullOrEmpty(options.RunId)) doc.RunId = options.RunId;
            else if (KitIo.IsRunId(leaf)) doc.RunId = leaf;
            else doc.RunId = KitIo.FileStamp(DateTime.UtcNow) + "-" + doc.Scenario;
            if (!KitIo.IsRunId(doc.RunId)) doc.RunId = KitIo.FileStamp(DateTime.UtcNow) + "-" + doc.Scenario;

            if (string.IsNullOrEmpty(options.OutDir))
            {
                string ws = FindWorkspace(null);
                options.OutDir = ws != null
                    ? Path.Combine(ws, "Docs", "qa", "runs", doc.RunId)
                    : Path.Combine(Application.isEditor ? Path.GetFullPath("Temp") : Application.persistentDataPath, "qa", doc.RunId);
            }
            options.OutDir = Path.GetFullPath(options.OutDir);
        }

        /// <summary>W from -qa-workspace, the run folder (W/Docs/qa/runs/x), or by walking up to Tools/kit/kit.json.</summary>
        static string FindWorkspace(string outDir)
        {
            string arg = KitArgs.Current.Get("-qa-workspace");
            if (!string.IsNullOrEmpty(arg) && Directory.Exists(arg)) return Path.GetFullPath(arg);
            string fromRun = KitIo.WorkspaceFromRunDir(outDir);
            if (fromRun != null) return fromRun;
            string kitJson = KitIo.FindUpwards(Path.GetDirectoryName(Application.dataPath), Path.Combine("Tools", "kit", "kit.json"), 4);
            return kitJson == null ? null : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(kitJson)));
        }

        sealed class Outcome
        {
            public bool TimedOut;
            public string FailMessage;
            public Exception Error;
        }

        static IEnumerator RunStep(QaStep step, QaContext ctx, QaStepResult res, float deadline)
        {
            // Precondition.
            if (step.Precondition != null)
            {
                while (true)
                {
                    bool ok;
                    try { ok = step.Precondition(ctx); }
                    catch (Exception e)
                    {
                        res.Status = QaStatus.Error;
                        res.AssertFailures.Add("precondition threw " + e.GetType().Name + ": " + e.Message);
                        yield break;
                    }
                    if (ok) break;
                    if (Time.realtimeSinceStartup > deadline)
                    {
                        if (step.Optional)
                        {
                            res.Status = QaStatus.Skip;
                            res.Probes["skipped"] = "precondition not met within " + step.TimeoutSeconds + " s";
                        }
                        else
                        {
                            res.Status = QaStatus.Fail;
                            res.AssertFailures.Add("precondition not met within " + step.TimeoutSeconds + " s");
                        }
                        yield break;
                    }
                    yield return null;
                }
            }

            // Action.
            if (step.Act != null)
            {
                IEnumerator act = null;
                try { act = step.Act(ctx); }
                catch (QaFailException f) { res.AssertFailures.Add(f.Message); }
                catch (Exception e)
                {
                    res.Status = QaStatus.Error;
                    res.AssertFailures.Add("action threw " + e.GetType().Name + ": " + e.Message);
                    yield break;
                }
                if (act != null)
                {
                    var outcome = new Outcome();
                    yield return Drive(act, deadline, outcome);
                    if (outcome.Error != null)
                    {
                        res.Status = QaStatus.Error;
                        res.AssertFailures.Add("action threw " + outcome.Error.GetType().Name + ": " + outcome.Error.Message);
                        KitLog.Warn("QA step " + res.Name + ": " + outcome.Error);
                        yield break;
                    }
                    if (outcome.FailMessage != null) res.AssertFailures.Add(outcome.FailMessage);
                    if (outcome.TimedOut)
                    {
                        res.Status = QaStatus.Fail;
                        res.AssertFailures.Add("timed out after " + step.TimeoutSeconds + " s");
                        yield break;
                    }
                }
            }

            // Probe and asserts.
            if (step.Probe != null)
            {
                try
                {
                    var facts = step.Probe(ctx);
                    if (facts != null) foreach (var kv in facts) res.Probes[kv.Key] = kv.Value;
                }
                catch (Exception e)
                {
                    res.Status = QaStatus.Error;
                    res.AssertFailures.Add("probe threw " + e.GetType().Name + ": " + e.Message);
                    yield break;
                }
            }
            foreach (var assert in step.Asserts)
            {
                string msg;
                try { msg = assert?.Invoke(res.Probes); }
                catch (Exception e) { msg = "assert threw " + e.GetType().Name + ": " + e.Message; }
                if (msg != null) res.AssertFailures.Add(msg);
            }

            // Captures.
            foreach (var cam in step.CaptureAt) yield return ctx.CaptureCamera(cam);

            if (res.Status == QaStatus.Pass && res.AssertFailures.Count > 0) res.Status = QaStatus.Fail;
        }

        /// <summary>
        /// Runs a coroutine tree by hand so nested IEnumerators honour the step deadline and exceptions are caught.
        /// AsyncOperations and CustomYieldInstructions are polled here, so they time out with the step (a scene load
        /// with allowSceneActivation = false never finishes). Other yield instructions (WaitForSeconds, a Coroutine)
        /// go to Unity; one that never ends is caught by the player watchdog (QaRunner.Update).
        /// </summary>
        static IEnumerator Drive(IEnumerator root, float deadline, Outcome outcome)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    outcome.TimedOut = true;
                    yield break;
                }
                var top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (QaFailException f)
                {
                    outcome.FailMessage = f.Message;
                    yield break;
                }
                catch (Exception e)
                {
                    outcome.Error = e;
                    yield break;
                }
                if (!moved)
                {
                    stack.Pop();
                    continue;
                }
                var current = top.Current;
                // CustomYieldInstruction (WaitUntil, WaitWhile, …) is an IEnumerator, so it is driven here too.
                if (current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                if (current is AsyncOperation op)
                {
                    while (!op.isDone)
                    {
                        if (Time.realtimeSinceStartup > deadline)
                        {
                            outcome.TimedOut = true;
                            yield break;
                        }
                        yield return null;
                    }
                    continue;
                }
                yield return current;
            }
        }
    }
}
#endif
