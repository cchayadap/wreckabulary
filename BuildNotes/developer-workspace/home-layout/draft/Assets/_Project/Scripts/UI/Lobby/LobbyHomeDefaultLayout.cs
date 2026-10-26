using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public static class LobbyHomeDefaultLayout
    {
        public static LobbyHomePageView Create(Transform parent, LobbyTypographyProfile typography = null)
        {
            if (typography && !typography.TryValidate(out string reason)) throw new InvalidOperationException(reason);
            var root = LobbyKit.Rect(parent, "Home page layout").Fill();
            try
            {
                var view = root.gameObject.AddComponent<LobbyHomePageView>();
                view.TurnHint = LobbyKit.Row(root, "Turn hint", 8, 0);
                view.TurnHint.Pin(new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(240, 40));
                var hintRow = view.TurnHint.GetComponent<HorizontalLayoutGroup>();
                hintRow.childAlignment = TextAnchor.MiddleCenter; hintRow.childForceExpandHeight = false;
                LobbyKit.Icon(view.TurnHint, LobbyIcons.Turn, LobbyKit.Cyan).Size(26, 26);
                var hint = Text(view.TurnHint, "Turn instruction", "Drag to turn", typography, true, 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
                var stroke = typography ? typography.Stroke : LobbyFonts.Stroke;
                if (stroke && hint.color.grayscale > .72f)
                    hint.fontSharedMaterial = LobbyRuntimeTypography.ResolveMaterial(typography ? typography.Display : LobbyFonts.Display, stroke);
                hint.Size(-1, 30);

                view.Dock = LobbyKit.Column(root, "Next match", 2, 12);
                view.Dock.Pin(new Vector2(1, 0), Vector2.zero, new Vector2(HomePage.DockWidth, 0));
                view.Dock.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                LobbyKit.PanelFace(view.Dock, 14, 1, 0);
                view.Start = Action(view.Dock, "GO", "START", typography, 42); view.Start.Size(-1, 64);
                view.Start.Underline.enabled = true; view.Start.Label.color = view.Start.ActiveInk;
                view.Mode = Action(view.Dock, "Choose mode", "Dibs!", typography, 22); view.Mode.Size(-1, 38);
                view.House = Action(view.Dock, "Change house", "House", typography, 20); view.House.Size(-1, 36);
                view.Wordbound = Action(view.Dock, "Wordbound", "WORDBOUND", typography, 24); view.Wordbound.Size(-1, 42);
                view.WordboundNote = Text(view.Dock, "Wordbound note", "Online parties · Map workshop", typography, false, 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                view.WordboundNote.textWrappingMode = TextWrappingModes.Normal;
                view.WordboundNote.Size(-1, 24);
                view.Validate();
                return view;
            }
            catch
            {
                root.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject); else UnityEngine.Object.DestroyImmediate(root.gameObject);
                throw;
            }
        }

        static LobbyShopActionView Action(Transform parent, string name, string label, LobbyTypographyProfile typography, float size)
        {
            var button = LobbyKit.Button(parent, name, Color.clear, null, 8);
            var press = button.GetComponent<LobbyPress>();
            var body = press.Body;
            var motion = LobbyKit.Rect(button.transform, "Motion (runtime animation)").Fill();
            body.SetParent(motion, false); press.Body = motion;
            var content = LobbyKit.Row(body, "Content", 10).Fill();
            var row = content.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter; row.childForceExpandHeight = false;
            row.padding = new RectOffset(8, 8, 4, 6);
            var view = button.gameObject.AddComponent<LobbyShopActionView>();
            view.Button = button; view.Press = press;
            view.Icon = LobbyKit.Icon(content, LobbyIcons.Coin, LobbyKit.Navy).Size(size, size);
            view.Icon.gameObject.SetActive(false);
            view.Label = Text(content, "Text", label, typography, true, size, LobbyKit.Navy, TextAlignmentOptions.Center);
            view.Label.characterSpacing = .6f;
            view.Label.enableAutoSizing = true;
            view.Label.fontSizeMin = Mathf.Min(size, Mathf.Max(14f, size * .78f)); view.Label.fontSizeMax = size;
            view.Label.Size(-1, size + 10).GetComponent<LayoutElement>().minWidth = 0;
            view.Underline = LobbyKit.Rect(body, "Underline").Place(Vector2.zero, new Vector2(1, 0), new Vector2(10, 1), new Vector2(-10, 4)).Paint(LobbyKit.Cyan, 1);
            view.Underline.raycastTarget = false; view.Underline.enabled = false;
            return view;
        }

        static TextMeshProUGUI Text(Transform parent, string name, string value, LobbyTypographyProfile typography,
            bool display, float size, Color ink, TextAlignmentOptions alignment)
        {
            var source = typography ? display ? typography.Display : typography.Body : display ? LobbyFonts.Display : LobbyFonts.Body;
            if (!source) throw new InvalidOperationException("The Home layout needs the lobby fonts.");
            var rect = LobbyKit.Rect(parent, name);
            rect.gameObject.SetActive(false);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LobbyRuntimeTypography.ResolveFont(source);
            text.fontSharedMaterial = LobbyRuntimeTypography.ResolveMaterial(source, source.material);
            text.text = value; text.fontSize = size; text.color = ink; text.alignment = alignment;
            text.raycastTarget = false; text.extraPadding = true;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            rect.gameObject.SetActive(true);
            return text;
        }
    }
}
