using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public sealed class WordboundHouseholdSetupTests
    {
        static readonly MethodInfo Create = typeof(WordboundHouseholdSetup).GetMethod("CreateAt", BindingFlags.NonPublic | BindingFlags.Static);
        string sourceRoot, outputRoot;

        [SetUp]
        public void SetUp()
        {
            string token = Guid.NewGuid().ToString("N");
            sourceRoot = "ArtSource/Collections/HouseholdTest-" + token;
            outputRoot = "Assets/_Project/Tests/Editor/HouseholdTest-" + token;
            Directory.CreateDirectory(sourceRoot);
            File.Copy(WordboundHouseholdSetup.SourceRoot + "/manifest.json", sourceRoot + "/manifest.json");
            foreach (string file in Directory.GetFiles(WordboundHouseholdSetup.SourceRoot + "/Source", "*", SearchOption.AllDirectories))
            {
                string relative = file.Replace('\\', '/').Substring(WordboundHouseholdSetup.SourceRoot.Length + 1);
                string destination = sourceRoot + "/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(outputRoot) && AssetDatabase.IsValidFolder(outputRoot))
                Assert.IsTrue(AssetDatabase.DeleteAsset(outputRoot), "Delete only this test's unique output folder.");
            if (!string.IsNullOrEmpty(sourceRoot) && sourceRoot.StartsWith("ArtSource/Collections/HouseholdTest-", StringComparison.Ordinal) && Directory.Exists(sourceRoot))
                Directory.Delete(sourceRoot, true);
        }

        [Test]
        public void RepeatedSetupPreservesAuthoredOutputsAndRejectsChangedImportScale()
        {
            var active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            var roots = active.GetRootGameObjects();
            int scenes = SceneManager.sceneCount;
            Run();
            var settings = AssetDatabase.LoadAssetAtPath<WordboundHouseholdSettings>(outputRoot + "/Settings.asset");
            var prefabPaths = AssetDatabase.FindAssets("t:GameObject", new[] { outputRoot + "/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.AreEqual(8, prefabPaths.Length);
            foreach (string savedPath in prefabPaths)
            {
                string id = Path.GetFileNameWithoutExtension(savedPath);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(savedPath);
                var bounds = Wreckabulary.Art.ModelVisual.BoundsIn(saved.transform, saved);
                Assert.AreEqual(settings.items.Single(item => item.id == id).targetHeight, bounds.size.y, .002f, id + " initial height");
                Assert.AreEqual(0f, bounds.min.y, .002f, id + " base");
                Assert.AreEqual(0f, bounds.center.x, .002f, id + " X center");
                Assert.AreEqual(0f, bounds.center.z, .002f, id + " Z center");
                Assert.IsEmpty(saved.GetComponentsInChildren<Rigidbody>(true), id + " is not accepted dynamic physics");
                var colliders = saved.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(id == "cardboardBoxClosed" ? 1 : 0, colliders.Length, id + " review collision boundary");
                if (colliders.Length > 0) Assert.IsInstanceOf<BoxCollider>(colliders[0]);
            }
            var glass = AssetDatabase.LoadAssetAtPath<Material>(outputRoot + "/Art/Materials/kitchenStove/glass.mat");
            Assert.IsNotNull(glass, "Preserve the source oven glass material.");
            Assert.AreEqual(.5f, glass.GetColor("_BaseColor").a, .005f, "The supplied FBX declares half-opacity glass.");
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Transparent, glass.renderQueue);
            var parcel = AssetDatabase.LoadAssetAtPath<GameObject>(outputRoot + "/Prefabs/PackingYard/cardboardBoxClosed.prefab");
            Assert.AreEqual(1, parcel.GetComponentsInChildren<Collider>(true).Length);
            Assert.IsNull(parcel.GetComponent<Rigidbody>(), "The review-only parcel must not imply tested dynamic physics.");
            string prefabPath = outputRoot + "/Prefabs/Kitchen/kitchenStove.prefab";
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.name = "My authored stove";
                contents.transform.GetChild(0).localPosition += new Vector3(.1f, .2f, .3f);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, prefabPath));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            string materialPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:Material", new[] { outputRoot + "/Art/Materials/kitchenStove" }).First());
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            material.SetColor("_BaseColor", Color.magenta);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            settings.items[0].targetHeight = 1.4f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            var studio = EditorSceneManager.OpenScene(outputRoot + "/Studio.unity", OpenSceneMode.Additive);
            try
            {
                Assert.IsTrue(SceneManager.SetActiveScene(studio));
                new GameObject("My authored review marker");
                Assert.IsTrue(EditorSceneManager.SaveScene(studio));
            }
            finally
            {
                EditorSceneManager.CloseScene(studio, true);
                SceneManager.SetActiveScene(active);
            }
            var snapshot = Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
            Run();
            foreach (var pair in snapshot) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key), pair.Key);
            CollectionAssert.AreEquivalent(snapshot.Keys, Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories));
            Assert.AreEqual(active.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(dirty, active.isDirty);
            Assert.AreEqual(scenes, SceneManager.sceneCount);
            CollectionAssert.AreEqual(roots, active.GetRootGameObjects());
            Assert.AreEqual(Color.magenta, material.GetColor("_BaseColor"));
            Assert.AreEqual(1.4f, settings.items[0].targetHeight);
            string modelPath = outputRoot + "/Art/Models/kitchenStove.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.globalScale = 2f;
            importer.SaveAndReimport();
            var changedImporterBytes = File.ReadAllBytes(modelPath + ".meta");
            var preservedPrefabBytes = File.ReadAllBytes(prefabPath);
            Assert.Throws<InvalidOperationException>(Run, "Changed scale must not silently alter the normalized furniture on repeat setup.");
            Assert.AreEqual(2f, ((ModelImporter)AssetImporter.GetAtPath(modelPath)).globalScale);
            CollectionAssert.AreEqual(changedImporterBytes, File.ReadAllBytes(modelPath + ".meta"));
            CollectionAssert.AreEqual(preservedPrefabBytes, File.ReadAllBytes(prefabPath));
            Assert.AreEqual(active.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(dirty, active.isDirty);
            Assert.AreEqual(scenes, SceneManager.sceneCount);
            CollectionAssert.AreEqual(roots, active.GetRootGameObjects());
        }

        [Test]
        public void ChangedSourceBytesFailBeforeAnyProjectAssetIsCreated()
        {
            File.AppendAllText(sourceRoot + "/Source/Kenney/License.txt", "\nChanged source fixture");
            Assert.Throws<InvalidOperationException>(Run);
            Assert.IsFalse(Directory.Exists(outputRoot));
            Assert.IsFalse(File.Exists(outputRoot + ".meta"));
        }

        [Test]
        public void TraversalInManifestFailsBeforeAnyProjectAssetIsCreated()
        {
            string manifest = sourceRoot + "/manifest.json";
            string text = File.ReadAllText(manifest);
            const string sourcePath = "Source/Kenney/Models/kitchenStove.fbx";
            Assert.IsTrue(text.Contains(sourcePath));
            File.WriteAllText(manifest, text.Replace(sourcePath, "../kitchenStove.fbx"));
            Assert.Throws<InvalidOperationException>(Run);
            Assert.IsFalse(Directory.Exists(outputRoot));
        }

        [Test]
        public void DestinationTraversalIsRejectedWithoutWritingAssets()
        {
            Assert.Throws<ArgumentException>(() => Invoke(sourceRoot, outputRoot + "/../Escaped", outputRoot + "/Prefabs", outputRoot + "/Settings.asset", outputRoot + "/Studio.unity"));
            Assert.IsFalse(Directory.Exists(outputRoot));
        }

        void Run() => Invoke(sourceRoot, outputRoot + "/Art", outputRoot + "/Prefabs", outputRoot + "/Settings.asset", outputRoot + "/Studio.unity");
        static void Invoke(params string[] paths)
        {
            try { Create.Invoke(null, paths.Cast<object>().ToArray()); }
            catch (TargetInvocationException error) when (error.InnerException != null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }
    }
}
