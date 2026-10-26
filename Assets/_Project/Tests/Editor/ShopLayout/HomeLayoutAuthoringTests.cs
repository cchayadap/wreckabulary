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
using UnityEngine.UI;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public sealed class HomeLayoutAuthoringTests
    {
        string root;
        LobbyTypographyProfile typography;

        [OneTimeSetUp]
        public void CreateOwnedTypography()
        {
            root = "Assets/_Project/Tests/HomeLayoutTemp-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(root);
            var setup = typeof(LobbyTypographySetup).GetMethod("CreateOrVerifyAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(setup);
            typography = setup.Invoke(null, new object[] { root + "/Typography.asset", root + "/Fonts", root + "/TextMaterials" }) as LobbyTypographyProfile;
            Assert.IsNotNull(typography);
        }

        [OneTimeTearDown]
        public void RemoveOnlyOwnedAssets()
        {
            if (string.IsNullOrEmpty(root)) return;
            Assert.IsTrue(root.StartsWith("Assets/_Project/Tests/HomeLayoutTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(root)) Assert.IsTrue(AssetDatabase.DeleteAsset(root));
        }

        LobbyHomeLayout Create(out string folder)
        {
            folder = root + "/Case-" + Guid.NewGuid().ToString("N");
            return LobbyHomeLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", typography);
        }

        static (string Guid, long FileId) Identity(UnityEngine.Object asset)
        {
            Assert.IsTrue(asset && EditorUtility.IsPersistent(asset));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId));
            Assert.IsNotEmpty(guid); Assert.AreNotEqual(0L, fileId);
            return (guid, fileId);
        }

        [Test]
        public void SavedHomeEditsSurviveReopenBindingAndRepeatedSetup()
        {
            var profile = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(profile.Page);
            var material = new Material(typography.Body.material) { name = "Authored Home mode" };
            var color = new Color(.64f, .21f, .32f, 1);
            material.SetColor("_FaceColor", color);
            string materialPath = folder + "/Mode.mat";
            AssetDatabase.CreateAsset(material, materialPath);
            var fontId = Identity(typography.Body); var materialId = Identity(material);
            byte[] materialBytes = File.ReadAllBytes(materialPath);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page = contents.GetComponent<LobbyHomePageView>();
                page.Dock.anchoredPosition = new Vector2(-22, 19);
                page.Dock.sizeDelta = new Vector2(340, page.Dock.sizeDelta.y);
                page.TurnHint.anchoredPosition = new Vector2(11, 12);
                page.Start.GetComponent<LayoutElement>().preferredHeight = 72;
                page.Mode.Label.font = typography.Body; page.Mode.Label.fontSharedMaterial = material;
                page.Mode.Label.enableAutoSizing = false; page.Mode.Label.fontSize = 25;
                page.Start.Label.text = "PLAY";
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            foreach (string assetPath in new[] { path, materialPath })
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] pageBytes = File.ReadAllBytes(path), profileBytes = File.ReadAllBytes(folder + "/Layout.asset");
            profile = LobbyHomeLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", null);
            CollectionAssert.AreEqual(pageBytes, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(profileBytes, File.ReadAllBytes(folder + "/Layout.asset"));
            CollectionAssert.AreEqual(materialBytes, File.ReadAllBytes(materialPath));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var page = ((GameObject)PrefabUtility.InstantiatePrefab(profile.Page.gameObject, preview)).GetComponent<LobbyHomePageView>();
                var first = page.Bind(true, false, false, true, true, "Double trouble", "My house", () => { }, () => { }, () => { }, () => { });
                Assert.AreSame(page.Start.Button, first);
                Assert.AreEqual(new Vector2(-22, 19), page.Dock.anchoredPosition);
                Assert.AreEqual(340, page.Dock.sizeDelta.x);
                Assert.AreEqual(new Vector2(11, 12), page.TurnHint.anchoredPosition);
                Assert.AreEqual(72, page.Start.GetComponent<LayoutElement>().preferredHeight);
                Assert.AreEqual("PLAY", page.Start.Label.text);
                Assert.AreEqual("Double trouble", page.Mode.Label.text); Assert.AreEqual("My house", page.House.Label.text);
                Assert.AreEqual(fontId, Identity(page.Mode.Label.font)); Assert.AreEqual(materialId, Identity(page.Mode.Label.fontSharedMaterial));
                Assert.That(page.Mode.Label.fontSharedMaterial.GetColor("_FaceColor"), Is.EqualTo(color).Using(ColorEqualityComparer.Instance));
                Assert.AreEqual(25, page.Mode.Label.fontSize);
                LobbyShopLayoutSetup.ValidatePersistent(profile.Page);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [Test]
        public void RebindingReplacesCallbacksAndDisablesUnavailableActions()
        {
            var profile = Create(out _);
            var preview = EditorSceneManager.NewPreviewScene();
            int stale = 0, start = 0, mode = 0, house = 0, wordbound = 0;
            try
            {
                var page = ((GameObject)PrefabUtility.InstantiatePrefab(profile.Page.gameObject, preview)).GetComponent<LobbyHomePageView>();
                page.Bind(true, false, false, true, true, "Old", "Old", () => stale++, () => stale++, () => stale++, () => stale++);
                page.Bind(true, false, false, true, true, "Dibs!", "House", () => start++, () => mode++, () => house++, () => wordbound++);
                page.Start.Button.onClick.Invoke(); page.Mode.Button.onClick.Invoke(); page.House.Button.onClick.Invoke(); page.Wordbound.Button.onClick.Invoke();
                Assert.AreEqual(0, stale);
                Assert.AreEqual(1, start); Assert.AreEqual(1, mode); Assert.AreEqual(1, house); Assert.AreEqual(1, wordbound);
                var first = page.Bind(false, false, true, false, false, "Tutorial", "House", () => start++, () => mode++, () => house++, () => wordbound++);
                Assert.AreSame(page.Mode.Button, first);
                Assert.IsFalse(page.Start.Button.interactable);
                Assert.IsFalse(page.TurnHint.gameObject.activeSelf); Assert.IsFalse(page.House.gameObject.activeSelf);
                Assert.IsFalse(page.Wordbound.gameObject.activeSelf); Assert.IsFalse(page.WordboundNote.gameObject.activeSelf);
                page.Start.Button.onClick.Invoke(); page.House.Button.onClick.Invoke(); page.Wordbound.Button.onClick.Invoke();
                Assert.AreEqual(1, start); Assert.AreEqual(1, house); Assert.AreEqual(1, wordbound);
                Assert.IsNull(page.Bind(true, true, false, true, true, "Dibs!", "House", () => start++, () => mode++, () => house++, () => wordbound++));
                Assert.IsFalse(page.Dock.gameObject.activeSelf); Assert.IsTrue(page.TurnHint.gameObject.activeSelf);
                page.Start.Button.onClick.Invoke(); page.Mode.Button.onClick.Invoke(); page.House.Button.onClick.Invoke(); page.Wordbound.Button.onClick.Invoke();
                Assert.AreEqual(1, start); Assert.AreEqual(1, mode); Assert.AreEqual(1, house); Assert.AreEqual(1, wordbound);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MalformedVisibilityHierarchyIsRejectedWithoutReplacingSavedContent(bool hintOwnsDock)
        {
            var profile = Create(out string folder);
            string path = AssetDatabase.GetAssetPath(profile.Page);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page = contents.GetComponent<LobbyHomePageView>();
                if (hintOwnsDock) page.Dock.SetParent(page.TurnHint, false);
                else page.Start.transform.SetParent(page.House.transform, false);
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<InvalidOperationException>(() => LobbyHomeLayoutSetup.CreateAt(folder + "/Layout.asset", folder + "/Prefabs", folder + "/Graphics", typography));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }

        [Test]
        public void DefaultCreationPreservesTypographyOpenSceneAndPlayerPreferences()
        {
            var paths = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(typography), true)
                .Where(path => File.Exists(path) && (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))).ToArray();
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
    }
}
