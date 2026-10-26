using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WorkspaceStudioSetup
    {
        public const string SceneFolder = "Assets/_Project/Scenes/Authoring";
        public const string EquipmentScene = SceneFolder + "/EquipmentStudio.unity";
        public const string CharacterScene = SceneFolder + "/CharacterStudio.unity";

        [MenuItem("Wreckabulary/Authoring/Create Missing Developer Assets", priority = 2)]
        public static void CreateOrVerify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create developer assets outside Play Mode.");
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Config/items.json");
            if (!library || !source || !library.Find("Avatar/Avatar")) throw new InvalidOperationException("The original model library and catalogue are required.");
            var items = ItemCatalogue.FromJson(source.text).Enabled.OrderBy(item => item.Category).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
            if (items.Length == 0 || items.Length > 64) throw new InvalidOperationException("The equipment studio requires 1–64 enabled items.");
            foreach (var item in items)
            {
                if (!library.Find(item.Model)) throw new InvalidOperationException("Missing original model: " + item.Model);
                CheckExisting<GameObject>(PrefabPath(item));
            }
            CheckExisting<SceneAsset>(EquipmentScene);
            CheckExisting<SceneAsset>(CharacterScene);
            var typography = LobbyTypographySetup.CreateOrVerify();
            if (!typography) throw new InvalidOperationException("Typography setup needs attention; existing assets were preserved.");
            WorkspaceAssetCopies.EnsureFolder(SceneFolder);
            WorkspaceAssetCopies.EnsureFolder("Assets/_Project/Materials/Authored");
            foreach (var item in items)
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(item)))
                    WorkspaceAssetCopies.Create(library.Find(item.Model), PrefabPath(item));
            CreateSceneIfMissing(EquipmentScene, () => CreateEquipment(items));
            CreateSceneIfMissing(CharacterScene, CreateCharacter);
            LobbyShopLayoutSetup.CreateAt(LobbyShopLayoutSetup.ProfilePath, LobbyShopLayoutSetup.PrefabFolder, LobbyShopLayoutSetup.GraphicFolder, typography);
            Debug.Log("WORKSPACE_AUTHORING_READY: saved equipment and character studios; categorized editable prefab variants; saved lobby typography and Shop layout. Existing assets preserved; gameplay visual assignments unchanged.");
        }

        public static string PrefabPath(ItemDefinition item) => "Assets/_Project/Prefabs/" +
            (item.Category == ItemCategory.Furniture ? "Furniture" : "Equipment/" + (item.Category == ItemCategory.Combat ? "Weapons" : "Tools")) +
            "/" + item.Id + ".prefab";

        static void CheckExisting<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset && asset is T) return;
            if (asset || File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                throw new InvalidOperationException("An existing path cannot be replaced: " + path);
        }

        internal static bool CreateSceneIfMissing(string path, Action populate)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create developer assets outside Play Mode.");
            if (populate == null) throw new ArgumentNullException(nameof(populate));
            CheckExisting<SceneAsset>(path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return false;
            WorkspaceAssetCopies.ValidateDestination(path, ".unity");
            string template = AssetDatabase.GUIDToAssetPath(WorkspaceWindow.WordboundGameplayGuid);
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(template)) throw new InvalidOperationException("The saved gameplay scene template is unavailable.");
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            WorkspaceAssetCopies.EnsureFolder(folder);
            string temporary = folder + "/WorkspaceStudioTemp-" + Guid.NewGuid().ToString("N") + ".unity";
            WorkspaceAssetCopies.ValidateDestination(temporary, ".unity");
            var previous = SceneManager.GetActiveScene();
            Scene scene = default;
            bool staged = false;
            try
            {
                if (!AssetDatabase.CopyAsset(template, temporary)) throw new IOException("Could not stage studio: " + temporary);
                staged = true;
                scene = EditorSceneManager.OpenScene(temporary, OpenSceneMode.Additive);
                if (!scene.IsValid() || !scene.isLoaded || scene.path != temporary || !SceneManager.SetActiveScene(scene))
                    throw new InvalidOperationException("Could not isolate the studio from the authoring scene.");
                foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
                populate();
                if (scene.rootCount == 0) throw new InvalidOperationException("The generated studio has no objects.");
                if (!EditorSceneManager.SaveScene(scene, temporary)) throw new IOException("Could not save staged studio: " + temporary);
                if (!EditorSceneManager.CloseScene(scene, true)) throw new IOException("Could not close staged studio: " + temporary);
                scene = default;
                WorkspaceAssetCopies.ValidateDestination(path, ".unity");
                string error = AssetDatabase.MoveAsset(temporary, path);
                if (!string.IsNullOrEmpty(error)) throw new IOException("Could not publish studio: " + error);
                return true;
            }
            finally
            {
                try
                {
                    if (scene.IsValid() && scene.isLoaded && !EditorSceneManager.CloseScene(scene, true))
                        throw new IOException("Could not close temporary studio: " + temporary);
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (staged && (!scene.IsValid() || !scene.isLoaded) &&
                        (File.Exists(temporary) || File.Exists(temporary + ".meta") || AssetDatabase.LoadMainAssetAtPath(temporary)) &&
                        !AssetDatabase.DeleteAsset(temporary))
                        throw new IOException("Could not remove temporary studio: " + temporary);
                }
            }
        }

        static void CreateEquipment(ItemDefinition[] items)
        {
            var root = new GameObject("Equipment · editable prefab copies");
            for (int i = 0; i < items.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(items[i]));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = items[i].Id;
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("The editable prefab has no renderers: " + items[i].Id);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                instance.transform.position += new Vector3((i % 4) * 3f, .05f, (i / 4) * 3f) - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            int rows = (items.Length + 3) / 4;
            var center = new Vector3(4.5f, .8f, (rows - 1) * 1.5f);
            CreateLightingAndCamera(center, new Vector3(12f, 12f, -13f), Mathf.Max(7f, rows * 2f));
        }

        static void CreateCharacter()
        {
            var root = new GameObject("Character · shared original rig");
            var character = PlayerAuthoring.CreatePreview(root.transform);
            character.transform.localPosition = Vector3.zero;
            CreateLightingAndCamera(new Vector3(0f, .85f, 0f), new Vector3(2.5f, 1.6f, -4f), 1.65f);
        }

        static void CreateLightingAndCamera(Vector3 center, Vector3 offset, float size)
        {
            var root = new GameObject("Lighting & camera");
            var camera = new GameObject("Studio Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.tag = "MainCamera";
            camera.transform.position = center + offset;
            camera.transform.LookAt(center);
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.09f, .13f, .15f);
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 150f;
            var light = new GameObject("Studio Key").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        }
    }
}
