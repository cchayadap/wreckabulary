using System;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Online;

namespace Wreckabulary.EditorTools
{
    public static class WordboundLaunchSetup
    {
        const string Folder = "Assets/_Project/Resources/Wordbound";
        const string SettingsPath = Folder + "/LaunchAssets.asset";
        const string TrustPath = Folder + "/production-client.json";

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Create launch scenes outside Play Mode.");
            string source = File.ReadAllText("OnlineServices/production-client.json");
            _ = new WordboundClientTrust(source);
            EnsureFolder(Folder);
            if (File.Exists(TrustPath) && File.ReadAllText(TrustPath) != source)
                throw new InvalidOperationException("Existing launch trust differs; preserve it for review.");
            if (!File.Exists(TrustPath)) File.WriteAllText(TrustPath, source);
            AssetDatabase.ImportAsset(TrustPath, ImportAssetOptions.ForceSynchronousImport);
            var assets = AssetDatabase.LoadAssetAtPath<WordboundLaunchAssets>(SettingsPath);
            if (!assets)
            {
                assets = ScriptableObject.CreateInstance<WordboundLaunchAssets>();
                AssetDatabase.CreateAsset(assets, SettingsPath);
            }
            assets.PublicTrust = AssetDatabase.LoadAssetAtPath<TextAsset>(TrustPath);
            assets.PlayerBody = Body("WordboundPlayerBody");
            assets.ItemBody = Body("WordboundItemBody");
            assets.PlatformBody = Body("WordboundPlatformBody");
            assets.SnapshotChunk = NetworkPrefabRef.Parse(
                AssetDatabase.AssetPathToGUID(WordboundFusionSetup.SnapshotPrefabPath));
            assets.BodyPrefabs();
            EditorUtility.SetDirty(assets);
            PrepareSceneWorkspace();
            CreateScene(WordboundLaunchAssets.LobbyPath, true, assets);
            CreateScene(WordboundLaunchAssets.GameplayPath, false, assets);
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { WordboundLaunchAssets.LobbyPath, WordboundLaunchAssets.GameplayPath })
            {
                var existing = scenes.FirstOrDefault(scene => scene.path == path);
                if (existing == null) scenes.Add(new EditorBuildSettingsScene(path, true));
                else if (!existing.enabled) existing.enabled = true;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Wreckabulary] Wordbound lobby, gameplay scene and public launch data are registered.");
        }

        static void PrepareSceneWorkspace()
        {
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) return;
            if (!Application.isBatchMode || Enumerable.Range(0, SceneManager.sceneCount)
                .Any(index => SceneManager.GetSceneAt(index).isDirty))
                throw new InvalidOperationException("Save the open scene before creating launch scenes.");
            var saved = EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled && File.Exists(scene.path));
            if (saved == null) throw new InvalidOperationException("No saved scene is available for the batch setup workspace.");
            EditorSceneManager.OpenScene(saved.path, OpenSceneMode.Single);
        }

        static NetworkObject Body(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(WordboundGameplaySetup.Folder + "/" + name + ".prefab");
            return prefab ? prefab : throw new InvalidOperationException("Missing native prefab: " + name);
        }

        static void CreateScene(string path, bool lobby, WordboundLaunchAssets assets)
        {
            if (File.Exists(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prior = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                if (lobby)
                {
                    var lens = new GameObject("Wordbound lobby camera").AddComponent<Camera>();
                    lens.clearFlags = CameraClearFlags.SolidColor;
                    lens.backgroundColor = new Color(.075f, .105f, .14f);
                    lens.cullingMask = 0;
                    lens.gameObject.AddComponent<AudioListener>();
                    var launcher = new GameObject("Wordbound").AddComponent<WordboundLauncher>();
                    launcher.Assets = assets;
                    launcher.LobbyCamera = lens;
                }
                else
                {
                    var light = new GameObject("Wordbound sunlight").AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    light.transform.rotation = Quaternion.Euler(50, -35, 0);
                    light.shadows = LightShadows.Soft;
                }
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
            }
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
