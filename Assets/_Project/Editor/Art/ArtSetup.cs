using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// One step after the Blender pipeline: saves the import settings for every imported texture
    /// and model into its .meta file, builds the material library, binds each model's
    /// materials to the library by name, and lists the models in the model library. The results
    /// live in the .meta files and the library assets, so teammates get the finished setup from
    /// git without running this.
    /// Menu: Wreckabulary → Art → Set Up Imported Art.
    /// Batch: Unity -batchmode -quit -projectPath &lt;abs&gt; -executeMethod Wreckabulary.EditorTools.ArtSetup.Run
    /// </summary>
    public static class ArtSetup
    {
        [MenuItem("Wreckabulary/Art/Set Up Imported Art")]
        static void RunFromMenu() => Run();

        public static void Run()
        {
            var report = ImportedArtSettings.ReadReport();

            int textures = 0;
            Batch(() =>
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ImportedArtSettings.Root + "Textures" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    ImportedArtSettings.Apply(ti);
                    ti.SaveAndReimport();
                    textures++;
                }
            });

            var library = MaterialLibraryBuilder.Build();

            int models = 0, remaps = 0;
            var missing = new List<string>();
            Batch(() =>
            {
                foreach (var file in report)
                {
                    if (!(AssetImporter.GetAtPath(file.Output) is ModelImporter mi))
                    {
                        missing.Add(file.Output);
                        continue;
                    }
                    ImportedArtSettings.Apply(mi);
                    if (mi.importAnimation)
                    {
                        var clips = ImportedArtSettings.Clips(mi);
                        if (clips != null) mi.clipAnimations = clips;
                    }
                    foreach (string name in file.Materials)
                    {
                        var mat = library.Find(name);
                        if (mat == null)
                        {
                            missing.Add($"{file.Output}: material {name}");
                            continue;
                        }
                        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
                        remaps++;
                    }
                    mi.SaveAndReimport();
                    models++;
                }
            });
            AssetDatabase.SaveAssets();

            if (missing.Count > 0)
                throw new InvalidOperationException("Art setup is incomplete. Rerun the Blender pipeline, then this. Missing:\n" + string.Join("\n", missing));
            var modelLibrary = ModelLibraryBuilder.Build(report);
            Debug.Log($"ART_SETUP_RESULT {{\"textures\": {textures}, \"materials\": {library.Materials.Count}, \"models\": {models}, \"remaps\": {remaps}, \"modelLibrary\": {modelLibrary.Entries.Count}}}");
        }

        static void Batch(Action work)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                work();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }
    }
}
