using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.UI;

namespace Wreckabulary.EditorTests
{
    public sealed class GeneratedItemArtTests
    {
        [TestCase("SHIELD"), TestCase("SODA"), TestCase("APPLE"), TestCase("WATER"), TestCase("FAN"), TestCase("CLOCK")]
        public void GeneratedIllustrationIsIncludedAndImportedForTransparentUI(string id)
        {
            Assert.IsTrue(GeneratedItemArt.TryGet(id, out var sprite));
            Assert.IsTrue(sprite);
            string path = AssetDatabase.GetAssetPath(sprite);
            StringAssert.StartsWith("Assets/_Project/Art/Generated/Items/", path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.IsFalse(importer.isReadable);
            Assert.LessOrEqual(sprite.texture.width, 512);
            Assert.LessOrEqual(sprite.texture.height, 512);
        }
    }
}
