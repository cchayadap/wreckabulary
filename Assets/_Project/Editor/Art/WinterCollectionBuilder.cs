using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class WinterCollectionBuilder
    {
        public const string Path = "Assets/_Project/Resources/Collections/Winter.asset";
        const string MaterialFolder = "Assets/_Project/Art/Seasonal/Winter/Finishes";

        [MenuItem("Wreckabulary/Art/Build Winter Collection")]
        public static void Build()
        {
            WinterPropsBuilder.Build();
            WinterEmoteImporter.Build();
            WinterAudioImporter.Build();
            LobbyThemeImporter.ImportAll();
            MaterialLibraryBuilder.EnsureFolder(MaterialFolder);
            MaterialLibraryBuilder.EnsureFolder("Assets/_Project/Resources/Collections");
            var collection = AssetDatabase.LoadAssetAtPath<SeasonalCollection>(Path);
            if (!collection) { collection = ScriptableObject.CreateInstance<SeasonalCollection>(); AssetDatabase.CreateAsset(collection, Path); }
            collection.themeId = "winter"; collection.finish = "Winter";
            collection.recipes = new[] { "SHIELD", "SODA" };
            collection.emoteName = "Winter shuffle";
            collection.decor = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Collections/WinterDecor.prefab");
            collection.emote = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Seasonal/Winter/Animations/WinterShuffle.fbx")
                .OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (!collection.decor || !collection.emote) throw new InvalidOperationException("Winter decor or emote import is missing.");
            var palette = new Dictionary<string, uint>
            {
                ["blue"] = 0x236C57, ["gold"] = 0xE4B451, ["ivory"] = 0xFFF3DB,
                ["wood_dark"] = 0x683E32, ["metal"] = 0xAACCCA, ["red"] = 0xC53943
            };
            var library = MaterialLibrary.Load();
            if (!library) throw new InvalidOperationException("MaterialLibrary is missing.");
            var finishes = new List<Material>();
            foreach (var entry in palette)
            {
                var original = library.Find(entry.Key + "_Classic");
                if (!original) throw new InvalidOperationException("Missing base finish " + entry.Key);
                string path = MaterialFolder + "/" + entry.Key + "_Winter.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, path); }
                else material.CopyPropertiesFromMaterial(original);
                material.name = entry.Key + "_Winter";
                uint rgb = entry.Value;
                material.SetColor("_BaseColor", new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255));
                material.SetFloat("_Smoothness", entry.Key == "gold" || entry.Key == "metal" ? .62f : .30f);
                EditorUtility.SetDirty(material); finishes.Add(material);
            }
            collection.materials = finishes.ToArray();
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            Debug.Log("Winter collection: 3 props, 1 emote, 2 recipe finishes and 6 sound variants imported.");
        }
    }
}
