#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Qa;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Online;
using Wreckabulary.Rules;

namespace Wreckabulary.Qa
{
    public sealed class WordboundPerformanceWorkload : MonoBehaviour
    {
        public static readonly string[] Equipment = { "BAT", "BROOM", "HAMMER", "SPEAR", "SHIELD", "PLATE", "FAN", "CLOCK" };
        readonly List<WordboundVisualInstance> actors = new List<WordboundVisualInstance>();
        readonly List<WordboundVisualInstance> held = new List<WordboundVisualInstance>();
        readonly List<WordboundAvatarMotion> motions = new List<WordboundAvatarMotion>();
        readonly List<Transform> anchors = new List<Transform>();
        readonly Dictionary<string, string> savedPrefs = new Dictionary<string, string>();
        readonly List<string> absentPrefs = new List<string>();
        readonly List<Dictionary<string, object>> artwork = new List<Dictionary<string, object>>();
        WordboundGameplayPresentation presentation;
        WordboundVisualResolver resolver;
        WordboundPlayerView view;
        WordboundMatchHud hud;
        Camera camera;
        Vector3 center, previousCameraPosition;
        Quaternion previousCameraRotation;
        float previousFov;
        int previousVsync, previousWidth, previousHeight;
        FullScreenMode previousScreenMode;
        bool settingsCaptured, restored, viewEnabled, hudEnabled, viewStateCaptured;
        string label;
        public bool Animate { get; set; }
        public bool Ready { get; private set; }
        public float Elapsed { get; private set; }
        public string ModuleId { get; private set; }
        public int SyntheticActors => actors.Count;
        public int HeldItems => held.Count;
        public int RendererCount { get; private set; }
        public long ItemTriangles { get; private set; }
        public string BaseArtworkSource { get; private set; }
        public string VisualProfileName { get; private set; }
        public IReadOnlyList<Dictionary<string, object>> Artwork => artwork.AsReadOnly();

        public void ConfigureDisplay()
        {
            if (settingsCaptured) throw new InvalidOperationException("QA graphics settings were already captured.");
            foreach (var key in GraphicsOptions.Keys)
                if (PlayerPrefs.HasKey(key)) savedPrefs.Add(key, PlayerPrefs.GetString(key)); else absentPrefs.Add(key);
            previousVsync = QualitySettings.vSyncCount;
            previousWidth = Screen.width; previousHeight = Screen.height; previousScreenMode = Screen.fullScreenMode;
            settingsCaptured = true;
            try
            {
                GraphicsOptions.UsePreset("Low");
                GraphicsOptions.SetFrameCap(0);
            }
            finally { RestorePreferenceBytes(); }
            QualitySettings.vSyncCount = 0;
            Screen.SetResolution(1920, 1080, false);
        }

        void RestorePreferenceBytes()
        {
            foreach (var pair in savedPrefs) PlayerPrefs.SetString(pair.Key, pair.Value);
            foreach (var key in absentPrefs) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        public void Prepare(WordboundLocalMatchClient client, ItemCatalogue items)
        {
            if (Ready || !client || !client.Gameplay || !client.Gameplay.CourseWorld)
                throw new QaFailException("The exact local flagship must be running before creating the synthetic workload.");
            presentation = client.GetComponent<WordboundGameplayPresentation>();
            resolver = presentation ? presentation.ArtworkResolver : null;
            RequireArtwork();
            BaseArtworkSource = resolver.UseWordboundCollection ? "wordbound_equipment" : "original_wreckabulary";
            VisualProfileName = resolver.Profile ? resolver.Profile.name : "(none; legacy caller policy)";
            var world = client.Gameplay.CourseWorld;
            var module = world.Course.Geometry.Where(m => m.Bounds.Max.X - m.Bounds.Min.X >= 18000 &&
                m.Bounds.Max.Z - m.Bounds.Min.Z >= 16000).OrderByDescending(m =>
                (long)(m.Bounds.Max.X - m.Bounds.Min.X) * (m.Bounds.Max.Z - m.Bounds.Min.Z))
                .ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
            if (module == null)
                throw new QaFailException("The flagship does not have the expected room for the fixed synthetic camera workload.");
            ModuleId = module.Id;
            center = world.ToWorld(new Rules.Wordbound.CoursePoint(
                (module.Bounds.Min.X + module.Bounds.Max.X) / 2, module.Bounds.Min.Y,
                (module.Bounds.Min.Z + module.Bounds.Max.Z) / 2));
            view = client.GetComponent<WordboundPlayerView>(); hud = client.GetComponent<WordboundMatchHud>();
            if (!view || !view.Camera || !hud) throw new QaFailException("The actual local player camera and HUD are required.");
            camera = view.Camera; previousCameraPosition = camera.transform.position; previousCameraRotation = camera.transform.rotation;
            previousFov = camera.fieldOfView; viewEnabled = view.enabled; hudEnabled = hud.enabled;
            viewStateCaptured = true;
            view.enabled = false; hud.enabled = false;
            camera.fieldOfView = 60; GraphicsOptions.ApplyTo(camera);
            for (int i = 0; i < Equipment.Length; i++)
            {
                var anchor = new GameObject("Synthetic render actor " + i).transform;
                anchor.SetParent(transform, false); anchors.Add(anchor);
                var actor = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor);
                actors.Add(actor); actor.Dress(""); motions.Add(new WordboundAvatarMotion(actor));
                var item = items.Get(Equipment[i]);
                string skin = resolver.SkinFor(Equipment[i], actor.Outfit);
                var equipment = resolver.Create(Equipment[i], new Vector3(item.Size[0], item.Size[1], item.Size[2]), anchor, skin);
                held.Add(equipment);
                ObserveArtwork(Equipment[i], skin, equipment);
            }
            RendererCount = GetComponentsInChildren<Renderer>(true).Length;
            label = "SYNTHETIC RENDER TEST | 8 animated actors + 8 held items | local world actors: " +
                client.Gameplay.Frame.Actors.Count + " | remote peers: 0";
            Ready = true; SetPose(0, 0); Animate = true;
        }

        public void RequireArtwork()
        {
            if (!presentation || presentation.Fault != "" || resolver == null ||
                !ReferenceEquals(presentation.ArtworkResolver, resolver))
                throw new QaFailException("The synthetic artwork must use the initialized resolver of the running local world.");
        }

        void ObserveArtwork(string id, string skin, WordboundVisualInstance visual)
        {
            var replacement = resolver.Profile?.Overrides?.SingleOrDefault(candidate => candidate != null && candidate.VisualKey == id);
            string modelKey = replacement == null ? resolver.ModelKey(id) : "";
            var collection = replacement == null && resolver.UseWordboundCollection && skin == Skin.Standard
                ? WordboundEquipmentCollection.Load() : null;
            var entry = collection ? collection.FindByModelKey(modelKey) : null;
            GameObject source = replacement != null ? replacement.Model : entry != null ? entry.model : ModelLibrary.Load()?.Find(modelKey);
            string sourceKind = replacement != null ? "profile_override" : entry != null ? "wordbound_equipment" : "original_model_library";
            if (!source) throw new QaFailException("The selected artwork source is missing for " + id + ".");
            var meshes = Meshes(visual.Model);
            if (meshes.Length == 0 || meshes.Any(mesh => !mesh) || !Meshes(source).SequenceEqual(meshes))
                throw new QaFailException("The measured " + id + " did not use its selected " + sourceKind + " mesh assets.");
            var meshFacts = new List<object>();
            foreach (var mesh in meshes)
            {
                long triangles = 0;
                var submeshes = new List<object>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    long indices = mesh.GetIndexCount(sub);
                    var topology = mesh.GetTopology(sub);
                    if (topology == MeshTopology.Triangles) triangles += indices / 3;
                    submeshes.Add(new Dictionary<string, object> { ["topology"] = topology.ToString(), ["index_count"] = indices });
                }
                ItemTriangles += triangles;
                meshFacts.Add(new Dictionary<string, object> {
                    ["name"] = mesh.name, ["vertices"] = mesh.vertexCount, ["triangles"] = triangles, ["submeshes"] = submeshes });
            }
            var materialFacts = new List<object>();
            var sourceRenderers = source.GetComponentsInChildren<Renderer>(true);
            var renderers = visual.Model.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers.Length != renderers.Length) throw new QaFailException("Artwork renderer count changed for " + id + ".");
            for (int r = 0; r < renderers.Length; r++)
            {
                var materials = renderers[r].sharedMaterials;
                var sourceMaterials = sourceRenderers[r].sharedMaterials;
                if (materials.Length != sourceMaterials.Length) throw new QaFailException("Artwork material slots changed for " + id + ".");
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (!material || !material.shader) throw new QaFailException("The measured artwork has a missing material/shader: " + id);
                    if (replacement != null)
                    {
                        var expected = slot < replacement.Materials.Length && replacement.Materials[slot]
                            ? replacement.Materials[slot] : sourceMaterials[slot];
                        if (material != expected) throw new QaFailException("The measured override material slot differs for " + id + ".");
                    }
                    materialFacts.Add(new Dictionary<string, object> {
                        ["renderer"] = renderers[r].name, ["renderer_index"] = r, ["slot"] = slot,
                        ["name"] = material.name, ["shader"] = material.shader.name,
                        ["base_color"] = ColorFact(material, "_BaseColor"),
                        ["metallic"] = FloatFact(material, "_Metallic"), ["smoothness"] = FloatFact(material, "_Smoothness"),
                        ["normal_keyword"] = material.IsKeywordEnabled("_NORMALMAP"),
                        ["metallic_gloss_keyword"] = material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                        ["base_map"] = TextureFact(material, "_BaseMap"),
                        ["normal_map"] = TextureFact(material, "_BumpMap"),
                        ["metallic_gloss_map"] = TextureFact(material, "_MetallicGlossMap") });
                }
            }
            artwork.Add(new Dictionary<string, object> {
                ["item"] = id, ["skin"] = skin, ["source"] = sourceKind, ["model_key"] = modelKey,
                ["source_model"] = source.name, ["spawned_model"] = visual.Model.name,
                ["selected_source_meshes_verified"] = true, ["meshes"] = meshFacts, ["materials"] = materialFacts });
        }

        static Mesh[] Meshes(GameObject model) => model.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh)
            .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();

        static object TextureFact(Material material, string property)
        {
            var texture = material.HasProperty(property) ? material.GetTexture(property) : null;
            return texture ? new Dictionary<string, object> { ["name"] = texture.name, ["width"] = texture.width, ["height"] = texture.height } : null;
        }

        static object ColorFact(Material material, string property)
        {
            if (!material.HasProperty(property)) return null;
            var color = material.GetColor(property);
            return new[] { color.r, color.g, color.b, color.a };
        }

        static object FloatFact(Material material, string property) => material.HasProperty(property) ? (object)material.GetFloat(property) : null;

        void LateUpdate()
        {
            if (Ready && Animate) SetPose(Elapsed + Time.unscaledDeltaTime, Time.unscaledDeltaTime);
        }

        public void SetPose(float seconds, float delta)
        {
            Elapsed = seconds;
            for (int i = 0; i < actors.Count; i++)
            {
                float phase = seconds * 1.1f + i * .65f;
                var point = new Vector3((i % 4 - 1.5f) * 2.8f + Mathf.Sin(phase) * .45f, 0,
                    (i / 4 - .5f) * 3.5f + Mathf.Cos(phase) * .45f);
                anchors[i].SetPositionAndRotation(center + point, Quaternion.Euler(0, 165 + Mathf.Sin(phase) * 25, 0));
                motions[i].Step(delta, true, true, held[i].TwoHanded);
                held[i].PlaceAtHand(anchors[i], actors[i].RightHand, true);
            }
            camera.transform.position = center + new Vector3(Mathf.Sin(seconds * .18f) * .8f, 6.5f, -11.5f);
            camera.transform.LookAt(center + Vector3.up * .8f);
        }

        public int ActorsInCamera()
        {
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return actors.Count(actor => actor.Model.GetComponentsInChildren<Renderer>(true).Any(renderer =>
                renderer.enabled && !renderer.forceRenderingOff && GeometryUtility.TestPlanesAABB(planes, renderer.bounds)));
        }

        public void RequireDisplay()
        {
            if (Screen.width != 1920 || Screen.height != 1080 || Screen.fullScreenMode != FullScreenMode.Windowed ||
                !Mathf.Approximately(GraphicsOptions.RenderScale, .75f) || GraphicsOptions.Smoothing != "FXAA" ||
                GraphicsOptions.Shadows != "Low" || GraphicsOptions.FrameCap != 0 || Application.targetFrameRate != -1 ||
                QualitySettings.vSyncCount != 0 || camera && !camera.isActiveAndEnabled)
                throw new QaFailException("The measured display must remain 1920x1080 Windowed, Low, uncapped, V-sync off.");
        }

        void OnGUI()
        {
            if (Ready) GUI.Label(new Rect(12, 12, 1000, 28), label);
        }

        public void Restore()
        {
            if (restored) return;
            restored = true; Animate = false; Ready = false;
            try
            {
                foreach (var motion in motions) motion.Dispose();
                foreach (var item in held) item.Dispose();
                foreach (var actor in actors) actor.Dispose();
                foreach (var anchor in anchors) if (anchor) Destroy(anchor.gameObject);
                if (viewStateCaptured)
                {
                    if (camera) { camera.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation); camera.fieldOfView = previousFov; }
                    if (view) view.enabled = viewEnabled;
                    if (hud) hud.enabled = hudEnabled;
                }
            }
            finally
            {
                if (settingsCaptured)
                {
                    try { RestorePreferenceBytes(); GraphicsOptions.Load(); }
                    finally
                    {
                        QualitySettings.vSyncCount = previousVsync;
                        Screen.SetResolution(previousWidth, previousHeight, previousScreenMode);
                    }
                }
            }
        }

        void OnApplicationQuit() => Restore();
        void OnDestroy() => Restore();
    }
}
#endif
