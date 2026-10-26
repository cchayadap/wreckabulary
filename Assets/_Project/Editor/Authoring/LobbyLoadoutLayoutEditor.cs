using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [CustomEditor(typeof(LobbyLoadoutLayout))]
    public sealed class LobbyLoadoutLayoutEditor : Editor
    {
        string result;
        bool valid;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Open a prefab to edit Loadout. Save it, then use Workspace → Scenes → Play Loadout to try the real page.", MessageType.Info);
            DrawDefaultInspector();
            var layout = (LobbyLoadoutLayout)target;
            using (new EditorGUI.DisabledScope(!layout.Page))
                if (GUILayout.Button("Edit Loadout panel")) AssetDatabase.OpenAsset(layout.Page.gameObject);
            using (new EditorGUI.DisabledScope(!layout.Recipe))
                if (GUILayout.Button("Edit recipe card")) AssetDatabase.OpenAsset(layout.Recipe.gameObject);
            if (GUILayout.Button("Check references"))
            {
                try { layout.Validate(); valid = true; result = "References are ready. Try the actual Loadout to check layout and navigation."; }
                catch (Exception error) { valid = false; result = error.Message; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, valid ? MessageType.Info : MessageType.Error);
        }
    }

    [CustomEditor(typeof(LobbyLoadoutPageView))]
    public sealed class LobbyLoadoutPageEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit the panel, tabs, body slots, recipe grid and Finish control. Wearable controls and recipe detail contents are filled during Play. Keep Finish, the scroll/grid and header controls outside Recipe detail.";
        protected override void ValidateView() => ((LobbyLoadoutPageView)target).Validate();
    }

    [CustomEditor(typeof(LobbyRecipeTileView))]
    public sealed class LobbyRecipeTileEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit the card, icon rectangle, letter-row slot and letter size budget. The game fills the icon and letters. Keep the icon, outline, focus ring and motion body outside Letters (runtime contents).";
        protected override void ValidateView() => ((LobbyRecipeTileView)target).Validate();
    }
}
