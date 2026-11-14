using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Audio;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class BeltRangeSetup
    {
        public const string ModelSource = "ArtSource/Collections/OriginalStyleBELTV1/build-05";
        public const string AudioSource = "ArtSource/Audio/ImpactV1/synth-02";
        public const string ArtRoot = "Assets/_Project/Art/Collections/BeltV1";
        public const string VisualPath = "Assets/_Project/Prefabs/Equipment/Prototypes/BELT.prefab";
        public const string ProfilePath = "Assets/_Project/Data/Equipment/BELT/BeltPrototype.asset";
        public const string AudioRoot = "Assets/_Project/Audio/Impact";
        public const string LibraryPath = "Assets/_Project/Resources/" + ImpactAudioLibrary.ResourcePath + ".asset";
        public const string ScenePath = "Assets/_Project/Scenes/Testing/BeltRange.unity";
        const string TemplateScene = "Assets/_Project/Scenes/Tutorial.unity";
        const string AudioManifestSha = "7c11c3946e11394f4985d92e9e279c67c12136f666a15feb2264165e6dbe31c7";
        static readonly string[] Skins = { "Classic", "Candy", "Arcade" };
        static readonly float[] LeatherRoughness = { 0.58f, 0.40f, 0.45f };
        static readonly string[] Bones = { "Belt_Root", "Belt_01", "Belt_02", "Belt_03", "Belt_04", "Belt_05", "Belt_06", "Belt_Tip" };
        static readonly string[] Weapons = { "BELT", "BAT", "HAMMER", "BROOM", "BLADE", ImpactCues.DefaultWeapon };

        static readonly (string source, string target, string sha)[] ModelFiles =
        {
            ("Exports/BELT.fbx", "Models/BELT.fbx", "2f0e07ed757227a3d796c4daca4529993936e7d6e92b92789781e2698602af73"),
            ("Textures/leather_Classic_BaseColor.png", "Textures/leather_Classic_BaseColor.png", "0f68fccb82b157b128c94e90cb64a38581f4a608bb7d78daf16309e89b668722"),
            ("Textures/leather_Candy_BaseColor.png", "Textures/leather_Candy_BaseColor.png", "28507eb40b5b351b7ff25a22b88a3ef95ae90ad998097b5dbb0d0178b0ee7080"),
            ("Textures/leather_Arcade_BaseColor.png", "Textures/leather_Arcade_BaseColor.png", "aa9d41005d2b497ecac213622c5e2d99d691879ab864440cd460b745aeb00ceb"),
            ("Textures/leather_NormalGL.png", "Textures/leather_NormalGL.png", "a6009f8765d97d236bddc9d0508675c6455a5ff08a72c1517c3a476164373bbd"),
            ("Icons/BELT-512.png", "Icons/BELT-512.png", "323b4466ac67f878fd31967499de554edcba50010afb97dbcc25f2c214cee940"),
        };

        static readonly (string word, SurfaceMaterial material, float health, Color color, float x)[] Targets =
        {
            ("GLASS", SurfaceMaterial.Glass, 18f, new Color(0.70f, 0.86f, 0.92f), -6f),
            ("VASE", SurfaceMaterial.Ceramic, 24f, new Color(0.86f, 0.80f, 0.72f), -3.6f),
            ("CRATE", SurfaceMaterial.Wood, 40f, new Color(0.71f, 0.51f, 0.35f), -1.2f),
            ("PAN", SurfaceMaterial.Metal, 60f, new Color(0.55f, 0.57f, 0.60f), 1.2f),
            ("ROCK", SurfaceMaterial.Stone, 72f, new Color(0.55f, 0.55f, 0.56f), 3.6f),
            ("SOFA", SurfaceMaterial.Soft, 48f, new Color(0.62f, 0.42f, 0.48f), 6f),
        };

        [Serializable] sealed class SoundFile { public string path, kind, weapon, part, surface, sha256; }
        [Serializable] sealed class SoundManifest { public int schema; public string run, licence; public SoundFile[] files; }

        [MenuItem("Wreckabulary/Equipment/Create Missing BELT Range")]
        public static void CreateOrVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the BELT range outside Play Mode.");
            foreach (var file in ModelFiles) VerifySource(ModelSource + "/" + file.source, file.sha);
            var sounds = ReadSounds();
            var materials = ImportModel();
            var visual = CreateVisual(materials);
            var profile = CreateProfile(visual, materials);
            var library = ImportSounds(sounds);
            CreateScene(profile);
            AddToBuild();
            var problems = profile.Validate().Concat(library.Validate()).ToList();
            if (problems.Count > 0) throw new InvalidOperationException("BELT range needs attention:\n" + string.Join("\n", problems));
            Debug.Log($"BELT_RANGE_READY: prototype {profile.Definition().Identity()}, {library.Keys.Count()} impact sounds, scene {ScenePath}. " +
                      "Existing assets preserved. BELT stays out of items.json, recipes, Shop and online modes.");
        }

        [MenuItem("Wreckabulary/Equipment/Open BELT Range")]
        public static void Open()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) CreateOrVerify();
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static Material[] ImportModel()
        {
            foreach (var file in ModelFiles)
            {
                string target = ArtRoot + "/" + file.target;
                if (CopyMissing(ModelSource + "/" + file.source, target, file.sha)) Configure(target);
                Validate(target);
            }
            CopyMissing(ModelSource + "/build-receipt.json", ArtRoot + "/build-receipt.json", null);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/leather_NormalGL.png");
            var result = new Material[Skins.Length + 1];
            for (int i = 0; i < Skins.Length; i++)
            {
                var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/Textures/leather_{Skins[i]}_BaseColor.png");
                result[i] = MaterialIfMissing($"{ArtRoot}/Materials/leather_{Skins[i]}.mat", Color.white, 0f, 1f - LeatherRoughness[i], baseMap, normal);
            }
            ColorUtility.TryParseHtmlString("#F5C873", out var gold);
            result[Skins.Length] = MaterialIfMissing(ArtRoot + "/Materials/gold_Classic.mat", gold, 0.68f, 0.68f, null, null);
            return result;
        }

        static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer is ModelImporter model)
            {
                model.globalScale = 1f;
                model.useFileScale = true;
                model.bakeAxisConversion = false;
                model.importCameras = model.importLights = model.importVisibility = model.importAnimation = model.importBlendShapes = false;
                model.animationType = ModelImporterAnimationType.Generic;
                model.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                model.optimizeGameObjects = false;
                model.meshCompression = ModelImporterMeshCompression.Off;
                model.importNormals = ModelImporterNormals.Import;
                model.importTangents = ModelImporterTangents.CalculateMikk;
                model.addCollider = model.isReadable = false;
                model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                model.materialLocation = ModelImporterMaterialLocation.InPrefab;
                model.SaveAndReimport();
            }
            else if (importer is TextureImporter texture)
            {
                bool icon = path.Contains("/Icons/");
                bool normal = path.EndsWith("_NormalGL.png", StringComparison.Ordinal);
                texture.textureType = icon ? TextureImporterType.Sprite : normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                texture.sRGBTexture = !normal;
                texture.mipmapEnabled = !icon;
                texture.alphaIsTransparency = icon;
                texture.wrapMode = icon ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                texture.maxTextureSize = 512;
                texture.textureCompression = TextureImporterCompression.CompressedHQ;
                texture.SaveAndReimport();
            }
        }

        static void Validate(string path)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer is ModelImporter model)
            {
                if (model.animationType != ModelImporterAnimationType.Generic || model.optimizeGameObjects || model.importAnimation ||
                    model.addCollider || model.importTangents == ModelImporterTangents.None)
                    throw new InvalidOperationException("Preserve and inspect changed BELT model import settings: " + path);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var skin = asset ? asset.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                if (!skin || !skin.sharedMesh || skin.sharedMesh.subMeshCount != 2)
                    throw new InvalidOperationException("BELT must import as one skinned mesh with leather and gold parts: " + path);
                var names = asset.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
                foreach (string bone in Bones) if (!names.Contains(bone)) throw new InvalidOperationException("BELT bone missing after import: " + bone);
                var size = SizeIn(asset.transform, skin);
                var expected = new Vector3(0.112f, 1.2f, 0.072f);
                for (int axis = 0; axis < 3; axis++)
                    if (Mathf.Abs(size[axis] - expected[axis]) > 0.003f)
                        throw new InvalidOperationException($"Imported BELT size {size:F4} differs from the model contract {expected:F4}; inspect axes and units.");
            }
            else if (importer is TextureImporter texture && texture.maxTextureSize > 512)
                throw new InvalidOperationException("Preserve and inspect changed BELT texture settings: " + path);
        }

        static Vector3 SizeIn(Transform root, SkinnedMeshRenderer skin)
        {
            var b = skin.sharedMesh.bounds;
            var toRoot = root.worldToLocalMatrix * skin.transform.localToWorldMatrix;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = toRoot.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return max - min;
        }

        static GameObject CreateVisual(Material[] materials)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (existing) return existing;
            CheckFree(VisualPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtRoot + "/Models/BELT.fbx");
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(VisualPath).Replace('\\', '/'));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.name = "BELT";
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                    if (component is Collider || component is MonoBehaviour) throw new InvalidOperationException("BELT visual must stay render-only.");
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                skin.sharedMaterials = skin.sharedMaterials.Select(m =>
                    m && m.name.StartsWith("gold", StringComparison.Ordinal) ? materials[Skins.Length] : materials[0]).ToArray();
                skin.updateWhenOffscreen = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, VisualPath);
                if (!saved) throw new IOException("Could not save the BELT visual prefab.");
                return saved;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static FlexibleWeaponProfile CreateProfile(GameObject visual, Material[] materials)
        {
            var profile = AssetDatabase.LoadAssetAtPath<FlexibleWeaponProfile>(ProfilePath);
            if (profile) return profile;
            CheckFree(ProfilePath);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            profile = ScriptableObject.CreateInstance<FlexibleWeaponProfile>();
            profile.visual = visual;
            profile.leather = materials.Take(Skins.Length).ToArray();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static SoundManifest ReadSounds()
        {
            string path = AudioSource + "/manifest.json";
            VerifySource(path, AudioManifestSha);
            var manifest = JsonUtility.FromJson<SoundManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.schema != 1 || manifest.files == null || manifest.files.Length == 0)
                throw new InvalidOperationException("Expected the verified impact sound manifest.");
            foreach (var file in manifest.files)
            {
                if (file.path == null || file.path.Contains("..") || !file.path.EndsWith(".wav", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected impact sound path: " + file.path);
                VerifySource(AudioSource + "/" + file.path, file.sha256);
            }
            return manifest;
        }

        static ImpactAudioLibrary ImportSounds(SoundManifest manifest)
        {
            var clips = new Dictionary<string, List<AudioClip>>(StringComparer.Ordinal);
            foreach (var file in manifest.files)
            {
                string target = AudioRoot + "/Clips/" + file.path;
                if (CopyMissing(AudioSource + "/" + file.path, target, file.sha256)) ConfigureClip(target);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
                if (!clip) throw new InvalidOperationException("Impact sound did not import: " + target);
                string key = file.kind switch
                {
                    "contact" => ContactKey(file.weapon, file.part, file.surface),
                    "swing" => ImpactCues.SwingKey(file.weapon),
                    "damage" => ImpactCues.DamageKey(Parse(file.surface)),
                    _ => ImpactCues.BreakKey(Parse(file.surface)),
                };
                if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
            CopyMissing(AudioSource + "/manifest.json", AudioRoot + "/manifest.json", AudioManifestSha);

            var library = AssetDatabase.LoadAssetAtPath<ImpactAudioLibrary>(LibraryPath);
            if (library) return library;
            CheckFree(LibraryPath);
            library = ScriptableObject.CreateInstance<ImpactAudioLibrary>();
            foreach (string weapon in Weapons) library.weapons.Add(WeaponSounds(weapon, clips));
            foreach (SurfaceMaterial surface in Enum.GetValues(typeof(SurfaceMaterial)))
                if (clips.ContainsKey(ImpactCues.BreakKey(surface))) library.surfaces.Add(SurfaceSounds(surface, clips));
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(library, LibraryPath);
            AssetDatabase.SaveAssetIfDirty(library);
            return library;
        }

        static string ContactKey(string weapon, string part, string surface)
        {
            string slot = weapon == "BELT" ? part : ContactPart.Any.ToString();
            return ImpactCues.ContactKey(weapon, slot, Parse(surface));
        }

        static SurfaceMaterial Parse(string surface) => (SurfaceMaterial)Enum.Parse(typeof(SurfaceMaterial), surface);

        static void ConfigureClip(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        static WeaponImpactSounds WeaponSounds(string weapon, Dictionary<string, List<AudioClip>> clips)
        {
            string path = $"{AudioRoot}/Profiles/Weapons/{weapon}.asset";
            var set = AssetDatabase.LoadAssetAtPath<WeaponImpactSounds>(path);
            if (set) return set;
            CheckFree(path);
            set = ScriptableObject.CreateInstance<WeaponImpactSounds>();
            set.weapon = weapon;
            set.swing = Sound(clips, ImpactCues.SwingKey(weapon), 0.7f, 0.94f, 1.06f, 0.1f);
            var parts = weapon == "BELT" ? new[] { ContactPart.Strap, ContactPart.Buckle } : new[] { ContactPart.Any };
            foreach (var part in parts)
                foreach (SurfaceMaterial surface in Enum.GetValues(typeof(SurfaceMaterial)))
                {
                    string key = ImpactCues.ContactKey(weapon, part.ToString(), surface);
                    if (clips.ContainsKey(key))
                        set.contacts.Add(new ContactSound { part = part, surface = surface, sound = Sound(clips, key, 1f, 0.95f, 1.05f, 0.06f) });
                }
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssetIfDirty(set);
            return set;
        }

        static SurfaceDamageSounds SurfaceSounds(SurfaceMaterial surface, Dictionary<string, List<AudioClip>> clips)
        {
            string path = $"{AudioRoot}/Profiles/Surfaces/{surface}.asset";
            var set = AssetDatabase.LoadAssetAtPath<SurfaceDamageSounds>(path);
            if (set) return set;
            CheckFree(path);
            set = ScriptableObject.CreateInstance<SurfaceDamageSounds>();
            set.surface = surface;
            set.damaged = Sound(clips, ImpactCues.DamageKey(surface), 0.9f, 0.96f, 1.04f, 0.1f);
            set.broken = Sound(clips, ImpactCues.BreakKey(surface), 1f, 0.97f, 1.03f, 0.2f);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssetIfDirty(set);
            return set;
        }

        static ImpactSound Sound(Dictionary<string, List<AudioClip>> clips, string key, float volume, float pitchMin, float pitchMax, float cooldown) =>
            new ImpactSound
            {
                clips = clips.TryGetValue(key, out var list) ? list.OrderBy(c => c.name, StringComparer.Ordinal).ToArray() : Array.Empty<AudioClip>(),
                volume = volume, pitchMin = pitchMin, pitchMax = pitchMax, cooldown = cooldown,
            };

        static void CreateScene(FlexibleWeaponProfile profile)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)) return;
            CheckFree(ScenePath);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            if (!AssetDatabase.CopyAsset(TemplateScene, ScenePath)) throw new IOException("Could not copy the Tutorial room for the BELT range.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (!scene.IsValid() || !SceneManager.SetActiveScene(scene)) throw new InvalidOperationException("Could not open the BELT range for setup.");
                var roots = scene.GetRootGameObjects();
                var tutorial = roots.SelectMany(r => r.GetComponentsInChildren<TutorialDirector>(true)).SingleOrDefault();
                if (!tutorial) throw new InvalidOperationException("The Tutorial room template no longer has its director.");
                var source = new SerializedObject(tutorial);
                var joins = source.FindProperty("joins").objectReferenceValue;
                var hud = source.FindProperty("hud").objectReferenceValue;
                Object.DestroyImmediate(tutorial);
                foreach (var root in roots)
                    if (root && (root.name == "Tutorial Spots" || root.name == "Sign")) Object.DestroyImmediate(root);

                var range = new GameObject("BELT Range");
                SceneManager.MoveGameObjectToScene(range, scene);
                var director = range.AddComponent<BeltRangeDirector>();
                range.AddComponent<ImpactAudioScope>();
                var spots = new GameObject("Target spots").transform;
                spots.SetParent(range.transform, false);
                var so = new SerializedObject(director);
                so.FindProperty("joins").objectReferenceValue = joins;
                so.FindProperty("hud").objectReferenceValue = hud;
                so.FindProperty("belt").objectReferenceValue = profile;
                var list = so.FindProperty("targets");
                list.arraySize = Targets.Length;
                for (int i = 0; i < Targets.Length; i++)
                {
                    var t = Targets[i];
                    var spot = new GameObject(t.word).transform;
                    spot.SetParent(spots, false);
                    spot.localPosition = new Vector3(t.x, 0f, 1.4f);
                    var entry = list.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("word").stringValue = t.word;
                    entry.FindPropertyRelative("material").enumValueIndex = (int)t.material;
                    entry.FindPropertyRelative("spot").objectReferenceValue = spot;
                    entry.FindPropertyRelative("health").floatValue = t.health;
                    entry.FindPropertyRelative("respawnSeconds").floatValue = 2.5f;
                    entry.FindPropertyRelative("color").colorValue = t.color;
                }
                var dummy = new GameObject("Dummy spot").transform;
                dummy.SetParent(range.transform, false);
                dummy.localPosition = new Vector3(0f, 0f, 3.8f);
                so.FindProperty("dummySpot").objectReferenceValue = dummy;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the BELT range scene.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AddToBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Any(s => s.path == ScenePath)) return;
            EditorBuildSettings.scenes = scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        }

        static Material MaterialIfMissing(string path, Color color, float metallic, float smoothness, Texture2D baseMap, Texture2D normal)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            CheckFree(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("BELT materials require URP Lit.");
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (baseMap) material.SetTexture("_BaseMap", baseMap);
            if (normal)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static bool CopyMissing(string source, string target, string sha)
        {
            if (File.Exists(target))
            {
                if (sha != null && Hash(target) != sha) throw new IOException("Preserve changed imported source: " + target);
                return false;
            }
            CheckFree(target);
            WorkspaceAssetCopies.EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));
            File.Copy(source, target, false);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        static void CheckFree(string path)
        {
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path))
                throw new IOException("Preserve conflicting destination: " + path);
        }

        static void VerifySource(string path, string sha)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024 || Hash(path) != sha)
                throw new InvalidOperationException("Source missing or changed since it was verified: " + path);
        }

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
