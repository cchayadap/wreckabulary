using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public class LobbyThemeAssetTests
    {
        [TestCase("sunroom")]
        [TestCase("candy")]
        [TestCase("lantern")]
        public void LobbyBackgroundsAreRuntimeReadyWithoutCpuOrMipmapCopies(string id)
        {
            string path = "Assets/_Project/Resources/UI/Themes/" + id + "-v1.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.NotNull(texture);
            Assert.Greater(texture.width, 1200);
            Assert.Greater((float)texture.width / texture.height, 1.6f);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.AreEqual(2048, importer.maxTextureSize);
            Assert.IsFalse(importer.isReadable);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreSame(texture, Resources.Load<Texture2D>("UI/Themes/" + id + "-v1"));
        }
    }
}
