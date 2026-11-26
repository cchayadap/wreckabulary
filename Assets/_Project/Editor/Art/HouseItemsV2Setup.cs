using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class HouseItemsV2Setup
    {
        public const string SourceRoot = "ArtSource/Collections/HouseItemsV2";
        public const string ArtRoot = "Assets/_Project/Art/Collections/HouseItemsV2";
        public const string PrefabRoot = "Assets/_Project/Prefabs/Equipment/HouseItemsV2";
        public const string Script = "Tools/AssetPipeline/build_house_items_v2.py";
        public const int MaxTriangles = 6000;
        const float FbxUnitScale = 1f;
        const float SizeTolerance = .02f;
        const string GripName = "Grip_R";
        static readonly string[] words = { "RAKE", "TEAPOT", "LID", "TOMATO", "ROCKET", "MARBLE", "LADDER", "PADDLE", "CHILI", "DISC",
            "HELMET", "LEMON", "TORCH", "CARPET", "SLIME", "SPOON", "TENT", "PEAR", "SOCK", "HOSE",
            "RACKET", "MELON", "PIANO", "TONIC", "BRICK", "PENCIL", "RAMP", "OIL", "DOOR", "BOOT",
            "ANCHOR", "POTION", "FENCE", "CAMERA", "LATTE", "PLANE", "PAINT", "CASTLE", "MALLET", "CRAB",
            "ROBOT", "RABBIT", "TERRIER", "BLASTER", "CHOMPER", "PATROL", "CONE", "BLOCKS", "SPIKES", "TRAP",
            "SPINNER", "FLIPPER", "HOOP", "COMPASS" };

        [Serializable] sealed class Rebuilt { public string script, sha256; public long bytes; public int triangles; }
        [Serializable] sealed class Entry
        {
            public string word, main, accent; public float[] size, grip; public float targetLongest, heldScale; public int maxTriangles;
            public string[] materials; public Rebuilt rebuilt;
        }
        [Serializable] sealed class Manifest { public int schemaVersion; public string package; public Entry[] entries; }

        public static IReadOnlyList<string> Words => words;
        public static string Key(string word) => "Items/" + word;
        public static string PrefabPath(string word) => PrefabRoot + "/" + word + ".prefab";
        public static string IconPath(string word) => HouseItemsSetup.IconPath(word);
        public static string ModelPath(string word) => ArtRoot + "/Models/" + word + ".fbx";

        [MenuItem("Wreckabulary/Art/Create Missing House Items (batch 2)")]
        public static void CreateOrVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create House items outside Play Mode.");
            var manifest = ReadSource();
            foreach (var entry in manifest.entries)
            {
                if (!File.Exists(ModelPath(entry.word))) throw new IOException($"Missing model {ModelPath(entry.word)}; run {Script} --in-place in Blender first.");
                HouseItemsSetup.CheckCopy(entry.rebuilt.sha256, ModelPath(entry.word));
                CheckExisting<GameObject>(PrefabPath(entry.word));
                CheckExisting<Texture2D>(IconPath(entry.word));
            }
            var created = new List<string>();
            var prefabs = manifest.entries.Select(entry => Prefab(entry, created)).ToArray();
            var library = Register(prefabs);
            for (int i = 0; i < prefabs.Length; i++) HouseItemsSetup.Icon(words[i], prefabs[i], created);
            Debug.Log($"HOUSE_ITEMS_V2_READY: {string.Join(", ", words)}; render-only prefabs in {PrefabRoot}; model library {library.Entries.Count} entries; " +
                      $"icons in {HouseItemsSetup.IconRoot}. Created {created.Count} prefabs/icons; existing assets preserved. Stats live in items_house.json.");
        }

        [MenuItem("Wreckabulary/Art/Re-render House Item Icons (batch 2)")]
        public static void RenderIcons()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Render House item icons outside Play Mode.");
            var written = new List<string>();
            foreach (string word in words)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(word));
                if (!prefab) throw new FileNotFoundException(PrefabPath(word) + " is missing; run Create Missing House Items (batch 2) first.");
                written.Add(HouseItemsSetup.Rerender(word, prefab, null));
            }
            Debug.Log("[Wreckabulary] HOUSE_ITEM_V2_ICONS: " + string.Join(", ", written));
        }

        public static IReadOnlyList<ModelLibrary.Entry> LibraryEntries()
        {
            var prefabs = words.Select(word => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(word))).ToArray();
            if (prefabs.All(prefab => !prefab)) return Array.Empty<ModelLibrary.Entry>();
            if (prefabs.Any(prefab => !prefab)) throw new InvalidOperationException("Batch 2 House item prefabs are incomplete; run Wreckabulary > Art > Create Missing House Items (batch 2).");
            return prefabs.Select((prefab, i) => new ModelLibrary.Entry { key = Key(words[i]), model = prefab, clips = Array.Empty<AnimationClip>() }).ToArray();
        }

        static Manifest ReadSource()
        {
            string path = SourceRoot + "/manifest.json";
            if (!File.Exists(path) || new FileInfo(path).Length > 256 * 1024) throw new InvalidOperationException("Missing or oversized House items manifest: " + path);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.package != "HouseItemsV2" || manifest.entries == null || manifest.entries.Length != words.Length)
                throw new InvalidOperationException($"Expected the {words.Length}-model HouseItemsV2 manifest.");
            for (int i = 0; i < words.Length; i++)
            {
                var entry = manifest.entries[i];
                if (entry == null || entry.word != words[i]) throw new InvalidOperationException("Unexpected House item identity or order at entry " + i);
                if (entry.rebuilt == null || entry.rebuilt.script != Script || !IsSha(entry.rebuilt.sha256) || entry.rebuilt.bytes <= 0 ||
                    entry.rebuilt.triangles <= 0 || entry.rebuilt.triangles > Math.Min(entry.maxTriangles, MaxTriangles))
                    throw new InvalidOperationException($"{entry.word} has no verified build record; run {Script} --in-place in Blender.");
                if (!Positive(entry.size) || entry.grip?.Length != 3 || !entry.grip.All(float.IsFinite) || !(entry.heldScale > 0f && entry.heldScale <= 1f) ||
                    Mathf.Abs(entry.targetLongest - entry.size.Max()) > .0001f || entry.materials == null || entry.materials.Length == 0 ||
                    entry.materials.Any(m => !m.EndsWith("_Classic", StringComparison.Ordinal)) || !entry.materials.Contains(entry.main) || !entry.materials.Contains(entry.accent))
                    throw new InvalidOperationException("Inconsistent House item size, grip, held scale or materials in the manifest: " + entry.word);
            }
            return manifest;
        }

        static GameObject Prefab(Entry entry, List<string> created)
        {
            string modelPath = ModelPath(entry.word), path = PrefabPath(entry.word);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Expected an FBX importer: " + modelPath);
            if (!Configured(importer))
            {
                if (prefab) throw new InvalidOperationException("Preserve and inspect changed rigid-model import settings: " + modelPath);
                Configure(importer);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!model || model.GetComponentsInChildren<MeshFilter>(true).Length == 0)
                throw new InvalidOperationException("Preserve and inspect incomplete model import: " + modelPath);
            RequireLibraryMaterials(model, entry);
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

        static Vector3 Grip(Entry entry) => new Vector3(entry.grip[0], entry.grip[1], entry.grip[2]);

        static bool Configured(ModelImporter importer) =>
            !importer.importAnimation && importer.animationType == ModelImporterAnimationType.None && !importer.importCameras && !importer.importLights &&
            !importer.importBlendShapes && !importer.addCollider && !importer.importVisibility && !importer.isReadable && importer.globalScale == FbxUnitScale &&
            importer.meshCompression == ModelImporterMeshCompression.Off && importer.importNormals == ModelImporterNormals.Import &&
            importer.importTangents == ModelImporterTangents.None && !importer.optimizeGameObjects && importer.useFileScale && importer.bakeAxisConversion &&
            importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription && importer.materialLocation == ModelImporterMaterialLocation.InPrefab;

        static void Configure(ModelImporter importer)
        {
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

        static void RequireLibraryMaterials(GameObject model, Entry entry)
        {
            var library = MaterialLibrary.Load();
            if (!library) throw new InvalidOperationException("Run Wreckabulary > Art > Finishes > Build materials before adding House items.");
            var names = model.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m ? m.name : null).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(entry.materials.OrderBy(n => n, StringComparer.Ordinal)))
                throw new InvalidOperationException($"{entry.word}: imported materials {string.Join(", ", names)} differ from the manifest {string.Join(", ", entry.materials)}.");
            foreach (string name in names)
                if (!library.Find(name)) throw new InvalidOperationException($"{entry.word}: {name} is not in the material library; run Wreckabulary > Art > Finishes > Build materials.");
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
                RequireRenderOnly(root, entry.word);
                var raw = ModelVisual.BoundsIn(root.transform, instance).size;
                if (!Positive(new[] { raw.x, raw.y, raw.z })) throw new InvalidOperationException("House item has invalid imported bounds: " + entry.word);
                instance.transform.localScale *= entry.targetLongest / Mathf.Max(raw.x, raw.y, raw.z);
                var bounds = ModelVisual.BoundsIn(root.transform, instance);
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
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

        static ModelLibrary Register(GameObject[] prefabs)
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            if (!library) throw new InvalidOperationException("Run Wreckabulary > Art > Set Up Imported Art before adding House items.");
            if (!Registered(library, prefabs)) ModelLibraryBuilder.Refresh();
            library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            if (!library || !Registered(library, prefabs)) throw new InvalidOperationException("The model library rebuild did not register every batch 2 House item.");
            return library;
        }

        static bool Registered(ModelLibrary library, GameObject[] prefabs) =>
            Enumerable.Range(0, words.Length).All(i => library.Find(Key(words[i])) == prefabs[i]);

        static void CheckExisting<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is T) return;
            if (asset || File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")) throw new IOException("Preserve conflicting destination: " + path);
        }

        static bool IsSha(string value) => value?.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        static bool Positive(float[] values) => values?.Length == 3 && values.All(value => float.IsFinite(value) && value > 0f);
    }
}
