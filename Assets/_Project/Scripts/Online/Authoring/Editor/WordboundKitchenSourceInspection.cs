using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundKitchenSourceInspection
    {
        public const string Evidence = "BuildNotes/kitchen-works-20261029/authoring/native/inspections";
        const string ScriptPath = "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundKitchenSourceInspection.cs";
        const string SourceFolder = "Assets/_Project/Art/Imported/Environment/";
        const string LibraryPath = "Assets/_Project/Resources/ModelLibrary.asset";
        static readonly string[] Keys = { "Kitchen_Fridge", "Kitchen_Counter", "Kitchen_Stove" };
        static readonly string[] ExpectedHashes = {
            "76270646a81ee62aac47823f8cc6de8b6788c03ab3e1b722cd0fff415dac6274",
            "928db9ffd492a4edd95ab02131197bd74c17d053e0099728920429ec7bd67285",
            "7197cc46158d44a8fc1428690beb39ec706116ebc05fafd0112b3be280baab00" };
        static readonly string[] Views = { "positive-z", "negative-z", "positive-x" };
        static readonly Vector3[] Directions = { Vector3.forward, Vector3.back, Vector3.right };

        [MenuItem("Tools/Wordbound/Courses/Inspect original Kitchen pilot sources")]
        public static void InspectSources()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Inspect originals outside Play mode.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && GraphicsSettings.currentRenderPipeline,
                "Kitchen source inspection needs the installed render pipeline and a graphics device.");
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            Require(library && !EditorUtility.IsDirty(library), "Save the original model library before inspection.");
            for (int i = 0; i < Keys.Length; i++)
            {
                string path = SourceFolder + Keys[i] + ".fbx";
                Require(Hash(path) == ExpectedHashes[i], "Original source bytes differ from the reviewed intake: " + path);
                Require(library.Find("Environment/" + Keys[i]) == RequireSaved<GameObject>(path), "Original model library binding differs: " + Keys[i]);
            }
            string[] dependencies = AssetDatabase.GetDependencies(Keys.Select(k => SourceFolder + k + ".fbx").ToArray(), true)
                .Append(LibraryPath).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            RequireClean(dependencies);
            var inputs = dependencies.Where(File.Exists).Append(ScriptPath).Append("Assets/_Project/Scripts/Art/ModelVisual.cs")
                .Append("Assets/_Project/Scripts/Online/Authoring/Editor/WordboundSoloPreviewCapture.cs")
                .SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, Hash, StringComparer.Ordinal);
            var dependencyHashes = dependencies.ToDictionary(p => p, p => AssetDatabase.GetAssetDependencyHash(p).ToString(), StringComparer.Ordinal);
            string beforeScenes = SceneState();
            string output = Evidence + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            RejectLinks(output); Require(!Directory.Exists(output) && !File.Exists(output), "A fresh evidence folder is required.");
            Directory.CreateDirectory(output);
            var facts = new List<object>(); var frames = new List<object>();
            foreach (string key in Keys)
            {
                string path = SourceFolder + key + ".fbx";
                var source = RequireSaved<GameObject>(path);
                using (var working = new WorkingScene())
                {
                    var parent = new GameObject("Owned kitchen source measurement"); parent.SetActive(false);
                    SceneManager.MoveGameObjectToScene(parent, working.Scene);
                    try
                    {
                        var clone = Object.Instantiate(source, parent.transform, false);
                        var bounds = ModelVisual.BoundsIn(parent.transform, clone);
                        Require(bounds.size.x > 0 && bounds.size.y > 0 && bounds.size.z > 0 &&
                            V(bounds.min).Concat(V(bounds.max)).All(v => !float.IsNaN(v) && !float.IsInfinity(v)), "Invalid native source bounds: " + key);
                        facts.Add(new { key, path, sourceSha256 = Hash(path), dependencyHash = dependencyHashes[path],
                            localMin = V(bounds.min), localMax = V(bounds.max), size = V(bounds.size),
                            rendererCount = source.GetComponentsInChildren<Renderer>(true).Length,
                            colliderCount = source.GetComponentsInChildren<Collider>(true).Length,
                            rigidbodyCount = source.GetComponentsInChildren<Rigidbody>(true).Length,
                            animatorCount = source.GetComponentsInChildren<Animator>(true).Length,
                            meshes = source.GetComponentsInChildren<MeshFilter>(true).Select(f => new { identity = Identity(f.sharedMesh),
                                vertices = f.sharedMesh.vertexCount, submeshes = f.sharedMesh.subMeshCount,
                                indices = Enumerable.Range(0, f.sharedMesh.subMeshCount).Select(i => f.sharedMesh.GetIndexCount(i)).ToArray() }).ToArray(),
                            materials = source.GetComponentsInChildren<Renderer>(true).Select(r => new { renderer = Hierarchy(r.transform, source.transform),
                                slots = r.sharedMaterials.Select(Identity).ToArray() }).ToArray() });
                    }
                    finally { Object.DestroyImmediate(parent); }
                }
                for (int view = 0; view < Views.Length; view++)
                {
                    string name = key + "-" + Views[view] + ".png";
                    CaptureOriginal(source, Directions[view], output + "/" + name);
                    frames.Add(new { name, view = Views[view], source = path, width = 512, height = 512,
                        bytes = new FileInfo(output + "/" + name).Length, sha256 = Hash(output + "/" + name) });
                }
            }
            foreach (var input in inputs) Require(File.Exists(input.Key) && Hash(input.Key) == input.Value, "Original input changed: " + input.Key);
            foreach (var input in dependencyHashes) Require(AssetDatabase.GetAssetDependencyHash(input.Key).ToString() == input.Value, "Dependency changed: " + input.Key);
            RequireClean(dependencies); Require(SceneState() == beforeScenes, "Open scene state changed during inspection.");
            WriteNew(output + "/inspection.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {
                schema = 1, status = "SOURCE_INSPECTION_ONLY_NOT_VISUALLY_APPROVED", utc = DateTime.UtcNow.ToString("O"),
                unity = Application.unityVersion, inputs, dependencyHashes, models = facts, images = frames,
                sourceFilesPreserved = true, sourceDependenciesPreserved = true, sceneStatePreserved = true,
                scope = "Raw original model axis views and native local bounds. Direction labels do not assert the front. No fitted physics, floor contact, Workshop or full-course traversal is proven."
            }, Formatting.Indented)));
            Debug.Log("[Kitchen pilot] Original source inspection: " + output + ". Review all nine axis views and native bounds before accepting appliance poses. No Unity assets changed.");
        }

        static void CaptureOriginal(GameObject source, Vector3 direction, string path)
        {
            var previousTarget = RenderTexture.active; bool previousSrgb = GL.sRGBWrite;
            bool previousSsao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), previousAsync = ShaderUtil.allowAsyncCompilation;
            PreviewRenderUtility preview = null; GameObject root = null; Texture2D pixels = null;
            Scene scene = default; bool begun = false;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene;
                Require(EditorSceneManager.IsPreviewScene(scene), "Expected an owned preview scene.");
                root = new GameObject("Original kitchen source inspection") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = Object.Instantiate(source, root.transform, false);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var bounds = ModelVisual.BoundsIn(root.transform, model);
                var camera = preview.camera;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * .7f;
                camera.aspect = 1; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .2f);
                camera.allowHDR = false; camera.allowMSAA = true;
                camera.transform.SetPositionAndRotation(bounds.center + direction * 4,
                    Quaternion.LookRotation(-direction, Vector3.up));
                preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.LookRotation(-direction) * Quaternion.Euler(35, 30, 0);
                preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.LookRotation(-direction) * Quaternion.Euler(335, 220, 0);
                foreach (var light in preview.lights) { light.color = Color.white; light.shadows = LightShadows.None; }
                preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512)); begun = true;
                preview.Render(true); preview.Render(true);
                pixels = preview.EndStaticPreview(); begun = false;
                Require(pixels && pixels.width == 512 && pixels.height == 512, "Expected a complete kitchen source image.");
                Require(pixels.GetPixels32().Select(c => c.r << 16 | c.g << 8 | c.b).Distinct().Take(65).Count() > 64,
                    "The kitchen source preview is blank or insufficiently varied. Preserve this attempt and inspect the renderer before retrying.");
                WriteNew(path, pixels.EncodeToPNG());
            }
            finally
            {
                var failures = new List<string>();
                void Clean(Action action) { try { action(); } catch (Exception e) { failures.Add(e.Message); } }
                Clean(() => { if (begun) { var abandoned = preview.EndStaticPreview(); if (abandoned) Object.DestroyImmediate(abandoned); } });
                Clean(() => { if (pixels) Object.DestroyImmediate(pixels); });
                Clean(() => { if (root) Object.DestroyImmediate(root); });
                Clean(() => preview?.Cleanup());
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); });
                Clean(() => RenderTexture.active = previousTarget); Clean(() => GL.sRGBWrite = previousSrgb);
                Clean(() => ShaderUtil.allowAsyncCompilation = previousAsync);
                Clean(() => { if (previousSsao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); });
                if (failures.Count != 0) throw new InvalidOperationException("Kitchen source preview cleanup failed: " + string.Join("; ", failures));
            }
        }

        static void RequireClean(IEnumerable<string> paths)
        {
            WordboundSoloPreviewCapture.RequireCleanDependencies(paths);
            foreach (string path in paths)
            {
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (value is Material || value is Texture || value is GameObject || value is ScriptableObject || value is Mesh)
                        Require(!EditorUtility.IsDirty(value), "Save pending original asset changes: " + path);
                var importer = AssetImporter.GetAtPath(path);
                Require(!importer || !EditorUtility.IsDirty(importer), "Save pending importer changes: " + path);
            }
        }
        static T RequireSaved<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(value && File.Exists(path) && File.Exists(path + ".meta") && !EditorUtility.IsDirty(value), "Required clean saved asset: " + path);
            return value;
        }
        static string Identity(Object value)
        {
            Require(value, "Original mesh/material is missing.");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string g, out long localId), "Original mesh/material must be saved.");
            return AssetDatabase.GetAssetPath(value) + " | " + g + " | " + localId + " | " + value.name;
        }
        static string Hierarchy(Transform value, Transform root)
        { return value == root ? root.name : Hierarchy(value.parent, root) + "/" + value.name; }
        sealed class WorkingScene : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public void Dispose() { if (Scene.IsValid() && Scene.isLoaded && EditorSceneManager.IsPreviewScene(Scene)) EditorSceneManager.ClosePreviewScene(Scene); }
        }
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static void RejectLinks(string path)
        {
            for (var at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                if (File.Exists(at) || Directory.Exists(at)) Require((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, "Linked evidence ancestry is not supported: " + at);
        }
        static void WriteNew(string path, byte[] bytes)
        { using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); output.Write(bytes, 0, bytes.Length); }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
