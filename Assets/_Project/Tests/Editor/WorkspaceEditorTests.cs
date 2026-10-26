using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public class WorkspaceEditorTests
    {
        SceneSetup[] previousScenes;
        SceneAsset previousPlayEntry;
        bool safeToRestore;

        [SetUp]
        public void SetUp()
        {
            safeToRestore = false;
            if (WorkspacePlayPreview.IsActive) Assert.Ignore("Stop the workspace preview before running scene integration tests.");
            for (int i = 0; !Application.isBatchMode && i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || (string.IsNullOrEmpty(scene.path) && scene.rootCount > 0))
                    Assert.Ignore("Save open authoring scenes before running workspace scene integration tests.");
            }
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousPlayEntry = EditorSceneManager.playModeStartScene;
            safeToRestore = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (!safeToRestore) return;
            EditorSceneManager.playModeStartScene = previousPlayEntry;
            if (previousScenes.Length > 0 && previousScenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void PrototypeBootstrapRefusesBeforeTouchingProductionAssets()
        {
            string[] paths =
            {
                WorkspaceWindow.HubPath,
                "Assets/_Project/Prefabs/Player.prefab",
                "Assets/_Project/Resources/GameAssets.asset",
                "ProjectSettings/EditorBuildSettings.asset"
            };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var scene = SceneManager.GetActiveScene().handle;
            var error = Assert.Throws<InvalidOperationException>(PrototypeBuilder.BuildAll);
            StringAssert.Contains("will not overwrite", error.Message);
            for (int i = 0; i < paths.Length; i++)
                CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
            Assert.AreEqual(scene, SceneManager.GetActiveScene().handle);
        }

        [Test]
        public void StartupCanUseACleanBlankScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupPreservesUnsavedAuthoringWork()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var editedObject = new GameObject("Unsaved authoring work");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
            Assert.IsTrue(editedObject);
            Assert.IsTrue(scene.isDirty);
        }

        [Test]
        public void StartupPreservesSavedAuthoringScene()
        {
            EditorSceneManager.OpenScene(WorkspaceWindow.HubPath, OpenSceneMode.Single);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupCanReplacePreviousEmptyTestScene()
        {
            string path = AssetDatabase.GUIDToAssetPath(WorkspaceWindow.EmptyGuid);
            Assert.IsNotEmpty(path);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.IsTrue(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupPreservesAdditiveWorkspace()
        {
            EditorSceneManager.OpenScene(WorkspaceWindow.HubPath, OpenSceneMode.Single);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void ActiveTestRunDisablesDefaultPlayEntry()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
            WorkspaceBootstrap.ApplyPlayEntry();
            Assert.IsNull(EditorSceneManager.playModeStartScene);
        }

        [TestCase(WorkspaceWindow.WordboundLobbyGuid, "Assets/_Project/Scenes/WordboundLobby.unity")]
        [TestCase(WorkspaceWindow.WordboundGameplayGuid, "Assets/_Project/Scenes/WordboundGameplay.unity")]
        public void WordboundSceneShortcutsResolveRegisteredSavedScenes(string guid, string expectedPath)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.AreEqual(expectedPath, path);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
            Assert.IsTrue(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path));
        }

        [TestCase(null)]
        [TestCase(WorkspaceWindow.HubGuid)]
        [TestCase(WorkspaceWindow.EmptyGuid)]
        public void OneShotPreviewRestoresItsPreviousEntryAndPreservesAuthoringState(string previousGuid)
        {
            using var preview = new PreviewState();
            var prior = previousGuid == null ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(previousGuid));
            EditorSceneManager.playModeStartScene = prior;
            bool opened = WorkspaceBootstrap.PlayOpenedScene;
            string outfit = PlayerPrefs.GetString(LobbyMenu.OutfitKey, "<unset>");
            string theme = PlayerPrefs.GetString(LobbyThemes.PreferenceKey, "<unset>");
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            InvokePreview("Arm", LobbyMenu.Shop, prior);
            Assert.IsTrue(WorkspacePlayPreview.IsActive);
            Assert.IsTrue((bool)InvokePreview("ApplyPlayEntry"));
            Assert.AreEqual(WorkspaceWindow.HubPath, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            Assert.IsTrue((bool)InvokePreview("HandlePlayModeChanged", PlayModeStateChange.EnteredPlayMode));
            Assert.IsTrue((bool)InvokePreview("HandlePlayModeChanged", PlayModeStateChange.EnteredEditMode));
            Assert.IsFalse(WorkspacePlayPreview.IsActive);
            Assert.AreSame(prior, EditorSceneManager.playModeStartScene);
            Assert.IsTrue((bool)InvokePreview("ApplyPlayEntry"), "A delayed bootstrap callback must preserve the restored entry.");
            Assert.AreSame(prior, EditorSceneManager.playModeStartScene);
            Assert.IsFalse((bool)InvokePreview("HandlePlayModeChanged", PlayModeStateChange.ExitingEditMode),
                "The next ordinary Play must return to the existing bootstrap policy.");
            Assert.IsFalse((bool)InvokePreview("ApplyPlayEntry"));
            Assert.AreEqual(opened, WorkspaceBootstrap.PlayOpenedScene);
            Assert.AreEqual(outfit, PlayerPrefs.GetString(LobbyMenu.OutfitKey, "<unset>"));
            Assert.AreEqual(theme, PlayerPrefs.GetString(LobbyThemes.PreferenceKey, "<unset>"));
            CollectionAssert.AreEqual(scenes.Select(SceneIdentity), EditorSceneManager.GetSceneManagerSetup().Select(SceneIdentity));
        }

        [Test]
        public void InvalidAndAutomatedPreviewRequestsLeaveTheWorkspaceUntouched()
        {
            using var preview = new PreviewState();
            var prior = EditorSceneManager.playModeStartScene;
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            Assert.Throws<ArgumentException>(() => WorkspacePlayPreview.Start("online"));
            Assert.IsFalse(WorkspacePlayPreview.Start(LobbyMenu.Loadout));
            Assert.IsFalse(WorkspacePlayPreview.IsActive);
            Assert.AreSame(prior, EditorSceneManager.playModeStartScene);
            CollectionAssert.AreEqual(scenes.Select(SceneIdentity), EditorSceneManager.GetSceneManagerSetup().Select(SceneIdentity));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingStopNotificationRecoversPendingOrPlayingPreviewWithoutReplayingItsPage(bool enteredPlay)
        {
            using var preview = new PreviewState();
            var prior = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(WorkspaceWindow.EmptyGuid));
            Assert.IsNotNull(prior);
            bool opened = WorkspaceBootstrap.PlayOpenedScene;
            var scenes = EditorSceneManager.GetSceneManagerSetup();
            InvokePreview("Arm", LobbyMenu.Shop, prior);
            InvokePreview("ApplyPlayEntry");
            if (enteredPlay) InvokePreview("HandlePlayModeChanged", PlayModeStateChange.EnteredPlayMode);
            EditorSceneManager.playModeStartScene = null;
            Assert.IsTrue(WorkspacePlayPreview.IsActive);

            Assert.IsTrue((bool)InvokePreview("RecoverStoppedPreview", false, false, false),
                "An explicit stopped-editor transition must recover even when EnteredEditMode was missed.");
            Assert.IsFalse(WorkspacePlayPreview.IsActive);
            Assert.AreSame(prior, EditorSceneManager.playModeStartScene);
            Assert.AreEqual("", SessionState.GetString("Wreckabulary.Workspace.Preview.page", ""));
            Assert.AreEqual(-1, SessionState.GetFloat("Wreckabulary.Workspace.Preview.started", -1));
            Assert.IsFalse((bool)InvokePreview("RecoverStoppedPreview", false, false, false));
            Assert.AreSame(prior, EditorSceneManager.playModeStartScene);
            Assert.IsTrue((bool)InvokePreview("ApplyPlayEntry"), "Deferred bootstrap still preserves the recovered entry.");
            Assert.IsFalse((bool)InvokePreview("HandlePlayModeChanged", PlayModeStateChange.ExitingEditMode));
            Assert.IsFalse((bool)InvokePreview("ApplyPlayEntry"), "The next ordinary Play must not replay the shortcut.");
            Assert.AreEqual(opened, WorkspaceBootstrap.PlayOpenedScene);
            CollectionAssert.AreEqual(scenes.Select(SceneIdentity), EditorSceneManager.GetSceneManagerSetup().Select(SceneIdentity));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void PreviewRecoveryWaitsForPlayCompilationAndImportToFinish(bool changingPlay, bool compiling, bool updating)
        {
            using var preview = new PreviewState();
            var hub = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
            InvokePreview("Arm", LobbyMenu.Loadout, null);
            InvokePreview("ApplyPlayEntry");
            InvokePreview("HandlePlayModeChanged", PlayModeStateChange.EnteredPlayMode);
            string message = WorkspacePlayPreview.Message;

            Assert.IsFalse((bool)InvokePreview("RecoverStoppedPreview", changingPlay, compiling, updating));
            Assert.IsTrue(WorkspacePlayPreview.IsActive);
            Assert.AreSame(hub, EditorSceneManager.playModeStartScene);
            Assert.AreEqual(LobbyMenu.Loadout, SessionState.GetString("Wreckabulary.Workspace.Preview.page", ""));
            Assert.AreEqual(message, WorkspacePlayPreview.Message);
            Assert.IsTrue((bool)InvokePreview("RecoverStoppedPreview", false, false, false));
            Assert.IsNull(EditorSceneManager.playModeStartScene);
            Assert.IsFalse(WorkspacePlayPreview.IsActive);
        }

        static string SceneIdentity(SceneSetup scene) => scene.path + "|" + scene.isLoaded + "|" + scene.isActive;

        static object InvokePreview(string method, params object[] arguments) => typeof(WorkspacePlayPreview)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments);

        sealed class PreviewState : IDisposable
        {
            const string Prefix = "Wreckabulary.Workspace.Preview.";
            readonly string[] keys = { "previous", "page", "message" };
            readonly string[] values;
            readonly int phase = SessionState.GetInt(Prefix + "phase", 0);
            readonly float started = SessionState.GetFloat(Prefix + "started", -1);
            public PreviewState() => values = keys.Select(key => SessionState.GetString(Prefix + key, "")).ToArray();
            public void Dispose()
            {
                for (int i = 0; i < keys.Length; i++)
                    if (values[i] == "") SessionState.EraseString(Prefix + keys[i]);
                    else SessionState.SetString(Prefix + keys[i], values[i]);
                if (phase == 0) SessionState.EraseInt(Prefix + "phase"); else SessionState.SetInt(Prefix + "phase", phase);
                if (started < 0) SessionState.EraseFloat(Prefix + "started"); else SessionState.SetFloat(Prefix + "started", started);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TemporaryCeilingsRestoreBeforeClosingAndPreserveExistingVisibility(bool prefab)
        {
            EditorSceneManager.OpenScene(WorkspaceWindow.HubPath, OpenSceneMode.Single);
            var stage = prefab ? PrefabStageUtility.OpenPrefab("Assets/_Project/Resources/Worlds/PinwheelHouse.prefab") : null;
            var scene = stage != null ? stage.scene : SceneManager.GetActiveScene();
            var visibility = SceneVisibilityManager.instance;
            var visible = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CutawaySurface>(true))
                .Where(surface => surface.Kind != CutawayKind.UpperWall)
                .Select(surface => surface.gameObject).ToArray();
            Assert.GreaterOrEqual(visible.Length, 2, "The current saved world contains editable ceiling sections.");
            var previousVisibility = visible.Select(ceiling => visibility.IsHidden(ceiling)).ToArray();
            var prehidden = visible[0];
            var owned = visible[1];
            var window = ScriptableObject.CreateInstance<WorkspaceWindow>();
            bool observed = false, restored = false, preserved = false;

            void RestorePreviousVisibility()
            {
                for (int i = 0; i < visible.Length; i++)
                {
                    if (!visible[i]) continue;
                    if (previousVisibility[i]) visibility.Hide(visible[i], false);
                    else visibility.Show(visible[i], false);
                }
            }

            void ObserveClosing()
            {
                observed = true;
                restored = !visibility.IsHidden(owned);
                preserved = visibility.IsHidden(prehidden);
                RestorePreviousVisibility();
            }
            void SceneClosing(Scene closing, bool removingScene) { if (closing == scene) ObserveClosing(); }
            void PrefabClosing(PrefabStage closing) { if (closing == stage) ObserveClosing(); }

            if (prefab) PrefabStage.prefabStageClosing += PrefabClosing;
            else EditorSceneManager.sceneClosing += SceneClosing;
            try
            {
                visibility.Show(owned, false);
                visibility.Hide(prehidden, false);
                typeof(WorkspaceWindow).GetMethod("HideCeilings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                Assert.IsTrue(visibility.IsHidden(owned));
                if (prefab) StageUtility.GoToMainStage();
                else EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(WorkspaceWindow.EmptyGuid), OpenSceneMode.Single);
                Assert.IsTrue(observed, "The actual editor scene/prefab closing callback ran.");
                Assert.IsTrue(restored, "Temporary visibility must restore before its scene objects are destroyed.");
                Assert.IsTrue(preserved, "The workspace must retain ceilings the artist had already hidden.");
            }
            finally
            {
                EditorSceneManager.sceneClosing -= SceneClosing;
                PrefabStage.prefabStageClosing -= PrefabClosing;
                UnityEngine.Object.DestroyImmediate(window);
                RestorePreviousVisibility();
                if (prefab && PrefabStageUtility.GetCurrentPrefabStage() == stage) StageUtility.GoToMainStage();
            }
        }
    }
}
