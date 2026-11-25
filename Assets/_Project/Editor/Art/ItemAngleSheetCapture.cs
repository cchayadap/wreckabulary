using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class ItemAngleSheetCapture
    {
        const string OutputRoot = "BuildNotes/asset-review";
        const string ScriptPath = "Assets/_Project/Editor/Art/ItemAngleSheetCapture.cs";
        const int Pixels = 512, Gutter = 8, Columns = 3, Rows = 2;
        const float Margin = 1.12f, PlayerHeight = 1.8f, BarWidth = .06f, BarGap = .3f, EyeHeight = 1.6f, EyeDistance = 3f, EyeFieldOfView = 50f, EyeYaw = -25f;
        const float SunPitch = 48f, SunYawOffset = -32f, SunIntensity = 1.1f;
        static readonly Vector3 PartyOffset = new Vector3(0f, 30f, -22f);
        static readonly Color Background = new Color(.42f, .45f, .48f), Ambient = new Color(.45f, .45f, .45f), BarColor = new Color(.95f, .55f, .15f);
        static readonly Color32 SheetFill = new Color32(24, 26, 30, 255);
        static readonly string[] Views = { "front", "back", "left", "three-quarter-high", "overhead-party", "eye-level" };
        static readonly string[] ViewDefinitions =
        {
            "front: orthographic from +Z (Unity forward taken as the model front), eye level, 1.8 m reference bar beside the model",
            "back: orthographic from -Z, eye level",
            "left: orthographic from -X (the model's left when it faces +Z), eye level",
            "three-quarter-high: orthographic from front-left, 45 degrees yaw, 35 degrees elevation",
            "overhead-party: orthographic from the party camera offset (0, 30, -22) looking at the bounds centre",
            "eye-level: perspective, 50 degrees FOV, camera 1.6 m above the pivot plane and 3 m from the model centre at -25 degrees yaw, 1.8 m reference bar beside the model"
        };

        [Serializable] sealed class Source { public string id, source, category, family; public bool enabled; public Vector3 size; }
        [Serializable] sealed class Shot
        {
            public string view, image, projection, sha256; public Vector3 cameraPosition, cameraEuler, lightEuler;
            public float orthographicSize, fieldOfView, coverage; public bool reference, modelInFrame, referenceInFrame; public int bytes;
        }
        [Serializable] sealed class Item
        {
            public string modelKey, asset, folder, sheet, sheetSha256, status = "pending"; public string[] ids, sources, materials, shaders, warnings;
            public int renderers, triangles; public Vector3 boundsSize, boundsCenter, boundsMin;
            public List<Source> catalogue = new List<Source>(); public List<Shot> views = new List<Shot>();
        }
        [Serializable] sealed class Problem { public string modelKey, asset, reason; public string[] ids, sources; }
        [Serializable] sealed class FileProof { public string path, before, after; }
        [Serializable] sealed class MemoryProof { public string path, name, before, after; public bool dirtyBefore, dirtyAfter; }
        [Serializable] sealed class Report
        {
            public string utc, unity, graphics, colorSpace, pipeline, folder, status = "IN_PROGRESS", error = "", cleanupError = "", globalBefore, globalAfter, finish = Skin.Standard;
            public string scope = "Isolated Editor preview scene. Each model spawns through ModelVisual.Spawn (the finish named in 'finish', tactile material variants) as the game does, at yaw 0 and scale 1 on the pivot plane. " +
                "One key directional light at the game sun's 48 degree elevation and -32 degree yaw relative to each camera, flat ambient, no fill, no shadows, no post-processing. " +
                "Front, back, left, three-quarter and overhead share one orthographic size per item; the front widens only when the reference bar needs room. " +
                "WordboundPvP replacement meshes and WordboundVisualProfile overrides are not captured; their item ids use the original ModelLibrary artwork. No art approval is implied.";
            public int width = Pixels, height = Pixels, sheetWidth = Columns * Pixels + (Columns - 1) * Gutter, sheetHeight = Rows * Pixels + (Rows - 1) * Gutter, sheetColumns = Columns, sheetRows = Rows;
            public string[] sheetOrder = Views, viewDefinitions = ViewDefinitions, sourcesEnumerated;
            public Color background = Background, ambient = Ambient, keyColor = Color.white, referenceColor = BarColor;
            public float keyIntensity = SunIntensity, keyPitch = SunPitch, keyYawOffset = SunYawOffset, referenceHeight = PlayerHeight, referenceWidth = BarWidth;
            public Vector3 partyOffset = PartyOffset;
            public bool filesPreserved, memoryPreserved, scenesPreserved, globalsPreserved, ownedReleased = true;
            public int filesChecked, memoryChecked, tactileScopesBefore, tactileScopesAfter, tactileMaterialsBefore, tactileMaterialsAfter, tactileTexturesBefore, tactileTexturesAfter;
            public List<Item> items = new List<Item>();
            public List<Problem> missing = new List<Problem>(), invalid = new List<Problem>(), skipped = new List<Problem>();
            public List<FileProof> changedFiles = new List<FileProof>(); public List<MemoryProof> changedMemory = new List<MemoryProof>();
        }
        sealed class Candidate { public string key; public readonly List<string> ids = new List<string>(), sources = new List<string>(); public readonly List<Source> catalogue = new List<Source>(); }
        sealed class Plan { public string view; public bool orthographic, reference; public Vector3 position, target; public float size, near, far; public Bounds bar; }

        [MenuItem("Wreckabulary/Art/Capture item angle sheets")]
        public static void Run() => Capture(Skin.Standard);

        [MenuItem("Wreckabulary/Art/Capture item angle sheets (Candy)")]
        public static void RunCandy() => Capture("Candy");

        [MenuItem("Wreckabulary/Art/Capture item angle sheets (Arcade)")]
        public static void RunArcade() => Capture("Arcade");

        [MenuItem("Wreckabulary/Art/Capture item angle sheets (Winter)")]
        public static void RunWinter() => Capture("Winter");

        public static void RunAllFinishes()
        {
            foreach (var finish in Finishes.All) Capture(finish.Id);
        }

        static void Capture(string finish)
        {
            if (Finishes.Find(finish) == null) throw new ArgumentException("Unknown finish " + finish);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture item angle sheets in Edit Mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Item angle sheets need a graphics device; do not run with -nographics.");
            if (!GraphicsSettings.currentRenderPipeline) throw new InvalidOperationException("Item angle sheets need the active render pipeline.");
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset) throw new InvalidOperationException("Item angle sheets expect the game's URP pipeline asset to be active.");
            var library = ModelLibrary.Load(); var materials = MaterialLibrary.Load(); var data = Resources.Load<GameData>(GameData.ResourcePath); var equipment = WordboundEquipmentCollection.Load();
            if (!library || !materials || !data) throw new InvalidOperationException("Resources ModelLibrary, MaterialLibrary and GameData are required; run Wreckabulary > Art > Set Up Imported Art and Data > Set Up Game Data.");
            string relative = OutputRoot + "/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + (finish == Skin.Standard ? "" : "-" + finish.ToLowerInvariant()), folder = Full(relative);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new IOException("Preserve existing item angle sheets: " + folder);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, folder = relative,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName, colorSpace = QualitySettings.activeColorSpace.ToString(),
                pipeline = AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), finish = finish };
            var candidates = Collect(library, data, equipment, report);
            if (finish != Skin.Standard) candidates = Wearing(candidates, finish, report);
            var settings = new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "ProjectSettings/EditorSettings.asset" };
            var inputs = new[] { AssetDatabase.GetAssetPath(library), AssetDatabase.GetAssetPath(materials), AssetDatabase.GetAssetPath(data),
                equipment ? AssetDatabase.GetAssetPath(equipment) : "", report.pipeline, ScriptPath }.Where(path => !string.IsNullOrEmpty(path)).ToArray();
            var dependencies = AssetDatabase.GetDependencies(inputs, true).Concat(inputs).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var files = dependencies.Concat(settings).SelectMany(path => new[] { path, path + ".meta" }).Where(path => File.Exists(Full(path)))
                .Distinct(StringComparer.Ordinal).Select(path => new FileProof { path = path, before = FileHash(path) }).ToList();
            var memory = new List<(Object value, MemoryProof proof)>(); var seen = new HashSet<Object>();
            foreach (string path in dependencies.Where(dependency => dependency.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null || typeof(Texture).IsAssignableFrom(type) || typeof(AudioClip).IsAssignableFrom(type) || typeof(MonoScript).IsAssignableFrom(type) || typeof(Shader).IsAssignableFrom(type)) continue;
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path).Where(asset => asset is Material || asset is ScriptableObject || asset is GameObject root && !root.transform.parent))
                    if (seen.Add(value)) memory.Add((value, new MemoryProof { path = path, name = value.name, before = Fingerprint(value), dirtyBefore = EditorUtility.IsDirty(value) }));
            }
            report.filesChecked = files.Count; report.memoryChecked = memory.Count;
            string scenes = SceneState(); report.globalBefore = Global();
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite, async = EditorSettings.asyncShaderCompilation, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION");
            report.tactileScopesBefore = TactileMaterials.RegisteredScopeCount; report.tactileMaterialsBefore = TactileMaterials.SharedMaterialCount; report.tactileTexturesBefore = TactileMaterials.GeneratedTextureCount;
            PreviewRenderUtility preview = null; Scene scene = default; Camera camera = null; Light[] lights = null; GameObject bar = null; Mesh barMesh = null; Material barMaterial = null;
            Exception failure = null; Directory.CreateDirectory(folder);
            try
            {
                EditorSettings.asyncShaderCompilation = false;
                preview = new PreviewRenderUtility(); camera = preview.camera; lights = preview.lights.ToArray(); scene = camera.gameObject.scene;
                if (!EditorSceneManager.IsPreviewScene(scene)) throw new InvalidOperationException("Expected an owned preview scene.");
                preview.ambientColor = Ambient;
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (!unlit || !unlit.isSupported) throw new InvalidOperationException("The URP Unlit shader is required for the height reference.");
                barMaterial = new Material(unlit) { name = "Player height reference", hideFlags = HideFlags.HideAndDontSave }; barMaterial.SetColor("_BaseColor", BarColor);
                barMesh = Box();
                bar = EditorUtility.CreateGameObjectWithHideFlags("Player height reference", HideFlags.HideAndDontSave, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(bar, scene);
                bar.GetComponent<MeshFilter>().sharedMesh = barMesh;
                var barRenderer = bar.GetComponent<MeshRenderer>(); barRenderer.sharedMaterial = barMaterial; barRenderer.shadowCastingMode = ShadowCastingMode.Off; barRenderer.receiveShadows = false;
                bar.SetActive(false);
                var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var candidate in candidates) Capture(candidate, library, preview, scene, bar, relative, slugs, report, finish);
                report.status = "CAPTURED_NOT_VISUALLY_APPROVED";
            }
            catch (Exception error) { failure = error; report.status = "FAIL"; report.error = error.ToString(); }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => { if (bar) Object.DestroyImmediate(bar); }, errors);
                Clean(() => { if (barMesh) Object.DestroyImmediate(barMesh); }, errors);
                Clean(() => { if (barMaterial) Object.DestroyImmediate(barMaterial); }, errors);
                Clean(() => preview?.Cleanup(), errors);
                Clean(() => { if (scene.IsValid() && scene.isLoaded && EditorSceneManager.IsPreviewScene(scene)) EditorSceneManager.ClosePreviewScene(scene); }, errors);
                Clean(() => EditorSettings.asyncShaderCompilation = async, errors);
                Clean(() => RenderTexture.active = active, errors); Clean(() => GL.sRGBWrite = srgb, errors); Clean(() => SetSsao(ssao), errors);
                report.ownedReleased &= !bar && !barMesh && !barMaterial && !camera && (lights == null || lights.All(light => !light)) && (!scene.IsValid() || !scene.isLoaded);
                report.globalAfter = Global(); report.globalsPreserved = report.globalBefore == report.globalAfter;
                report.scenesPreserved = SceneState() == scenes;
                foreach (var proof in files)
                {
                    Clean(() => proof.after = File.Exists(Full(proof.path)) ? FileHash(proof.path) : "MISSING", errors);
                    if (proof.before != proof.after) report.changedFiles.Add(proof);
                }
                foreach (var (value, proof) in memory)
                {
                    Clean(() => { proof.after = value ? Fingerprint(value) : "DESTROYED"; proof.dirtyAfter = value && EditorUtility.IsDirty(value); }, errors);
                    if (proof.before != proof.after || proof.dirtyBefore != proof.dirtyAfter) report.changedMemory.Add(proof);
                }
                report.filesPreserved = report.changedFiles.Count == 0; report.memoryPreserved = report.changedMemory.Count == 0;
                report.tactileScopesAfter = TactileMaterials.RegisteredScopeCount; report.tactileMaterialsAfter = TactileMaterials.SharedMaterialCount; report.tactileTexturesAfter = TactileMaterials.GeneratedTextureCount;
                report.cleanupError = string.Join("\n", errors.Select(error => error.ToString()));
                if (errors.Count > 0 || !Preserved(report)) report.status = "FAIL: preservation or cleanup";
                WriteNew(Path.Combine(folder, "index.json"), Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true)));
            }
            string index = relative + "/index.json";
            if (failure != null) throw new InvalidOperationException("Item angle sheet capture failed; evidence kept at " + index, failure);
            if (report.cleanupError.Length != 0 || !Preserved(report)) throw new InvalidOperationException("Item angle sheet preservation or cleanup failed; inspect " + index + "\n" + report.cleanupError);
            string summary = "[Wreckabulary] Item angle sheets " + report.status + ": finish=" + finish + " items=" + report.items.Count + " missing=" + report.missing.Count + " invalid=" + report.invalid.Count +
                " skipped=" + report.skipped.Count + " index=" + index;
            if (report.missing.Count + report.invalid.Count > 0) Debug.LogWarning(summary); else Debug.Log(summary);
        }

        static List<Candidate> Collect(ModelLibrary library, GameData data, WordboundEquipmentCollection equipment, Report report)
        {
            var found = new Dictionary<string, Candidate>(StringComparer.Ordinal);
            void Add(string key, string id, string source, ItemDefinition item = null)
            {
                if (!found.TryGetValue(key, out var candidate)) found.Add(key, candidate = new Candidate { key = key });
                if (!candidate.ids.Contains(id)) candidate.ids.Add(id);
                if (!candidate.sources.Contains(source)) candidate.sources.Add(source);
                if (item != null) candidate.catalogue.Add(new Source { id = item.Id, source = source, enabled = item.Enabled, category = item.Category.ToString(), family = item.Family.ToString(),
                    size = item.Size != null && item.Size.Length == 3 ? new Vector3(item.Size[0], item.Size[1], item.Size[2]) : Vector3.zero });
            }
            var sources = new List<string>();
            sources.Add("ModelLibrary: every Items/<WORD> key in " + AssetDatabase.GetAssetPath(library));
            foreach (var entry in library.Entries)
                if (!string.IsNullOrEmpty(entry.key) && entry.key.StartsWith("Items/", StringComparison.Ordinal)) Add(entry.key, entry.key.Substring(6), "ModelLibrary");
            var specs = typeof(FurnitureCatalog).GetField("Specs", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IDictionary;
            if (specs == null) throw new MissingFieldException("FurnitureCatalog.Specs changed; review this capture's furniture source.");
            sources.Add("FurnitureCatalog: spawn words, resolved like LetterBuilt (RUG uses Environment/Round_Rug)");
            foreach (string word in specs.Keys) Add(FurnitureKey(word), word.ToUpperInvariant(), "FurnitureCatalog");
            var config = data.Parse();
            foreach (var house in config.Houses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                sources.Add("house_" + house.Key + ".json: furniture placements");
                foreach (var placement in house.Value.Furniture)
                    if (!string.IsNullOrEmpty(placement.Word)) Add(FurnitureKey(placement.Word), placement.Word.ToUpperInvariant(), "house_" + house.Key + ".json");
            }
            foreach (var asset in new[] { data.items, data.houseItems })
            {
                if (!asset) continue;
                string label = Path.GetFileName(AssetDatabase.GetAssetPath(asset)); sources.Add(label + ": item catalogue via ItemCatalogue.FromJson");
                foreach (var item in ItemCatalogue.FromJson(asset.text, label).All)
                {
                    if (!string.IsNullOrEmpty(item.Model)) { Add(item.Model, item.Id, label, item); continue; }
                    var problem = new Problem { modelKey = "", ids = new[] { item.Id }, sources = new[] { label }, reason = item.Enabled ? "Enabled catalogue item has no model key." : "Disabled catalogue entry has no model key." };
                    (item.Enabled ? report.missing : report.skipped).Add(problem);
                }
            }
            if (equipment)
            {
                sources.Add("Wordbound original equipment: " + AssetDatabase.GetAssetPath(equipment) + " item ids whose model keys resolve through ModelLibrary");
                foreach (var entry in equipment.Entries.Where(row => row != null && !string.IsNullOrEmpty(row.modelKey)))
                {
                    if (library.Find(entry.modelKey)) { Add(entry.modelKey, entry.itemId, "Wordbound original equipment"); continue; }
                    report.skipped.Add(new Problem { modelKey = entry.modelKey, asset = entry.model ? AssetDatabase.GetAssetPath(entry.model) : "", ids = new[] { entry.itemId },
                        sources = new[] { "Wordbound original equipment" }, reason = "Key does not resolve through ModelLibrary; replacement-only artwork is not captured." });
                }
            }
            report.sourcesEnumerated = sources.ToArray();
            var result = new List<Candidate>();
            foreach (var candidate in found.Values.OrderBy(value => value.key, StringComparer.Ordinal))
            {
                var model = library.Find(candidate.key); string asset = model ? AssetDatabase.GetAssetPath(model) : "";
                var problem = new Problem { modelKey = candidate.key, asset = asset, ids = candidate.ids.OrderBy(id => id, StringComparer.Ordinal).ToArray(), sources = candidate.sources.ToArray() };
                if (Belt(candidate, asset)) { problem.reason = "BELT collection is excluded from this review."; report.skipped.Add(problem); continue; }
                if (!model)
                {
                    problem.reason = library.Entries.Any(entry => entry.key == candidate.key) ? "ModelLibrary entry has no model asset." :
                        "No ModelLibrary entry; furniture falls back to letter blocks and Wordbound cannot resolve it.";
                    report.missing.Add(problem); continue;
                }
                result.Add(candidate);
            }
            return result;
        }

        static List<Candidate> Wearing(List<Candidate> candidates, string finish, Report report)
        {
            var items = GameConfig.Current.Items;
            var kept = new List<Candidate>();
            foreach (var candidate in candidates)
            {
                bool wears = candidate.ids.Any(id => items.TryGet(id, out var item) && Finishes.ForItem(item.Id, item.Skins).Any(f => f.Id == finish));
                if (wears) { kept.Add(candidate); continue; }
                report.skipped.Add(new Problem { modelKey = candidate.key, ids = candidate.ids.ToArray(), sources = candidate.sources.ToArray(), reason = "No catalogue item with this model lists the " + finish + " finish." });
            }
            return kept;
        }

        static void Capture(Candidate candidate, ModelLibrary library, PreviewRenderUtility preview, Scene scene, GameObject bar, string relative, HashSet<string> slugs, Report report, string finish)
        {
            string slug = Slug(candidate.key, slugs);
            var item = new Item { modelKey = candidate.key, asset = AssetDatabase.GetAssetPath(library.Find(candidate.key)), folder = relative + "/" + slug,
                ids = candidate.ids.OrderBy(id => id, StringComparer.Ordinal).ToArray(), sources = candidate.sources.ToArray(), catalogue = candidate.catalogue.ToList() };
            GameObject anchor = null; Exception failure = null; var warnings = new List<string>();
            try
            {
                anchor = EditorUtility.CreateGameObjectWithHideFlags("Item angle sheet " + candidate.key, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(anchor, scene);
                var model = ModelVisual.SpawnItem(candidate.key, anchor.transform, finish);
                if (!model || model.scene != scene) throw new InvalidOperationException("The model did not spawn into the owned preview scene.");
                var renderers = model.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("The model has no visible renderers.");
                var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (!Finite(bounds.center) || !Finite(bounds.size) || bounds.size.sqrMagnitude <= 0f || bounds.size.magnitude > 50f)
                    throw new InvalidOperationException("The model bounds are degenerate, non-finite or larger than 50 m: " + bounds.size.ToString("F3"));
                var shared = renderers.SelectMany(renderer => renderer.sharedMaterials).ToArray();
                item.renderers = renderers.Length; item.triangles = renderers.Sum(Triangles);
                item.boundsSize = bounds.size; item.boundsCenter = bounds.center; item.boundsMin = bounds.min;
                item.materials = shared.Select(material => material ? material.name : "MISSING").Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                item.shaders = shared.Where(material => material).Select(material => material.shader ? material.shader.name : "MISSING").Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
                if (shared.Any(material => !material)) warnings.Add("A renderer has an empty material slot.");
                if (shared.Any(material => material && (!material.shader || !material.shader.isSupported || material.shader.name.Contains("InternalErrorShader")))) warnings.Add("A material uses a missing, unsupported or error shader.");
                if (bounds.min.y < -.01f) warnings.Add("Geometry extends " + (-bounds.min.y).ToString("F3") + " m below the pivot plane.");
                if (bounds.min.y > .05f) warnings.Add("Geometry starts " + bounds.min.y.ToString("F3") + " m above the pivot plane.");
                string folder = Full(item.folder); Directory.CreateDirectory(folder);
                var images = new List<Color32[]>();
                foreach (var plan in Plans(preview.camera.transform, bounds))
                {
                    var shot = Render(preview, plan, bounds, bar, folder, item.folder, out var pixels);
                    item.views.Add(shot); images.Add(pixels);
                    if (!shot.modelInFrame) warnings.Add(plan.view + ": the model extends beyond the frame.");
                    if (plan.reference && !shot.referenceInFrame) warnings.Add(plan.view + ": the height reference extends beyond the frame.");
                    if (shot.coverage < .0005f) warnings.Add(plan.view + ": the view is nearly empty.");
                }
                var sheet = Sheet(images); item.sheet = item.folder + "/sheet.png"; item.sheetSha256 = Hash(sheet);
                WriteNew(Path.Combine(folder, "sheet.png"), sheet);
                item.warnings = warnings.ToArray(); item.status = warnings.Count == 0 ? "captured" : "captured-with-warnings";
                report.items.Add(item);
            }
            catch (Exception error) when (error is not (IOException or UnauthorizedAccessException))
            {
                report.invalid.Add(new Problem { modelKey = candidate.key, asset = item.asset, ids = item.ids, sources = item.sources,
                    reason = error.GetType().Name + ": " + error.Message + (item.views.Count > 0 ? " (" + item.views.Count + " views written before the failure)" : "") });
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                var errors = new List<Exception>();
                Clean(() => bar.SetActive(false), errors);
                Clean(() => { if (anchor) TactileMaterials.Release(anchor); }, errors);
                Clean(() => { if (anchor) Object.DestroyImmediate(anchor); }, errors);
                if (anchor) { report.ownedReleased = false; errors.Add(new InvalidOperationException("Owned preview model remains: " + candidate.key)); }
                if (errors.Count > 0) { if (failure != null) errors.Insert(0, failure); throw new AggregateException("Item preview cleanup failed: " + candidate.key, errors); }
            }
        }

        static List<Plan> Plans(Transform pose, Bounds model)
        {
            float reach = Mathf.Max(model.size.magnitude, 4f), distance = reach * 2f + 2f;
            var party = PartyOffset * Mathf.Max(1f, distance / PartyOffset.magnitude);
            var views = new (string name, Vector3 offset)[] { ("front", Vector3.forward * distance), ("back", Vector3.back * distance), ("left", Vector3.left * distance),
                ("three-quarter-high", Quaternion.Euler(-35f, -45f, 0f) * Vector3.forward * distance), ("overhead-party", party) };
            float size = views.Max(view => Frame(pose, model.center, view.offset, model)) * Margin;
            var plans = views.Select(view => new Plan { view = view.name, orthographic = true, position = model.center + view.offset, target = model.center,
                size = size, near = .05f, far = view.offset.magnitude + reach * 2f + 4f }).ToList();
            var front = plans[0]; front.reference = true; front.bar = Reference(model, Quaternion.LookRotation(Vector3.back) * Vector3.right);
            var union = model; union.Encapsulate(front.bar);
            front.target = union.center; front.position = union.center + views[0].offset; front.size = Mathf.Max(size, Frame(pose, union.center, views[0].offset, union) * Margin);
            var ground = new Vector3(model.center.x, 0f, model.center.z);
            var eye = new Plan { view = "eye-level", reference = true, near = .05f, far = 50f,
                position = ground + Quaternion.Euler(0f, EyeYaw, 0f) * Vector3.forward * EyeDistance + Vector3.up * EyeHeight,
                target = ground + Vector3.up * ((Mathf.Min(0f, model.min.y) + Mathf.Max(model.max.y, PlayerHeight)) * .5f) };
            eye.bar = Reference(model, Quaternion.LookRotation(eye.target - eye.position) * Vector3.right);
            plans.Add(eye);
            return plans;
        }

        static float Frame(Transform pose, Vector3 target, Vector3 offset, Bounds bounds)
        {
            pose.position = target + offset; pose.LookAt(target);
            float frame = .05f;
            foreach (var corner in Corners(bounds)) { var local = pose.InverseTransformPoint(corner); frame = Mathf.Max(frame, Mathf.Abs(local.x), Mathf.Abs(local.y)); }
            return frame;
        }

        static Bounds Reference(Bounds model, Vector3 right)
        {
            var side = new Vector3(right.x, 0f, right.z).normalized;
            float reach = Mathf.Abs(side.x) * model.extents.x + Mathf.Abs(side.z) * model.extents.z + BarGap + BarWidth * .5f;
            return new Bounds(new Vector3(model.center.x, PlayerHeight * .5f, model.center.z) + side * reach, new Vector3(BarWidth, PlayerHeight, BarWidth));
        }

        static Shot Render(PreviewRenderUtility preview, Plan plan, Bounds model, GameObject bar, string folder, string relative, out Color32[] pixels)
        {
            Texture2D image = null; bool begun = false; var active = RenderTexture.active; pixels = null;
            var shot = new Shot { view = plan.view, image = relative + "/" + plan.view + ".png", projection = plan.orthographic ? "orthographic" : "perspective", reference = plan.reference };
            try
            {
                if (plan.reference) { bar.transform.SetPositionAndRotation(plan.bar.center, Quaternion.identity); bar.transform.localScale = plan.bar.size; }
                bar.SetActive(plan.reference);
                preview.BeginStaticPreview(new Rect(0, 0, Pixels, Pixels)); begun = true;
                var camera = preview.camera;
                camera.aspect = 1f; camera.orthographic = plan.orthographic; camera.orthographicSize = plan.orthographic ? plan.size : 1f; camera.fieldOfView = EyeFieldOfView;
                camera.nearClipPlane = plan.near; camera.farClipPlane = plan.far; camera.cullingMask = ~0;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background; camera.allowHDR = false; camera.allowMSAA = false;
                camera.transform.position = plan.position; camera.transform.LookAt(plan.target);
                var key = preview.lights[0];
                key.color = Color.white; key.intensity = SunIntensity; key.shadows = LightShadows.None; key.cullingMask = ~0;
                key.transform.rotation = Quaternion.Euler(SunPitch, camera.transform.eulerAngles.y + SunYawOffset, 0f);
                foreach (var light in preview.lights.Skip(1)) light.intensity = 0f;
                shot.cameraPosition = camera.transform.position; shot.cameraEuler = camera.transform.eulerAngles; shot.lightEuler = key.transform.eulerAngles;
                shot.orthographicSize = plan.orthographic ? plan.size : 0f; shot.fieldOfView = plan.orthographic ? 0f : EyeFieldOfView;
                shot.modelInFrame = Inside(camera, Corners(model)); shot.referenceInFrame = plan.reference && Inside(camera, Corners(plan.bar));
                SetSsao(false); preview.Render(true, false); preview.Render(true, false);
                image = preview.EndStaticPreview(); begun = false;
                if (!image || image.width != Pixels || image.height != Pixels) throw new InvalidOperationException("Native capture has the wrong size: " + plan.view);
                pixels = image.GetPixels32();
                var background = pixels[0]; int covered = 0;
                foreach (var pixel in pixels) if (Math.Abs(pixel.r - background.r) > 6 || Math.Abs(pixel.g - background.g) > 6 || Math.Abs(pixel.b - background.b) > 6) covered++;
                shot.coverage = covered / (float)pixels.Length;
                var bytes = image.EncodeToPNG(); shot.bytes = bytes.Length; shot.sha256 = Hash(bytes);
                WriteNew(Path.Combine(folder, plan.view + ".png"), bytes);
                return shot;
            }
            finally
            {
                if (begun) { var pending = preview.EndStaticPreview(); if (pending) Object.DestroyImmediate(pending); }
                if (image) Object.DestroyImmediate(image);
                RenderTexture.active = active; bar.SetActive(false);
            }
        }

        static byte[] Sheet(List<Color32[]> images)
        {
            int width = Columns * Pixels + (Columns - 1) * Gutter, height = Rows * Pixels + (Rows - 1) * Gutter;
            var canvas = new Color32[width * height]; for (int i = 0; i < canvas.Length; i++) canvas[i] = SheetFill;
            for (int index = 0; index < images.Count && index < Columns * Rows; index++)
            {
                int x = index % Columns * (Pixels + Gutter), y = (Rows - 1 - index / Columns) * (Pixels + Gutter);
                for (int row = 0; row < Pixels; row++) Array.Copy(images[index], row * Pixels, canvas, (y + row) * width + x, Pixels);
            }
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(width, height, TextureFormat.RGB24, false, false) { hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(canvas); texture.Apply(false); return texture.EncodeToPNG();
            }
            finally { if (texture) Object.DestroyImmediate(texture); }
        }

        static Mesh Box()
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up); var bitangent = Vector3.Cross(normal, tangent); int start = vertices.Count;
                foreach (var corner in new[] { -tangent - bitangent, tangent - bitangent, tangent + bitangent, -tangent + bitangent }) { vertices.Add((normal + corner) * .5f); normals.Add(normal); }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = "Player height reference", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }

        static int Triangles(Renderer renderer)
        {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
            else { var filter = renderer.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
            if (!mesh) return 0;
            long total = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var sub = mesh.GetSubMesh(i);
                total += sub.topology == MeshTopology.Triangles ? sub.indexCount / 3 : sub.topology == MeshTopology.Quads ? sub.indexCount / 2 : 0;
            }
            return (int)Math.Min(total, int.MaxValue);
        }

        static bool Belt(Candidate candidate, string asset) => candidate.ids.Contains("BELT") || candidate.key.EndsWith("/BELT", StringComparison.Ordinal) ||
            asset.IndexOf("/Collections/BeltV1/", StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(Path.GetFileNameWithoutExtension(asset), "BELT", StringComparison.OrdinalIgnoreCase);
        static string FurnitureKey(string word) { word = word.ToUpperInvariant(); return word == "RUG" ? "Environment/Round_Rug" : "Items/" + word; }
        static string Slug(string key, HashSet<string> used)
        {
            string slug = new string(key.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '-').ToArray()), result = slug;
            for (int i = 2; !used.Add(result); i++) result = slug + "-" + i;
            return result;
        }
        static bool Inside(Camera camera, IEnumerable<Vector3> points) => points.All(point => { var view = camera.WorldToViewportPoint(point);
            return view.z > camera.nearClipPlane && view.z < camera.farClipPlane && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f; });
        static IEnumerable<Vector3> Corners(Bounds bounds)
        { for (int i = 0; i < 8; i++) yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)); }
        static bool Preserved(Report report) => report.filesPreserved && report.memoryPreserved && report.scenesPreserved && report.globalsPreserved && report.ownedReleased &&
            report.tactileScopesAfter == report.tactileScopesBefore;
        static string Global() => string.Join("|", new object[] {
            RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
            RenderSettings.ambientIntensity, RenderSettings.reflectionIntensity, RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode,
            RenderSettings.skybox ? RenderSettings.skybox.GetEntityId().ToString() : "", RenderSettings.sun ? RenderSettings.sun.GetEntityId().ToString() : "",
            AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline), RenderTexture.active ? RenderTexture.active.GetEntityId().ToString() : "",
            GL.sRGBWrite, EditorSettings.asyncShaderCompilation, Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"), QualitySettings.GetQualityLevel(), QualitySettings.activeColorSpace });
        static string SceneState() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
            var s = SceneManager.GetSceneAt(i); return s.handle.GetRawData() + ":" + s.path + ":" + s.isLoaded + ":" + s.isDirty + ":" + (s == SceneManager.GetActiveScene()) + ":" +
                (s.isLoaded ? string.Join(",", s.GetRootGameObjects().Select(o => o.GetEntityId().ToString()).OrderBy(id => id, StringComparer.Ordinal)) : "unloaded"); }));
        static string Fingerprint(Object value) => Hash(Encoding.UTF8.GetBytes(EditorJsonUtility.ToJson(value)));
        static void SetSsao(bool enabled) { if (enabled) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION"); }
        static void Clean(Action action, List<Exception> errors) { try { action(); } catch (Exception error) { errors.Add(error); } }
        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        static string FileHash(string path) => Hash(File.ReadAllBytes(Full(path)));
        static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        static void WriteNew(string path, byte[] bytes) { using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes, 0, bytes.Length); }
    }
}
