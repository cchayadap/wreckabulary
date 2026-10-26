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

namespace Wreckabulary.Tests.Editor
{
    public sealed class LoadoutLayoutAuthoringTests
    {
        string root;
        LobbyTypographyProfile typography;
        LobbyShopLayout seed;

        [OneTimeSetUp]
        public void CreateOwnedSeed()
        {
            root = "Assets/_Project/Tests/LoadoutLayoutTemp-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(root);
            var setup = typeof(LobbyTypographySetup).GetMethod("CreateOrVerifyAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(setup);
            typography = setup.Invoke(null, new object[] { root + "/Typography.asset", root + "/Fonts", root + "/TextMaterials" }) as LobbyTypographyProfile;
            Assert.IsNotNull(typography);
            seed = LobbyShopLayoutSetup.CreateAt(root + "/Shop.asset", root + "/ShopPrefabs", root + "/Graphics", typography);
        }

        [OneTimeTearDown]
        public void RemoveOnlyOwnedAssets()
        {
            if (string.IsNullOrEmpty(root)) return;
            Assert.IsTrue(root.StartsWith("Assets/_Project/Tests/LoadoutLayoutTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(root)) Assert.IsTrue(AssetDatabase.DeleteAsset(root));
        }

        LobbyLoadoutLayout Create(out string folder)
        {
            folder = root + "/Case-" + Guid.NewGuid().ToString("N");
            return LobbyLoadoutLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", seed);
        }

        static (string Guid, long FileId) Identity(UnityEngine.Object asset)
        {
            Assert.IsTrue(asset && EditorUtility.IsPersistent(asset));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId));
            Assert.IsNotEmpty(guid); Assert.AreNotEqual(0L, fileId);
            return (guid, fileId);
        }

        [Test]
        public void SavedPageAndRecipeEditsSurviveReopenBindingAndRepeatedSetup()
        {
            var layout = Create(out string folder);
            string pagePath = AssetDatabase.GetAssetPath(layout.Page), tilePath = AssetDatabase.GetAssetPath(layout.Recipe);
            var material = new Material(typography.Body.material) { name = "Authored Loadout label" };
            var face = new Color(.72f, .18f, .32f, 1);
            Assert.IsTrue(material.HasProperty("_FaceColor"));
            material.SetColor("_FaceColor", face);
            string materialPath = folder + "/Finish.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var fontId = Identity(typography.Body); var materialId = Identity(material);
            byte[] materialBytes = File.ReadAllBytes(materialPath);
            var pageRoot = PrefabUtility.LoadPrefabContents(pagePath);
            try
            {
                var page = pageRoot.GetComponent<LobbyLoadoutPageView>();
                page.Panel.offsetMin = new Vector2(-660, 7);
                page.RecipeGrid.cellSize = new Vector2(136, 128);
                page.RecipeGrid.spacing = new Vector2(15, 17);
                page.RecipeDetail.offsetMin = new Vector2(9, 61);
                page.Finish.Label.font = typography.Body; page.Finish.Label.fontSharedMaterial = material;
                page.Finish.Label.enableAutoSizing = false; page.Finish.Label.fontSize = 21;
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(pageRoot, pagePath));
            }
            finally { PrefabUtility.UnloadPrefabContents(pageRoot); }
            var tileRoot = PrefabUtility.LoadPrefabContents(tilePath);
            try
            {
                var tile = tileRoot.GetComponent<LobbyRecipeTileView>();
                tile.Icon.rectTransform.anchoredPosition = new Vector2(3, -13);
                tile.Icon.rectTransform.sizeDelta = new Vector2(70, 68);
                tile.MaximumLetterSize = 26; tile.LetterWidthBudget = 124;
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(tileRoot, tilePath));
            }
            finally { PrefabUtility.UnloadPrefabContents(tileRoot); }
            foreach (string path in new[] { pagePath, tilePath, materialPath })
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] pageBytes = File.ReadAllBytes(pagePath), tileBytes = File.ReadAllBytes(tilePath);
            layout = LobbyLoadoutLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", null);
            CollectionAssert.AreEqual(pageBytes, File.ReadAllBytes(pagePath));
            CollectionAssert.AreEqual(tileBytes, File.ReadAllBytes(tilePath));
            CollectionAssert.AreEqual(materialBytes, File.ReadAllBytes(materialPath));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var page = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Page.gameObject, preview)).GetComponent<LobbyLoadoutPageView>();
                var tile = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Recipe.gameObject, preview)).GetComponent<LobbyRecipeTileView>();
                page.Bind(true, () => { }, _ => { }, () => { });
                tile.Bind("BAT", true, () => { }, _ => { });
                Assert.AreEqual(new Vector2(-660, 7), page.Panel.offsetMin);
                Assert.AreEqual(new Vector2(136, 128), page.RecipeGrid.cellSize);
                Assert.AreEqual(new Vector2(15, 17), page.RecipeGrid.spacing);
                Assert.AreEqual(new Vector2(9, 61), page.RecipeDetail.offsetMin);
                Assert.AreEqual(fontId, Identity(page.Finish.Label.font));
                Assert.AreEqual(materialId, Identity(page.Finish.Label.fontSharedMaterial));
                Assert.That(page.Finish.Label.fontSharedMaterial.GetColor("_FaceColor"), Is.EqualTo(face).Using(ColorEqualityComparer.Instance));
                Assert.AreEqual(21, page.Finish.Label.fontSize);
                Assert.AreEqual(new Vector2(3, -13), tile.Icon.rectTransform.anchoredPosition);
                Assert.AreEqual(new Vector2(70, 68), tile.Icon.rectTransform.sizeDelta);
                Assert.AreEqual(26, tile.MaximumLetterSize); Assert.AreEqual(124, tile.LetterWidthBudget);
                Assert.AreEqual(3, tile.Letters.childCount);
                LobbyShopLayoutSetup.ValidatePersistent(layout.Page); LobbyShopLayoutSetup.ValidatePersistent(layout.Recipe);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void RebindingReplacesOwnedCloseTabFinishPinAndHoverCallbacks()
        {
            var layout = Create(out _);
            var preview = EditorSceneManager.NewPreviewScene();
            int stale = 0, close = 0, locker = 0, recipes = 0, finish = 0, pin = 0, hover = 0;
            try
            {
                var page = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Page.gameObject, preview)).GetComponent<LobbyLoadoutPageView>();
                var tile = ((GameObject)PrefabUtility.InstantiatePrefab(layout.Recipe.gameObject, preview)).GetComponent<LobbyRecipeTileView>();
                page.Bind(false, () => stale++, _ => stale++, () => stale++);
                page.Bind(true, () => close++, on => { if (on) recipes++; else locker++; }, () => finish++);
                tile.Bind("BAT", false, () => stale++, _ => stale++);
                tile.Bind("SHIELD", true, () => pin++, _ => hover++);
                page.Close.onClick.Invoke(); page.LockerTab.Button.onClick.Invoke(); page.RecipesTab.Button.onClick.Invoke();
                page.Finish.Button.onClick.Invoke(); tile.Button.onClick.Invoke(); tile.Press.Hot?.Invoke(true); tile.Press.Hot?.Invoke(false);
                Assert.AreEqual(0, stale);
                Assert.AreEqual(1, close); Assert.AreEqual(1, locker); Assert.AreEqual(1, recipes);
                Assert.AreEqual(1, finish); Assert.AreEqual(1, pin); Assert.AreEqual(2, hover);
                Assert.AreEqual(6, tile.Letters.childCount);
                Assert.IsFalse(page.LockerBody.gameObject.activeSelf); Assert.IsTrue(page.RecipesBody.gameObject.activeSelf);
                Assert.IsFalse(page.Finish.transform.IsChildOf(page.RecipeDetail));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void FinishInsideDynamicDetailIsRejectedWithoutRewritingAuthoredContent()
        {
            var layout = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(layout.Page);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page = contents.GetComponent<LobbyLoadoutPageView>();
                page.Finish.transform.SetParent(page.RecipeDetail, false);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => LobbyLoadoutLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", seed));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void SeedingPreservesOriginalShopAssetsOpenSceneAndPlayerSave()
        {
            string[] seeds = { AssetDatabase.GetAssetPath(seed), AssetDatabase.GetAssetPath(seed.Page), AssetDatabase.GetAssetPath(seed.Offer) };
            var paths = AssetDatabase.GetDependencies(seeds, true).Where(path => File.Exists(path) &&
                (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))).ToArray();
            Assert.IsTrue(seeds.All(paths.Contains));
            var bytes = paths.Select(File.ReadAllBytes).ToArray();
            var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty; int count = SceneManager.sceneCount;
            var roots = scene.GetRootGameObjects();
            string[] keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
            var prefs = keys.Select(key => (exists: PlayerPrefs.HasKey(key), value: PlayerPrefs.GetString(key))).ToArray();
            Create(out string folder);
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]));
            Assert.AreEqual(scene.handle, SceneManager.GetActiveScene().handle);
            Assert.AreEqual(dirty, scene.isDirty); Assert.AreEqual(count, SceneManager.sceneCount);
            CollectionAssert.AreEquivalent(roots, scene.GetRootGameObjects());
            for (int i = 0; i < keys.Length; i++)
            { Assert.AreEqual(prefs[i].exists, PlayerPrefs.HasKey(keys[i])); Assert.AreEqual(prefs[i].value, PlayerPrefs.GetString(keys[i])); }
            Assert.IsEmpty(AssetDatabase.FindAssets("t:Scene", new[] { folder }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DynamicDetailCannotOwnRecipeGridOrScroll(bool containsScroll)
        {
            var layout = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(layout.Page);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page = contents.GetComponent<LobbyLoadoutPageView>();
                page.RecipeDetail = containsScroll ? (RectTransform)page.RecipeScroll.transform : (RectTransform)page.RecipeGrid.transform;
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => LobbyLoadoutLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", seed));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DynamicLettersCannotOwnTheIconOrMotionBody(bool containsMotion)
        {
            var layout = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(layout.Recipe);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var tile = contents.GetComponent<LobbyRecipeTileView>();
                if (containsMotion)
                {
                    tile.Letters = (RectTransform)tile.transform;
                    tile.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                }
                else tile.Icon.transform.SetParent(tile.Letters, false);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => LobbyLoadoutLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", seed));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
