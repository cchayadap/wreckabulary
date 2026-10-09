using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    /// <summary>Idempotent migration of the four shipped scenes into the current editable workspace.</summary>
    public static class SceneWorkspace
    {
        public const string SceneFolder = "Assets/_Project/Scenes/";
        public const string MigrationLabel = "CurrentAuthoringV2";
        public static readonly string[] SceneNames = { "Hub", "LivingRoom", "MovingDay", "Tutorial" };
        const string PreviewRoot = "Authoring Preview (Editor Only)";

        [MenuItem("Wreckabulary/Authoring/Upgrade Current Scenes", priority = 5)]
        public static void UpgradeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play mode before upgrading authoring scenes.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BackupOriginals();
            EditorSceneManager.OpenScene(SceneFolder + "Hub.unity", OpenSceneMode.Single);
            PlayerAuthoring.UpgradePrefab();
            WorldAuthoring.UpgradeWorldAssetsToCurrentVersion();
            foreach (string name in SceneNames)
            {
                var scene = EditorSceneManager.OpenScene(SceneFolder + name + ".unity", OpenSceneMode.Single);
                UpgradeOpenScene(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Failed to save " + scene.path);
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                var labels = AssetDatabase.GetLabels(asset);
                if (!labels.Contains(MigrationLabel)) AssetDatabase.SetLabels(asset, labels.Append(MigrationLabel).ToArray());
            }
            var current = EditorBuildSettings.scenes;
            var hubPath = SceneFolder + "Hub.unity";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(hubPath, true) }
                .Concat(current.Where(scene => scene.path != hubPath)).ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(hubPath, OpenSceneMode.Single);
            Debug.Log("AUTHORING_WORKSPACE_READY: editable current scenes, worlds and Player prefab.");
        }

        static void UpgradeOpenScene(Scene scene)
        {
            WorldAuthoring.ArchiveLegacySceneDecor();
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            if (scene.name is "LivingRoom" or "MovingDay") WorldAuthoring.UpgradeSceneWorld();
            var labels = asset ? AssetDatabase.GetLabels(asset) : Array.Empty<string>();
            if (labels.Contains(MigrationLabel)) return;
            if (labels.Any(label => label.StartsWith("CurrentAuthoring", StringComparison.Ordinal)))
            {
                HousePresentation.ApplyDefaultLighting();
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }
            if (scene.name is not ("LivingRoom" or "MovingDay"))
            {
                var room = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Room");
                // Preserve the authored hub/tutorial placement while replacing legacy block-built visual children.
                var library = ModelLibrary.Load();
                foreach (var item in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LetterBuilt>(true)))
                    if (!item.UsesImportedModel && library.Find(item.word == "RUG" ? "Environment/Round_Rug" : "Items/" + item.word)) item.Build();
                if (room) WorldAuthoring.PersistPresentation(room, scene.name.ToLowerInvariant());
            }
            HousePresentation.ApplyDefaultLighting();
            var marker = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EditorScenePreview>(true)).FirstOrDefault();
            var preview = marker ? marker.gameObject : new GameObject(PreviewRoot);
            preview.tag = "EditorOnly";
            if (!marker) preview.AddComponent<EditorScenePreview>();
            var existingAvatar = preview.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform playerRoot = existingAvatar ? existingAvatar.transform : null;
            while (playerRoot && playerRoot != preview.transform && playerRoot.parent != preview.transform) playerRoot = playerRoot.parent;
            var player = playerRoot ? playerRoot.gameObject : PlayerAuthoring.CreatePreview(preview.transform);
            var spawn = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "Spawn 1");
            Vector3 position = spawn ? spawn.position : new Vector3(0f, 0f, -3f);
            if (scene.name == "Hub") position = new Vector3(0f, 0f, -3.8f);
            if (scene.name is "LivingRoom" or "MovingDay")
                position = new Vector3(-6.2f, .15f, 7f);
            player.transform.position = position;
            player.transform.rotation = Quaternion.Euler(0f, scene.name is "LivingRoom" or "MovingDay" ? 90f : 0f, 0f);
            var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            PlayerAuthoring.FramePreview(camera, player.transform);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void BackupOriginals()
        {
            string folder = Path.Combine("Logs", "authoring-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(folder);
            foreach (string path in SceneNames.Select(name => SceneFolder + name + ".unity").Append(PlayerAuthoring.PlayerPrefabPath))
            {
                File.Copy(path, Path.Combine(folder, Path.GetFileName(path)));
                if (File.Exists(path + ".meta")) File.Copy(path + ".meta", Path.Combine(folder, Path.GetFileName(path) + ".meta"));
            }
            File.WriteAllText(Path.Combine(folder, "README.txt"), "Original local files before the reversible authoring migration. Restore only deliberately; current source may contain later edits.");
        }

        /// <summary>Launcher entry; opening the workspace never regenerates authored scene content.</summary>
        [MenuItem("Wreckabulary/Open Latest Hub", priority = 1)]
        public static void OpenLatest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SceneFolder + "Hub.unity", OpenSceneMode.Single);
            if (!Application.isBatchMode)
            {
                WorkspaceWindow.ShowWindow();
                var view = SceneView.lastActiveSceneView;
                if (view) view.LookAt(new Vector3(0f, .5f, 0f), Quaternion.Euler(45f, 0f, 0f), 13f);
            }
        }
    }
}
