using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    /// <summary>Renders the saved scene content without entering Play or generating a runtime room.</summary>
    public static class AuthoringCapture
    {
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture saved authoring scenes outside Play mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                bool disposableStartup = WorkspaceBootstrap.IsAutomatedSession && string.IsNullOrEmpty(open.path);
                if (!disposableStartup && (open.isDirty || (string.IsNullOrEmpty(open.path) && open.rootCount > 0)))
                    throw new InvalidOperationException("Save open scene work before capturing authored scenes: " + open.name);
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            string output = Environment.GetEnvironmentVariable("WRECKABULARY_CAPTURE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(output)) output = "docs/reviews/evidence/unity-authoring-2026-10-09";
            Directory.CreateDirectory(output);
            bool asyncCompilation = EditorSettings.asyncShaderCompilation;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                foreach (string name in SceneWorkspace.SceneNames)
                {
                    var scene = EditorSceneManager.OpenScene(SceneWorkspace.SceneFolder + name + ".unity", OpenSceneMode.Single);
                    var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).First();
                    var position = camera.transform.position;
                    var rotation = camera.transform.rotation;
                    float fieldOfView = camera.fieldOfView;
                    var target = camera.targetTexture;
                    try
                    {
                        Capture(camera, output + "/" + name + "-saved-camera.png");
                        camera.transform.SetPositionAndRotation(new Vector3(0f, 26f, -22f), Quaternion.Euler(49f, 0f, 0f));
                        camera.fieldOfView = 55f;
                        Capture(camera, output + "/" + name + "-editor-overview.png");
                    }
                    finally
                    {
                        camera.transform.SetPositionAndRotation(position, rotation);
                        camera.fieldOfView = fieldOfView;
                        camera.targetTexture = target;
                    }
                }
            }
            finally
            {
                EditorSettings.asyncShaderCompilation = asyncCompilation;
                if (setup.Length > 0 && setup.All(item => !string.IsNullOrEmpty(item.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.OpenScene(SceneWorkspace.SceneFolder + "Hub.unity", OpenSceneMode.Single);
            }
            Debug.Log("AUTHORING_CAPTURE_COMPLETE: saved Unity scene cameras and editor overviews.");
        }

        static void Capture(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                // Prime freshly imported materials before reading the first saved-scene frame.
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
