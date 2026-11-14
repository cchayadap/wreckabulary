using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public class HouseItemsSetupTests
    {
        static JsonNode[] Manifest() =>
            Json.Parse(File.ReadAllText(HouseItemsSetup.SourceRoot + "/manifest.json"), "manifest.json")["entries"].Items.ToArray();

        [Test]
        public void PrefabsAreRenderOnlyAndMatchTheManifestSizes()
        {
            var entries = Manifest();
            CollectionAssert.AreEqual(new[] { "PILLOW", "RACK", "MIRROR", "SPEAKER", "BENCH", "TOASTER", "BEAR" }, entries.Select(e => e["word"].String()));
            CollectionAssert.AreEqual(HouseItemsSetup.Words, entries.Select(e => e["word"].String()));
            foreach (var entry in entries)
            {
                string word = entry["word"].String();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseItemsSetup.PrefabPath(word));
                Assert.IsNotNull(prefab, word);
                Assert.AreEqual(word, prefab.name);
                Assert.IsTrue(prefab.GetComponentsInChildren<Component>(true).All(c => c is Transform || c is MeshFilter || c is MeshRenderer), word + " is render-only");
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.IsNotEmpty(renderers, word);
                foreach (var material in renderers.SelectMany(r => r.sharedMaterials))
                {
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, word);
                    StringAssert.StartsWith(HouseItemsSetup.ArtRoot + "/Materials/" + word + "/", AssetDatabase.GetAssetPath(material));
                }
                var bounds = ModelVisual.BoundsIn(prefab.transform, prefab);
                var size = entry["size"].Floats(3);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(bounds.size[axis], Is.EqualTo(size[axis]).Within(size[axis] * .02f), $"{word} axis {axis}");
                Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.EqualTo(entry["targetLongest"].Float()).Within(.001f), word);
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.001f), word + " stands on its origin");
                Assert.That(new Vector2(bounds.center.x, bounds.center.z).magnitude, Is.LessThan(.001f), word + " is centred");
            }
        }

        [Test]
        public void LibraryResolvesEveryHouseItemAndMatchesARebuild()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var house = HouseItemsSetup.LibraryEntries();
            CollectionAssert.AreEqual(HouseItemsSetup.Words.Select(HouseItemsSetup.Key), house.Select(e => e.key));
            foreach (string word in HouseItemsSetup.Words)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseItemsSetup.PrefabPath(word));
                Assert.AreSame(prefab, library.Find("Items/" + word), word);
                Assert.AreSame(prefab, house.Single(e => e.key == "Items/" + word).model, word);
            }
            var rebuilt = ImportedArtSettings.ReadReport().Select(file => ModelLibraryBuilder.KeyOf(file.Output))
                .Concat(house.Select(e => e.key)).OrderBy(key => key, StringComparer.Ordinal);
            CollectionAssert.AreEqual(rebuilt, library.Entries.Select(e => e.key), "a library rebuild keeps the same keys in the same order");
        }

        [Test]
        public void IconsAreTransparent192RgbaWithTheItemIconImportSettings()
        {
            foreach (string reference in new[] { "PAN", "CHAIR" })
                Assert.IsTrue(HouseItemsSetup.HasIconSettings(AssetImporter.GetAtPath(HouseItemsSetup.IconPath(reference)) as TextureImporter), reference);
            foreach (string word in HouseItemsSetup.Words)
            {
                string path = HouseItemsSetup.IconPath(word);
                var png = File.ReadAllBytes(path);
                Assert.AreEqual(192, BigEndian(png, 16), word + " width");
                Assert.AreEqual(192, BigEndian(png, 20), word + " height");
                Assert.AreEqual(8, png[24], word + " bit depth");
                Assert.AreEqual(6, png[25], word + " is RGBA");
                Assert.IsTrue(HouseItemsSetup.HasIconSettings(AssetImporter.GetAtPath(path) as TextureImporter), word);
                var texture = UI.OriginalItemArt.Find(word);
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(texture), word + " loads as recipe art");
                Assert.AreEqual(192, texture.width);
                Assert.AreEqual(192, texture.height);
                var pixels = texture.GetPixels32();
                Assert.AreEqual(0, pixels[0].a, word + " has a transparent background");
                Assert.AreEqual(0, pixels[pixels.Length - 1].a, word + " has a transparent background");
                Assert.Greater(pixels.Count(p => p.a == 255), pixels.Length / 100, word + " is visible");
            }
        }

        [Test]
        public void SetupRunsAgainWithoutChangingAnything()
        {
            var paths = HouseItemsSetup.Words.SelectMany(word => new[]
                {
                    HouseItemsSetup.PrefabPath(word), HouseItemsSetup.IconPath(word), HouseItemsSetup.IconPath(word) + ".meta",
                    HouseItemsSetup.ArtRoot + "/Models/" + word + ".fbx",
                })
                .Concat(new[] { ImportedArtSettings.ModelLibraryAssetPath, HouseItemsSetup.SourceRoot + "/manifest.json", HouseItemsSetup.ArtRoot + "/License.txt" })
                .ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            HouseItemsSetup.CreateOrVerify();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }

        static int BigEndian(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
    }
}
