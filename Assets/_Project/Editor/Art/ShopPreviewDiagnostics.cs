using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class ShopPreviewDiagnostics
    {
        const string Output = "BuildNotes/experience-refresh/portrait-probe-20261026";
        [Serializable] sealed class Report
        {
            public string utc, unity, graphics, status = "IN PROGRESS", error;
            public List<CaseReport> cases = new List<CaseReport>();
        }
        [Serializable] sealed class CaseReport
        {
            public string mode, image, previewScene;
            public Bounds actualBounds, framingBounds;
            public Matrix4x4 camera;
            public float orthographicSize;
            public Color[] lightColors;
            public float[] lightIntensities;
            public Vector3[] lightEulerAngles;
            public MeshReport[] meshes;
            public NodeReport[] hierarchy;
        }
        [Serializable] sealed class NodeReport
        {
            public string path;
            public Vector3 localPosition, localScale;
            public Quaternion localRotation;
            public Matrix4x4 world;
            public bool active;
        }
        [Serializable] sealed class MeshReport
        {
            public string path, sourceMesh, rootBone;
            public bool enabled, updateWhenOffscreen;
            public int vertices, negativeTangentW, zeroTangentW, invalidTangentW;
            public float minTangentW, maxTangentW;
            public Matrix4x4 world;
            public string[] blendShapes, materials, shaders, keywords;
            public float[] blendWeights;
            public VectorStats normals, tangents;
            public VectorStats[] submeshNormals;
        }
        [Serializable] sealed class VectorStats
        {
            public int count, invalid, zero;
            public float minLength, maxLength;
            public Vector3 mean;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run the portrait probe outside Play Mode.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Output));
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing probe output: " + folder);
            var library = ModelLibrary.Load();
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            if (!library || !library.Find("Avatar/Avatar") || !idle) throw new InvalidOperationException("Original avatar and Idle are required.");
            var outfit = CosmeticBundles.Find("courier")?.Apply(GameConfig.Current.Wardrobe.Default, GameConfig.Current.Wardrobe);
            if (outfit == null) throw new InvalidOperationException("Courier outfit is unavailable.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName };
            Directory.CreateDirectory(folder);
            var framing = new Bounds();
            try
            {
                foreach (string mode in new[] { "A-sample", "B-playable", "C-baked" })
                    report.cases.Add(Capture(mode, folder, idle, outfit, ref framing));
                report.status = "CAPTURED; diagnosis and visual acceptance pending";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); throw; }
            finally { WriteNew(Path.Combine(folder, "diagnostics.json"), System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true))); }
            Debug.Log("SHOP_PORTRAIT_PROBE: " + folder + "; cases=" + report.cases.Count);
        }

        static CaseReport Capture(string mode, string folder, AnimationClip idle, Outfit outfit, ref Bounds framing)
        {
            var preview = new PreviewRenderUtility();
            var ownedMeshes = new List<Mesh>();
            var staticMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
            Texture2D image = null;
            PlayableGraph graph = default;
            try
            {
                var holder = new GameObject("Courier " + mode);
                preview.AddSingleGO(holder);
                var model = ModelVisual.Spawn("Avatar/Avatar", holder.transform);
                if (!model) throw new InvalidOperationException("Original avatar spawn failed.");
                if (mode != "B-playable") idle.SampleAnimation(model, 0);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                PlayerAppearance.Dress(skins, outfit);
                holder.transform.rotation = Quaternion.Euler(0, 160, 0);
                if (mode == "B-playable")
                {
                    var animator = model.GetComponentInChildren<Animator>(true);
                    if (!animator) throw new InvalidOperationException("Original Animator missing.");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    graph = PlayableGraph.Create("Shop portrait diagnostic");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip = AnimationClipPlayable.Create(graph, idle);
                    clip.SetApplyFootIK(false);
                    clip.SetTime(0);
                    AnimationPlayableOutput.Create(graph, "Idle", animator).SetSourcePlayable(clip);
                    graph.Play(); graph.Evaluate(0);
                }
                var visible = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff).ToArray();
                if (visible.Length == 0) throw new InvalidOperationException("Courier has no visible renderers.");
                var bounds = visible[0].bounds;
                foreach (var renderer in visible.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (mode == "A-sample") framing = bounds;
                if (mode == "C-baked") foreach (var skin in skins.Where(s => s.enabled && !s.forceRenderingOff)) staticMeshes.Add(skin, BakeStatic(skin, ownedMeshes));
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(framing.extents.y, framing.extents.x / .8f) * 1.12f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.97f, .94f, .85f, 0);
                camera.allowHDR = false;
                camera.transform.position = framing.center + Vector3.back * 5;
                camera.transform.LookAt(framing.center);
                preview.lights[0].intensity = 1.15f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = .65f;
                preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 0);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.BeginStaticPreview(new Rect(0, 0, 256, 320));
                preview.Render(true);
                image = preview.EndStaticPreview();
                if (!image || image.width != 256 || image.height != 320) throw new InvalidOperationException("Invalid diagnostic image.");
                WriteNew(Path.Combine(folder, mode + ".png"), image.EncodeToPNG());
                return new CaseReport { mode = mode, image = mode + ".png", previewScene = model.scene.name,
                    actualBounds = bounds, framingBounds = framing, camera = camera.transform.localToWorldMatrix,
                    orthographicSize = camera.orthographicSize,
                    lightColors = preview.lights.Select(l => l.color).ToArray(), lightIntensities = preview.lights.Select(l => l.intensity).ToArray(),
                    lightEulerAngles = preview.lights.Select(l => l.transform.eulerAngles).ToArray(),
                    meshes = skins.Where(s => s.name == "SK_Head" || s.name == "SK_Hoodie")
                        .Select(s => Describe(s, holder.transform, ownedMeshes, staticMeshes.TryGetValue(s, out var baked) ? baked : null)).ToArray(),
                    hierarchy = model.scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Transform>(true)).Select(t => new NodeReport {
                        path = t.root.name + "/" + AnimationUtility.CalculateTransformPath(t, t.root), localPosition = t.localPosition,
                        localRotation = t.localRotation, localScale = t.localScale, world = t.localToWorldMatrix, active = t.gameObject.activeInHierarchy }).ToArray() };
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (image) Object.DestroyImmediate(image);
                try { preview.Cleanup(); }
                finally { foreach (var mesh in ownedMeshes) if (mesh) Object.DestroyImmediate(mesh); }
            }
        }

        static Mesh Bake(SkinnedMeshRenderer skin, List<Mesh> owned)
        {
            var mesh = new Mesh { name = skin.name + " diagnostic bake" };
            owned.Add(mesh); skin.BakeMesh(mesh, false); return mesh;
        }

        static Mesh BakeStatic(SkinnedMeshRenderer skin, List<Mesh> owned)
        {
            var copy = new GameObject(skin.name + " static snapshot") { layer = skin.gameObject.layer };
            copy.transform.SetParent(skin.transform, false);
            var mesh = Bake(skin, owned);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = skin.sharedMaterials;
            renderer.shadowCastingMode = skin.shadowCastingMode; renderer.receiveShadows = skin.receiveShadows;
            renderer.lightProbeUsage = skin.lightProbeUsage; renderer.reflectionProbeUsage = skin.reflectionProbeUsage;
            renderer.probeAnchor = skin.probeAnchor; renderer.renderingLayerMask = skin.renderingLayerMask;
            var block = new MaterialPropertyBlock();
            skin.GetPropertyBlock(block); renderer.SetPropertyBlock(block);
            for (int i = 0; i < skin.sharedMaterials.Length; i++) { block.Clear(); skin.GetPropertyBlock(block, i); renderer.SetPropertyBlock(block, i); }
            skin.enabled = false;
            return mesh;
        }

        static MeshReport Describe(SkinnedMeshRenderer skin, Transform root, List<Mesh> owned, Mesh renderedMesh)
        {
            var mesh = renderedMesh ? renderedMesh : Bake(skin, owned);
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var handedness = tangents.Select(t => t.w).Where(w => !float.IsNaN(w) && !float.IsInfinity(w)).ToArray();
            return new MeshReport { path = AnimationUtility.CalculateTransformPath(skin.transform, root),
                sourceMesh = skin.sharedMesh.name, rootBone = skin.rootBone ? AnimationUtility.CalculateTransformPath(skin.rootBone, root) : "",
                enabled = skin.enabled, updateWhenOffscreen = skin.updateWhenOffscreen, vertices = mesh.vertexCount,
                world = skin.localToWorldMatrix, blendShapes = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).Select(skin.sharedMesh.GetBlendShapeName).ToArray(),
                blendWeights = Enumerable.Range(0, skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),
                materials = skin.sharedMaterials.Select(m => m ? m.name : "MISSING").ToArray(),
                shaders = skin.sharedMaterials.Select(m => m && m.shader ? m.shader.name : "MISSING").ToArray(),
                keywords = skin.sharedMaterials.Select(m => m ? string.Join(",", m.shaderKeywords) : "").ToArray(),
                normals = Measure(normals), tangents = Measure(tangents.Select(t => new Vector3(t.x, t.y, t.z))),
                negativeTangentW = handedness.Count(w => w < 0), zeroTangentW = handedness.Count(w => Mathf.Abs(w) < .00001f),
                invalidTangentW = tangents.Length - handedness.Length, minTangentW = handedness.Length == 0 ? 0 : handedness.Min(),
                maxTangentW = handedness.Length == 0 ? 0 : handedness.Max(), submeshNormals = Enumerable.Range(0, mesh.subMeshCount)
                    .Select(i => Measure(mesh.GetIndices(i).Distinct().Where(index => index < normals.Length).Select(index => normals[index]))).ToArray() };
        }

        static VectorStats Measure(IEnumerable<Vector3> values)
        {
            var result = new VectorStats { minLength = float.MaxValue };
            foreach (var value in values)
            {
                result.count++;
                float length = value.magnitude;
                if (float.IsNaN(length) || float.IsInfinity(length)) { result.invalid++; continue; }
                if (length < .00001f) result.zero++;
                result.minLength = Mathf.Min(result.minLength, length); result.maxLength = Mathf.Max(result.maxLength, length); result.mean += value;
            }
            if (result.count > result.invalid) result.mean /= result.count - result.invalid; else result.minLength = 0;
            return result;
        }

        static void WriteNew(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes, 0, bytes.Length);
        }
    }
}
