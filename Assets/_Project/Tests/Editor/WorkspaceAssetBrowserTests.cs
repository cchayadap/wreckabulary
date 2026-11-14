using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class WorkspaceAssetBrowserTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/_Project/Tests/WorkspaceCopy_" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(folder)) Assert.IsTrue(AssetDatabase.DeleteAsset(folder));
        }

        [Test]
        public void MaterialCopyHasIndependentIdentityAndNeverOverwritesSourceOrDestination()
        {
            const string sourcePath = "Assets/_Project/Materials/Library/wood_Classic.mat";
            var original = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            Assert.NotNull(original);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            string path = folder + "/OwnWood.mat";
            var copy = (Material)WorkspaceAssetCopies.Create(original, path);
            Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(sourcePath), AssetDatabase.AssetPathToGUID(path));
            copy.SetColor("_BaseColor", Color.magenta);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            var savedCopy = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => WorkspaceAssetCopies.Create(original, path));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(sourcePath));
            CollectionAssert.AreEqual(savedCopy, File.ReadAllBytes(path));
            Assert.AreEqual(Color.magenta, AssetDatabase.LoadAssetAtPath<Material>(path).GetColor("_BaseColor"));
        }

        [Test]
        public void ModelCopyPreservesMeshAndPrefabSourceWithoutChangingOpenScene()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var original = library.Find("Items/BAT");
            Assert.NotNull(original);
            string originalPath = AssetDatabase.GetAssetPath(original);
            var sourceBytes = File.ReadAllBytes(originalPath);
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            bool dirty = scene.isDirty;
            var copy = (GameObject)WorkspaceAssetCopies.Create(original, folder + "/OwnBat.prefab");
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(copy));
            Assert.AreEqual(original, PrefabUtility.GetCorrespondingObjectFromOriginalSource(copy));
            Assert.AreEqual(original.transform.localPosition, copy.transform.localPosition);
            Assert.AreEqual(original.transform.localRotation, copy.transform.localRotation);
            Assert.AreEqual(original.transform.localScale, copy.transform.localScale);
            CollectionAssert.AreEqual(original.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh),
                copy.GetComponentsInChildren<MeshFilter>(true).Select(value => value.sharedMesh));
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(dirty, scene.isDirty);
            CollectionAssert.AreEqual(roots, scene.GetRootGameObjects());
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(originalPath));
        }

        [TestCase("Assets/_Project/../outside.mat")]
        [TestCase("Assets/_Projectish/outside.mat")]
        [TestCase("Assets/_Project/own.prefab")]
        public void CopyRejectsInvalidDestinationsBeforeCreatingFiles(string path)
        {
            Assert.Throws<ArgumentException>(() => WorkspaceAssetCopies.ValidateDestination(path, ".mat"));
        }

        [Test]
        public void BrowserFindsOriginalWeaponsRigPiecesFontsAndClipsWithoutChangingTheirIdentities()
        {
            var entries = WorkspaceAssetBrowser.ReadEntries();
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var bat = entries.Single(entry => entry.Detail == "Items/BAT" && entry.Category == "Weapons");
            Assert.AreSame(library.Find("Items/BAT"), bat.Asset);
            Assert.IsTrue(entries.Any(entry => entry.Category == "Accessories" && entry.Name == "Boots" && entry.Asset is Mesh));
            Assert.IsTrue(entries.Any(entry => entry.Category == "Fonts" && entry.Asset is Font));
            Assert.IsTrue(entries.Any(entry => entry.Category == "Animation" && entry.Asset is AnimationClip));
            Assert.IsTrue(entries.Where(entry => entry.Category == "Animation").All(entry => entry.Asset is AnimationClip));
            Assert.IsTrue(entries.Where(entry => entry.Category == "Materials").All(entry => entry.Asset is Material));
            Assert.IsTrue(entries.All(entry => EditorUtility.IsPersistent(entry.Asset)));
        }
    }
}
