// Game Studio Kit (MIT). World-coherence audits that run in a player or in the editor (ARCHITECTURE §3.12 and the
// critic rows of its threshold table, from WoO owner-found defects): orphan colliders (invisible walls), floating and
// sunk props, collider versus render divergence, placeholders and error shaders, animators without their controller,
// LOD double draw, objects below the terrain, terrain splat sums, navmesh reachability, route clearance and ground holes
// for no-jump traversal, extra cameras rendering too often, and bodies that glide in bind pose while moving.
// Every audit lists its offenders by hierarchy path so a reviewer can find them, and every AuditAllow exemption is
// listed in "allowed" and counted in the ROUTE metric audit_exemptions.
using System;
using System.Collections;
using System.Collections.Generic;
using GameStudioKit.Common;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace GameStudioKit.Qa.Probes
{
    public sealed class WorldAuditOptions
    {
        public float FloatingGapM = 0.05f;
        public float SunkDepthM = 0.01f;
        public float DivergenceM = 0.25f;
        public float BelowTerrainM = 0.01f;
        public bool IncludeInactive;
        public int MaxOffendersPerMetric = 50;

        /// <summary>Colliders the physics queries see (support, route obstacles, ground under the route).</summary>
        public LayerMask QueryMask = Physics.DefaultRaycastLayers;

        /// <summary>Layer names that count as ground for the sunk audit (TerrainColliders always do). Missing layers are ignored.</summary>
        public List<string> GroundLayers = new List<string> { "Ground", "Terrain" };

        /// <summary>Overrides <see cref="GroundLayers"/> when set.</summary>
        public LayerMask? GroundMaskOverride;

        /// <summary>The sunk audit's ground layers.</summary>
        public LayerMask GroundMask => GroundMaskOverride ?? (LayerMask)LayerMask.GetMask(GroundLayers.ToArray());

        /// <summary>A placed unit (floating and sunk audits) is at most this wide or tall; larger ancestors are containers.</summary>
        public float MaxUnitExtentM = 12f;

        /// <summary>Returns the placed unit a transform belongs to, or null to use the default rules. The editor sets it to
        /// the prefab instance root.</summary>
        public Func<Transform, Transform> UnitRootResolver;

        /// <summary>Colliders whose object name starts with one of these are never orphans (triggers, volumes).</summary>
        public List<string> OrphanNameAllowPrefixes = new List<string> { "Trigger", "KillZone", "Bounds", "Volume", "Nav" };

        /// <summary>Consecutive pairs are checked for a complete navmesh path (navmesh_unreachable).</summary>
        public List<Vector3> Waypoints = new List<Vector3>();

        /// <summary>
        /// Expected animator controllers (animator_unexpected_controller, row 28: 21 prefabs kept a stale controller).
        /// Key: an Animator's hierarchy path, or a prefab asset path ending in .prefab (editor, through
        /// <see cref="PrefabPathOf"/>). Value: the controller's asset path (editor, through <see cref="ControllerPathOf"/>)
        /// or its name.
        /// </summary>
        public Dictionary<string, string> ExpectedControllers = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Editor hook: the prefab asset path an Animator's object comes from, or null.</summary>
        public Func<Animator, string> PrefabPathOf;

        /// <summary>Editor hook: a controller's asset path, or null.</summary>
        public Func<RuntimeAnimatorController, string> ControllerPathOf;

        /// <summary>Which static audits run (null = all). Names: orphan_colliders, floating, divergence, placeholders,
        /// animators, lod, terrain, splat, navmesh.</summary>
        public HashSet<string> Only;

        public bool Runs(string audit) => Only == null || Only.Count == 0 || Only.Contains(audit);
    }

    public sealed class WorldAuditReport
    {
        public string Scene;
        public readonly List<ProbeResult> Results = new List<ProbeResult>();
        public readonly Dictionary<string, List<string>> Offenders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public readonly List<Dictionary<string, object>> Cameras = new List<Dictionary<string, object>>();
        public readonly List<string> Notes = new List<string>();

        /// <summary>Every AuditAllow exemption an audit honoured (never silent).</summary>
        public readonly List<AuditExemption> Allowed = new List<AuditExemption>();
        readonly HashSet<string> _allowedKeys = new HashSet<string>(StringComparer.Ordinal);
        internal int MaxOffenders = 50;

        public void AddOffender(string metric, string what)
        {
            if (!Offenders.TryGetValue(metric, out var list)) Offenders[metric] = list = new List<string>();
            if (list.Count < MaxOffenders) list.Add(what);
            else if (list.Count == MaxOffenders) list.Add("…");
        }

        /// <summary>Records one exemption (once per path and flag).</summary>
        public void Exempt(string path, AuditException flag, string why)
        {
            string key = path + "|" + flag;
            if (!_allowedKeys.Add(key)) return;
            Allowed.Add(new AuditExemption { Path = path, Flag = flag.ToString(), Why = why });
        }

        public void Exempt(AuditExemption e)
        {
            if (e == null) return;
            if (_allowedKeys.Add(e.Path + "|" + e.Flag)) Allowed.Add(e);
        }

        /// <summary>The exec result shape {schema:1, scene, results:[probeResult], offenders, allowed, cameras, notes}
        /// (CONTRACTS §7.9; "allowed" is additive).</summary>
        public Dictionary<string, object> ToJsonObject()
        {
            var allowed = new List<object>();
            foreach (var a in Allowed) allowed.Add(a.ToJsonObject());
            return new Dictionary<string, object>
            {
                { "schema", 1 },
                { "scene", Scene },
                { "results", Results },
                { "offenders", Offenders },
                { "allowed", allowed },
                { "cameras", Cameras },
                { "notes", Notes },
            };
        }
    }

    public sealed class RouteClearanceResult
    {
        public int Obstacles;
        public float MinJumpableMarginM = float.PositiveInfinity;
        public float MinBlockingMarginM = float.PositiveInfinity;

        /// <summary>Route sample points checked for ground, and those with none within step height + margin.</summary>
        public int GroundSamples;
        public int GroundHoles;
        public readonly List<string> Offenders = new List<string>();
        public readonly List<string> HoleOffenders = new List<string>();
        public readonly List<AuditExemption> Allowed = new List<AuditExemption>();
    }

    public static class WorldAudit
    {
        const string Probe = "WorldAudit";

        /// <summary>Extra depth below the step height at which a route sample still finds its ground.</summary>
        public const float RouteHoleMarginM = 0.25f;

        /// <summary>Runs the static audits (no frames needed) over one loaded scene.</summary>
        public static WorldAuditReport RunStatic(Scene scene, ProbeConfig config, WorldAuditOptions options = null)
        {
            config = config ?? ProbeConfig.Defaults();
            options = options ?? new WorldAuditOptions();
            var report = new WorldAuditReport { Scene = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path, MaxOffenders = options.MaxOffendersPerMetric };
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Notes.Add("scene is not loaded");
                return report;
            }
            if (options.Runs("orphan_colliders")) Count(report, config, "orphan_colliders", OrphanColliders(scene, options, report));
            if (options.Runs("floating")) FloatingAndSunk(scene, options, report, config);
            if (options.Runs("divergence")) Count(report, config, "collider_render_divergence", Divergence(scene, options, report));
            if (options.Runs("placeholders")) Count(report, config, "placeholder_objects", Placeholders(scene, options, report));
            if (options.Runs("animators")) AnimatorControllers(scene, options, report, config);
            if (options.Runs("lod")) Count(report, config, "lod_double_draw", LodDoubleDraw(scene, options, report));
            if (options.Runs("terrain")) Count(report, config, "below_terrain_objects", BelowTerrain(scene, options, report));
            if (options.Runs("splat")) TerrainSplat(scene, options, report, config);
            if (options.Runs("navmesh") && options.Waypoints.Count >= 2) Count(report, config, "navmesh_unreachable", NavmeshUnreachable(options, report));
            UpdateExemptionResult(report, config);
            return report;
        }

        static void Count(WorldAuditReport report, ProbeConfig config, string metric, int count) =>
            report.Results.Add(config.Evaluate(Probe, metric, count));

        /// <summary>(Re)computes the audit_exemptions result from report.Allowed (ROUTE: the reviewer sees every exemption).</summary>
        public static void UpdateExemptionResult(WorldAuditReport report, ProbeConfig config)
        {
            config = config ?? ProbeConfig.Defaults();
            report.Results.RemoveAll(r => r.Metric == "audit_exemptions");
            report.Offenders.Remove("audit_exemptions");
            report.Results.Add(config.Evaluate(Probe, "audit_exemptions", report.Allowed.Count));
            foreach (var a in report.Allowed) report.AddOffender("audit_exemptions", a.Path + " (" + a.Flag + "): " + a.Why);
        }

        public static List<T> Collect<T>(Scene scene, bool includeInactive) where T : Component
        {
            var list = new List<T>();
            var buffer = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
            {
                root.GetComponentsInChildren(includeInactive, buffer);
                list.AddRange(buffer);
            }
            return list;
        }

        /// <summary>Hierarchy path "Root/Child/Leaf".</summary>
        public static string PathOf(Transform t)
        {
            if (t == null) return "";
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static bool Active(Component c, WorldAuditOptions o) => o.IncludeInactive || c.gameObject.activeInHierarchy;

        /// <summary>Checks an exemption and records it when it applies.</summary>
        static bool Exempted(WorldAuditReport report, GameObject go, AuditException flag)
        {
            if (!AuditAllow.Allows(go, flag, out string why)) return false;
            report.Exempt(PathOf(go.transform), flag, why);
            return true;
        }

        // ---- Orphan colliders (row 33: an invisible wall) ----
        static int OrphanColliders(Scene scene, WorldAuditOptions o, WorldAuditReport report)
        {
            int n = 0;
            foreach (var col in Collect<Collider>(scene, o.IncludeInactive))
            {
                if (!Active(col, o) || !col.enabled || col.isTrigger) continue;
                if (col is TerrainCollider || col is CharacterController) continue;
                if (NameAllowed(col.name, o.OrphanNameAllowPrefixes)) continue;
                if (HasVisual(col)) continue;
                if (Exempted(report, col.gameObject, AuditException.OrphanCollider)) continue;
                n++;
                report.AddOffender("orphan_colliders", PathOf(col.transform));
            }
            return n;
        }

        static bool NameAllowed(string name, List<string> prefixes)
        {
            foreach (var p in prefixes) if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>A renderer on the collider's object, a child, or up to two parents, whose bounds touch the collider.</summary>
        static bool HasVisual(Collider col)
        {
            var cb = col.bounds;
            cb.Expand(0.2f);
            foreach (var r in col.GetComponentsInChildren<Renderer>())
                if (r.enabled && !(r is ParticleSystemRenderer) && r.bounds.Intersects(cb)) return true;
            var p = col.transform.parent;
            for (int i = 0; i < 2 && p != null; i++, p = p.parent)
                foreach (var r in p.GetComponents<Renderer>())
                    if (r.enabled && r.bounds.Intersects(cb)) return true;
            return false;
        }

        // ---- Floating and sunk props (the owner saw floating props; row 17: 64 groups sunk 31.57 m) ----

        /// <summary>Carried or moving: under an Animator, a CharacterController or a Rigidbody (a held sword, a hat, a
        /// crate that physics places). Such objects are neither audited nor counted as support.</summary>
        public static bool IsCarriedOrDynamic(Transform t)
        {
            if (t == null) return false;
            return t.GetComponentInParent<Animator>(true) != null
                   || t.GetComponentInParent<CharacterController>(true) != null
                   || t.GetComponentInParent<Rigidbody>(true) != null;
        }

        /// <summary>Union of the enabled renderers (particles excluded) under <paramref name="root"/>.</summary>
        public static bool TryRenderBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            if (root == null) return false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>True when a unit rooted at <paramref name="root"/> is prop-sized (fits in MaxUnitExtentM).</summary>
        public static bool UnitFits(Transform root, WorldAuditOptions o)
        {
            if (!TryRenderBounds(root, out var b)) return false;
            return Mathf.Max(b.size.x, b.size.z) <= o.MaxUnitExtentM && b.size.y <= o.MaxUnitExtentM;
        }

        /// <summary>
        /// The placed unit <paramref name="t"/> belongs to: the resolver's answer (editor: the prefab instance root), else
        /// the nearest AuditUnit, else the largest ancestor whose renderers still fit in MaxUnitExtentM.
        /// </summary>
        public static Transform UnitRootOf(Transform t, WorldAuditOptions o, Dictionary<Transform, bool> fitsCache = null)
        {
            if (t == null) return null;
            if (o.UnitRootResolver != null)
            {
                var r = o.UnitRootResolver(t);
                if (r != null && (t == r || t.IsChildOf(r))) return r;
            }
            var marker = t.GetComponentInParent<AuditUnit>(true);
            if (marker != null) return marker.transform;
            var best = t;
            for (var p = t.parent; p != null; p = p.parent)
            {
                bool fits;
                if (fitsCache == null || !fitsCache.TryGetValue(p, out fits))
                {
                    fits = UnitFits(p, o);
                    if (fitsCache != null) fitsCache[p] = fits;
                }
                if (!fits) break;
                best = p;
            }
            return best;
        }

        static bool InUnit(Transform t, Transform unit) => t == unit || t.IsChildOf(unit);

        static void FloatingAndSunk(Scene scene, WorldAuditOptions o, WorldAuditReport report, ProbeConfig config)
        {
            int floating = 0, sunk = 0;
            var units = new Dictionary<Transform, List<Renderer>>();
            var order = new List<Transform>();
            var fitsCache = new Dictionary<Transform, bool>();
            foreach (var r in Collect<MeshRenderer>(scene, o.IncludeInactive))
            {
                if (!Active(r, o) || !r.enabled) continue;
                if (IsCarriedOrDynamic(r.transform)) continue;
                var unit = UnitRootOf(r.transform, o, fitsCache);
                if (!units.TryGetValue(unit, out var list))
                {
                    units[unit] = list = new List<Renderer>();
                    order.Add(unit);
                }
                list.Add(r);
            }

            var overlap = new Collider[256];
            var hits = new RaycastHit[64];
            var surfaces = new List<SurfaceHit>(16);
            int groundMask = o.GroundMask;
            foreach (var unit in order)
            {
                if (!TryRenderBounds(unit, out var b)) continue;
                if (b.size.y < 0.01f && Mathf.Max(b.size.x, b.size.z) > 5f) continue; // ground planes and decals
                string path = PathOf(unit);

                if (!UnitExempted(report, unit, units[unit], AuditException.Floating))
                {
                    if (!Touches(b, unit, o, overlap) && GapBelow(b, unit, o, hits, out float gap) && gap > o.FloatingGapM)
                    {
                        floating++;
                        report.AddOffender("floating_props", path + " gap " + KitJsonWriter.FormatNumber(gap) + " m");
                    }
                }

                if (!UnitExempted(report, unit, units[unit], AuditException.Sunk))
                {
                    if (Buried(b, unit, o, groundMask, hits, surfaces, out float depth))
                    {
                        sunk++;
                        report.AddOffender("sunk_props", path + " depth " + KitJsonWriter.FormatNumber(depth) + " m");
                    }
                }
            }
            report.Results.Add(config.Evaluate(Probe, "floating_props", floating));
            report.Results.Add(config.Evaluate(Probe, "sunk_props", sunk));
        }

        static bool UnitExempted(WorldAuditReport report, Transform unit, List<Renderer> renderers, AuditException flag)
        {
            if (Exempted(report, unit.gameObject, flag)) return true;
            foreach (var r in renderers)
                if (r != null && r.transform != unit && Exempted(report, r.gameObject, flag)) return true;
            return false;
        }

        /// <summary>Anything outside the unit (and not carried or dynamic) within FloatingGapM of its bounds: a floor, a
        /// table, the wall a shelf hangs on, the ceiling a lamp hangs from.</summary>
        static bool Touches(Bounds b, Transform unit, WorldAuditOptions o, Collider[] buffer)
        {
            var half = b.extents + Vector3.one * o.FloatingGapM;
            int n = Physics.OverlapBoxNonAlloc(b.center, half, buffer, Quaternion.identity, o.QueryMask, QueryTriggerInteraction.Ignore);
            if (n >= buffer.Length) return true; // too crowded to tell: never a false floating report
            for (int i = 0; i < n; i++)
            {
                var c = buffer[i];
                if (c == null || InUnit(c.transform, unit) || IsCarriedOrDynamic(c.transform)) continue;
                return true;
            }
            return false;
        }

        /// <summary>Smallest distance from the unit's bottom to a support below it, over the centre and four inset corners
        /// of its footprint. False when nothing is below within 100 m (ground itself, or open sky).</summary>
        static bool GapBelow(Bounds b, Transform unit, WorldAuditOptions o, RaycastHit[] buffer, out float gap)
        {
            gap = float.PositiveInfinity;
            float y = b.min.y + 0.05f;
            float ix = b.extents.x * 0.8f, iz = b.extents.z * 0.8f;
            var c = b.center;
            var points = new[]
            {
                new Vector3(c.x, y, c.z), new Vector3(c.x - ix, y, c.z - iz), new Vector3(c.x + ix, y, c.z - iz),
                new Vector3(c.x - ix, y, c.z + iz), new Vector3(c.x + ix, y, c.z + iz),
            };
            bool any = false;
            foreach (var origin in points)
            {
                int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, 100f, o.QueryMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var h = buffer[i];
                    if (h.collider == null || InUnit(h.collider.transform, unit) || IsCarriedOrDynamic(h.collider.transform)) continue;
                    float g = b.min.y - h.point.y;
                    if (g < gap) gap = g;
                    any = true;
                }
            }
            if (!any) return false;
            gap = Mathf.Max(0f, gap);
            return true;
        }

        /// <summary>Ground (terrain or a ground layer) that encloses the unit's top: the unit is buried. Roofs, tables and
        /// bridges above it never count (ProbeMath.TryBuryingSurface).</summary>
        static bool Buried(Bounds b, Transform unit, WorldAuditOptions o, int groundMask, RaycastHit[] buffer, List<SurfaceHit> surfaces, out float depth)
        {
            depth = 0f;
            var origin = new Vector3(b.center.x, b.max.y + 50f, b.center.z);
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, 50f + b.size.y + 1f, o.QueryMask, QueryTriggerInteraction.Ignore);
            surfaces.Clear();
            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                var col = h.collider;
                if (col == null || InUnit(col.transform, unit)) continue;
                bool terrain = col is TerrainCollider;
                bool ground = terrain || (groundMask & (1 << col.gameObject.layer)) != 0;
                surfaces.Add(new SurfaceHit(h.point.y, h.normal.y, col.bounds.min.y, ground, terrain));
            }
            if (!ProbeMath.TryBuryingSurface(surfaces, b.max.y, o.SunkDepthM, out float surfaceY)) return false;
            depth = surfaceY - b.max.y;
            return true;
        }

        /// <summary>Nearest hit straight down from <paramref name="origin"/> that is not part of <paramref name="self"/>.</summary>
        public static bool FirstHitBelow(Vector3 origin, float distance, Transform self, LayerMask mask, RaycastHit[] buffer, out float y)
        {
            y = 0f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, distance, mask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var t = buffer[i].collider.transform;
                if (self != null && (t.IsChildOf(self) || self.IsChildOf(t))) continue;
                if (buffer[i].distance < best)
                {
                    best = buffer[i].distance;
                    y = buffer[i].point.y;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        // ---- Collider versus render bounds ----
        static int Divergence(Scene scene, WorldAuditOptions o, WorldAuditReport report)
        {
            int n = 0;
            foreach (var col in Collect<Collider>(scene, o.IncludeInactive))
            {
                if (!Active(col, o) || !col.enabled || col.isTrigger || col is TerrainCollider || col is CharacterController) continue;
                var r = col.GetComponent<Renderer>();
                if (r == null || r is SkinnedMeshRenderer || !r.enabled) continue;
                var a = col.bounds;
                var b = r.bounds;
                float d = Mathf.Max(MaxAbs(a.min - b.min), MaxAbs(a.max - b.max));
                if (d <= o.DivergenceM) continue;
                if (Exempted(report, col.gameObject, AuditException.ColliderRenderDivergence)) continue;
                n++;
                report.AddOffender("collider_render_divergence", PathOf(col.transform) + " " + KitJsonWriter.FormatNumber(d) + " m");
            }
            return n;
        }

        static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        // ---- Placeholders and error shaders ----
        static readonly System.Text.RegularExpressions.Regex PlaceholderName = new System.Text.RegularExpressions.Regex(
            @"(^|[_\s\-.])(placeholder|todo|dummy|temp|tmp|fixme)([_\s\-.(]|$)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        public static bool IsPlaceholderName(string name) => !string.IsNullOrEmpty(name) && PlaceholderName.IsMatch(name);

        static int Placeholders(Scene scene, WorldAuditOptions o, WorldAuditReport report)
        {
            int n = 0;
            foreach (var r in Collect<Renderer>(scene, o.IncludeInactive))
            {
                if (!Active(r, o) || !r.enabled || r is ParticleSystemRenderer) continue;
                string why = null;
                if (IsPlaceholderName(r.name)) why = "placeholder name";
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { why = "missing material (renders magenta)"; break; }
                    if (m.shader == null || m.shader.name == "Hidden/InternalErrorShader") { why = "error shader on " + m.name; break; }
                    if (m.name == "Default-Material" || m.name == "Lit" || m.name == "Default-Diffuse") { why = "default material " + m.name; break; }
                }
                if (why == null) continue;
                if (Exempted(report, r.gameObject, AuditException.Placeholder)) continue;
                n++;
                report.AddOffender("placeholder_objects", PathOf(r.transform) + ": " + why);
            }
            return n;
        }

        // ---- Animators without their controller (row 28: 21 prefabs kept a stale controller) ----
        static void AnimatorControllers(Scene scene, WorldAuditOptions o, WorldAuditReport report, ProbeConfig config)
        {
            int missing = 0, unexpected = 0;
            bool checkExpected = o.ExpectedControllers != null && o.ExpectedControllers.Count > 0;
            foreach (var a in Collect<Animator>(scene, o.IncludeInactive))
            {
                if (!Active(a, o) || !a.enabled) continue;
                string path = PathOf(a.transform);
                var controller = a.runtimeAnimatorController;
                if (controller == null)
                {
                    if (Exempted(report, a.gameObject, AuditException.AnimatorWithoutController)) continue;
                    missing++;
                    report.AddOffender("animator_missing_controller", path);
                    continue;
                }
                if (!checkExpected || !TryExpectedController(a, path, o, out string expected)) continue;
                if (ControllerMatches(controller, expected, o)) continue;
                if (Exempted(report, a.gameObject, AuditException.AnimatorWithoutController)) continue;
                unexpected++;
                report.AddOffender("animator_unexpected_controller", path + ": has " + Describe(controller, o) + ", expected " + expected);
            }
            report.Results.Add(config.Evaluate(Probe, "animator_missing_controller", missing));
            if (checkExpected) report.Results.Add(config.Evaluate(Probe, "animator_unexpected_controller", unexpected));
        }

        static bool TryExpectedController(Animator a, string path, WorldAuditOptions o, out string expected)
        {
            if (o.ExpectedControllers.TryGetValue(path, out expected)) return true;
            string prefab = o.PrefabPathOf?.Invoke(a);
            if (!string.IsNullOrEmpty(prefab) && o.ExpectedControllers.TryGetValue(prefab, out expected)) return true;
            expected = null;
            return false;
        }

        /// <summary>The controller (or, for an override controller, its base) matches by asset path or by name.</summary>
        static bool ControllerMatches(RuntimeAnimatorController controller, string expected, WorldAuditOptions o)
        {
            if (string.IsNullOrEmpty(expected)) return true;
            string expectedName = System.IO.Path.GetFileNameWithoutExtension(expected.Replace('\\', '/'));
            for (var c = controller; c != null; c = (c as AnimatorOverrideController)?.runtimeAnimatorController)
            {
                if (c.name == expected || c.name == expectedName) return true;
                string p = o.ControllerPathOf?.Invoke(c);
                if (!string.IsNullOrEmpty(p) && string.Equals(p.Replace('\\', '/'), expected.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)) return true;
                if (!(c is AnimatorOverrideController)) break;
            }
            return false;
        }

        static string Describe(RuntimeAnimatorController c, WorldAuditOptions o)
        {
            string p = o.ControllerPathOf?.Invoke(c);
            return string.IsNullOrEmpty(p) ? c.name : p;
        }

        // ---- LOD double draw ----
        static int LodDoubleDraw(Scene scene, WorldAuditOptions o, WorldAuditReport report)
        {
            int n = 0;
            foreach (var group in Collect<LODGroup>(scene, o.IncludeInactive))
            {
                if (!Active(group, o) || !group.enabled) continue;
                var seen = new HashSet<Renderer>();
                var problems = new List<string>();
                foreach (var lod in group.GetLODs())
                {
                    if (lod.renderers == null) continue;
                    foreach (var r in lod.renderers)
                    {
                        if (r == null) continue;
                        if (!seen.Add(r)) problems.Add(PathOf(r.transform) + " is in two LODs");
                    }
                }
                foreach (var r in group.GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled && !(r is ParticleSystemRenderer) && !seen.Contains(r))
                        problems.Add(PathOf(r.transform) + " is under a LODGroup but in no LOD (always drawn)");
                }
                if (problems.Count == 0) continue;
                if (Exempted(report, group.gameObject, AuditException.LodDoubleDraw)) continue;
                n++;
                foreach (var p in problems) report.AddOffender("lod_double_draw", p);
            }
            return n;
        }

        // ---- Objects below the terrain ----
        static int BelowTerrain(Scene scene, WorldAuditOptions o, WorldAuditReport report)
        {
            var terrains = Collect<Terrain>(scene, false);
            if (terrains.Count == 0) return 0;
            int n = 0;
            foreach (var r in Collect<Renderer>(scene, o.IncludeInactive))
            {
                if (!Active(r, o) || !r.enabled || r is ParticleSystemRenderer) continue;
                var b = r.bounds;
                foreach (var t in terrains)
                {
                    if (t.terrainData == null) continue;
                    var tp = t.transform.position;
                    var size = t.terrainData.size;
                    if (b.center.x < tp.x || b.center.x > tp.x + size.x || b.center.z < tp.z || b.center.z > tp.z + size.z) continue;
                    float h = t.SampleHeight(b.center) + tp.y;
                    if (b.max.y < h - o.BelowTerrainM && !Exempted(report, r.gameObject, AuditException.BelowTerrain))
                    {
                        n++;
                        report.AddOffender("below_terrain_objects", PathOf(r.transform) + " " + KitJsonWriter.FormatNumber(h - b.max.y) + " m below");
                    }
                    break;
                }
            }
            return n;
        }

        // ---- Terrain splat weights (row 13: 52,734 texels summed to 0) ----
        static void TerrainSplat(Scene scene, WorldAuditOptions o, WorldAuditReport report, ProbeConfig config)
        {
            float worst = 0f;
            bool any = false;
            foreach (var t in Collect<Terrain>(scene, false))
            {
                var td = t.terrainData;
                if (td == null || td.alphamapLayers < 1) continue;
                any = true;
                int w = td.alphamapWidth, h = td.alphamapHeight, layers = td.alphamapLayers;
                const int tile = 256;
                int bad = 0;
                for (int y = 0; y < h; y += tile)
                {
                    for (int x = 0; x < w; x += tile)
                    {
                        int tw = Mathf.Min(tile, w - x), th = Mathf.Min(tile, h - y);
                        var maps = td.GetAlphamaps(x, y, tw, th);
                        for (int j = 0; j < th; j++)
                        {
                            for (int i = 0; i < tw; i++)
                            {
                                float sum = 0f;
                                for (int l = 0; l < layers; l++) sum += maps[j, i, l];
                                float err = Mathf.Abs(sum - 1f);
                                if (float.IsNaN(sum)) err = 1f;
                                if (err > 0.01f) bad++;
                                if (err > worst) worst = err;
                            }
                        }
                    }
                }
                if (bad > 0) report.AddOffender("bake_splat_sum_error", PathOf(t.transform) + ": " + bad + " texels off by > 0.01");
            }
            if (any) report.Results.Add(config.Evaluate(Probe, "bake_splat_sum_error", worst));
        }

        // ---- Navmesh reachability ----
        static int NavmeshUnreachable(WorldAuditOptions o, WorldAuditReport report)
        {
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0)
            {
                report.Notes.Add("no navmesh data: every waypoint pair counts as unreachable");
                return o.Waypoints.Count - 1;
            }
            int n = 0;
            var path = new NavMeshPath();
            for (int i = 0; i + 1 < o.Waypoints.Count; i++)
            {
                bool ok = NavMesh.SamplePosition(o.Waypoints[i], out var a, 1f, NavMesh.AllAreas)
                          && NavMesh.SamplePosition(o.Waypoints[i + 1], out var b, 1f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
                          && path.status == NavMeshPathStatus.PathComplete;
                if (ok) continue;
                n++;
                report.AddOffender("navmesh_unreachable", "waypoint " + i + " → " + (i + 1));
            }
            return n;
        }

        /// <summary>
        /// No-jump traversal check along a route (rows 20 and 22): a capsule is swept between consecutive points with
        /// its bottom raised by the step height. Every obstacle it meets is either jumpable (height ≤ jump apex; margin
        /// = apex − height, want ≥ 0.1 m) or blocking (margin = height − apex, want ≥ 0.2 m, or an intended wall).
        /// The route is also sampled every <paramref name="radius"/> for ground: a point with nothing below it within
        /// step height + RouteHoleMarginM is a hole (route_ground_holes).
        /// </summary>
        public static RouteClearanceResult RouteClearance(IList<Vector3> route, float radius, float height, float stepHeight,
            float jumpApex, LayerMask mask)
        {
            var result = new RouteClearanceResult();
            if (route == null || route.Count < 2) return result;
            var hits = new RaycastHit[32];
            var down = new RaycastHit[16];
            var seen = new HashSet<Collider>();
            for (int i = 0; i + 1 < route.Count; i++)
            {
                Vector3 p0 = route[i], p1 = route[i + 1];
                Vector3 d = p1 - p0;
                float len = d.magnitude;
                if (len < 1e-3f) continue;
                d /= len;
                Vector3 bottom = p0 + Vector3.up * (radius + stepHeight);
                Vector3 top = p0 + Vector3.up * Mathf.Max(radius + stepHeight, height - radius);
                int n = Physics.CapsuleCastNonAlloc(bottom, top, radius, d, hits, len, mask, QueryTriggerInteraction.Ignore);
                for (int h = 0; h < n; h++)
                {
                    var col = hits[h].collider;
                    if (col == null || hits[h].distance <= 0f || !seen.Add(col)) continue;
                    result.Obstacles++;
                    Vector3 at = hits[h].point;
                    float groundY = FirstHitBelow(new Vector3(at.x, p0.y + 0.5f, at.z) - d * (radius + 0.05f), 5f, col.transform, mask, down, out float g) ? g : p0.y;
                    float obstacle = col.bounds.max.y - groundY;
                    if (obstacle <= jumpApex)
                    {
                        float margin = jumpApex - obstacle;
                        result.MinJumpableMarginM = Mathf.Min(result.MinJumpableMarginM, margin);
                        if (margin < 0.1f) result.Offenders.Add(PathOf(col.transform) + " jumpable with only " + KitJsonWriter.FormatNumber(margin) + " m to spare");
                    }
                    else if (AuditAllow.Allows(col.gameObject, AuditException.IntendedWall, out string why))
                    {
                        result.Allowed.Add(new AuditExemption { Path = PathOf(col.transform), Flag = AuditException.IntendedWall.ToString(), Why = why });
                    }
                    else
                    {
                        float margin = obstacle - jumpApex;
                        result.MinBlockingMarginM = Mathf.Min(result.MinBlockingMarginM, margin);
                        if (margin < 0.2f) result.Offenders.Add(PathOf(col.transform) + " blocks the route but is only " + KitJsonWriter.FormatNumber(margin) + " m above the jump apex");
                    }
                }

                // Ground under the route.
                float stepLen = Mathf.Max(0.05f, radius);
                int samples = Mathf.Max(1, Mathf.CeilToInt(len / stepLen));
                bool last = i + 2 == route.Count;
                for (int s = 0; s < samples + (last ? 1 : 0); s++)
                {
                    Vector3 pt = s >= samples ? p1 : p0 + d * (s * stepLen);
                    result.GroundSamples++;
                    if (HasGround(pt, stepHeight, mask, down)) continue;
                    result.GroundHoles++;
                    if (result.HoleOffenders.Count < 50)
                        result.HoleOffenders.Add("no ground within " + KitJsonWriter.FormatNumber(stepHeight + RouteHoleMarginM) + " m below (" +
                                                 KitJsonWriter.FormatNumber(pt.x) + ", " + KitJsonWriter.FormatNumber(pt.y) + ", " + KitJsonWriter.FormatNumber(pt.z) + ")");
                }
            }
            return result;
        }

        /// <summary>Ground within step height above and step height + margin below a route point.</summary>
        static bool HasGround(Vector3 point, float stepHeight, LayerMask mask, RaycastHit[] buffer)
        {
            var origin = point + Vector3.up * stepHeight;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, 2f * stepHeight + RouteHoleMarginM, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (buffer[i].collider != null && !IsCarriedOrDynamic(buffer[i].collider.transform)) return true;
            return false;
        }

        /// <summary>Probe results for a route clearance (margins only for metrics that met an obstacle).</summary>
        public static List<ProbeResult> Evaluate(RouteClearanceResult r, ProbeConfig config)
        {
            config = config ?? ProbeConfig.Defaults();
            var list = new List<ProbeResult>();
            if (!float.IsPositiveInfinity(r.MinJumpableMarginM)) list.Add(config.Evaluate(Probe, "jumpable_obstacle_margin_m", r.MinJumpableMarginM));
            if (!float.IsPositiveInfinity(r.MinBlockingMarginM)) list.Add(config.Evaluate(Probe, "blocking_collider_margin_m", r.MinBlockingMarginM));
            if (r.GroundSamples > 0) list.Add(config.Evaluate(Probe, "route_ground_holes", r.GroundHoles));
            return list;
        }

        /// <summary>
        /// Live audits over <paramref name="frames"/> frames: how often each camera renders (row 12: a minimap camera
        /// took 84 → 35 FPS) and whether moving animated bodies actually animate (bind pose while moving; row 28).
        /// </summary>
        public static IEnumerator RunLive(Scene scene, ProbeConfig config, WorldAuditReport into, int frames = 120, WorldAuditOptions options = null)
        {
            config = config ?? ProbeConfig.Defaults();
            options = options ?? new WorldAuditOptions();
            frames = Mathf.Max(8, frames);
            var renders = new Dictionary<Camera, int>();
            void OnSrp(ScriptableRenderContext ctx, Camera cam) => Bump(renders, cam);
            void OnBuiltin(Camera cam) => Bump(renders, cam);

            var watched = new List<AnimatorWatch>();
            foreach (var a in Collect<Animator>(scene, false))
                if (a.enabled && a.gameObject.activeInHierarchy) watched.Add(new AnimatorWatch(a));

            RenderPipelineManager.beginCameraRendering += OnSrp;
            Camera.onPreRender += OnBuiltin;
            float start = Time.time;
            try
            {
                for (int f = 0; f < frames; f++)
                {
                    yield return null;
                    foreach (var w in watched) w.Sample(Time.time - start);
                }
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= OnSrp;
                Camera.onPreRender -= OnBuiltin;
            }

            // Cameras.
            float minInterval = frames;
            foreach (var cam in Camera.allCameras)
            {
                renders.TryGetValue(cam, out int count);
                bool extra = cam.targetTexture != null;
                if (extra && AuditAllow.Allows(cam.gameObject, AuditException.ExtraCamera, out string why))
                {
                    into.Exempt(PathOf(cam.transform), AuditException.ExtraCamera, why);
                    extra = false;
                }
                float interval = count > 0 ? frames / (float)count : float.PositiveInfinity;
                into.Cameras.Add(new Dictionary<string, object>
                {
                    { "camera", PathOf(cam.transform) },
                    { "renders", count },
                    { "frames", frames },
                    { "target_texture", cam.targetTexture != null ? cam.targetTexture.name : null },
                    { "extra", extra },
                });
                if (extra && count > 0 && interval < minInterval)
                {
                    minInterval = interval;
                    if (interval < 4f) into.AddOffender("extra_world_camera_min_interval_frames", PathOf(cam.transform) + " renders every " + KitJsonWriter.FormatNumber(interval) + " frames");
                }
            }
            into.Results.Add(config.Evaluate(Probe, "extra_world_camera_min_interval_frames", minInterval));

            // Animated bodies that move but never change pose.
            int frozen = 0;
            float worstLatency = 0f;
            float window = Time.time - start;
            foreach (var w in watched)
            {
                if (!w.Moved) continue;
                if (!w.Alive)
                {
                    frozen++;
                    into.AddOffender("bind_pose_while_moving", PathOf(w.Animator != null ? w.Animator.transform : null));
                    worstLatency = Mathf.Max(worstLatency, window);
                }
                else worstLatency = Mathf.Max(worstLatency, w.Latency);
            }
            into.Results.Add(config.Evaluate(Probe, "bind_pose_while_moving", frozen));
            if (watched.Exists(w => w.Moved)) into.Results.Add(config.Evaluate(Probe, "bone_motion_latency_s", worstLatency));
            UpdateExemptionResult(into, config);
        }

        static void Bump(Dictionary<Camera, int> renders, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game) return;
            renders.TryGetValue(cam, out int n);
            renders[cam] = n + 1;
        }

        sealed class AnimatorWatch
        {
            public readonly Animator Animator;
            readonly Transform[] _bones;
            readonly Quaternion[] _start;
            readonly Vector3 _startPos;
            public bool Moved;
            public bool Alive;
            public float Latency;
            float _movedAt = -1f;

            public AnimatorWatch(Animator a)
            {
                Animator = a;
                _startPos = a.transform.position;
                var list = new List<Transform>();
                foreach (var smr in a.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.bones == null) continue;
                    foreach (var b in smr.bones)
                    {
                        if (b != null && !list.Contains(b)) list.Add(b);
                        if (list.Count >= 32) break;
                    }
                    if (list.Count >= 32) break;
                }
                _bones = list.ToArray();
                _start = new Quaternion[_bones.Length];
                for (int i = 0; i < _bones.Length; i++) _start[i] = _bones[i].localRotation;
            }

            public void Sample(float t)
            {
                if (Animator == null || _bones.Length == 0) return;
                if (!Moved && BodyBones.Planar(Animator.transform.position, _startPos) > 0.2f)
                {
                    Moved = true;
                    _movedAt = t;
                }
                if (Alive) return;
                for (int i = 0; i < _bones.Length; i++)
                {
                    if (_bones[i] == null) continue;
                    if (Quaternion.Angle(_start[i], _bones[i].localRotation) > 0.5f)
                    {
                        Alive = true;
                        Latency = _movedAt >= 0f ? Mathf.Max(0f, t - _movedAt) : 0f;
                        return;
                    }
                }
            }
        }
    }
}
