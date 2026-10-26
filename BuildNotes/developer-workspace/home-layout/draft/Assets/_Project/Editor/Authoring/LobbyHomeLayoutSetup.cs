using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyHomeLayoutSetup
    {
        public const string ProfilePath = "Assets/_Project/Resources/UI/LobbyHomeLayout.asset";
        public const string PrefabFolder = "Assets/_Project/Prefabs/UI/Home";
        public const string GraphicFolder = "Assets/_Project/UI/Home/Graphics";

        [MenuItem("Wreckabulary/Authoring/Create or Verify Saved Home Layout")]
        public static void CreateOrVerify()
        {
            var typography = AssetDatabase.LoadAssetAtPath<LobbyTypographyProfile>(LobbyTypographySetup.ProfilePath);
            var profile = CreateAt(ProfilePath, PrefabFolder, GraphicFolder, typography);
            Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
        }

        public static LobbyHomeLayout CreateAt(string profilePath, string folder, string graphicFolder, LobbyTypographyProfile typography)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create Home layouts outside Play Mode.");
            CheckPath(profilePath, ".asset"); CheckFolder(folder); CheckFolder(graphicFolder);
            var current = AssetDatabase.LoadMainAssetAtPath(profilePath);
            if (current)
            {
                if (current is not LobbyHomeLayout existing) throw new IOException("The Home profile destination is occupied.");
                Validate(existing); return existing;
            }
            RequireVacant(profilePath);
            string path = folder + "/HomePage.prefab";
            CheckPath(path, ".prefab");
            var saved = AssetDatabase.LoadMainAssetAtPath(path);
            LobbyHomePageView page = null;
            if (saved)
            {
                if (saved is not GameObject prefab || !prefab.TryGetComponent(out page))
                    throw new IOException("Existing Home prefab needs its typed view: " + path);
                page.Validate(); LobbyShopLayoutSetup.ValidatePersistent(page);
            }
            else
            {
                RequireVacant(path);
                if (!typography || !EditorUtility.IsPersistent(typography) || !typography.TryValidate(out _))
                    throw new InvalidOperationException("Create and validate saved Lobby typography before the Home layout.");
            }
            WorkspaceAssetCopies.EnsureFolder(folder);
            WorkspaceAssetCopies.EnsureFolder(graphicFolder);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(profilePath).Replace('\\', '/'));
            if (!page)
                page = LobbyShopLayoutSetup.SaveTemplate(path, () => LobbyHomeDefaultLayout.Create(null, typography), graphicFolder, new Dictionary<Sprite, Sprite>());
            var profile = ScriptableObject.CreateInstance<LobbyHomeLayout>();
            try
            {
                profile.Page = page;
                Validate(profile); RequireVacant(profilePath);
                AssetDatabase.CreateAsset(profile, profilePath); AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        static void Validate(LobbyHomeLayout profile)
        {
            profile.Validate(); LobbyShopLayoutSetup.ValidatePersistent(profile.Page);
        }

        static void CheckFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/_Project/", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(part => string.IsNullOrEmpty(part) || part == "." || part == ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("Home authoring paths must stay inside Assets/_Project.");
        }

        static void CheckPath(string path, string extension)
        {
            CheckFolder(path);
            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unexpected Home asset extension.");
        }

        static void RequireVacant(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new IOException("Existing Home authoring content is preserved; inspect this destination: " + path);
        }
    }
}
