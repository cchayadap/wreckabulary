using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public sealed class WordboundEquipmentImporter : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/_Project/Art/Collections/WordboundPvP";
        public const string CollectionPath = "Assets/_Project/Resources/Collections/WordboundPvP.asset";
        public const string SourceRoot = "ArtSource/Collections/WordboundPvP";
        public const string CataloguePath = "Assets/_Project/Data/Config/items.json";
        public const string PaintName = "WordboundPvPPaint";
        public const string BrassName = "WordboundPvPBrass";
        static readonly string[] ids = { "BAT", "BROOM", "HAMMER", "SPEAR", "SHIELD", "PLATE", "TABLE", "SOFA", "BED", "STOOL", "FAN", "CLOCK" };
        public static IReadOnlyList<string> ItemIds => ids;
        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot + "/Models/", StringComparison.Ordinal)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        void OnPreprocessTexture()
        {
            bool icon = assetPath.StartsWith(ArtRoot + "/Icons/", StringComparison.Ordinal);
            if (!icon && assetPath != ArtRoot + "/WordboundPvPPalette.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = icon ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = icon ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = icon;
            importer.mipmapEnabled = !icon;
            importer.isReadable = false;
            importer.maxTextureSize = 512;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (icon)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 256;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
        }

        [Serializable] public sealed class FileRecord { public string path; public string sha256; }
        [Serializable] public sealed class ExportRecords { public FileRecord fbx; public FileRecord glb; }
        [Serializable] public sealed class PreviewRecord { public string view; public string path; public string sha256; }
        [Serializable] public sealed class AssetRecord
        {
            public string id;
            public int triangles;
            public string[] materials;
            public float[] size_unity_metres;
            public float[] grip_unity_metres;
            public float held_scale;
            public bool has_uv;
            public ExportRecords exports;
            public PreviewRecord[] previews;
        }
        [Serializable] public sealed class Manifest { public FileRecord palette; public AssetRecord[] assets; }
        [Serializable] sealed class ExportCheck { public string id; public string format; public bool passed; }
        [Serializable] sealed class ExportVerification { public bool passed; public ExportCheck[] checks; }
        [Serializable] sealed class DeliveryVerification { public bool passed; public int concepts; public int models; public int exports; public int icons; }

        public static Manifest ReadVerifiedManifest()
        {
            var delivery = Read<DeliveryVerification>("delivery-verification.json");
            var verification = Read<ExportVerification>("export-verification.json");
            if (!delivery.passed || delivery.concepts != 12 || delivery.models != 12 || delivery.exports != 24 || delivery.icons != 12 ||
                !verification.passed || verification.checks == null || verification.checks.Length != 24)
                throw new InvalidOperationException("The twelve-asset delivery and both-format Blender verification must pass first.");
            var manifest = Read<Manifest>("manifest.json");
            if (manifest.assets == null || manifest.assets.Length != ids.Length ||
                !manifest.assets.Select(asset => asset.id).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(id => id, StringComparer.Ordinal)))
                throw new InvalidOperationException("The Wordbound manifest must contain exactly the twelve approved item IDs.");
            VerifyFile(manifest.palette, "Exports/WordboundPvPPalette.png");
            var catalogue = ItemCatalogue.FromJson(File.ReadAllText(CataloguePath), CataloguePath);
            foreach (var asset in manifest.assets)
            {
                if (!catalogue.TryGet(asset.id, out var item) || !item.Enabled || string.IsNullOrEmpty(item.Model))
                    throw new InvalidOperationException("No enabled catalogue model for " + asset.id);
                if (!Near(Vector(asset.size_unity_metres), Vector(item.Size), .000001f) ||
                    !Near(Vector(asset.grip_unity_metres), Vector(item.Grip), .000001f) ||
                    Mathf.Abs(asset.held_scale - item.HeldScale) > .000001f ||
                    asset.triangles <= 0 || asset.triangles > 6000 || !asset.has_uv ||
                    asset.materials == null || !asset.materials.OrderBy(name => name).SequenceEqual(new[] { BrassName, PaintName }))
                    throw new InvalidOperationException("Catalogue or mesh contract changed for " + asset.id);
                foreach (string format in new[] { "fbx", "glb" })
                    if (verification.checks.Count(check => check.id == asset.id && check.format == format && check.passed) != 1)
                        throw new InvalidOperationException("Missing unique passing export check for " + asset.id + "/" + format);
                VerifyFile(asset.exports.fbx, "Exports/" + asset.id + ".fbx");
                VerifyFile(asset.exports.glb, "Exports/" + asset.id + ".glb");
                var icon = asset.previews.Single(preview => preview.view == "icon");
                VerifyFile(new FileRecord { path = icon.path, sha256 = icon.sha256 }, "Icons/" + asset.id + ".png");
            }
            return manifest;
        }

        [MenuItem("Wreckabulary/Art/Build Wordbound Equipment Collection")]
        public static void Build()
        {
            var manifest = ReadVerifiedManifest();
            foreach (string folder in new[] { ArtRoot + "/Models", ArtRoot + "/Icons", ArtRoot + "/Materials", "Assets/_Project/Resources/Collections" })
                EnsureFolder(folder);
            CopyImport("Exports/WordboundPvPPalette.png", ArtRoot + "/WordboundPvPPalette.png");
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/WordboundPvPPalette.png");
            if (!palette || palette.width > 512 || palette.height > 512) throw new InvalidOperationException("Invalid imported Wordbound palette.");
            var paint = MakeMaterial(PaintName, Color.white, 0f, .38f, palette);
            var brass = MakeMaterial(BrassName, new Color32(0xD6, 0xAE, 0x62, 255), .72f, .70f, null);
            var catalogue = ItemCatalogue.FromJson(File.ReadAllText(CataloguePath), CataloguePath);
            var entries = new List<WordboundEquipmentCollection.Entry>();
            foreach (var asset in manifest.assets)
            {
                string modelPath = ArtRoot + "/Models/" + asset.id + ".fbx";
                string iconPath = ArtRoot + "/Icons/" + asset.id + ".png";
                CopyImport(asset.exports.fbx.path, modelPath);
                CopyImport("Icons/" + asset.id + ".png", iconPath);
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), PaintName), paint);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), BrassName), brass);
                importer.SaveAndReimport();
                var entry = new WordboundEquipmentCollection.Entry
                {
                    itemId = asset.id, modelKey = catalogue.Get(asset.id).Model,
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),
                    icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath),
                    size = Vector(asset.size_unity_metres), grip = Vector(asset.grip_unity_metres), heldScale = asset.held_scale
                };
                ValidateImported(entry, asset.triangles, paint, brass);
                entries.Add(entry);
            }
            var collection = AssetDatabase.LoadAssetAtPath<WordboundEquipmentCollection>(CollectionPath);
            if (!collection)
            {
                collection = ScriptableObject.CreateInstance<WordboundEquipmentCollection>();
                AssetDatabase.CreateAsset(collection, CollectionPath);
            }
            collection.SetEntries(entries);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssetIfDirty(collection);
            Debug.Log("WORDBOUND_EQUIPMENT_READY: twelve independent models/icons; shared catalogue and model/material libraries preserved.");
        }

        public static void ValidateImported(WordboundEquipmentCollection.Entry entry, int expectedTriangles, Material paint, Material brass)
        {
            if (!entry.model || !entry.icon || !paint || !brass) throw new InvalidOperationException("Missing imported assets for " + entry.itemId);
            var anchor = new GameObject("Wordbound import verification");
            try
            {
                var copy = Object.Instantiate(entry.model, anchor.transform, false);
                var bounds = ModelVisual.BoundsIn(anchor.transform, copy);
                var grip = ModelVisual.FindNamed(copy, "Grip_R");
                var front = ModelVisual.FindNamed(copy, entry.itemId + "_Front");
                if (!Near(bounds.size, entry.size, .001f) || Mathf.Abs(bounds.min.y) > .001f ||
                    !grip || !Near(anchor.transform.InverseTransformPoint(grip.position), entry.grip, .001f))
                    throw new InvalidOperationException("Unity dimensions, grounding or Grip_R differs for " + entry.itemId);
                if (!front || !Near(anchor.transform.InverseTransformPoint(front.position),
                    new Vector3(0f, entry.size.y * .5f, entry.size.z * .6f), .001f))
                    throw new InvalidOperationException("Unity front marker does not face +Z for " + entry.itemId);
                if (copy.GetComponentsInChildren<Collider>(true).Length != 0 || copy.GetComponentsInChildren<Camera>(true).Length != 0 ||
                    copy.GetComponentsInChildren<Light>(true).Length != 0)
                    throw new InvalidOperationException("Visual models must not bring gameplay colliders, cameras or lights.");
                var filters = copy.GetComponentsInChildren<MeshFilter>(true);
                long triangles = 0;
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                        throw new InvalidOperationException("Missing mesh or UVs for " + entry.itemId);
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        if (mesh.GetTopology(submesh) != MeshTopology.Triangles) throw new InvalidOperationException("Non-triangle mesh.");
                        triangles += mesh.GetIndexCount(submesh) / 3;
                    }
                }
                if (triangles != expectedTriangles || triangles > 6000) throw new InvalidOperationException("Unity triangle count differs for " + entry.itemId);
                var renderers = copy.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0 || renderers.Any(renderer => renderer.sharedMaterials.Length == 0 ||
                    renderer.sharedMaterials.Any(material => material != paint && material != brass)))
                    throw new InvalidOperationException("Unity material remapping failed for " + entry.itemId);
                if (renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count() != 2)
                    throw new InvalidOperationException("Expected both collection materials for " + entry.itemId);
            }
            finally { Object.DestroyImmediate(anchor); }
        }

        static T Read<T>(string file) => JsonUtility.FromJson<T>(File.ReadAllText(SourceRoot + "/" + file));
        static Vector3 Vector(float[] values)
        {
            if (values == null || values.Length != 3 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidOperationException("Expected three finite coordinates.");
            return new Vector3(values[0], values[1], values[2]);
        }
        static bool Near(Vector3 left, Vector3 right, float tolerance) =>
            Mathf.Abs(left.x - right.x) <= tolerance && Mathf.Abs(left.y - right.y) <= tolerance && Mathf.Abs(left.z - right.z) <= tolerance;

        static void VerifyFile(FileRecord record, string expectedPath)
        {
            if (record == null || record.path != expectedPath || string.IsNullOrEmpty(record.sha256))
                throw new InvalidOperationException("Unexpected source path: " + expectedPath);
            using (var sha = SHA256.Create())
            using (var input = File.OpenRead(SourceRoot + "/" + expectedPath))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                if (hash != record.sha256) throw new InvalidOperationException("Source changed since verification: " + expectedPath);
            }
        }

        static void CopyImport(string source, string target)
        {
            byte[] bytes = File.ReadAllBytes(SourceRoot + "/" + source);
            if (!File.Exists(target) || !File.ReadAllBytes(target).SequenceEqual(bytes)) File.WriteAllBytes(target, bytes);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        static Material MakeMaterial(string name, Color color, float metallic, float smoothness, Texture palette)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP/Lit is required for Wordbound equipment.");
            string path = ArtRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", palette);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
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
