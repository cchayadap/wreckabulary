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
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundKitchenPilotSetupTests
    {
        string folder, evidence;
        WordboundCourseAsset source;
        WordboundLaunchAssets launch;
        TextAsset items;
        static readonly string[] Names = { "Course.asset", "Presentation.asset", "LaunchAssets.asset", "KitchenAuthoring.unity" };

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            folder = "Assets/__WordboundKitchenSetup_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            evidence = Path.Combine(Path.GetTempPath(), "wbqa", "kitchen-setup-" + Guid.NewGuid().ToString("N"));
            source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            launch = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>("Assets/_Project/Resources/Wordbound/LaunchAssets.asset");
            items = AssetDatabase.LoadAssetAtPath<TextAsset>(WordboundKitchenPilotSetup.ItemsPath);
            Assert.IsNotNull(source); Assert.IsNotNull(launch); Assert.IsNotNull(items);
        }
        [TearDown]
        public void TearDown()
        {
            if (folder != null && folder.StartsWith("Assets/__WordboundKitchenSetup_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(folder);
            string allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wbqa")) + Path.DirectorySeparatorChar + "kitchen-setup-";
            if (evidence != null && Path.GetFullPath(evidence).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(evidence))
                Directory.Delete(evidence, true);
        }
        WordboundCourseAsset Create(string scene = null, TextAsset overrideItems = null) => WordboundKitchenPilotSetup.CreateOrSelectAt(folder,
            overrideItems ? overrideItems : items, source, launch, scene ?? WordboundLaunchAssets.LobbyPath, evidence);

        [Test]
        public void CreatesIndependentSavedCourseProfilesAndSceneWithoutChangingGlobalSourcesOrOpenScenes()
        {
            var paths = new[] { WordboundAuthoringSetup.FlagshipPath, AssetDatabase.GetAssetPath(source.VisualProfile),
                AssetDatabase.GetAssetPath(source.Items), AssetDatabase.GetAssetPath(launch), WordboundLaunchAssets.LobbyPath,
                "ProjectSettings/EditorBuildSettings.asset", WordboundKitchenPilotSetup.ItemsPath };
            var protectedFiles = paths.SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
                .Distinct().ToDictionary(p => p, File.ReadAllBytes);
            string scenes = SceneState();
            var course = Create();
            Assert.AreEqual(WordboundKitchenPilotContent.CourseId, course.ReadDocument().Id);
            Assert.AreEqual(34, course.ReadDocument().Modules.Count); Assert.AreEqual("0123456789abcdef", course.Seed);
            Assert.AreEqual("TimeRun", course.ModeId); Assert.AreEqual(RaceFormat.Solo, course.Format);
            Assert.AreSame(items, course.Items); Assert.AreSame(source.RuleBook, course.RuleBook); Assert.AreSame(source.CameraProfile, course.CameraProfile);
            Assert.AreNotSame(source.VisualProfile, course.VisualProfile);
            Assert.AreEqual(folder + "/Presentation.asset", AssetDatabase.GetAssetPath(course.VisualProfile));
            var local = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(folder + "/LaunchAssets.asset");
            Assert.AreNotSame(launch, local); Assert.AreSame(course, local.WorkshopCourse); Assert.AreSame(course.VisualProfile, local.VisualProfile);
            using (var original = new SerializedObject(launch))
            using (var copied = new SerializedObject(local))
                Assert.AreSame(original.FindProperty("PlayerBody").objectReferenceValue, copied.FindProperty("PlayerBody").objectReferenceValue);
            Assert.AreSame(launch.ApprovedPublicCourses, local.ApprovedPublicCourses);
            Assert.AreSame(launch.ParcelServiceCourse, local.ParcelServiceCourse);
            Assert.IsNotNull(course.CreateContent(course.Resolve()));
            CheckScene(folder + "/KitchenAuthoring.unity", value => Assert.AreSame(local, value.Assets));
            Assert.AreEqual(scenes, SceneState());
            foreach (var file in protectedFiles) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
            Assert.IsTrue(File.Exists(evidence + "/COMPLETE.json"));
        }

        [Test]
        public void RepeatAfterReloadPreservesSavedCourseVisualLaunchAndSceneEditsAndAllGuids()
        {
            var course = Create(); var doc = course.ReadDocument(); doc.Title = "James kitchen arrival"; doc.Spawns[0].Yaw = 90;
            course.SaveDocument(doc); course.Seed = "fedcba9876543210"; course.ModeId = "Freestyle";
            EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            course.VisualProfile.name = "James edited kitchen presentation";
            EditorUtility.SetDirty(course.VisualProfile); AssetDatabase.SaveAssetIfDirty(course.VisualProfile);
            var local = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(folder + "/LaunchAssets.asset");
            local.name = "James local workshop"; EditorUtility.SetDirty(local); AssetDatabase.SaveAssetIfDirty(local);
            Scene scene = default; var previous = SceneManager.GetActiveScene();
            try
            {
                scene = EditorSceneManager.OpenScene(folder + "/KitchenAuthoring.unity", OpenSceneMode.Additive);
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
                camera.backgroundColor = new Color(.21f, .23f, .25f); EditorUtility.SetDirty(camera);
                EditorSceneManager.MarkSceneDirty(scene); Assert.IsTrue(EditorSceneManager.SaveScene(scene));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            var files = Names.SelectMany(n => new[] { folder + "/" + n, folder + "/" + n + ".meta" }).ToDictionary(p => p, File.ReadAllBytes);
            string expectedInput = course.InputHash;
            Resources.UnloadAsset(course); AssetDatabase.ImportAsset(folder + "/Course.asset", ImportAssetOptions.ForceUpdate);
            var repeated = Create();
            Assert.AreEqual(expectedInput, repeated.InputHash); Assert.AreEqual("James kitchen arrival", repeated.ReadDocument().Title);
            Assert.AreEqual(90, repeated.ReadDocument().Spawns[0].Yaw); Assert.AreEqual("Freestyle", repeated.ModeId);
            foreach (var file in files) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
            CheckScene(folder + "/KitchenAuthoring.unity", value => Assert.LessOrEqual(
                Vector4.Distance(new Color(.21f, .23f, .25f), value.LobbyCamera.backgroundColor), 1e-5f, "Saved colour survives float serialization; complete scene/meta bytes above remain exact."));
        }

        [Test]
        public void PartialWrongTypeAndDirtyExistingOutputsAreNeverReplaced()
        {
            var wrong = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            string path = folder + "/Course.asset"; AssetDatabase.CreateAsset(wrong, path);
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            Assert.Throws<InvalidOperationException>(() => Create());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
            Assert.IsFalse(Directory.Exists(evidence)); Assert.IsFalse(File.Exists(folder + "/Presentation.asset"));
            AssetDatabase.DeleteAsset(path);
            var course = Create(); before = File.ReadAllBytes(path); string seed = course.Seed;
            course.Seed = "fedcba9876543210"; EditorUtility.SetDirty(course);
            Assert.Throws<InvalidOperationException>(() => Create()); CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            course.Seed = seed; EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
        }

        [Test]
        public void MissingKitchenCatalogueOrInvalidSceneFailsBeforeAnyOutputOrEvidence()
        {
            Assert.Throws<InvalidOperationException>(() => Create(overrideItems: source.Items));
            Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Items), folder + "/MissingKitchen.json"));
            var incomplete = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/MissingKitchen.json");
            Assert.IsNotNull(incomplete);
            Assert.Throws<ArgumentException>(() => Create(overrideItems: incomplete), "A separate file must also contain all three real furniture definitions.");
            Assert.IsFalse(Names.Any(n => File.Exists(folder + "/" + n))); Assert.IsFalse(Directory.Exists(evidence));
            Assert.Throws<InvalidOperationException>(() => Create(scene: WordboundLaunchAssets.GameplayPath));
            Assert.IsFalse(Names.Any(n => File.Exists(folder + "/" + n))); Assert.IsFalse(Directory.Exists(evidence));
        }

        static void CheckScene(string path, Action<WordboundLauncher> check)
        {
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(path);
                var value = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WordboundLauncher>(true)).Single();
                check(value);
            }
            finally { if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene); }
        }
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var scene = SceneManager.GetSceneAt(i); return scene.handle.GetRawData() + ":" + scene.path + ":" + scene.isLoaded + ":" + scene.isDirty + ":" + (scene == SceneManager.GetActiveScene()); }));
    }
}
