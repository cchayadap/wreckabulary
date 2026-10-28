using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    [Serializable]
    public sealed class WordboundSoloPreviewPose
    {
        public string Module = "Packing_ParcelGate";
        public Vector3 Position = new Vector3(-8, 4, -9);
        public Vector3 Target = new Vector3(1, 1.5f, 1);
        [Range(30, 80)] public float FieldOfView = 55;
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Module) || !Finite(Position) || !Finite(Target) ||
                Position.magnitude > 100 || Target.magnitude > 100 || (Position - Target).sqrMagnitude < .01f ||
                float.IsNaN(FieldOfView) || FieldOfView < 30 || FieldOfView > 80)
                throw new InvalidOperationException("Use a finite camera pose near the selected module, with a separate target and 30–80 degree lens.");
        }
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
            float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    public sealed class WordboundSoloPreviewSettings : ScriptableObject
    {
        public const string Path = "Assets/_Project/Data/Wordbound/Authoring/Previews/SoloMapPreviews.asset";
        [Tooltip("Representative sample only. Normal Solo chooses a new seed on launch.")]
        public string Seed = "0123456789abcdef";
        public string Mode = "Freestyle";
        [Tooltip("Positions are metres relative to the named module. The saved gameplay camera is not changed.")]
        public WordboundSoloPreviewPose LostAddress = new WordboundSoloPreviewPose();
        public WordboundSoloPreviewPose ParcelHall = new WordboundSoloPreviewPose();
        public WordboundSoloPreviewPose ParcelService = new WordboundSoloPreviewPose { Module = "Service",
            Position = new Vector3(-2.2f, 1.7f, -2.6f), Target = new Vector3(1, 1, 2.3f), FieldOfView = 50 };
        public void Validate()
        {
            if (!Regex.IsMatch(Seed ?? "", "^[0-9a-fA-F]{16}$") || !WordboundSoloEntry.Supports(Mode) || LostAddress == null || ParcelHall == null || ParcelService == null)
                throw new InvalidOperationException("Choose a 16-digit hexadecimal sample seed, TimeRun or Freestyle, and all camera poses.");
            LostAddress.Validate(); ParcelHall.Validate(); ParcelService.Validate();
        }
        public WordboundSoloPreviewPose Pose(string map) => map == WordboundSoloEntry.LostAddressMap ? LostAddress :
            map == WordboundSoloEntry.ParcelHallMap ? ParcelHall :
            map == WordboundSoloEntry.ParcelServiceMap ? ParcelService : throw new ArgumentException("Unknown installed Solo map.", nameof(map));
    }

    [CustomEditor(typeof(WordboundSoloPreviewSettings))]
    public sealed class WordboundSoloPreviewSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Previews show actual installed Solo content at a sample seed. Orbit and zoom in the preview window, then Save camera. Changing these poses does not change gameplay or public maps.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Frame Lost Address")) WordboundSoloPreviewWindow.Open((WordboundSoloPreviewSettings)target, WordboundSoloEntry.LostAddressMap);
                if (GUILayout.Button("Frame Parcel Hall")) WordboundSoloPreviewWindow.Open((WordboundSoloPreviewSettings)target, WordboundSoloEntry.ParcelHallMap);
                if (GUILayout.Button("Frame Parcel Service")) WordboundSoloPreviewWindow.Open((WordboundSoloPreviewSettings)target, WordboundSoloEntry.ParcelServiceMap);
                if (GUILayout.Button("Save settings")) { ((WordboundSoloPreviewSettings)target).Validate(); AssetDatabase.SaveAssetIfDirty(target); }
                if (GUILayout.Button("Capture Lost Address and Parcel Hall")) WordboundSoloPreviewCapture.CaptureDrafts();
                if (GUILayout.Button("Capture Parcel Service")) WordboundSoloPreviewCapture.CaptureParcelServiceDraft();
            }
        }
    }
}
