using UnityEditor;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// Applies <see cref="ImportedArtSettings"/> whenever a file under <c>Art/Imported</c> is
    /// imported, so regenerated or newly added pipeline output gets the right scale, axes,
    /// rig and clips without anyone touching the inspector. Material binding is stored in the
    /// .meta files by <see cref="ArtSetup"/>, not done here, so it survives without this class.
    /// </summary>
    public sealed class ImportedArtPostprocessor : AssetPostprocessor
    {
        // Bump when the settings change, so Unity reimports the art.
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
