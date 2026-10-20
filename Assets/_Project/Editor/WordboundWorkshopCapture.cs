using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WordboundWorkshopCapture
    {
        const int Width = 1280, Height = 720;

        [MenuItem("Wreckabulary/Wordbound/Capture workshop templates")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture workshop templates outside Play mode.");
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("The workshop capture requires the project's active URP configuration.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Workshop capture requires a graphics device; omit -nographics.");

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/wordbound"));
            Directory.CreateDirectory(output);
            bool asyncCompilation = EditorSettings.asyncShaderCompilation;
            var previousActive = RenderTexture.active;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                Capture(WorkshopTemplate.RaceCircuit, "race-circuit", output);
                Capture(WorkshopTemplate.CombatArena, "combat-arena", output);
                Capture(WorkshopTemplate.FreestyleWorld, "freestyle-world", output);
            }
            finally
            {
                RenderTexture.active = previousActive;
                EditorSettings.asyncShaderCompilation = asyncCompilation;
            }
            Debug.Log("WORDBOUND_WORKSHOP_CAPTURE_COMPLETE: " + output);
        }

        static void Capture(WorkshopTemplate template, string filename, string output)
        {
            var preset = WorkshopTemplates.Create(template);
            var history = new WorkshopHistory(preset.Snapshot);
            var resolved = history.Preview(preset.Catalogue, preset.Play);
            if (!resolved.Success)
                throw new InvalidOperationException("Workshop template validation failed: " + template);

            var scene = EditorSceneManager.NewPreviewScene();
            GameObject owner = null;
            WordboundWorkshopViewport viewport = null;
            Texture2D pixels = null;
            var previousActive = RenderTexture.active;
            try
            {
                owner = new GameObject("Wordbound workshop capture") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(owner, scene);
                viewport = new WordboundWorkshopViewport(owner.transform)
                {
                    SpawnAssetVisual = (id, parent) => ModelVisual.Spawn("Items/" + id, parent)
                };
                viewport.Resize(Width, Height);
                viewport.Sync(preset.Snapshot, preset.Catalogue, resolved.Manifest);
                viewport.Frame();

                var camera = owner.GetComponentsInChildren<Camera>(true).Single();
                var target = viewport.Texture as RenderTexture;
                if (!target || camera.targetTexture != target || !target.IsCreated())
                    throw new InvalidOperationException("The workshop camera has no owned render target.");
                camera.enabled = false;
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);

                var lightObject = new GameObject("Workshop capture light") { hideFlags = HideFlags.HideAndDontSave };
                lightObject.transform.SetParent(owner.transform, false);
                lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
                light.cullingMask = camera.cullingMask;

                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderPipeline.SubmitRenderRequest(camera, request);
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false)
                {
                    name = "Workshop capture readback",
                    hideFlags = HideFlags.HideAndDontSave
                };
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                string path = Path.Combine(output, "workshop-" + filename + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                var record = new CaptureRecord
                {
                    template = template.ToString(),
                    courseId = preset.Snapshot.Course().Id,
                    seed = preset.Snapshot.Seed,
                    activeLayer = preset.Snapshot.ActiveLayer,
                    sourceHash = resolved.Manifest.SourceHash,
                    manifestHash = resolved.Manifest.Hash,
                    unityVersion = Application.unityVersion,
                    renderPipeline = GraphicsSettings.currentRenderPipeline.name,
                    capturedAtUtc = DateTime.UtcNow.ToString("O"),
                    cameraPosition = camera.transform.localPosition,
                    cameraEuler = camera.transform.localEulerAngles,
                    fieldOfView = camera.fieldOfView,
                    image = Path.GetFileName(path),
                    width = Width,
                    height = Height
                };
                File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonUtility.ToJson(record, true));
                Debug.Log("WORDBOUND_WORKSHOP_CAPTURE: " + path + " manifest=" + record.manifestHash);
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (pixels) Object.DestroyImmediate(pixels);
                try { viewport?.Dispose(); }
                finally
                {
                    try { if (owner) Object.DestroyImmediate(owner); }
                    finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
                }
            }
        }

        [Serializable]
        sealed class CaptureRecord
        {
            public string template, courseId, seed, activeLayer, sourceHash, manifestHash;
            public string unityVersion, renderPipeline, capturedAtUtc, image;
            public Vector3 cameraPosition, cameraEuler;
            public float fieldOfView;
            public int width, height;
        }
    }
}
