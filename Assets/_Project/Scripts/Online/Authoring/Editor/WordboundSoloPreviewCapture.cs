using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Editor
{
    public static class WordboundSoloPreviewCapture
    {
        public const string CaptureRoot = "BuildNotes/visual-coherence-20261029/map-previews/captures";
        internal const int Width = 960, Height = 540;
        const string ScriptRoot = "Assets/_Project/Scripts/Online/Authoring/Editor/";
        sealed class Input
        {
            public string path, before, after, memoryBefore, memoryAfter;
            public string dependencyBefore = "", dependencyAfter = "";
            public bool dirtyBefore, dirtyAfter;
            [JsonIgnore] public Object value;
        }

        [MenuItem("Tools/Wordbound/Presentation/Create or select Solo preview settings")]
        public static void CreateOrSelectSettings()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Create preview settings outside Play mode.");
            var existing = AssetDatabase.LoadMainAssetAtPath(WordboundSoloPreviewSettings.Path);
            Require(!existing || existing is WordboundSoloPreviewSettings, "The settings path is occupied by another asset.");
            var settings = existing as WordboundSoloPreviewSettings;
            if (!settings)
            {
                Require(!File.Exists(WordboundSoloPreviewSettings.Path) && !File.Exists(WordboundSoloPreviewSettings.Path + ".meta"), "Settings file exists but has not imported.");
                EnsureFolder("Assets/_Project/Data/Wordbound/Authoring/Previews");
                settings = ScriptableObject.CreateInstance<WordboundSoloPreviewSettings>(); settings.Validate();
                AssetDatabase.CreateAsset(settings, WordboundSoloPreviewSettings.Path); AssetDatabase.SaveAssetIfDirty(settings);
            }
            settings.Validate(); Selection.activeObject = settings; EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Wordbound/Presentation/Use closer Solo sample framing")]
        public static void UseCloserSampleFraming()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen,
                "Close the framing window and leave Play mode before changing sample views.");
            var settings = AssetDatabase.LoadAssetAtPath<WordboundSoloPreviewSettings>(WordboundSoloPreviewSettings.Path);
            Require(settings && !EditorUtility.IsDirty(settings), "Create and save preview settings first.");
            Undo.RecordObject(settings, "Use closer Solo sample framing");
            settings.LostAddress = new WordboundSoloPreviewPose { Module = "Packing_LabelBench",
                Position = new Vector3(0, 2.2f, -10), Target = new Vector3(7, 1, -5), FieldOfView = 50 };
            settings.ParcelHall = new WordboundSoloPreviewPose { Module = "Packing_ParcelGate",
                Position = new Vector3(-1, 2.2f, -5), Target = new Vector3(-5.5f, .9f, -9), FieldOfView = 50 };
            settings.Validate(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Selection.activeObject = settings;
        }

        [MenuItem("Tools/Wordbound/Presentation/Capture two Solo draft previews")]
        public static void CaptureDrafts() => CaptureMaps(new[] { WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap });

        [MenuItem("Tools/Wordbound/Presentation/Capture Parcel Service draft preview")]
        public static void CaptureParcelServiceDraft() => CaptureMaps(new[] { WordboundSoloEntry.ParcelServiceMap });

        static void CaptureMaps(string[] maps)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode && !WordboundSoloPreviewWindow.IsOpen, "Close the framing window and leave Play mode before capture.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && GraphicsSettings.currentRenderPipeline, "Capture requires the current renderer and a graphics device.");
            var settings = AssetDatabase.LoadAssetAtPath<WordboundSoloPreviewSettings>(WordboundSoloPreviewSettings.Path);
            Require(settings && !EditorUtility.IsDirty(settings), "Create and save Solo preview settings first."); settings.Validate();
            var inputs = Inputs(settings);
            Require(inputs.All(i => !i.dirtyBefore), "Save the inspected source inputs before capture.");
            RequireCleanDependencies(inputs.Select(i => i.path));
            string sourceFingerprint = SourceFingerprint();
            string scenes = SceneState(); var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            bool async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            int tactile = TactileMaterials.RegisteredScopeCount;
            string output = FreshOutput(); var frames = new List<object>(); var sets = new List<object>();
            var errors = new List<string>(); Exception failure = null;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                foreach (string map in maps)
                {
                    using var stage = new WordboundSoloPreviewStage(settings, map);
                    stage.RequireCameraOutsideSurface();
                    sets.Add(new { map, seed = settings.Seed, mode = settings.Mode, sourceHash = stage.Content.Course.SourceHash,
                        manifestHash = stage.Content.Course.Hash, geometryHash = stage.Content.Geometry.Hash,
                        catalogueHash = stage.Content.CatalogueHash, propVisuals = stage.Content.Course.Placements.Count,
                        source = "WordboundLauncher.CreateSoloContent", pose = new { settings.Pose(map).Module,
                            Position = V(settings.Pose(map).Position), Target = V(settings.Pose(map).Target), settings.Pose(map).FieldOfView },
                        lights = stage.Preview.lights.Select(l => new { l.intensity, rotation = V(l.transform.eulerAngles) }).ToArray() });
                    frames.Add(Capture(stage.Preview, output, map + ".png", settings.Pose(map).Module, true,
                        stage.Content.Course.Hash, stage.Center + settings.Pose(map).Target));
                }
                Require(frames.Count == maps.Length, "Expected every requested map preview.");
            }
            catch (Exception e) { failure = e; errors.Add(e.ToString()); }
            finally
            {
                Clean(() => RenderTexture.active = active, errors); Clean(() => GL.sRGBWrite = srgb, errors);
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => { if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }, errors);
                foreach (var input in inputs) Clean(() => {
                    input.after = File.Exists(input.path) ? FileHash(input.path) : "MISSING";
                    input.dependencyAfter = input.dependencyBefore.Length == 0 ? "" : AssetDatabase.GetAssetDependencyHash(input.path).ToString();
                    input.memoryAfter = input.value ? EditorJsonUtility.ToJson(input.value) : "MISSING";
                    input.dirtyAfter = input.value && EditorUtility.IsDirty(input.value);
                }, errors);
                bool filesPreserved = inputs.All(i => i.before == i.after && i.dependencyBefore == i.dependencyAfter);
                bool memoryPreserved = inputs.All(i => i.memoryBefore == i.memoryAfter && i.dirtyBefore == i.dirtyAfter);
                bool scenesPreserved = scenes == SceneState();
                bool globalsPreserved = active == RenderTexture.active && srgb == GL.sRGBWrite && async == EditorSettings.asyncShaderCompilation && ssao == Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                bool tactileReleased = tactile == TactileMaterials.RegisteredScopeCount;
                bool sourcePreserved = false;
                Clean(() => sourcePreserved = sourceFingerprint == SourceFingerprint(), errors);
                Clean(() => RequireCleanDependencies(inputs.Select(i => i.path)), errors);
                if (!filesPreserved || !memoryPreserved || !scenesPreserved || !globalsPreserved || !tactileReleased || !sourcePreserved) errors.Add("Input, source, scene, render-state or owned tactile preservation failed.");
                var receipt = new { schema = 1, status = errors.Count == 0 ? "CAPTURED_NOT_VISUALLY_APPROVED" : "FAILED",
                    utc = DateTime.UtcNow.ToString("O"), seed = settings.Seed, mode = settings.Mode, width = Width, height = Height,
                    unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName,
                    scope = "Actual installed Solo content at a recorded sample seed. Static world and original prop visuals; no player, live puzzles, letter supplies, Windows, multiplayer or visual-acceptance claim.",
                    filesPreserved, memoryPreserved, scenesPreserved, globalsPreserved, tactileReleased, sourcePreserved, sourceFingerprint, inputFacts = inputs, sets, frames, errors };
                WriteNew(Path.Combine(output, "receipt.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(receipt, Formatting.Indented)));
                Debug.Log("WORDBOUND_SOLO_PREVIEW_DRAFTS: " + output);
            }
            if (failure != null || errors.Count != 0) throw new InvalidOperationException("Preview capture failed; inspect its retained receipt: " + output, failure);
        }

        internal static string[] CurrentInputPaths(WordboundSoloPreviewSettings settings) =>
            Inputs(settings).Select(i => i.path).OrderBy(p => p, StringComparer.Ordinal).ToArray();

        static List<Input> Inputs(WordboundSoloPreviewSettings settings)
        {
            var launch = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            var style = Resources.Load<WordboundRoomStyleProfile>(WordboundRoomStyleProfile.ResourcePath);
            Require(launch && launch.VisualProfile && style && data && data.items && data.rules, "Installed content or visual profiles are missing.");
            var authored = launch.ParcelServiceCourse;
            var values = new Object[] { settings, launch, launch.VisualProfile, authored, authored ? authored.Items : null,
                authored ? authored.RuleBook : null, authored ? authored.VisualProfile : null,
                style, style.WallArtwork, data, data.items, data.rules,
                ModelLibrary.Load(), MaterialLibrary.Load(), GraphicsSettings.currentRenderPipeline };
            var paths = values.Where(v => v).Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p)).Concat(new[] {
                ScriptRoot + "WordboundSoloPreviewCapture.cs", ScriptRoot + "WordboundSoloPreviewStage.cs", ScriptRoot + "WordboundSoloPreviewSettings.cs",
                ScriptRoot + "WordboundSoloPreviewWindow.cs", ScriptRoot + "WordboundSoloPreviewImport.cs",
                "Assets/_Project/Scripts/Online/Client/WordboundLauncherSolo.cs", "Assets/_Project/Scripts/Rules/Wordbound/WordboundArrivalVariant.cs",
                "Assets/_Project/Scripts/Rules/Wordbound/WordboundFlagshipContent.cs", "Assets/_Project/Scripts/WordboundRuntime/WordboundCourseWorld.cs",
                "Assets/_Project/Scripts/Online/Presentation/WordboundVisualResolver.cs",
                "Assets/_Project/Scripts/WordboundRuntime/WordboundRoomArchitecture.cs", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset"
            }).Distinct(StringComparer.Ordinal);
            var result = new List<Input>();
            foreach (string path in paths)
            {
                Require(File.Exists(path), "Required preview input missing: " + path);
                var value = values.FirstOrDefault(v => v && AssetDatabase.GetAssetPath(v) == path);
                result.Add(new Input { path = path, before = FileHash(path), value = value,
                    dependencyBefore = path.StartsWith("Assets/", StringComparison.Ordinal) ? AssetDatabase.GetAssetDependencyHash(path).ToString() : "",
                    memoryBefore = value ? EditorJsonUtility.ToJson(value) : "MISSING", dirtyBefore = value && EditorUtility.IsDirty(value) });
                if (File.Exists(path + ".meta")) result.Add(new Input { path = path + ".meta", before = FileHash(path + ".meta"), memoryBefore = "MISSING" });
            }
            return result;
        }
        static string FreshOutput()
        {
            string name = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string root = Path.GetFullPath(CaptureRoot); RejectLinks(root);
            string output = Path.Combine(root, name);
            Require(!Directory.Exists(output) && !File.Exists(output), "Capture output already exists.");
            Directory.CreateDirectory(output); return output;
        }
        internal static void RejectLinks(string path)
        {
            for (var d = new DirectoryInfo(path); d != null; d = d.Parent)
                if (d.Exists) Require((d.Attributes & FileAttributes.ReparsePoint) == 0, "Preview output cannot traverse reparse points.");
        }
        internal static string SourceFingerprint()
        {
            var roots = new[] { "Assets/_Project/Scripts/Art", "Assets/_Project/Scripts/Core", "Assets/_Project/Scripts/Rules",
                "Assets/_Project/Scripts/WordboundRuntime", "Assets/_Project/Scripts/Online/Presentation", "Assets/_Project/Scripts/Online/Puzzles",
                "Assets/_Project/Scripts/Online/Client", "Assets/_Project/Scripts/Online/Authoring/Editor" };
            var paths = roots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Select(path => path.Replace('\\', '/')).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            Require(paths.Length > 0 && paths.Length <= 512, "Preview source fingerprint exceeded its bounded roster.");
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n", paths.Select(path => path + ":" + FileHash(path)))));
        }
        internal static void RequireCleanDependencies(IEnumerable<string> inputPaths)
        {
            var roots = inputPaths.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !p.EndsWith(".meta", StringComparison.Ordinal)).Distinct().ToArray();
            var dependencies = AssetDatabase.GetDependencies(roots, true).Distinct(StringComparer.Ordinal).ToArray();
            Require(dependencies.Length <= 4096, "Preview dependency roster exceeded its bounded size.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Require(stage == null || !stage.scene.isDirty || !dependencies.Contains(stage.assetPath), "Save the dependent prefab being edited before capture/import.");
            foreach (string path in dependencies)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path); var importer = AssetImporter.GetAtPath(path);
                bool packageShader = asset is Shader && path.StartsWith("Packages/", StringComparison.Ordinal);
                Require((!asset || packageShader || !EditorUtility.IsDirty(asset)) && (!importer || !EditorUtility.IsDirty(importer)),
                    "Save or discard pending source material, texture, prefab or import changes before capture/import: " + path);
            }
        }
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Require(!File.Exists(path) && !File.Exists(path + ".meta") && !Directory.Exists(path), "Asset folder is occupied: " + path);
            int split = path.LastIndexOf('/'); Require(split > 0, "Expected an Assets folder path.");
            EnsureFolder(path.Substring(0, split));
            Require(!string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1))), "Cannot create preview asset folder.");
        }
        static object Capture(PreviewRenderUtility preview, string output, string filename, string module, bool styled, string hash, Vector3 target)
        {
            Texture2D pixels = null; bool begun = false;
            try
            {
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height)); begun = true;
                preview.Render(true); preview.Render(true);
                pixels = preview.EndStaticPreview(); begun = false;
                Require(pixels && pixels.width == Width && pixels.height == Height, "Native frame size differs.");
                var colors = pixels.GetPixels32(); var distinct = new HashSet<int>(); int nonblack = 0, opaque = 0;
                foreach (var c in colors)
                {
                    if (c.r + c.g + c.b > 6) nonblack++; if (c.a > 0) opaque++;
                    if (distinct.Count < 4096) distinct.Add(c.r << 16 | c.g << 8 | c.b);
                }
                Require(nonblack > colors.Length / 20 && opaque > colors.Length / 20 && distinct.Count > 64,
                    "Completed render is blank or insufficiently varied; no automatic retry.");
                byte[] bytes = pixels.EncodeToPNG(); WriteNew(Path.Combine(output, filename), bytes);
                return new { filename, sha256 = Hash(bytes), bytes = bytes.Length, module, styled, manifestHash = hash,
                    cameraPosition = V(preview.camera.transform.position), cameraEuler = V(preview.camera.transform.eulerAngles), target = V(target),
                    width = Width, height = Height, nonblack, opaque, distinctRgbCapped4096 = distinct.Count, nativeRenderCalls = 2,
                    windowsGameplay = false };
            }
            finally
            {
                if (begun) { var abandoned = preview.EndStaticPreview(); if (abandoned) Object.DestroyImmediate(abandoned); }
                if (pixels) Object.DestroyImmediate(pixels);
            }
        }

        static bool Contains(BoxCollider box, Vector3 point)
        {
            var p = box.transform.InverseTransformPoint(point) - box.center; var h = box.size * .5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static object AssetFact(Object value)
        {
            if (!value) return null; string path = AssetDatabase.GetAssetPath(value);
            return new { path, guid = AssetDatabase.AssetPathToGUID(path), dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() };
        }
        static Vector3 Metres(CoursePoint p) => new Vector3(p.X, p.Y, p.Z) / 1000f;
        static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
        internal static string FileHash(string path) => Hash(File.ReadAllBytes(path));
        internal static string Hash(byte[] data) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        internal static void WriteNew(string path, byte[] bytes) { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes, 0, bytes.Length); }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Clean(Action action, List<string> errors) { try { action(); } catch (Exception error) { errors.Add(error.ToString()); } }
    }
}
