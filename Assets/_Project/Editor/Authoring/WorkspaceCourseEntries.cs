using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Wreckabulary.EditorTools
{
    public static class WorkspaceCourseEntries
    {
        const string Root = "Assets/_Project/Scenes/Authoring/Courses/";

        [MenuItem("Wreckabulary/Course entries/Open Flagship for Play")]
        public static void Flagship() => OpenForPlay(Root + "Flagship/Workshop.unity");
        [MenuItem("Wreckabulary/Course entries/Open Freestyle for Play")]
        public static void Freestyle() => OpenForPlay(Root + "Freestyle/Workshop.unity");
        [MenuItem("Wreckabulary/Course entries/Open Parcel Service for Play")]
        public static void ParcelService() => OpenForPlay(Root + "ParcelService/Workshop.unity");
        [MenuItem("Wreckabulary/Course entries/Open Kitchen pilot for Play")]
        public static void Kitchen() => OpenForPlay("Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/KitchenAuthoring.unity");

        public static void OpenForPlay(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || WorkspacePlayPreview.IsActive)
                throw new InvalidOperationException("Stop the current Play session before opening another entry.");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (!scene || !File.Exists(path) || !File.Exists(path + ".meta"))
                throw new InvalidOperationException("Create the saved course entry first: " + path);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            WorkspaceBootstrap.PlayOpenedScene = true;
            Selection.activeObject = scene;
            DebugLog(path);
        }
        static void DebugLog(string path) => UnityEngine.Debug.Log("[Course entry] Opened " + path + ". Play starts in the opened scene; choose Workshop. Restore Hub entry using Wreckabulary/Play Opened Scene when desired. Course geometry is edited through its saved course asset or Workshop.");
    }
}
