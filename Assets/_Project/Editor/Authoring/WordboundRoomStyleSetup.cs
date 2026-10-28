using System;
using UnityEditor;
using UnityEngine;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.EditorTools
{
    public static class WordboundRoomStyleSetup
    {
        public const string Path = "Assets/_Project/Resources/Wordbound/RoomArchitecture.asset";

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
