// Game Studio Kit (MIT). Editor side of the world-coherence audits (CONTRACTS §7.9 exec entry points):
//  - Run: the runtime static audits (WorldAudit) on a scene, plus editor-only checks: texture import downsampling
//    (row 29: a sky imported at 256×128), lightmap NaN/Inf texels and single-texel black holes (row 13), optional
//    route clearance, route ground holes and navmesh waypoints. Result {schema:1, scene, results:[probeResult],
//    offenders, allowed, cameras, notes, verdict}; verdict FAIL when any result is FAIL.
//  - Snapshot / Diff: before/after a bulk tool (row 17): bulk_tool_unintended_move_m, bulk_tool_missing_groups and
//    below_terrain_objects.
//  - TextureAudit: the downsample check over a folder.
// A scene is required: without "scene" the active scene is used only when it is a saved scene with objects (a cold
// batchmode editor has an empty untitled scene, which would pass vacuously). The scene is matched by GUID, so any
// spelling of its path finds an already open scene. Only a scene this call added (or loaded) is closed (or unloaded)
// again, and never one with unsaved changes, so the open multi-scene setup and its edits stay as they were.
// Placed units for the floating and sunk audits are prefab instance roots (outermost, else nearest, if prop-sized).
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using GameStudioKit.Qa;
using GameStudioKit.Qa.Probes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameStudioKit.Editor.Qa
{
    public static class WorldAuditEditor
    {
        public const string AllowDownsampleLabel = "gsk-allow-downsample";

        /// <summary>
        /// Args: {"scene": "Assets/Scenes/Level1.unity" (required unless the active scene is saved and not empty),
        /// "audits": ["orphan_colliders", …, "textures", "lightmaps"], "probes_config": path, "waypoints": [[x,y,z], …],
        /// "route": {"points": [[x,y,z], …], "radius": 0.3, "height": 1.8, "step_height": 0.3, "jump_apex": 1.0},
        /// "expected_controllers": {"&lt;hierarchy path or Assets/….prefab&gt;": "Assets/….controller or a name"},
        /// "ground_layers": ["Ground", "Terrain"], "max_unit_m": 12,
        /// "out": "Docs/qa/runs/&lt;run_id&gt;/world_audit.json" (optional copy)}.
        /// </summary>
        public static void Run()
        {
            var args = KitExec.Args();
            var config = KitExec.LoadProbeConfig(args);
            var options = EditorOptions(args);
            var only = KitJson.GetStringList(args, "audits");
            if (only.Count > 0) options.Only = new HashSet<string>(only);
            foreach (var p in ReadPoints(KitJson.GetList(args, "waypoints"))) options.Waypoints.Add(p);

            var handle = OpenScene(KitJson.GetString(args, "scene"));
            var scene = handle.Scene;
            try
            {
                Physics.SyncTransforms();
                var report = WorldAudit.RunStatic(scene, config, options);
                if (options.Runs("textures")) TextureDownsample(DependencyTextures(scene.path), config, report);
                if (options.Runs("lightmaps")) LightmapTexels(config, report);
                var route = KitJson.GetObject(args, "route");
                if (route != null)
                {
                    var r = WorldAudit.RouteClearance(ReadPoints(KitJson.GetList(route, "points")),
                        (float)KitJson.GetDouble(route, "radius", 0.3), (float)KitJson.GetDouble(route, "height", 1.8),
                        (float)KitJson.GetDouble(route, "step_height", 0.3), (float)KitJson.GetDouble(route, "jump_apex", 1.0), options.QueryMask);
                    report.Results.AddRange(WorldAudit.Evaluate(r, config));
                    foreach (var o in r.Offenders) report.AddOffender("route_clearance", o);
                    foreach (var o in r.HoleOffenders) report.AddOffender("route_ground_holes", o);
                    foreach (var a in r.Allowed) report.Exempt(a);
                    WorldAudit.UpdateExemptionResult(report, config);
                }
                report.Notes.AddRange(handle.Notes);
                var result = report.ToJsonObject();
                result["verdict"] = Verdict(report.Results);
                string outPath = KitExec.ResolvePath(KitJson.GetString(args, "out"));
                if (outPath != null) KitIo.WriteJsonAtomic(outPath, result);
                KitExec.Result(result);
            }
            finally
            {
                handle.Restore();
            }
        }

        /// <summary>Options with the editor hooks: prefab instance roots as placed units, and prefab and controller asset
        /// paths for the expected-controller check.</summary>
        public static WorldAuditOptions EditorOptions(Dictionary<string, object> args)
        {
            var o = new WorldAuditOptions();
            o.MaxUnitExtentM = (float)KitJson.GetDouble(args, "max_unit_m", o.MaxUnitExtentM);
            var layers = KitJson.GetStringList(args, "ground_layers");
            if (layers.Count > 0) o.GroundLayers = layers;
            var expected = KitJson.GetObject(args, "expected_controllers");
            if (expected != null)
            {
                foreach (var kv in expected)
                    if (kv.Value is string v && !string.IsNullOrEmpty(v)) o.ExpectedControllers[KitIo.ToPosix(kv.Key)] = KitIo.ToPosix(v);
            }
            o.UnitRootResolver = t =>
            {
                var outer = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (outer != null && WorldAudit.UnitFits(outer.transform, o)) return outer.transform;
                var nearest = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
                if (nearest != null && WorldAudit.UnitFits(nearest.transform, o)) return nearest.transform;
                return null;
            };
            o.PrefabPathOf = a =>
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(a.gameObject);
                return root == null ? null : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            };
            o.ControllerPathOf = c => c == null ? null : AssetDatabase.GetAssetPath(c);
            return o;
        }

        /// <summary>Args: {"scene": path (required unless the active scene is saved and not empty), "out": "Temp/gsk_snapshot.json"}.
        /// Writes the snapshot file.</summary>
        public static void Snapshot()
        {
            var args = KitExec.Args();
            string outPath = KitExec.ResolvePath(KitJson.GetString(args, "out")) ?? Path.Combine(KitExec.ProjectRoot, "Temp", "gsk_snapshot.json");
            var handle = OpenScene(KitJson.GetString(args, "scene"));
            var scene = handle.Scene;
            try
            {
                var snap = WorldSnapshot.Capture(scene);
                KitIo.WriteJsonAtomic(outPath, snap.ToJsonObject());
                KitExec.Result(new Dictionary<string, object>
                {
                    { "schema", 1 }, { "scene", scene.path }, { "out", KitExec.ToWorkspaceRel(outPath) }, { "entries", snap.Entries.Count },
                    { "notes", new List<string>(handle.Notes) }, { "verdict", "PASS" },
                });
            }
            finally
            {
                handle.Restore();
            }
        }

        /// <summary>Args: {"scene": path, "before": snapshot path, "intended": ["Root/Group"], "threshold_m": 0.5, "probes_config"}.
        /// A group outside the intended prefixes that moved more than threshold_m, or disappeared, fails the diff.</summary>
        public static void Diff()
        {
            var args = KitExec.Args();
            var config = KitExec.LoadProbeConfig(args);
            string beforePath = KitExec.ResolvePath(KitJson.GetString(args, "before")) ?? Path.Combine(KitExec.ProjectRoot, "Temp", "gsk_snapshot.json");
            var before = WorldSnapshot.FromJson(KitIo.ReadAllText(beforePath));
            var handle = OpenScene(KitJson.GetString(args, "scene"));
            var scene = handle.Scene;
            try
            {
                var after = WorldSnapshot.Capture(scene);
                var diff = WorldSnapshot.Diff(before, after, KitJson.GetStringList(args, "intended"), (float)KitJson.GetDouble(args, "threshold_m", 0.5), scene);
                var results = WorldSnapshot.Evaluate(diff, config);
                var allowed = new List<object>();
                foreach (var a in diff.Allowed) allowed.Add(a.ToJsonObject());
                KitExec.Result(new Dictionary<string, object>
                {
                    { "schema", 1 },
                    { "scene", scene.path },
                    { "results", results },
                    { "offenders", new Dictionary<string, object> { { "bulk_tool", diff.Offenders } } },
                    { "allowed", allowed },
                    { "missing", diff.Missing },
                    { "missing_unintended", diff.UnintendedMissing },
                    { "notes", new List<string>(handle.Notes) },
                    { "verdict", Verdict(results) },
                });
            }
            finally
            {
                handle.Restore();
            }
        }

        /// <summary>Args: {"root": "Assets/Art" (default Assets), "probes_config"}.</summary>
        public static void TextureAudit()
        {
            var args = KitExec.Args();
            var config = KitExec.LoadProbeConfig(args);
            string root = KitJson.GetString(args, "root", "Assets");
            var report = new WorldAuditReport { Scene = root };
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            TextureDownsample(paths, config, report);
            var result = report.ToJsonObject();
            result["verdict"] = Verdict(report.Results);
            KitExec.Result(result);
        }

        [MenuItem("Tools/Game Studio Kit/World Audit (active scene)")]
        static void MenuRun()
        {
            var config = ProbeConfig.LoadOrDefaults(Path.Combine(KitExec.WorkspaceRoot, "Docs", "qa", "probes.config.json"));
            Physics.SyncTransforms();
            var report = WorldAudit.RunStatic(SceneManager.GetActiveScene(), config, EditorOptions(new Dictionary<string, object>()));
            TextureDownsample(DependencyTextures(SceneManager.GetActiveScene().path), config, report);
            foreach (var r in report.Results) KitLog.Info("World audit: " + r);
            foreach (var kv in report.Offenders) KitLog.Info("World audit " + kv.Key + ": " + string.Join("; ", kv.Value));
        }

        static string Verdict(List<ProbeResult> results)
        {
            foreach (var r in results) if (r.Verdict == ProbeVerdict.FAIL) return "FAIL";
            return "PASS";
        }

        /// <summary>A scene an entry point works on, and how to put the editor back afterwards.</summary>
        public sealed class SceneHandle
        {
            public Scene Scene;

            /// <summary>This call added the scene to the hierarchy (close and remove it again).</summary>
            public bool Added;

            /// <summary>The scene was in the hierarchy but unloaded, and this call loaded it (unload it again).</summary>
            public bool Loaded;
            public readonly List<string> Notes = new List<string>();

            /// <summary>Closes or unloads only what this call opened, and never a scene with unsaved changes.</summary>
            public void Restore()
            {
                if (!Scene.IsValid() || (!Added && !Loaded)) return;
                if (Scene.isDirty)
                {
                    KitLog.Warn("World audit: " + Scene.path + " has unsaved changes; left open");
                    return;
                }
                EditorSceneManager.CloseScene(Scene, Added);
            }
        }

        /// <summary>
        /// The scene named by <paramref name="path"/> (any spelling; matched by GUID against the scenes in the
        /// hierarchy), or the active scene when it is saved and has objects. Throws ArgumentException otherwise (the exec
        /// envelope then reports ok:false), so a cold editor's empty untitled scene never passes an audit.
        /// </summary>
        public static SceneHandle OpenScene(string path)
        {
            var handle = new SceneHandle();
            if (string.IsNullOrWhiteSpace(path))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid() || string.IsNullOrEmpty(active.path) || active.rootCount == 0)
                {
                    string what = !active.IsValid() || string.IsNullOrEmpty(active.path) ? "untitled" : active.path;
                    if (active.IsValid() && active.rootCount == 0) what += " and empty";
                    throw new ArgumentException("a scene is required: pass {\"scene\": \"Assets/…/Level.unity\"} (the active scene is " + what + ")");
                }
                handle.Scene = active;
                handle.Notes.Add("no scene argument: audited the active scene " + active.path);
                return handle;
            }
            string assetPath = KitIo.ToPosix(path.Trim());
            if (Path.IsPathRooted(assetPath)) assetPath = KitIo.RelativePosix(KitExec.ProjectRoot, assetPath) ?? assetPath;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("no scene asset at " + path);
            string canonical = AssetDatabase.GUIDToAssetPath(guid);
            if (!canonical.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(path + " is not a scene");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(s.path) || AssetDatabase.AssetPathToGUID(s.path) != guid) continue;
                if (!s.isLoaded)
                {
                    s = EditorSceneManager.OpenScene(canonical, OpenSceneMode.Additive);
                    handle.Loaded = true;
                }
                handle.Scene = s;
                return handle;
            }
            handle.Scene = EditorSceneManager.OpenScene(canonical, OpenSceneMode.Additive);
            handle.Added = true;
            return handle;
        }

        static List<Vector3> ReadPoints(List<object> list)
        {
            var points = new List<Vector3>();
            if (list == null) return points;
            foreach (var item in list)
            {
                var p = item as List<object>;
                if (p == null || p.Count != 3) continue;
                points.Add(new Vector3((float)KitJson.ToDouble(p[0]), (float)KitJson.ToDouble(p[1]), (float)KitJson.ToDouble(p[2])));
            }
            return points;
        }

        static List<string> DependencyTextures(string scenePath)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(scenePath)) return list;
            foreach (var dep in AssetDatabase.GetDependencies(scenePath, true))
                if (AssetImporter.GetAtPath(dep) is TextureImporter) list.Add(dep);
            return list;
        }

        /// <summary>texture_import_downsample_factor: source size ÷ imported max size, worst texture.</summary>
        public static void TextureDownsample(IEnumerable<string> texturePaths, ProbeConfig config, WorldAuditReport report)
        {
            double worst = 1;
            bool any = false;
            foreach (var path in texturePaths)
            {
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                any = true;
                ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                int source = Math.Max(w, h);
                int max = ti.maxTextureSize;
                var standalone = ti.GetPlatformTextureSettings("Standalone");
                if (standalone != null && standalone.overridden) max = standalone.maxTextureSize;
                if (source <= 0 || max <= 0 || source <= max) continue;
                double factor = source / (double)max;
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                bool allowed = asset != null && Array.IndexOf(AssetDatabase.GetLabels(asset), AllowDownsampleLabel) >= 0;
                if (allowed)
                {
                    if (factor > 2) report.Exempt(path, AuditException.TextureDownsample, "asset label " + AllowDownsampleLabel);
                    continue;
                }
                worst = Math.Max(worst, factor);
                if (factor > 2) report.AddOffender("texture_import_downsample_factor", path + " " + w + "×" + h + " imported at max " + max);
            }
            if (any) report.Results.Add(config.Evaluate("WorldAuditEditor", "texture_import_downsample_factor", worst));
            if (report.Allowed.Count > 0) WorldAudit.UpdateExemptionResult(report, config);
        }

        /// <summary>
        /// bake_nan_black_texel_pct over the active lighting's lightmaps: NaN or infinite texels, plus black texels whose
        /// four neighbours are all lit (single-texel dropouts). Empty atlas space is black too, so plain black texels are
        /// not counted.
        /// </summary>
        public static void LightmapTexels(ProbeConfig config, WorldAuditReport report)
        {
            var maps = LightmapSettings.lightmaps;
            if (maps == null || maps.Length == 0) return;
            long bad = 0, total = 0;
            for (int i = 0; i < maps.Length; i++)
            {
                var tex = maps[i].lightmapColor;
                if (tex == null) continue;
                CountBadTexels(tex, out long b, out long t);
                bad += b;
                total += t;
                if (b > 0) report.AddOffender("bake_nan_black_texel_pct", tex.name + ": " + b + " bad texels");
            }
            if (total > 0) report.Results.Add(config.Evaluate("WorldAuditEditor", "bake_nan_black_texel_pct", 100.0 * bad / total));
        }

        static void CountBadTexels(Texture2D tex, out long bad, out long total)
        {
            bad = 0;
            total = (long)tex.width * tex.height;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            const int tile = 512;
            var buffer = new Texture2D(tile + 2, tile + 2, TextureFormat.RGBAFloat, false, true);
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                for (int y0 = 0; y0 < tex.height; y0 += tile)
                {
                    for (int x0 = 0; x0 < tex.width; x0 += tile)
                    {
                        // Read the tile with a one-texel border for the neighbour test.
                        int rx = Math.Max(0, x0 - 1), ry = Math.Max(0, y0 - 1);
                        int rw = Math.Min(tex.width, x0 + tile + 1) - rx, rh = Math.Min(tex.height, y0 + tile + 1) - ry;
                        buffer.ReadPixels(new Rect(rx, ry, rw, rh), 0, 0, false);
                        var px = buffer.GetPixels(0, 0, rw, rh);
                        int tw = Math.Min(tile, tex.width - x0), th = Math.Min(tile, tex.height - y0);
                        for (int j = 0; j < th; j++)
                        {
                            for (int i = 0; i < tw; i++)
                            {
                                int lx = x0 + i - rx, ly = y0 + j - ry;
                                var c = px[ly * rw + lx];
                                if (!IsFinite(c)) { bad++; continue; }
                                if (!IsBlack(c)) continue;
                                if (lx <= 0 || ly <= 0 || lx >= rw - 1 || ly >= rh - 1) continue;
                                if (!IsBlack(px[ly * rw + lx - 1]) && !IsBlack(px[ly * rw + lx + 1]) &&
                                    !IsBlack(px[(ly - 1) * rw + lx]) && !IsBlack(px[(ly + 1) * rw + lx])) bad++;
                            }
                        }
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(buffer);
            }
        }

        static bool IsFinite(Color c) =>
            !(float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b));

        static bool IsBlack(Color c) => c.r < 1e-4f && c.g < 1e-4f && c.b < 1e-4f;
    }
}
