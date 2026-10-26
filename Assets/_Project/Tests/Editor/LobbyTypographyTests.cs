using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public class LobbyTypographyTests
    {
        string root;
        LobbyTypographyProfile defaults;
        static readonly string[] FontFields = { "Display", "Body", "Black" };
        static readonly string[] MaterialFields = { "Stroke", "Drop", "DisplayForeground", "BodyForeground", "BlackForeground" };

        [OneTimeSetUp]
        public void CreateIsolatedDefaults()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) Assert.Ignore("Run typography authoring tests outside Play mode.");
            root = "Assets/_LobbyTypographyTests_" + Guid.NewGuid().ToString("N");
            defaults = SetupAt(root + "/Defaults");
            Assert.NotNull(defaults, "The installed source fonts and TMP shader must create saved defaults.");
        }

        [OneTimeTearDown]
        public void RemoveOnlyTestAssets()
        {
            if (!string.IsNullOrEmpty(root) && AssetDatabase.IsValidFolder(root)) AssetDatabase.DeleteAsset(root);
        }

        [Test]
        public void DefaultsPersistFontsAtlasesMaterialsAndCurrentVisualSettings()
        {
            Assert.IsTrue(defaults.TryValidate(out string reason), reason);
            for (int index = 0; index < FontFields.Length; index++)
            {
                var font = Reference(defaults, FontFields[index]);
                string path = AssetDatabase.GetAssetPath(font);
                Assert.IsTrue(EditorUtility.IsPersistent(font));
                StringAssert.StartsWith(root + "/Defaults/Fonts/", path);
                Assert.AreEqual(2048, Property(font, "atlasWidth"));
                Assert.AreEqual(2048, Property(font, "atlasHeight"));
                Assert.AreEqual(index == 0 ? 12 : 10, Property(font, "atlasPadding"));
                Assert.AreEqual("SDFAA", Property(font, "atlasRenderMode").ToString());
                Assert.AreEqual("Dynamic", Property(font, "atlasPopulationMode").ToString());
                Assert.AreEqual(true, Property(font, "isMultiAtlasTexturesEnabled"));
                var settings = Property(font, "creationSettings");
                Assert.AreEqual(index == 0 ? 96 : 80, settings.GetType().GetField("pointSize").GetValue(settings));
                foreach (var atlas in ((IEnumerable)Property(font, "atlasTextures")).Cast<Texture2D>().Where(atlas => atlas))
                {
                    Assert.AreEqual(path, AssetDatabase.GetAssetPath(atlas));
                    Assert.IsTrue(AssetDatabase.IsSubAsset(atlas));
                    Assert.AreEqual(FilterMode.Bilinear, atlas.filterMode);
                }
                Assert.AreEqual(path, AssetDatabase.GetAssetPath((Object)Property(font, "material")));
                Assert.IsFalse(new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue);
                var material = (Material)Property(font, "material");
                var normalized = new Material(material);
                try
                {
                    var update = font.GetType().Assembly.GetType("TMPro.ShaderUtilities")
                        .GetMethod("UpdateShaderRatios", BindingFlags.Public | BindingFlags.Static);
                    Assert.IsNotNull(update);
                    update.Invoke(null, new object[] { normalized });
                    foreach (string ratio in new[] { "_ScaleRatioA", "_ScaleRatioB", "_ScaleRatioC" })
                        Assert.AreEqual(normalized.GetFloat(ratio), material.GetFloat(ratio), FontFields[index] + " " + ratio);
                }
                finally { Object.DestroyImmediate(normalized); }
            }
            foreach (string name in MaterialFields)
                StringAssert.StartsWith(root + "/Defaults/Materials/", AssetDatabase.GetAssetPath(Reference(defaults, name)));
            var stroke = (Material)Reference(defaults, "Stroke");
            var drop = (Material)Reference(defaults, "Drop");
            Assert.AreEqual(.28f, stroke.GetFloat("_OutlineWidth"));
            Assert.AreEqual(.28f, stroke.GetFloat("_FaceDilate"));
            Assert.That(stroke.GetColor("_OutlineColor"), Is.EqualTo(LobbyKit.Navy).Using(UnityEngine.TestTools.Utils.ColorEqualityComparer.Instance));
            Assert.AreEqual(-.55f, drop.GetFloat("_UnderlayOffsetY"));
            Assert.AreEqual(.28f, drop.GetFloat("_UnderlayDilate"));
            var foreground = (Material)Reference(defaults, "BodyForeground");
            Assert.AreEqual(.18f, foreground.GetFloat("_OutlineWidth"));
            Assert.AreEqual(.02f, foreground.GetFloat("_FaceDilate"));
            Assert.That(foreground.GetColor("_OutlineColor"), Is.EqualTo(new Color(1f, .976f, .914f, .96f)).Using(UnityEngine.TestTools.Utils.ColorEqualityComparer.Instance));
        }

        [Test]
        public void RepeatedVerificationPreservesAuthoredReferencesAndBytes()
        {
            string folder = root + "/Authored";
            Folder(folder);
            var authored = Object.Instantiate(defaults);
            var custom = new Material((Material)Reference(defaults, "Stroke"));
            custom.SetFloat("_OutlineWidth", .13f);
            AssetDatabase.CreateAsset(custom, folder + "/Custom.mat");
            SetReference(authored, "Stroke", custom);
            AssetDatabase.CreateAsset(authored, folder + "/Profile.asset");
            AssetDatabase.SaveAssetIfDirty(custom); AssetDatabase.SaveAssetIfDirty(authored);
            string[] paths = AssetDatabase.FindAssets("", new[] { root }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(File.Exists).ToArray();
            var bytes = paths.Select(File.ReadAllBytes).ToArray();
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            Assert.AreSame(authored, SetupAt(folder));
            Assert.AreSame(authored, SetupAt(folder));
            Assert.AreSame(custom, Reference(authored, "Stroke"));
            for (int i = 0; i < paths.Length; i++)
            {
                CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]), paths[i]);
                Assert.AreEqual(guids[i], AssetDatabase.AssetPathToGUID(paths[i]));
            }
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder + "/Fonts"));
        }

        [Test]
        public void CollisionStopsBeforeCreatingAnyDefaultFontOrProfile()
        {
            string folder = root + "/Collision";
            Folder(folder + "/Materials");
            var occupied = new Material((Material)Reference(defaults, "Stroke"));
            string occupiedPath = folder + "/Materials/Lobby Display Stroke.mat";
            AssetDatabase.CreateAsset(occupied, occupiedPath);
            var before = File.ReadAllBytes(occupiedPath);
            LogAssert.Expect(LogType.Warning, new Regex("Lobby typography setup stopped.*already occupied"));
            Assert.IsNull(SetupAt(folder));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(occupiedPath));
            Assert.IsFalse(File.Exists(folder + "/Profile.asset"));
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder + "/Fonts"));
        }

        [Test]
        public void InvalidAuthoredProfileIsWarnedAndNeverFilledOrReplaced()
        {
            string folder = root + "/Invalid";
            Folder(folder);
            var invalid = ScriptableObject.CreateInstance<LobbyTypographyProfile>();
            string path = folder + "/Profile.asset";
            AssetDatabase.CreateAsset(invalid, path);
            var before = File.ReadAllBytes(path);
            LogAssert.Expect(LogType.Warning, new Regex("Lobby typography setup stopped.*Authored profile needs attention"));
            Assert.IsNull(SetupAt(folder));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.IsNull(Reference(invalid, "Display"));
            Assert.IsFalse(AssetDatabase.IsValidFolder(folder + "/Fonts"));
        }

        [Test]
        public void ValidationRejectsAnotherFontsAtlasAndMismatchedSdfScale()
        {
            var copy = Object.Instantiate(defaults);
            var material = new Material((Material)Reference(defaults, "Stroke"));
            try
            {
                SetReference(copy, "Stroke", material);
                Assert.IsTrue(copy.TryValidate(out string reason), reason);
                var correct = material.mainTexture;
                material.mainTexture = ((Material)Reference(defaults, "BodyForeground")).mainTexture;
                Assert.IsFalse(copy.TryValidate(out reason)); StringAssert.Contains("Stroke", reason);
                material.mainTexture = correct;
                material.SetFloat("_GradientScale", material.GetFloat("_GradientScale") + 1);
                Assert.IsFalse(copy.TryValidate(out reason)); StringAssert.Contains("Stroke", reason);
            }
            finally { Object.DestroyImmediate(copy); Object.DestroyImmediate(material); }
        }

        [Test]
        public void SavedProfileUsesExistingObjectsAndResetPreservesThemWithoutDomainReload()
        {
            var type = typeof(LobbyFonts);
            string[] names = { "display", "body", "black", "stroke", "drop", "failed" };
            var fields = names.Select(name => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)).ToArray();
            var values = fields.Select(field => field.GetValue(null)).ToArray();
            var cache = (IDictionary)type.GetField("foreground", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var priorCache = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in cache) priorCache.Add(entry);
            var ownership = new[] { "generatedFonts", "generatedMaterials" }
                .Select(name => (IList)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).ToArray();
            var priorOwnership = ownership.Select(list => list.Cast<object>().ToArray()).ToArray();
            try
            {
                foreach (var list in ownership) list.Clear();
                Assert.IsTrue((bool)type.GetMethod("TryUseProfile", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { defaults }));
                Assert.AreSame(Reference(defaults, "Display"), type.GetProperty("Display").GetValue(null));
                Assert.AreSame(Reference(defaults, "BodyForeground"), type.GetMethod("Foreground").Invoke(null, new object[] { Reference(defaults, "Body") }));
                Assert.IsTrue(ownership.All(list => list.Count == 0), "Adopting saved typography must not allocate duplicate fonts/materials.");
                var reset = type.GetMethod("ResetCache", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration,
                    reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>().loadType);
                reset.Invoke(null, null);
                Assert.IsNull(fields[0].GetValue(null)); Assert.AreEqual(0, cache.Count);
                Assert.IsTrue(defaults.TryValidate(out string reason), reason);
                foreach (string name in FontFields.Concat(MaterialFields)) Assert.IsTrue(Reference(defaults, name), name);
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, values[i]);
                cache.Clear(); foreach (var entry in priorCache) cache.Add(entry.Key, entry.Value);
                for (int i = 0; i < ownership.Length; i++)
                { ownership[i].Clear(); foreach (var value in priorOwnership[i]) ownership[i].Add(value); }
            }
        }

        static LobbyTypographyProfile SetupAt(string folder) => (LobbyTypographyProfile)typeof(LobbyTypographySetup)
            .GetMethod("CreateOrVerifyAt", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { folder + "/Profile.asset", folder + "/Fonts", folder + "/Materials" });
        static Object Reference(LobbyTypographyProfile profile, string field) => (Object)typeof(LobbyTypographyProfile).GetField(field).GetValue(profile);
        static void SetReference(LobbyTypographyProfile profile, string field, Object value) => typeof(LobbyTypographyProfile).GetField(field).SetValue(profile, value);
        static object Property(Object value, string property) => value.GetType().GetProperty(property).GetValue(value);
        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
