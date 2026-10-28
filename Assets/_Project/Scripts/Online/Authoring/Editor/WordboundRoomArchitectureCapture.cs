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
    public static class WordboundRoomArchitectureCapture
    {
        const int Width = 1280, Height = 720, ExpectedFrames = 14;
        const string ScriptPath = "Assets/_Project/Scripts/Online/Authoring/Editor/WordboundRoomArchitectureCapture.cs";
        const string StylePath = "Assets/_Project/Resources/Wordbound/RoomArchitecture.asset";
        const string VariantPath = WordboundArrivalVariantSetup.Path;
        const string Seed = "0123456789abcdef";

        sealed class Input
        {
            public string path, before, after, memoryBefore, memoryAfter;
            public string dependencyBefore = "", dependencyAfter = "";
            public bool dirtyBefore, dirtyAfter;
            [JsonIgnore] public Object value;
        }
        sealed class View
        {
            public string Name, Module;
            public Vector3 CameraOffset, TargetOffset;
            public View(string name, string module, bool reverse = false)
            {
                Name = name; Module = module;
                CameraOffset = reverse ? new Vector3(7.5f, 2.4f, 8.5f) : new Vector3(-7.5f, 2.4f, -8.5f);
                TargetOffset = reverse ? new Vector3(-2, 1.7f, -1) : new Vector3(2, 1.7f, 1);
            }
        }

        [MenuItem("Tools/Wordbound/Presentation/Capture room architecture comparison")]
        public static void Run()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Capture outside Play Mode.");
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is required; omit -nographics.");
            Require(GraphicsSettings.currentRenderPipeline, "Use the current project render pipeline.");
            var original = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            var variant = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(VariantPath);
            var style = AssetDatabase.LoadAssetAtPath<WordboundRoomStyleProfile>(StylePath);
            Require(original && variant && style && style.Enabled, "Create the saved room architecture and furnished Parcel Hall authoring assets first.");
            Require(original.ReadDocument().Id == WordboundFlagshipContent.CourseId &&
                variant.ReadDocument().Id == WordboundArrivalVariant.CourseId, "Expected original Lost Address and its separately saved Parcel Hall variant.");
            style.Validate();
            Require(original.VisualProfile && variant.VisualProfile &&
                original.VisualProfile.BaseArtwork == WordboundBaseArtwork.OriginalWreckabulary &&
                variant.VisualProfile.BaseArtwork == WordboundBaseArtwork.OriginalWreckabulary, "Use the currently assigned original Wreckabulary artwork profiles.");
            var inputs = Inputs(original, variant, style);
            string scenesBefore = SceneState(); var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            bool async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            int tactileBefore = TactileMaterials.RegisteredScopeCount;
            string output = FreshOutput(); var frames = new List<object>(); var sets = new List<object>();
            var errors = new List<string>(); Exception failure = null;
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                var destinations = new[] {
                    new View("packing-entry", "Packing_ParcelGate"), new View("packing-return", "Packing_ParcelGate", true),
                    new View("kitchen", "Kitchen_CutlerySort"), new View("attic", "Attic_LostLabels"),
                    new View("rooftop", "Rooftop_PottingSort") };
                var arrival = new[] { new View("arrival-entry", WordboundArrivalVariant.EntryModuleId),
                    new View("arrival-return", WordboundArrivalVariant.EntryModuleId, true) };
                foreach (bool enabled in new[] { false, true })
                {
                    CaptureSet(original, style, enabled, destinations, output, frames, sets);
                    CaptureSet(variant, style, enabled, arrival, output, frames, sets);
                }
                Require(frames.Count == ExpectedFrames, "Expected exactly fourteen comparison frames.");
            }
            catch (Exception error) { failure = error; errors.Add(error.ToString()); }
            finally
            {
                Clean(() => RenderTexture.active = active, errors); Clean(() => GL.sRGBWrite = srgb, errors);
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => { if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }, errors);
                foreach (var input in inputs)
                    Clean(() => {
                        input.after = File.Exists(input.path) ? FileHash(input.path) : "MISSING";
                        input.dependencyAfter = input.dependencyBefore.Length == 0 ? "" : AssetDatabase.GetAssetDependencyHash(input.path).ToString();
                        input.memoryAfter = input.value ? EditorJsonUtility.ToJson(input.value) : "MISSING";
                        input.dirtyAfter = input.value && EditorUtility.IsDirty(input.value);
                    }, errors);
                bool filesPreserved = inputs.All(i => i.before == i.after && i.dependencyBefore == i.dependencyAfter);
                bool memoryPreserved = inputs.All(i => i.memoryBefore == i.memoryAfter && i.dirtyBefore == i.dirtyAfter);
                bool scenesPreserved = scenesBefore == SceneState();
                bool globalsPreserved = active == RenderTexture.active && srgb == GL.sRGBWrite && async == EditorSettings.asyncShaderCompilation &&
                    ssao == Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                bool tactileReleased = TactileMaterials.RegisteredScopeCount == tactileBefore;
                if (!filesPreserved || !memoryPreserved || !scenesPreserved || !globalsPreserved || !tactileReleased)
                    errors.Add("Saved input, scene, global or owned tactile-scope preservation failed.");
                var receipt = new {
                    schema = 1, status = errors.Count == 0 ? "CAPTURED_NOT_VISUALLY_APPROVED" : "FAILED",
                    utc = DateTime.UtcNow.ToString("O"), seed = Seed, width = Width, height = Height, expectedFrames = ExpectedFrames,
                    unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                    colorSpace = QualitySettings.activeColorSpace.ToString(), pipeline = AssetFact(GraphicsSettings.currentRenderPipeline),
                    scope = "Isolated static Editor preview using actual saved course, canonical world renderer and original prop presentation. " +
                        "No live player, puzzle simulation, letter supply visuals, UI, Windows frame-time, multiplayer or gameplay proof.",
                    comparison = "Identical exact seed, input course, camera and neutral preview lights; architecture disabled versus enabled. " +
                        "The furnished variant is a separate exact document, compared with itself for material/architecture changes.",
                    filesPreserved, memoryPreserved, scenesPreserved, globalsPreserved, tactileReleased,
                    inputFacts = inputs, sets, frames, errors
                };
                WriteNew(Path.Combine(output, "receipt.json"), Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(receipt, Formatting.Indented)));
                Debug.Log("WORDBOUND_ROOM_CAPTURE_" + (errors.Count == 0 ? "COMPLETE" : "FAILED") + ": " + output);
            }
            if (failure != null) throw new InvalidOperationException("Room comparison failed; retained its receipt and any completed frames in " + output, failure);
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        static void CaptureSet(WordboundCourseAsset source, WordboundRoomStyleProfile style, bool styled, View[] views,
            string output, List<object> frames, List<object> sets)
        {
            WordboundCourseAsset copy = null; PreviewRenderUtility preview = null; WordboundCourseWorld world = null;
            GameObject stage = null; Scene ownedScene = default;
            var visuals = new List<WordboundVisualInstance>(); var cleanup = new List<string>();
            Exception failure = null;
            try
            {
                copy = Object.Instantiate(source); copy.hideFlags = HideFlags.HideAndDontSave;
                copy.Seed = Seed; copy.ModeId = "TimeRun"; copy.Format = RaceFormat.Solo;
                var exact = copy.Resolve(); var content = copy.CreateContent(exact);
                preview = new PreviewRenderUtility(); ownedScene = preview.camera.gameObject.scene;
                Require(EditorSceneManager.IsPreviewScene(ownedScene), "Expected an owned preview scene.");
                stage = EditorUtility.CreateGameObjectWithHideFlags("Room architecture comparison", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(stage, ownedScene);
                world = WordboundCourseWorld.Build(exact, content.Geometry, stage.transform,
                    new WordboundCourseWorldOptions { EnableRoomArchitecture = styled, RoomStyle = style });
                var resolver = new WordboundVisualResolver(content.Items, profile: copy.VisualProfile);
                foreach (var placement in exact.Placements)
                {
                    var definition = content.Geometry.Assets[placement.AssetId];
                    var anchor = new GameObject("Preview prop " + placement.Id).transform;
                    anchor.SetParent(world.transform, false); anchor.localPosition = Metres(placement.Position);
                    anchor.localRotation = Quaternion.Euler(0, placement.Yaw, 0);
                    visuals.Add(resolver.Create(placement.AssetId,
                        new Vector3(definition.WidthMm, definition.HeightMm, definition.DepthMm) / 1000f, anchor));
                }
                foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var camera = preview.camera;
                camera.fieldOfView = source.CameraProfile ? source.CameraProfile.FieldOfView : 55;
                camera.aspect = (float)Width / Height; camera.nearClipPlane = .08f; camera.farClipPlane = 180;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f, .19f, .22f);
                camera.allowHDR = false; camera.allowMSAA = true;
                preview.lights[0].intensity = 1.2f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                preview.lights[1].intensity = .7f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 220, 0);
                foreach (var light in preview.lights) { light.color = Color.white; light.shadows = LightShadows.None; }
                preview.ambientColor = Color.gray;
                var architecture = world.GetComponent<WordboundRoomArchitecture>();
                sets.Add(new { courseAsset = AssetFact(source), manifestHash = exact.Hash, sourceHash = exact.SourceHash,
                    geometryHash = content.Geometry.Hash, styled, propVisuals = visuals.Count,
                    colliders = world.Surfaces.Count, roomInsets = architecture ? architecture.InsetCount : 0,
                    wallRenderBatches = architecture ? architecture.RenderBatchCount : 0,
                    rendererCount = world.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled),
                    fieldOfView = camera.fieldOfView, near = camera.nearClipPlane, far = camera.farClipPlane,
                    lights = preview.lights.Select(l => new { l.intensity, rotation = V(l.transform.eulerAngles), shadows = l.shadows.ToString() }).ToArray(),
                    ambient = new[] { .5f, .5f, .5f }, lighting = "Neutral matched Editor preview lights; no saved scene lighting changes." });
                foreach (var view in views)
                {
                    var module = exact.Geometry.Single(m => m.Id == view.Module); Vector3 center = Metres(module.Position);
                    Vector3 position = center + view.CameraOffset, target = center + view.TargetOffset;
                    Require(!world.Surfaces.Cast<BoxCollider>().Any(c => Contains(c, position)), "Review camera cannot be inside a canonical surface.");
                    camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
                    frames.Add(Capture(preview, output, view.Name + (styled ? "-styled.png" : "-baseline.png"),
                        view.Module, styled, exact.Hash, target));
                }
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                Clean(() => { if (stage) TactileMaterials.Release(stage); }, cleanup);
                foreach (var visual in visuals) Clean(visual.Dispose, cleanup);
                Clean(() => { if (world) world.Dispose(); }, cleanup);
                Clean(() => { if (stage) Object.DestroyImmediate(stage); }, cleanup);
                Clean(() => preview?.Cleanup(), cleanup);
                Clean(() => { if (ownedScene.IsValid() && ownedScene.isLoaded && EditorSceneManager.IsPreviewScene(ownedScene)) EditorSceneManager.ClosePreviewScene(ownedScene); }, cleanup);
                Clean(() => { if (copy) Object.DestroyImmediate(copy); }, cleanup);
                if (cleanup.Count != 0) throw new InvalidOperationException("Preview cleanup failed: " + string.Join("\n", cleanup), failure);
            }
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

        static List<Input> Inputs(WordboundCourseAsset original, WordboundCourseAsset variant, WordboundRoomStyleProfile style)
        {
            var values = new Object[] { original, variant, style, original.VisualProfile, variant.VisualProfile,
                original.CameraProfile, variant.CameraProfile, original.Items, original.RuleBook, variant.Items, variant.RuleBook,
                style.WallArtwork, ModelLibrary.Load(), MaterialLibrary.Load(), GraphicsSettings.currentRenderPipeline };
            var paths = values.Where(v => v).Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p))
                .Concat(new[] { ScriptPath, "Assets/_Project/Scripts/WordboundRuntime/WordboundCourseWorld.cs",
                    "Assets/_Project/Scripts/WordboundRuntime/WordboundRoomArchitecture.cs",
                    "Assets/_Project/Scripts/WordboundRuntime/WordboundRoomStyleProfile.cs",
                    "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" }).Distinct(StringComparer.Ordinal).ToArray();
            var result = new List<Input>();
            foreach (string path in paths)
            {
                Require(File.Exists(path), "Required input missing: " + path);
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
            string name = Environment.GetEnvironmentVariable("WORDBOUND_MAP_CAPTURE_NAME");
            if (string.IsNullOrEmpty(name)) name = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Require(Regex.IsMatch(name, "^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$"), "Use a simple fresh capture folder label.");
            string parent = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuildNotes/redesign-20261028/map-capture/captures"));
            for (var directory = new DirectoryInfo(parent); directory != null; directory = directory.Parent)
                if (directory.Exists) Require((directory.Attributes & FileAttributes.ReparsePoint) == 0, "Capture output cannot traverse reparse points.");
            string output = Path.Combine(parent, name);
            Require(!Directory.Exists(output) && !File.Exists(output), "Output already exists; choose a fresh label.");
            Directory.CreateDirectory(output); return output;
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
        static string FileHash(string path) => Hash(File.ReadAllBytes(path));
        static string Hash(byte[] data) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        static void WriteNew(string path, byte[] bytes) { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes, 0, bytes.Length); }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Clean(Action action, List<string> errors) { try { action(); } catch (Exception error) { errors.Add(error.ToString()); } }
    }
}
