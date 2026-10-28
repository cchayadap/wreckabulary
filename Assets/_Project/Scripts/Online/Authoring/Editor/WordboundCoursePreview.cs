using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.WordboundRuntime;

namespace Wreckabulary.Online.Editor
{
    public sealed class WordboundCoursePreview : EditorWindow
    {
        PreviewRenderUtility renderer;
        WordboundCourseWorld world;
        WordboundCourseAsset asset;
        ResolvedCourse course;
        string inputHash, error = "";
        Vector3 center;
        float yaw = 35, pitch = 35, distance;
        public static void Show(WordboundCourseAsset asset, ResolvedCourse exact)
        {
            var window = GetWindow<WordboundCoursePreview>("Wordbound exact preview");
            window.Initialize(asset, exact); window.Show();
        }
        void Initialize(WordboundCourseAsset selected, ResolvedCourse exact)
        {
            Release(); asset = selected; course = exact;
            var content = asset.CreateContent(exact);
            inputHash = asset.InputHash;
            renderer = new PreviewRenderUtility();
            try
            {
                world = WordboundCourseWorld.Build(exact, content.Geometry, options:
                    new WordboundCourseWorldOptions { RoomStyle = asset.VisualProfile ? asset.VisualProfile.RoomStyle : null });
                renderer.AddSingleGO(world.gameObject);
                var resolver = new WordboundVisualResolver(content.Items, profile: asset.VisualProfile);
                foreach (var placement in exact.Placements)
                {
                    var definition = content.Geometry.Assets[placement.AssetId];
                    var parent = new GameObject("Preview prop " + placement.Id).transform;
                    parent.SetParent(world.transform, false);
                    parent.localPosition = new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z) / 1000f;
                    parent.localRotation = Quaternion.Euler(0, placement.Yaw, 0);
                    resolver.Create(placement.AssetId, new Vector3(definition.WidthMm, definition.HeightMm, definition.DepthMm) / 1000f, parent);
                }
                foreach (var t in world.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
                var all = world.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = all.Length == 0 ? new Bounds(Vector3.zero, Vector3.one * 20) : all[0].bounds;
                foreach (var r in all.Skip(1)) bounds.Encapsulate(r.bounds);
                center = bounds.center; distance = Mathf.Max(12, bounds.size.magnitude);
                renderer.camera.fieldOfView = asset.CameraProfile ? asset.CameraProfile.FieldOfView : 55;
                renderer.camera.nearClipPlane = .1f; renderer.camera.farClipPlane = 10000;
                renderer.lights[0].intensity = 1.2f; renderer.lights[0].transform.rotation = Quaternion.Euler(35, 25, 0);
                renderer.lights[1].intensity = .7f; renderer.ambientColor = Color.gray;
                error = "";
            }
            catch (Exception e) { error = e.Message; Release(); }
        }
        void OnGUI()
        {
            if (error != "") { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            if (!asset || course == null || renderer == null) { EditorGUILayout.HelpBox("Select an editable course and choose Validate and preview exact seed.", MessageType.Info); return; }
            bool stale = false;
            try { stale = inputHash != asset.InputHash; }
            catch { stale = true; }
            EditorGUILayout.LabelField(asset.name + " · " + asset.ModeId + " · seed " + course.Seed, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(stale ? "Saved content changed. Regenerate the preview before playing." :
                "Static geometry and authored prop artwork from the exact manifest. Drag to orbit; scroll to zoom. Dynamic puzzles and players run in local play.", stale ? MessageType.Warning : MessageType.Info);
            using (new EditorGUI.DisabledScope(stale || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Play this exact preview locally"))
                    try { WordboundCoursePlaytest.Start(asset, course, inputHash); } catch (Exception failure) { error = failure.Message; }
            Rect area = GUILayoutUtility.GetRect(100, 10000, 100, 10000);
            var e = Event.current;
            if (area.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0)
                { yaw += e.delta.x * .5f; pitch = Mathf.Clamp(pitch + e.delta.y * .5f, 5, 85); e.Use(); Repaint(); }
                if (e.type == EventType.ScrollWheel)
                { distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * .04f), 2, 10000); e.Use(); Repaint(); }
            }
            if (e.type != EventType.Repaint || area.width <= 0 || area.height <= 0) return;
            renderer.BeginPreview(area, GUIStyle.none);
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            renderer.camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
            renderer.Render(true);
            GUI.DrawTexture(area, renderer.EndPreview(), ScaleMode.StretchToFill, false);
        }
        void OnDisable() => Release();
        void Release()
        {
            if (world) world.Dispose(); world = null;
            renderer?.Cleanup(); renderer = null;
        }
    }
}
