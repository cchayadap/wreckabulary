using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public sealed class WinterPropsImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(WinterPropsBuilder.ArtRoot + "/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = importer.importLights = importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessTexture()
        {
            if (assetPath != WinterPropsBuilder.ArtRoot + "/WinterPalette.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }

    public static class WinterPropsBuilder
    {
        public const string ArtRoot = "Assets/_Project/Art/Seasonal/Winter/Props";
        public const string PrefabPath = "Assets/_Project/Resources/Collections/WinterDecor.prefab";
        const string Source = "ArtSource/Collections/Winter/Props";
        static readonly string[] Models = { "WinterTree", "WrappedGifts", "DoorWreath" };

        [Serializable] sealed class Verification { public bool passed; }

        [MenuItem("Wreckabulary/Art/Build Winter Props")]
        public static void Build()
        {
            string report = Source + "/fbx-verification.json";
            if (!File.Exists(report) || !JsonUtility.FromJson<Verification>(File.ReadAllText(report)).passed)
                throw new InvalidOperationException("Verify the staged Blender winter exports before importing them.");
            EnsureFolder(ArtRoot);
            EnsureFolder(ArtRoot + "/Materials");
            EnsureFolder(ArtRoot + "/Meshes");
            EnsureFolder("Assets/_Project/Resources/Collections");
            foreach (string file in Models.Select(name => name + ".fbx").Append("WinterPalette.png"))
            {
                string source = Source + "/exports/" + file, target = ArtRoot + "/" + file;
                if (!File.Exists(target) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(target))) File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/WinterPalette.png");
            var paint = Material("WinterPaint", Color.white, 0f, .36f, texture);
            var brass = Material("WinterBrass", Hex(0xD6AE62), .72f, .69f);
            var wood = Material("WinterDisplayWood", Hex(0xB38452), 0f, .23f);
            foreach (string name in Models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(ArtRoot + "/" + name + ".fbx");
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "WinterPaint"), paint);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "WinterBrass"), brass);
                importer.SaveAndReimport();
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                Debug.Log("WINTER_PROPS_READY: saved editable decor preserved.");
                return;
            }
            var root = new GameObject("Winter decor");
            try
            {
                var tree = BakeProp("WinterTree", "Winter tree", root.transform);
                tree.localPosition = new Vector3(-.9f, 0f, .70f);
                tree.localScale = Vector3.one * .82f;
                var gifts = BakeProp("WrappedGifts", "Winter gifts", root.transform, true);
                gifts.localPosition = new Vector3(-.72f, 0f, -.10f);
                gifts.localScale = Vector3.one * .60f;
                var stand = new GameObject("Wreath display").transform;
                stand.SetParent(root.transform, false);
                Box("Stand foot", stand, new Vector3(0f, .04f, 0f), new Vector3(.55f, .08f, .30f), wood);
                Box("Stand post", stand, new Vector3(0f, .53f, .035f), new Vector3(.07f, .98f, .07f), wood);
                Box("Wreath mounting board", stand, new Vector3(0f, .78f, 0f), new Vector3(.73f, .76f, .035f), wood);
                var wreath = BakeProp("DoorWreath", "Mounted wreath", stand);
                var bounds = ModelVisual.BoundsIn(wreath, wreath.gameObject);
                wreath.localPosition = new Vector3(0f, .42f, -.02f - bounds.max.z);
                stand.localPosition = new Vector3(.88f, 0f, .95f);
                stand.localScale = Vector3.one * .78f;
                if (!PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)) throw new InvalidOperationException("Could not save winter decor.");
                AssetDatabase.SaveAssets();
                Debug.Log("WINTER_PROPS_READY: three verified native props, reusable gifts, mounted wreath and persistent materials.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform BakeProp(string name, string label, Transform parent, bool separateGifts = false)
        {
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/" + name + ".fbx");
            if (!imported) throw new InvalidOperationException("Missing winter prop " + name);
            var staging = new GameObject("Source placement anchor");
            var target = new GameObject(label).transform;
            target.SetParent(parent, false);
            try
            {
                var model = Object.Instantiate(imported, staging.transform, false);
                var front = ModelVisual.FindNamed(model, name + "_Front");
                if (!front) throw new InvalidOperationException("Missing authored facing marker for " + name);
                var direction = staging.transform.InverseTransformPoint(front.position);
                direction.y = 0f;
                staging.transform.rotation = Quaternion.FromToRotation(direction.normalized, Vector3.back);
                if (separateGifts)
                    foreach (string childName in new[] { "GiftTall", "GiftWide", "GiftSmall" })
                    {
                        var child = ModelVisual.FindNamed(model, childName);
                        if (!child) throw new InvalidOperationException("Missing reusable parcel " + childName);
                        var parcel = new GameObject(childName).transform;
                        parcel.SetParent(target, false);
                        parcel.localPosition = staging.transform.TransformPoint(staging.transform.InverseTransformPoint(child.position));
                        Combine(child.gameObject, parcel, name + "_" + childName);
                    }
                else Combine(model, target, name);
                return target;
            }
            finally { Object.DestroyImmediate(staging); }
        }

        static void Combine(GameObject source, Transform target, string name)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    var material = renderer.sharedMaterials[i];
                    if (!groups.TryGetValue(material, out var group)) groups.Add(material, group = new List<CombineInstance>());
                    group.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i,
                        transform = target.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            var materials = groups.Keys.OrderBy(material => material.name, StringComparer.Ordinal).ToArray();
            var temporary = new List<Mesh>();
            string path = ArtRoot + "/Meshes/" + name + ".asset";
            var combined = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!combined)
            {
                try
                {
                    var submeshes = new CombineInstance[materials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var mesh = new Mesh();
                        temporary.Add(mesh);
                        mesh.CombineMeshes(groups[materials[i]].ToArray(), true, true);
                        submeshes[i] = new CombineInstance { mesh = mesh };
                    }
                    combined = new Mesh { name = name };
                    combined.CombineMeshes(submeshes, false, false);
                    combined.RecalculateBounds();
                    AssetDatabase.CreateAsset(combined, path);
                }
                finally { foreach (var mesh in temporary) Object.DestroyImmediate(mesh); }
            }
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = combined;
            var result = target.gameObject.AddComponent<MeshRenderer>();
            result.sharedMaterials = materials;
            result.shadowCastingMode = ShadowCastingMode.On;
        }

        static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Material Material(string name, Color colour, float metallic, float smoothness, Texture texture = null)
        {
            string path = ArtRoot + "/Materials/" + name + ".mat";
            var saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (saved) return saved;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP/Lit is required for winter props.");
            saved = new Material(shader) { name = name };
            saved.SetColor("_BaseColor", colour);
            saved.SetFloat("_Metallic", metallic);
            saved.SetFloat("_Smoothness", smoothness);
            if (texture) saved.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(saved, path);
            return saved;
        }

        static Color Hex(uint value) => new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
