using UnityEditor;

namespace Wreckabulary.EditorTools
{
    public sealed class ImportedArtPostprocessor : AssetPostprocessor
    {
        const uint Version = 2;

        public override uint GetVersion() => Version;

        void OnPreprocessModel()
        {
            if (!ImportedArtSettings.IsImportedArt(assetPath)) return;
            ImportedArtSettings.Apply((ModelImporter)assetImporter);
        }

        void OnPreprocessAnimation()
        {
            if (!ImportedArtSettings.IsImportedArt(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            if (!mi.importAnimation) return;
            var clips = ImportedArtSettings.Clips(mi);
            if (clips != null) mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!ImportedArtSettings.IsImportedArt(assetPath)) return;
            ImportedArtSettings.Apply((TextureImporter)assetImporter);
        }
    }
}
