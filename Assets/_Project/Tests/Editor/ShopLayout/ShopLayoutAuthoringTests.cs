using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class ShopLayoutAuthoringTests
    {
        string root;
        LobbyTypographyProfile typography;

        [OneTimeSetUp]
        public void CreateOwnedTypography()
        {
            root = "Assets/_Project/Tests/ShopLayoutTemp-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(root);
            var setup = typeof(LobbyTypographySetup).GetMethod("CreateOrVerifyAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(setup, "Typography setup must expose its existing isolated test seam.");
            typography = setup.Invoke(null, new object[] { root + "/Typography.asset", root + "/Fonts", root + "/TextMaterials" }) as LobbyTypographyProfile;
            Assert.IsNotNull(typography, "The isolated typography fixture must be saved successfully.");
        }

        [OneTimeTearDown]
        public void RemoveOnlyOwnedAssets()
        {
            if (string.IsNullOrEmpty(root)) return;
            Assert.IsTrue(root.StartsWith("Assets/_Project/Tests/ShopLayoutTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(root)) Assert.IsTrue(AssetDatabase.DeleteAsset(root), "Remove only this fixture's unique asset tree.");
        }

        LobbyShopLayout Create(out string folder)
        {
            folder = root + "/Case-" + Guid.NewGuid().ToString("N");
            return LobbyShopLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", typography);
        }

        static (string Guid, long FileId) AssetIdentity(Object asset)
        {
            Assert.IsTrue(asset && EditorUtility.IsPersistent(asset), "The authored reference must remain a saved asset.");
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId));
            Assert.IsNotEmpty(guid);
            Assert.AreNotEqual(0L, fileId);
            return (guid, fileId);
        }

        [Test]
        public void SavedAuthoredGeometryFontsAndMaterialsSurviveReopenBindAndRepeatedSetup()
        {
            var layout = Create(out string folder);
            string pagePath = AssetDatabase.GetAssetPath(layout.Page);
            string offerPath = AssetDatabase.GetAssetPath(layout.Offer);
            var material = new Material(typography.Body.material) { name = "Authored title material" };
            var authoredFace = new Color(.82f, .17f, .37f, 1);
            Assert.IsTrue(material.HasProperty("_FaceColor"));
            material.SetColor("_FaceColor", authoredFace);
            string materialPath = folder + "/AuthoredTitle.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var materialIdentity = AssetIdentity(material);
            var fontIdentity = AssetIdentity(typography.Body);
            byte[] materialBytes = File.ReadAllBytes(materialPath);
            var pageRoot = PrefabUtility.LoadPrefabContents(pagePath);
            try
            {
                var page = pageRoot.GetComponent<LobbyShopPageView>();
                page.Panel.offsetMin = new Vector2(-932, 17);
                page.Offers.cellSize = new Vector2(281, 302);
                page.Offers.spacing = new Vector2(19, 23);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(pageRoot, pagePath));
            }
            finally { PrefabUtility.UnloadPrefabContents(pageRoot); }
            var offerRoot = PrefabUtility.LoadPrefabContents(offerPath);
            try
            {
                var offer = offerRoot.GetComponent<LobbyShopOfferView>();
                offer.Art.offsetMin = new Vector2(18, -171);
                offer.Title.font = typography.Body;
                offer.Title.fontSharedMaterial = material;
                offer.Title.enableAutoSizing = false;
                offer.Title.fontSize = 29;
                offer.Title.rectTransform.offsetMin = new Vector2(19, -213);
                offer.PreviewColour = new Color(.8f, .2f, .4f, 1);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(offerRoot, offerPath));
            }
            finally { PrefabUtility.UnloadPrefabContents(offerRoot); }
            AssetDatabase.ImportAsset(pagePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(offerPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] pageBytes = File.ReadAllBytes(pagePath), offerBytes = File.ReadAllBytes(offerPath);
            layout = LobbyShopLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", typography);
            CollectionAssert.AreEqual(pageBytes, File.ReadAllBytes(pagePath));
            CollectionAssert.AreEqual(offerBytes, File.ReadAllBytes(offerPath));
            CollectionAssert.AreEqual(materialBytes, File.ReadAllBytes(materialPath));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var pageInstance = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Page.gameObject, preview)).GetComponent<LobbyShopPageView>();
                var offerInstance = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Offer.gameObject, preview)).GetComponent<LobbyShopOfferView>();
                pageInstance.Bind(ShopPage.Looks, () => { }, _ => { }, "Sample");
                offerInstance.Bind("look:test", "Sample", "Description", false, false, 40, true, () => { }, () => { });
                Assert.AreEqual(new Vector2(-932, 17), pageInstance.Panel.offsetMin);
                Assert.AreEqual(new Vector2(281, 302), pageInstance.Offers.cellSize);
                Assert.AreEqual(new Vector2(19, 23), pageInstance.Offers.spacing);
                Assert.AreEqual(new Vector2(18, -171), offerInstance.Art.offsetMin);
                Assert.AreEqual(new Vector2(19, -213), offerInstance.Title.rectTransform.offsetMin);
                Assert.AreEqual(fontIdentity, AssetIdentity(offerInstance.Title.font));
                Assert.AreEqual(materialIdentity, AssetIdentity(offerInstance.Title.fontSharedMaterial));
                Assert.That(offerInstance.Title.fontSharedMaterial.GetColor("_FaceColor"),
                    Is.EqualTo(authoredFace).Using(ColorEqualityComparer.Instance));
                Assert.AreEqual(29, offerInstance.Title.fontSize);
                Assert.AreEqual(offerInstance.PreviewColour, offerInstance.Outline.color);
                LobbyShopLayoutSetup.ValidatePersistent(layout.Page);
                LobbyShopLayoutSetup.ValidatePersistent(layout.Offer);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void RebindingDoesNotDuplicatePurchasePreviewCategoryOrCloseCallbacks()
        {
            var layout = Create(out _);
            var previewScene = EditorSceneManager.NewPreviewScene();
            int previous = 0, previews = 0, purchases = 0, categories = 0, closes = 0;
            try
            {
                var page = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Page.gameObject, previewScene)).GetComponent<LobbyShopPageView>();
                var offer = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Offer.gameObject, previewScene)).GetComponent<LobbyShopOfferView>();
                page.Bind(ShopPage.Looks, () => previous++, _ => previous++, null);
                page.Bind(ShopPage.Gear, () => closes++, _ => categories++, null);
                offer.Bind("look:a", "Old", "", false, false, 50, false, () => previous++, () => previous++);
                offer.Bind("look:b", "New", "", false, false, 75, true, () => previews++, () => purchases++);
                page.Close.onClick.Invoke();
                page.Categories.Single(binding => binding.Id == ShopPage.Gear).Action.Button.onClick.Invoke();
                offer.Preview.onClick.Invoke();
                offer.Action.Button.onClick.Invoke();
                Assert.AreEqual(0, previous);
                Assert.AreEqual(1, closes); Assert.AreEqual(1, categories);
                Assert.AreEqual(1, previews); Assert.AreEqual(1, purchases);
                offer.Bind("look:b", "New", "", true, true, 0, false, () => previews++, () => purchases++);
                Assert.IsFalse(offer.Action.Button.interactable);
                offer.Action.Button.onClick.Invoke();
                Assert.AreEqual(1, purchases, "An equipped action has no purchase/equip callback even if invoked directly.");
            }
            finally { EditorSceneManager.ClosePreviewScene(previewScene); }
        }

        [Test]
        public void MalformedAuthoredBindingFailsWithoutRepairingOrReplacingThePrefab()
        {
            var layout = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(layout.Page);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<LobbyShopPageView>().Footnote = null;
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => LobbyShopLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", typography));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void CreatingSavedViewsPreservesOpenSceneAndPlayerSaveState()
        {
            var scene = SceneManager.GetActiveScene();
            bool dirty = scene.isDirty;
            int count = SceneManager.sceneCount;
            var originalRoots = scene.GetRootGameObjects();
            string[] keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyThemes.PreferenceKey };
            var values = keys.Select(key => (exists: PlayerPrefs.HasKey(key), value: PlayerPrefs.GetString(key))).ToArray();
            Create(out string folder);
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(dirty, scene.isDirty);
            Assert.AreEqual(count, SceneManager.sceneCount);
            CollectionAssert.AreEquivalent(originalRoots, scene.GetRootGameObjects());
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.AreEqual(values[i].exists, PlayerPrefs.HasKey(keys[i]));
                Assert.AreEqual(values[i].value, PlayerPrefs.GetString(keys[i]));
            }
            Assert.IsEmpty(AssetDatabase.FindAssets("t:Scene", new[] { folder }), "Temporary saved creation scenes must be removed.");
        }
    }
}
