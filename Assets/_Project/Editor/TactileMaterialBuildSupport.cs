using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class TactileMaterialBuildSupport
    {
        const string Folder = "Assets/_Project/Resources/TactileVariantSeeds";
        const string CollectionPath = "Assets/_Project/Resources/TactileShaderVariants.shadervariants";

        public static int Generate(MaterialLibrary library = null)
        {
            if (!library) library = MaterialLibrary.Load();
            if (!library) throw new InvalidOperationException("Build the imported MaterialLibrary before tactile variants.");
            MaterialLibraryBuilder.EnsureFolder(Folder);
            var albedo = Solid("NeutralDetail", new Color(.5f, .5f, .5f, 1f));
            var normal = Solid("NeutralNormal", new Color(.5f, .5f, 1f, 1f));
            var finish = Solid("NeutralFinish", new Color(0f, 0f, 0f, 1f));
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionPath);
            if (!collection)
            {
                collection = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(collection, CollectionPath);
            }
            collection.Clear();
            int count = 0;
            foreach (var original in library.Materials)
            {
                if (!TactileMaterials.IsEligible(original)) continue;
                string path = Folder + "/" + original.name + ".mat";
                var seed = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = !seed;
                if (created) seed = new Material(original);
                else
                {
                    seed.shader = original.shader;
                    seed.CopyPropertiesFromMaterial(original);
                }
                seed.name = original.name + " tactile build seed";
                seed.SetTexture("_DetailAlbedoMap", albedo);
                seed.SetTexture("_DetailNormalMap", normal);
                seed.SetFloat("_DetailAlbedoMapScale", 1f);
                seed.SetFloat("_DetailNormalMapScale", .1f);
                seed.SetTexture("_MetallicGlossMap", finish);
                seed.DisableKeyword("_DETAIL_SCALED");
                seed.EnableKeyword("_DETAIL_MULX2");
                seed.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (created) AssetDatabase.CreateAsset(seed, path);
                else EditorUtility.SetDirty(seed);
                var keywords = new HashSet<string>(seed.shaderKeywords);
                collection.Add(new ShaderVariantCollection.ShaderVariant(seed.shader,
                    PassType.ScriptableRenderPipeline, new List<string>(keywords).ToArray()));
                count++;
            }
            if (count == 0) throw new InvalidOperationException("No inspected prop material families were found for tactile variants.");
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            Debug.Log("Prepared tactile URP build variants for " + count + " shared materials; native rendering checks remain separate.");
            return count;
        }

        static Texture2D Solid(string name, Color colour)
        {
            string path = Folder + "/" + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture) return texture;
            texture = new Texture2D(4, 4, TextureFormat.RGBA32, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear
            };
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
