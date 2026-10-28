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
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundParcelServiceDressingSetup
    {
        public const string Folder = WordboundParcelServiceMaterialsSetup.Folder + "/Dressing";
        public const string Evidence = "BuildNotes/visual-coherence-20261029/map-composition/parcel-dressing-setup/native";
        public const string Backup = Evidence + "/before-profile";
        const string ScriptPath = "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundParcelServiceDressingSetup.cs";
        const string SourceFolder = "Assets/_Project/Art/Imported/Environment/";
        const string ProfilePath = WordboundParcelServiceMaterialsSetup.VisualPath;
        static readonly string[] Keys = { "Wall_Sconce", "Wall_Shelf", "Wall_Sign" };
        static readonly string[] Modules = { "Service", "Sorting", "Dispatch" };

        sealed class ModelFact
        {
            public string key, path, dependencyHash;
            public float[] min, max;
            public string[] meshes, materials;
        }
        sealed class Inspection
        {
            public int version = 1;
            public string status = "SOURCE_INSPECTION_ONLY_NOT_VISUALLY_APPROVED";
            public Dictionary<string, string> inputs, images;
            public ModelFact[] models;
        }
        sealed class Completion
        {
            public string status, inspection, inspectionSha256;
            public Dictionary<string, string> backup, protectedSources, outputs;
        }

        [MenuItem("Tools/Wordbound/Courses/Inspect original Parcel wall fixtures")]
        public static void InspectSources()
        {
            RequireEditMode();
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && GraphicsSettings.currentRenderPipeline,
                "Fixture inspection needs the installed render pipeline and a graphics device.");
            RequireCourse();
            var inputs = Inputs();
            var models = MeasureSources();
            string output = Evidence + "/inspections/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            RequireVacant(output); RejectLinks(output); Directory.CreateDirectory(output);
            var images = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var model in models)
                foreach (bool back in new[] { false, true })
                {
                    string name = model.key + (back ? "-back.png" : "-front.png");
                    CaptureOriginal(RequireSaved<GameObject>(model.path), back, output + "/" + name);
                    images.Add(name, Hash(output + "/" + name));
                }
            Verify(inputs); RequireCleanDependencies();
            WriteNew(output + "/inspection.json", JsonConvert.SerializeObject(new Inspection { inputs = inputs, images = images, models = models }, Formatting.Indented));
            Debug.Log("[Parcel wall fixtures] Source-only native inspection saved: " + output + ". Open all six images, especially the sign faces, before choosing CreateFromReviewedInspection. No Unity assets changed.");
        }

        [MenuItem("Tools/Wordbound/Courses/Create Parcel fixtures from reviewed inspection")]
        public static void CreateReviewedFixtures()
        {
            if (File.Exists(Backup + "/COMPLETE.json")) { CreateFromReviewedInspection(""); return; }
            string folder = Environment.GetEnvironmentVariable("WORDBOUND_PARCEL_FIXTURE_INSPECTION");
            if (string.IsNullOrWhiteSpace(folder) && !Application.isBatchMode)
            {
                folder = EditorUtility.OpenFolderPanel("Choose the fixture inspection whose sign faces you reviewed", Path.GetFullPath(Evidence), "");
                if (string.IsNullOrWhiteSpace(folder)) return;
            }
            if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Select a reviewed native fixture inspection first. Creation does not infer whether the sign artwork is appropriate.");
            CreateFromReviewedInspection(folder);
        }

        public static void CreateFromReviewedInspection(string inspectionFolder)
        {
            RequireEditMode();
            var course = RequireCourse();
            var profile = course.VisualProfile;
            if (File.Exists(Backup + "/COMPLETE.json"))
            {
                VerifyCompleted(course);
                Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
                Debug.Log("[Parcel wall fixtures] Existing authored groups verified and selected. No prefab, pose, material or profile was rewritten.");
                return;
            }
            RequireVacant(Backup); RequireVacant(Folder);
            Require(profile.ModuleDressing == null || profile.ModuleDressing.Count == 0,
                "Parcel Service already has authored wall fixtures. Preserve those edits and review them manually.");
            string receipt = Path.Combine(inspectionFolder, "inspection.json");
            RejectLinks(receipt);
            Require(File.Exists(receipt) && new FileInfo(receipt).Length <= 2 * 1024 * 1024, "A bounded inspection receipt is required.");
            var inspected = JsonConvert.DeserializeObject<Inspection>(File.ReadAllText(receipt));
            Require(inspected != null && inspected.version == 1 && inspected.status == "SOURCE_INSPECTION_ONLY_NOT_VISUALLY_APPROVED" &&
                inspected.models != null && inspected.models.Select(m => m.key).SequenceEqual(Keys), "Unsupported or incomplete source inspection.");
            var inputs = Inputs();
            Require(SameFacts(inputs, inspected.inputs), "The source/profile/course changed after inspection. Inspect the current fixtures again.");
            Require(JsonConvert.SerializeObject(MeasureSources()) == JsonConvert.SerializeObject(inspected.models),
                "Native source bounds or material roles differ from inspection. Inspect again.");
            var imageNames = Keys.SelectMany(k => new[] { k + "-front.png", k + "-back.png" }).ToArray();
            foreach (string name in imageNames) RejectLinks(Path.Combine(inspectionFolder, name));
            Require(inspected.images != null && inspected.images.Count == imageNames.Length && imageNames.All(n =>
                inspected.images.TryGetValue(n, out var hash) && File.Exists(Path.Combine(inspectionFolder, n)) && Hash(Path.Combine(inspectionFolder, n)) == hash),
                "The exact six source inspection images are required.");
            using var working = new WorkingScene();
            var groups = BuildGroups(working.Scene);
            try
            {
                ValidateGroups(course, groups);
                RejectLinks(Backup); Directory.CreateDirectory(Backup);
                var backups = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var path in new[] { ProfilePath, ProfilePath + ".meta" })
                {
                    string saved = Backup + "/" + Path.GetFileName(path);
                    WriteNew(saved, File.ReadAllBytes(path));
                    Require(Hash(saved) == inputs[path], "Exact profile backup failed."); backups.Add(Path.GetFileName(path), inputs[path]);
                }
                WriteNew(Backup + "/BACKUP.json", JsonConvert.SerializeObject(backups, Formatting.Indented));
                WriteNew(Backup + "/INSPECTED-SOURCES.json", JsonConvert.SerializeObject(inputs, Formatting.Indented));
                EnsureFolder(Folder);
                var rows = new List<WordboundModuleDressingEntry>();
                for (int i = 0; i < Modules.Length; i++)
                {
                    string path = PrefabPath(Modules[i]); RequireVacant(path);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(groups[i], path, out bool success);
                    Require(success && prefab, "Could not save the fixture group. Partial output is preserved for inspection: " + path);
                    rows.Add(new WordboundModuleDressingEntry { ModuleId = Modules[i], Prefab = prefab });
                }
                WordboundModuleDressing.Validate(rows);
                Verify(inputs); RequireCleanDependencies();
                string unrelated = OtherProfileFields(profile);
                Undo.RecordObject(profile, "Assign Parcel Service wall fixtures");
                profile.ModuleDressing = rows; profile.Validate();
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                var protectedInputs = inputs.Where(p => p.Key != ProfilePath).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                Verify(protectedInputs);
                Require(unrelated == OtherProfileFields(profile), "Unrelated profile fields changed. Exact pre-edit backup retained.");
                VerifyRows(course);
                var outputs = Modules.SelectMany(m => new[] { PrefabPath(m), PrefabPath(m) + ".meta" }).Append(ProfilePath)
                    .ToDictionary(p => p, Hash, StringComparer.Ordinal);
                WriteNew(Backup + "/COMPLETE.json", JsonConvert.SerializeObject(new Completion {
                    status = "CREATED_REQUIRES_WORLD_VISUAL_REVIEW", inspection = Path.GetFullPath(receipt), inspectionSha256 = Hash(receipt),
                    backup = backups, protectedSources = protectedInputs, outputs = outputs
                }, Formatting.Indented));
                Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
                Debug.Log("[Parcel wall fixtures] Three editable prefabs / eight original fixtures saved. Original assets and gameplay bytes unchanged. Inspect gameplay, Workshop and thumbnails before accepting the design. Exact profile backup: " + Backup);
            }
            finally { foreach (var group in groups) if (group) Object.DestroyImmediate(group); }
        }

        static GameObject[] BuildGroups(Scene scene)
        {
            var groups = new List<GameObject>();
            try
            {
                foreach (string module in Modules)
                {
                    var group = new GameObject(module + " wall fixtures"); groups.Add(group);
                    SceneManager.MoveGameObjectToScene(group, scene);
                }
                AddFixture(groups[0], "Wall_Sconce", "Service left sconce", -1.2f, 2.5f);
                AddFixture(groups[0], "Wall_Sconce", "Service right sconce", 1.2f, 2.5f);
                AddFixture(groups[0], "Wall_Sign", "Service sign", 0, 2.55f);
                AddFixture(groups[1], "Wall_Shelf", "Sorting left shelf", -1.3f, 2.55f);
                AddFixture(groups[1], "Wall_Shelf", "Sorting right shelf", 1.3f, 2.55f);
                AddFixture(groups[1], "Wall_Sconce", "Sorting sconce", 0, 2.7f);
                AddFixture(groups[2], "Wall_Sign", "Dispatch sign", 0, 2.55f);
                AddFixture(groups[2], "Wall_Sconce", "Dispatch sconce", 1.1f, 2.5f);
                return groups.ToArray();
            }
            catch { foreach (var group in groups) if (group) Object.DestroyImmediate(group); throw; }
        }

        static void AddFixture(GameObject group, string key, string label, float x, float bottom)
        {
            var pose = new GameObject(label).transform; pose.SetParent(group.transform, false);
            pose.localRotation = Quaternion.Euler(0, 180, 0);
            var model = Object.Instantiate(RequireSaved<GameObject>(SourceFolder + key + ".fbx"), pose, false);
            var bounds = ModelVisual.BoundsIn(group.transform, model);
            pose.localPosition = new Vector3(x - bounds.center.x, bottom - bounds.min.y, 3.895f - bounds.max.z);
        }

        static void ValidateGroups(WordboundCourseAsset course, GameObject[] groups)
        {
            var document = course.ReadDocument(); var catalogue = course.Catalogue();
            var validation = new GameObject("Owned fixture validation");
            SceneManager.MoveGameObjectToScene(validation, groups[0].scene);
            try
            {
                for (int i = 0; i < Modules.Length; i++)
                {
                    var module = document.Modules.Single(m => m.Id == Modules[i]);
                    var definition = catalogue.Modules[module.DefinitionId];
                    Require(definition.DepthMm == 8000, "This initial setup expects the inspected flat depth8m Parcel rooms. Custom room edits are preserved.");
                    var clone = WordboundModuleDressing.Apply(new[] { new WordboundModuleDressingEntry { ModuleId = module.Id, Prefab = groups[i] } },
                        module.Id, definition, validation.transform);
                    Object.DestroyImmediate(clone);
                }
            }
            finally { Object.DestroyImmediate(validation); }
        }

        static void VerifyRows(WordboundCourseAsset course)
        {
            var profile = course.VisualProfile; profile.Validate();
            var groups = Modules.Select(m => RequireSaved<GameObject>(PrefabPath(m))).ToArray();
            foreach (string module in Modules)
            {
                var row = profile.ModuleDressing.SingleOrDefault(r => r.ModuleId == module);
                Require(row != null && row.Prefab == groups[Array.IndexOf(Modules, module)],
                    "Saved fixture assignments were changed or setup is partial. They were preserved for review.");
            }
            using var working = new WorkingScene();
            var validation = new GameObject("Owned saved fixture validation");
            SceneManager.MoveGameObjectToScene(validation, working.Scene);
            try
            {
                var document = course.ReadDocument(); var catalogue = course.Catalogue();
                foreach (string id in Modules)
                {
                    var module = document.Modules.Single(m => m.Id == id);
                    var clone = WordboundModuleDressing.Apply(profile.ModuleDressing, id, catalogue.Modules[module.DefinitionId], validation.transform);
                    Object.DestroyImmediate(clone);
                }
            }
            finally { Object.DestroyImmediate(validation); }
        }

        static void VerifyCompleted(WordboundCourseAsset course)
        {
            var record = JsonConvert.DeserializeObject<Completion>(File.ReadAllText(Backup + "/COMPLETE.json"));
            Require(record != null && record.status == "CREATED_REQUIRES_WORLD_VISUAL_REVIEW" && record.backup != null && record.backup.Count == 2,
                "Incomplete setup receipt; preserve the partial work for review.");
            foreach (string name in new[] { Path.GetFileName(ProfilePath), Path.GetFileName(ProfilePath) + ".meta" })
                Require(record.backup.TryGetValue(name, out var hash) && Hash(Backup + "/" + name) == hash, "Original backup verification failed.");
            Require(Hash(ProfilePath + ".meta") == record.backup[Path.GetFileName(ProfilePath) + ".meta"], "The existing profile identity differs from its exact backup.");
            RequireCleanDependencies(); VerifyRows(course);
        }

        static ModelFact[] MeasureSources()
        {
            var facts = new List<ModelFact>();
            using var working = new WorkingScene();
            foreach (string key in Keys)
            {
                string path = SourceFolder + key + ".fbx";
                var source = RequireSaved<GameObject>(path);
                WordboundModuleDressing.Validate(new[] { new WordboundModuleDressingEntry { ModuleId = "Inspection", Prefab = source } });
                var parent = new GameObject("Owned original fixture measurement"); parent.SetActive(false);
                SceneManager.MoveGameObjectToScene(parent, working.Scene);
                try
                {
                    var clone = Object.Instantiate(source, parent.transform, false);
                    var bounds = ModelVisual.BoundsIn(parent.transform, clone);
                    facts.Add(new ModelFact { key = key, path = path, dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                        min = V(bounds.min), max = V(bounds.max),
                        meshes = source.GetComponentsInChildren<MeshFilter>(true).Select(f => Identity(f.sharedMesh)).ToArray(),
                        materials = source.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Select(Identity).ToArray() });
                }
                finally { Object.DestroyImmediate(parent); }
            }
            return facts.ToArray();
        }

        static void CaptureOriginal(GameObject source, bool back, string path)
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
                root = new GameObject("Original fixture inspection") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = Object.Instantiate(source, root.transform, false);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var bounds = ModelVisual.BoundsIn(root.transform, model);
                var camera = preview.camera;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y) * .7f;
                camera.aspect = 1; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .16f, .2f);
                camera.allowHDR = false; camera.allowMSAA = true;
                camera.transform.SetPositionAndRotation(bounds.center + Vector3.forward * (back ? -3 : 3),
                    Quaternion.LookRotation(back ? Vector3.forward : Vector3.back, Vector3.up));
                preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(35, back ? 210 : 30, 0);
                preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.Euler(335, back ? 40 : 220, 0);
                foreach (var light in preview.lights) { light.color = Color.white; light.shadows = LightShadows.None; }
                preview.ambientColor = Color.gray;
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512)); begun = true;
                preview.Render(true); preview.Render(true);
                pixels = preview.EndStaticPreview(); begun = false;
                Require(pixels && pixels.width == 512 && pixels.height == 512, "Expected a complete fixture image.");
                Require(pixels.GetPixels32().Select(c => c.r << 16 | c.g << 8 | c.b).Distinct().Take(65).Count() > 64,
                    "The fixture preview is blank or insufficiently varied. Preserve this attempt and inspect the renderer before retrying.");
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
                if (failures.Count != 0) throw new InvalidOperationException("Fixture preview cleanup failed: " + string.Join("; ", failures));
            }
        }

        static WordboundCourseAsset RequireCourse()
        {
            var course = RequireSaved<WordboundCourseAsset>(WordboundParcelServiceSetup.Path);
            var profile = RequireSaved<WordboundVisualProfile>(ProfilePath);
            var global = RequireSaved<WordboundVisualProfile>(WordboundAuthoringSetup.VisualPath);
            Require(course.ReadDocument().Id == WordboundParcelServiceContent.CourseId && course.VisualProfile == profile && profile != global,
                "Only the saved Parcel Service course-owned visual profile can receive these fixtures.");
            profile.Validate(); return course;
        }
        static string OtherProfileFields(WordboundVisualProfile profile)
        {
            var copy = Object.Instantiate(profile);
            try { copy.ModuleDressing = null; return EditorJsonUtility.ToJson(copy); }
            finally { Object.DestroyImmediate(copy); }
        }
        static string[] DependencyPaths() => AssetDatabase.GetDependencies(Keys.Select(k => SourceFolder + k + ".fbx")
            .Append(WordboundParcelServiceSetup.Path).Append(ProfilePath).Append(WordboundAuthoringSetup.VisualPath).ToArray(), true)
            .Where(File.Exists).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        static void RequireCleanDependencies()
        {
            WordboundSoloPreviewCapture.RequireCleanDependencies(DependencyPaths());
            foreach (string path in DependencyPaths())
            {
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (value is Material || value is Texture || value is GameObject || value is ScriptableObject)
                        Require(!EditorUtility.IsDirty(value), "Save pending authoring edits before setup/inspection: " + path);
                var importer = AssetImporter.GetAtPath(path);
                Require(!importer || !EditorUtility.IsDirty(importer), "Save pending importer edits: " + path);
            }
        }
        static Dictionary<string, string> Inputs()
        {
            RequireCleanDependencies();
            return DependencyPaths().Append(ScriptPath).Append("Assets/_Project/Scripts/WordboundRuntime/WordboundModuleDressing.cs")
                .Append("Assets/_Project/Scripts/Art/ModelVisual.cs")
                .SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p }).Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, Hash, StringComparer.Ordinal);
        }
        static T RequireSaved<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(value && File.Exists(path) && File.Exists(path + ".meta") && !EditorUtility.IsDirty(value), "Required clean saved asset: " + path);
            return value;
        }
        static void Verify(Dictionary<string, string> inputs)
        { foreach (var fact in inputs) Require(File.Exists(fact.Key) && Hash(fact.Key) == fact.Value, "Protected source changed: " + fact.Key); }
        static bool SameFacts(Dictionary<string, string> a, Dictionary<string, string> b) => b != null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var h) && h == p.Value);
        static string Identity(Object value)
        {
            Require(value, "Original mesh/material is missing.");
            Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string g, out long fileId), "Original mesh/material must be a persistent asset.");
            return AssetDatabase.GetAssetPath(value) + " | " + g + " | " + fileId + " | " + value.name;
        }
        sealed class WorkingScene : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public void Dispose()
            { if (Scene.IsValid() && Scene.isLoaded && EditorSceneManager.IsPreviewScene(Scene)) EditorSceneManager.ClosePreviewScene(Scene); }
        }
        static void EnsureFolder(string path)
        {
            RejectLinks(path); if (AssetDatabase.IsValidFolder(path)) return;
            RequireVacant(path); string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))), "Cannot create folder: " + path);
        }
        static void RequireVacant(string path)
        { Require(!AssetDatabase.LoadMainAssetAtPath(path) && !File.Exists(path) && !Directory.Exists(path) && !File.Exists(path + ".meta"), "Existing/partial target preserved: " + path); }
        static void RejectLinks(string path)
        {
            for (var at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at))
                if (File.Exists(at) || Directory.Exists(at)) Require((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, "Linked output/input ancestry is not supported: " + at);
        }
        static void WriteNew(string path, string text) => WriteNew(path, Encoding.UTF8.GetBytes(text));
        static void WriteNew(string path, byte[] bytes)
        { using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); output.Write(bytes, 0, bytes.Length); }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static float[] V(Vector3 value) => new[] { value.x, value.y, value.z };
        static string PrefabPath(string module) => Folder + "/" + module + "WallFixtures.prefab";
        static void RequireEditMode() => Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Inspect/create fixtures outside Play mode.");
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
