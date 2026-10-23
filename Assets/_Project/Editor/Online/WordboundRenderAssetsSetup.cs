using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class WordboundRenderAssetsSetup
    {
        [MenuItem("Tools/Wordbound/Create or verify render materials")]
        public static void Create()
        {
            Save(WordboundRenderMaterials.OpaquePath, false);
            Save(WordboundRenderMaterials.TransparentPath, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wreckabulary] Wordbound opaque and transparent render materials saved and verified.");
        }

        static void Save(string resource, bool transparent)
        {
            string path = "Assets/_Project/Resources/" + resource + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                if (File.Exists(path) || AssetDatabase.LoadMainAssetAtPath(path))
                    throw new InvalidOperationException("Preserve conflicting render asset: " + path);
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) throw new InvalidOperationException("The installed URP Unlit shader is required.");
                EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Surface", transparent ? 1 : 0);
                material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
                material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                material.SetFloat("_ZWrite", transparent ? 0 : 1);
                if (transparent) material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)(transparent ? RenderQueue.Transparent : RenderQueue.Geometry);
                material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            if (!material || !material.shader || material.shader.name != "Universal Render Pipeline/Unlit" ||
                !material.HasProperty("_BaseMap") || !material.HasProperty("_BaseColor") ||
                material.GetFloat("_Surface") != (transparent ? 1 : 0) ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") != transparent)
                throw new InvalidOperationException("Render material differs from the required surface contract; preserve and inspect " + path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
