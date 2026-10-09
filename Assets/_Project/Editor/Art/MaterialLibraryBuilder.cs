using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class MaterialLibraryBuilder
    {
        public static MaterialLibrary Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader wasn't found. Is URP the active render pipeline?");
            EnsureFolder(ImportedArtSettings.LibraryFolder);
            EnsureFolder(System.IO.Path.GetDirectoryName(ImportedArtSettings.LibraryAssetPath).Replace('\\', '/'));

            var specs = ImportedArtSettings.ReadMaterials();
            var built = new List<Material>();
            foreach (var spec in specs) built.Add(BuildMaterial(spec, shader));

            var expected = new HashSet<string>(specs.Select(s => s.Name));
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedArtSettings.LibraryFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!expected.Contains(name))
                    Debug.LogWarning($"{path} is no longer in materials.json. Delete it once nothing uses it.");
            }

            var library = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ImportedArtSettings.LibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(library, ImportedArtSettings.LibraryAssetPath);
            }
            library.Set(built.OrderBy(m => m.name, StringComparer.Ordinal));
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        static Material BuildMaterial(ImportedArtSettings.MaterialSpec s, Shader lit)
        {
            string path = $"{ImportedArtSettings.LibraryFolder}/{s.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(lit) { name = s.Name };
            else if (mat.shader != lit) mat.shader = lit;

            mat.SetColor("_BaseColor", s.LinearBaseColor.gamma);
            mat.SetFloat("_Metallic", s.Metallic);
            mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - s.Roughness));
            mat.SetTexture("_BaseMap", Load(s.BaseMap));
            mat.SetTexture("_BumpMap", Load(s.NormalMap));
            mat.SetFloat("_BumpScale", 1f);

            bool emits = s.LinearEmissive.maxColorComponent > 0f || s.EmissionMap != null;
            mat.SetColor("_EmissionColor", emits ? s.LinearEmissive.gamma : Color.black);
            mat.SetTexture("_EmissionMap", Load(s.EmissionMap));
            mat.globalIlluminationFlags = emits ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            bool transparent = s.AlphaMode == "BLEND";
            bool cutout = s.AlphaMode == "MASK";
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", s.AlphaCutoff);
            mat.SetFloat("_Cull", s.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);

            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                throw new InvalidOperationException(
                    $"materials.json names {path}, which didn't load as a texture (guid '{AssetDatabase.AssetPathToGUID(path)}', " +
                    $"main type {AssetDatabase.GetMainAssetTypeAtPath(path)}, main asset {(main == null ? "none" : main.GetType().Name)}, " +
                    $"on disk {System.IO.File.Exists(path)}). Rerun the Blender pipeline if it's missing.");
            }
            return tex;
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
