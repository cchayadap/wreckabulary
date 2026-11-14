// Game Studio Kit (MIT). Frame-time statistics and the perf.json document (checks.schema.json#/$defs/perf).
// Engine-free, so the percentile maths and the JSON shape are unit-tested under plain .NET.
using System;
using System.Collections.Generic;
using GameStudioKit.Common;

namespace GameStudioKit.Qa
{
    /// <summary>
    /// Collects frame times without per-frame allocation (a pre-sized list) and reports nearest-rank percentiles.
    /// Frames inside a "first visit" window (a new scene or area) count toward the hitch figure, not toward the
    /// steady-state worst frame.
    /// </summary>
    public sealed class FrameStats
    {
        readonly List<float> _frames;
        readonly List<float> _gcKb;
        float _worst;
        float _firstVisitWorst;
        bool _hasFirstVisit;
        double _sysMemMbMax;
        bool _hasSysMem;
        double _memStartMb, _memEndMb, _memStartS, _memEndS;
        bool _hasMemSpan;

        /// <summary>Shortest post-warm-up window (seconds) over which memory growth is reported; shorter windows would
        /// extrapolate noise to 30 minutes.</summary>
        public const double DefaultMemoryGrowthMinSeconds = 600;

        public FrameStats(int expectedFrames = 4096)
        {
            _frames = new List<float>(Math.Max(16, expectedFrames));
            _gcKb = new List<float>(Math.Max(16, expectedFrames));
        }

        public int Count => _frames.Count;

        public void Clear()
        {
            _frames.Clear();
            _gcKb.Clear();
            _worst = 0;
            _firstVisitWorst = 0;
            _hasFirstVisit = false;
            _sysMemMbMax = 0;
            _hasSysMem = false;
            _memStartMb = _memEndMb = _memStartS = _memEndS = 0;
            _hasMemSpan = false;
        }

        /// <summary>Adds one measured frame. <paramref name="inFirstVisitWindow"/> frames are excluded from the
        /// steady-state worst frame and tracked as the first-visit hitch instead.</summary>
        public void AddFrame(float ms, bool inFirstVisitWindow = false)
        {
            if (float.IsNaN(ms) || float.IsInfinity(ms) || ms < 0) return;
            _frames.Add(ms);
            if (inFirstVisitWindow)
            {
                _hasFirstVisit = true;
                if (ms > _firstVisitWorst) _firstVisitWorst = ms;
            }
            else if (ms > _worst) _worst = ms;
        }

        public void AddGcKb(float kb)
        {
            if (!float.IsNaN(kb) && kb >= 0) _gcKb.Add(kb);
        }

        public void AddSysMemMb(double mb)
        {
            if (double.IsNaN(mb) || mb < 0) return;
            _hasSysMem = true;
            if (mb > _sysMemMbMax) _sysMemMbMax = mb;
        }

        /// <summary>A post-warm-up memory sample at <paramref name="seconds"/> (any monotonic clock). The first sample is
        /// the growth baseline and the latest one the end value.</summary>
        public void AddSysMemMb(double mb, double seconds)
        {
            if (double.IsNaN(mb) || mb < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            AddSysMemMb(mb);
            if (!_hasMemSpan)
            {
                _hasMemSpan = true;
                _memStartMb = mb;
                _memStartS = seconds;
            }
            _memEndMb = mb;
            _memEndS = Math.Max(_memStartS, seconds);
        }

        public double Percentile(double p) => PercentileOf(_frames, p);
        public double WorstFrameMs => _worst;
        public double? FirstVisitHitchMs => _hasFirstVisit ? _firstVisitWorst : (double?)null;
        public double? GcKbFrameP95 => _gcKb.Count > 0 ? PercentileOf(_gcKb, 95) : (double?)null;
        public double? SysMemMbMax => _hasSysMem ? _sysMemMbMax : (double?)null;
        public double? SysMemMbStart => _hasMemSpan ? _memStartMb : (double?)null;
        public double? SysMemMbEnd => _hasMemSpan ? _memEndMb : (double?)null;

        /// <summary>Seconds between the first and last timed memory sample (0 without samples).</summary>
        public double MemorySpanSeconds => _hasMemSpan ? _memEndS - _memStartS : 0;

        /// <summary>
        /// soak_memory_growth_pct_per_30min: (end − start) / start × 100 × (1800 / span), or null when the span is shorter
        /// than <paramref name="minSeconds"/> or there is no baseline.
        /// </summary>
        public double? MemoryGrowthPctPer30Min(double minSeconds = DefaultMemoryGrowthMinSeconds) =>
            GrowthPctPer30Min(SysMemMbStart, SysMemMbEnd, MemorySpanSeconds, minSeconds);

        public static double? GrowthPctPer30Min(double? startMb, double? endMb, double spanSeconds, double minSeconds = DefaultMemoryGrowthMinSeconds)
        {
            if (!startMb.HasValue || !endMb.HasValue || startMb.Value <= 0) return null;
            if (spanSeconds <= 0 || spanSeconds < minSeconds) return null;
            return (endMb.Value - startMb.Value) / startMb.Value * 100.0 * (1800.0 / spanSeconds);
        }

        /// <summary>Nearest-rank percentile (p in 0..100) of an unsorted list; 0 for an empty list.</summary>
        public static double PercentileOf(List<float> values, double p)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = new float[values.Count];
            values.CopyTo(sorted);
            Array.Sort(sorted);
            if (p <= 0) return sorted[0];
            if (p >= 100) return sorted[sorted.Length - 1];
            int rank = (int)Math.Ceiling(p / 100.0 * sorted.Length);
            int idx = Math.Min(sorted.Length - 1, Math.Max(0, rank - 1));
            return sorted[idx];
        }

        public QaPerfSummary ToSummary(double memoryGrowthMinSeconds = DefaultMemoryGrowthMinSeconds) => new QaPerfSummary
        {
            FrameMsP50 = Count > 0 ? Percentile(50) : (double?)null,
            FrameMsP95 = Count > 0 ? Percentile(95) : (double?)null,
            WorstFrameMs = Count > 0 ? WorstFrameMs : (double?)null,
            FirstVisitHitchMs = FirstVisitHitchMs,
            GcKbFrameP95 = GcKbFrameP95,
            SysMemMbMax = SysMemMbMax,
            SysMemGrowthPctPer30Min = MemoryGrowthPctPer30Min(memoryGrowthMinSeconds),
            MeasuredSeconds = _hasMemSpan ? MemorySpanSeconds : (double?)null,
        };
    }

    /// <summary>perf.json. Only kind release and release-qa count for gates; editor numbers never do.</summary>
    public sealed class PerfReport : IKitJson
    {
        public string BuildId;

        /// <summary>release | release-qa | dev | editor.</summary>
        public string Kind = "editor";
        public string Scenario;
        public string Machine;
        public string StartedUtc;
        public string EndedUtc;
        public int Frames;
        public int WarmupFrames;
        public double FrameMsP50;
        public double FrameMsP95;
        public double FrameMsP99;
        public double WorstFrameMs;
        public double? FirstVisitHitchMs;
        public double? GcKbFrameP95;
        public double? SysMemMbMax;

        /// <summary>Additive fields: memory at the first and last post-warm-up sample, the seconds between them, and the
        /// growth scaled to 30 minutes (only when the window is at least the minimum; see FrameStats).</summary>
        public double? SysMemMbStart;
        public double? SysMemMbEnd;
        public double? MeasuredSeconds;
        public double? SysMemGrowthPctPer30Min;

        public static PerfReport From(FrameStats stats, int warmupFrames, double memoryGrowthMinSeconds = FrameStats.DefaultMemoryGrowthMinSeconds)
        {
            return new PerfReport
            {
                Frames = stats.Count,
                WarmupFrames = Math.Max(0, warmupFrames),
                FrameMsP50 = stats.Percentile(50),
                FrameMsP95 = stats.Percentile(95),
                FrameMsP99 = stats.Percentile(99),
                WorstFrameMs = stats.WorstFrameMs,
                FirstVisitHitchMs = stats.FirstVisitHitchMs,
                GcKbFrameP95 = stats.GcKbFrameP95,
                SysMemMbMax = stats.SysMemMbMax,
                SysMemMbStart = stats.SysMemMbStart,
                SysMemMbEnd = stats.SysMemMbEnd,
                MeasuredSeconds = stats.SysMemMbStart.HasValue ? stats.MemorySpanSeconds : (double?)null,
                SysMemGrowthPctPer30Min = stats.MemoryGrowthPctPer30Min(memoryGrowthMinSeconds),
            };
        }

        public void WriteJson(KitJsonWriter w)
        {
            w.BeginObject();
            w.Prop("schema", 1);
            w.Prop("build_id", BuildId);
            w.Prop("kind", Kind);
            if (!string.IsNullOrEmpty(Scenario)) w.Prop("scenario", Scenario);
            w.Prop("machine", string.IsNullOrEmpty(Machine) ? "unknown" : Machine);
            w.Prop("started_utc", StartedUtc);
            if (!string.IsNullOrEmpty(EndedUtc)) w.Prop("ended_utc", EndedUtc);
            w.Prop("frames", Frames);
            w.Prop("warmup_frames", WarmupFrames);
            w.Prop("frame_ms_p50", Math.Max(0, FrameMsP50));
            w.Prop("frame_ms_p95", Math.Max(0, FrameMsP95));
            w.Prop("frame_ms_p99", Math.Max(0, FrameMsP99));
            w.Prop("worst_frame_ms", Math.Max(0, WorstFrameMs));
            QaPerfSummary.Opt(w, "first_visit_hitch_ms", FirstVisitHitchMs);
            QaPerfSummary.Opt(w, "gc_kb_frame_p95", GcKbFrameP95);
            QaPerfSummary.Opt(w, "sys_mem_mb_max", SysMemMbMax);
            QaPerfSummary.Opt(w, "sys_mem_mb_start", SysMemMbStart);
            QaPerfSummary.Opt(w, "sys_mem_mb_end", SysMemMbEnd);
            QaPerfSummary.Opt(w, "measured_s", MeasuredSeconds);
            QaPerfSummary.OptSigned(w, "sys_mem_growth_pct_per_30min", SysMemGrowthPctPer30Min);
            w.EndObject();
        }

        /// <summary>No frame was measured after the warmup (the session was shorter than the warmup).</summary>
        public bool IsEmpty => Frames <= 0;

        public string ToJson() => KitJson.Serialize(this, true) + "\n";

        /// <summary>Atomic write. Refuses an empty report: perf.json needs at least one measured frame
        /// (checks.schema.json#/$defs/perf "frames" >= 1), and a missing file tells the gate the run measured nothing.</summary>
        public void WriteTo(string path)
        {
            if (IsEmpty)
                throw new InvalidOperationException("no frames were measured after the " + WarmupFrames + "-frame warmup (raise -qa-perf-seconds or lower -qa-perf-warmup)");
            KitIo.WriteAllTextAtomic(path, ToJson());
        }
    }
}
