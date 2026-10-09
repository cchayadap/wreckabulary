using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public sealed class LobbyThemeImporter : AssetPostprocessor
    {
        const string Directory = "Assets/_Project/Resources/UI/Themes/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Directory, StringComparison.Ordinal) || !assetPath.EndsWith("-v1.png", StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        [MenuItem("Wreckabulary/Art/Import Lobby Themes")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var theme in LobbyThemes.All)
                AssetDatabase.ImportAsset(Directory + theme.Id + "-v1.png", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
