#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WordboundHoldingViewsCapture
    {
        const string OutputRoot = "BuildNotes/character-experience-20261026/holding/views/captures";
        const int Pixels = 768;
        [Serializable] sealed class FileProof { public string path, before, after; }
        [Serializable] sealed class MemoryProof { public string name, path, before, after; public bool dirtyBefore, dirtyAfter; }
        [Serializable] sealed class Geometry
        {
            public string renderer, mesh;
            public int vertices;
            public Vector3 center, size;
            public bool weaponBoundsOverlap, scaleCompensatedSkin;
            public Vector3 rendererLossyScale;
            public float bakeWorldAgreementMax;
        }
        [Serializable] sealed class Frame
        {
            public string item, phase, view, image, sha256;
            public int age, windupTicks, activeTicks, cycleTicks;
            public float gripDistance, gripAngle, orthographicSize;
            public Vector3 hand, grip, cameraPosition, cameraEuler;
            public Quaternion handRotation, gripRotation;
            public Vector3 weaponCenter, weaponSize;
            public bool allGeometryInsideFrame, skinMatricesRefreshedPerRender;
            public Color ambient;
            public float keyIntensity, fillIntensity;
            public List<Geometry> body = new List<Geometry>();
        }
        [Serializable] sealed class Report
        {
            public string status = "IN PROGRESS", error = "", cleanupError = "", utc, unity, folder, profileJson;
            public string poseScope = "Production cosmetic graph and production held placement; synthetic canonical ages, no runtime interpolation, command, hit or networking simulation. Face-expression driver is not evaluated.";
            public string geometryScope = "Bounds are from actual baked visible geometry. AABB overlap is only a broad-phase flag: neither overlap nor separation is a triangle-level body-clearance verdict. No collision or penetration algorithm is claimed.";
            public bool filesPreserved, memoryPreserved, scenesPreserved, globalsPreserved;
            public bool ownedReleased = true;
            public int tactileScopesBefore, tactileScopesAfter, tactileMaterialsBefore, tactileMaterialsAfter, tactileTexturesBefore, tactileTexturesAfter;
            public int tactilePeakMaterials, tactilePeakTextures;
            public bool tactileCleanupVerified, sharedCacheRetentionExpected;
            public List<FileProof> inputs = new List<FileProof>();
            public List<MemoryProof> memory = new List<MemoryProof>();
            public List<Frame> frames = new List<Frame>();
        }
        sealed class Shape
        {
            public Renderer renderer;
            public Mesh mesh;
            public Vector3[] world;
            public Bounds bounds;
            public bool scaleCompensatedSkin;
            public float bakeWorldAgreementMax;
        }
        sealed class Memory
        {
            public Object value;
            public MemoryProof proof;
        }

        [MenuItem("Wreckabulary/Art/Capture Original Held Windup Views")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run in Edit Mode only.");
            var profile = AssetDatabase.LoadAssetAtPath<WordboundVisualProfile>(WordboundAuthoringSetup.VisualPath);
            var library = ModelLibrary.Load();
            var surface = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            if (!profile || !library || !surface) throw new InvalidOperationException("Saved presentation, model library and validated surface correction are required.");
            profile.Validate();
            if (profile.BaseArtwork != WordboundBaseArtwork.OriginalWreckabulary || profile.Overrides.Any(v => v.VisualKey == "BAT" || v.VisualKey == "HAMMER"))
                throw new InvalidOperationException("This diagnostic requires the saved original BAT/HAMMER route; no override is temporarily removed.");
            var observe = typeof(WordboundAvatarMotion).GetMethod("ObserveMelee", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(long), typeof(long), typeof(MatchActorState), typeof(string), typeof(ItemDefinition), typeof(bool) }, null);
            if (observe == null) throw new MissingMethodException("Production ObserveMelee signature changed; review this diagnostic.");
            string folder = OutputRoot + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (Directory.Exists(folder)) throw new IOException("Preserve existing captures.");
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                folder = folder, profileJson = EditorJsonUtility.ToJson(profile),
                tactileScopesBefore = TactileMaterials.RegisteredScopeCount, tactileMaterialsBefore = TactileMaterials.SharedMaterialCount,
                tactileTexturesBefore = TactileMaterials.GeneratedTextureCount };
            report.tactilePeakMaterials = report.tactileMaterialsBefore; report.tactilePeakTextures = report.tactileTexturesBefore;
            var sources = new[] { library.Find(WordboundVisualResolver.AvatarKey), library.Find("Items/BAT"), library.Find("Items/HAMMER") };
            if (sources.Any(s => !s)) throw new InvalidOperationException("Original source models are missing.");
            var explicitPaths = sources.Select(AssetDatabase.GetAssetPath).Concat(new[] { AssetDatabase.GetAssetPath(library),
                WordboundAuthoringSetup.VisualPath, AvatarSurfaceCorrectionSetup.ProfilePath, AvatarSurfaceCorrectionSetup.MeshPath,
                "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" }).ToArray();
            foreach (string path in explicitPaths) if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new IOException("Required input missing: " + path);
            var paths = AssetDatabase.GetDependencies(explicitPaths, true).Concat(explicitPaths).Distinct(StringComparer.Ordinal).ToArray();
            var memory = new List<Memory>();
            foreach (string path in paths)
            {
                if (!File.Exists(path)) { if (path.StartsWith("Assets/", StringComparison.Ordinal)) throw new IOException("Missing dependency: " + path); continue; }
                report.inputs.Add(new FileProof { path = path, before = Hash(path) });
                if (File.Exists(path + ".meta")) report.inputs.Add(new FileProof { path = path + ".meta", before = Hash(path + ".meta") });
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path).Where(v => v && (v is Mesh || v is Material || v is AnimationClip || v is ScriptableObject)))
                {
                    if (memory.Any(m => m.value == value)) continue;
                    if (value is Material material && (!material.shader || !material.shader.isSupported || material.shader.name.Contains("InternalErrorShader")))
                        throw new InvalidOperationException("Unsupported material: " + value.name);
                    var proof = new MemoryProof { name = value.name, path = path, before = Fingerprint(value), dirtyBefore = EditorUtility.IsDirty(value) };
                    memory.Add(new Memory { value = value, proof = proof }); report.memory.Add(proof);
                }
            }
            string scenes = SceneState(); var active = RenderTexture.active; bool srgb = GL.sRGBWrite;
            bool async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            Exception failure = null; Directory.CreateDirectory(folder);
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                foreach (string word in new[] { "BAT", "HAMMER" }) CaptureItem(word, profile, observe, report);
                if (report.frames.Count != 18 || report.frames.Any(f => !f.allGeometryInsideFrame || !f.skinMatricesRefreshedPerRender || f.gripDistance > .0001f))
                    throw new InvalidOperationException("Expected 18 complete in-frame views with maintained socket position.");
                report.status = "CAPTURED; MULTI-ANGLE VISUAL REVIEW REQUIRED, NO CLEARANCE APPROVAL";
            }
            catch (Exception error) { failure = error; report.status = "FAIL"; report.error = error.ToString(); throw; }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => RenderTexture.active = active, errors); Clean(() => GL.sRGBWrite = srgb, errors);
                Clean(() => SetSsao(ssao), errors);
                foreach (var proof in report.inputs) proof.after = File.Exists(proof.path) ? Hash(proof.path) : "MISSING";
                foreach (var entry in memory)
                { entry.proof.after = entry.value ? Fingerprint(entry.value) : "DESTROYED"; entry.proof.dirtyAfter = entry.value && EditorUtility.IsDirty(entry.value); }
                report.filesPreserved = report.inputs.All(p => p.before == p.after);
                report.memoryPreserved = report.memory.All(p => p.before == p.after && p.dirtyBefore == p.dirtyAfter) && EditorJsonUtility.ToJson(profile) == report.profileJson;
                report.scenesPreserved = SceneState() == scenes;
                report.globalsPreserved = RenderTexture.active == active && GL.sRGBWrite == srgb && EditorSettings.asyncShaderCompilation == async && Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION") == ssao;
                report.tactileScopesAfter = TactileMaterials.RegisteredScopeCount;
                report.tactileMaterialsAfter = TactileMaterials.SharedMaterialCount; report.tactileTexturesAfter = TactileMaterials.GeneratedTextureCount;
                report.sharedCacheRetentionExpected = report.tactileScopesBefore > 0;
                report.tactileCleanupVerified = report.tactileScopesAfter == report.tactileScopesBefore &&
                    (report.sharedCacheRetentionExpected
                        ? report.tactileMaterialsAfter >= report.tactileMaterialsBefore && report.tactileMaterialsAfter <= report.tactilePeakMaterials && report.tactileMaterialsAfter <= 32 &&
                          report.tactileTexturesAfter >= report.tactileTexturesBefore && report.tactileTexturesAfter <= report.tactilePeakTextures && report.tactileTexturesAfter <= 38
                        : report.tactileMaterialsAfter == 0 && report.tactileTexturesAfter == 0);
                report.cleanupError = string.Join("\n", errors.Select(e => e.ToString()));
                if (errors.Count > 0 || !report.filesPreserved || !report.memoryPreserved || !report.scenesPreserved || !report.globalsPreserved || !report.ownedReleased || !report.tactileCleanupVerified) report.status = "FAIL: preservation or cleanup";
                using (var stream = new FileStream(folder + "/receipt.json", FileMode.CreateNew))
                using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
                Debug.Log("[Wreckabulary] Holding views " + report.status + "; receipt=" + folder + "/receipt.json");
                if (errors.Count > 0) { if (failure != null) errors.Insert(0, failure); throw new AggregateException(errors); }
                if (!report.filesPreserved || !report.memoryPreserved || !report.scenesPreserved || !report.globalsPreserved || !report.ownedReleased || !report.tactileCleanupVerified)
                    throw new InvalidOperationException("Holding capture preservation/cleanup failed; inspect its receipt.");
            }
        }

        static void CaptureItem(string word, WordboundVisualProfile profile, MethodInfo observe, Report report)
        {
            PreviewRenderUtility preview = null; GameObject anchor = null; WordboundVisualInstance avatar = null, held = null;
            WordboundAvatarMotion motion = null; Scene scene = default; Camera camera = null; Light[] lights = null;
            var ownedScopes = new List<TactileMaterials>();
            Exception failure = null; var active = RenderTexture.active; bool ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            try
            {
                preview = new PreviewRenderUtility(); scene = preview.camera.gameObject.scene; camera = preview.camera; lights = preview.lights.ToArray();
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected owned preview scene.");
                anchor = EditorUtility.CreateGameObjectWithHideFlags("Held pose inspection", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(anchor, scene);
                var item = GameConfig.Current.Items.Get(word);
                if (item.Model != "Items/" + word || item.Melee == null || item.Family != HandlingFamily.MeleeSwing) throw new InvalidOperationException("Expected original melee item.");
                var resolver = new WordboundVisualResolver(GameConfig.Current.Items, profile: profile);
                avatar = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor.transform); avatar.Dress(null);
                foreach (var skin in avatar.Model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                { skin.forceMatrixRecalculationPerRender = true; skin.updateWhenOffscreen = true; }
                held = resolver.Create(word, new Vector3(item.Size[0], item.Size[1], item.Size[2]), anchor.transform);
                ownedScopes.AddRange(anchor.GetComponentsInChildren<TactileMaterials>(true));
                report.tactilePeakMaterials = Math.Max(report.tactilePeakMaterials, TactileMaterials.SharedMaterialCount);
                report.tactilePeakTextures = Math.Max(report.tactilePeakTextures, TactileMaterials.GeneratedTextureCount);
                var source = ModelLibrary.Load().Find(item.Model);
                if (!source.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).SequenceEqual(held.Model.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)))
                    throw new InvalidOperationException("Held model is not the exact original mesh set.");
                var socket = ModelVisual.FindNamed(held.Model, "Grip_R");
                if (!socket || !avatar.RightHand) throw new InvalidOperationException("Required authored grip missing.");
                motion = new WordboundAvatarMotion(avatar); motion.Step(.1f, true, true, false); motion.Step(.1f, true, true, false);
                int windup = WordboundCombatPolicy.WindupTicks(item.Melee), duration = WordboundCombatPolicy.ActiveTicks(item.Melee), cycle = WordboundCombatPolicy.CycleTicks(item.Melee);
                int[] ages = { Math.Max(1, windup / 3), Math.Max(1, windup - 1), windup + Math.Max(1, duration / 2) };
                if (ages.Distinct().Count() != 3 || ages[2] >= windup + duration) throw new InvalidOperationException("Unsupported phase sampling; review timings.");
                string[] phases = { "early-windup", "late-windup", "active" }; int next = 0;
                for (int age = 0; age <= ages[2]; age++)
                {
                    var actor = new MatchActorState("inspection", 100, 100, nextAttackTick: 100 + cycle, equippedSlot: 0,
                        attack: new MatchAttackState(100, "inspection-held"));
                    observe.Invoke(motion, new object[] { 0L, 100L + age, actor, "inspection-held", item, true });
                    motion.Step(0, true, true, false); held.PlaceAtHand(anchor.transform, avatar.RightHand, true);
                    if (age != ages[next]) continue;
                    var owned = new List<Mesh>();
                    try
                    {
                        var body = Shapes(avatar.Model, owned); var weapon = Shapes(held.Model, owned);
                        var weaponBounds = Combined(weapon); var allPoints = body.Concat(weapon).SelectMany(s => s.world).ToArray();
                        foreach (string view in new[] { "front", "side", "rear" })
                        {
                            var frame = new Frame { item = word, phase = phases[next], view = view, age = age,
                                windupTicks = windup, activeTicks = duration, cycleTicks = cycle,
                                hand = avatar.RightHand.position, grip = socket.position, handRotation = avatar.RightHand.rotation, gripRotation = socket.rotation,
                                gripDistance = Vector3.Distance(avatar.RightHand.position, socket.position), gripAngle = Quaternion.Angle(avatar.RightHand.rotation, socket.rotation),
                                weaponCenter = weaponBounds.center, weaponSize = weaponBounds.size,
                                skinMatricesRefreshedPerRender = body.Where(s => s.renderer is SkinnedMeshRenderer)
                                    .All(s => ((SkinnedMeshRenderer)s.renderer).forceMatrixRecalculationPerRender && ((SkinnedMeshRenderer)s.renderer).updateWhenOffscreen) };
                            foreach (var shape in body) frame.body.Add(new Geometry { renderer = shape.renderer.name, mesh = shape.mesh.name,
                                vertices = shape.world.Length, center = shape.bounds.center, size = shape.bounds.size, weaponBoundsOverlap = shape.bounds.Intersects(weaponBounds),
                                rendererLossyScale = shape.renderer.transform.lossyScale, scaleCompensatedSkin = shape.scaleCompensatedSkin, bakeWorldAgreementMax = shape.bakeWorldAgreementMax });
                            Render(preview, allPoints, frame, report.folder); report.frames.Add(frame);
                        }
                    }
                    finally
                    {
                        var errors = new List<Exception>();
                        foreach (var mesh in owned) Clean(() => { if (mesh) Object.DestroyImmediate(mesh); }, errors);
                        if (owned.Any(mesh => mesh)) errors.Add(new InvalidOperationException("Owned baked geometry remains."));
                        if (errors.Count > 0) throw new AggregateException("Baked geometry cleanup failed.", errors);
                    }
                    next++; if (next == ages.Length) break;
                }
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => motion?.Dispose(), errors);
                Clean(() => { if (anchor) TactileMaterials.Release(anchor); }, errors);
                Clean(() => held?.Dispose(), errors); Clean(() => avatar?.Dispose(), errors);
                Clean(() => { if (anchor) Object.DestroyImmediate(anchor); }, errors); Clean(() => preview?.Cleanup(), errors);
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, errors);
                Clean(() => RenderTexture.active = active, errors); Clean(() => SetSsao(ssao), errors);
                bool released = ownedScopes.All(scope => !scope) && !anchor && (avatar == null || !avatar.Root) && (held == null || !held.Root) && !camera &&
                    (lights == null || lights.All(l => !l)) && (!scene.IsValid() || !scene.isLoaded);
                report.ownedReleased &= released;
                if (!released) errors.Add(new InvalidOperationException("Owned holding preview objects remain."));
                if (errors.Count > 0) { if (failure != null) errors.Insert(0, failure); throw new AggregateException("Holding preview cleanup failed.", errors); }
            }
        }

        static void Render(PreviewRenderUtility preview, Vector3[] world, Frame frame, string folder)
        {
            Texture2D image = null; bool begun = false; var active = RenderTexture.active; Exception failure = null;
            try
            {
                preview.BeginStaticPreview(new Rect(0, 0, Pixels, Pixels)); begun = true;
                var camera = preview.camera; var center = new Vector3(0, 1.05f, 0);
                Vector3 direction = frame.view == "front" ? Vector3.forward : frame.view == "side" ? Vector3.right : Vector3.back;
                camera.transform.position = center + direction * 5; camera.transform.LookAt(center);
                camera.orthographic = true; camera.aspect = 1; camera.orthographicSize = 1.5f; camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.97f, .94f, .85f, 1); camera.allowHDR = false;
                frame.cameraPosition = camera.transform.position; frame.cameraEuler = camera.transform.eulerAngles; frame.orthographicSize = camera.orthographicSize;
                frame.allGeometryInsideFrame = world.All(v => { var p = camera.WorldToViewportPoint(v); return p.z > camera.nearClipPlane && p.z < camera.farClipPlane && p.x >= .01f && p.x <= .99f && p.y >= .01f && p.y <= .99f; });
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.lights[0].color = preview.lights[1].color = Color.white;
                preview.lights[0].intensity = .8f; preview.lights[1].intensity = .4f;
                preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(25, -25, 0);
                preview.lights[1].transform.rotation = camera.transform.rotation * Quaternion.Euler(10, 35, 0);
                SetSsao(false); preview.Render(true); preview.Render(true);
                frame.ambient = preview.ambientColor; frame.keyIntensity = preview.lights[0].intensity; frame.fillIntensity = preview.lights[1].intensity;
                image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width != Pixels || image.height != Pixels || image.GetPixels32().Select(c => c.r << 16 | c.g << 8 | c.b).Distinct().Take(128).Count() < 64)
                    throw new InvalidOperationException("Native image missing, malformed or blank.");
                frame.image = folder + "/" + frame.item.ToLowerInvariant() + "-" + frame.phase + "-" + frame.view + ".png";
                using (var file = new FileStream(frame.image, FileMode.CreateNew)) { var bytes = image.EncodeToPNG(); file.Write(bytes, 0, bytes.Length); }
                frame.sha256 = Hash(frame.image);
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => { if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); } }, errors);
                Clean(() => { if (image) Object.DestroyImmediate(image); }, errors);
                Clean(() => RenderTexture.active = active, errors);
                if (image) errors.Add(new InvalidOperationException("Owned capture image remains."));
                if (errors.Count > 0) { if (failure != null) errors.Insert(0, failure); throw new AggregateException("Holding image cleanup failed.", errors); }
            }
        }

        static List<Shape> Shapes(GameObject model, List<Mesh> owned)
        {
            var result = new List<Shape>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy))
            {
                Mesh mesh, scaledBake = null;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh { name = "Owned compensated pose " + renderer.name, hideFlags = HideFlags.HideAndDontSave }; owned.Add(mesh);
                    skin.BakeMesh(mesh, true);
                    scaledBake = new Mesh { name = "Owned scaled control " + renderer.name, hideFlags = HideFlags.HideAndDontSave }; owned.Add(scaledBake);
                    skin.BakeMesh(scaledBake, false);
                }
                else { var filter = renderer.GetComponent<MeshFilter>(); mesh = filter ? filter.sharedMesh : null; }
                if (!mesh) throw new InvalidOperationException("Geometry required: " + renderer.name);
                using var meshData = MeshUtility.AcquireReadOnlyMeshData(mesh);
                using var vertices = new NativeArray<Vector3>(meshData[0].vertexCount, Allocator.Temp);
                meshData[0].GetVertices(vertices);
                var points = new Vector3[vertices.Length];
                for (int i = 0; i < points.Length; i++) points[i] = renderer.transform.TransformPoint(vertices[i]);
                if (points.Length == 0 || points.Any(p => !AvatarSurfaceMeshData.Finite(p))) throw new InvalidOperationException("Invalid baked geometry.");
                float agreement = 0;
                if (scaledBake)
                {
                    using var controlData = MeshUtility.AcquireReadOnlyMeshData(scaledBake);
                    using var control = new NativeArray<Vector3>(controlData[0].vertexCount, Allocator.Temp);
                    controlData[0].GetVertices(control);
                    if (control.Length != points.Length) throw new InvalidOperationException("Scale-control vertex count changed.");
                    var rigid = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                    for (int i = 0; i < points.Length; i++)
                    {
                        Vector3 expected = rigid.MultiplyPoint3x4(control[i]);
                        if (!AvatarSurfaceMeshData.Finite(expected)) throw new InvalidOperationException("Nonfinite scaled-bake control.");
                        agreement = Mathf.Max(agreement, Vector3.Distance(points[i], expected));
                    }
                    if (agreement > .0001f) throw new InvalidOperationException("Baked coordinate mappings disagree: " + renderer.name + " delta=" + agreement);
                }
                var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                result.Add(new Shape { renderer = renderer, mesh = mesh, world = points, bounds = bounds,
                    scaleCompensatedSkin = scaledBake, bakeWorldAgreementMax = agreement });
            }
            if (result.Count == 0) throw new InvalidOperationException("No visible geometry.");
            return result;
        }
        static Bounds Combined(List<Shape> shapes) { var bounds = shapes[0].bounds; foreach (var shape in shapes.Skip(1)) bounds.Encapsulate(shape.bounds); return bounds; }
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static string Fingerprint(Object value) => value is Mesh mesh ? AvatarSurfaceMeshData.Fingerprint(mesh) : AvatarSurfaceMeshData.Hash(System.Text.Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(value)));
        static string Hash(string path) => AvatarSurfaceMeshData.Hash(File.ReadAllBytes(path));
        static void SetSsao(bool enabled) { if (enabled) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }
        static void Clean(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
    }
}
#endif
