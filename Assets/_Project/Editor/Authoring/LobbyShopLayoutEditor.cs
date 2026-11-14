using System;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    [CustomEditor(typeof(LobbyShopLayout))]
    public sealed class LobbyShopLayoutEditor : Editor
    {
        string result;
        bool valid;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Open a prefab to edit the real Shop. Save it, then use Workspace → Play Shop to see your changes.", MessageType.Info);
            DrawDefaultInspector();
            var layout = (LobbyShopLayout)target;
            using (new EditorGUI.DisabledScope(!layout.Page))
                if (GUILayout.Button("Edit Shop panel")) AssetDatabase.OpenAsset(layout.Page.gameObject);
            using (new EditorGUI.DisabledScope(!layout.Offer))
                if (GUILayout.Button("Edit offer card")) AssetDatabase.OpenAsset(layout.Offer.gameObject);
            if (GUILayout.Button("Check references"))
            {
                try { layout.Validate(); valid = true; result = "References are ready. Try the actual Shop to check layout and navigation."; }
                catch (Exception error) { valid = false; result = error.Message; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, valid ? MessageType.Info : MessageType.Error);
        }
    }

    public abstract class LobbyShopViewEditor : Editor
    {
        string result;
        bool valid;
        protected abstract string Guidance { get; }
        protected abstract void ValidateView();

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(Guidance, MessageType.Info);
            EditorGUILayout.HelpBox("Edit Body and child elements. Motion is reserved for button animation. Save in Prefab Mode; changes made during Play are temporary.", MessageType.None);
            DrawDefaultInspector();
            if (GUILayout.Button("Check references"))
            {
                try { ValidateView(); valid = true; result = "References are valid."; }
                catch (Exception error) { valid = false; result = error.Message; }
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, valid ? MessageType.Info : MessageType.Error);
        }
    }

    [CustomEditor(typeof(LobbyShopPageView))]
    public sealed class LobbyShopPageEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit panel, tabs, scroll area, grid and fonts here. The game fills the offer cards and status message. Gear's recipe picker and Extras still use generated layouts.";
        protected override void ValidateView() => ((LobbyShopPageView)target).Validate();
    }

    [CustomEditor(typeof(LobbyShopOfferView))]
    public sealed class LobbyShopOfferEditor : LobbyShopViewEditor
    {
        protected override string Guidance => "Edit card size, picture area, title style and action placement here. Item names, pictures, prices and equipped state come from the selected offer.";
        protected override void ValidateView() => ((LobbyShopOfferView)target).Validate();
    }
}
