// Game Studio Kit (MIT). Docs/qa/probes.config.json (probes.config.schema.json, CONTRACTS §7.4) loader and threshold
// evaluator with body-class scaling. Engine-free: the node tests run it under plain .NET against the example config.
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;

namespace GameStudioKit.Qa
{
    public sealed class ProbeThreshold
    {
        public string Metric;
        public string Op;
        public double Number;
        public bool? Bool;
        public string Unit;
        public bool ScalesWithBody;
        public string OnFail;
        public string Tag;
        public string Source;

        public ProbeThreshold Clone() => (ProbeThreshold)MemberwiseClone();
    }

    public sealed class ProbeBody
    {
        public string Id;
        public string BodyClass;
        public double HeightM;
        public string Rig;
        public List<string> Clips = new List<string>();
        public string Prefab;
    }

    public sealed class ProbeOverride
    {
        public string Metric;
        public string Body;
        public double Number;
        public bool? Bool;
        public string Reason;
        public string Utc;
    }

    /// <summary>
    /// Thresholds for every probe metric. Distance thresholds with scales_with_body are multiplied by
    /// k = body height / reference height (a 0.95 m frog against the 1.8 m reference gives k ≈ 0.53).
    /// Unknown metrics evaluate to ROUTE: a number without a threshold goes to the reviewer, never an automatic pass.
    /// </summary>
    public sealed class ProbeConfig
    {
        public const double DefaultReferenceHeightM = 1.8;

        public double ReferenceHeightM = DefaultReferenceHeightM;
        public string Project;
        public readonly Dictionary<string, double> BodyClassHeights = new Dictionary<string, double>(StringComparer.Ordinal);
        public readonly List<ProbeBody> Bodies = new List<ProbeBody>();
        public readonly Dictionary<string, ProbeThreshold> Thresholds = new Dictionary<string, ProbeThreshold>(StringComparer.Ordinal);
        public readonly List<ProbeOverride> Overrides = new List<ProbeOverride>();

        /// <summary>Problems found while parsing (bad ops, bad types). Parsing never throws for these.</summary>
        public readonly List<string> Warnings = new List<string>();

        /// <summary>Where the config came from: a file path, or "defaults".</summary>
        public string SourcePath = "defaults";

        static readonly HashSet<string> Ops = new HashSet<string> { "<=", "<", ">=", ">", "==", "abs<=" };
        static readonly HashSet<string> OnFails = new HashSet<string> { "FAIL", "ROUTE", "BISECT", "REVERT" };

        /// <summary>
        /// The 40 default metrics of ARCHITECTURE §3.12 (values equal probes.config.example.json; a node test checks
        /// this) plus kit extras used by the world, contact and audio probes.
        /// </summary>
        public static ProbeConfig Defaults()
        {
            var c = new ProbeConfig();
            c.BodyClassHeights["humanoid"] = 1.8;
            c.BodyClassHeights["small_biped"] = 0.95;
            c.BodyClassHeights["quadruped"] = 1.0;
            c.BodyClassHeights["prop"] = 1.0;
            foreach (var t in DefaultTable()) c.Thresholds[t.Metric] = t;
            return c;
        }

        static IEnumerable<ProbeThreshold> DefaultTable()
        {
            // metric, op, value, unit, scales_with_body, on_fail, tag
            yield return D("idle_foot_drift_lock_on_cm", "<=", 1, "cm", true, "FAIL", "[V-bin]");
            yield return D("idle_foot_drift_lock_off_cm", "<=", 1.2, "cm", true, "FAIL", "[C]");
            yield return D("walk_start_foot_pop_cm", "<=", 1.5, "cm", true, "FAIL", "[V-bin]");
            yield return D("idle_pop_offset_jump_cm", "<=", 3, "cm", true, "FAIL", "[V-bin]");
            yield return D("lock_release_latency_s", "<=", 0.15, "s", false, "FAIL", "[V-bin]");
            yield return D("locked_fraction", ">=", 0.95, "ratio", false, "FAIL", "[V-bin]");
            yield return D("foot_heading_vs_facing_deg", "<=", 6, "deg", false, "FAIL", "[C]");
            yield return D("stance_foot_slide_cm_per_step", "<=", 2, "cm", true, "FAIL", "[P]");
            yield return D("stance_height_error_cm", "abs<=", 3, "cm", true, "FAIL", "[P]");
            yield return D("foot_crossing_frames_per_cycle", "<=", 0, "frames", false, "FAIL", "[C]");
            yield return D("idle_spine_lean_deg", "<", 10, "deg", false, "FAIL", "[P]");
            yield return D("idle_head_tilt_deg", "<=", 5, "deg", false, "FAIL", "[C]");
            yield return D("prop_over_face_pct", "<=", 0, "pct", false, "FAIL", "[C]");
            yield return D("grip_offset_cm", "<=", 3, "cm", true, "FAIL", "[P]");
            yield return D("death_pose_ground_penetration_cm", "<=", 1, "cm", true, "FAIL", "[P]");
            yield return D("foot_lock_cpu_ms_per_character", "<=", 0.01, "ms", false, "FAIL", "[C]");
            yield return D("frame_time_p95_ms", "<=", 16.6, "ms", false, "FAIL", "project");
            yield return D("perf_regression_p95_pct", "<=", 5, "pct", false, "BISECT", "[P]");
            yield return D("visual_diff_changed_pct", "<=", 0.5, "pct", false, "ROUTE", "[P]");
            yield return D("console_exceptions", "<=", 0, "count", false, "FAIL", "[C]");
            yield return D("console_errors", "<=", 0, "count", false, "FAIL", "[C]");
            yield return D("soak_seed_completion_pct", ">=", 90, "pct", false, "FAIL", "[C]");
            yield return D("soak_memory_growth_pct_per_30min", "<=", 5, "pct", false, "FAIL", "[P]");
            yield return D("prop_angular_velocity_deg_s", "<=", 90, "deg/s", false, "FAIL", "[P]");
            yield return D("prop_up_axis_error_deg", "<=", 20, "deg", false, "FAIL", "[P]");
            yield return D("worst_frame_after_warmup_ms", "<=", 33, "ms", false, "FAIL", "[P]");
            yield return D("first_visit_hitch_ms", "<=", 50, "ms", false, "FAIL", "[P]");
            yield return B("goldpath_no_jump_completes", "==", true, "bool", false, "FAIL", "[P]");
            yield return D("jumpable_obstacle_margin_m", ">=", 0.1, "m", false, "FAIL", "[P]");
            yield return D("blocking_collider_margin_m", ">=", 0.2, "m", false, "FAIL", "[P]");
            yield return D("orphan_colliders", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("bulk_tool_unintended_move_m", "<=", 0.5, "m", false, "FAIL", "[P]");
            yield return D("below_terrain_objects", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("animator_missing_controller", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("bone_motion_latency_s", "<=", 1, "s", false, "FAIL", "[P]");
            yield return D("extra_world_camera_min_interval_frames", ">=", 4, "frames", false, "FAIL", "[P]");
            yield return D("bake_splat_sum_error", "<=", 0.01, "ratio", false, "FAIL", "[P]");
            yield return D("bake_nan_black_texel_pct", "<", 0.1, "pct", false, "FAIL", "[P]");
            yield return D("texture_import_downsample_factor", "<=", 2, "ratio", false, "FAIL", "[P]");
            yield return D("polish_lane_pass_delta", ">=", 0, "count", false, "REVERT", "[C]");
            // Kit extras (not in the 40 required ids; a project config may override them like any other metric).
            yield return D("floating_props", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("sunk_props", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("collider_render_divergence", "<=", 0, "count", false, "ROUTE", "[P]");
            yield return D("placeholder_objects", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("lod_double_draw", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("bind_pose_while_moving", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("navmesh_unreachable", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("pose_float_cm", "<=", 3, "cm", true, "FAIL", "[P]");
            yield return D("audio_events_missing", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("audio_events_inaudible", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("animator_unexpected_controller", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("route_ground_holes", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("bulk_tool_missing_groups", "<=", 0, "count", false, "FAIL", "[P]");
            yield return D("audit_exemptions", "<=", 0, "count", false, "ROUTE", "[P]");
        }

        static ProbeThreshold D(string metric, string op, double value, string unit, bool scales, string onFail, string tag) =>
            new ProbeThreshold { Metric = metric, Op = op, Number = value, Unit = unit, ScalesWithBody = scales, OnFail = onFail, Tag = tag };

        static ProbeThreshold B(string metric, string op, bool value, string unit, bool scales, string onFail, string tag) =>
            new ProbeThreshold { Metric = metric, Op = op, Bool = value, Unit = unit, ScalesWithBody = scales, OnFail = onFail, Tag = tag };

        /// <summary>Parses a probes.config.json document. Thresholds it does not name keep their defaults.</summary>
        public static ProbeConfig Parse(string json)
        {
            var c = Defaults();
            var root = KitJson.AsObject(KitJson.Parse(json));
            if (root == null) throw new FormatException("probes.config.json: the root is not an object");
            c.Project = KitJson.GetString(root, "project");
            double refH = KitJson.GetDouble(root, "reference_height_m", DefaultReferenceHeightM);
            if (refH > 0) c.ReferenceHeightM = refH;
            else c.Warnings.Add("reference_height_m must be > 0; using 1.8");

            var classes = KitJson.GetObject(root, "body_classes");
            if (classes != null)
            {
                foreach (var kv in classes)
                {
                    double h = KitJson.GetDouble(KitJson.AsObject(kv.Value), "height_m", 0);
                    if (h > 0) c.BodyClassHeights[kv.Key] = h;
                    else c.Warnings.Add("body_classes." + kv.Key + ".height_m must be > 0");
                }
            }

            var bodies = KitJson.GetList(root, "bodies");
            if (bodies != null)
            {
                foreach (var item in bodies)
                {
                    var o = KitJson.AsObject(item);
                    if (o == null) continue;
                    var b = new ProbeBody
                    {
                        Id = KitJson.GetString(o, "id"),
                        BodyClass = KitJson.GetString(o, "body_class"),
                        HeightM = KitJson.GetDouble(o, "height_m", 0),
                        Rig = KitJson.GetString(o, "rig"),
                        Prefab = KitJson.GetString(o, "prefab"),
                        Clips = KitJson.GetStringList(o, "clips"),
                    };
                    if (string.IsNullOrEmpty(b.Id)) { c.Warnings.Add("a body has no id"); continue; }
                    if (b.HeightM <= 0) c.Warnings.Add("bodies[" + b.Id + "].height_m must be > 0");
                    c.Bodies.Add(b);
                }
            }

            var thresholds = KitJson.GetObject(root, "thresholds");
            if (thresholds != null)
            {
                foreach (var kv in thresholds)
                {
                    var o = KitJson.AsObject(kv.Value);
                    if (o == null) { c.Warnings.Add("thresholds." + kv.Key + " is not an object"); continue; }
                    var t = new ProbeThreshold
                    {
                        Metric = kv.Key,
                        Op = KitJson.GetString(o, "op"),
                        Unit = KitJson.GetString(o, "unit", ""),
                        ScalesWithBody = KitJson.GetBool(o, "scales_with_body"),
                        OnFail = KitJson.GetString(o, "on_fail", "FAIL"),
                        Tag = KitJson.GetString(o, "tag"),
                        Source = KitJson.GetString(o, "source"),
                    };
                    var v = KitJson.Get(o, "value");
                    if (v is bool bv) t.Bool = bv;
                    else if (KitJson.IsNumber(v)) t.Number = KitJson.ToDouble(v);
                    else { c.Warnings.Add("thresholds." + kv.Key + ".value must be a number or a boolean"); continue; }
                    if (t.Op == null || !Ops.Contains(t.Op)) { c.Warnings.Add("thresholds." + kv.Key + ".op is invalid"); continue; }
                    if (!OnFails.Contains(t.OnFail)) { c.Warnings.Add("thresholds." + kv.Key + ".on_fail is invalid; using FAIL"); t.OnFail = "FAIL"; }
                    c.Thresholds[kv.Key] = t;
                }
            }

            var overrides = KitJson.GetList(root, "overrides");
            if (overrides != null)
            {
                foreach (var item in overrides)
                {
                    var o = KitJson.AsObject(item);
                    if (o == null) continue;
                    var ov = new ProbeOverride
                    {
                        Metric = KitJson.GetString(o, "metric"),
                        Body = KitJson.GetString(o, "body"),
                        Reason = KitJson.GetString(o, "reason"),
                        Utc = KitJson.GetString(o, "utc"),
                    };
                    var v = KitJson.Get(o, "value");
                    if (v is bool bv) ov.Bool = bv;
                    else if (KitJson.IsNumber(v)) ov.Number = KitJson.ToDouble(v);
                    else { c.Warnings.Add("an override for " + ov.Metric + " has no usable value"); continue; }
                    if (string.IsNullOrEmpty(ov.Metric)) { c.Warnings.Add("an override has no metric"); continue; }
                    c.Overrides.Add(ov);
                }
            }
            return c;
        }

        /// <summary>Loads <paramref name="path"/> when it exists; otherwise (or on a parse error) returns the defaults
        /// with a warning, so a missing config never crashes a QA run.</summary>
        public static ProbeConfig LoadOrDefaults(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                var d = Defaults();
                if (!string.IsNullOrEmpty(path)) d.Warnings.Add("probes config not found at " + path + "; using kit defaults");
                return d;
            }
            try
            {
                var c = Parse(KitIo.ReadAllText(path));
                c.SourcePath = path;
                return c;
            }
            catch (Exception e)
            {
                var d = Defaults();
                d.Warnings.Add("probes config at " + path + " could not be read (" + e.Message + "); using kit defaults");
                return d;
            }
        }

        public ProbeBody FindBody(string bodyId)
        {
            if (string.IsNullOrEmpty(bodyId)) return null;
            foreach (var b in Bodies) if (b.Id == bodyId) return b;
            return null;
        }

        /// <summary>Scale for a body id (bodies[].height_m), or a body class name; 1 when unknown.</summary>
        public double K(string bodyOrClass)
        {
            if (string.IsNullOrEmpty(bodyOrClass)) return 1;
            var b = FindBody(bodyOrClass);
            if (b != null && b.HeightM > 0) return b.HeightM / ReferenceHeightM;
            if (b != null && b.BodyClass != null && BodyClassHeights.TryGetValue(b.BodyClass, out double ch) && ch > 0)
                return ch / ReferenceHeightM;
            if (BodyClassHeights.TryGetValue(bodyOrClass, out double h) && h > 0) return h / ReferenceHeightM;
            return 1;
        }

        public double KForHeight(double heightM) => heightM > 0 ? heightM / ReferenceHeightM : 1;

        /// <summary>The effective (unscaled) threshold for a metric after overrides: global overrides first, then
        /// overrides for this body, each later one winning.</summary>
        public ProbeThreshold Resolve(string metric, string bodyId = null)
        {
            if (metric == null || !Thresholds.TryGetValue(metric, out var baseT)) return null;
            var t = baseT.Clone();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var ov in Overrides)
                {
                    if (ov.Metric != metric) continue;
                    bool bodySpecific = !string.IsNullOrEmpty(ov.Body);
                    if (pass == 0 && bodySpecific) continue;
                    if (pass == 1 && (!bodySpecific || ov.Body != bodyId)) continue;
                    if (ov.Bool.HasValue) t.Bool = ov.Bool;
                    else { t.Number = ov.Number; t.Bool = null; }
                }
            }
            return t;
        }

        /// <summary>Evaluates a numeric measurement.</summary>
        public ProbeResult Evaluate(string probe, string metric, double value, string bodyId = null, string clip = null)
        {
            var r = new ProbeResult { Probe = probe, Metric = metric, Value = value, Body = bodyId, Clip = clip };
            var t = Resolve(metric, bodyId);
            if (t == null)
            {
                r.Unit = "unitless";
                r.Verdict = ProbeVerdict.ROUTE;
                return r;
            }
            r.Unit = t.Unit;
            r.ThresholdOp = t.Op;
            if (t.Bool.HasValue)
            {
                r.ThresholdBool = t.Bool;
                r.Verdict = Fail(t);
                return r;
            }
            double limit = t.Number;
            if (t.ScalesWithBody)
            {
                double k = K(bodyId);
                r.K = Math.Round(k, 4);
                limit *= k;
            }
            r.ThresholdNumber = Math.Round(limit, 4);
            bool pass = !double.IsNaN(value) && !double.IsInfinity(value) && Compare(value, t.Op, limit);
            r.Verdict = pass ? ProbeVerdict.PASS : Fail(t);
            return r;
        }

        /// <summary>Evaluates a boolean measurement (only "==" is meaningful).</summary>
        public ProbeResult Evaluate(string probe, string metric, bool value, string bodyId = null, string clip = null)
        {
            var r = new ProbeResult { Probe = probe, Metric = metric, BoolValue = value, Body = bodyId, Clip = clip };
            var t = Resolve(metric, bodyId);
            if (t == null)
            {
                r.Unit = "bool";
                r.Verdict = ProbeVerdict.ROUTE;
                return r;
            }
            r.Unit = t.Unit;
            r.ThresholdOp = t.Op;
            if (t.Bool.HasValue)
            {
                r.ThresholdBool = t.Bool;
                r.Verdict = value == t.Bool.Value ? ProbeVerdict.PASS : Fail(t);
            }
            else
            {
                r.ThresholdNumber = t.Number;
                r.Verdict = Compare(value ? 1 : 0, t.Op, t.Number) ? ProbeVerdict.PASS : Fail(t);
            }
            return r;
        }

        static ProbeVerdict Fail(ProbeThreshold t) => t.OnFail == "ROUTE" ? ProbeVerdict.ROUTE : ProbeVerdict.FAIL;

        /// <summary>Applies an op with a 1e-9 tolerance so values that print equal to the limit pass "&lt;=".</summary>
        public static bool Compare(double value, string op, double limit)
        {
            const double eps = 1e-9;
            switch (op)
            {
                case "<=": return value <= limit + eps;
                case "<": return value < limit;
                case ">=": return value >= limit - eps;
                case ">": return value > limit;
                case "==": return Math.Abs(value - limit) <= eps;
                case "abs<=": return Math.Abs(value) <= limit + eps;
                default: return false;
            }
        }

        /// <summary>The thresholds as a probes.config.json "thresholds" object (the node tests compare it with the
        /// example config).</summary>
        public Dictionary<string, object> ThresholdsAsJson()
        {
            var o = new Dictionary<string, object>();
            foreach (var kv in Thresholds)
            {
                var t = kv.Value;
                o[kv.Key] = new Dictionary<string, object>
                {
                    { "op", t.Op },
                    { "value", t.Bool.HasValue ? (object)t.Bool.Value : t.Number },
                    { "unit", t.Unit },
                    { "scales_with_body", t.ScalesWithBody },
                    { "on_fail", t.OnFail },
                    { "tag", t.Tag },
                };
            }
            return o;
        }
    }
}
