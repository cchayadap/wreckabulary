using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class PlaySettingsFrameAuthoringTests
    {
        const string TempPrefix = "Assets/_Project/Tests/PSF-";
        string root;
        int caseNumber;
        LobbyTypographyProfile typography;

        [OneTimeSetUp]
        public void CreateOwnedTypography()
        {
            string destination = TempPrefix + Guid.NewGuid().ToString("N");
            RequireVacantFolder(destination);
            WorkspaceAssetCopies.EnsureFolder(destination);
            root = destination;
            var create = typeof(LobbyTypographySetup).GetMethod("CreateOrVerifyAt", BindingFlags.Static | BindingFlags.NonPublic);
            typography = (LobbyTypographyProfile)create.Invoke(null, new object[] { root + "/Typography.asset", root + "/Fonts", root + "/Materials" });
            Assert.IsNotNull(typography);
        }

        [OneTimeTearDown]
        public void RemoveOwnedAssets()
        {
            if (string.IsNullOrEmpty(root)) return;
            Assert.IsTrue(root.StartsWith(TempPrefix, StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(root)) Assert.IsTrue(AssetDatabase.DeleteAsset(root));
        }

        (LobbyPlayLayout Play, LobbySettingsLayout Settings, string Folder) Create()
        {
            string folder = root + "/c" + ++caseNumber;
            RequireVacantFolder(folder);
            Assert.Less(Path.GetFullPath(folder + "/Graphics/" + new string('f', 64) + ".asset.meta").Length, 240,
                "Owned asset byte checks require a short temporary path on the Windows Editor runtime.");
            return (LobbyPlaySettingsLayoutSetup.CreatePlayAt(folder + "/Play.asset", folder + "/Play.prefab", folder + "/Graphics", typography),
                LobbyPlaySettingsLayoutSetup.CreateSettingsAt(folder + "/Settings.asset", folder + "/Settings.prefab", folder + "/Graphics", typography), folder);
        }

        static void RequireVacantFolder(string path)
        {
            Assert.IsFalse(File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") ||
                AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path), "Owned temporary path is already occupied: " + path);
        }

        static void Edit(string path, Action<GameObject> change)
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try { change(contents); Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(contents, path)); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        [Test]
        public void SavedFrameEditsSurviveReopenBindingAndRepeatedMissingOnlySetup()
        {
            var entry = Create(); string folder = entry.Folder;
            Assert.AreEqual(new Vector2(1100, 600), entry.Play.CompactMaximum);
            Edit(folder + "/Play.prefab", root => {
                var view = root.GetComponent<LobbyPlayPageView>();
                view.Queues.anchoredPosition += new Vector2(13, 0);
                view.Panel.offsetMin = new Vector2(7, 9); view.Panel.offsetMax = new Vector2(-11, -13);
                view.Title.font = typography.Body; view.Title.fontSharedMaterial = typography.Body.material;
                view.Choices.GetComponent<VerticalLayoutGroup>().spacing = 19;
                view.Summary.offsetMax = new Vector2(0, 112);
                view.Title.fontSize = 35;
            });
            Edit(folder + "/Settings.prefab", root => {
                var view = root.GetComponent<LobbySettingsPageView>();
                view.Tabs.offsetMax = new Vector2(260, 0);
                ((RectTransform)view.Content.parent).offsetMin = new Vector2(284, 0);
                view.Confirmation.GetComponent<LayoutElement>().preferredHeight = 124;
                view.Title.fontSize = 31;
            });
            entry.Play.CompactMaximum = new Vector2(1000, 590);
            EditorUtility.SetDirty(entry.Play); AssetDatabase.SaveAssetIfDirty(entry.Play);
            var before = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
            string playGuid = AssetDatabase.AssetPathToGUID(folder + "/Play.prefab");
            string settingsGuid = AssetDatabase.AssetPathToGUID(folder + "/Settings.prefab");
            var play = LobbyPlaySettingsLayoutSetup.CreatePlayAt(folder + "/Play.asset", folder + "/Play.prefab", folder + "/Graphics", null);
            var settings = LobbyPlaySettingsLayoutSetup.CreateSettingsAt(folder + "/Settings.asset", folder + "/Settings.prefab", folder + "/Graphics", null);
            foreach (var file in before) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), file.Key);
            CollectionAssert.AreEquivalent(before.Keys, Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
            Assert.AreEqual(playGuid, AssetDatabase.AssetPathToGUID(folder + "/Play.prefab"));
            Assert.AreEqual(settingsGuid, AssetDatabase.AssetPathToGUID(folder + "/Settings.prefab"));
            Assert.AreEqual(new Vector2(1000, 590), play.CompactMaximum);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var pv = ((GameObject)PrefabUtility.InstantiatePrefab(play.Page.gameObject, scene)).GetComponent<LobbyPlayPageView>();
                var sv = ((GameObject)PrefabUtility.InstantiatePrefab(settings.Page.gameObject, scene)).GetComponent<LobbySettingsPageView>();
                int stale = 0, closes = 0;
                pv.Bind(() => stale++, "Old", false, play.CompactMaximum);
                pv.Bind(() => closes++, "New", false, play.CompactMaximum);
                sv.Bind(() => stale++, false); sv.Bind(() => closes++, true);
                pv.Close.onClick.Invoke(); sv.Close.onClick.Invoke();
                Assert.AreEqual(0, stale); Assert.AreEqual(2, closes);
                Assert.AreEqual(19, pv.Choices.GetComponent<VerticalLayoutGroup>().spacing);
                Assert.AreEqual(112, pv.Summary.offsetMax.y); Assert.AreEqual(35, pv.Title.fontSize);
                Assert.AreEqual(play.Page.Queues.anchoredPosition, pv.Queues.anchoredPosition);
                Assert.AreEqual("New", pv.Subtitle.text);
                Assert.AreEqual(new Vector2(7, 9), pv.Panel.offsetMin); Assert.AreEqual(new Vector2(-11, -13), pv.Panel.offsetMax);
                Assert.AreEqual(260, sv.Tabs.offsetMax.x); Assert.AreEqual(31, sv.Title.fontSize);
                Assert.AreEqual(124, sv.Confirmation.GetComponent<LayoutElement>().preferredHeight);
                Assert.IsTrue(sv.Confirmation.gameObject.activeSelf);
                sv.Bind(() => closes++, false); Assert.IsFalse(sv.Confirmation.gameObject.activeSelf);
                Assert.AreSame(play.Page.Title.font, pv.Title.font);
                Assert.AreSame(play.Page.Title.fontSharedMaterial, pv.Title.fontSharedMaterial);
                LobbyShopLayoutSetup.ValidatePersistent(play.Page); LobbyShopLayoutSetup.ValidatePersistent(settings.Page);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void InvalidOrAliasedRolesAndUnsafeSizingAreRejected()
        {
            var entry = Create();
            var sourceBytes = new[] { "/Play.prefab", "/Play.prefab.meta", "/Settings.prefab", "/Settings.prefab.meta" }
                .ToDictionary(suffix => entry.Folder + suffix, suffix => File.ReadAllBytes(entry.Folder + suffix));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var pv = ((GameObject)PrefabUtility.InstantiatePrefab(entry.Play.Page.gameObject, scene)).GetComponent<LobbyPlayPageView>();
                PrefabUtility.UnpackPrefabInstance(pv.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(pv.gameObject));
                var originalChoices = pv.Choices;
                pv.Choices = pv.Summary; Assert.Throws<InvalidOperationException>(pv.Validate);
                pv.Choices = originalChoices;
                var originalSubtitle = pv.Subtitle;
                pv.Subtitle = pv.Title; Assert.Throws<InvalidOperationException>(pv.Validate);
                pv.Subtitle = originalSubtitle; pv.Validate();
                pv.Summary.SetParent(pv.Choices, false);
                Assert.AreSame(pv.Choices, pv.Summary.parent);
                Assert.Throws<InvalidOperationException>(pv.Validate);
                var sv = ((GameObject)PrefabUtility.InstantiatePrefab(entry.Settings.Page.gameObject, scene)).GetComponent<LobbySettingsPageView>();
                PrefabUtility.UnpackPrefabInstance(sv.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(sv.gameObject));
                var originalCloseParent = sv.Close.transform.parent;
                sv.Close.transform.SetParent(sv.Confirmation, false);
                Assert.AreSame(sv.Confirmation, sv.Close.transform.parent);
                Assert.Throws<InvalidOperationException>(sv.Validate);
                sv.Close.transform.SetParent(originalCloseParent, false);
                sv.Confirmation.GetComponent<LayoutElement>().preferredHeight = float.NaN;
                Assert.Throws<InvalidOperationException>(sv.Validate);
                var invalid = Object.Instantiate(entry.Play);
                try
                {
                    foreach (var size in new[] { new Vector2(float.PositiveInfinity, 600), new Vector2(899, 600), new Vector2(1100, 579) })
                    { invalid.CompactMaximum = size; Assert.Throws<InvalidOperationException>(invalid.Validate); }
                }
                finally { Object.DestroyImmediate(invalid); }
            }
            finally
            {
                try { EditorSceneManager.ClosePreviewScene(scene); }
                finally { foreach (var source in sourceBytes) CollectionAssert.AreEqual(source.Value, File.ReadAllBytes(source.Key), source.Key); }
            }
        }

        [Test]
        public void InvalidPathAndOccupiedDestinationAreRefusedWithoutWrites()
        {
            Assert.Throws<ArgumentException>(() => LobbyPlaySettingsLayoutSetup.CreatePlayAt(root + "/../Escape.asset", root + "/None.prefab", root + "/Graphics", typography));
            string occupied = root + "/Occupied.asset";
            var material = new Material(typography.Body.material);
            AssetDatabase.CreateAsset(material, occupied);
            byte[] before = File.ReadAllBytes(occupied), meta = File.ReadAllBytes(occupied + ".meta");
            string prefab = root + "/MustNotExist.prefab";
            Assert.Throws<IOException>(() => LobbyPlaySettingsLayoutSetup.CreateSettingsAt(occupied, prefab, root + "/UnusedGraphics", typography));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(occupied)); CollectionAssert.AreEqual(meta, File.ReadAllBytes(occupied + ".meta"));
            Assert.IsFalse(File.Exists(prefab)); Assert.IsFalse(AssetDatabase.IsValidFolder(root + "/UnusedGraphics"));
        }
    }
}
