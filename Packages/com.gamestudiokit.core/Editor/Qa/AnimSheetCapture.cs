// Game Studio Kit (MIT). Exec entry point GameStudioKit.Editor.Qa.AnimSheetCapture.Run (CONTRACTS §7.9): renders each
// clip on the real body (after retargeting) as a frame sheet — rows front, side, back × N evenly spaced frames, grey
// ground, one light — and measures lean, head tilt, foot-crossing frames and the lowest point per clip. Reviewers get
// motion as sheets, not stills ("posture is measured, not guessed"). Runs in an isolated preview scene, so the open
// scenes are untouched. Needs a graphics device: never run it with -nographics.
// Args: {"asset": "Assets/…/Model.fbx or .prefab", "clips_from": "Assets/…/Clips.fbx" (optional), "clips": ["Walk"],
//        "out": "Docs/qa/sheets/<name>" (default), "views": ["front","side","back"], "frames": 8, "cell": [320, 320]}
// Result: {schema:1, out, frames, sheets:[{clip, path, frames, metrics:{lean_deg_max, head_tilt_deg_max,
//          foot_cross_frames, lowest_point_m}}], verdict}
using System;
using System.Collections.Generic;
using System.IO;
using GameStudioKit.Common;
using GameStudioKit.Qa.Probes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GameStudioKit.Editor.Qa
{
    public static class AnimSheetCapture
    {
        public sealed class ClipMetrics
        {
            public float LeanDegMax;
            public float HeadTiltDegMax;
            public int FootCrossFrames;
            public float LowestPointM = float.PositiveInfinity;
        }

        public static void Run()
        {
            var args = KitExec.Args();
            string asset = KitJson.GetString(args, "asset");
            if (string.IsNullOrEmpty(asset)) throw new ArgumentException("AnimSheetCapture needs {\"asset\": \"Assets/…\"}");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            if (model == null) throw new ArgumentException("no model or prefab at " + asset);

            string clipsFrom = KitJson.GetString(args, "clips_from", asset);
            var wanted = new HashSet<string>(KitJson.GetStringList(args, "clips"));
            var clips = new List<AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(clipsFrom))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__", StringComparison.Ordinal) && (wanted.Count == 0 || wanted.Contains(c.name)))
                    clips.Add(c);

            string name = KitIo.SafeFileName(Path.GetFileNameWithoutExtension(asset));
            string outDir = KitExec.ResolvePath(KitJson.GetString(args, "out")) ?? Path.Combine(KitExec.WorkspaceRoot, "Docs", "qa", "sheets", name);
            var views = KitJson.GetStringList(args, "views");
            if (views.Count == 0) views.AddRange(new[] { "front", "side", "back" });
            int frames = Mathf.Clamp(KitJson.GetInt(args, "frames", 8), 1, 32);
            int cellW = 320, cellH = 320;
            var cell = KitJson.GetList(args, "cell");
            if (cell != null && cell.Count == 2)
            {
                cellW = Mathf.Clamp((int)KitJson.ToDouble(cell[0], 320), 64, 1024);
                cellH = Mathf.Clamp((int)KitJson.ToDouble(cell[1], 320), 64, 1024);
            }

            var sheets = new List<object>();
            int total = 0;
            foreach (var clip in clips)
            {
                string path = Path.Combine(outDir, name + "_" + KitIo.SafeFileName(clip.name) + "_sheet.png");
                var m = CaptureClip(model, clip, path, views, frames, cellW, cellH);
                total += frames;
                var metrics = new Dictionary<string, object>
                {
                    { "lean_deg_max", Math.Round(m.LeanDegMax, 2) },
                    { "head_tilt_deg_max", Math.Round(m.HeadTiltDegMax, 2) },
                    { "foot_cross_frames", m.FootCrossFrames },
                    { "lowest_point_m", float.IsPositiveInfinity(m.LowestPointM) ? null : (object)Math.Round(m.LowestPointM, 4) },
                };
                KitIo.WriteJsonAtomic(Path.ChangeExtension(path, null) + "_metrics.json", metrics);
                sheets.Add(new Dictionary<string, object>
                {
                    { "clip", clip.name },
                    { "path", KitExec.ToWorkspaceRel(path) },
                    { "frames", frames },
                    { "metrics", metrics },
                });
            }

            KitExec.Result(new Dictionary<string, object>
            {
                { "schema", 1 },
                { "out", KitExec.ToWorkspaceRel(outDir) },
                { "frames", total },
                { "sheets", sheets },
                { "verdict", clips.Count > 0 ? "PASS" : "ERROR" },
            });
        }

        /// <summary>Renders one clip's sheet to <paramref name="pngPath"/> and returns its pose metrics.</summary>
        public static ClipMetrics CaptureClip(GameObject model, AnimationClip clip, string pngPath, IList<string> views, int frames, int cellW, int cellH)
        {
            var metrics = new ClipMetrics();
            var scene = EditorSceneManager.NewPreviewScene();
            var graph = default(PlayableGraph);
            Texture2D sheet = null;
            RenderTexture rt = null;
            try
            {
                var instance = (GameObject)Object.Instantiate(model);
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var bones = BodyBones.FromAnimator(animator);
                Vector3 headUpLocal = bones.Head != null ? bones.Head.InverseTransformDirection(Vector3.up) : Vector3.up;

                var lightGo = new GameObject("GSK Sheet Light");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.localScale = Vector3.one * 2f;

                var camGo = new GameObject("GSK Sheet Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.scene = scene;
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.45f, 0.45f, 0.45f, 1f);
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100f;

                graph = PlayableGraph.Create("GSK AnimSheet");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "sheet", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);

                // Frame the body at its first pose.
                playable.SetTime(0);
                graph.Evaluate(0f);
                Bounds bounds = BodyBounds(instance);
                float radius = Mathf.Max(0.25f, Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z)));
                float distance = radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;

                rt = new RenderTexture(cellW, cellH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                sheet = new Texture2D(cellW * frames, cellH * views.Count, TextureFormat.RGB24, false);
                bool loop = clip.isLooping;
                for (int f = 0; f < frames; f++)
                {
                    double t = frames == 1 ? 0 : clip.length * (loop ? f / (double)frames : f / (double)(frames - 1));
                    playable.SetTime(t);
                    graph.Evaluate(0f);
                    Measure(instance, bones, headUpLocal, metrics);
                    Vector3 center = bounds.center;
                    for (int v = 0; v < views.Count; v++)
                    {
                        Vector3 dir = ViewDirection(views[v]);
                        cam.transform.SetPositionAndRotation(center + dir * distance, Quaternion.LookRotation(-dir, Vector3.up));
                        cam.targetTexture = rt;
                        cam.Render();
                        var previous = RenderTexture.active;
                        RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, cellW, cellH), f * cellW, (views.Count - 1 - v) * cellH);
                        RenderTexture.active = previous;
                        cam.targetTexture = null;
                    }
                }
                sheet.Apply(false);
                string dirPath = Path.GetDirectoryName(Path.GetFullPath(pngPath));
                if (!string.IsNullOrEmpty(dirPath)) Directory.CreateDirectory(dirPath);
                File.WriteAllBytes(pngPath, sheet.EncodeToPNG());
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (rt != null) Object.DestroyImmediate(rt);
                if (sheet != null) Object.DestroyImmediate(sheet);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            return metrics;
        }

        static Vector3 ViewDirection(string view)
        {
            switch ((view ?? "").ToLowerInvariant())
            {
                case "side": return Vector3.right;
                case "back": return Vector3.back;
                case "left": return Vector3.left;
                default: return Vector3.forward;
            }
        }

        static Bounds BodyBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void Measure(GameObject instance, BodyBones bones, Vector3 headUpLocal, ClipMetrics m)
        {
            if (bones.HasSpine)
                m.LeanDegMax = Mathf.Max(m.LeanDegMax, Vector3.Angle(bones.Head.position - bones.Hips.position, Vector3.up));
            if (bones.Head != null)
                m.HeadTiltDegMax = Mathf.Max(m.HeadTiltDegMax, Vector3.Angle(bones.Head.TransformDirection(headUpLocal), Vector3.up));
            if (bones.HasFeet)
            {
                float xl = instance.transform.InverseTransformPoint(bones.LeftFoot.position).x;
                float xr = instance.transform.InverseTransformPoint(bones.RightFoot.position).x;
                if (xl >= xr) m.FootCrossFrames++;
            }
            if (Anim.RestPose.LowestPoint(instance.transform, out float lowest)) m.LowestPointM = Mathf.Min(m.LowestPointM, lowest);
        }
    }
}
