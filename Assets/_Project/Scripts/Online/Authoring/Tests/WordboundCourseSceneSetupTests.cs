using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;

namespace Wreckabulary.Tests
{
    public sealed class WordboundCourseSceneSetupTests
    {
        string folder;
        WordboundCourseAsset course;
        WordboundLaunchAssets source;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/_Project/Tests/CourseScene_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/_Project/Tests", Path.GetFileName(folder));
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            source = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Assert.IsNotNull(course); Assert.IsNotNull(source);
        }
        [TearDown]
        public void TearDown()
        {
            if (folder != null && folder.StartsWith("Assets/_Project/Tests/CourseScene_", StringComparison.Ordinal))
                Assert.IsTrue(AssetDatabase.DeleteAsset(folder));
        }

        [Test]
        public void SavedEntryBindsLocalLaunchAndCourseWhileGlobalSceneCourseAndBuildListStayExact()
        {
            string[] sources = { WordboundLaunchAssets.LobbyPath, AssetDatabase.GetAssetPath(source),
                AssetDatabase.GetAssetPath(course), "ProjectSettings/EditorBuildSettings.asset" };
            var before = sources.SelectMany(p => new[] { p, p + ".meta" }).Where(File.Exists).ToDictionary(p => p, File.ReadAllBytes);
            var sceneState = EditorSceneManager.GetSceneManagerSetup();
            var entry = WordboundCourseSceneSetup.CreateAt(folder, course, source);
            Assert.AreEqual(folder + "/Workshop.unity", AssetDatabase.GetAssetPath(entry));
            var local = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(folder + "/LaunchAssets.asset");
            Assert.AreNotSame(source, local); Assert.AreSame(course, local.WorkshopCourse);
            Assert.AreSame(course.VisualProfile, local.VisualProfile);
            Assert.AreSame(source.ApprovedPublicCourses, local.ApprovedPublicCourses);
            using (var original = new SerializedObject(source))
            using (var copied = new SerializedObject(local))
                Assert.AreSame(original.FindProperty("PlayerBody").objectReferenceValue, copied.FindProperty("PlayerBody").objectReferenceValue);
            Scene preview = default;
            try
            {
                preview = EditorSceneManager.OpenPreviewScene(AssetDatabase.GetAssetPath(entry));
                var launcher = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WordboundLauncher>(true)).Single();
                Assert.AreSame(local, launcher.Assets); Assert.IsNotNull(launcher.LobbyCamera);
                Assert.AreEqual(preview, launcher.LobbyCamera.gameObject.scene);
            }
            finally { if (preview.IsValid() && preview.isLoaded) EditorSceneManager.ClosePreviewScene(preview); }
            Assert.AreEqual(SceneSetup(sceneState), SceneSetup(EditorSceneManager.GetSceneManagerSetup()));
            foreach (var file in before) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
        }

        [Test]
        public void RepeatPreservesAuthoredLaunchAndSceneEditsAllGuidsAndSavedSourceBytes()
        {
            var entry = WordboundCourseSceneSetup.CreateAt(folder, course, source);
            var local = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(folder + "/LaunchAssets.asset");
            local.name = "James's local entry"; EditorUtility.SetDirty(local); AssetDatabase.SaveAssetIfDirty(local);
            Scene scene = default; var active = SceneManager.GetActiveScene();
            try
            {
                scene = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(entry), OpenSceneMode.Additive);
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
                camera.fieldOfView = 61;
                EditorUtility.SetDirty(camera); EditorSceneManager.MarkSceneDirty(scene);
                Assert.IsTrue(EditorSceneManager.SaveScene(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) Assert.IsTrue(EditorSceneManager.CloseScene(scene, true));
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
            var paths = new[] { folder + "/LaunchAssets.asset", folder + "/Workshop.unity" };
            var before = paths.SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, File.ReadAllBytes);
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Assert.AreEqual(entry, WordboundCourseSceneSetup.CreateAt(folder, course, source));
            CollectionAssert.AreEqual(guids, paths.Select(AssetDatabase.AssetPathToGUID));
            foreach (var file in before) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
        }

        [Test]
        public void PartialOutputsAndDirtyExistingEntriesAreRefusedWithoutReplacingThem()
        {
            string launchPath = folder + "/LaunchAssets.asset";
            var conflicting = new TextAsset("preserved partial path"); AssetDatabase.CreateAsset(conflicting, launchPath);
            byte[] before = File.ReadAllBytes(launchPath);
            Assert.Throws<InvalidOperationException>(() => WordboundCourseSceneSetup.CreateAt(folder, course, source));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(launchPath));
            Assert.IsFalse(File.Exists(folder + "/Workshop.unity"));
            Assert.IsTrue(AssetDatabase.DeleteAsset(launchPath));
            WordboundCourseSceneSetup.CreateAt(folder, course, source);
            var local = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(launchPath);
            local.name = "Unsaved designer change"; EditorUtility.SetDirty(local);
            before = File.ReadAllBytes(launchPath);
            Assert.Throws<InvalidOperationException>(() => WordboundCourseSceneSetup.CreateAt(folder, course, source));
            Assert.AreEqual("Unsaved designer change", local.name); Assert.IsTrue(EditorUtility.IsDirty(local));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(launchPath));
        }

        static string SceneSetup(SceneSetup[] scenes) => string.Join("|", scenes.Select(s => s.path + ":" + s.isLoaded + ":" + s.isActive));
    }
}
