using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;
using Wreckabulary.Online;

namespace Wreckabulary.EditorTools
{
    public static class WordboundBuildProfiles
    {
        const string Folder = "Assets/Settings/Build Profiles";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Create()
        {
            if (Application.unityVersion != "6000.6.3f1" && Application.unityVersion != "6000.6.4f1")
                throw new InvalidOperationException("Recheck the profile factory against this editor before creating build profiles.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Create profiles outside Play Mode and player builds.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows standalone build support is not installed.");
            string[] defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';');
            if (defines.Any(value => value.Trim() == "GSK_QA"))
                throw new InvalidOperationException("GSK_QA must be confined to the QA profile before making a stripped player.");
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).ToArray();
            if (scenes.Length == 0 || !scenes.Any(scene => scene.path == WordboundLaunchAssets.LobbyPath) ||
                !scenes.Any(scene => scene.path == WordboundLaunchAssets.GameplayPath))
                throw new InvalidOperationException("Register the saved lobby and Wordbound scenes before creating profiles.");
            var factory = typeof(BuildProfile).GetMethod("CreateInstance", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(BuildTarget), typeof(StandaloneBuildSubtarget) }, null);
            if (factory == null) throw new MissingMethodException("The verified Unity build-profile factory is unavailable.");
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            EnsureFolder(Folder);
            CreateOne("Win", false, false, factory, scenes);
            CreateOne("Win-QA", true, false, factory, scenes);
            CreateOne("Win-Dev", true, true, factory, scenes);
            if (BuildProfile.GetActiveBuildProfile() != activeProfile)
                throw new InvalidOperationException("Profile creation unexpectedly changed the active build profile.");
            Debug.Log("[Wreckabulary] Windows release, QA and development profiles verified; global scene order and active profile preserved.");
        }

        static void CreateOne(string name, bool qa, bool development, MethodInfo factory, EditorBuildSettingsScene[] scenes)
        {
            string path = Folder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
            if (existing)
            {
                Validate(existing, qa, development, scenes);
                return;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) || System.IO.File.Exists(path))
                throw new InvalidOperationException("Preserve the existing non-profile asset at " + path);
            var profile = (BuildProfile)factory.Invoke(null,
                new object[] { BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player });
            try
            {
                profile.name = name;
                profile.overrideGlobalScenes = false;
                profile.scriptingDefines = qa ? new[] { "GSK_QA" } : Array.Empty<string>();
                object platform = Property(typeof(BuildProfile), "platformBuildProfile").GetValue(profile);
                if (platform == null) throw new InvalidOperationException("Windows profile settings were not created.");
                Property(platform.GetType(), "development").SetValue(platform, development);
                Validate(profile, qa, development, scenes);
                AssetDatabase.CreateAsset(profile, path);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Validate(AssetDatabase.LoadAssetAtPath<BuildProfile>(path), qa, development, scenes);
            }
            finally
            {
                if (profile && !EditorUtility.IsPersistent(profile)) UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        static void Validate(BuildProfile profile, bool qa, bool development, EditorBuildSettingsScene[] scenes)
        {
            if (!profile ||
                (BuildTarget)Property(typeof(BuildProfile), "buildTarget").GetValue(profile) != BuildTarget.StandaloneWindows64 ||
                (StandaloneBuildSubtarget)Property(typeof(BuildProfile), "subtarget").GetValue(profile) != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("The build profile must target a Windows 64-bit player.");
            if (profile.overrideGlobalScenes || !profile.GetScenesForBuild().Where(scene => scene.enabled)
                .Select(scene => scene.path).SequenceEqual(scenes.Select(scene => scene.path)))
                throw new InvalidOperationException("The build profile must retain the registered global scenes.");
            string[] expected = qa ? new[] { "GSK_QA" } : Array.Empty<string>();
            if (!profile.scriptingDefines.SequenceEqual(expected))
                throw new InvalidOperationException("The profile scripting defines differ from its release/QA purpose.");
            object platform = Property(typeof(BuildProfile), "platformBuildProfile").GetValue(profile);
            if (platform == null || (bool)Property(platform.GetType(), "development").GetValue(platform) != development)
                throw new InvalidOperationException("The profile development setting differs from its purpose.");
        }

        static PropertyInfo Property(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }
            throw new MissingMemberException(type.FullName, name);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
