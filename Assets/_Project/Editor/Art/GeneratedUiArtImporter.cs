using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    /// <summary>Preserves generated UI transparency and limits runtime texture memory.</summary>
    public sealed class GeneratedUiArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/UI/Generated/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = assetPath.Contains("spark") ? 256 : 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
