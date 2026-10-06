using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class ImportedArtSettings
    {
        public const string Root = "Assets/_Project/Art/Imported/";
        public const string ReportPath = "Assets/_Project/Data/Generated/build_report.json";
        public const string MaterialsPath = "Assets/_Project/Data/Generated/materials.json";
        public const string LibraryFolder = "Assets/_Project/Materials/Library";
        public const string LibraryAssetPath = "Assets/_Project/Resources/MaterialLibrary.asset";
        public const string ModelLibraryAssetPath = "Assets/_Project/Resources/ModelLibrary.asset";

        public static readonly string[] LoopingClips = { "Idle", "Walk_InPlace", "Run_InPlace", "Hold_OneHand", "Carry_TwoHand" };

        public static bool IsImportedArt(string assetPath) =>
            assetPath != null && assetPath.Replace('\\', '/').StartsWith(Root, StringComparison.Ordinal);

        static bool In(string assetPath, string folder) =>
            assetPath.Replace('\\', '/').StartsWith(Root + folder + "/", StringComparison.Ordinal);

        public static void Apply(ModelImporter mi)
        {
            string path = mi.assetPath;
            bool avatar = In(path, "Avatar");
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.addCollider = false;
            mi.generateSecondaryUV = In(path, "Environment");
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (avatar)
            {
                mi.animationType = ModelImporterAnimationType.Generic;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.optimizeGameObjects = false;
                mi.importAnimation = true;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
                mi.resampleCurves = true;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        public static ModelImporterClipAnimation[] Clips(ModelImporter mi)
        {
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return null;
            foreach (var clip in clips)
            {
                clip.name = ClipName(clip.takeName);
                clip.loopTime = LoopingClips.Contains(clip.name);
                clip.loopPose = false;
            }
            return clips;
        }

        public static string ClipName(string takeName)
        {
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }

        public static void Apply(TextureImporter ti)
        {
            bool normal = Path.GetFileNameWithoutExtension(ti.assetPath).EndsWith("_NormalGL", StringComparison.Ordinal);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.textureShape = TextureImporterShape.Texture2D;
            ti.sRGBTexture = !normal;
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 512;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 2;

            var so = new SerializedObject(ti);
            var gamma = so.FindProperty("m_ApplyGammaDecoding");
            if (gamma != null && gamma.boolValue)
            {
                gamma.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static bool AppliesGammaDecoding(TextureImporter ti)
        {
            var gamma = new SerializedObject(ti).FindProperty("m_ApplyGammaDecoding");
            return gamma != null && gamma.boolValue;
        }

        public sealed class ReportFile
        {
            public string Kind;
            public string Name;
            public string Output;
            public List<string> Materials;
            public List<string> Empties;
            public List<string> Clips;
            public List<string> Bones;
            public float[] BoundsMin;
            public float[] BoundsMax;
            public int Triangles;
        }

        public static List<ReportFile> ReadReport()
        {
            var root = Json.Parse(File.ReadAllText(ReportPath), Path.GetFileName(ReportPath));
            var files = new List<ReportFile>();
            foreach (var f in root["files"].Items)
            {
                files.Add(new ReportFile
                {
                    Kind = f["kind"].String(),
                    Name = f["name"].String(),
                    Output = f["output"].String(),
                    Materials = f["materials"].Strings(),
                    Empties = f["empties"].Strings(),
                    Clips = f["actions"].Strings(),
                    Bones = f.Has("bones") ? f["bones"].Strings() : new List<string>(),
                    BoundsMin = f["bounds_min"].Floats(3),
                    BoundsMax = f["bounds_max"].Floats(3),
                    Triangles = f["triangles"].Int(),
                });
            }
            return files;
        }

        public sealed class MaterialSpec
        {
            public string Name;
            public Color LinearBaseColor;
            public float Metallic;
            public float Roughness;
            public Color LinearEmissive;
            public string AlphaMode;
            public float AlphaCutoff;
            public bool DoubleSided;
            public string BaseMap;
            public string NormalMap;
            public string EmissionMap;
            public string Family;
            public string Skin;
        }

        public static List<MaterialSpec> ReadMaterials()
        {
            var root = Json.Parse(File.ReadAllText(MaterialsPath), Path.GetFileName(MaterialsPath));
            var specs = new List<MaterialSpec>();
            foreach (var m in root["materials"].Items)
            {
                var c = m["baseColor"].Floats(4);
                var e = m["emissive"].Floats(3);
                specs.Add(new MaterialSpec
                {
                    Name = m["name"].String(),
                    LinearBaseColor = new Color(c[0], c[1], c[2], c[3]),
                    Metallic = m["metallic"].Float(),
                    Roughness = m["roughness"].Float(),
                    LinearEmissive = new Color(e[0], e[1], e[2], 1f),
                    AlphaMode = m["alphaMode"].String("OPAQUE"),
                    AlphaCutoff = m["alphaCutoff"].Float(0.5f),
                    DoubleSided = m["doubleSided"].Bool(false),
                    BaseMap = m["baseMap"].String(null),
                    NormalMap = m["normalMap"].String(null),
                    EmissionMap = m["emissionMap"].String(null),
                    Family = m["family"].String(null),
                    Skin = m["skin"].String(null),
                });
            }
            return specs;
        }
    }
}
