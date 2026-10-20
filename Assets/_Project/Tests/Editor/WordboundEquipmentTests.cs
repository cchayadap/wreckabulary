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
    public sealed class WordboundEquipmentTests
    {
        WordboundEquipmentCollection collection;

        [OneTimeSetUp]
        public void LoadCollection()
        {
            collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(WordboundEquipmentImporter.CollectionPath);
            Assert.IsNotNull(collection, "Run WordboundEquipmentImporter.Build before native collection tests.");
        }

        [Test]
        public void TwelveModelsAndIconsMapToTheExistingCatalogue()
        {
            var catalogue = ItemCatalogue.FromJson(File.ReadAllText(WordboundEquipmentImporter.CataloguePath));
            Assert.AreEqual(12, collection.Entries.Count);
            CollectionAssert.AreEquivalent(WordboundEquipmentImporter.ItemIds, collection.Entries.Select(entry => entry.itemId));
            Assert.AreSame(collection, WordboundEquipmentCollection.Load());
            foreach (var entry in collection.Entries)
            {
                var item = catalogue.Get(entry.itemId);
                Assert.IsTrue(item.Enabled, entry.itemId);
                Assert.AreEqual(item.Model, entry.modelKey, entry.itemId);
                Assert.AreSame(entry, collection.Find(entry.itemId));
                Assert.AreSame(entry, collection.FindByModelKey(item.Model));
                Assert.AreEqual(new Vector3(item.Size[0], item.Size[1], item.Size[2]), entry.size);
                Assert.AreEqual(new Vector3(item.Grip[0], item.Grip[1], item.Grip[2]), entry.grip);
                Assert.AreEqual(item.HeldScale, entry.heldScale);
                Assert.IsNotNull(entry.model, entry.itemId);
                Assert.IsNotNull(entry.icon, entry.itemId);
            }
            Assert.IsNull(collection.Find("ROPE"), "later concept is not an equipped item");
            Assert.IsNull(collection.Find("MAGNET"), "later concept is not an equipped item");
            Assert.IsNull(collection.FindByModelKey("Items/APPLE"), "uncovered items use the existing library");
            Assert.IsNull(collection.FindByModelKey(null));
        }

        [Test]
        public void NativeModelsPreserveBoundsGripTrianglesUvsAndMaterials()
        {
            var manifest = WordboundEquipmentImporter.ReadVerifiedManifest();
            var paint = Material(WordboundEquipmentImporter.PaintName);
            var brass = Material(WordboundEquipmentImporter.BrassName);
            foreach (var entry in collection.Entries)
            {
                int triangles = manifest.assets.Single(asset => asset.id == entry.itemId).triangles;
                Assert.DoesNotThrow(() => WordboundEquipmentImporter.ValidateImported(entry, triangles, paint, brass), entry.itemId);
            }
            Assert.AreEqual("Universal Render Pipeline/Lit", paint.shader.name);
            Assert.AreEqual("Universal Render Pipeline/Lit", brass.shader.name);
            Assert.IsNotNull(paint.GetTexture("_BaseMap"));
            Assert.IsNull(brass.GetTexture("_BaseMap"));
            Assert.AreEqual(.72f, brass.GetFloat("_Metallic"), .001f);
        }

        [Test]
        public void IconAndModelImportSettingsKeepTheCollectionLightweight()
        {
            foreach (var entry in collection.Entries)
            {
                var model = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.model));
                Assert.IsTrue(model.bakeAxisConversion, entry.itemId);
                Assert.IsFalse(model.importAnimation || model.addCollider || model.isReadable, entry.itemId);
                var icon = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.icon));
                Assert.AreEqual(TextureImporterType.Sprite, icon.textureType);
                Assert.AreEqual(SpriteImportMode.Single, icon.spriteImportMode);
                Assert.AreEqual(512, icon.maxTextureSize);
                Assert.IsFalse(icon.mipmapEnabled || icon.isReadable);
                Assert.IsTrue(icon.alphaIsTransparency);
                Assert.IsTrue(icon.DoesSourceTextureHaveAlpha(), entry.itemId);
                Assert.AreEqual(new Vector2(512, 512), entry.icon.rect.size);
            }
            var palette = (TextureImporter)AssetImporter.GetAtPath(WordboundEquipmentImporter.ArtRoot + "/WordboundPvPPalette.png");
            Assert.AreEqual(512, palette.maxTextureSize);
            Assert.IsTrue(palette.sRGBTexture);
            Assert.IsFalse(palette.isReadable);
        }

        [Test]
        public void LegacySkinAndTactileCallsPreserveCollectionMaterialReferences()
        {
            var library = MaterialLibrary.Load();
            Assert.IsNotNull(library);
            foreach (var entry in collection.Entries)
            {
                var copy = Object.Instantiate(entry.model);
                try
                {
                    var renderers = copy.GetComponentsInChildren<Renderer>(true);
                    var original = renderers.Select(renderer => renderer.sharedMaterials).ToArray();
                    TactileMaterials.Apply(copy);
                    foreach (string skin in new[] { "Classic", "Candy", "Arcade", "Winter" })
                    {
                        library.ApplySkin(copy, skin);
                        for (int i = 0; i < renderers.Length; i++)
                            CollectionAssert.AreEqual(original[i], renderers[i].sharedMaterials, entry.itemId + "/" + skin);
                    }
                }
                finally { Object.DestroyImmediate(copy); }
            }
        }

        [Test]
        public void OriginalNinetyEightModelLibraryAndSkinFamiliesStayIndependent()
        {
            var report = ImportedArtSettings.ReadReport();
            var models = ModelLibrary.Load();
            var materials = MaterialLibrary.Load();
            Assert.AreEqual(98, report.Count);
            Assert.AreEqual(98, models.Entries.Count);
            foreach (var entry in collection.Entries)
            {
                Assert.IsNotNull(models.Find(entry.modelKey), entry.modelKey);
                Assert.AreNotSame(entry.model, models.Find(entry.modelKey), "collection must not overwrite original model");
            }
            foreach (string name in new[] { WordboundEquipmentImporter.PaintName, WordboundEquipmentImporter.BrassName })
            {
                Assert.IsNull(MaterialLibrary.FamilyOf(name));
                Assert.IsNull(materials.Find(name), "collection materials must not enter the shared material library");
                Assert.IsFalse(TactileMaterials.IsEligible(Material(name)));
            }
            var classic = materials.Materials.First(material => material.name.EndsWith("_Classic"));
            string family = MaterialLibrary.FamilyOf(classic.name);
            Assert.AreEqual(family + "_Candy", materials.ForSkin(classic, "Candy").name);
            Assert.AreEqual(family + "_Arcade", materials.ForSkin(classic, "Arcade").name);
            Assert.AreSame(classic, materials.ForSkin(materials.ForSkin(classic, "Candy"), "Classic"));
        }

        static Material Material(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(WordboundEquipmentImporter.ArtRoot + "/Materials/" + name + ".mat");
    }
}
