using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundCourseSceneSetup
    {
        public const string Root = "Assets/_Project/Scenes/Authoring/Courses";
        const string LaunchSource = "Assets/_Project/Resources/Wordbound/LaunchAssets.asset";

        [MenuItem("Tools/Wordbound/Courses/Create missing course entry scenes")]
        public static void CreateOrVerify()
        {
            var launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(LaunchSource);
            foreach (var pair in new[] {
                ("Flagship", WordboundAuthoringSetup.FlagshipPath),
                ("Freestyle", WordboundAuthoringSetup.FreestylePath),
                ("ParcelService", WordboundParcelServiceSetup.Path) })
                CreateAt(Root + "/" + pair.Item1,
                    AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(pair.Item2), launch);
            Debug.Log("[Course entry scenes] Three saved local Workshop entries verified. Open an entry using Wreckabulary/Course entries, then Play and Workshop. Global lobby, course sources and build scenes are unchanged.");
        }

        public static SceneAsset CreateAt(string folder, WordboundCourseAsset course, WordboundLaunchAssets source)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Create course entries outside Play mode.");
            Require(!string.IsNullOrWhiteSpace(folder) && folder.StartsWith("Assets/_Project/", StringComparison.Ordinal) &&
                !folder.Contains('\\') && folder.Split('/').All(p => p != "" && p != "." && p != ".." && p != "Resources"),
                "Use an explicit authoring folder outside Resources.");
            WordboundSoloPreviewCapture.RejectLinks(Path.GetFullPath(folder));
            Saved(course); Saved(source); Saved(course.Items); Saved(course.RuleBook); Saved(course.VisualProfile); Saved(course.CameraProfile);
            _ = course.CreateContent(course.Resolve());
            string launchPath = folder + "/LaunchAssets.asset", scenePath = folder + "/Workshop.unity";
            bool hasLaunch = Occupied(launchPath), hasScene = Occupied(scenePath);
            Require(hasLaunch == hasScene, "Partial course entry outputs are preserved; resolve the conflicting group before retrying.");
            var inputs = new[] { AssetDatabase.GetAssetPath(course), AssetDatabase.GetAssetPath(source),
                AssetDatabase.GetAssetPath(course.Items), AssetDatabase.GetAssetPath(course.RuleBook),
                AssetDatabase.GetAssetPath(course.VisualProfile), AssetDatabase.GetAssetPath(course.CameraProfile),
                WordboundLaunchAssets.LobbyPath, "ProjectSettings/EditorBuildSettings.asset" }
                .Distinct().SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
                .ToDictionary(p => p, File.ReadAllBytes);
            WordboundSoloPreviewCapture.RequireCleanDependencies(inputs.Keys);
            string scenes = SceneState();
            if (hasLaunch)
            {
                var savedLaunch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(launchPath);
                Saved(savedLaunch);
                Require(savedLaunch.WorkshopCourse == course, "Existing entry belongs to another course; its saved bindings were preserved.");
                VerifyScene(scenePath, savedLaunch);
                Require(SceneState() == scenes, "Entry verification changed the open scene state.");
                Verify(inputs);
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            }
            VerifyScene(WordboundLaunchAssets.LobbyPath, source);
            WordboundSoloPreviewCapture.EnsureFolder(folder);
            WordboundLaunchAssets local = Object.Instantiate(source);
            try
            {
                local.name = Path.GetFileName(folder) + " local Workshop entry";
                local.WorkshopCourse = course;
                local.VisualProfile = course.VisualProfile;
                AssetDatabase.CreateAsset(local, launchPath); AssetDatabase.SaveAssetIfDirty(local);
                Require(AssetDatabase.CopyAsset(WordboundLaunchAssets.LobbyPath, scenePath), "Could not copy the small lobby shell; partial output retained.");
                var previous = SceneManager.GetActiveScene(); Scene copy = default;
                try
                {
                    copy = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    var launcher = Launcher(copy); launcher.Assets = local;
                    EditorUtility.SetDirty(launcher); EditorSceneManager.MarkSceneDirty(copy);
                    Require(EditorSceneManager.SaveScene(copy, scenePath), "Could not save the local scene binding.");
                }
                finally
                {
                    if (copy.IsValid() && copy.isLoaded)
                        Require(EditorSceneManager.CloseScene(copy, true), "Could not close the owned copied entry scene.");
                    if (previous.IsValid() && previous.isLoaded && SceneManager.GetActiveScene() != previous)
                        Require(SceneManager.SetActiveScene(previous), "Could not restore active scene.");
                }
                VerifyScene(scenePath, local); Verify(inputs);
                Require(SceneState() == scenes, "Entry creation changed the open scene state.");
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            }
            finally { if (local && !AssetDatabase.Contains(local)) Object.DestroyImmediate(local); }
        }

        static void VerifyScene(string path, WordboundLaunchAssets launch)
        {
            Saved(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                Require(scene.path != path || !scene.isDirty, "Save pending entry scene edits first: " + path);
            Scene owned = default;
            try
            {
                owned = EditorSceneManager.OpenPreviewScene(path);
                var value = Launcher(owned);
                Require(value.Assets == launch && value.LobbyCamera && value.LobbyCamera.gameObject.scene == owned,
                    "Saved scene must use its local launch settings and its own camera.");
            }
            finally { if (owned.IsValid() && owned.isLoaded) EditorSceneManager.ClosePreviewScene(owned); }
        }
        static WordboundLauncher Launcher(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(all.All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Entry contains a missing script.");
            var values = all.Select(t => t.GetComponent<WordboundLauncher>()).Where(v => v).ToArray();
            Require(values.Length == 1, "Entry needs exactly one launcher."); return values[0];
        }
        static void Saved(Object value)
        {
            Require(value && AssetDatabase.Contains(value) && !EditorUtility.IsDirty(value), "Assign clean saved entry/course assets.");
            string path = AssetDatabase.GetAssetPath(value);
            Require(File.Exists(path) && File.Exists(path + ".meta"), "Saved source metadata is required: " + path);
        }
        static bool Occupied(string path) => File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path);
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()); }));
        static void Verify(System.Collections.Generic.Dictionary<string, byte[]> inputs)
        { foreach (var input in inputs) Require(input.Value.SequenceEqual(File.ReadAllBytes(input.Key)), "Source changed: " + input.Key); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
