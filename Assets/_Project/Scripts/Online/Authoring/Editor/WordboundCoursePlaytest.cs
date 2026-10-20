using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Editor
{
    [InitializeOnLoad]
    public static class WordboundCoursePlaytest
    {
        const string Prefix = "Wordbound.Authoring.Playtest.";
        [Serializable] sealed class SceneRecord { public string path; public bool loaded, active; }
        [Serializable] sealed class SceneRecords { public SceneRecord[] scenes; }
        static WordboundLocalMatchClient client;
        static bool restoring;
        static WordboundCoursePlaytest() => EditorApplication.playModeStateChanged += StateChanged;

        public static void Start(WordboundCourseAsset asset, ResolvedCourse exact, string expectedInputHash)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Prefix + "active", false))
                throw new InvalidOperationException("Close the current playtest before starting another.");
            if (asset.ModeId != "TimeRun" && asset.ModeId != "Freestyle")
                throw new ArgumentException("Local authoring playtests support Time Run and Freestyle.");
            if (expectedInputHash != asset.InputHash)
                throw new ArgumentException("Saved content changed. Preview the current course, rules and catalogue before playing.");
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Save the course asset before playing.");
            var content = asset.CreateContent(exact);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            if (!launch) throw new ArgumentException("Generate the registered Wordbound launch assets before playing.");
            _ = launch.GameplayScene(); _ = launch.BodyPrefabs();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = new SceneRecords { scenes = EditorSceneManager.GetSceneManagerSetup().Select(s =>
                new SceneRecord { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() };
            SessionState.SetString(Prefix + "scenes", JsonUtility.ToJson(setup));
            SessionState.SetString(Prefix + "asset", path);
            SessionState.SetString(Prefix + "manifest", CourseJson.ExportManifest(exact));
            SessionState.SetString(Prefix + "content", content.CatalogueHash);
            SessionState.SetString(Prefix + "input", expectedInputHash);
            SessionState.SetBool(Prefix + "active", true);
            try { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.isPlaying = true; }
            catch { RestoreScenes(); throw; }
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "active", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) StartRuntime();
            if (state == PlayModeStateChange.EnteredEditMode) RestoreScenes();
        }
        static async void StartRuntime()
        {
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(SessionState.GetString(Prefix + "asset", ""));
                if (!asset) throw new InvalidOperationException("The saved course asset is no longer available.");
                if (asset.InputHash != SessionState.GetString(Prefix + "input", ""))
                    throw new InvalidOperationException("The authoring inputs changed while entering play mode. Preview again.");
                var loaded = CourseJson.ImportManifest(SessionState.GetString(Prefix + "manifest", ""), asset.Catalogue());
                if (!loaded.Success) throw new ArgumentException(string.Join("\n", loaded.Validation.Issues));
                var content = asset.CreateContent(loaded.Manifest);
                if (content.CatalogueHash != SessionState.GetString(Prefix + "content", ""))
                    throw new InvalidOperationException("Gameplay content changed after preview. Exit and preview again.");
                var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
                if (!launch) throw new InvalidOperationException("Wordbound launch assets are unavailable.");
                var root = new GameObject("Wordbound Inspector playtest");
                client = root.AddComponent<WordboundLocalMatchClient>();
                client.ReturnedToWorkshop += Returned;
                client.Failed += Failed;
                await client.StartLocal(content, launch.BodyPrefabs(), launch.GameplayScene(),
                    cameraProfile: asset.CameraProfile, visualProfile: asset.VisualProfile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (client)
                {
                    try { await client.ReturnToWorkshop(); }
                    catch (Exception shutdown) { Debug.LogException(shutdown); }
                }
                EditorApplication.isPlaying = false;
            }
        }
        static void Returned() => EditorApplication.isPlaying = false;
        static void Failed(string error) => Debug.LogError("Wordbound Inspector playtest: " + error);
        static void RestoreScenes()
        {
            if (restoring || EditorApplication.isPlayingOrWillChangePlaymode) return;
            restoring = true;
            bool restored = false;
            try
            {
                if (client) { client.ReturnedToWorkshop -= Returned; client.Failed -= Failed; }
                client = null;
                var records = JsonUtility.FromJson<SceneRecords>(SessionState.GetString(Prefix + "scenes", "{}"));
                if (records?.scenes?.Length > 0 && records.scenes.All(s => !string.IsNullOrEmpty(s.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(records.scenes.Select(s =>
                        new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                restored = true;
            }
            finally
            {
                if (restored)
                {
                    foreach (string key in new[] { "scenes", "asset", "manifest", "content", "input" }) SessionState.EraseString(Prefix + key);
                    SessionState.EraseBool(Prefix + "active");
                }
                restoring = false;
            }
        }
        [MenuItem("Tools/Wordbound/Restore authoring scene setup")]
        static void RestoreSavedScenes()
        {
            if (SessionState.GetBool(Prefix + "active", false) && !EditorApplication.isPlayingOrWillChangePlaymode) RestoreScenes();
        }
    }
}
