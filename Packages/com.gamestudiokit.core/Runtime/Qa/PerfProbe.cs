// Game Studio Kit (MIT). Frame-time and memory probe that compiles into every player, release included, because release
// performance must be measured in the release build (ARCHITECTURE §4.4). It is inert unless the player is started with
// -qa-perf; then it writes <-qa-out>/perf.json (checks.schema.json#/$defs/perf) and quits after -qa-perf-seconds.
// Release players only get counters the Unity 6000.3 Profiler counters reference marks available there; the GC-alloc
// counter is development/editor only.
using System;
using System.IO;
using GameStudioKit.Common;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameStudioKit.Qa
{
    [DefaultExecutionOrder(-32000)]
    [AddComponentMenu("")]
    public sealed class PerfProbe : MonoBehaviour
    {
        public const int DefaultWarmupFrames = 120;
        public const int DefaultFirstVisitFrames = 30;
        public const float DefaultSeconds = 60f;

        public static PerfProbe Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        /// <summary>Set by QaRunner: the runner decides when to finish and quit, not the probe.</summary>
        public bool OwnedByRunner { get; set; }

        /// <summary>Shortest post-warm-up window (s) over which memory growth is reported (-qa-mem-min-seconds).</summary>
        public double MemoryGrowthMinSeconds { get; set; } = FrameStats.DefaultMemoryGrowthMinSeconds;
        public bool IsRunning => _running;
        public string Scenario => _scenario;

        FrameStats _stats = new FrameStats(8192);
        ProfilerRecorder _sysMem;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        ProfilerRecorder _gcAlloc;
#endif
        bool _running;
        int _warmupFrames;
        int _warmupLeft;
        int _firstVisitLeft;
        float _secondsLimit;
        float _startRealtime;
        bool _selfQuit;
        string _outDir;
        string _scenario;
        string _startedUtc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Application.isEditor) return;
            var args = KitArgs.Current;
            if (!args.Has("-qa-perf")) return;
            var probe = GetOrCreate();
            probe._outDir = args.Get("-qa-out");
            probe._secondsLimit = Mathf.Max(1f, args.GetFloat("-qa-perf-seconds", DefaultSeconds));
            probe._selfQuit = true;
            probe.MemoryGrowthMinSeconds = Mathf.Max(0f, args.GetFloat("-qa-mem-min-seconds", (float)FrameStats.DefaultMemoryGrowthMinSeconds));
            probe.Begin(args.Get("-qa-scenario", "perf_frontend"), args.GetInt("-qa-perf-warmup", DefaultWarmupFrames));
            KitLog.Info("PerfProbe: measuring for " + probe._secondsLimit + " s (-qa-perf)");
        }

        /// <summary>The live probe, created on a DontDestroyOnLoad object when missing.</summary>
        public static PerfProbe GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GSK PerfProbe");
            DontDestroyOnLoad(go);
            return go.AddComponent<PerfProbe>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            StopRecorders();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        /// <summary>Starts (or restarts) a measurement session.</summary>
        public void Begin(string scenario, int warmupFrames = DefaultWarmupFrames)
        {
            StopRecorders();
            _stats.Clear();
            _scenario = string.IsNullOrEmpty(scenario) ? "perf" : scenario;
            _warmupFrames = Mathf.Max(0, warmupFrames);
            _warmupLeft = _warmupFrames;
            _firstVisitLeft = 0;
            _startRealtime = Time.realtimeSinceStartup;
            _startedUtc = KitIo.UtcNowIso();
            _sysMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
#endif
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _running = true;
        }

        /// <summary>Marks the next frames as a first visit (new scene or area): they feed first_visit_hitch_ms and
        /// are kept out of the steady-state worst frame.</summary>
        public void MarkFirstVisit(int frames = DefaultFirstVisitFrames)
        {
            _firstVisitLeft = Mathf.Max(_firstVisitLeft, Mathf.Max(1, frames));
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MarkFirstVisit();

        void Update()
        {
            if (!_running) return;
            float ms = Time.unscaledDeltaTime * 1000f;
            if (_warmupLeft > 0) _warmupLeft--;
            else
            {
                _stats.AddFrame(ms, _firstVisitLeft > 0);
                if (_sysMem.Valid) _stats.AddSysMemMb(_sysMem.LastValue / (1024.0 * 1024.0), Time.realtimeSinceStartup - _startRealtime);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (_gcAlloc.Valid) _stats.AddGcKb(_gcAlloc.LastValue / 1024f);
#endif
            }
            if (_firstVisitLeft > 0) _firstVisitLeft--;

            if (_selfQuit && !OwnedByRunner && Time.realtimeSinceStartup - _startRealtime >= _secondsLimit)
            {
                _selfQuit = false;
                Finish(true);
                Application.Quit(0);
            }
        }

        /// <summary>Summary for the checks.json "perf" block.</summary>
        public QaPerfSummary Summary() => _stats.ToSummary(MemoryGrowthMinSeconds);

        /// <summary>soak_memory_growth_pct_per_30min so far, or null while the post-warm-up window is shorter than
        /// <see cref="MemoryGrowthMinSeconds"/>.</summary>
        public double? MemoryGrowthPctPer30Min() => _stats.MemoryGrowthPctPer30Min(MemoryGrowthMinSeconds);

        /// <summary>Seconds of post-warm-up memory samples so far.</summary>
        public double MeasuredSeconds => _stats.MemorySpanSeconds;

        /// <summary>Ends the session and returns perf.json; with <paramref name="writeFile"/> it is written to
        /// &lt;-qa-out&gt;/perf.json (or persistentDataPath/qa when -qa-out is missing).</summary>
        public PerfReport Finish(bool writeFile, string outDir = null)
        {
            var report = PerfReport.From(_stats, _warmupFrames, MemoryGrowthMinSeconds);
            report.Kind = BuildKind;
            report.BuildId = BuildId();
            report.Scenario = _scenario;
            report.Machine = MachineKey();
            report.StartedUtc = _startedUtc ?? KitIo.UtcNowIso();
            report.EndedUtc = KitIo.UtcNowIso();
            _running = false;
            StopRecorders();
            if (writeFile)
            {
                string dir = outDir ?? _outDir;
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Path.Combine(Application.persistentDataPath, "qa");
                    KitLog.Warn("PerfProbe: no -qa-out given; writing perf.json to " + dir);
                }
                try
                {
                    report.WriteTo(Path.Combine(dir, "perf.json"));
                }
                catch (Exception e)
                {
                    KitLog.Error("PerfProbe: could not write perf.json: " + e.Message);
                }
            }
            return report;
        }

        void StopRecorders()
        {
            if (_sysMem.Valid) _sysMem.Dispose();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_gcAlloc.Valid) _gcAlloc.Dispose();
#endif
        }

        /// <summary>release | release-qa | dev | editor, from compile-time defines.</summary>
        public static string BuildKind
        {
            get
            {
#if UNITY_EDITOR
                return "editor";
#elif DEVELOPMENT_BUILD
                return "dev";
#elif GSK_QA
                return "release-qa";
#else
                return "release";
#endif
            }
        }

        /// <summary>
        /// The build id of a player: -qa-build-id, else the Builds/&lt;dir&gt; name with its qa-/dev- prefix removed (the
        /// same rule as packed_assets.json and build.json, so evidence of one build joins on build_id); null in the editor.
        /// </summary>
        public static string BuildId()
        {
            if (Application.isEditor) return null;
            string arg = KitArgs.Current.Get("-qa-build-id");
            if (!string.IsNullOrEmpty(arg)) return arg;
            try
            {
                string parent = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(parent)) return null;
                KitIo.ParseBuildFolder(Path.GetFileName(parent), false, out _, out string id);
                return id;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Baseline key: device name plus a GPU tag (per-machine baselines; never compare across machines).</summary>
        public static string MachineKey() => SystemInfo.deviceName + "-" + KitIo.Slug(SystemInfo.graphicsDeviceName, 32);
    }
}
