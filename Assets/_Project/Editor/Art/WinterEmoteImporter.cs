using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public sealed class WinterEmoteImporter : AssetPostprocessor
    {
        public const string ClipPath = "Assets/_Project/Art/Seasonal/Winter/Animations/WinterShuffle.fbx";

        public override uint GetVersion() => 1;

        [MenuItem("Wreckabulary/Art/Import Winter Emote")]
        public static void Build()
        {
            const string source = "ArtSource/Collections/Winter/Animation/WinterShuffle.fbx";
            if (!File.Exists(source)) throw new FileNotFoundException("The authored WinterShuffle source is missing", source);
            Directory.CreateDirectory(Path.GetDirectoryName(ClipPath));
            byte[] bytes = File.ReadAllBytes(source);
            if (!File.Exists(ClipPath) || !File.ReadAllBytes(ClipPath).SequenceEqual(bytes)) File.WriteAllBytes(ClipPath, bytes);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath).OfType<AnimationClip>()
                .SingleOrDefault(candidate => candidate.name == "WinterShuffle");
            if (!clip || Mathf.Abs(clip.length - 2.4f) > .001f)
                throw new InvalidOperationException("WinterShuffle import did not produce the expected 2.4-second clip.");
        }

        void OnPreprocessModel()
        {
            if (!string.Equals(assetPath, ClipPath, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.addCollider = false;
        }

        void OnPreprocessAnimation()
        {
            if (!string.Equals(assetPath, ClipPath, StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length != 1)
                throw new InvalidOperationException("WinterShuffle must contain exactly one separate animation take.");
            var clip = clips[0];
            clip.name = "WinterShuffle";
            clip.loopTime = false;
            clip.loopPose = false;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = clips;
        }
    }
}
