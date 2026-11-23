using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.EditorTools
{
    public static class CourseBackdropKitBake
    {
        public const string AssetPath = "Assets/_Project/Resources/" + CourseBackdropKit.ResourcePath + ".asset";
        public const string StylePath = SurroundingsImport.DataFolder + "/Courses/course_city.json";
        public const string ReceiptPath = SurroundingsBake.ReceiptFolder + "/course_kit.json";
        public const string SkylineShaderPath = "Assets/_Project/Shaders/Skyline.shader";

        static readonly (string id, CourseFootprint footprint)[] Samples =
        {
            ("wordbound-parcel-service-v2", new CourseFootprint(-36, -20, 36, 20, 0, 7.2f)),
            ("sample-arena", new CourseFootprint(-10, -10, 10, 10, 0, 4)),
            ("sample-tower", new CourseFootprint(-48, -42, 48, 18, 0, 30)),
        };

        [Serializable]
        public sealed class Receipt
        {
            public string asset, style;
            public int pieces, slots;
            public bool passed;
            public List<SurroundingsBake.FileHash> inputs = new();
            public List<string> samples = new(), errors = new();
        }

        [MenuItem("Wreckabulary/Art/Bake Course Backdrop Kit")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake the course backdrop kit in Edit Mode.");
            if (AssetDatabase.IsValidFolder(SurroundingsImport.KitFolder)) SurroundingsImport.EnsureImported();
            var receipt = new Receipt { asset = AssetPath, style = StylePath };
            var manifests = SurroundingsImport.LoadManifests();
            var kit = SurroundingsBake.LoadKit();
            var manifestAssets = manifests.Select(m => (m.path, asset: AssetDatabase.LoadAssetAtPath<TextAsset>(m.path))).ToList();
            foreach (var (path, asset) in manifestAssets.Where(m => !m.asset)) receipt.errors.Add("No text asset at " + path);
            var styleAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(StylePath);
            if (!styleAsset) receipt.errors.Add("No course style at " + StylePath);
            var pieces = Pieces(kit, receipt.errors);
            var slots = Slots(receipt.errors);
            receipt.pieces = pieces.Count;
            receipt.slots = slots.Count;
            receipt.inputs = Inputs(kit, manifests.Select(m => m.path)).Select(SurroundingsBake.Hash).ToList();
            if (receipt.errors.Count == 0)
            {
                var built = Save(manifestAssets.Select(m => m.asset), styleAsset, pieces, slots);
                receipt.errors.AddRange(built.Gaps());
                if (receipt.errors.Count == 0) Sample(built, receipt);
            }
            receipt.passed = receipt.errors.Count == 0;
            Write(receipt);
            string summary = $"COURSE_BACKDROP_KIT: {(receipt.passed ? "PASS" : "FAIL")} pieces={receipt.pieces} slots={receipt.slots} asset={AssetPath} receipt={ReceiptPath} {string.Join(" ", receipt.samples)}";
            if (!receipt.passed) throw new InvalidOperationException(summary + "\n" + string.Join("\n", receipt.errors));
            Debug.Log(summary);
        }

        static List<CourseBackdropKit.Piece> Pieces(SurroundingsKit kit, List<string> errors)
        {
            var pieces = new List<CourseBackdropKit.Piece>();
            foreach (var piece in kit.Pieces)
            {
                var node = SurroundingsImport.FindObject(piece.Fbx, piece.FbxObject);
                if (!node) { errors.Add($"{piece.Id}: no mesh object '{piece.FbxObject}' in {piece.Fbx}; run SurroundingsImport.Verify."); continue; }
                var mesh = node.GetComponent<MeshFilter>().sharedMesh;
                var materials = node.TryGetComponent<MeshRenderer>(out var renderer) ? renderer.sharedMaterials : Array.Empty<Material>();
                if (!mesh.isReadable) errors.Add($"{piece.Id}: {piece.Fbx} is not readable; run SurroundingsImport.Verify.");
                if (materials.Length < mesh.subMeshCount) errors.Add($"{piece.Id}: {mesh.subMeshCount} submeshes but {materials.Length} material slots.");
                pieces.Add(new CourseBackdropKit.Piece
                {
                    id = piece.Id, mesh = mesh, slots = materials.Take(mesh.subMeshCount).Select(m => m ? m.name : "").ToArray(),
                    rotation = node.transform.localRotation, scale = node.transform.localScale,
                });
            }
            return pieces;
        }

        static List<CourseBackdropKit.Slot> Slots(List<string> errors)
        {
            var slots = new List<CourseBackdropKit.Slot>();
            foreach (string slot in SurroundingsImport.Slots)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(SurroundingsBake.MaterialPath(slot));
                if (!material) { errors.Add("Missing backdrop material " + SurroundingsBake.MaterialPath(slot)); continue; }
                if (material.shader.name != BackdropShader.ShaderName) errors.Add($"{SurroundingsBake.MaterialPath(slot)} uses {material.shader.name}, not {BackdropShader.ShaderName}.");
                slots.Add(new CourseBackdropKit.Slot { id = slot, material = material });
            }
            var skyline = SkylineMaterial(errors);
            if (skyline) slots.Add(new CourseBackdropKit.Slot { id = CourseSkyline.SkylineSlot, material = skyline });
            return slots;
        }

        public static Material SkylineMaterial(List<string> errors)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkylineShaderPath);
            if (!shader) { errors.Add("Missing skyline shader " + SkylineShaderPath); return null; }
            if (shader.name != CourseSkylineMesh.ShaderName) { errors.Add($"{SkylineShaderPath} is named {shader.name}, not {CourseSkylineMesh.ShaderName}."); return null; }
            if (ShaderUtil.ShaderHasError(shader))
            {
                errors.Add($"{CourseSkylineMesh.ShaderName} has compile errors: " + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
                return null;
            }
            string path = SurroundingsBake.MaterialPath(CourseSkyline.SkylineSlot);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = !material;
            if (fresh) material = new Material(shader);
            else if (material.shader != shader) material.shader = shader;
            material.name = CourseSkyline.SkylineSlot;
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_EmissionColor", CourseSkylineMesh.Glow);
            material.SetFloat("_Smoothness", CourseSkyline.GlassSmoothness);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (fresh) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        static IEnumerable<string> Inputs(SurroundingsKit kit, IEnumerable<string> manifests)
        {
            var paths = new List<string> { StylePath };
            paths.AddRange(manifests);
            foreach (string fbx in kit.Pieces.Select(p => p.Fbx).Where(f => !string.IsNullOrEmpty(f)).Distinct().OrderBy(f => f, StringComparer.Ordinal))
            {
                paths.Add(fbx);
                paths.Add(fbx + ".meta");
            }
            paths.AddRange(SurroundingsImport.Slots.Select(SurroundingsBake.MaterialPath));
            paths.Add(SkylineShaderPath);
            return paths.Distinct(StringComparer.Ordinal);
        }

        static CourseBackdropKit Save(IEnumerable<TextAsset> manifests, TextAsset style, List<CourseBackdropKit.Piece> pieces, List<CourseBackdropKit.Slot> slots)
        {
            var asset = AssetDatabase.LoadAssetAtPath<CourseBackdropKit>(AssetPath);
            bool fresh = !asset;
            if (fresh) asset = ScriptableObject.CreateInstance<CourseBackdropKit>();
            asset.Configure(manifests, style, pieces, slots);
            if (fresh)
            {
                EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            else EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void Sample(CourseBackdropKit kit, Receipt receipt)
        {
            foreach (var (id, footprint) in Samples)
                foreach (CourseSurroundingsTier tier in Enum.GetValues(typeof(CourseSurroundingsTier)))
                {
                    var layout = CourseSurroundings.Draft(kit.Style(), kit.Kit(), id, footprint, tier);
                    receipt.samples.Add($"{id}[{tier} {layout.Renderers}r/{layout.Triangles}t {layout.Placements.Count}p trimmed={layout.Trimmed} hash={layout.Hash}]");
                    receipt.errors.AddRange(layout.Problems.Select(p => $"{id} {tier}: {p}"));
                }
        }

        static void Write(Receipt receipt)
        {
            string path = SurroundingsImport.ProjectPath(ReceiptPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(receipt, true) + "\n");
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
