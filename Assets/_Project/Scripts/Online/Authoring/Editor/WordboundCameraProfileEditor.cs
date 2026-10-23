using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    [CustomEditor(typeof(WordboundCameraProfile))]
    public sealed class WordboundCameraProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Assign this profile to a Wordbound course or the launch assets. During play, valid changes update the camera immediately. Save the asset to keep them.", MessageType.Info);
            EditorGUILayout.LabelField("View and follow", EditorStyles.boldLabel);
            Field("FieldOfView", "Field of view (degrees)");
            Field("Distance", "Follow distance (m)");
            Field("PivotHeight", "Look-at height (m)");
            Field("Lift", "Camera lift (m)");
            Degrees("DefaultPitch", "Starting angle (degrees)", -1.3f, 1.3f);
            Degrees("MinPitch", "Minimum angle (degrees)", -1.3f, 0);
            Degrees("MaxPitch", "Maximum angle (degrees)", 0, 1.3f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Look controls", EditorStyles.boldLabel);
            Field("MouseSensitivity", "Mouse sensitivity");
            Field("StickYawSpeed", "Controller horizontal speed");
            Field("StickPitchSpeed", "Controller vertical speed");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Walls and visibility", EditorStyles.boldLabel);
            Field("MinDistance", "Closest follow distance (m)");
            Field("ProbeRadius", "Wall probe radius (m)");
            Field("Skin", "Wall clearance (m)");
            Field("OutEase", "Return speed after obstruction");
            Field("NearClip", "Near visibility limit (m)");
            Field("FarClip", "Far visibility limit (m)");
            serializedObject.ApplyModifiedProperties();

            try { ((WordboundCameraProfile)target).Validate(); }
            catch (ArgumentException error)
            {
                EditorGUILayout.HelpBox(error.Message + " During play, the camera keeps its last valid settings until this is corrected.", MessageType.Error);
            }
        }

        void Field(string property, string label) =>
            EditorGUILayout.PropertyField(serializedObject.FindProperty(property), new GUIContent(label));

        void Degrees(string property, string label, float minimum, float maximum)
        {
            var value = serializedObject.FindProperty(property);
            EditorGUI.BeginChangeCheck();
            float degrees = EditorGUILayout.Slider(label, value.floatValue * Mathf.Rad2Deg,
                minimum * Mathf.Rad2Deg, maximum * Mathf.Rad2Deg);
            if (EditorGUI.EndChangeCheck()) value.floatValue = degrees * Mathf.Deg2Rad;
        }
    }
}
