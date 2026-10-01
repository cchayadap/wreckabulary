using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// Fills <see cref="ModelLibrary"/> with every model the Blender pipeline reported, keyed by
    /// its path under <c>Art/Imported</c> without the extension ("Items/BALL").
    /// </summary>
    public static class ModelLibraryBuilder
    {
        public static ModelLibrary Build(List<ImportedArtSettings.ReportFile> report)
        {
            MaterialLibraryBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(ImportedArtSettings.ModelLibraryAssetPath).Replace('\\', '/'));
            var entries = new List<ModelLibrary.Entry>();
            var missing = new List<string>();
            foreach (var file in report)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Output);
                if (model == null) missing.Add(file.Output);
                else entries.Add(new ModelLibrary.Entry
                {
                    key = KeyOf(file.Output), model = model,
                    clips = AssetDatabase.LoadAllAssetsAtPath(file.Output).OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray()
                });
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("These models didn't import:\n" + string.Join("\n", missing));

            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(library, ImportedArtSettings.ModelLibraryAssetPath);
            }
            library.Set(entries.OrderBy(e => e.key, StringComparer.Ordinal));
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        /// <summary>"Assets/_Project/Art/Imported/Items/BALL.fbx" → "Items/BALL".</summary>
        public static string KeyOf(string assetPath)
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(ImportedArtSettings.Root, StringComparison.Ordinal))
                throw new ArgumentException($"{assetPath} isn't under {ImportedArtSettings.Root}");
            path = path.Substring(ImportedArtSettings.Root.Length);
            int dot = path.LastIndexOf('.');
            return dot > 0 ? path.Substring(0, dot) : path;
        }
    }
}
