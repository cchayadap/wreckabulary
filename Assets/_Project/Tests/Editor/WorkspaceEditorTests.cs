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
