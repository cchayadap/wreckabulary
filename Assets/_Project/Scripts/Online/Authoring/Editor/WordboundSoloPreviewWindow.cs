using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    public sealed class WordboundSoloPreviewWindow : EditorWindow
    {
        WordboundSoloPreviewSettings settings;
        WordboundSoloPreviewStage stage;
        string map, sourceSettings, error;
        Vector3 target;
        float yaw, pitch, distance, fov;
        public static bool IsOpen => HasOpenInstances<WordboundSoloPreviewWindow>();
        public static void Open(WordboundSoloPreviewSettings settings, string map)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Frame previews outside Play mode.");
            settings.Validate();
            var window = GetWindow<WordboundSoloPreviewWindow>("Solo map camera");
            window.Release(); window.settings = settings; window.map = map;
            window.sourceSettings = EditorJsonUtility.ToJson(settings);
            var pose = settings.Pose(map); window.target = pose.Target; window.fov = pose.FieldOfView;
            var rotation = Quaternion.LookRotation(pose.Target - pose.Position, Vector3.up).eulerAngles;
            window.yaw = rotation.y; window.pitch = Mathf.DeltaAngle(0, rotation.x); window.distance = Vector3.Distance(pose.Position, pose.Target);
            try { window.stage = new WordboundSoloPreviewStage(settings, map); window.error = null; }
            catch (Exception e) { window.error = e.Message; }
            window.Show();
        }
        void OnGUI()
        {
            if (!settings || stage == null) { EditorGUILayout.HelpBox(error ?? "Select the saved Solo map preview settings.", MessageType.Info); return; }
            bool stale = sourceSettings != EditorJsonUtility.ToJson(settings);
            EditorGUILayout.LabelField(map + " · sample seed " + settings.Seed, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(stale ? "Settings changed. Reopen this map to refresh before saving its camera." :
                "Actual installed map, static props only. Drag to orbit; scroll to zoom. Save camera then close this window before capturing both images.",
                stale ? MessageType.Warning : MessageType.Info);
            using (new EditorGUI.DisabledScope(stale || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Save camera to settings"))
                {
                    stage.RequireCameraOutsideSurface();
                    Undo.RecordObject(settings, "Save Solo thumbnail camera");
                    var pose = settings.Pose(map);
                    pose.Position = stage.Preview.camera.transform.position - stage.Center; pose.Target = target; pose.FieldOfView = fov;
                    settings.Validate(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                    sourceSettings = EditorJsonUtility.ToJson(settings);
                }
            var available = GUILayoutUtility.GetRect(100, 10000, 100, 10000);
            float width = Mathf.Min(available.width, available.height * 16 / 9f);
            var area = new Rect(available.center.x - width / 2, available.center.y - width * 9 / 32f, width, width * 9 / 16f);
            var e = Event.current;
            if (!stale && area.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseDrag && e.button == 0) { yaw += e.delta.x * .5f; pitch = Mathf.Clamp(pitch + e.delta.y * .5f, -60, 80); e.Use(); Repaint(); }
                if (e.type == EventType.ScrollWheel) { distance = Mathf.Clamp(distance * Mathf.Exp(e.delta.y * .04f), .5f, 60); e.Use(); Repaint(); }
            }
            stage.SetPose(target - Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * distance, target, fov);
            if (e.type != EventType.Repaint || area.width <= 0 || area.height <= 0) return;
            var active = RenderTexture.active; bool srgb = GL.sRGBWrite, ssao = Shader.IsKeywordEnabled("_SCREEN_SPACE_OCCLUSION"); bool begun = false;
            try
            {
                stage.Preview.BeginPreview(area, GUIStyle.none); begun = true;
                stage.Preview.Render(true);
                var texture = stage.Preview.EndPreview(); begun = false;
                GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, false);
            }
            finally
            {
                try { if (begun) stage.Preview.EndPreview(); }
                finally
                {
                    RenderTexture.active = active; GL.sRGBWrite = srgb;
                    if (ssao) Shader.EnableKeyword("_SCREEN_SPACE_OCCLUSION"); else Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
                }
            }
        }
        void OnDisable() => Release();
        void Release() { try { stage?.Dispose(); } finally { stage = null; } }
    }
}
