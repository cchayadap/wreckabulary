using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public sealed class HomeNavigationMigrationTests
    {
        string folder, prefabPath, profilePath;

        [SetUp]
        public void CreateOnlyOwnedCopy()
        {
            folder = "Assets/_Project/Tests/HomeNavigationTemp-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(folder);
            var source = AssetDatabase.LoadAssetAtPath<LobbyHomeLayout>(LobbyHomeLayoutSetup.ProfilePath);
            Assert.IsNotNull(source); source.Validate();
            prefabPath = folder + "/Home.prefab"; profilePath = folder + "/Layout.asset";
            Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Page), prefabPath));
            var profile = ScriptableObject.CreateInstance<LobbyHomeLayout>();
            profile.Page = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<LobbyHomePageView>();
            AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssetIfDirty(profile);
            Edit(page =>
            {
                page.Start.Label.text = "START"; page.Wordbound.Label.text = "WORDBOUND";
                page.WordboundNote.text = "Online parties · Map workshop";
                page.Dock.anchoredPosition = new Vector2(-33, 21);
                page.Start.Label.fontSize = 37;
                page.Start.Label.color = new Color(.14f, .34f, .26f, 1);
                page.House.Label.text = "Artist house label";
            });
        }

        [TearDown]
        public void RemoveOnlyOwnedCopy()
        {
            Assert.IsTrue(folder.StartsWith("Assets/_Project/Tests/HomeNavigationTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(folder)) Assert.IsTrue(AssetDatabase.DeleteAsset(folder));
        }

        [Test]
        public void ThreeLabelsPersistWithAllOtherSerializedFieldsAndDependenciesUnchanged()
        {
            var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty;
            var roots = scene.GetRootGameObjects();
            var original = AssetDatabase.LoadAssetAtPath<LobbyHomeLayout>(LobbyHomeLayoutSetup.ProfilePath);
            string originalPath = AssetDatabase.GetAssetPath(original.Page);
            byte[] originalBytes = File.ReadAllBytes(originalPath), originalMeta = File.ReadAllBytes(originalPath + ".meta");
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt);
            Assert.AreEqual("APPLIED; VERIFIED", receipt.status);
            Assert.IsTrue(receipt.saveAttempted && receipt.reloaded && receipt.contentsReleased && receipt.scenePreserved && receipt.dependenciesPreserved);
            Assert.AreEqual(receipt.otherFieldsBefore, receipt.otherFieldsAfter);
            Assert.IsNotEmpty(receipt.preservedFiles); Assert.IsTrue(receipt.preservedFiles.All(file => file.before == file.after));
            CollectionAssert.AreEqual(new[] { "LOCAL PLAY", "ONLINE PLAY", "Human players · Public / private" }, receipt.afterLabels);
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<LobbyHomePageView>();
            Assert.AreEqual(new Vector2(-33, 21), page.Dock.anchoredPosition);
            Assert.AreEqual(37, page.Start.Label.fontSize); Assert.AreEqual("Artist house label", page.House.Label.text);
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle); Assert.AreEqual(dirty, scene.isDirty);
            CollectionAssert.AreEqual(roots, scene.GetRootGameObjects());
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(originalPath));
            CollectionAssert.AreEqual(originalMeta, File.ReadAllBytes(originalPath + ".meta"));
            byte[] once = File.ReadAllBytes(prefabPath);
            var repeat = new LobbyHomeNavigationSetup.Receipt();
            LobbyHomeNavigationSetup.ApplyAt(profilePath, repeat);
            Assert.AreEqual("ALREADY APPLIED; NO SAVE", repeat.status);
            Assert.IsTrue(repeat.alreadyApplied); Assert.IsFalse(repeat.saveAttempted);
            CollectionAssert.AreEqual(once, File.ReadAllBytes(prefabPath));
        }

        [TestCase("My local game")]
        [TestCase("LOCAL PLAY")]
        public void CustomOrPartiallyMigratedLabelRefusesWithoutSavedChanges(string start)
        {
            Edit(page => page.Start.Label.text = start);
            byte[] before = File.ReadAllBytes(prefabPath), meta = File.ReadAllBytes(prefabPath + ".meta");
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            Assert.Throws<InvalidOperationException>(() => LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt));
            Assert.AreEqual("FAIL", receipt.status); Assert.IsFalse(receipt.saveAttempted);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(prefabPath)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(prefabPath + ".meta"));
        }

        [Test]
        public void PreExistingDirtyImportedFontIsPreservedWithoutClearingOrSaving()
        {
            var font = AddOwnedImportedFont();
            string path = AssetDatabase.GetAssetPath(font);
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            EditorUtility.SetDirty(font);
            Assert.IsTrue(EditorUtility.IsDirty(font));
            Assert.IsFalse(EditorUtility.IsDirty(AssetImporter.GetAtPath(path)));
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt);
            var proof = receipt.importedFonts.Single(entry => entry.path == path && entry.objectType == typeof(Font).FullName);
            Assert.AreEqual("APPLIED; VERIFIED", receipt.status);
            Assert.IsTrue(receipt.importedFontsPreserved && receipt.dependenciesPreserved);
            Assert.IsTrue(proof.dirtyBefore && proof.dirtyAfter && EditorUtility.IsDirty(font));
            Assert.AreEqual(proof.beforeState, proof.afterState);
            Assert.AreEqual(proof.importerBeforeState, proof.importerAfterState);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
        }

        [Test]
        public void DirtyOwnedFontImporterStillRefusesBeforeSavingThePrefab()
        {
            var font = AddOwnedImportedFont();
            EditorUtility.SetDirty(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font)));
            byte[] before = File.ReadAllBytes(prefabPath);
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            Assert.Throws<InvalidOperationException>(() => LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt));
            Assert.IsFalse(receipt.saveAttempted);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(prefabPath));
        }

        [Test]
        public void PreExistingDirtyOwnedFontTextureIsPreservedWithoutClearingOrSaving()
        {
            var font = AddOwnedImportedFont();
            string path = AssetDatabase.GetAssetPath(font);
            var texture = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().SingleOrDefault();
            Assert.IsNotNull(texture, "The copied TrueTypeFontImporter should expose its own Font Texture subasset.");
            Assert.IsTrue(AssetDatabase.IsSubAsset(texture));
            byte[] before = File.ReadAllBytes(path), meta = File.ReadAllBytes(path + ".meta");
            EditorUtility.SetDirty(texture);
            Assert.IsTrue(EditorUtility.IsDirty(texture));
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt);
            var proof = receipt.importedFonts.Single(entry => entry.path == path && entry.objectType == typeof(Texture2D).FullName);
            Assert.AreEqual("APPLIED; VERIFIED", receipt.status);
            Assert.IsTrue(receipt.importedFontsPreserved && receipt.dependenciesPreserved && proof.subAsset && !proof.mainAsset);
            Assert.IsTrue(proof.dirtyBefore && proof.dirtyAfter && EditorUtility.IsDirty(texture));
            Assert.AreEqual(proof.beforeState, proof.afterState);
            Assert.AreEqual(proof.importerBeforeState, proof.importerAfterState);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
        }

        [Test]
        public void DirtyAuthoredTextureStillRefusesBeforeSavingThePrefab()
        {
            string path = folder + "/AuthoredTexture.asset";
            var texture = new Texture2D(2, 2) { name = "Authored test texture" };
            AssetDatabase.CreateAsset(texture, path); AssetDatabase.SaveAssetIfDirty(texture);
            Edit(page =>
            {
                var child = new GameObject("Owned authored texture dependency", typeof(RectTransform));
                child.transform.SetParent(page.transform, false);
                var image = child.AddComponent<UnityEngine.UI.RawImage>(); image.enabled = false; image.texture = texture;
            });
            EditorUtility.SetDirty(texture);
            byte[] before = File.ReadAllBytes(prefabPath);
            var receipt = new LobbyHomeNavigationSetup.Receipt();
            var error = Assert.Throws<InvalidOperationException>(() => LobbyHomeNavigationSetup.ApplyAt(profilePath, receipt));
            StringAssert.Contains(path, error.Message);
            Assert.IsFalse(receipt.saveAttempted);
            Assert.IsTrue(EditorUtility.IsDirty(texture));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(prefabPath));
        }

        Font AddOwnedImportedFont()
        {
            string source = AssetDatabase.GetDependencies(profilePath, true).First(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));
            string path = folder + "/Owned.ttf";
            Assert.IsTrue(AssetDatabase.CopyAsset(source, path));
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            Assert.IsNotNull(font);
            Edit(page =>
            {
                var child = new GameObject("Owned imported font dependency", typeof(RectTransform));
                child.transform.SetParent(page.transform, false);
                var label = child.AddComponent<UnityEngine.UI.Text>(); label.enabled = false; label.font = font;
            });
            Assert.Contains(path, AssetDatabase.GetDependencies(profilePath, true));
            return font;
        }

        void Edit(Action<LobbyHomePageView> action)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try { action(contents.GetComponent<LobbyHomePageView>()); Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, prefabPath)); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
