using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.EditorTools
{
    public static class WordboundRoomStyleSetup
    {
        public const string Path = "Assets/_Project/Resources/Wordbound/RoomArchitecture.asset";
        public const string CorrectionBackupPath = "Assets/_Project/Data/Wordbound/Authoring/ArchitectureArchive/BeforeTextureCorrection20261028.asset";

        [MenuItem("Tools/Wordbound/Presentation/Apply reviewed floor and wall correction")]
        public static void ApplyReviewedCorrection()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Correct room architecture in Edit Mode.");
            var profile = AssetDatabase.LoadAssetAtPath<WordboundRoomStyleProfile>(Path);
            if (!profile) throw new InvalidOperationException("Create room architecture before applying its correction.");
            profile.Validate();
            if (AssetDatabase.LoadMainAssetAtPath(CorrectionBackupPath))
            {
                if (!AssetDatabase.LoadAssetAtPath<WordboundRoomStyleProfile>(CorrectionBackupPath))
                    throw new InvalidOperationException("The architecture backup has an unexpected type.");
                Selection.activeObject = profile;
                return;
            }
            if (EditorUtility.IsDirty(profile))
                throw new InvalidOperationException("Save or revert the room profile edits before applying its correction.");
            EnsureFolder("Assets/_Project/Data/Wordbound/Authoring/ArchitectureArchive");
            if (!AssetDatabase.CopyAsset(Path, CorrectionBackupPath))
                throw new InvalidOperationException("Could not preserve the current room profile.");
            profile.MaximumInsetsPerWallPiece = 0;
            profile.Validate(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            Selection.activeObject = profile;
        }

        [MenuItem("Tools/Wordbound/Presentation/Create or select room architecture")]
        public static void CreateOrSelect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create room architecture in Edit Mode.");
            var existing = AssetDatabase.LoadMainAssetAtPath(Path);
            if (existing && !(existing is WordboundRoomStyleProfile))
                throw new InvalidOperationException("A different asset already occupies " + Path);
            var profile = existing as WordboundRoomStyleProfile;
            if (!profile)
            {
                EnsureFolder("Assets/_Project/Resources/Wordbound");
                profile = ScriptableObject.CreateInstance<WordboundRoomStyleProfile>();
                try
                {
                    profile.WallArtwork = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Assets/_Project/Art/Generated/Environment/townhouse-print-v1.png");
                    profile.Validate(); AssetDatabase.CreateAsset(profile, Path);
                    AssetDatabase.SaveAssetIfDirty(profile);
                }
                catch { if (!AssetDatabase.Contains(profile)) UnityEngine.Object.DestroyImmediate(profile); throw; }
            }
            profile.Validate(); Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/'); string parent = folder.Substring(0, slash);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
