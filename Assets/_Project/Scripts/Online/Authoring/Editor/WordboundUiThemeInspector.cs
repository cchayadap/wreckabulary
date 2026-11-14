using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Editor
{
    [CustomEditor(typeof(WordboundUiTheme))]
    public sealed class WordboundUiThemeInspector : UnityEditor.Editor
    {
        readonly WordboundUiStyles styles = new WordboundUiStyles();
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var theme = (WordboundUiTheme)target;
            try { theme.Validate(); }
            catch (ArgumentException error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            EditorGUILayout.HelpBox("Assign this asset to WordboundLaunchAssets → Ui Theme. Changes appear at the next menu layout. " +
                "Camera, models, controls and course content are unchanged.", MessageType.Info);
            using var scope = styles.Apply(theme);
            GUILayout.BeginVertical(styles.Panel);
            GUILayout.Label("Wordbound interface", styles.Heading);
            GUILayout.Label("Equipment and workshop preview", styles.Wrapped);
            styles.Button(new GUIContent("Resume"), styles.Primary, GUILayout.MinHeight(styles.RowHeight));
            styles.Button(new GUIContent("Selected control"), styles.Selected, GUILayout.MinHeight(styles.RowHeight));
            GUI.enabled = false;
            styles.Button(new GUIContent("TABLE · Need A"), null, GUILayout.MinHeight(styles.RowHeight));
            GUI.enabled = true;
            styles.Button(new GUIContent("Flagship Room\n12 × 12 m"), null, GUILayout.MinHeight(styles.ModuleHeight));
            GUILayout.EndVertical();
        }
        void OnDisable() => styles.Dispose();
    }
}
