using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary.EditorTools
{
    /// <summary>Opens the current workspace only from a clean, empty editor session.</summary>
    [InitializeOnLoad]
    public static class WorkspaceBootstrap
    {
        const string InitializedKey = "Wreckabulary.Workspace.Started";
        const string TestingKey = "Wreckabulary.Workspace.Testing";
        const string ToggleMenu = "Wreckabulary/Play Opened Scene";
        static readonly string PreferenceKey = "Wreckabulary.PlayOpenedScene." + Hash128.Compute(Application.dataPath);
        static readonly TestCallbacks Callbacks = new();

        static WorkspaceBootstrap()
        {
            TestRunnerApi.RegisterTestCallback(Callbacks, 1000);
            AssemblyReloadEvents.beforeAssemblyReload += () => TestRunnerApi.UnregisterTestCallback(Callbacks);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += Initialize;
        }

        public static bool PlayOpenedScene
        {
            get => EditorPrefs.GetBool(PreferenceKey, false);
            set
            {
                EditorPrefs.SetBool(PreferenceKey, value);
                ApplyPlayEntry();
            }
        }

        public static bool IsAutomatedSession
        {
            get
            {
                if (Application.isBatchMode) return true;
                var arguments = Environment.GetCommandLineArgs();
                for (int i = 0; i < arguments.Length; i++)
                {
                    string argument = arguments[i];
                    if (string.Equals(argument, "-runTests", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(argument, "-runEditorTests", StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(argument, "-executeMethod", StringComparison.OrdinalIgnoreCase) &&
                         (i + 1 >= arguments.Length || arguments[i + 1] != "Wreckabulary.EditorTools.SceneWorkspace.OpenLatest"))) return true;
                }
                return false;
            }
        }

        [MenuItem(ToggleMenu, priority = -90)]
        public static void TogglePlayOpenedScene() => PlayOpenedScene = !PlayOpenedScene;

        [MenuItem(ToggleMenu, true)]
        static bool ValidateToggle()
        {
            Menu.SetChecked(ToggleMenu, PlayOpenedScene);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        static void Initialize()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Initialize;
                return;
            }

            ApplyPlayEntry();
            if (IsAutomatedSession || SessionState.GetBool(TestingKey, false) ||
                EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(InitializedKey, false)) return;

            SessionState.SetBool(InitializedKey, true);
            if (!CanOpenHubOnStartup()) return;
            string hub = WorkspaceWindow.HubPath;
            if (string.IsNullOrEmpty(hub)) return;
            EditorSceneManager.OpenScene(hub, OpenSceneMode.Single);
            WorkspaceWindow.ShowWindow();
        }

        public static bool CanOpenHubOnStartup()
        {
            if (SceneManager.sceneCount == 0) return true;
            if (SceneManager.sceneCount != 1) return false;
            var scene = SceneManager.GetSceneAt(0);
            if (!scene.IsValid() || !scene.isLoaded || scene.isDirty) return false;
            if (string.IsNullOrEmpty(scene.path)) return scene.rootCount == 0;
            return AssetDatabase.AssetPathToGUID(scene.path) == WorkspaceWindow.EmptyGuid;
        }

        public static void ApplyPlayEntry()
        {
            if (EditorApplication.isPlaying) return;
            EditorSceneManager.playModeStartScene = IsAutomatedSession || SessionState.GetBool(TestingKey, false) || PlayOpenedScene
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
                ApplyPlayEntry();
        }

        sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                SessionState.SetBool(TestingKey, true);
                EditorSceneManager.playModeStartScene = null;
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool(TestingKey, false);
                EditorApplication.delayCall += ApplyPlayEntry;
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
