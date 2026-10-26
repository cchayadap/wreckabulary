using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class ShopPreviewImporter
    {
        const string Folder = "Assets/_Project/Resources/UI/Shop/Looks";
        const string Evidence = "BuildNotes/developer-workspace/shop-portraits-corrected";
        const string ScriptPath = "Assets/_Project/Editor/Art/ShopPreviewImporter.cs";
        const int Width = 256, Height = 320;
        static readonly string[] Ids = { "sunroom", "courier", "candy", "arcade", "lantern", "winter" };

        [Serializable] sealed class FileProof { public string path, before, after; public bool unchanged; }
        [Serializable] sealed class MemoryProof
        {
            public string path, name, before, after; public bool dirtyBefore, dirtyAfter, unchanged;
            [NonSerialized] public Object source;
        }
        [Serializable] sealed class LightState { public bool enabled; public Color color; public float intensity; public Quaternion rotation; }
        [Serializable] sealed class Lighting { public Color ambient; public LightState[] lights; }
        [Serializable] sealed class Portrait
        {
            public string control, outfit;
            public string id, name, previousPng, previousMeta, previousGuid, previousImporter;
            public string stagedPng, installedPng, installedMeta, installedGuid, installedImporter, error = "";
            public string originalHeadPath, correctedHeadPath, sourceFingerprint, imageGraphicsFormat, imageTextureFormat;
            public bool exactHeadCorrection, originalMeshIdentity, previewClosed, imageIsDataSRGB, installed;
            public bool ssaoBeforeRender, ssaoAfterRender;
            public string nativeTargetFormat, rgbaFormat, rgbaSha256;
            public bool nativeTargetSrgb, rgbaIsDataSrgb, alphaVerified;
            public int alphaZero, alphaOpaque, alphaPartial, borderNonzeroAlpha, rgbMaxDelta, rgbChangedPixels;
            public int bytes, distinctRgb, remainingOwnedObjects, importedWidth, importedHeight;
            public Bounds bounds; public Matrix4x4 camera; public float orthographicSize, near, far; public Color background;
            public Lighting beforeBegin, afterBegin, afterRender;
            public string[] visibleRenderers, materials, shapeNames; public float[] shapeWeights;
        }
        [Serializable] sealed class Report
        {
            public string utc, unity, graphics, colorSpace, status = "IN_PROGRESS", error = "", folder, globalBefore, globalAfter;
            public string scope = "Six existing shop looks through ModelVisual.Spawn and the saved exact head correction; original Idle, wardrobe, materials, camera, numerical lights and framing. Existing target PNG/meta backed up before rendering. Original source files/meta and memory preserved. Sequential target-only publication after staging; art acceptance remains pending.";
            public bool sourcesAndGlobalPreserved; public int width = Width, height = Height;
            public string[] excludedNonFileDependencies;
            public List<FileProof> files = new List<FileProof>(); public List<MemoryProof> memory = new List<MemoryProof>();
            public List<Portrait> portraits = new List<Portrait>();
        }
        [Serializable] sealed class GlobalState
        {
            public string active, scenes, skybox, sun, pipeline, renderTexture, ambientMode; public bool asyncShaders, ssao, fog, srgbWrite;
            public Color ambient, sky, equator, ground, fogColor; public float ambientIntensity, reflectionIntensity, fogDensity;
        }

        [MenuItem("Wreckabulary/Art/Rebuild shop portraits")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Rebuild in Edit Mode with the active render pipeline and a graphics device.");
            var profile = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            var library = ModelLibrary.Load(); var avatar = library ? library.Find("Avatar/Avatar") : null;
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            var materialLibrary = MaterialLibrary.Load(); var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!avatar || !idle || !materialLibrary || !data || !CosmeticBundles.All.Select(look => look.Id).SequenceEqual(Ids))
                throw new InvalidOperationException("The original avatar, Idle, material library, game data and exact six shop looks are required.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName, colorSpace = QualitySettings.activeColorSpace.ToString(), globalBefore = Global(),
                folder = Evidence + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) };
            foreach (var look in CosmeticBundles.All)
            {
                string path = Target(look.Id);
                if (!File.Exists(Full(path)) || !File.Exists(Full(path + ".meta")) || AssetImporter.GetAtPath(path) is not TextureImporter importer)
                    throw new IOException("This bounded rebuild requires the existing portrait and metadata: " + path);
                report.portraits.Add(new Portrait { id = look.Id, name = look.Name, previousPng = FileHash(path), previousMeta = FileHash(path + ".meta"),
                    previousGuid = AssetDatabase.AssetPathToGUID(path), previousImporter = EditorJsonUtility.ToJson(importer) });
            }
            TrackSources(report, avatar, idle, library, materialLibrary, data, profile);
            string folder = Full(report.folder);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing portrait evidence: " + folder);
            Directory.CreateDirectory(Path.Combine(folder, "Previous")); Directory.CreateDirectory(Path.Combine(folder, "Rendered"));
            bool asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            var active = RenderTexture.active;
            try
            {
                foreach (var portrait in report.portraits)
                {
                    RequireUnchangedTarget(portrait);
                    WriteNew(Path.Combine(folder, "Previous", portrait.id + ".png"), File.ReadAllBytes(Full(Target(portrait.id))));
                    WriteNew(Path.Combine(folder, "Previous", portrait.id + ".png.meta"), File.ReadAllBytes(Full(Target(portrait.id) + ".meta")));
                    if (FileHash(report.folder + "/Previous/" + portrait.id + ".png") != portrait.previousPng
                        || FileHash(report.folder + "/Previous/" + portrait.id + ".png.meta") != portrait.previousMeta)
                        throw new IOException("Portrait backup verification failed: " + portrait.id);
                }
                EditorSettings.asyncShaderCompilation = false;
                foreach (var portrait in report.portraits) Capture(portrait, folder, profile, idle);
                Restore(active, asyncShaders, ssao);
                VerifySources(report);
                if (!report.sourcesAndGlobalPreserved) throw new InvalidOperationException("Preservation failed before publication; staged images were retained.");
                foreach (var portrait in report.portraits) RequireUnchangedTarget(portrait);
                foreach (var portrait in report.portraits)
                {
                    RequireUnchangedTarget(portrait);
                    string path = Target(portrait.id);
                    byte[] bytes = File.ReadAllBytes(Path.Combine(folder, "Rendered", portrait.id + ".png"));
                    if (AvatarSurfaceMeshData.Hash(bytes) != portrait.stagedPng) throw new IOException("Staged image changed: " + portrait.id);
                    File.WriteAllBytes(Full(path), bytes);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (!importer) throw new IOException("Portrait texture importer missing after import: " + portrait.id);
                    importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false; importer.maxTextureSize = 512; importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                    var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    portrait.importedWidth = image ? image.width : 0; portrait.importedHeight = image ? image.height : 0;
                    portrait.installedPng = FileHash(path); portrait.installedMeta = FileHash(path + ".meta");
                    portrait.installedGuid = AssetDatabase.AssetPathToGUID(path); portrait.installedImporter = EditorJsonUtility.ToJson(importer);
                    portrait.installed = portrait.installedPng == portrait.stagedPng && portrait.installedGuid == portrait.previousGuid
                        && portrait.importedWidth == Width && portrait.importedHeight == Height;
                    if (!portrait.installed) throw new InvalidOperationException("Portrait publication verification failed: " + portrait.id);
                }
                report.status = "REBUILT_NOT_VISUALLY_APPROVED";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); }
            finally
            {
                try { Restore(active, asyncShaders, ssao); } catch (Exception error) { report.error += "\nRestore: " + error; }
                try { VerifySources(report); } catch (Exception error) { report.error += "\nPreservation: " + error; }
                foreach (var portrait in report.portraits)
                {
                    portrait.installedPng = FileHash(Target(portrait.id)); portrait.installedMeta = FileHash(Target(portrait.id) + ".meta");
                    portrait.installedGuid = AssetDatabase.AssetPathToGUID(Target(portrait.id));
                }
                if (!report.sourcesAndGlobalPreserved || report.error.Length != 0) report.status = "FAIL";
                WriteNew(Path.Combine(folder, "receipt.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            }
            if (report.status == "FAIL") throw new InvalidOperationException("Shop portrait rebuild failed; retain previous/staged images and receipt at " + folder + "\n" + report.error);
            Debug.Log("SHOP_PORTRAITS: count=6; source=original-avatar; saved_head_correction=true; width=256; height=320; receipt=" + report.folder + "/receipt.json; art acceptance pending.");
        }

        [MenuItem("Wreckabulary/Art/Compare corrected portrait lighting")]
        public static void CompareLighting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Compare in Edit Mode with the active render pipeline and a graphics device.");
            var profile = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            var library = ModelLibrary.Load(); var avatar = library ? library.Find("Avatar/Avatar") : null;
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            var materials = MaterialLibrary.Load(); var data = Resources.Load<GameData>(GameData.ResourcePath);
            if (!avatar || !idle || !materials || !data || CosmeticBundles.Find("courier") == null)
                throw new InvalidOperationException("The original courier portrait inputs are required.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName, colorSpace = QualitySettings.activeColorSpace.ToString(), globalBefore = Global(),
                folder = Evidence + "-lighting/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                scope = "Nonpublishing courier lighting comparison: current preview lighting, neutral front/fill 1.15/0.65, then the same neutral directions at 1.55/0.85. All use the saved corrected head, original materials/tints, Idle at zero, pose, camera and ambient. A to B is a lighting-preset comparison; B to C changes intensities only. Each frame also has native RGBA readback with alpha and RGB-transfer validation. No target portrait, metadata, original material, texture or scene is saved." };
            TrackSources(report, avatar, idle, library, materials, data, profile);
            foreach (string id in Ids)
                foreach (string target in new[] { Target(id), Target(id) + ".meta" })
                    report.files.Add(new FileProof { path = target, before = FileHash(target) });
            string folder = Full(report.folder);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing lighting evidence: " + folder);
            Directory.CreateDirectory(Path.Combine(folder, "Rendered"));
            var active = RenderTexture.active; bool asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                foreach (string control in new[] { "A-current", "B-neutral-front", "C-neutral-front-brighter" })
                {
                    var portrait = new Portrait { id = "courier", name = "Mint Messenger", control = control, previousPng = FileHash(Target("courier")) };
                    report.portraits.Add(portrait); Capture(portrait, folder, profile, idle);
                }
                report.status = "CAPTURED_NOT_VISUALLY_APPROVED";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); }
            finally
            {
                try { Restore(active, asyncShaders, ssao); } catch (Exception error) { report.error += "\nRestore: " + error; }
                try { VerifySources(report); } catch (Exception error) { report.error += "\nPreservation: " + error; }
                if (!report.sourcesAndGlobalPreserved || report.error.Length != 0) report.status = "FAIL";
                WriteNew(Path.Combine(folder, "receipt.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            }
            if (report.status == "FAIL") throw new InvalidOperationException("Lighting comparison failed; preserve " + folder + "\n" + report.error);
            Debug.Log("SHOP_PORTRAIT_LIGHTING: comparisons=3; published=0; preserved=true; receipt=" + report.folder + "/receipt.json; visual review pending.");
        }

        static void Capture(Portrait result, string folder, AvatarSurfaceProfile profile, AnimationClip idle)
        {
            PreviewRenderUtility preview = null; GameObject holder = null; Texture2D image = null, rgba = null; Scene scene = default; bool begun = false;
            var active = RenderTexture.active; bool ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene;
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected an owned preview scene.");
                holder = new GameObject("Shop portrait " + result.id); preview.AddSingleGO(holder);
                var model = ModelVisual.Spawn("Avatar/Avatar", holder.transform);
                if (!model || model.scene != scene) throw new InvalidOperationException("Avatar did not enter the owned preview scene.");
                var head = model.transform.Find(profile.RendererPath)?.GetComponent<SkinnedMeshRenderer>();
                result.exactHeadCorrection = head && head.sharedMesh == profile.CorrectedMesh
                    && profile.MatchesOnlyBoundHeadReplacement(profile.SourceAvatar, model, Meshes(profile.SourceAvatar), Meshes(model));
                result.originalMeshIdentity = Meshes(profile.SourceAvatar).SequenceEqual(Meshes(model));
                if (!result.exactHeadCorrection || result.originalMeshIdentity) throw new InvalidOperationException("Portrait did not use the exact saved head correction.");
                result.originalHeadPath = AssetDatabase.GetAssetPath(profile.SourceMesh); result.correctedHeadPath = AssetDatabase.GetAssetPath(head.sharedMesh);
                result.sourceFingerprint = profile.SourceFingerprint;
                idle.SampleAnimation(model, 0f);
                var outfit = CosmeticBundles.Find(result.id).Apply(GameConfig.Current.Wardrobe.Default, GameConfig.Current.Wardrobe);
                if (outfit == null) throw new InvalidOperationException("Invalid shop outfit: " + result.id);
                PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), outfit);
                result.outfit = outfit.Serialize();
                result.shapeNames = Enumerable.Range(0, head.sharedMesh.blendShapeCount).Select(head.sharedMesh.GetBlendShapeName).ToArray();
                result.shapeWeights = Enumerable.Range(0, head.sharedMesh.blendShapeCount).Select(head.GetBlendShapeWeight).ToArray();
                holder.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                var renderers = model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled && !renderer.forceRenderingOff).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No visible outfit geometry: " + result.id);
                if (renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material => !material || !material.shader || !material.shader.isSupported
                    || material.shader.name.Contains("InternalErrorShader"))) throw new InvalidOperationException("Invalid original portrait material or shader: " + result.id);
                var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (!AvatarSurfaceMeshData.Finite(bounds.center) || !AvatarSurfaceMeshData.Finite(bounds.size) || bounds.size.sqrMagnitude <= 0)
                    throw new InvalidOperationException("Invalid portrait bounds: " + result.id);
                result.bounds = bounds; result.visibleRenderers = renderers.Select(renderer => renderer.name).ToArray();
                result.materials = renderers.SelectMany(renderer => renderer.sharedMaterials).Select(material => material ? AssetDatabase.GetAssetPath(material) + ":" + material.name : "MISSING").ToArray();
                var camera = preview.camera; camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / .8f) * 1.12f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 30f; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.97f, .94f, .85f, 0f); camera.allowHDR = false;
                camera.transform.position = bounds.center + Vector3.back * 5f; camera.transform.LookAt(bounds.center);
                preview.lights[0].intensity = 1.15f; preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.Euler(340f, 218f, 0f);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                if (result.control == "B-neutral-front" || result.control == "C-neutral-front-brighter")
                {
                    preview.lights[0].color = preview.lights[1].color = Color.white;
                    preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(25f, -25f, 0f);
                    preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(10f, 35f, 0f);
                    if (result.control == "C-neutral-front-brighter")
                    { preview.lights[0].intensity = 1.55f; preview.lights[1].intensity = .85f; }
                }
                result.camera = camera.transform.localToWorldMatrix; result.orthographicSize = camera.orthographicSize;
                result.near = camera.nearClipPlane; result.far = camera.farClipPlane; result.background = camera.backgroundColor;
                result.beforeBegin = Lights(preview); result.ssaoBeforeRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height)); begun = true; result.afterBegin = Lights(preview);
                RequireSameLighting(result.beforeBegin, result.afterBegin);
                preview.Render(true); result.afterRender = Lights(preview); RequireSameLighting(result.beforeBegin, result.afterRender);
                if (!string.IsNullOrEmpty(result.control)) rgba = ReadNativeRgba(preview.camera.targetTexture, result);
                image = preview.EndStaticPreview(); begun = false; result.ssaoAfterRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                if (!image || image.width != Width || image.height != Height) throw new InvalidOperationException("Invalid portrait render: " + result.id);
                result.imageGraphicsFormat = image.graphicsFormat.ToString(); result.imageTextureFormat = image.format.ToString(); result.imageIsDataSRGB = image.isDataSRGB;
                result.distinctRgb = image.GetPixels32().Select(color => color.r << 16 | color.g << 8 | color.b).Distinct().Take(4096).Count();
                if (result.distinctRgb < 64) throw new InvalidOperationException("Portrait has insufficient pixel variation: " + result.id);
                byte[] bytes = image.EncodeToPNG(); result.bytes = bytes.Length; result.stagedPng = AvatarSurfaceMeshData.Hash(bytes);
                string imageName = string.IsNullOrEmpty(result.control) ? result.id : result.control + "-" + result.id;
                WriteNew(Path.Combine(folder, "Rendered", imageName + ".png"), bytes);
                if (rgba)
                {
                    byte[] alphaBytes = rgba.EncodeToPNG(); result.rgbaSha256 = AvatarSurfaceMeshData.Hash(alphaBytes);
                    WriteNew(Path.Combine(folder, "Rendered", imageName + "-rgba.png"), alphaBytes);
                    ValidateAlphaAndTransfer(rgba, image, result);
                }
            }
            catch (Exception error) { result.error = error.ToString(); throw; }
            finally
            {
                Clean(() => { if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); } }, result);
                Clean(() => { if (image) Object.DestroyImmediate(image); }, result);
                Clean(() => { if (rgba) Object.DestroyImmediate(rgba); }, result);
                Clean(() => preview?.Cleanup(), result);
                Clean(() => { if (holder) Object.DestroyImmediate(holder); }, result);
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, result);
                Clean(() => RenderTexture.active = active, result);
                Clean(() => { if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }, result);
                result.remainingOwnedObjects = (holder ? 1 : 0) + (image ? 1 : 0) + (rgba ? 1 : 0); result.previewClosed = !scene.IsValid() || !scene.isLoaded;
            }
            if (result.error.Length != 0 || result.remainingOwnedObjects != 0 || !result.previewClosed)
                throw new InvalidOperationException("Portrait cleanup failed: " + result.id + "\n" + result.error);
        }

        static Texture2D ReadNativeRgba(RenderTexture source, Portrait result)
        {
            if (!source || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(source.graphicsFormat))
                throw new InvalidOperationException("The native preview target has no alpha channel.");
            result.nativeTargetFormat = source.graphicsFormat.ToString(); result.nativeTargetSrgb = source.sRGB;
            RenderTexture encoded = null; Texture2D copy = null; var active = RenderTexture.active; bool srgbWrite = GL.sRGBWrite;
            try
            {
                encoded = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                if (!encoded.Create() || !encoded.sRGB || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(encoded.graphicsFormat))
                    throw new InvalidOperationException("Could not create a native sRGB alpha output target.");
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, encoded); RenderTexture.active = encoded;
                copy = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                copy.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false); copy.Apply(false, false);
                result.rgbaFormat = copy.graphicsFormat.ToString(); result.rgbaIsDataSrgb = copy.isDataSRGB;
                var output = copy; copy = null; return output;
            }
            finally
            {
                RenderTexture.active = active; GL.sRGBWrite = srgbWrite;
                if (copy) Object.DestroyImmediate(copy);
                if (encoded) { encoded.Release(); Object.DestroyImmediate(encoded); }
            }
        }

        static void ValidateAlphaAndTransfer(Texture2D rgba, Texture2D rgb, Portrait result)
        {
            var alphaPixels = rgba.GetPixels32(); var reference = rgb.GetPixels32();
            if (alphaPixels.Length != reference.Length || rgba.width != Width || rgba.height != Height || !rgba.isDataSRGB
                || !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(rgba.graphicsFormat))
                throw new InvalidOperationException("Invalid alpha readback dimensions or format.");
            for (int i = 0; i < alphaPixels.Length; i++)
            {
                var a = alphaPixels[i]; var b = reference[i];
                if (a.a == 0) result.alphaZero++; else if (a.a == 255) result.alphaOpaque++; else result.alphaPartial++;
                int x = i % Width, y = i / Width;
                if ((x == 0 || x == Width - 1 || y == 0 || y == Height - 1) && a.a != 0) result.borderNonzeroAlpha++;
                int delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));
                result.rgbMaxDelta = Mathf.Max(result.rgbMaxDelta, delta); if (delta != 0) result.rgbChangedPixels++;
            }
            result.alphaVerified = result.alphaZero > 1000 && result.alphaOpaque > 1000 && result.borderNonzeroAlpha == 0
                && alphaPixels[0].a == 0 && alphaPixels[(Height - 1 - 200) * Width + 128].a == 255 && result.rgbMaxDelta <= 2;
            if (!result.alphaVerified) throw new InvalidOperationException("Native alpha or RGB transfer did not pass; preserve both images and inspect the receipt.");
        }

        static void TrackSources(Report report, GameObject avatar, AnimationClip idle, ModelLibrary library, MaterialLibrary materials, GameData data, AvatarSurfaceProfile profile)
        {
            var roots = new Object[] { avatar, idle, materials, data, profile, profile.CorrectedMesh, GraphicsSettings.currentRenderPipeline };
            var inputs = roots.Select(AssetDatabase.GetAssetPath).Concat(new[] { ScriptPath });
            var dependencies = AssetDatabase.GetDependencies(inputs.ToArray(), true);
            if (dependencies.Any(path => path.StartsWith("Assets/", StringComparison.Ordinal) && !File.Exists(Full(path))))
                throw new IOException("A saved portrait dependency is missing.");
            report.excludedNonFileDependencies = dependencies.Where(path => !File.Exists(Full(path))).ToArray();
            var fonts = new[] { "Assets/_Project/Resources/Fonts", "Assets/_Project/UI/Fonts" }
                .Where(path => Directory.Exists(Full(path))).SelectMany(path => Directory.GetFiles(Full(path), "*", SearchOption.AllDirectories)).Select(Relative);
            var files = dependencies.Where(path => File.Exists(Full(path))).Concat(inputs).Concat(new[] { AssetDatabase.GetAssetPath(library),
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "ProjectSettings/EditorSettings.asset" })
                .SelectMany(path => new[] { path, path + ".meta" }).Concat(fonts).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal);
            foreach (string path in files)
            {
                if (path.StartsWith(Folder + "/", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected source dependency on a target portrait.");
                report.files.Add(new FileProof { path = path, before = FileHash(path) });
            }
            var objects = roots.Concat(new Object[] { library }).Concat(Meshes(avatar)).Concat(new Object[] { profile.CorrectedMesh })
                .Concat(avatar.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials)).Concat(materials.Materials).Where(value => value).Distinct();
            foreach (var value in objects) report.memory.Add(new MemoryProof { source = value, path = AssetDatabase.GetAssetPath(value), name = value.name,
                before = Memory(value), dirtyBefore = EditorUtility.IsDirty(value) });
        }

        static void VerifySources(Report report)
        {
            report.globalAfter = Global();
            foreach (var proof in report.files) { proof.after = FileHash(proof.path); proof.unchanged = proof.before == proof.after; }
            foreach (var proof in report.memory)
            {
                proof.after = proof.source ? Memory(proof.source) : "MISSING"; proof.dirtyAfter = proof.source && EditorUtility.IsDirty(proof.source);
                proof.unchanged = proof.before == proof.after && proof.dirtyBefore == proof.dirtyAfter;
            }
            report.sourcesAndGlobalPreserved = report.globalBefore == report.globalAfter && report.files.All(proof => proof.unchanged) && report.memory.All(proof => proof.unchanged);
        }
        static Lighting Lights(PreviewRenderUtility preview) => new Lighting { ambient = preview.ambientColor, lights = preview.lights.Select(light =>
            new LightState { enabled = light.enabled, color = light.color, intensity = light.intensity, rotation = light.transform.rotation }).ToArray() };
        static void RequireSameLighting(Lighting expected, Lighting actual)
        {
            if (expected.ambient != actual.ambient || expected.lights.Length != actual.lights.Length) throw new InvalidOperationException("Preview ambient/light count changed.");
            for (int i = 0; i < expected.lights.Length; i++)
                if (expected.lights[i].color != actual.lights[i].color || expected.lights[i].intensity != actual.lights[i].intensity
                    || Quaternion.Angle(expected.lights[i].rotation, actual.lights[i].rotation) > .001f)
                    throw new InvalidOperationException("Preview light parameters changed.");
        }
        static string Global()
        {
            var scene = SceneManager.GetActiveScene();
            return JsonUtility.ToJson(new GlobalState { active = scene.handle.GetRawData() + ":" + scene.path + ":" + scene.isDirty,
                scenes = string.Join(";", Enumerable.Range(0, SceneManager.sceneCount).Select(i => { var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isDirty + ":" + string.Join(",", s.GetRootGameObjects().Select(root => root.GetEntityId().ToString())); })),
                skybox = RenderSettings.skybox ? RenderSettings.skybox.GetEntityId().ToString() : "", sun = RenderSettings.sun ? RenderSettings.sun.GetEntityId().ToString() : "",
                pipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), renderTexture = RenderTexture.active ? RenderTexture.active.GetEntityId().ToString() : "",
                asyncShaders = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), srgbWrite = GL.sRGBWrite,
                ambientMode = RenderSettings.ambientMode.ToString(), ambient = RenderSettings.ambientLight, sky = RenderSettings.ambientSkyColor,
                equator = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity,
                reflectionIntensity = RenderSettings.reflectionIntensity, fog = RenderSettings.fog, fogDensity = RenderSettings.fogDensity, fogColor = RenderSettings.fogColor });
        }
        static void Restore(RenderTexture active, bool asyncShaders, bool ssao)
        {
            RenderTexture.active = active; EditorSettings.asyncShaderCompilation = asyncShaders;
            if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
        }
        static void RequireUnchangedTarget(Portrait portrait)
        {
            if (FileHash(Target(portrait.id)) != portrait.previousPng || FileHash(Target(portrait.id) + ".meta") != portrait.previousMeta
                || AssetDatabase.AssetPathToGUID(Target(portrait.id)) != portrait.previousGuid)
                throw new IOException("Portrait changed since preflight; preserve the concurrent work: " + portrait.id);
        }
        static Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
            .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
        static string Memory(Object value) => value is Mesh mesh ? AvatarSurfaceMeshData.Fingerprint(mesh) : AvatarSurfaceMeshData.Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(value)));
        static void Clean(Action action, Portrait result) { try { action(); } catch (Exception error) { result.error += "\nCleanup: " + error; } }
        static string Target(string id) => Folder + "/" + id + ".png";
        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        static string Relative(string path) => Path.GetRelativePath(Full("."), path).Replace('\\', '/');
        static string FileHash(string path) => File.Exists(Full(path)) ? AvatarSurfaceMeshData.Hash(File.ReadAllBytes(Full(path))) : "MISSING";
        static void WriteNew(string path, byte[] bytes) { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes, 0, bytes.Length); }
    }
}
