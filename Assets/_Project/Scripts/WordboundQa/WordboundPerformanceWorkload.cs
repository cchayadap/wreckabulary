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
        WordboundPlayerView view;
        WordboundMatchHud hud;
        Camera camera;
        Vector3 center, previousCameraPosition;
        Quaternion previousCameraRotation;
        float previousFov;
        int previousVsync, previousWidth, previousHeight;
        FullScreenMode previousScreenMode;
        bool settingsCaptured, restored, viewEnabled, hudEnabled;
        string label;
        public bool Animate { get; set; }
        public bool Ready { get; private set; }
        public float Elapsed { get; private set; }
        public string ModuleId { get; private set; }
        public int SyntheticActors => actors.Count;
        public int HeldItems => held.Count;
        public int RendererCount { get; private set; }
        public long ItemTriangles { get; private set; }

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
            var collection = WordboundEquipmentCollection.Load();
            if (!collection || collection.Entries.Count != 12) throw new QaFailException("The detailed equipment collection is not loaded.");
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
            view.enabled = false; hud.enabled = false;
            camera.fieldOfView = 60; GraphicsOptions.ApplyTo(camera);
            var resolver = new WordboundVisualResolver(items);
            for (int i = 0; i < Equipment.Length; i++)
            {
                var entry = collection.Find(Equipment[i]);
                RequireDetailedEntry(entry);
                var anchor = new GameObject("Synthetic render actor " + i).transform;
                anchor.SetParent(transform, false); anchors.Add(anchor);
                var actor = resolver.Create("player", new Vector3(.6f, 1.8f, .6f), anchor);
                actors.Add(actor); actor.Dress(""); motions.Add(new WordboundAvatarMotion(actor));
                var item = items.Get(Equipment[i]);
                var equipment = resolver.Create(Equipment[i], new Vector3(item.Size[0], item.Size[1], item.Size[2]), anchor);
                held.Add(equipment);
                var sourceMeshes = entry.model.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
                var meshes = equipment.Model.GetComponentsInChildren<MeshFilter>(true).Select(m => m.sharedMesh).ToArray();
                if (!sourceMeshes.SequenceEqual(meshes)) throw new QaFailException("The measured held item did not use the selected collection meshes.");
                foreach (var mesh in meshes)
                    for (int sub = 0; sub < mesh.subMeshCount; sub++) ItemTriangles += (long)mesh.GetIndexCount(sub) / 3;
            }
            RendererCount = GetComponentsInChildren<Renderer>(true).Length;
            label = "SYNTHETIC RENDER TEST | 8 animated actors + 8 held items | local world actors: " +
                client.Gameplay.Frame.Actors.Count + " | remote peers: 0";
            Ready = true; SetPose(0, 0); Animate = true;
        }

        static void RequireDetailedEntry(WordboundEquipmentCollection.Entry entry)
        {
            if (entry == null || !entry.model) throw new QaFailException("A required equipment model is missing.");
            bool detailed = entry.model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(material => {
                if (!material || !material.HasProperty("_BumpMap") || !material.HasProperty("_MetallicGlossMap") ||
                    !material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICSPECGLOSSMAP")) return false;
                var color = material.GetTexture("_BaseMap"); var normal = material.GetTexture("_BumpMap");
                var mask = material.GetTexture("_MetallicGlossMap");
                return color && normal && mask && color.width == 512 && color.height == 256 &&
                    normal.width == 512 && normal.height == 256 && mask.width == 512 && mask.height == 256;
            });
            if (!detailed) throw new QaFailException(entry.itemId + " is not using the detailed normal/mask material set.");
        }

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
                GraphicsOptions.Shadows != "Low" || GraphicsOptions.FrameCap != 0 || QualitySettings.vSyncCount != 0)
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
                if (camera) { camera.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation); camera.fieldOfView = previousFov; }
                if (view) view.enabled = viewEnabled;
                if (hud) hud.enabled = hudEnabled;
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
