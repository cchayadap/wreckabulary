using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundArenaPreviewCapture
    {
        public const string CaptureRoot = "BuildNotes/play-flow-20261029/arena-preview/native";
        internal const string Seed = "0123456789abcdef";
        internal const string LayoutPath = "Assets/_Project/Resources/UI/LobbyPlayLayout.asset";
        internal const string ImageName = "ToyboxArena.png";

        [MenuItem("Tools/Wordbound/Presentation/Capture actual Toybox Arena preview")]
        public static void Capture()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen,
                "Close Play and the framing window before capturing the arena.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && GraphicsSettings.currentRenderPipeline,
                "Arena capture needs the installed renderer and a graphics device.");
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            Require(data && data.items && data.rules && launch && launch.VisualProfile &&
                launch.VisualProfile.BaseArtwork == WordboundBaseArtwork.OriginalWreckabulary, "Use the saved original-art release inputs.");
            var content = Content(data, launch);
            var inputs = Inputs(data, launch);
            var pose = new WordboundSoloPreviewPose { Module = "Start", Position = new Vector3(-8, 3.2f, -8.8f),
                Target = new Vector3(-1, .8f, .5f), FieldOfView = 60 };
            var beforeScene = SceneState(); string source = WordboundSoloPreviewCapture.SourceFingerprint();
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite, async = EditorSettings.asyncShaderCompilation;
            bool ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"); int tactile = TactileMaterials.RegisteredScopeCount;
            string folder = Path.GetFullPath(CaptureRoot + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            WordboundSoloPreviewCapture.RejectLinks(folder); Require(!Directory.Exists(folder) && !File.Exists(folder), "Use a fresh capture folder.");
            Directory.CreateDirectory(folder); var errors = new List<string>(); object frame = null;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                using var stage = new WordboundSoloPreviewStage(content.Course, content.Geometry, content.Items, launch.VisualProfile, pose, "Toybox Arena");
                stage.RequireCameraOutsideSurface();
                frame = WordboundSoloPreviewCapture.Capture(stage.Preview, folder, ImageName, pose.Module, true,
                    content.Course.Hash, stage.Center + pose.Target);
            }
            catch (Exception error) { errors.Add(error.ToString()); }
            finally
            {
                void Clean(Action action) { try { action(); } catch (Exception error) { errors.Add(error.ToString()); } }
                Clean(() => RenderTexture.active = active); Clean(() => GL.sRGBWrite = srgb);
                Clean(() => EditorSettings.asyncShaderCompilation = async);
                Clean(() => { if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); });
                Clean(() => Verify(inputs));
                Clean(() => Require(source == WordboundSoloPreviewCapture.SourceFingerprint() && SceneState() == beforeScene &&
                    tactile == TactileMaterials.RegisteredScopeCount && RenderTexture.active == active && GL.sRGBWrite == srgb &&
                    EditorSettings.asyncShaderCompilation == async && Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION") == ssao,
                    "Source, scene, render state or owned tactile scopes were not preserved."));
            }
            Write(folder + "/receipt.json", JsonConvert.SerializeObject(new {
                schema = 1, kind = "wreckabulary.arena-preview", status = errors.Count == 0 ? "CAPTURED_NOT_VISUALLY_APPROVED" : "FAILED",
                utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, seed = Seed, mode = "FreeForAll", format = "Solo",
                courseId = content.Course.CopyDocument().Id, sourceHash = content.Course.SourceHash, manifestHash = content.Course.Hash,
                geometryHash = content.Geometry.Hash, contentHash = content.PublicBinding.CatalogHash, sourceFingerprint = source,
                width = WordboundSoloPreviewCapture.Width, height = WordboundSoloPreviewCapture.Height,
                scope = "Actual installed arena static geometry and prop artwork, original profile and fixed approved seed. No gameplay actors, spawned supply tiles, input or match result is proven.",
                inputs, frame, errors
            }, Formatting.Indented));
            Require(errors.Count == 0, "Arena capture failed; retained receipt: " + folder + "/receipt.json");
            Debug.Log("[Arena preview] Review the unchanged image before ImportReviewed: " + folder + "/receipt.json");
        }

        internal static WordboundClientContent Content(GameData data, WordboundLaunchAssets launch)
        {
            var catalog = WordboundPublicCourseCatalog.Parse(launch.PublicCoursesJson);
            catalog.RequireRelease(launch.MapPoolVersion);
            var content = catalog.ResolveSelection(data.items.text, data.rules.text, "FreeForAll", "Solo", Seed);
            Require(content.Course.CopyDocument().Id == "arena-proof", "The selected public entry is not the installed arena.");
            launch.VisualProfile.Validate(); return content;
        }
        internal static Dictionary<string, string> Inputs(GameData data, WordboundLaunchAssets launch)
        {
            var values = new UnityEngine.Object[] { data, data.items, data.rules, launch, launch.VisualProfile,
                launch.ApprovedPublicCourses, ModelLibrary.Load(), MaterialLibrary.Load(), GraphicsSettings.currentRenderPipeline,
                AssetDatabase.LoadMainAssetAtPath(LayoutPath) };
            var roots = values.Where(v => v).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).ToArray();
            WordboundSoloPreviewCapture.RequireCleanDependencies(roots);
            var paths = AssetDatabase.GetDependencies(roots, true).Concat(new[] {
                "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundArenaPreviewCapture.cs",
                "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundArenaPreviewImport.cs",
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" })
                .Where(File.Exists).SelectMany(p => File.Exists(p + ".meta") ? new[] { p, p + ".meta" } : new[] { p })
                .Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Require(paths.Length > 0 && paths.Length <= 8192, "Arena input roster exceeded its bound.");
            return paths.ToDictionary(p => p, WordboundSoloPreviewCapture.FileHash, StringComparer.Ordinal);
        }
        internal static void Verify(Dictionary<string, string> inputs, bool permitLayoutChange = false)
        {
            foreach (var input in inputs)
                if (!permitLayoutChange || input.Key != LayoutPath)
                    Require(File.Exists(input.Key) && WordboundSoloPreviewCapture.FileHash(input.Key) == input.Value, "Arena input changed: " + input.Key);
            WordboundSoloPreviewCapture.RequireCleanDependencies(inputs.Keys);
        }
        internal static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()); }));
        internal static void Write(string path, string text) => WordboundSoloPreviewCapture.WriteNew(path, Encoding.UTF8.GetBytes(text));
        internal static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    }
}
