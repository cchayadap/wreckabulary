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
        const string Output = "BuildNotes/developer-workspace/portrait-controls/capture-04";
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
            public LightReport[] lights, lightsAfterBegin, lightsAfterRender;
            public List<SlotReport> slots = new List<SlotReport>();
            public List<MeshNormalReport> meshes = new List<MeshNormalReport>();
        }
        [Serializable] sealed class MeshNormalReport
        {
            public string renderer, sourceMesh;
            public string sourceDataBefore, sourceDataAfter, nonNormalDataBefore, nonNormalDataAfter;
            public string[] sourceVertexAttributes, controlledVertexAttributes;
            public bool sourceUnchanged, sourceDirtyBefore, sourceDirtyAfter, ownedNormalControlApplied;
            public int recalculatedNormalsChanged;
            public Matrix4x4 world;
            public float worldDeterminant;
            public int vertices;
            public FloatValue[] blendShapesBefore, blendShapesAfter;
            public NormalStats normals;
            public NormalStats[] submeshNormals;
            public WindingStats[] submeshWinding;
            [NonSerialized] public Mesh sourceObject;
        }
        [Serializable] sealed class WindingStats
        {
            public int submesh, triangles, validTriangles, invalidPositions, degenerateTriangles;
            public int normalSamples, invalidNormals, zeroNormals, alignedCorners, opposedCorners, sideCorners;
            public int outwardTriangles, inwardTriangles, sideTriangles, zeroRadialTriangles;
            public double areaWorld, normalSampleAreaWeight, radialSampleAreaWeight;
            public double areaWeightedNormalDot, areaWeightedRadialDot, signedVolumeWorld;
            public Bounds submeshWorldBounds;
            public string convention = "World cross(v1-v0,v2-v0) vs inverse-transpose baked vertex normals; dot thresholds +/-0.05; corner samples weighted triangle area/3";
            public string limitation = "Radial dot uses this submesh's world AABB center; signed volume uses world origin. Open or non-star-shaped surfaces, accessories and overlapping shells limit inward/outward interpretation.";
        }
        [Serializable] sealed class NormalStats
        {
            public int count, invalid, zero, towardCamera, awayCamera, sideCamera, towardKey, awayKey, sideKey;
            public float minLength, maxLength;
            public Vector3 meanWorldDirection;
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
            public uint renderingLayerMask;
        }
        [Serializable] sealed class SlotReport
        {
            public string renderer;
            public int slot;
            public uint renderingLayerMask;
            public string lightProbeUsage;
            public bool receiveShadows;
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
                foreach (string control in new[] { "A-original-lit", "H-head-recalculate-normals" })
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
            var meshes = new List<Mesh>();
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
                var original = ModelLibrary.Load().Find("Avatar/Avatar");
                var model = original ? Object.Instantiate(original, holder.transform, false) : null;
                if (!model) throw new InvalidOperationException("Original avatar spawn failed.");
                model.name = original.name;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var materialLibrary = MaterialLibrary.Load();
                if (materialLibrary) materialLibrary.ApplySkin(model, MaterialLibrary.StandardSkin);
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
                            slot = i, renderingLayerMask = renderer.renderingLayerMask, lightProbeUsage = renderer.lightProbeUsage.ToString(),
                            receiveShadows = renderer.receiveShadows,
                            source = Describe(source), rendererBlock = Describe(whole), slotBlock = Describe(block) };
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
                preview.lights[0].transform.rotation = result.control == "D-frontal-key" ? camera.transform.rotation :
                    result.control == "F-back-key" ? camera.transform.rotation * Quaternion.Euler(0, 180, 0) : Quaternion.Euler(35, 35, 0);
                preview.lights[1].intensity = .65f; preview.lights[1].transform.rotation = Quaternion.Euler(340, 218, 0);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                foreach (string name in new[] { "SK_Head", "SK_Hoodie" })
                {
                    var skin = visible.OfType<SkinnedMeshRenderer>().Single(r => r.name == name);
                    DescribeNormals(skin, holder.transform, camera.transform, preview.lights[0], result, meshes);
                }
                result.camera = camera.transform.localToWorldMatrix; result.orthographicSize = camera.orthographicSize;
                result.background = camera.backgroundColor; result.ambient = preview.ambientColor;
                result.lights = DescribeLights(preview.lights);
                result.ssaoKeywordBeforeRender = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
                preview.BeginStaticPreview(new Rect(0, 0, 256, 320));
                result.lightsAfterBegin = DescribeLights(preview.lights);
                preview.Render(true);
                result.lightsAfterRender = DescribeLights(preview.lights);
                image = preview.EndStaticPreview();
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
                result.ownedMeshes = meshes.Count;
                Cleanup(() => { if (image) Object.DestroyImmediate(image); }, result);
                result.previewCleanupAttempted = preview != null;
                Cleanup(() => preview?.Cleanup(), result);
                Cleanup(() => { if (holder) Object.DestroyImmediate(holder); }, result);
                Cleanup(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, result);
                foreach (var material in materials) Cleanup(() => { if (material) Object.DestroyImmediate(material); }, result);
                foreach (var mesh in meshes) Cleanup(() => { if (mesh) Object.DestroyImmediate(mesh); }, result);
                Cleanup(() => VerifySourceMeshes(result), result);
                Cleanup(() => RenderTexture.active = originalActive, result);
                result.remainingOwnedObjects = (holder ? 1 : 0) + (image ? 1 : 0) + materials.Count(m => m) + meshes.Count(m => m);
                result.previewSceneClosed = !scene.IsValid() || !scene.isLoaded;
            }
            if (result.cleanupErrors.Count != 0 || result.remainingOwnedObjects != 0 || !result.previewSceneClosed)
                throw new InvalidOperationException("Portrait control cleanup incomplete: " + result.control);
        }

        static void DescribeNormals(SkinnedMeshRenderer skin, Transform root, Transform camera, Light key,
            CaseReport result, List<Mesh> owned)
        {
            var source = skin.sharedMesh;
            if (!source || !EditorUtility.IsPersistent(source) || EditorUtility.IsPersistent(skin))
                throw new InvalidOperationException("Expected an instance renderer with a saved source mesh: " + skin.name);
            var report = new MeshNormalReport { renderer = AnimationUtility.CalculateTransformPath(skin.transform, root),
                sourceMesh = AssetDatabase.GetAssetPath(source) + ":" + source.name, world = skin.localToWorldMatrix,
                worldDeterminant = skin.localToWorldMatrix.determinant,
                blendShapesBefore = ReadBlendShapes(skin), sourceObject = source,
                sourceVertexAttributes = DescribeVertexAttributes(source),
                sourceDataBefore = MeshFingerprint(source, true), sourceDirtyBefore = EditorUtility.IsDirty(source) };
            result.meshes.Add(report);
            if (float.IsNaN(report.worldDeterminant) || float.IsInfinity(report.worldDeterminant) || report.worldDeterminant <= 0)
                throw new InvalidOperationException("This winding control requires a finite positive renderer determinant.");
            if (result.control == "H-head-recalculate-normals" && skin.name == "SK_Head")
            {
                var bones = skin.bones; var rootBone = skin.rootBone; var localBounds = skin.localBounds;
                var materials = skin.sharedMaterials;
                report.nonNormalDataBefore = MeshFingerprint(source, false);
                var copy = Object.Instantiate(source);
                owned.Add(copy);
                copy.name = source.name + " owned recalculated normals control";
                copy.hideFlags = HideFlags.HideAndDontSave;
                if (EditorUtility.IsPersistent(copy) || ReferenceEquals(copy, source)
                    || MeshFingerprint(copy, false) != report.nonNormalDataBefore)
                    throw new InvalidOperationException("Expected an exact owned mesh copy before changing normals.");
                var previous = copy.normals;
                copy.RecalculateNormals();
                report.nonNormalDataAfter = MeshFingerprint(copy, false);
                if (report.nonNormalDataAfter != report.nonNormalDataBefore)
                    throw new InvalidOperationException("Normal control changed positions, indices, skin data, blendshapes, tangents, UVs, colors or bounds.");
                var recalculated = copy.normals;
                if (previous.Length != copy.vertexCount || recalculated.Length != copy.vertexCount)
                    throw new InvalidOperationException("Normal control did not preserve the normal-array size.");
                for (int i = 0; i < recalculated.Length; i++)
                {
                    if (!Finite(recalculated[i]) || recalculated[i].sqrMagnitude < .0000000001f)
                        throw new InvalidOperationException("Normal recalculation returned a nonfinite or zero normal at " + i);
                    if ((previous[i] - recalculated[i]).sqrMagnitude > .0000000001f) report.recalculatedNormalsChanged++;
                }
                skin.sharedMesh = copy;
                for (int i = 0; i < report.blendShapesBefore.Length; i++)
                    skin.SetBlendShapeWeight(i, report.blendShapesBefore[i].value);
                skin.localBounds = localBounds;
                if (!ReferenceEquals(skin.sharedMesh, copy) || skin.rootBone != rootBone
                    || !skin.bones.SequenceEqual(bones) || !skin.sharedMaterials.SequenceEqual(materials)
                    || skin.localToWorldMatrix != report.world)
                    throw new InvalidOperationException("Owned normal control changed the renderer binding or pose.");
                report.ownedNormalControlApplied = true;
            }
            report.controlledVertexAttributes = DescribeVertexAttributes(skin.sharedMesh);
            report.blendShapesAfter = ReadBlendShapes(skin);
            if (report.blendShapesBefore.Length != report.blendShapesAfter.Length
                || report.blendShapesBefore.Where((value, i) => value.property != report.blendShapesAfter[i].property
                    || value.value != report.blendShapesAfter[i].value).Any())
                throw new InvalidOperationException("Sampled blendshape weights changed in a normal-only control.");
            var baked = new Mesh { name = skin.name + " normal telemetry", hideFlags = HideFlags.HideAndDontSave };
            owned.Add(baked); skin.BakeMesh(baked, false);
            var normals = baked.normals;
            report.vertices = baked.vertexCount;
            if (normals.Length != baked.vertexCount || normals.Length == 0)
                throw new InvalidOperationException("Missing baked normals: " + skin.name);
            var normalMatrix = skin.localToWorldMatrix.inverse.transpose;
            report.normals = MeasureNormals(normals, normalMatrix, -camera.forward, -key.transform.forward);
            report.submeshNormals = Enumerable.Range(0, baked.subMeshCount).Select(i =>
                MeasureNormals(baked.GetIndices(i).Distinct().Select(index => normals[index]), normalMatrix,
                    -camera.forward, -key.transform.forward)).ToArray();
            var vertices = baked.vertices;
            report.submeshWinding = Enumerable.Range(0, baked.subMeshCount).Select(i =>
                MeasureWinding(baked, i, vertices, normals, skin.localToWorldMatrix, normalMatrix)).ToArray();
        }

        static void VerifySourceMeshes(CaseReport result)
        {
            foreach (var mesh in result.meshes)
            {
                if (!mesh.sourceObject) throw new InvalidOperationException("Original source mesh was destroyed.");
                mesh.sourceDataAfter = MeshFingerprint(mesh.sourceObject, true);
                mesh.sourceDirtyAfter = EditorUtility.IsDirty(mesh.sourceObject);
                mesh.sourceUnchanged = mesh.sourceDataAfter == mesh.sourceDataBefore && mesh.sourceDirtyAfter == mesh.sourceDirtyBefore;
                if (!mesh.sourceUnchanged) throw new InvalidOperationException("Original mesh memory changed: " + mesh.sourceMesh);
            }
        }

        static string MeshFingerprint(Mesh mesh, bool includeNormals)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(mesh.vertexCount); writer.Write((int)mesh.indexFormat); writer.Write(mesh.subMeshCount);
            Write(writer, mesh.bounds.center); Write(writer, mesh.bounds.extents);
            var vertices = mesh.vertices; writer.Write(vertices.Length); foreach (var value in vertices) Write(writer, value);
            if (includeNormals)
            { var normals = mesh.normals; writer.Write(normals.Length); foreach (var value in normals) Write(writer, value); }
            var attributes = DescribeVertexAttributes(mesh); writer.Write(attributes.Length); foreach (string value in attributes) writer.Write(value);
            var tangents = mesh.tangents; writer.Write(tangents.Length); foreach (var value in tangents) Write(writer, value);
            var colors = mesh.colors; writer.Write(colors.Length);
            foreach (var value in colors) { writer.Write(value.r); writer.Write(value.g); writer.Write(value.b); writer.Write(value.a); }
            var uv = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            { uv.Clear(); mesh.GetUVs(channel, uv); writer.Write(uv.Count); foreach (var value in uv) Write(writer, value); }
            var bindposes = mesh.bindposes; writer.Write(bindposes.Length);
            foreach (var matrix in bindposes) for (int i = 0; i < 16; i++) writer.Write(matrix[i]);
            var counts = mesh.GetBonesPerVertex(); writer.Write(counts.Length);
            for (int i = 0; i < counts.Length; i++) writer.Write(counts[i]);
            var weights = mesh.GetAllBoneWeights(); writer.Write(weights.Length);
            for (int i = 0; i < weights.Length; i++) { writer.Write(weights[i].boneIndex); writer.Write(weights[i].weight); }
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var descriptor = mesh.GetSubMesh(i);
                writer.Write((int)descriptor.topology); writer.Write(descriptor.indexStart); writer.Write(descriptor.indexCount);
                writer.Write(descriptor.baseVertex); writer.Write(descriptor.firstVertex); writer.Write(descriptor.vertexCount);
                Write(writer, descriptor.bounds.center); Write(writer, descriptor.bounds.extents);
                var indices = mesh.GetIndices(i); writer.Write(indices.Length); foreach (int index in indices) writer.Write(index);
            }
            writer.Write(mesh.blendShapeCount);
            var positions = new Vector3[mesh.vertexCount]; var shapeNormals = new Vector3[mesh.vertexCount]; var shapeTangents = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                writer.Write(mesh.GetBlendShapeName(shape)); int frames = mesh.GetBlendShapeFrameCount(shape); writer.Write(frames);
                for (int frame = 0; frame < frames; frame++)
                {
                    writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame));
                    mesh.GetBlendShapeFrameVertices(shape, frame, positions, shapeNormals, shapeTangents);
                    for (int i = 0; i < mesh.vertexCount; i++) { Write(writer, positions[i]); Write(writer, shapeNormals[i]); Write(writer, shapeTangents[i]); }
                }
            }
            writer.Flush(); return Hash(stream.ToArray());
        }

        static void Write(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        static void Write(BinaryWriter writer, Vector4 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); writer.Write(value.w); }
        static string[] DescribeVertexAttributes(Mesh mesh) => mesh.GetVertexAttributes()
            .Select(value => value.attribute + ":" + value.format + "x" + value.dimension + " stream" + value.stream).ToArray();
        static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z)
            || float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));

        static WindingStats MeasureWinding(Mesh mesh, int submesh, Vector3[] vertices, Vector3[] normals,
            Matrix4x4 worldMatrix, Matrix4x4 normalMatrix)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("Triangle telemetry requires triangle topology: " + mesh.name + "/" + submesh);
            int[] indices = mesh.GetIndices(submesh);
            if (indices.Length % 3 != 0 || indices.Any(index => index < 0 || index >= vertices.Length))
                throw new InvalidOperationException("Invalid triangle indices: " + mesh.name + "/" + submesh);
            var stats = new WindingStats { submesh = submesh, triangles = indices.Length / 3 };
            var worldVertices = vertices.Select(worldMatrix.MultiplyPoint3x4).ToArray();
            var finitePoints = indices.Distinct().Select(index => worldVertices[index]).Where(Finite).ToArray();
            if (finitePoints.Length != 0)
            { stats.submeshWorldBounds = new Bounds(finitePoints[0], Vector3.zero); foreach (var point in finitePoints.Skip(1)) stats.submeshWorldBounds.Encapsulate(point); }
            double weightedNormalDot = 0, weightedRadialDot = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = worldVertices[indices[i]], b = worldVertices[indices[i + 1]], c = worldVertices[indices[i + 2]];
                if (!Finite(a) || !Finite(b) || !Finite(c)) { stats.invalidPositions++; continue; }
                Vector3 cross = Vector3.Cross(b - a, c - a); float twiceArea = cross.magnitude;
                if (!Finite(cross) || float.IsInfinity(twiceArea) || float.IsNaN(twiceArea)) { stats.invalidPositions++; continue; }
                if (twiceArea < 1e-12f) { stats.degenerateTriangles++; continue; }
                stats.validTriangles++;
                double area = twiceArea * .5; stats.areaWorld += area;
                Vector3 geometric = cross / twiceArea;
                stats.signedVolumeWorld += ((double)a.x * ((double)b.y * c.z - (double)b.z * c.y)
                    + (double)a.y * ((double)b.z * c.x - (double)b.x * c.z)
                    + (double)a.z * ((double)b.x * c.y - (double)b.y * c.x)) / 6;
                Vector3 radial = (a + b + c) / 3 - stats.submeshWorldBounds.center;
                float radialLength = radial.magnitude;
                if (!Finite(radial) || float.IsInfinity(radialLength) || float.IsNaN(radialLength)) stats.invalidPositions++;
                else if (radialLength < .000001f) stats.zeroRadialTriangles++;
                else
                {
                    float dot = Mathf.Clamp(Vector3.Dot(geometric, radial / radialLength), -1, 1);
                    weightedRadialDot += dot * area; stats.radialSampleAreaWeight += area;
                    if (dot > .05f) stats.outwardTriangles++; else if (dot < -.05f) stats.inwardTriangles++; else stats.sideTriangles++;
                }
                for (int corner = 0; corner < 3; corner++)
                {
                    stats.normalSamples++;
                    Vector3 normal = normalMatrix.MultiplyVector(normals[indices[i + corner]]);
                    float length = normal.magnitude;
                    if (!Finite(normal) || float.IsNaN(length) || float.IsInfinity(length)) { stats.invalidNormals++; continue; }
                    if (length < .00001f) { stats.zeroNormals++; continue; }
                    float dot = Mathf.Clamp(Vector3.Dot(geometric, normal / length), -1, 1);
                    double weight = area / 3; weightedNormalDot += dot * weight; stats.normalSampleAreaWeight += weight;
                    if (dot > .05f) stats.alignedCorners++; else if (dot < -.05f) stats.opposedCorners++; else stats.sideCorners++;
                }
            }
            if (stats.normalSampleAreaWeight > 0) stats.areaWeightedNormalDot = weightedNormalDot / stats.normalSampleAreaWeight;
            if (stats.radialSampleAreaWeight > 0) stats.areaWeightedRadialDot = weightedRadialDot / stats.radialSampleAreaWeight;
            return stats;
        }

        static FloatValue[] ReadBlendShapes(SkinnedMeshRenderer skin) => Enumerable.Range(0, skin.sharedMesh.blendShapeCount)
            .Select(i => new FloatValue { property = skin.sharedMesh.GetBlendShapeName(i), value = skin.GetBlendShapeWeight(i) }).ToArray();

        static NormalStats MeasureNormals(IEnumerable<Vector3> normals, Matrix4x4 normalMatrix, Vector3 toCamera, Vector3 toKey)
        {
            var stats = new NormalStats { minLength = float.MaxValue };
            foreach (var normal in normals)
            {
                stats.count++;
                Vector3 world = normalMatrix.MultiplyVector(normal);
                float length = world.magnitude;
                if (float.IsNaN(length) || float.IsInfinity(length)) { stats.invalid++; continue; }
                stats.minLength = Mathf.Min(stats.minLength, length); stats.maxLength = Mathf.Max(stats.maxLength, length);
                if (length < .00001f) { stats.zero++; continue; }
                Vector3 direction = world / length;
                stats.meanWorldDirection += direction;
                float cameraDot = Vector3.Dot(direction, toCamera), keyDot = Vector3.Dot(direction, toKey);
                if (cameraDot > .05f) stats.towardCamera++; else if (cameraDot < -.05f) stats.awayCamera++; else stats.sideCamera++;
                if (keyDot > .05f) stats.towardKey++; else if (keyDot < -.05f) stats.awayKey++; else stats.sideKey++;
            }
            int valid = stats.count - stats.invalid - stats.zero;
            if (valid > 0) stats.meanWorldDirection /= valid;
            if (stats.count == stats.invalid) stats.minLength = 0;
            return stats;
        }

        static LightReport[] DescribeLights(Light[] lights) => lights.Select(l => new LightReport
        {
            type = l.type.ToString(), shadows = l.shadows.ToString(), enabled = l.enabled, intensity = l.intensity,
            color = l.color, position = l.transform.position, forward = l.transform.forward, rotation = l.transform.rotation,
            cullingMask = l.cullingMask, renderingLayerMask = unchecked((uint)l.renderingLayerMask)
        }).ToArray();

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
