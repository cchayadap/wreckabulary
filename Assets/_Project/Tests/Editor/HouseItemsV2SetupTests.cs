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
    public class HouseItemsV2SetupTests
    {
        static JsonNode[] Manifest() =>
            Json.Parse(File.ReadAllText(HouseItemsV2Setup.SourceRoot + "/manifest.json"), "manifest.json")["entries"].Items.ToArray();

        [Test]
        public void PrefabsAreRenderOnlyLibraryColouredAndMatchTheManifest()
        {
            var entries = Manifest();
            CollectionAssert.AreEqual(HouseItemsV2Setup.Words, entries.Select(e => e["word"].String()));
            var library = MaterialLibrary.Load();
            foreach (var entry in entries)
            {
                string word = entry["word"].String();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseItemsV2Setup.PrefabPath(word));
                Assert.IsNotNull(prefab, word);
                Assert.AreEqual(word, prefab.name);
                Assert.IsTrue(prefab.GetComponentsInChildren<Component>(true).All(c => c is Transform || c is MeshFilter || c is MeshRenderer), word + " is render-only");
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.IsNotEmpty(renderers, word);
                var names = renderers.SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().OrderBy(n => n).ToArray();
                CollectionAssert.AreEqual(entry["materials"].Strings().OrderBy(n => n).ToArray(), names, word + " keeps the slots its builder made");
                foreach (var material in renderers.SelectMany(r => r.sharedMaterials))
                {
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name, word);
                    Assert.AreEqual(HouseItemsV2Setup.ModelPath(word), AssetDatabase.GetAssetPath(material), word + " keeps its built model's own materials");
                    Assert.IsNotNull(library.Find(material.name), $"{word} {material.name} resolves in the material library");
                    foreach (string finish in new[] { "Candy", "Arcade" })
                        Assert.IsTrue(library.Resolves(material, finish), $"{word} {material.name} {finish}");
                }
                int triangles = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3);
                Assert.LessOrEqual(triangles, entry["maxTriangles"].Int(), word + " stays inside its triangle budget");
                var bounds = ModelVisual.BoundsIn(prefab.transform, prefab);
                var size = entry["size"].Floats(3);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(bounds.size[axis], Is.EqualTo(size[axis]).Within(size[axis] * .02f), $"{word} axis {axis}");
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(.001f), word + " stands on its origin");
                Assert.That(new Vector2(bounds.center.x, bounds.center.z).magnitude, Is.LessThan(.001f), word + " is centred");
                var grip = ModelVisual.FindNamed(prefab, "Grip_R");
                Assert.IsNotNull(grip, word);
                var expected = entry["grip"].Floats(3);
                Assert.Less(Vector3.Distance(new Vector3(expected[0], expected[1], expected[2]), grip.localPosition), .001f, word + " grip");
            }
        }

        [Test]
        public void TheLibraryResolvesEveryBatchItem()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var entries = HouseItemsV2Setup.LibraryEntries();
            CollectionAssert.AreEqual(HouseItemsV2Setup.Words.Select(HouseItemsV2Setup.Key), entries.Select(e => e.key));
            foreach (string word in HouseItemsV2Setup.Words)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HouseItemsV2Setup.PrefabPath(word));
                Assert.AreSame(prefab, library.Find("Items/" + word), word);
                Assert.AreEqual("Items/" + word, GameConfig.Current.Items.Get(word).Model, word);
            }
        }

        [Test]
        public void IconsAreTransparent192RgbaAndLoadAsRecipeArt()
        {
            foreach (string word in HouseItemsV2Setup.Words)
            {
                string path = HouseItemsV2Setup.IconPath(word);
                var png = File.ReadAllBytes(path);
                Assert.AreEqual(192, BigEndian(png, 16), word + " width");
                Assert.AreEqual(192, BigEndian(png, 20), word + " height");
                Assert.AreEqual(6, png[25], word + " is RGBA");
                Assert.IsTrue(HouseItemsSetup.HasIconSettings(AssetImporter.GetAtPath(path) as TextureImporter), word);
                var texture = UI.OriginalItemArt.Find(word);
                Assert.AreEqual(path, AssetDatabase.GetAssetPath(texture), word + " loads as recipe art");
                var pixels = texture.GetPixels32();
                Assert.AreEqual(0, pixels[0].a, word + " has a transparent background");
                Assert.Greater(pixels.Count(p => p.a == 255), pixels.Length / 100, word + " is visible");
                var opaque = pixels.Where(p => p.a == 255).Select(p => { Color.RGBToHSV(p, out _, out float s, out _); return s; }).OrderBy(s => s).ToArray();
                Assert.GreaterOrEqual(opaque[opaque.Length / 2], .45f, word + " icon reads vivid, not pale");
            }
        }

        [Test]
        public void SetupRunsAgainWithoutChangingAnything()
        {
            var paths = HouseItemsV2Setup.Words.SelectMany(word => new[]
                {
                    HouseItemsV2Setup.PrefabPath(word), HouseItemsV2Setup.IconPath(word), HouseItemsV2Setup.IconPath(word) + ".meta", HouseItemsV2Setup.ModelPath(word),
                })
                .Concat(new[] { ImportedArtSettings.ModelLibraryAssetPath, HouseItemsV2Setup.SourceRoot + "/manifest.json" })
                .ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            HouseItemsV2Setup.CreateOrVerify();
            for (int i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
        }

        static int BigEndian(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];
    }
}
