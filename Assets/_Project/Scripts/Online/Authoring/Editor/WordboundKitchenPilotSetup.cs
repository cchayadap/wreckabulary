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
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundKitchenPilotSetup
    {
        public const string Folder = WordboundAuthoringSetup.Folder + "/FlagshipKitchenV1";
        public const string ItemsPath = Folder + "/Items.json";
        public const string CoursePath = Folder + "/Course.asset";
        public const string VisualPath = Folder + "/Presentation.asset";
        public const string LaunchPath = Folder + "/LaunchAssets.asset";
        public const string ScenePath = Folder + "/KitchenAuthoring.unity";
        const string LaunchSourcePath = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";
        const string Evidence = "BuildNotes/kitchen-works-20261029/authoring/native/setup";
        static readonly string[] Names = { "Course.asset", "Presentation.asset", "LaunchAssets.asset", "KitchenAuthoring.unity" };

        [MenuItem("Tools/Wordbound/Courses/Create or select Kitchen pilot")]
        public static void CreateOrSelect()
        {
            var source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            var launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchSourcePath);
            var items = AssetDatabase.LoadAssetAtPath<TextAsset>(ItemsPath);
            var course = CreateOrSelectAt(Folder, items, source, launch, WordboundLaunchAssets.LobbyPath,
                Evidence + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Selection.activeObject = course; EditorGUIUtility.PingObject(course);
            Debug.Log("[Kitchen pilot] Saved course selected: " + CoursePath +
                ". Validate/preview exact seed, choose Kitchen ServiceLanding in View, then Frame. For drag editing, open " +
                ScenePath + ", press Play, then Workshop. The normal lobby/global launch assets are unchanged. Local authoring only; no public admission.");
        }

        [MenuItem("Tools/Wordbound/Courses/Open Kitchen pilot authoring scene")]
        public static void OpenAuthoringScene()
        {
            RequireEditMode();
            VerifyExisting(Folder, AssetDatabase.LoadAssetAtPath<TextAsset>(ItemsPath));
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static WordboundCourseAsset CreateOrSelectAt(string folder, TextAsset items,
            WordboundCourseAsset source, WordboundLaunchAssets launchSource, string sourceScene, string evidenceFolder)
        {
            RequireEditMode(); ValidateFolder(folder);
            string[] paths = Names.Select(n => folder + "/" + n).ToArray();
            int occupied = paths.Count(Occupied);
            if (occupied != 0)
            {
                Require(occupied == paths.Length, "Partial or conflicting Kitchen pilot assets were preserved. Resolve them before setup.");
                return VerifyExisting(folder, items);
            }
            RequireSaved(items); RequireSaved(source); RequireSaved(launchSource);
            RequireSaved(source.RuleBook); RequireSaved(source.CameraProfile); RequireSaved(source.VisualProfile);
            Require(source.VisualProfile.BaseArtwork == WordboundBaseArtwork.OriginalWreckabulary, "Use the original Wreckabulary artwork source.");
            Require(items != source.Items, "The pilot needs its separate augmented item catalogue.");
            Require(!string.IsNullOrWhiteSpace(evidenceFolder), "Choose a fresh setup evidence folder.");
            RejectLinks(evidenceFolder); Require(!Occupied(evidenceFolder), "Existing setup evidence was preserved.");
            source.CameraProfile.Validate(); source.VisualProfile.Validate();
            var content = WordboundKitchenPilotContent.Create(ItemCatalogue.FromJson(items.text));
            VerifyScene(sourceScene, launchSource);
            var inputs = Inputs(source, launchSource, items, sourceScene);
            string scenes = SceneState();
            WordboundCourseAsset course = null, saved = null;
            WordboundVisualProfile visual = null;
            WordboundLaunchAssets launch = null;
            try
            {
                course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
                visual = Object.Instantiate(source.VisualProfile); launch = Object.Instantiate(launchSource);
                visual.name = "Kitchen presentation";
                course.name = "Kitchen pilot V1";
                course.Initialize(content.CopyDocument(), content.Geometry);
                course.Items = items; course.RuleBook = source.RuleBook; course.CameraProfile = source.CameraProfile; course.VisualProfile = visual;
                course.Seed = "0123456789abcdef"; course.ModeId = "TimeRun"; course.Format = RaceFormat.Solo;
                launch.name = "Kitchen local launch assets"; launch.WorkshopCourse = course; launch.VisualProfile = visual;
                ValidateCourse(course);
                Directory.CreateDirectory(evidenceFolder);
                WriteNew(evidenceFolder + "/INPUTS.json", JsonConvert.SerializeObject(inputs, Formatting.Indented));
                EnsureFolder(folder);
                for (int i = 0; i < 3; i++) Require(!Occupied(paths[i]), "Output became occupied before creation: " + paths[i]);
                AssetDatabase.CreateAsset(visual, paths[1]); AssetDatabase.SaveAssetIfDirty(visual);
                AssetDatabase.CreateAsset(course, paths[0]); AssetDatabase.SaveAssetIfDirty(course);
                AssetDatabase.CreateAsset(launch, paths[2]); AssetDatabase.SaveAssetIfDirty(launch);
                CopyScene(sourceScene, paths[3], launch);
                saved = VerifyExisting(folder, items);
            }
            finally
            {
                var failures = new List<string>();
                foreach (var value in new Object[] { course, visual, launch })
                    try { if (value && !AssetDatabase.Contains(value)) Object.DestroyImmediate(value); }
                    catch (Exception error) { failures.Add(error.Message); }
                if (failures.Count != 0) throw new InvalidOperationException("Kitchen setup owned-object cleanup failed: " + string.Join("; ", failures));
            }
            Verify(inputs); Require(SceneState() == scenes, "The open scene setup changed; output retained for diagnosis.");
            var outputs = paths.SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, Hash, StringComparer.Ordinal);
            WriteNew(evidenceFolder + "/COMPLETE.json", JsonConvert.SerializeObject(new {
                schema = 1, status = "CREATED_REQUIRES_NATIVE_GAMEPLAY_AND_VISUAL_REVIEW", utc = DateTime.UtcNow.ToString("O"),
                inputs, outputs, sourceFilesPreserved = true, openScenesPreserved = true,
                courseId = saved.ReadDocument().Id, seed = saved.Seed, mode = saved.ModeId, format = saved.Format.ToString(),
                sourceHash = saved.SourceHash, inputHash = saved.InputHash, localAuthoringScene = paths[3], publicAdmissionChanged = false
            }, Formatting.Indented));
            return saved;
        }

        static WordboundCourseAsset VerifyExisting(string folder, TextAsset items)
        {
            RequireEditMode(); ValidateFolder(folder); RequireSaved(items);
            string scenes = SceneState();
            string[] paths = Names.Select(n => folder + "/" + n).ToArray();
            Require(paths.All(p => File.Exists(p) && File.Exists(p + ".meta")), "All four saved pilot outputs are required; partial content is preserved.");
            var before = paths.SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, Hash, StringComparer.Ordinal);
            var course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(paths[0]);
            var visual = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(paths[1]);
            var launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(paths[2]);
            RequireSaved(course); RequireSaved(visual); RequireSaved(launch);
            Require(course.Items == items && course.VisualProfile == visual && launch.WorkshopCourse == course && launch.VisualProfile == visual,
                "Saved pilot bindings differ from the isolated course/visual/launch contract. Existing edits are preserved.");
            RequireSaved(course.RuleBook); RequireSaved(course.CameraProfile);
            WordboundSoloPreviewCapture.RequireCleanDependencies(paths);
            ValidateCourse(course); VerifyScene(paths[3], launch); Verify(before);
            Require(SceneState() == scenes, "Existing pilot verification changed the open scene setup.");
            return course;
        }

        static void ValidateCourse(WordboundCourseAsset course)
        {
            Require(course.ReadDocument().Id == WordboundKitchenPilotContent.CourseId, "Existing output must remain the explicit Kitchen pilot course.");
            Require(course.Format == RaceFormat.Solo && (course.ModeId == "TimeRun" || course.ModeId == "Freestyle"),
                "Local pilot authoring supports TimeRun/Freestyle Solo; other saved choices are preserved for review.");
            _ = WordboundKitchenPilotContent.Create(ItemCatalogue.FromJson(course.Items.text));
            course.CameraProfile.Validate(); course.VisualProfile.Validate();
            _ = course.CreateContent(course.Resolve());
        }

        static void CopyScene(string source, string target, WordboundLaunchAssets launch)
        {
            Require(!Occupied(target), "Scene output already exists: " + target);
            Require(AssetDatabase.CopyAsset(source, target), "Could not copy the existing lobby scene. Partial output retained.");
            var previous = SceneManager.GetActiveScene(); Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive);
                Require(scene.IsValid() && scene.isLoaded && scene.path == target, "Could not open the copied authoring scene.");
                var launcher = Launcher(scene); launcher.Assets = launch;
                EditorUtility.SetDirty(launcher); EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene, target), "Could not save the copied scene binding. Partial output retained.");
            }
            finally
            {
                var failures = new List<string>();
                try { if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.CloseScene(scene, true)) failures.Add("Could not close owned copied scene."); }
                catch (Exception e) { failures.Add(e.Message); }
                try { if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous && !SceneManager.SetActiveScene(previous)) failures.Add("Could not restore active scene."); }
                catch (Exception e) { failures.Add(e.Message); }
                if (failures.Count != 0) throw new InvalidOperationException(string.Join("; ", failures));
            }
        }
        static void VerifyScene(string path, WordboundLaunchAssets launch)
        {
            var saved = AssetDatabase.LoadAssetAtPath<SceneAsset>(path); RequireSaved(saved);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                Require(open.path != path || !open.isDirty, "Save pending scene edits before verification: " + path);
            }
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(path);
                Require(EditorSceneManager.IsPreviewScene(scene), "Expected an owned scene verification copy.");
                var launcher = Launcher(scene);
                Require(launcher.Assets == launch && launcher.LobbyCamera && launcher.LobbyCamera.gameObject.scene == scene,
                    "The authoring scene must bind its local launch assets and own lobby camera.");
            }
            finally { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }
        }
        static WordboundLauncher Launcher(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            Require(roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0),
                "The scene contains a missing component script.");
            var values = roots.SelectMany(r => r.GetComponentsInChildren<WordboundLauncher>(true)).ToArray();
            Require(values.Length == 1, "The scene must have exactly one Wordbound launcher."); return values[0];
        }
        static Dictionary<string, string> Inputs(WordboundCourseAsset source, WordboundLaunchAssets launch, TextAsset items, string scene)
        {
            var roots = new Object[] { source, source.Items, source.RuleBook, source.CameraProfile, source.VisualProfile, launch, items };
            foreach (var value in roots) RequireSaved(value);
            var paths = roots.Select(AssetDatabase.GetAssetPath).Append(scene).ToArray();
            WordboundSoloPreviewCapture.RequireCleanDependencies(paths);
            return paths.Append("ProjectSettings/EditorBuildSettings.asset").SelectMany(p => new[] { p, p + ".meta" })
                .Where(File.Exists).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => p, Hash, StringComparer.Ordinal);
        }
        static void RequireSaved(Object value)
        {
            Require(value && AssetDatabase.Contains(value) && !EditorUtility.IsDirty(value), "Assign clean saved source assets; save pending edits first.");
            string path = AssetDatabase.GetAssetPath(value); Require(File.Exists(path) && File.Exists(path + ".meta"), "Source asset is not fully saved: " + path);
        }
        static void ValidateFolder(string path)
        {
            Require(!string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains('\\') && !path.Contains(':') &&
                path.Split('/').All(p => p != "" && p != "." && p != "..") && !path.Split('/').Any(p => p.Equals("Resources", StringComparison.OrdinalIgnoreCase)), "Use a dedicated Assets folder outside Resources.");
            RejectLinks(path);
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Require(!Occupied(path), "Unimported folder collision was preserved: " + path);
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))), "Could not create folder: " + path);
        }
        static bool Occupied(string path) => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || path.StartsWith("Assets/", StringComparison.Ordinal) && AssetDatabase.LoadMainAssetAtPath(path);
        static void Verify(Dictionary<string, string> inputs)
        { foreach (var input in inputs) Require(File.Exists(input.Key) && Hash(input.Key) == input.Value, "Protected file changed: " + input.Key); }
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static void RejectLinks(string path)
        { for (var at = Path.GetFullPath(path); !string.IsNullOrEmpty(at); at = Path.GetDirectoryName(at)) if (File.Exists(at) || Directory.Exists(at)) Require((File.GetAttributes(at) & FileAttributes.ReparsePoint) == 0, "Linked path ancestry is not supported: " + at); }
        static void WriteNew(string path, string text)
        { byte[] bytes = Encoding.UTF8.GetBytes(text); using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes, 0, bytes.Length); }
        static string Hash(string path)
        { using var sha = SHA256.Create(); using var stream = File.OpenRead(path); return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static void RequireEditMode() => Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Create/verify Kitchen authoring assets outside Play mode.");
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
