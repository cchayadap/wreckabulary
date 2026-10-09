using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.UI;

namespace Wreckabulary.EditorTools
{
    public sealed class GeneratedItemArtImporter : AssetPostprocessor
    {
        const string Source = "Assets/_Project/Art/Generated/Items/";
        const string LibraryPath = "Assets/_Project/Resources/UI/Generated/PowerItemArt.asset";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Source, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = 256;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        [MenuItem("Wreckabulary/Art/Rebuild Generated Item Library")]
        public static void Rebuild()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] ids = { "SHIELD", "SODA", "APPLE", "WATER", "FAN", "CLOCK" };
            var entries = ids.Select(id =>
            {
                string file = Directory.GetFiles(Source, "*.png").Single(path =>
                    string.Equals(Path.GetFileNameWithoutExtension(path), id + "-v1", StringComparison.OrdinalIgnoreCase));
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprite) throw new InvalidOperationException("Missing generated sprite: " + path);
                return new GeneratedItemArt.Entry { itemId = id, sprite = sprite };
            }).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<GeneratedItemArt>(LibraryPath);
            if (!library)
            {
                library = ScriptableObject.CreateInstance<GeneratedItemArt>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("[PowerArt] Saved " + entries.Length + " generated item illustrations.");
        }
    }
}
