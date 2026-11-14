using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.UI;

namespace Wreckabulary.EditorTests
{
    public sealed class GeneratedItemArtTests
    {
        [TestCase("SHIELD"), TestCase("SODA"), TestCase("APPLE"), TestCase("WATER"), TestCase("FAN"), TestCase("CLOCK")]
        public void CommonArtworkPrefersExistingOriginalWhileGeneratedRemainsExplicit(string id)
        {
            var original = OriginalItemArt.Find(id);
            Assert.IsNotNull(original);
            Assert.IsTrue(GeneratedItemArt.TryGet(id, out var generated));
            Assert.AreNotSame(original, generated.texture, "The competing art sources must actually be different.");
            Assert.IsTrue(ItemArt.TryGet(id, out var selected, out var uv));
            Assert.AreSame(original, selected);
            Assert.AreEqual(new Rect(0, 0, 1, 1), uv);
            Assert.AreEqual("Assets/_Project/Resources/UI/Items/" + id + ".png", AssetDatabase.GetAssetPath(selected));
            Assert.IsTrue(GeneratedItemArt.TryGet(id, out var after));
            Assert.AreSame(generated, after, "Default lookup does not replace the explicit generated library.");
        }

        [Test]
        public void ExplicitWordboundShieldAndMissingOriginalFallbackRemainAvailable()
        {
            var collection = Wreckabulary.Art.WordboundEquipmentCollection.Load();
            Assert.IsNotNull(collection);
            Assert.IsTrue(GeneratedItemArt.TryGet("SHIELD", out var explicitIcon, wordbound: true));
            Assert.AreSame(collection.Find("SHIELD").icon, explicitIcon);
            Assert.AreNotSame(OriginalItemArt.Find("SHIELD"), explicitIcon.texture);

            Assert.IsNull(OriginalItemArt.Find("shield"), "The original library accepts canonical upper-case IDs.");
            Assert.IsTrue(GeneratedItemArt.TryGet("shield", out var fallback), "Keep the existing case-insensitive generated fallback.");
            Assert.IsTrue(ItemArt.TryGet("shield", out var selected, out var uv));
            Assert.AreSame(fallback.texture, selected);
            var region = fallback.textureRect;
            Assert.AreEqual(new Rect(region.x / selected.width, region.y / selected.height,
                region.width / selected.width, region.height / selected.height), uv);
            Assert.IsFalse(ItemArt.TryGet("NO_SUCH_ITEM", out var missing, out _));
            Assert.IsNull(missing);
        }

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
