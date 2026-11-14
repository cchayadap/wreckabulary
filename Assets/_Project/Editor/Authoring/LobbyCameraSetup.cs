using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class LobbyCameraSetup
    {
        public const string ProfilePath = "Assets/_Project/Resources/UI/LobbyCamera.asset";

        [MenuItem("Wreckabulary/Authoring/Create or Verify Lobby Camera")]
        public static void CreateOrVerify()
        {
            var profile = CreateOrVerifyAt(ProfilePath);
            if (!profile) throw new InvalidOperationException("Lobby camera setup did not produce a valid profile; preserve the existing assets and inspect the warning.");
            Selection.activeObject = profile; EditorGUIUtility.PingObject(profile);
        }

        public static LobbyCameraProfile CreateOrVerifyAt(string path)
        {
            LobbyCameraProfile created = null;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Create or verify lobby camera assets outside Play Mode.");
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/_Project/", StringComparison.Ordinal) ||
                    !path.EndsWith(".asset", StringComparison.Ordinal) || path.Contains('\\') ||
                    path.Split('/').Any(part => part.Length == 0 || part == "." || part == ".."))
                    throw new InvalidOperationException("Choose an .asset path inside Assets/_Project.");
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing)
                {
                    if (existing is not LobbyCameraProfile profile) throw new IOException("The target path is occupied by another asset: " + path);
                    if (!profile.TryValidate(out string reason)) throw new InvalidOperationException("Authored profile needs attention: " + reason);
                    return profile;
                }
                RequireVacant(path);
                string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                CheckFolders(folder);
                EnsureFolder(folder);
                created = ScriptableObject.CreateInstance<LobbyCameraProfile>();
                created.name = "LobbyCamera";
                if (!created.TryValidate(out string invalid)) throw new InvalidOperationException(invalid);
                RequireVacant(path);
                AssetDatabase.CreateAsset(created, path);
                AssetDatabase.SaveAssetIfDirty(created);
                return created;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Lobby camera setup stopped without replacing authored assets. " + error.Message);
                return null;
            }
            finally { if (created && !EditorUtility.IsPersistent(created)) Object.DestroyImmediate(created); }
        }

        static void RequireVacant(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) || File.Exists(path) || File.Exists(path + ".meta") || Directory.Exists(path))
                throw new IOException("The target path is already occupied: " + path);
        }

        static void CheckFolders(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (File.Exists(folder) || File.Exists(folder + ".meta") || Directory.Exists(folder))
                throw new IOException("An unrecognized path blocks the target folder: " + folder);
            int slash = folder.LastIndexOf('/');
            if (slash < 0) throw new IOException("The Assets folder is unavailable.");
            CheckFolders(folder.Substring(0, slash));
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1))))
                throw new IOException("Could not create the missing folder: " + folder);
        }
    }

    [CustomEditor(typeof(LobbyCameraProfile))]
    public sealed class LobbyCameraProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var profile = (LobbyCameraProfile)target;
            if (!profile.TryValidate(out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Error);
            EditorGUILayout.HelpBox("Save this asset and start a new lobby to apply it. The default lobby uses Resources/UI/LobbyCamera. This does not change the Wordbound gameplay camera.", MessageType.Info);
        }
    }
}
