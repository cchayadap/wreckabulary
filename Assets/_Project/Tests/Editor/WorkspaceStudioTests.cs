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
    public class WorkspaceStudioTests
    {
        static readonly Func<string, Action, bool> CreateMissing = (Func<string, Action, bool>)Delegate.CreateDelegate(
            typeof(Func<string, Action, bool>), typeof(WorkspaceStudioSetup).GetMethod("CreateSceneIfMissing", BindingFlags.Static | BindingFlags.NonPublic));
        SceneSetup[] previousScenes;
        SceneAsset previousPlayEntry;
        string folder;
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
            folder = "Assets/_Project/Tests/Editor/WorkspaceStudioTest-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(folder);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            if (!safeToRestore) return;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (previousScenes.Length > 0 && previousScenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
            finally
            {
                EditorSceneManager.playModeStartScene = previousPlayEntry;
                if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                    Assert.IsTrue(AssetDatabase.DeleteAsset(folder), "Remove only this test's unique asset folder.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FreshStudioPreservesUntitledSceneAndSourceTemplate(bool dirty)
        {
            var authoring = SceneManager.GetActiveScene();
            if (dirty)
            {
                new GameObject("Unsaved authoring work").transform.position = new Vector3(3f, 4f, 5f);
                EditorSceneManager.MarkSceneDirty(authoring);
            }
            var roots = authoring.GetRootGameObjects();
            bool wasDirty = authoring.isDirty;
            int count = SceneManager.sceneCount;
            string template = AssetDatabase.GUIDToAssetPath(WorkspaceWindow.WordboundGameplayGuid);
            var source = File.ReadAllBytes(template);
            var sourceMeta = File.ReadAllBytes(template + ".meta");
            string destination = folder + "/Studio.unity";
            Assert.IsTrue(CreateMissing(destination, () =>
            {
                var temporary = SceneManager.GetActiveScene();
                Assert.AreNotEqual(authoring.handle, temporary.handle);
                Assert.IsNotEmpty(temporary.path);
                Assert.AreEqual(0, temporary.rootCount, "Only the copied scene's original roots are removed.");
                new GameObject("Saved studio content");
            }));
            AssertAuthoringPreserved(authoring, roots, wasDirty, count);
            CollectionAssert.AreEqual(source, File.ReadAllBytes(template));
            CollectionAssert.AreEqual(sourceMeta, File.ReadAllBytes(template + ".meta"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(destination));
            CollectionAssert.AreEqual(new[] { destination }, Directory.GetFiles(folder, "*.unity").Select(path => path.Replace('\\', '/')).ToArray());
            AssertNoTemporaryAssets();

            var saved = EditorSceneManager.OpenScene(destination, OpenSceneMode.Additive);
            try { CollectionAssert.AreEqual(new[] { "Saved studio content" }, saved.GetRootGameObjects().Select(root => root.name).ToArray()); }
            finally
            {
                EditorSceneManager.CloseScene(saved, true);
                SceneManager.SetActiveScene(authoring);
            }
            AssertAuthoringPreserved(authoring, roots, wasDirty, count);
        }

        [Test]
        public void RepeatSetupPreservesAuthoredSavedSceneAndUnsavedChanges()
        {
            string destination = folder + "/Studio.unity";
            Assert.IsTrue(CreateMissing(destination, () => new GameObject("Initial content")));
            var studio = EditorSceneManager.OpenScene(destination, OpenSceneMode.Additive);
            Assert.IsTrue(SceneManager.SetActiveScene(studio));
            var root = studio.GetRootGameObjects().Single();
            root.name = "Authored studio content";
            Assert.IsTrue(EditorSceneManager.SaveScene(studio));
            var bytes = File.ReadAllBytes(destination);
            var meta = File.ReadAllBytes(destination + ".meta");
            root.transform.position = new Vector3(7f, 2f, 1f);
            EditorSceneManager.MarkSceneDirty(studio);
            int count = SceneManager.sceneCount;

            Assert.IsFalse(CreateMissing(destination, () => Assert.Fail("Existing authored scenes must never be repopulated.")));
            Assert.AreEqual(studio.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(count, SceneManager.sceneCount);
            Assert.IsTrue(studio.isDirty);
            Assert.AreEqual(new Vector3(7f, 2f, 1f), root.transform.position);
            Assert.AreEqual("Authored studio content", root.name);
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(destination));
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(destination + ".meta"));
            AssertNoTemporaryAssets();
        }

        [Test]
        public void PopulateFailureDoesNotPublishPartialStudioOrTouchUntitledWork()
        {
            var authoring = SceneManager.GetActiveScene();
            new GameObject("Keep my unsaved work");
            EditorSceneManager.MarkSceneDirty(authoring);
            var roots = authoring.GetRootGameObjects();
            int count = SceneManager.sceneCount;
            string destination = folder + "/Studio.unity";
            var error = Assert.Throws<InvalidOperationException>(() => CreateMissing(destination, () =>
            {
                new GameObject("Incomplete studio content");
                throw new InvalidOperationException("Injected population failure");
            }));
            Assert.AreEqual("Injected population failure", error.Message);
            AssertAuthoringPreserved(authoring, roots, true, count);
            Assert.IsFalse(File.Exists(destination));
            Assert.IsFalse(File.Exists(destination + ".meta"));
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(destination));
            AssertNoTemporaryAssets();
        }

        static void AssertAuthoringPreserved(Scene authoring, GameObject[] roots, bool dirty, int count)
        {
            Assert.AreEqual(authoring.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(count, SceneManager.sceneCount);
            Assert.AreEqual("", authoring.path);
            Assert.AreEqual(dirty, authoring.isDirty);
            CollectionAssert.AreEqual(roots, authoring.GetRootGameObjects());
        }

        void AssertNoTemporaryAssets() => Assert.IsEmpty(Directory.GetFiles(folder, "WorkspaceStudioTemp-*"));
    }
}
