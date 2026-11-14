using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [CustomEditor(typeof(LobbyPlayLayout))]
    public sealed class LobbyPlayLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Edit the saved frame outside Play. Cards and actions are generated inside its regions. Compact maximum sizes the Wordbound frame; full house pages retain authored anchors.", MessageType.Info);
            DrawDefaultInspector(); var profile = (LobbyPlayLayout)target;
            if (profile.Page && GUILayout.Button("Edit Play panel")) AssetDatabase.OpenAsset(profile.Page.gameObject);
            if (GUILayout.Button("Check references"))
                try { profile.Validate(); EditorUtility.DisplayDialog("Play frame", "References are ready. Preview the real page to check layout and navigation.", "Close"); }
                catch (Exception error) { EditorUtility.DisplayDialog("Play frame", error.Message, "Close"); }
        }
    }

    [CustomEditor(typeof(LobbySettingsLayout))]
    public sealed class LobbySettingsLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Edit the saved frame regions outside Play. The settings and binding rows retain their current behavior. The Display confirmation LayoutElement controls its reserved height.", MessageType.Info);
            DrawDefaultInspector(); var profile = (LobbySettingsLayout)target;
            if (profile.Page && GUILayout.Button("Edit Settings panel")) AssetDatabase.OpenAsset(profile.Page.gameObject);
            if (GUILayout.Button("Check references"))
                try { profile.Validate(); EditorUtility.DisplayDialog("Settings frame", "References are ready. Preview the real page to check layout and navigation.", "Close"); }
                catch (Exception error) { EditorUtility.DisplayDialog("Settings frame", error.Message, "Close"); }
        }
    }

    [CustomEditor(typeof(LobbyPlayPageView))]
    public sealed class LobbyPlayPageViewEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit panel, title, queue strip, Choices and Summary regions. Their contents are filled at runtime; cards remain generated. Compact mode owns the panel's responsive root frame.";
        protected override void ValidateView() => ((LobbyPlayPageView)target).Validate();
    }

    [CustomEditor(typeof(LobbySettingsPageView))]
    public sealed class LobbySettingsPageViewEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit panel, title, Tabs and content stack. Keep Content flexible and Display confirmation at a fixed preferred height. Controls and binding rows remain generated.";
        protected override void ValidateView() => ((LobbySettingsPageView)target).Validate();
    }
}
