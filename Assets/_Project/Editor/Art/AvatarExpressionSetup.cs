#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class AvatarExpressionSetup
    {
        public const string Path = "Assets/_Project/Resources/Art/AvatarExpressions.asset";
        [MenuItem("Wreckabulary/Art/Create Missing Avatar Expression Profile")]
        public static void Run()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AvatarExpressionProfile>(Path);
            if (existing) { existing.Validate(); return; }
            if (File.Exists(Path) || File.Exists(Path + ".meta")) throw new InvalidOperationException("Preserve the existing expression asset/path; resolve its type first.");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Art")) throw new InvalidOperationException("Expected the existing Art resources folder.");
            var profile = ScriptableObject.CreateInstance<AvatarExpressionProfile>();
            try { profile.Validate(); AssetDatabase.CreateAsset(profile, Path); }
            catch { if (!AssetDatabase.Contains(profile)) UnityEngine.Object.DestroyImmediate(profile); throw; }
        }
    }
}
#endif
