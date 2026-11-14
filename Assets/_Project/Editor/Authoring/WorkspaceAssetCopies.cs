using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WorkspaceAssetCopies
    {
        public static Object Create(Object source, string destination)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create copies outside Play Mode.");
            if (!source || !EditorUtility.IsPersistent(source) || source is not (Material or GameObject))
                throw new ArgumentException("Choose a saved material or model asset.");
            string extension = source is Material ? ".mat" : ".prefab";
            ValidateDestination(destination, extension);
            EnsureFolder(Path.GetDirectoryName(destination).Replace('\\', '/'));
            if (source is Material material)
            {
                var copy = new Material(material) { name = Path.GetFileNameWithoutExtension(destination) };
                try { AssetDatabase.CreateAsset(copy, destination); AssetDatabase.SaveAssetIfDirty(copy); return copy; }
                catch { if (!AssetDatabase.Contains(copy)) Object.DestroyImmediate(copy); throw; }
            }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                model.name = Path.GetFileNameWithoutExtension(destination);
                var saved = PrefabUtility.SaveAsPrefabAsset(model, destination);
                if (!saved) throw new IOException("Unity could not save the editable prefab.");
                return saved;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void ValidateDestination(string destination, string extension)
        {
            if (string.IsNullOrWhiteSpace(destination) || destination.Contains('\\') || !destination.StartsWith("Assets/_Project/", StringComparison.Ordinal) ||
                InvalidSegments(destination.Split('/')) || !string.Equals(Path.GetExtension(destination), extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the copy inside Assets/_Project with its expected file type.");
            if (File.Exists(destination) || Directory.Exists(destination) || File.Exists(destination + ".meta") || AssetDatabase.LoadMainAssetAtPath(destination))
                throw new IOException("That destination already exists. Choose a new name to preserve existing work.");
        }

        static bool InvalidSegments(string[] segments)
        {
            foreach (string segment in segments)
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == ".." || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
            return false;
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || folder.Contains('\\') || InvalidSegments(folder.Split('/')) ||
                folder != "Assets/_Project" && !folder.StartsWith("Assets/_Project/", StringComparison.Ordinal))
                throw new ArgumentException("Authoring folders must stay in Assets/_Project.");
            if (AssetDatabase.IsValidFolder(folder)) return;
            if (File.Exists(folder) || Directory.Exists(folder) || File.Exists(folder + ".meta"))
                throw new IOException("An existing folder path cannot be replaced: " + folder);
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || !folder.StartsWith("Assets/_Project", StringComparison.Ordinal))
                throw new ArgumentException("Authoring folders must stay in Assets/_Project.");
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new IOException("Could not create authoring folder: " + folder);
        }
    }
}
