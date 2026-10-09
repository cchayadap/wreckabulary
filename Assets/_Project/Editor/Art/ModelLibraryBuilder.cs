using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class ModelLibraryBuilder
    {
        public static void Refresh() => Build(ImportedArtSettings.ReadReport());

        public static ModelLibrary Build(List<ImportedArtSettings.ReportFile> report)
        {
            var entries = new List<ModelLibrary.Entry>();
            var missing = new List<string>();
            foreach (var file in report)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(file.Output);
                if (model == null) missing.Add(file.Output);
                else
                {
                    var clips = AssetDatabase.LoadAllAssetsAtPath(file.Output).OfType<AnimationClip>()
                        .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                        .OrderBy(clip => ImportedArtSettings.ClipName(clip.name), StringComparer.Ordinal).ToArray();
                    var names = clips.Select(clip => ImportedArtSettings.ClipName(clip.name)).ToArray();
                    var expected = file.Clips.OrderBy(name => name, StringComparer.Ordinal).ToArray();
                    if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || !names.SequenceEqual(expected))
                        throw new InvalidOperationException($"Imported clips do not match the report for {file.Output}. Expected: {string.Join(", ", expected)}. Actual: {string.Join(", ", names)}.");
                    entries.Add(new ModelLibrary.Entry { key = KeyOf(file.Output), model = model, clips = clips });
                }
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("These models didn't import:\n" + string.Join("\n", missing));

            MaterialLibraryBuilder.EnsureFolder(System.IO.Path.GetDirectoryName(ImportedArtSettings.ModelLibraryAssetPath).Replace('\\', '/'));
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
