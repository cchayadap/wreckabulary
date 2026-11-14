using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        GUIStyle centeredTitle, centeredHint;
        int centeredGeneration = -1;
        LobbyPlayLayout installedMapArt;

        void PrepareCenteredStyles()
        {
            if (centeredGeneration != ui.Generation || centeredTitle == null)
            {
                centeredTitle = new GUIStyle(ui.Heading) { alignment = TextAnchor.MiddleCenter };
                centeredHint = new GUIStyle(ui.Wrapped) { alignment = TextAnchor.MiddleCenter };
                centeredGeneration = ui.Generation;
            }
            title = centeredTitle; hint = centeredHint;
        }

        void DrawInstalledMapPreview()
        {
            installedMapArt ??= Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            bool arena = Modes[mode] == "FreeForAll";
            GUILayout.Space(ui.Gap);
            GUILayout.Label(arena ? "Toybox Arena" : "The Lost Address", title);
            var sprite = installedMapArt ? arena ? installedMapArt.CombatArenaPreview : installedMapArt.LostAddressPreview : null;
            if (!sprite || !sprite.texture) return;
            float previewWidth = Mathf.Clamp((partyAvailableHeight - 500) * 16f / 9f, 160, 300);
            float width = Mathf.Min(previewWidth, Mathf.Max(120, partyRect.width - ui.Panel.padding.horizontal - 32));
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            var image = GUILayoutUtility.GetRect(width, width * 9f / 16f, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal();
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            try
            {
                GUI.color = Color.white;
                Rect uv = sprite.textureRect;
                float scale = Mathf.Min(image.width / uv.width, image.height / uv.height);
                var fitted = new Rect(image.center - new Vector2(uv.width, uv.height) * scale * .5f,
                    new Vector2(uv.width, uv.height) * scale);
                uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height,
                    uv.width / sprite.texture.width, uv.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv, true);
            }
            finally { GUI.color = previous; }
        }
    }
}
