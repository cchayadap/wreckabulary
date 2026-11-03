using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class HouseItemsSetup
    {
        public const string SourceRoot = "ArtSource/Collections/HouseItemsV1";
        public const string ArtRoot = "Assets/_Project/Art/Collections/HouseItemsV1";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Equipment/HouseItemsV1";
        public const string IconRoot = "Assets/_Project/Resources/UI/Items";
        public const int IconSize = 192;
        const float FbxUnitScale = .1f;
        const float SizeTolerance = .02f;
        static readonly string[] words = { "PILLOW", "RACK", "MIRROR", "SPEAKER", "BENCH", "TOASTER", "BEAR" };
        static readonly string[] KenneyNames = { "pillow", "coatRackStanding", "bathroomMirror", "speaker", "benchCushionLow", "toaster", "bear" };
        static readonly Vector3 IconView = new Vector3(-2.3f, 2.3f, 3.6f), KeyLight = new Vector3(3f, 4f, 4f), FillLight = new Vector3(-3f, 3f, 1f);

        [Serializable] sealed class FileRecord { public string packagePath, sha256; public long bytes; }
        [Serializable] sealed class License { public string name; public FileRecord evidence; }
        [Serializable] sealed class PackBounds { public float[] size; }
        [Serializable] sealed class Entry
        {
            public string word, kenneyName, packagePath, sha256; public long bytes; public PackBounds packBounds;
            public float targetLongest, heldLongest, heldScale, yaw; public float[] size, grip;
        }
        [Serializable] sealed class Manifest { public int schemaVersion; public string package; public License license; public Entry[] entries; }

        public static IReadOnlyList<string> Words => words;
        public static string Key(string word) => "Items/" + word;
        public static string PrefabPath(string word) => PrefabRoot + "/" + word + ".prefab";
        public static string IconPath(string word) => IconRoot + "/" + word + ".png";
        static string ModelPath(string word) => ArtRoot + "/Models/" + word + ".fbx";

        [MenuItem("Wreckabulary/Art/Create Missing House Items")]
        public static void CreateOrVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create House items outside Play Mode.");
            var manifest = ReadSource();
            CheckCopy(manifest.license.evidence.sha256, ArtRoot + "/License.txt");
            foreach (var entry in manifest.entries)
            {
                CheckCopy(entry.sha256, ModelPath(entry.word));
                CheckExisting<GameObject>(PrefabPath(entry.word));
                CheckExisting<Texture2D>(IconPath(entry.word));
            }
            WorkspaceAssetCopies.EnsureFolder(ArtRoot + "/Models");
            CopyMissing(manifest.license.evidence.packagePath, manifest.license.evidence.sha256, ArtRoot + "/License.txt");
            var created = new List<string>();
            var prefabs = manifest.entries.Select(entry => Prefab(entry, created)).ToArray();
            var library = Register(prefabs);
            for (int i = 0; i < prefabs.Length; i++) Icon(words[i], prefabs[i], created);
            Debug.Log($"HOUSE_ITEMS_READY: {string.Join(", ", words)}; render-only prefabs in {PrefabRoot}; model library {library.Entries.Count} entries " +
                      $"incl. Items/<WORD>; icons in {IconRoot}. Created {created.Count} prefabs/icons; existing assets preserved. Stats live in items_house.json.");
        }

        public static IReadOnlyList<ModelLibrary.Entry> LibraryEntries()
        {
            var prefabs = words.Select(word => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(word))).ToArray();
            if (prefabs.All(prefab => !prefab)) return Array.Empty<ModelLibrary.Entry>();
            if (prefabs.Any(prefab => !prefab)) throw new InvalidOperationException("House item prefabs are incomplete; run Wreckabulary > Art > Create Missing House Items.");
            return prefabs.Select((prefab, i) => new ModelLibrary.Entry { key = Key(words[i]), model = prefab, clips = Array.Empty<AnimationClip>() }).ToArray();
        }

        public static bool HasIconSettings(TextureImporter importer) => importer && importer.textureType == TextureImporterType.Default &&
            importer.textureShape == TextureImporterShape.Texture2D && importer.sRGBTexture && importer.alphaSource == TextureImporterAlphaSource.FromInput &&
            importer.alphaIsTransparency && !importer.mipmapEnabled && importer.isReadable && importer.npotScale == TextureImporterNPOTScale.None &&
            importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Bilinear && importer.maxTextureSize == 256 &&
            importer.textureCompression == TextureImporterCompression.Uncompressed;

        static Manifest ReadSource()
        {
            string path = SourceRoot + "/manifest.json";
            if (!File.Exists(path) || new FileInfo(path).Length > 256 * 1024) throw new InvalidOperationException("Missing or oversized House items manifest: " + path);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.package != "HouseItemsV1" || manifest.license?.name != "CC0" ||
                manifest.license.evidence == null || manifest.entries == null || manifest.entries.Length != words.Length)
                throw new InvalidOperationException("Expected the verified seven-model HouseItemsV1 manifest.");
            var license = manifest.license.evidence;
            VerifyFile(license.packagePath, license.sha256, license.bytes, "Source/Kenney/License.txt");
            for (int i = 0; i < words.Length; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null || entry.word != words[i] || entry.kenneyName != KenneyNames[i])
                    throw new InvalidOperationException("Unexpected House item identity or order at entry " + i);
                VerifyFile(entry.packagePath, entry.sha256, entry.bytes, "Source/Kenney/Models/" + KenneyNames[i] + ".fbx");
                if (!Positive(entry.packBounds?.size) || !Positive(entry.size) || entry.grip?.Length != 3 || !entry.grip.All(float.IsFinite) ||
                    !(entry.targetLongest > 0f) || !(entry.heldScale > 0f && entry.heldScale <= 1f) || entry.yaw is not (0f or 90f or 180f or 270f) ||
                    Mathf.Abs(entry.heldScale - entry.heldLongest / entry.targetLongest) > .0001f ||
                    Enumerable.Range(0, 3).Any(axis => Mathf.Abs(entry.size[axis] - Turned(entry)[axis] * entry.targetLongest / entry.packBounds.size.Max()) > .0001f))
                    throw new InvalidOperationException("Inconsistent House item size, grip or held scale in the manifest: " + entry.word);
            }
            return manifest;
        }

        static float[] Turned(Entry entry) => entry.yaw % 180f == 0f ? entry.packBounds.size
            : new[] { entry.packBounds.size[2], entry.packBounds.size[1], entry.packBounds.size[0] };

        static void VerifyFile(string packagePath, string sha256, long bytes, string expected)
        {
            if (packagePath != expected || bytes <= 0 || bytes > 4 * 1024 * 1024 || sha256 == null || sha256.Length != 64 ||
                sha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
                throw new InvalidOperationException("Unexpected House item source path, size or hash: " + expected);
            string path = SourceRoot + "/" + expected;
            if (!File.Exists(path) || new FileInfo(path).Length != bytes || Hash(path) != sha256)
                throw new InvalidOperationException("Source changed since verification: " + path);
        }

        static GameObject Prefab(Entry entry, List<string> created)
        {
            string modelPath = ModelPath(entry.word), path = PrefabPath(entry.word);
            if (CopyMissing(entry.packagePath, entry.sha256, modelPath)) ConfigureImporter(modelPath);
            ValidateImporter(modelPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!model || model.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                throw new InvalidOperationException("Preserve and inspect incomplete model import: " + modelPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                prefab = CreatePrefab(entry, model, path);
                created.Add(path);
            }
            if (prefab.name != entry.word) throw new InvalidOperationException("Preserve and inspect renamed House item prefab: " + path);
            RequireRenderOnly(prefab, entry.word);
            CheckSize(entry, prefab, "saved prefab " + path);
            var grip = ModelVisual.FindNamed(prefab, GripName);
            if (!grip || grip.parent != prefab.transform || Vector3.Distance(grip.localPosition, Grip(entry)) > .001f)
                throw new InvalidOperationException("Preserve and inspect House item grip socket: " + path);
            return prefab;
        }

        const string GripName = "Grip_R";
        static Vector3 Grip(Entry entry) => new Vector3(entry.grip[0], entry.grip[1], entry.grip[2]);

        static void ConfigureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Expected an FBX importer: " + path);
            importer.globalScale = FbxUnitScale;
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
            importer.SaveAndReimport();
        }

        static void ValidateImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None ||
                importer.importCameras || importer.importLights || importer.importBlendShapes || importer.addCollider ||
                importer.importVisibility || importer.isReadable || importer.globalScale != FbxUnitScale ||
                importer.meshCompression != ModelImporterMeshCompression.Off || importer.importNormals != ModelImporterNormals.Import ||
                importer.importTangents != ModelImporterTangents.None || importer.optimizeGameObjects || !importer.useFileScale || !importer.bakeAxisConversion ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription ||
                importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                throw new InvalidOperationException("Preserve and inspect changed or incomplete rigid-model import settings: " + path);
        }

        static GameObject CreatePrefab(Entry entry, GameObject model, string path)
        {
            WorkspaceAssetCopies.EnsureFolder(PrefabRoot);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(entry.word);
                SceneManager.MoveGameObjectToScene(root, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                if (!instance) throw new InvalidOperationException("Could not instantiate House item model: " + entry.word);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localRotation = Quaternion.Euler(0f, entry.yaw, 0f) * instance.transform.localRotation;
                RequireRenderOnly(root, entry.word);
                var raw = ModelVisual.BoundsIn(root.transform, instance).size;
                if (!Positive(new[] { raw.x, raw.y, raw.z })) throw new InvalidOperationException("House item has invalid imported bounds: " + entry.word);
                instance.transform.localScale *= entry.targetLongest / Mathf.Max(raw.x, raw.y, raw.z);
                var bounds = ModelVisual.BoundsIn(root.transform, instance);
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material => LitCopy(entry.word, material)).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                CheckSize(entry, root, $"imported size {raw:F4}");
                new GameObject(GripName).transform.SetParent(root.transform, false);
                root.transform.Find(GripName).localPosition = Grip(entry);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!saved) throw new IOException("Could not save House item prefab: " + path);
                return saved;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void RequireRenderOnly(GameObject root, string word)
        {
            if (!root.GetComponentInChildren<MeshRenderer>(true) ||
                root.GetComponentsInChildren<Component>(true).Any(component => !component || component is not (Transform or MeshFilter or MeshRenderer)))
                throw new InvalidOperationException("Expected a rigid render-only House item: " + word);
        }

        static void CheckSize(Entry entry, GameObject root, string detail)
        {
            var bounds = ModelVisual.BoundsIn(root.transform, root);
            var expected = new Vector3(entry.size[0], entry.size[1], entry.size[2]);
            bool bottomCentre = Mathf.Abs(bounds.center.x) < .001f && Mathf.Abs(bounds.min.y) < .001f && Mathf.Abs(bounds.center.z) < .001f;
            if (!bottomCentre || Enumerable.Range(0, 3).Any(axis => Mathf.Abs(bounds.size[axis] - expected[axis]) > expected[axis] * SizeTolerance))
                throw new InvalidOperationException($"{entry.word}: normalised size {bounds.size:F4} (centre {bounds.center:F4}, bottom {bounds.min.y:F4}) " +
                    $"differs from the manifest {expected:F4} by more than 2% or is not bottom-centred; {detail}. Inspect axes and units.");
        }

        static Material LitCopy(string word, Material imported)
        {
            if (!imported || !SafeName(imported.name)) throw new InvalidOperationException("Invalid imported material name: " + word);
            string path = ArtRoot + "/Materials/" + word + "/" + imported.name + ".mat";
            CheckExisting<Material>(path);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            Color color = imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : imported.color;
            float metallic = imported.HasProperty("_Metallic") ? imported.GetFloat("_Metallic") : 0f;
            float smoothness = imported.HasProperty("_Smoothness") ? imported.GetFloat("_Smoothness") : imported.HasProperty("_Glossiness") ? imported.GetFloat("_Glossiness") : .2f;
            if (!float.IsFinite(color.r) || !float.IsFinite(color.g) || !float.IsFinite(color.b) || !float.IsFinite(color.a) ||
                !float.IsFinite(metallic) || !float.IsFinite(smoothness)) throw new InvalidOperationException("Imported material values must be finite: " + path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("House item materials require URP Lit.");
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var material = new Material(shader) { name = imported.name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (color.a < .999f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static ModelLibrary Register(GameObject[] prefabs)
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            if (!library) throw new InvalidOperationException("Run Wreckabulary > Art > Set Up Imported Art before adding House items.");
            if (!Registered(library, prefabs)) ModelLibraryBuilder.Refresh();
            library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            if (!library || !Registered(library, prefabs)) throw new InvalidOperationException("The model library rebuild did not register every House item.");
            return library;
        }

        static bool Registered(ModelLibrary library, GameObject[] prefabs) =>
            Enumerable.Range(0, words.Length).All(i => library.Find(Key(words[i])) == prefabs[i]);

        static void Icon(string word, GameObject prefab, List<string> created)
        {
            string path = IconPath(word);
            if (!File.Exists(path))
            {
                WriteNew(path, RenderIcon(prefab));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                ConfigureIcon(path);
                created.Add(path);
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || texture.width != IconSize || texture.height != IconSize || !HasIconSettings(AssetImporter.GetAtPath(path) as TextureImporter))
                throw new InvalidOperationException("Preserve and inspect changed House item icon: " + path);
        }

        static void ConfigureIcon(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static byte[] RenderIcon(GameObject prefab)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline || QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Render House item icons in the Editor with a graphics device, URP and linear colour.");
            PreviewRenderUtility preview = null; GameObject clone = null; Texture2D image = null; bool begun = false;
            var active = RenderTexture.active; bool asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                preview = new PreviewRenderUtility();
                clone = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview.camera.gameObject.scene);
                var bounds = ModelVisual.BoundsIn(clone.transform, clone);
                var camera = preview.camera;
                float distance = bounds.size.magnitude * 2f + 1f;
                camera.orthographic = true; camera.aspect = 1f; camera.allowHDR = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.nearClipPlane = .01f; camera.farClipPlane = distance * 2f;
                camera.transform.position = bounds.center + IconView.normalized * distance;
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize = 1.08f * Enumerable.Range(0, 8)
                    .Select(i => camera.transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))))
                    .Max(point => Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)));
                preview.ambientColor = new Color(.45f, .48f, .51f);
                preview.lights[0].color = new Color(1f, .86f, .72f); preview.lights[0].intensity = 1.1f;
                preview.lights[0].transform.rotation = Quaternion.LookRotation(-KeyLight);
                preview.lights[1].color = new Color(.72f, .84f, 1f); preview.lights[1].intensity = .6f;
                preview.lights[1].transform.rotation = Quaternion.LookRotation(-FillLight);
                preview.BeginStaticPreview(new Rect(0, 0, IconSize, IconSize)); begun = true;
                preview.Render(true); preview.Render(true);
                image = ReadRgba(camera.targetTexture);
                begun = false; Object.DestroyImmediate(preview.EndStaticPreview());
                return Encode(image);
            }
            finally
            {
                if (begun) Object.DestroyImmediate(preview.EndStaticPreview());
                if (image) Object.DestroyImmediate(image);
                if (clone) Object.DestroyImmediate(clone);
                preview?.Cleanup();
                RenderTexture.active = active; EditorSettings.asyncShaderCompilation = asyncShaders;
                if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
            }
        }

        static Texture2D ReadRgba(RenderTexture source)
        {
            if (!source || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(source.graphicsFormat))
                throw new InvalidOperationException("The icon preview target has no alpha channel.");
            var active = RenderTexture.active; bool srgbWrite = GL.sRGBWrite;
            var target = new RenderTexture(IconSize, IconSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                GL.sRGBWrite = true;
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var image = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0, false);
                image.Apply(false);
                return image;
            }
            finally
            {
                RenderTexture.active = active; GL.sRGBWrite = srgbWrite;
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        static byte[] Encode(Texture2D image)
        {
            var pixels = image.GetPixels32();
            int opaque = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                int x = i % IconSize, y = i / IconSize;
                if (pixel.a != 0 && (x == 0 || y == 0 || x == IconSize - 1 || y == IconSize - 1))
                    throw new InvalidOperationException("House item icon has opaque border pixels (framing or missing alpha); inspect the preview render.");
                if (pixel.a == 255) opaque++;
                else pixels[i] = pixel.a == 0 ? default : new Color32(Straight(pixel.r, pixel.a), Straight(pixel.g, pixel.a), Straight(pixel.b, pixel.a), pixel.a);
            }
            if (opaque < pixels.Length / 100) throw new InvalidOperationException("House item icon rendered almost empty; inspect the preview render.");
            image.SetPixels32(pixels);
            image.Apply(false);
            return image.EncodeToPNG();
        }

        static byte Straight(byte value, byte alpha) =>
            (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(value / 255f) * 255f / alpha)) * 255f);

        static bool CopyMissing(string packagePath, string sha256, string target)
        {
            CheckCopy(sha256, target);
            if (File.Exists(target)) return false;
            File.Copy(SourceRoot + "/" + packagePath, target, false);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        static void CheckCopy(string sha256, string target)
        {
            if (File.Exists(target))
            {
                if (Hash(target) != sha256) throw new IOException("Preserve changed imported source: " + target);
                if (!AssetDatabase.LoadMainAssetAtPath(target)) throw new IOException("Preserve unimported source and inspect: " + target);
            }
            else if (File.Exists(target + ".meta") || Directory.Exists(target) || AssetDatabase.LoadMainAssetAtPath(target))
                throw new IOException("Preserve conflicting destination: " + target);
        }

        static void CheckExisting<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is T) return;
            if (asset || File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")) throw new IOException("Preserve conflicting destination: " + path);
        }

        static void WriteNew(string path, byte[] bytes)
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(bytes, 0, bytes.Length);
        }

        static bool Positive(float[] values) => values?.Length == 3 && values.All(value => float.IsFinite(value) && value > 0f);
        static bool SafeName(string value) => !string.IsNullOrEmpty(value) && value != "." && value != ".." && value.TrimEnd(' ', '.') == value && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
