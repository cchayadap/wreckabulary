using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public static class ShopPortraitControlsDiagnostics
    {
        const string Output = "BuildNotes/developer-workspace/portrait-controls/capture-01";
        const string Lit = "Universal Render Pipeline/Lit", Unlit = "Universal Render Pipeline/Unlit";
        const string PureAlbedo = "Hidden/Wordbound/PortraitPureAlbedoDiagnostic";
        static readonly string[] Floats = { "_BumpScale", "_DetailNormalMapScale", "_Metallic", "_Smoothness", "_OcclusionStrength", "_Cull", "_Surface", "_Blend", "_AlphaClip", "_Cutoff", "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_AlphaToMask", "_QueueOffset" };
        static readonly string[] Textures = { "_BaseMap", "_MainTex", "_BumpMap", "_DetailNormalMap", "_DetailMask", "_OcclusionMap", "_EmissionMap" };
        static readonly string[] Colors = { "_BaseColor", "_Color", "_EmissionColor" };

        [Serializable] sealed class Report
        {
            public string utc, unity, graphics, colorSpace, renderPipeline, status = "IN PROGRESS", error = "";
            public string source = "Original Avatar/Avatar; courier; sampled Idle at zero; holder yaw 160";
            public string globalBefore, globalAfter;
            public bool globalStateUnchanged;
            public List<CaseReport> cases = new List<CaseReport>();
            public List<AssetProof> sources = new List<AssetProof>();
        }
        [Serializable] sealed class CaseReport
        {
            public string control, image, sha256, error = "", previewScene;
            public string imageGraphicsFormat, imageTextureFormat;
            public bool imageIsDataSRGB;
            public Bounds actualBounds, framingBounds;
            public Matrix4x4 camera;
            public float orthographicSize;
            public Color background, ambient;
            public int ownedMaterials, ownedMeshes, ownedImages, remainingOwnedObjects;
            public bool previewCleanupAttempted, previewSceneClosed;
            public bool ssaoKeywordBeforeRender, ssaoKeywordAfterRender;
            public List<string> cleanupErrors = new List<string>();
            public LightReport[] lights;
            public List<SlotReport> slots = new List<SlotReport>();
        }
        [Serializable] sealed class LightReport
        {
            public string type, shadows;
            public bool enabled;
            public float intensity;
            public Color color;
            public Vector3 position, forward;
            public Quaternion rotation;
            public int cullingMask;
        }
        [Serializable] sealed class SlotReport
        {
            public string renderer;
            public int slot;
            public MaterialReport source, rendered;
            public BlockReport rendererBlock, slotBlock;
            public string albedoBlockScope;
            public Color effectiveBaseColor;
            public TextureValue effectiveBaseMap;
            public Vector4 effectiveBaseMapST;
        }
        [Serializable] sealed class MaterialReport
        {
            public string name, path, guid, shader;
            public string[] keywords;
            public int renderQueue;
            public ColorValue[] colors;
            public FloatValue[] floats;
            public TextureValue[] textures;
        }
        [Serializable] sealed class ColorValue { public string property; public Color value; }
        [Serializable] sealed class FloatValue { public string property; public float value; }
        [Serializable] sealed class VectorValue { public string property; public Vector4 value; }
        [Serializable] sealed class TextureValue
        {
            public string property, path, guid, name, graphicsFormat, textureType, compression;
            public int width, height, maxTextureSize;
            public bool sRGB, flipGreenChannel, readable, isDataSRGB;
            public Vector2 scale, offset;
        }
        [Serializable] sealed class BlockReport
        {
            public bool empty;
            public ColorValue[] colors;
            public FloatValue[] floats;
            public TextureValue[] textures;
            public VectorValue[] vectors;
        }
        [Serializable] sealed class AssetProof
        {
            public string path, before, after, metaBefore, metaAfter, memoryBefore, memoryAfter;
            public bool unchanged, dirtyBefore, dirtyAfter;
            [NonSerialized] public Material material;
        }
        [Serializable] sealed class GlobalState
        {
            public ulong activeSceneHandle;
            public string activeScenePath, ambientMode;
            public Color ambientLight, ambientSky, ambientEquator, ambientGround, fogColor;
            public float ambientIntensity, reflectionIntensity, fogDensity;
            public bool fog;
            public bool activeSceneDirty;
            public string skybox, sun;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run outside Play Mode.");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Output));
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing control output: " + folder);
            var library = ModelLibrary.Load();
            var avatar = library ? library.Find("Avatar/Avatar") : null;
            var idle = library ? library.FindClip("Avatar/Avatar", "Idle") : null;
            var unlit = Shader.Find(Unlit);
            var pureAlbedo = Shader.Find(PureAlbedo);
            var outfit = CosmeticBundles.Find("courier")?.Apply(GameConfig.Current.Wardrobe.Default, GameConfig.Current.Wardrobe);
            if (!avatar || !idle || !unlit || !unlit.isSupported || !pureAlbedo || !pureAlbedo.isSupported || outfit == null)
                throw new InvalidOperationException("Original avatar, Idle, courier, URP Unlit and the pure-albedo diagnostic shader are required.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName,
                colorSpace = QualitySettings.activeColorSpace.ToString(), globalBefore = Global(),
                renderPipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline) };
            var proofs = new Dictionary<string, AssetProof>(StringComparer.Ordinal);
            Track(avatar, proofs); Track(idle, proofs); Track(library, proofs); Track(MaterialLibrary.Load(), proofs);
            Track(GraphicsSettings.currentRenderPipeline, proofs); Track(pureAlbedo, proofs);
            foreach (var renderer in avatar.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Track(material, proofs);
                    if (material) foreach (string property in material.GetTexturePropertyNames()) Track(material.GetTexture(property), proofs);
                }
            Directory.CreateDirectory(folder);
            var framing = new Bounds();
            try
            {
                foreach (string control in new[] { "A-original-lit", "B-no-normal-map", "C-urp-unlit", "D-frontal-key", "E-pure-albedo" })
                {
                    var result = new CaseReport { control = control, image = control + ".png" };
                    report.cases.Add(result);
                    Capture(result, folder, idle, outfit, unlit, pureAlbedo, proofs, ref framing);
                }
                report.status = "CAPTURED; controlled comparison and art acceptance pending";
            }
            catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); }
            finally
            {
                report.globalAfter = Global();
                report.globalStateUnchanged = report.globalBefore == report.globalAfter;
                report.sources = proofs.Values.OrderBy(p => p.path, StringComparer.Ordinal).ToList();
                foreach (var proof in report.sources)
                {
                    try
                    {
                        proof.after = FileHash(proof.path); proof.metaAfter = FileHash(proof.path + ".meta");
                        proof.memoryAfter = proof.material ? EditorJsonUtility.ToJson(proof.material) : "";
                        proof.dirtyAfter = proof.material && EditorUtility.IsDirty(proof.material);
                        proof.unchanged = proof.before == proof.after && proof.metaBefore == proof.metaAfter
                            && proof.memoryBefore == proof.memoryAfter && proof.dirtyBefore == proof.dirtyAfter;
                    }
                    catch (Exception error) { report.error += "\nSource verification: " + error; }
                }
                if (!report.globalStateUnchanged || report.sources.Any(p => !p.unchanged))
                { report.status = "FAIL"; report.error += "\nGlobal state or source preservation check failed; no automatic repair attempted."; }
                if (report.error.Length != 0) report.status = "FAIL";
                WriteNew(Path.Combine(folder, "diagnostics.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            }
            if (report.status == "FAIL") throw new InvalidOperationException("Portrait controls failed; retain " + folder + "\n" + report.error);
            Debug.Log("SHOP_PORTRAIT_CONTROLS: " + folder + "; cases=" + report.cases.Count);
        }

        static void Capture(CaseReport result, string folder, AnimationClip idle, Outfit outfit, Shader unlit, Shader pureAlbedo,
            Dictionary<string, AssetProof> proofs, ref Bounds framing)
        {
            PreviewRenderUtility preview = null;
            GameObject holder = null;
            Texture2D image = null;
            var materials = new List<Material>();
            var originalActive = RenderTexture.active;
            var scene = default(Scene);
            try
            {
                preview = new PreviewRenderUtility();
                if (!EditorSceneManager.IsPreviewScene(preview.camera.gameObject.scene))
                    throw new InvalidOperationException("Expected an owned preview scene.");
                scene = preview.camera.gameObject.scene; result.previewScene = scene.name;
                holder = new GameObject("Courier " + result.control) { hideFlags = HideFlags.HideAndDontSave };
                preview.AddSingleGO(holder);
                if (holder.scene != scene) throw new InvalidOperationException("Avatar holder did not enter the owned preview scene.");
                var model = ModelVisual.Spawn("Avatar/Avatar", holder.transform);
                if (!model) throw new InvalidOperationException("Original avatar spawn failed.");
                idle.SampleAnimation(model, 0);
                PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), outfit);
                holder.transform.rotation = Quaternion.Euler(0, 160, 0);
                var visible = model.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !r.forceRenderingOff).ToArray();
                if (visible.Length == 0) throw new InvalidOperationException("Courier has no visible renderers.");
                var bounds = visible[0].bounds;
                foreach (var renderer in visible.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (result.control == "A-original-lit") framing = bounds;
                result.actualBounds = bounds; result.framingBounds = framing;
                foreach (var renderer in visible)
                {
                    var slots = renderer.sharedMaterials;
                    var whole = new MaterialPropertyBlock(); renderer.GetPropertyBlock(whole);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (!source || !source.shader || source.shader.name != Lit)
                            throw new InvalidOperationException("Expected original URP Lit material: " + renderer.name + "/" + i);
                        if (!EditorUtility.IsPersistent(source)) throw new InvalidOperationException("Expected a saved source material: " + source.name);
                        Track(source, proofs);
                        foreach (string property in source.GetTexturePropertyNames()) Track(source.GetTexture(property), proofs);
                        var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, i);
                        var slot = new SlotReport { renderer = AnimationUtility.CalculateTransformPath(renderer.transform, holder.transform),
                            slot = i, source = Describe(source), rendererBlock = Describe(whole), slotBlock = Describe(block) };
                        var albedo = !block.isEmpty ? block : whole;
                        slot.albedoBlockScope = !block.isEmpty ? "material slot" : !whole.isEmpty ? "renderer" : "material asset";
                        slot.effectiveBaseColor = albedo.HasProperty("_BaseColor") ? albedo.GetColor("_BaseColor") : source.GetColor("_BaseColor");
                        slot.effectiveBaseMap = Describe("_BaseMap", albedo.HasProperty("_BaseMap") ? albedo.GetTexture("_BaseMap") : source.GetTexture("_BaseMap"),
                            source.GetTextureScale("_BaseMap"), source.GetTextureOffset("_BaseMap"));
                        var scale = source.GetTextureScale("_BaseMap"); var offset = source.GetTextureOffset("_BaseMap");
                        slot.effectiveBaseMapST = albedo.HasProperty("_BaseMap_ST") ? albedo.GetVector("_BaseMap_ST") : new Vector4(scale.x, scale.y, offset.x, offset.y);
                        if (result.control == "B-no-normal-map" && new[] { "_DetailNormalMap", "_DetailNormalMapScale" }.Any(p => whole.HasProperty(p) || block.HasProperty(p)))
                            throw new InvalidOperationException("Unexpected detail-normal property-block override; keep the control fail-closed.");
                        if (result.control == "B-no-normal-map" || result.control == "C-urp-unlit" || result.control == "E-pure-albedo")
                        {
                            var copy = result.control == "B-no-normal-map" ? new Material(source)
                                : new Material(result.control == "C-urp-unlit" ? unlit : pureAlbedo);
                            materials.Add(copy); copy.name = source.name + " " + result.control; copy.hideFlags = HideFlags.HideAndDontSave;
                            if (result.control == "B-no-normal-map") DisableNormals(copy);
                            else CopyAlbedo(source, copy);
                            slots[i] = copy;
                        }
                        slot.rendered = Describe(slots[i]); result.slots.Add(slot);
                    }
                    renderer.sharedMaterials = slots;
                }
                var camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(framing.extents.y, framing.extents.x / .8f) * 1.12f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.97f, .94f, .85f, 0);
                camera.allowHDR = false; camera.transform.position = framing.center + Vector3.back * 5;
                camera.transform.LookAt(framing.center);
                preview.lights[0].intensity = 1.15f;
                preview.lights[0].transform.rotation = result.control == "D-frontal-key" ? camera.transform.rotation : Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 0);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                result.camera = camera.transform.localToWorldMatrix; result.orthographicSize = camera.orthographicSize;
                result.background = camera.backgroundColor; result.ambient = preview.ambientColor;
                result.lights = preview.lights.Select(l => new LightReport { type = l.type.ToString(), shadows = l.shadows.ToString(),
                    enabled = l.enabled, intensity = l.intensity, color = l.color, position = l.transform.position,
                    forward = l.transform.forward, rotation = l.transform.rotation, cullingMask = l.cullingMask }).ToArray();
                result.ssaoKeywordBeforeRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                preview.BeginStaticPreview(new Rect(0, 0, 256, 320)); preview.Render(true); image = preview.EndStaticPreview();
                result.ssaoKeywordAfterRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                result.ownedImages = image ? 1 : 0;
                if (!image || image.width != 256 || image.height != 320) throw new InvalidOperationException("Invalid control image.");
                result.imageGraphicsFormat = image.graphicsFormat.ToString(); result.imageTextureFormat = image.format.ToString();
                result.imageIsDataSRGB = image.isDataSRGB;
                byte[] bytes = image.EncodeToPNG(); result.sha256 = Hash(bytes); WriteNew(Path.Combine(folder, result.image), bytes);
            }
            catch (Exception error) { result.error = error.ToString(); throw; }
            finally
            {
                result.ownedMaterials = materials.Count;
                Cleanup(() => { if (image) Object.DestroyImmediate(image); }, result);
                result.previewCleanupAttempted = preview != null;
                Cleanup(() => preview?.Cleanup(), result);
                Cleanup(() => { if (holder) Object.DestroyImmediate(holder); }, result);
                Cleanup(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, result);
                foreach (var material in materials) Cleanup(() => { if (material) Object.DestroyImmediate(material); }, result);
                Cleanup(() => RenderTexture.active = originalActive, result);
                result.remainingOwnedObjects = (holder ? 1 : 0) + (image ? 1 : 0) + materials.Count(m => m);
                result.previewSceneClosed = !scene.IsValid() || !scene.isLoaded;
            }
            if (result.cleanupErrors.Count != 0 || result.remainingOwnedObjects != 0 || !result.previewSceneClosed)
                throw new InvalidOperationException("Portrait control cleanup incomplete: " + result.control);
        }

        static void DisableNormals(Material copy)
        {
            copy.DisableKeyword("_NORMALMAP");
            foreach (string property in new[] { "_BumpMap", "_DetailNormalMap" }) if (copy.HasProperty(property)) copy.SetTexture(property, null);
            foreach (string property in new[] { "_BumpScale", "_DetailNormalMapScale" }) if (copy.HasProperty(property)) copy.SetFloat(property, 0);
        }

        static void CopyAlbedo(Material source, Material copy)
        {
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
                if (source.HasProperty(property) && copy.HasProperty(property))
                { copy.SetTexture(property, source.GetTexture(property)); copy.SetTextureScale(property, source.GetTextureScale(property)); copy.SetTextureOffset(property, source.GetTextureOffset(property)); }
            foreach (string property in new[] { "_BaseColor", "_Color" })
                if (source.HasProperty(property) && copy.HasProperty(property)) copy.SetColor(property, source.GetColor(property));
            foreach (string property in Floats)
                if (source.HasProperty(property) && copy.HasProperty(property)) copy.SetFloat(property, source.GetFloat(property));
            foreach (string keyword in new[] { "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAMODULATE_ON" })
                if (source.IsKeywordEnabled(keyword)) copy.EnableKeyword(keyword);
            copy.renderQueue = source.renderQueue; copy.SetOverrideTag("RenderType", source.GetTag("RenderType", false, "Opaque"));
        }

        static MaterialReport Describe(Material material) => new MaterialReport
        {
            name = material.name, path = AssetDatabase.GetAssetPath(material), guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)),
            shader = material.shader ? material.shader.name : "MISSING", keywords = material.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
            renderQueue = material.renderQueue,
            colors = Colors.Where(material.HasProperty).Select(p => new ColorValue { property = p, value = material.GetColor(p) }).ToArray(),
            floats = Floats.Where(material.HasProperty).Select(p => new FloatValue { property = p, value = material.GetFloat(p) }).ToArray(),
            textures = Textures.Where(material.HasProperty).Select(p => Describe(p, material.GetTexture(p), material.GetTextureScale(p), material.GetTextureOffset(p))).ToArray()
        };

        static BlockReport Describe(MaterialPropertyBlock block) => new BlockReport
        {
            empty = block.isEmpty,
            colors = Colors.Where(p => block.HasProperty(p)).Select(p => new ColorValue { property = p, value = block.GetColor(p) }).ToArray(),
            floats = Floats.Where(p => block.HasProperty(p)).Select(p => new FloatValue { property = p, value = block.GetFloat(p) }).ToArray(),
            textures = Textures.Where(p => block.HasProperty(p)).Select(p => Describe(p, block.GetTexture(p), Vector2.one, Vector2.zero)).ToArray(),
            vectors = new[] { "_BaseMap_ST", "_MainTex_ST", "_BumpMap_ST", "_DetailNormalMap_ST" }.Where(p => block.HasProperty(p))
                .Select(p => new VectorValue { property = p, value = block.GetVector(p) }).ToArray()
        };

        static TextureValue Describe(string property, Texture texture, Vector2 scale, Vector2 offset)
        {
            string path = texture ? AssetDatabase.GetAssetPath(texture) : "";
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;
            return new TextureValue { property = property, path = path, guid = AssetDatabase.AssetPathToGUID(path), name = texture ? texture.name : "NONE",
                width = texture ? texture.width : 0, height = texture ? texture.height : 0, graphicsFormat = texture ? texture.graphicsFormat.ToString() : "",
                scale = scale, offset = offset, textureType = importer ? importer.textureType.ToString() : "", compression = importer ? importer.textureCompression.ToString() : "",
                maxTextureSize = importer ? importer.maxTextureSize : 0, sRGB = importer && importer.sRGBTexture,
                isDataSRGB = texture is Texture2D image && image.isDataSRGB,
                flipGreenChannel = importer && importer.flipGreenChannel, readable = importer && importer.isReadable };
        }

        static void Track(Object asset, Dictionary<string, AssetProof> proofs)
        {
            if (!asset) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || proofs.ContainsKey(path)) return;
            var material = asset as Material;
            proofs.Add(path, new AssetProof { path = path, before = FileHash(path), metaBefore = FileHash(path + ".meta"), material = material,
                memoryBefore = material ? EditorJsonUtility.ToJson(material) : "", dirtyBefore = material && EditorUtility.IsDirty(material) });
        }

        static string Global()
        {
            var scene = SceneManager.GetActiveScene();
            return JsonUtility.ToJson(new GlobalState { activeSceneHandle = scene.handle.GetRawData(), activeScenePath = scene.path, activeSceneDirty = scene.isDirty,
                ambientMode = RenderSettings.ambientMode.ToString(),
                ambientLight = RenderSettings.ambientLight, ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
                ambientGround = RenderSettings.ambientGroundColor, ambientIntensity = RenderSettings.ambientIntensity, reflectionIntensity = RenderSettings.reflectionIntensity,
                fog = RenderSettings.fog, fogDensity = RenderSettings.fogDensity, fogColor = RenderSettings.fogColor,
                skybox = RenderSettings.skybox ? RenderSettings.skybox.GetEntityId().ToString() : "",
                sun = RenderSettings.sun ? RenderSettings.sun.GetEntityId().ToString() : "" });
        }

        static void Cleanup(Action action, CaseReport result)
        { try { action(); } catch (Exception error) { result.cleanupErrors.Add(error.ToString()); } }
        static string FileHash(string path)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            return File.Exists(fullPath) ? Hash(File.ReadAllBytes(fullPath)) : "MISSING";
        }
        static string Hash(byte[] bytes) { using var hash = SHA256.Create(); return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void WriteNew(string path, byte[] bytes)
        { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes, 0, bytes.Length); }
    }
}
