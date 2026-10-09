using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class ProductionBuild
    {
        [MenuItem("Wreckabulary/Build/Linux PC")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Wreckabulary.x86_64");

        [MenuItem("Wreckabulary/Build/Windows PC")]
        public static void Windows()
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Wreckabulary.exe");
        }

        [MenuItem("Wreckabulary/Build/Unity Web")]
        public static void Web()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            Build(BuildTarget.WebGL, "Builds/UnityWeb");
        }

        [MenuItem("Wreckabulary/Build/Android")]
        public static void Android()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Build(BuildTarget.Android, "Builds/Android/Wreckabulary.apk");
        }

        static void Build(BuildTarget target, string destination)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException($"Install the {target} build-support module for Unity 6000.6.3f1.");
            ProjectSetup.Run();
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != "Assets/_Project/Scenes/Hub.unity")
                throw new InvalidOperationException("The build must start in the Hub scene.");
            foreach (string scene in scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Build scene missing", scene);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes, locationPathName = destination, target = target, options = BuildOptions.None
            });
            Debug.Log($"PRODUCTION_BUILD target={target} result={report.summary.result} errors={report.summary.totalErrors} bytes={report.summary.totalSize}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"{target} build failed with {report.summary.totalErrors} error(s).");
        }
    }
}
