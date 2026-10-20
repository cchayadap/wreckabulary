using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    [Explicit("Run after WordboundEquipmentImporter.BuildDetailed imports the verified detailed revision.")]
    public sealed class WordboundEquipmentDetailTests
    {
        [Test]
        public void DetailedMapsUseLinearDataAndUrpSmoothnessAlpha()
        {
            var manifest = WordboundEquipmentImporter.ReadVerifiedManifest();
            Assert.AreEqual("DetailV2", manifest.revision);
            Assert.IsNotNull(manifest.detail_maps);
            var paint = LoadMaterial(WordboundEquipmentImporter.PaintName);
            var brass = LoadMaterial(WordboundEquipmentImporter.BrassName);
            Assert.IsNotNull(paint);
            Assert.IsNotNull(brass);
            var color = Importer("WordboundPvPPalette.png");
            var normal = Importer("WordboundPvPNormal.png");
            var mask = Importer("WordboundPvPMask.png");
            Assert.IsTrue(color.sRGBTexture);
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            Assert.IsFalse(normal.sRGBTexture || normal.convertToNormalmap || normal.flipGreenChannel);
            Assert.AreEqual(TextureImporterType.Default, mask.textureType);
            Assert.IsFalse(mask.sRGBTexture || mask.alphaIsTransparency);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, mask.alphaSource);
            Assert.IsTrue(mask.DoesSourceTextureHaveAlpha());
            foreach (var importer in new[] { color, normal, mask })
            {
                Assert.AreEqual(512, importer.maxTextureSize);
                Assert.IsTrue(importer.mipmapEnabled);
                Assert.IsFalse(importer.isReadable);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(importer.assetPath);
                Assert.AreEqual(512, texture.width);
                Assert.AreEqual(256, texture.height);
            }
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(color.assetPath), paint.GetTexture("_BaseMap"));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(normal.assetPath), paint.GetTexture("_BumpMap"));
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<Texture2D>(mask.assetPath), paint.GetTexture("_MetallicGlossMap"));
            Assert.IsTrue(paint.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(paint.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            Assert.AreEqual(1f, paint.GetFloat("_Smoothness"));
            Assert.AreEqual(0f, paint.GetFloat("_SmoothnessTextureChannel"));
            Assert.IsNull(brass.GetTexture("_BumpMap"));
            Assert.IsNull(brass.GetTexture("_MetallicGlossMap"));
            Assert.IsFalse(brass.IsKeywordEnabled("_NORMALMAP") || brass.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        }

        [Test]
        public void DetailedNativeMeshesKeepTheOriginalDimensionsGripAndTwoMaterialBudget()
        {
            var manifest = WordboundEquipmentImporter.ReadVerifiedManifest();
            Assert.AreEqual("DetailV2", manifest.revision);
            var collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(WordboundEquipmentImporter.CollectionPath);
            Assert.IsNotNull(collection);
            Assert.AreEqual(12, collection.Entries.Count);
            var paint = LoadMaterial(WordboundEquipmentImporter.PaintName);
            var brass = LoadMaterial(WordboundEquipmentImporter.BrassName);
            foreach (var entry in collection.Entries)
            {
                var source = manifest.assets.Single(record => record.id == entry.itemId);
                Assert.DoesNotThrow(() => WordboundEquipmentImporter.ValidateImported(entry, source.triangles, paint, brass), entry.itemId);
            }
        }

        static TextureImporter Importer(string file) =>
            (TextureImporter)AssetImporter.GetAtPath(WordboundEquipmentImporter.ArtRoot + "/" + file);

        static Material LoadMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>(WordboundEquipmentImporter.ArtRoot + "/Materials/" + name + ".mat");
    }
}
