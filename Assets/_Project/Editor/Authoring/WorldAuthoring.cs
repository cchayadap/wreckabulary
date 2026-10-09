using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    /// <summary>One-time migration from generated rooms to normal editable Unity world assets.</summary>
    public static class WorldAuthoring
    {
        const string ResourceFolder = "Assets/_Project/Resources/Worlds";
        const string MaterialFolder = "Assets/_Project/Worlds/Generated/Materials";
        const string MeshFolder = "Assets/_Project/Worlds/Generated/Meshes";
        const string TextureFolder = "Assets/_Project/Worlds/Generated/Textures";
        const string LegacyFolder = "Assets/_Project/Editor/Legacy/Scenes";
        const string LegacyWorldFolder = "Assets/_Project/Editor/Legacy/Worlds";
        const string LegacyDependencyFolder = "Assets/_Project/Editor/Legacy/Dependencies";
        static readonly string[] MapIds = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        /// <summary>Repair only this edition's generated GPU-only textures, retaining their GUIDs and material references.</summary>
        public static void RepairGeneratedWorldTextures()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Repair textures outside Play mode.");
            if (!AssetDatabase.IsValidFolder(TextureFolder)) return;
            string prefix = TextureFolder + "/v" + AuthoredHouse.CurrentContentVersion + "_";
            int repaired = 0;
            foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.Ordinal)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!texture || texture.isReadable) continue;
                var readable = ReadTextureForPersistence(texture);
                try
                {
                    if (!path.Contains("_tactile_finish_") && !HasPixelVariation(readable))
                        throw new InvalidOperationException("Generated texture has no pattern data; left unchanged: " + path);
                    EditorUtility.CopySerialized(readable, texture);
                    EditorUtility.SetDirty(texture);
                    repaired++;
                }
                finally { Object.DestroyImmediate(readable); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_TEXTURES_REPAIRED: " + repaired + " generated textures retained their GUIDs.");
        }

        /// <summary>Read GPU-only procedural textures without Instantiate; keep a serializable CPU copy in the asset.</summary>
        public static Texture2D ReadTextureForPersistence(Texture2D source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (source.isReadable)
            {
                var copy = Object.Instantiate(source);
                copy.hideFlags = HideFlags.None;
                return copy;
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Saving GPU-only textures requires a graphics device; omit -nographics.");
            if (GraphicsFormatUtility.IsCompressedFormat(source.graphicsFormat))
                throw new InvalidOperationException("Generated texture persistence requires an uncompressed source: " + source.name);

            bool srgb = GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat);
            var descriptor = new RenderTextureDescriptor(source.width, source.height, RenderTextureFormat.ARGB32, 0)
            { graphicsFormat = source.graphicsFormat, msaaSamples = 1, useMipMap = false, autoGenerateMips = false };
            var target = RenderTexture.GetTemporary(descriptor);
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            Texture2D result = null;
            try
            {
                if ((SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0)
                    Graphics.CopyTexture(source, 0, 0, target, 0, 0);
                else
                {
                    GL.sRGBWrite = srgb;
                    Graphics.Blit(source, target);
                }
                RenderTexture.active = target;
                result = new Texture2D(source.width, source.height, source.format, source.mipmapCount, !srgb)
                {
                    name = source.name, hideFlags = HideFlags.None, filterMode = source.filterMode,
                    wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV, wrapModeW = source.wrapModeW,
                    anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias
                };
                result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                result.Apply(source.mipmapCount > 1, false);
                return result;
            }
            catch
            {
                if (result) Object.DestroyImmediate(result);
                throw;
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        static bool HasPixelVariation(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            if (pixels.Length < 2) return false;
            Color32 first = pixels[0];
            for (int i = 1; i < pixels.Length; i++)
                if (!pixels[i].Equals(first)) return true;
            return false;
        }

        public static void BakeWorldAssetsIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake worlds outside Play mode.");
            EnsureFolder(ResourceFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            OrganizeLegacyDependencies();
            foreach (string mapId in MapIds)
            {
                string assetPath = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(mapId) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)) continue;
                BakeMissingWorld(mapId, assetPath);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Explicit schema migration: retain old editions and replace only obsolete resource prefabs.</summary>
        public static void UpgradeWorldAssetsToCurrentVersion()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Upgrade worlds outside Play mode.");
            EnsureFolder(LegacyWorldFolder);
            foreach (string mapId in MapIds)
            {
                string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(mapId) + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                var world = prefab.GetComponent<AuthoredHouse>();
                if (world && world.IsCurrent) continue;
                int version = world ? world.ContentVersion : 0;
                string archive = AssetDatabase.GenerateUniqueAssetPath(LegacyWorldFolder + "/" + Path.GetFileNameWithoutExtension(path) + "_v" + version + ".prefab");
                string error = AssetDatabase.MoveAsset(path, archive);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not preserve authored world " + path + ": " + error);
            }
            BakeWorldAssetsIfMissing();
        }

        /// <summary>Move only archived prototype dependencies, retaining asset GUIDs and all prefab references.</summary>
        public static void OrganizeLegacyDependencies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Organize legacy assets outside Play mode.");
            foreach (var entry in new[] { (source: MaterialFolder, kind: "Materials", extension: ".mat"),
                         (source: MeshFolder, kind: "Meshes", extension: ".asset"),
                         (source: TextureFolder, kind: "Textures", extension: ".asset") })
            {
                if (!AssetDatabase.IsValidFolder(entry.source)) continue;
                string destinationFolder = LegacyDependencyFolder + "/" + entry.kind;
                EnsureFolder(destinationFolder);
                var paths = AssetDatabase.FindAssets("", new[] { entry.source })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.StartsWith(entry.source + "/legacy_", StringComparison.Ordinal)
                        && string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), entry.source, StringComparison.Ordinal)
                        && path.EndsWith(entry.extension, StringComparison.Ordinal)).ToArray();
                foreach (string path in paths)
                {
                    string destination = AssetDatabase.GenerateUniqueAssetPath(destinationFolder + "/" + Path.GetFileName(path));
                    string error = AssetDatabase.MoveAsset(path, destination);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not move archived dependency " + path + ": " + error);
                }
            }
        }

        static void BakeMissingWorld(string mapId, string assetPath)
        {
            var previousScene = SceneManager.GetActiveScene();
            string previousMode = Match.ModeOverride;
            var bakeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(bakeScene);
            try
            {
                Match.ModeOverride = "Dibs";
                var layout = GameConfig.Current.HouseFor(mapId);
                var root = new GameObject(layout.Name);
                var generator = root.AddComponent<RoomBuilder>();
                var world = generator.BuildForAuthoring(layout, mapId);
                string stem = mapId + "_v" + AuthoredHouse.CurrentContentVersion;
                PersistPresentation(world.GeometryRoot.gameObject, stem);
                PersistAssets(world.FurnitureRoot.gameObject, stem + "_furniture");
                Object.DestroyImmediate(generator);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath, out bool success);
                if (!success || !saved) throw new InvalidOperationException("Could not save authored world: " + assetPath);
            }
            finally
            {
                Match.ModeOverride = previousMode;
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(bakeScene, true);
            }
        }

        /// <summary>Current authored instances win; obsolete editions and their overrides are archived before replacement.</summary>
        public static void UpgradeSceneWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Upgrade scenes outside Play mode.");
            OrganizeLegacyDependencies();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var builder = roots.SelectMany(root => root.GetComponentsInChildren<RoomBuilder>(true)).FirstOrDefault();
            if (!builder)
            {
                var movingDay = roots.SelectMany(root => root.GetComponentsInChildren<MovingDayDirector>(true)).FirstOrDefault();
                if (!movingDay) return;
                builder = Undo.AddComponent<RoomBuilder>(movingDay.gameObject);
            }
            var existing = builder.AuthoredWorld;
            if (!existing) existing = roots.SelectMany(root => root.GetComponentsInChildren<AuthoredHouse>(true)).FirstOrDefault();
            if (existing && existing.IsCurrent)
            {
                if (builder.AuthoredWorld != existing)
                {
                    Undo.RecordObject(builder, "Connect authored house");
                    builder.SetAuthoredWorld(existing);
                    EditorUtility.SetDirty(builder);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                ArchiveLegacyRoots(scene, roots);
                return;
            }

            string mapId = existing ? existing.MapId : "pinwheel";
            string resource = AuthoredHouse.ResourcePath(mapId);
            if (resource == null) throw new InvalidOperationException("Cannot upgrade unknown authored map: " + mapId);
            UpgradeWorldAssetsToCurrentVersion();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/" + resource + ".prefab");
            if (!prefab) throw new InvalidOperationException("The authored world prefab is missing: " + resource);
            Transform parent = existing ? existing.transform.parent : null;
            Vector3 position = existing ? existing.transform.localPosition : Vector3.zero;
            Quaternion rotation = existing ? existing.transform.localRotation : Quaternion.identity;
            Vector3 scale = existing ? existing.transform.localScale : Vector3.one;
            if (existing) ArchiveRoots(scene, new[] { existing.gameObject }, "WorldV" + existing.ContentVersion);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(parent, false);
            instance.transform.SetLocalPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            Undo.RegisterCreatedObjectUndo(instance, "Add authored house");
            var world = instance.GetComponent<AuthoredHouse>();
            if (!world) throw new InvalidOperationException("The world prefab must contain AuthoredHouse.");
            Undo.RecordObject(builder, "Connect authored house");
            builder.SetAuthoredWorld(world);
            EditorUtility.SetDirty(builder);

            ArchiveLegacyRoots(scene, roots);
            HousePresentation.ApplyDefaultLighting();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void ArchiveLegacyRoots(Scene scene, GameObject[] roots)
        {
            var legacyRoots = roots.Where(root => root && (root.name is "Room" or "Furniture"))
                .Where(root => !root.GetComponentInChildren<AuthoredHouse>(true)).ToArray();
            ArchiveRoots(scene, legacyRoots, "PrototypeContent");
            ArchiveLegacySceneDecor(scene, roots);
        }

        /// <summary>Remove only unchanged prototype signs/room labels, after archiving them outside shipped content.</summary>
        public static void ArchiveLegacySceneDecor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Archive legacy decor outside Play mode.");
            var scene = SceneManager.GetActiveScene();
            ArchiveLegacySceneDecor(scene, scene.GetRootGameObjects());
        }

        static void ArchiveLegacySceneDecor(Scene scene, GameObject[] roots)
        {
            if (scene.name is not ("LivingRoom" or "MovingDay")) return;
            var legacyDecor = roots.Where(root => IsLegacyDecor(root, scene.name)).ToArray();
            ArchiveRoots(scene, legacyDecor, "LegacyDecor");
        }

        static bool IsLegacyDecor(GameObject root, string sceneName)
        {
            if (!root || root.GetComponentInChildren<AuthoredHouse>(true) || !root.TryGetComponent<TMP_Text>(out var label)) return false;
            if (sceneName == "LivingRoom") return root.name == "Sign" && label.text == "<i>Home Sweet Home</i>";
            return sceneName == "MovingDay" &&
                ((root.name == "Sign" && label.text == "<i>Moving Day</i>") ||
                 (root.name == "Floor Label" && (label.text is "LIVING ROOM" or "BEDROOM")));
        }

        static void ArchiveRoots(Scene scene, GameObject[] legacyRoots, string suffix)
        {
            if (legacyRoots.Length == 0) return;
            EnsureFolder(LegacyFolder);
            var archive = new GameObject(scene.name + " " + suffix);
            archive.SetActive(false);
            try
            {
                foreach (var legacy in legacyRoots)
                {
                    var copy = Object.Instantiate(legacy, archive.transform, true);
                    copy.name = legacy.name;
                }
                PersistAssets(archive, "legacy_" + scene.name);
                string path = AssetDatabase.GenerateUniqueAssetPath(LegacyFolder + "/" + SafeName(scene.name) + "_" + suffix + ".prefab");
                var saved = PrefabUtility.SaveAsPrefabAsset(archive, path, out bool success);
                if (!success || !saved) throw new InvalidOperationException("Could not preserve legacy scene content: " + path);
                foreach (var legacy in legacyRoots) Undo.DestroyObjectImmediate(legacy);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            finally
            {
                Object.DestroyImmediate(archive);
            }
        }

        /// <summary>Bake the palette and generated detail once; subsequent calls preserve artist material/mesh edits.</summary>
        public static void PersistPresentation(GameObject root, string assetStem)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Persist presentation outside Play mode.");
            var presentation = root.GetComponent<HousePresentation>();
            if (presentation && presentation.IsAuthored) return;
            if (!presentation)
            {
                HousePresentation.Apply(root, true);
                presentation = root.GetComponent<HousePresentation>();
            }
            PersistAssets(root, assetStem);
            presentation.MarkAuthored();
            EditorUtility.SetDirty(presentation);
            AssetDatabase.SaveAssets();
        }

        public static void PersistAssets(GameObject root, string assetStem)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Persist authored dependencies outside Play mode.");
            bool legacy = assetStem.StartsWith("legacy_", StringComparison.Ordinal);
            string materialFolder = legacy ? LegacyDependencyFolder + "/Materials" : MaterialFolder;
            string meshFolder = legacy ? LegacyDependencyFolder + "/Meshes" : MeshFolder;
            string textureFolder = legacy ? LegacyDependencyFolder + "/Textures" : TextureFolder;
            EnsureFolder(materialFolder);
            EnsureFolder(meshFolder);
            EnsureFolder(textureFolder);
            string stem = SafeName(assetStem);
            var meshes = new Dictionary<Mesh, Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || EditorUtility.IsPersistent(mesh)) continue;
                if (!meshes.TryGetValue(mesh, out var persistent))
                {
                    persistent = Object.Instantiate(mesh);
                    persistent.name = mesh.name;
                    persistent.hideFlags = HideFlags.None;
                    string path = AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + stem + "_" + SafeName(mesh.name) + ".asset");
                    AssetDatabase.CreateAsset(persistent, path);
                    meshes.Add(mesh, persistent);
                }
                filter.sharedMesh = persistent;
                EditorUtility.SetDirty(filter);
            }

            var block = new MaterialPropertyBlock();
            var slotBlock = new MaterialPropertyBlock();
            var textures = new Dictionary<Texture, Texture>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source) continue;
                    renderer.GetPropertyBlock(slotBlock, i);
                    if (EditorUtility.IsPersistent(source) && block.isEmpty && slotBlock.isEmpty && !HasTransientTextures(source)) continue;
                    var copy = new Material(source) { name = source.name, hideFlags = HideFlags.None };
                    ApplyProperties(copy, block);
                    ApplyProperties(copy, slotBlock);
                    PersistTextures(copy, textureFolder, textures);
                    string fingerprint = MaterialFingerprint(source, copy);
                    string path = materialFolder + "/" + stem + "_" + SafeName(source.name) + "_" + Hash128.Compute(fingerprint) + ".mat";
                    var persistent = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (persistent) Object.DestroyImmediate(copy);
                    else
                    {
                        AssetDatabase.CreateAsset(copy, path);
                        persistent = copy;
                    }
                    materials[i] = persistent;
                    renderer.SetPropertyBlock(null, i);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null);
                EditorUtility.SetDirty(renderer);
                block.Clear();
                slotBlock.Clear();
            }
        }

        static bool HasTransientTextures(Material material) => material.GetTexturePropertyNames()
            .Any(property => material.GetTexture(property) && !EditorUtility.IsPersistent(material.GetTexture(property)));

        static void PersistTextures(Material material, string folder, Dictionary<Texture, Texture> textures)
        {
            foreach (string property in material.GetTexturePropertyNames())
            {
                var source = material.GetTexture(property);
                if (!source || EditorUtility.IsPersistent(source)) continue;
                if (source is RenderTexture) throw new InvalidOperationException("Cannot persist a live render target on " + material.name + ": " + property);
                if (!textures.TryGetValue(source, out var persistent))
                {
                    string fingerprint = source.name + "|" + source.width + "x" + source.height + "|" + source.graphicsFormat + "|" + source.imageContentsHash;
                    string path = folder + "/v" + AuthoredHouse.CurrentContentVersion + "_" + SafeName(source.name) + "_" + Hash128.Compute(fingerprint) + ".asset";
                    persistent = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (!persistent)
                    {
                        if (source is not Texture2D source2D)
                            throw new InvalidOperationException("Only procedural Texture2D assets can be persisted: " + source.name);
                        persistent = ReadTextureForPersistence(source2D);
                        persistent.name = source.name;
                        persistent.hideFlags = HideFlags.None;
                        AssetDatabase.CreateAsset(persistent, path);
                    }
                    textures.Add(source, persistent);
                }
                material.SetTexture(property, persistent);
            }
        }

        static void ApplyProperties(Material material, MaterialPropertyBlock properties)
        {
            if (properties.HasColor(BaseColor) && material.HasProperty(BaseColor)) material.SetColor(BaseColor, properties.GetColor(BaseColor));
            if (properties.HasColor(LegacyColor) && material.HasProperty(LegacyColor)) material.SetColor(LegacyColor, properties.GetColor(LegacyColor));
            if (properties.HasFloat(Smoothness) && material.HasProperty(Smoothness)) material.SetFloat(Smoothness, properties.GetFloat(Smoothness));
        }

        static string MaterialFingerprint(Material source, Material material)
        {
            return AssetDatabase.GetAssetPath(source) + "|" + EditorJsonUtility.ToJson(material);
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "World";
            return new string(value.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
