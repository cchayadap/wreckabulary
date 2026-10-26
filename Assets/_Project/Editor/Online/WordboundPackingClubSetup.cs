using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WordboundPackingClubSetup
    {
        public const string SourceRoot = "ArtSource/Collections/PackingClub/V1/build-20261026-a";
        public const string ArtRoot = "Assets/_Project/Art/Collections/PackingClub/V1";
        public const string ProfilePath = "Assets/_Project/Data/Wordbound/Authoring/PackingClubPreview.asset";
        const string PaletteName = "PackingClubPalette.png";
        static readonly string[] Items = { "BAT", "SHIELD" };
        static readonly string[] Materials = { "PackingClub_Surface", "PackingClub_Wrap" };
        static readonly JsonLoadSettings JsonSettings = new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error };

        sealed class Entry
        {
            public string Id, ModelSource, IconSource;
            public int Triangles;
            public Bounds Bounds;
            public Matrix4x4 Grip;
        }

        sealed class Source
        {
            public string Root, Palette, ManifestHash, VerificationHash;
            public readonly List<Entry> Entries = new List<Entry>();
        }

        [MenuItem("Tools/Wordbound/Create or verify Packing Club comparison")]
        public static void Configure() => ConfigureFrom(SelectedSourceRoot());

        public static void ConfigureFrom(string sourceRoot)
        {
            RequireEditMode();
            var source = ReadVerifiedSource(ValidateSourceRoot(sourceRoot));
            PreflightDestinations(source);
            foreach (string folder in new[] { ArtRoot + "/Models", ArtRoot + "/Icons", ArtRoot + "/Materials", ArtRoot + "/Prefabs" })
                EnsureFolder(folder);
            string palettePath = ArtRoot + "/" + PaletteName;
            var palette = ImportTexture(source.Palette, palettePath, false);
            var surface = MakeMaterial(Materials[0], .32f, palette);
            var wrap = MakeMaterial(Materials[1], .14f, palette);
            var models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (var entry in source.Entries)
            {
                ImportTexture(entry.IconSource, ArtRoot + "/Icons/" + entry.Id + ".png", true);
                var model = ImportModel(entry, surface, wrap);
                ValidateModel(model, entry, surface, wrap);
                string path = ArtRoot + "/Prefabs/" + entry.Id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab)
                {
                    RequireUnused(path);
                    var root = new GameObject("Packing Club " + entry.Id);
                    try
                    {
                        var child = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                        if (!child) throw new InvalidOperationException("Could not instantiate imported " + entry.Id);
                        ValidateModel(root, entry, surface, wrap);
                        prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                        if (!prefab) throw new InvalidOperationException("Could not save comparison prefab: " + path);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                ValidateModel(prefab, entry, surface, wrap);
                models.Add(entry.Id, prefab);
            }
            bool created = CreateProfile(models);
            Debug.Log("[Wreckabulary] PACKING_CLUB_IMPORT: models=2; triangles=BAT:" + source.Entries.Single(e => e.Id == "BAT").Triangles +
                ",SHIELD:" + source.Entries.Single(e => e.Id == "SHIELD").Triangles + "; manifest=" + source.ManifestHash +
                "; source=" + source.Root + "; verification=" + source.VerificationHash + "; profile=" + ProfilePath +
                "; profileCreated=" + created + "; optional course-wide comparison; defaults and per-player skins unchanged.");
        }

        [MenuItem("Tools/Wordbound/Validate Packing Club comparison")]
        public static void Validate()
        {
            RequireEditMode();
            var source = ReadVerifiedSource(SelectedSourceRoot());
            PreflightDestinations(source);
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/" + PaletteName);
            if (!palette || palette.width != 512 || palette.height != 512) throw new InvalidOperationException("Import the palette first.");
            var materials = Materials.Select(name => AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/" + name + ".mat")).ToArray();
            foreach (var material in materials) ValidateMaterial(material, palette);
            foreach (var entry in source.Entries)
            {
                ValidateModel(AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Models/" + entry.Id + ".fbx"), entry, materials[0], materials[1]);
                ValidateModel(AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Prefabs/" + entry.Id + ".prefab"), entry, materials[0], materials[1]);
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Icons/" + entry.Id + ".png"))
                    throw new InvalidOperationException("Missing comparison icon: " + entry.Id);
            }
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(ProfilePath);
            if (!profile) throw new InvalidOperationException("Create the optional comparison profile first.");
            profile.Validate();
            Debug.Log("[Wreckabulary] PACKING_CLUB_VALIDATE: two models/prefabs; manifest=" + source.ManifestHash +
                "; existing authored profile preserved; visual acceptance remains pending.");
        }

        static Source ReadVerifiedSource(string sourceRoot)
        {
            string manifestPath = ProjectPath(sourceRoot + "/manifest.json");
            string verificationPath = ProjectPath(sourceRoot + "/export-verification.json");
            var manifest = ReadObject(manifestPath);
            var verification = ReadObject(verificationPath);
            string manifestHash = Hash(manifestPath);
            if ((int?)manifest["schema"] != 1 || (int?)manifest["version"] != 1 || (string)manifest["collection"] != "PackingClub" ||
                (string)verification["result"] != "PASS" || (string)verification["manifest_sha256"] != manifestHash ||
                (int?)verification["expected_export_count"] != 8 || (int?)verification["checked_export_count"] != 8)
                throw new InvalidOperationException("Packing Club requires its exact passing eight-export verification.");
            var assets = manifest["assets"] as JArray;
            var checks = verification["checks"] as JArray;
            if (assets == null || assets.Count != 4 || checks == null || checks.Count != 8)
                throw new InvalidOperationException("Expected exactly four item/LOD records and eight export checks.");
            string palette = VerifyFile(sourceRoot, manifest["palette"], "Exports/" + PaletteName);
            if ((string)verification["verified_palette_sha256"] != Hash(palette)) throw new InvalidOperationException("Palette verification differs.");
            CheckPng(palette, false);
            VerifyFile(sourceRoot, manifest["source_blend"], "Source/PackingClub.blend");
            string scriptHash = (string)manifest["script_sha256"];
            if (Hash(ProjectPath(sourceRoot + "/Source/packing_club.py")) != scriptHash)
                throw new InvalidOperationException("Copied generator differs from the source manifest.");
            var catalogue = ItemCatalogue.FromJson(File.ReadAllText(ProjectPath("Assets/_Project/Data/Config/items.json")));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var seenChecks = new HashSet<string>(StringComparer.Ordinal);
            var source = new Source { Root = sourceRoot, Palette = palette, ManifestHash = manifestHash, VerificationHash = Hash(verificationPath) };
            foreach (var token in assets)
            {
                var asset = token as JObject ?? throw new InvalidOperationException("Invalid asset record.");
                string item = (string)asset["item_id"];
                int lod = Integer(asset["lod"]);
                if (!Items.Contains(item) || (lod != 0 && lod != 1) || (string)asset["skin_id"] != "PackingClub" || !seen.Add(item + "/" + lod))
                    throw new InvalidOperationException("Duplicate or unexpected item/LOD record.");
                int budget = item == "BAT" ? (lod == 0 ? 3000 : 1200) : (lod == 0 ? 1200 : 600);
                int triangles = Integer(asset["triangles"]);
                if (Integer(asset["triangle_budget"]) != budget || triangles <= 0 || triangles > budget || !NamesEqual(asset["materials"], Materials))
                    throw new InvalidOperationException("Invalid geometry budget or material identities: " + item);
                if (!catalogue.TryGet(item, out var definition) || !definition.Enabled ||
                    !Near(Vector(asset["canonical_gameplay_size"]), new Vector3(definition.Size[0], definition.Size[1], definition.Size[2]), .000001f) ||
                    !Near(Vector(asset["grip_game_metres"]), new Vector3(definition.Grip[0], definition.Grip[1], definition.Grip[2]), .000001f) ||
                    Mathf.Abs(Number(asset["held_scale"]) - definition.HeldScale) > .000001f)
                    throw new InvalidOperationException("Canonical item contract changed: " + item);
                var exports = asset["exports"] as JObject;
                if (exports == null || !exports.Properties().Select(property => property.Name).OrderBy(name => name).SequenceEqual(new[] { "fbx", "glb" }))
                    throw new InvalidOperationException("Each asset requires exactly FBX and GLB exports.");
                string suffix = lod == 0 ? "" : "_LOD1";
                foreach (string format in new[] { "fbx", "glb" })
                {
                    string exported = VerifyFile(sourceRoot, exports[format], "Exports/" + item + suffix + "." + format);
                    var matching = checks.OfType<JObject>().Where(check => (string)check["item_id"] == item &&
                        (int?)check["lod"] == lod && (string)check["format"] == format).ToArray();
                    if (matching.Length != 1 || (string)matching[0]["result"] != "PASS" ||
                        (string)matching[0]["export_sha256"] != Hash(exported) || Integer(matching[0]["triangles"]) != triangles ||
                        !seenChecks.Add(item + "/" + lod + "/" + format))
                        throw new InvalidOperationException("Missing unique passing export check: " + item + "/" + lod + "/" + format);
                    var bindings = matching[0]["palette_bindings"] as JArray;
                    if (bindings == null || bindings.Count != 2 || !bindings.OfType<JObject>().Select(binding => (string)binding["material"]).OrderBy(name => name).SequenceEqual(Materials))
                        throw new InvalidOperationException("Missing verified palette bindings.");
                    foreach (var binding in bindings)
                        if ((string)binding["binding"] != "Principled Base Color <- Image Color" ||
                            Number(binding["max_decoded_channel_error"]) > 1f / 255f + .00001f ||
                            (format == "fbx" && (bool?)binding["verified_external_reference"] != true) ||
                            !(binding["image_size"] is JArray size) || size.Count != 2 || Integer(size[0]) != 512 || Integer(size[1]) != 512)
                            throw new InvalidOperationException("Incomplete palette verification.");
                }
                VerifyFile(sourceRoot, asset["original"], "Source/original-" + item + ".glb");
                if (lod != 0) continue;
                var boundsRows = asset["bounds_game_metres"] as JArray;
                if (boundsRows == null || boundsRows.Count != 2) throw new InvalidOperationException("Expected min/max game-space bounds.");
                Vector3 low = Vector(boundsRows[0]), high = Vector(boundsRows[1]);
                if ((high - low).x <= 0 || (high - low).y <= 0 || (high - low).z <= 0 || Mathf.Abs(low.y) > .00001f)
                    throw new InvalidOperationException("Invalid grounded visual bounds.");
                var swap = Matrix4x4.identity;
                swap.m11 = swap.m22 = 0; swap.m12 = swap.m21 = 1;
                var halfTurn = Matrix4x4.Rotate(Quaternion.AngleAxis(180f, Vector3.forward));
                Matrix4x4 grip = swap * halfTurn * Matrix(asset["grip_matrix_blender"]) * swap;
                if (!Near(grip.GetColumn(3), Vector(asset["grip_game_metres"]), .00001f))
                    throw new InvalidOperationException("Composed socket conversion differs from canonical grip.");
                string icon = VerifiedIcon(sourceRoot, item, manifest);
                source.Entries.Add(new Entry { Id = item, ModelSource = VerifyFile(sourceRoot, exports["fbx"], "Exports/" + item + ".fbx"),
                    IconSource = icon, Triangles = triangles, Bounds = new Bounds((low + high) * .5f, high - low), Grip = grip });
            }
            if (seen.Count != 4 || seenChecks.Count != 8 || source.Entries.Count != 2)
                throw new InvalidOperationException("Incomplete asset/export matrix.");
            return source;
        }

        static string VerifiedIcon(string sourceRoot, string item, JObject manifest)
        {
            string stem = sourceRoot + "/Previews/" + item + "-three-quarter-candidate";
            var receipt = ReadObject(ProjectPath(stem + ".json"));
            string image = ProjectPath(stem + ".png"), icon = ProjectPath(sourceRoot + "/Icons/" + item + ".png");
            if ((string)receipt["item_id"] != item || (string)receipt["variant"] != "candidate" || (string)receipt["view"] != "three-quarter" ||
                (string)receipt["blend_sha256"] != (string)(manifest["source_blend"]?["sha256"]) ||
                (string)receipt["script_sha256"] != (string)manifest["script_sha256"] ||
                (string)receipt["alpha_background_and_content"] != "PASS" || (string)receipt["sha256"] != Hash(image) || Hash(icon) != Hash(image))
                throw new InvalidOperationException("Render both verified candidate icons before importing: " + item);
            CheckPng(icon, true);
            return icon;
        }

        static void PreflightDestinations(Source source)
        {
            CheckCopy(source.Palette, ArtRoot + "/" + PaletteName);
            foreach (var entry in source.Entries)
            {
                CheckCopy(entry.ModelSource, ArtRoot + "/Models/" + entry.Id + ".fbx");
                CheckCopy(entry.IconSource, ArtRoot + "/Icons/" + entry.Id + ".png");
                CheckAssetType<GameObject>(ArtRoot + "/Prefabs/" + entry.Id + ".prefab");
            }
            foreach (string name in Materials) CheckAssetType<Material>(ArtRoot + "/Materials/" + name + ".mat");
            CheckAssetType<WordboundVisualProfile>(ProfilePath);
        }

        static Texture2D ImportTexture(string source, string destination, bool icon)
        {
            if (CopyNew(source, destination))
            {
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
                importer.textureType = icon ? TextureImporterType.Sprite : TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = true;
                importer.alphaSource = icon ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = icon;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.maxTextureSize = 512;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = icon ? FilterMode.Bilinear : FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (icon)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 256;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings);
                }
                importer.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(destination);
            if (!texture || texture.width != 512 || texture.height != 512 || (icon && !AssetDatabase.LoadAssetAtPath<Sprite>(destination)))
                throw new InvalidOperationException("Imported texture differs; preserve and inspect " + destination);
            return texture;
        }

        static Material MakeMaterial(string name, float smoothness, Texture2D palette)
        {
            string path = ArtRoot + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                RequireUnused(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit is required.");
                material = new Material(shader) { name = name };
                material.SetTexture("_BaseMap", palette);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_Cull", (float)CullMode.Back);
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
            }
            ValidateMaterial(material, palette);
            return material;
        }

        static void ValidateMaterial(Material material, Texture2D palette)
        {
            if (!material || !material.shader || material.shader.name != "Universal Render Pipeline/Lit" ||
                material.GetTexture("_BaseMap") != palette || material.GetFloat("_Surface") != 0f ||
                !Finite(material.GetFloat("_Smoothness")) || material.GetFloat("_Smoothness") < 0f || material.GetFloat("_Smoothness") > 1f)
                throw new InvalidOperationException("Preserve and inspect the authored palette material; no automatic replacement is allowed.");
        }

        static GameObject ImportModel(Entry entry, Material surface, Material wrap)
        {
            string path = ArtRoot + "/Models/" + entry.Id + ".fbx";
            if (CopyNew(entry.ModelSource, path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importCameras = importer.importLights = importer.importVisibility = importer.importAnimation = importer.importBlendShapes = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.optimizeGameObjects = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.None;
                importer.addCollider = importer.isReadable = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), Materials[0]), surface);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), Materials[1]), wrap);
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!model) throw new InvalidOperationException("Import is incomplete; preserve and inspect " + path);
            return model;
        }

        static void ValidateModel(GameObject model, Entry entry, Material surface, Material wrap)
        {
            WordboundVisualProfile.ValidateModel(model);
            var anchor = new GameObject("Packing Club validation");
            try
            {
                var copy = Object.Instantiate(model, anchor.transform, false);
                foreach (var component in copy.GetComponentsInChildren<Component>(true))
                    if (!component || !(component is Transform || component is MeshFilter || component is MeshRenderer))
                        throw new InvalidOperationException("Only rigid render components are allowed: " + entry.Id);
                var filters = copy.GetComponentsInChildren<MeshFilter>(true);
                var renderers = copy.GetComponentsInChildren<MeshRenderer>(true);
                if (filters.Length != 1 || renderers.Length != 1 || filters[0].gameObject != renderers[0].gameObject)
                    throw new InvalidOperationException("Expected one combined rigid mesh: " + entry.Id);
                var mesh = filters[0].sharedMesh;
                if (!mesh || mesh.subMeshCount != 2 || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0) || !mesh.HasVertexAttribute(VertexAttribute.Normal))
                    throw new InvalidOperationException("Expected two textured, normal-bearing submeshes: " + entry.Id);
                long triangles = 0;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles || mesh.GetIndexCount(submesh) == 0 || mesh.GetIndexCount(submesh) % 3 != 0)
                        throw new InvalidOperationException("Invalid triangle submesh: " + entry.Id);
                    triangles += mesh.GetIndexCount(submesh) / 3;
                }
                var assigned = renderers[0].sharedMaterials;
                if (triangles != entry.Triangles || assigned.Length != 2 || assigned.Distinct().Count() != 2 ||
                    assigned.Any(material => material != surface && material != wrap))
                    throw new InvalidOperationException("Triangle count or material mapping differs: " + entry.Id);
                var bounds = new Bounds();
                bool initialized = false;
                using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
                {
                    data[0].GetVertices(vertices);
                    var matrix = anchor.transform.worldToLocalMatrix * filters[0].transform.localToWorldMatrix;
                    foreach (var vertex in vertices)
                    {
                        Vector3 point = matrix.MultiplyPoint3x4(vertex);
                        if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) throw new InvalidOperationException("Nonfinite mesh vertex.");
                        if (initialized) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    }
                }
                var grips = copy.GetComponentsInChildren<Transform>(true).Where(transform => transform.name == "Grip_R").ToArray();
                if (!initialized || !Near(bounds.min, entry.Bounds.min, .001f) || !Near(bounds.max, entry.Bounds.max, .001f) || grips.Length != 1)
                    throw new InvalidOperationException("Actual vertex envelope or unique Grip_R differs: " + entry.Id + "; got=" + bounds + "; expected=" + entry.Bounds);
                Matrix4x4 actualGrip = anchor.transform.worldToLocalMatrix * grips[0].localToWorldMatrix;
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        if (!Finite(actualGrip[row, column]) || Mathf.Abs(actualGrip[row, column] - entry.Grip[row, column]) > .001f)
                            throw new InvalidOperationException("Composed socket matrix differs: " + entry.Id + "; actual=" + actualGrip + "; expected=" + entry.Grip);
            }
            finally { Object.DestroyImmediate(anchor); }
        }

        static bool CreateProfile(IReadOnlyDictionary<string, GameObject> models)
        {
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(ProfilePath);
            if (profile) { profile.Validate(); return false; }
            RequireUnused(ProfilePath);
            EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            profile = ScriptableObject.CreateInstance<WordboundVisualProfile>();
            try
            {
                profile.BaseArtwork = WordboundBaseArtwork.OriginalWreckabulary;
                profile.Overrides = Items.Select(item => new WordboundVisualOverride { VisualKey = item, Model = models[item] }).ToList();
                profile.Validate();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
                return true;
            }
            catch { if (!AssetDatabase.Contains(profile)) Object.DestroyImmediate(profile); throw; }
        }

        static JObject ReadObject(string path) => JObject.Parse(File.ReadAllText(path), JsonSettings);
        static string SelectedSourceRoot()
        {
            string selected = Environment.GetEnvironmentVariable("WORDBOUND_PACKING_CLUB_SOURCE_ROOT");
            return ValidateSourceRoot(string.IsNullOrWhiteSpace(selected) ? SourceRoot : selected);
        }
        static string ValidateSourceRoot(string value)
        {
            const string prefix = "ArtSource/Collections/PackingClub/V1/build-";
            if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal) || value.Length == prefix.Length ||
                value.Substring(prefix.Length).Any(character => !((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9') || character == '-')))
                throw new ArgumentException("Select a build-* folder under this checkout's PackingClub/V1 source folder.");
            return value;
        }
        static string VerifyFile(string sourceRoot, JToken record, string relative)
        {
            string path = ProjectPath(sourceRoot + "/" + relative);
            if (!(record is JObject) || (string)record["path"] != relative || record["bytes"]?.Type != JTokenType.Integer ||
                !File.Exists(path) || (long)record["bytes"] != new FileInfo(path).Length || (string)record["sha256"] != Hash(path))
                throw new InvalidOperationException("Source bytes/hash/path differ: " + relative);
            return path;
        }
        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string ProjectPath(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        static int Integer(JToken value) => value?.Type == JTokenType.Integer ? (int)value : throw new InvalidOperationException("Expected integer metadata.");
        static float Number(JToken value)
        {
            if (value == null || (value.Type != JTokenType.Float && value.Type != JTokenType.Integer)) throw new InvalidOperationException("Expected numeric metadata.");
            float result = (float)value;
            return Finite(result) ? result : throw new InvalidOperationException("Expected finite metadata.");
        }
        static Vector3 Vector(JToken value)
        {
            if (!(value is JArray array) || array.Count != 3) throw new InvalidOperationException("Expected three coordinates.");
            return new Vector3(Number(array[0]), Number(array[1]), Number(array[2]));
        }
        static Matrix4x4 Matrix(JToken value)
        {
            if (!(value is JArray rows) || rows.Count != 4) throw new InvalidOperationException("Expected a four-row matrix.");
            var matrix = new Matrix4x4();
            for (int row = 0; row < 4; row++)
            {
                if (!(rows[row] is JArray columns) || columns.Count != 4) throw new InvalidOperationException("Expected a four-column matrix.");
                for (int column = 0; column < 4; column++) matrix[row, column] = Number(columns[column]);
            }
            return matrix;
        }
        static bool NamesEqual(JToken value, IEnumerable<string> expected) => value is JArray names &&
            names.All(name => name.Type == JTokenType.String) && names.Select(name => (string)name).OrderBy(name => name).SequenceEqual(expected.OrderBy(name => name));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Near(Vector3 a, Vector3 b, float tolerance) => Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance && Mathf.Abs(a.z - b.z) <= tolerance;
        static void RequireEditMode() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import comparison assets in Edit Mode."); }
        static void CheckAssetType<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if ((asset && !(asset is T)) || ((File.Exists(ProjectPath(path)) || File.Exists(ProjectPath(path + ".meta"))) && !asset))
                throw new InvalidOperationException("Preserve conflicting or unimported asset: " + path);
        }
        static void RequireUnused(string path)
        {
            if (File.Exists(ProjectPath(path)) || File.Exists(ProjectPath(path + ".meta")) || AssetDatabase.LoadMainAssetAtPath(path))
                throw new InvalidOperationException("Preserve existing asset: " + path);
        }
        static void CheckCopy(string source, string target)
        {
            if (File.Exists(ProjectPath(target)))
            {
                if (Hash(ProjectPath(target)) != Hash(source)) throw new InvalidOperationException("Preserve changed imported file: " + target);
            }
            else RequireUnused(target);
        }
        static bool CopyNew(string source, string target)
        {
            CheckCopy(source, target);
            if (File.Exists(ProjectPath(target))) return false;
            File.Copy(source, ProjectPath(target), false);
            return true;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new InvalidOperationException("Could not create asset folder: " + path);
        }
        static void CheckPng(string path, bool icon)
        {
            if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new InvalidOperationException("Source image exceeds its bounded import size.");
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(image, File.ReadAllBytes(path), false) || image.width != 512 || image.height != 512)
                    throw new InvalidOperationException("Expected a decodable 512px source image: " + path);
                if (!icon) return;
                var pixels = image.GetPixels32();
                if (!pixels.Any(pixel => pixel.a == 0) || !pixels.Any(pixel => pixel.a == 255) || pixels.Count(pixel => pixel.a > 127) < pixels.Length / 100)
                    throw new InvalidOperationException("Icon lacks visible geometry or a transparent background.");
                for (int index = 0; index < 512; index++)
                    if (pixels[index].a > 12 || pixels[pixels.Length - 512 + index].a > 12 || pixels[index * 512].a > 12 || pixels[index * 512 + 511].a > 12)
                        throw new InvalidOperationException("Icon geometry touches its border.");
            }
            finally { Object.DestroyImmediate(image); }
        }
    }
}
