using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoringPersistenceTests
    {
        const string SessionPrefix = "Wordbound.Authoring.Playtest.";
        string folder;
        [SetUp] public void CreateFolder()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Close the authoring playtest before running persistence checks.");
            folder = "Assets/__WordboundAuthoring_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }
        [TearDown] public void DeleteFolder()
        {
            if (folder != null && folder.StartsWith("Assets/__WordboundAuthoring_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void SavedCourseProfilesPrefabAndMaterialsReloadIntoTheExactPreviewAndPlayContent()
        {
            var camera = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            camera.FieldOfView = 67; camera.Distance = 4.5f; camera.Validate();
            AssetDatabase.CreateAsset(camera, folder + "/Camera.asset");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader, "The project URP shader must be imported before this native fixture.");
            var material = new Material(shader) { color = new Color(.12f, .47f, .81f) };
            AssetDatabase.CreateAsset(material, folder + "/Blue.mat");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject prefab;
            try
            {
                Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = material;
                var grip = new GameObject("Grip_R"); grip.transform.SetParent(model.transform, false);
                grip.transform.localPosition = new Vector3(.1f, 0, .1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(model, folder + "/Model.prefab");
            }
            finally { Object.DestroyImmediate(model); }
            var visual = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            visual.Overrides.Add(new WordboundVisualOverride { VisualKey = "STOOL", Model = prefab, Materials = new[] { material } });
            visual.Validate(); AssetDatabase.CreateAsset(visual, folder + "/Visual.asset");
            var course = ScriptableObject.CreateInstance<WordboundCourseAsset>();
            course.Initialize(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue());
            course.Items = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            course.RuleBook = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/rules.json");
            course.CameraProfile = camera; course.VisualProfile = visual; course.Seed = "0123456789abcdef";
            var document = course.ReadDocument(); document.Title = "Saved Inspector course"; course.SaveDocument(document);
            AssetDatabase.CreateAsset(course, folder + "/Course.asset");
            foreach (var saved in new Object[] { camera, material, visual, course }) { EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved); }
            string inputHash = course.InputHash, sourceHash = course.SourceHash;
            var resolved = course.Resolve();
            string manifestJson = CourseJson.ExportManifest(resolved), manifestHash = resolved.Hash;
            string contentHash = course.CreateContent(resolved).CatalogueHash;
            Assert.That(new FileInfo(folder + "/Course.asset").Length, Is.GreaterThan(0));

            document.Title = "UNSAVED runtime mutation"; course.SaveDocument(document); camera.FieldOfView = 39;
            Resources.UnloadAsset(course); Resources.UnloadAsset(visual); Resources.UnloadAsset(camera);
            course = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(folder + "/Course.asset");
            Assert.AreEqual("Saved Inspector course", course.ReadDocument().Title);
            Assert.AreEqual(sourceHash, course.SourceHash); Assert.AreEqual(inputHash, course.InputHash);
            Assert.AreEqual(67, course.CameraProfile.FieldOfView); Assert.AreEqual(4.5f, course.CameraProfile.Distance);
            Assert.AreEqual(folder + "/Visual.asset", AssetDatabase.GetAssetPath(course.VisualProfile));
            Assert.AreEqual(folder + "/Model.prefab", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Model));
            Assert.AreEqual(folder + "/Blue.mat", AssetDatabase.GetAssetPath(course.VisualProfile.Overrides.Single().Materials.Single()));
            var imported = CourseJson.ImportManifest(manifestJson, course.Catalogue());
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            var playContent = course.CreateContent(imported.Manifest);
            Assert.AreEqual(manifestHash, playContent.Course.Hash); Assert.AreEqual(contentHash, playContent.CatalogueHash);
            Assert.AreEqual(manifestHash, course.Resolve().Hash, "Reload must resolve the same saved seed and inputs.");

            var preview = ScriptableObject.CreateInstance<WordboundCoursePreview>();
            Component previewWorld = null;
            try
            {
                typeof(WordboundCoursePreview).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(preview, new object[] { course, imported.Manifest });
                Assert.AreEqual("", typeof(WordboundCoursePreview).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview));
                previewWorld = (Component)typeof(WordboundCoursePreview).GetField("world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
                Assert.IsNotNull(previewWorld, "The actual preview must construct the saved manifest's geometry.");
                Assert.AreEqual(manifestHash, previewWorld.GetType().GetProperty("ManifestHash").GetValue(previewWorld));
                Assert.That(previewWorld.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(preview); }
            Assert.IsTrue(previewWorld == null, "Closing the preview must dispose its generated world.");

            var parent = new GameObject("Reloaded visual check");
            try
            {
                Assert.IsTrue(course.VisualProfile.TryInstantiate("STOOL", parent.transform, out var created));
                Assert.AreEqual(course.VisualProfile.Overrides[0].Materials[0], created.GetComponent<Renderer>().sharedMaterial);
                Assert.AreEqual(0, created.GetComponentsInChildren<Collider>(true).Length);
                Assert.IsNotNull(created.transform.Find("Grip_R"));
            }
            finally { Object.DestroyImmediate(parent); }
            var before = EditorSceneManager.GetSceneManagerSetup();
            Assert.Throws<ArgumentException>(() => WordboundCoursePlaytest.Start(course, imported.Manifest, new string('0', 64)));
            CollectionAssert.AreEqual(before.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false), "Rejected play setup must not claim the editor session.");
        }

        [Test]
        public void RestorationRetainsSavedSceneBesideUntitledAdditiveSceneAndOriginalActiveSelection()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                Assert.IsFalse(scene.isDirty, "Run scene restoration checks from a clean editor setup.");
                Assert.IsTrue(scene.path != "" || scene.rootCount == 0, "Save populated untitled scenes before this native check.");
            }
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            try
            {
                var saved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = folder + "/Saved.unity";
                Assert.IsTrue(EditorSceneManager.SaveScene(saved, path));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(saved);
                string records = "{\"scenes\":[{\"path\":\"" + path + "\",\"loaded\":true,\"active\":true}," +
                    "{\"path\":\"\",\"loaded\":true,\"active\":false}]}";
                SessionState.SetString(SessionPrefix + "scenes", records);
                SessionState.SetBool(SessionPrefix + "active", true);
                InvokeRestore();
                Assert.AreEqual(2, SceneManager.sceneCount);
                Assert.AreEqual(path, SceneManager.GetSceneAt(0).path);
                Assert.AreEqual("", SceneManager.GetSceneAt(1).path);
                Assert.AreEqual(path, SceneManager.GetActiveScene().path);
                Assert.IsFalse(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                SessionState.SetString(SessionPrefix + "scenes", JsonUtility.ToJson(new SavedSetup {
                    scenes = previous.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() }));
                SessionState.SetBool(SessionPrefix + "active", true);
                InvokeRestore();
                SessionState.SetString(SessionPrefix + "scenes", oldScenes);
            }
        }

        [Test]
        public void MissingSavedSceneKeepsCurrentSetupAndRecoveryRecord()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            string oldScenes = SessionState.GetString(SessionPrefix + "scenes", "");
            string missing = "{\"scenes\":[{\"path\":\"" + folder + "/Missing.unity\",\"loaded\":true,\"active\":true}]}";
            try
            {
                SessionState.SetString(SessionPrefix + "scenes", missing);
                SessionState.SetBool(SessionPrefix + "active", true);
                Assert.Throws<InvalidOperationException>(InvokeRestore);
                CollectionAssert.AreEqual(previous.Select(s => s.path).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.path).ToArray());
                CollectionAssert.AreEqual(previous.Select(s => s.isActive).ToArray(), EditorSceneManager.GetSceneManagerSetup().Select(s => s.isActive).ToArray());
                Assert.AreEqual(missing, SessionState.GetString(SessionPrefix + "scenes", ""));
                Assert.IsTrue(SessionState.GetBool(SessionPrefix + "active", false));
            }
            finally
            {
                SessionState.SetString(SessionPrefix + "scenes", oldScenes);
                SessionState.EraseBool(SessionPrefix + "active");
            }
        }
        [Serializable] sealed class SavedSetup { public SavedScene[] scenes; }
        [Serializable] sealed class SavedScene { public string path; public bool loaded, active; }
        static void InvokeRestore()
        {
            try { typeof(WordboundCoursePlaytest).GetMethod("RestoreScenes", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
