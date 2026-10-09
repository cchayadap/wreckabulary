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
    public static class EnvironmentLightingAuthoring
    {
        public const string ProfilePath = "Assets/_Project/Resources/Environment/SunlitHouse.asset";
        public const string SkyPath = "Assets/_Project/Materials/Environment/DaylightSky.mat";

        [MenuItem("Wreckabulary/Authoring/Add Daylight and Practical Lighting", priority = 8)]
        public static void UpgradeScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before authoring lighting.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var profile = EnsureProfile();
            foreach (string name in SceneWorkspace.SceneNames)
            {
                var scene = EditorSceneManager.OpenScene(SceneWorkspace.SceneFolder + name + ".unity", OpenSceneMode.Single);
                bool changed = AddMissing(scene, profile);
                if (changed && !EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save lighting: " + scene.path);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneWorkspace.SceneFolder + "Hub.unity", OpenSceneMode.Single);
            Debug.Log("ENVIRONMENT_LIGHTING_READY: editable daylight profile, sky and scene-owned sun/fill.");
        }

        public static EnvironmentLightingProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnvironmentLightingProfile>(ProfilePath);
            if (profile) return profile;
            EnsureFolder(Path.GetDirectoryName(ProfilePath).Replace('\\', '/'));
            EnsureFolder(Path.GetDirectoryName(SkyPath).Replace('\\', '/'));
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (!sky)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (!shader) throw new InvalidOperationException("The procedural sky shader is missing.");
                sky = new Material(shader) { name = "Daylight Sky" };
                sky.SetFloat("_SunDisk", 2f);
                sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
                sky.SetFloat("_SunSize", .06f);
                sky.SetFloat("_SunSizeConvergence", 5f);
                sky.SetFloat("_AtmosphereThickness", .8f);
                sky.SetColor("_SkyTint", new Color(.48f, .61f, .69f));
                sky.SetColor("_GroundColor", new Color(.67f, .69f, .57f));
                sky.SetFloat("_Exposure", 1.1f);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            profile = ScriptableObject.CreateInstance<EnvironmentLightingProfile>();
            profile.Skybox = sky;
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Add missing settings once; existing profile choices, transforms and artist light edits stay intact.</summary>
        public static bool AddMissing(Scene scene, EnvironmentLightingProfile profile)
        {
            var roots = scene.GetRootGameObjects();
            if (roots.SelectMany(root => root.GetComponentsInChildren<EnvironmentLighting>(true)).Any()) return false;
            var root = new GameObject("Environment lighting");
            SceneManager.MoveGameObjectToScene(root, scene);
            var sun = roots.SelectMany(item => item.GetComponentsInChildren<Light>(true))
                .Where(light => light.type == LightType.Directional).OrderByDescending(light => light.name == "Sun").FirstOrDefault();
            if (!sun) sun = Directional("Sun", root.transform);
            var fill = Directional("House fill", root.transform);
            var lighting = root.AddComponent<EnvironmentLighting>();
            lighting.Configure(profile, sun, fill);
            lighting.ApplySceneSettings();
            foreach (var camera in roots.SelectMany(item => item.GetComponentsInChildren<Camera>(true))) lighting.ApplyCamera(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        static Light Directional(string name, Transform parent)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = LightType.Directional;
            return light;
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
