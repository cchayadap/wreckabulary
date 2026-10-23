using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary
{
    public sealed class WordboundUiStyles : IDisposable
    {
        readonly List<Texture2D> textures = new List<Texture2D>();
        WordboundUiTheme defaults, source;
        GUISkin skin;
        int fingerprint;
        public int Generation { get; private set; }
        public GUIStyle Panel { get; private set; }
        public GUIStyle Heading { get; private set; }
        public GUIStyle Wrapped { get; private set; }
        public GUIStyle Primary { get; private set; }
        public GUIStyle Selected { get; private set; }
        public GUIStyle DisabledText { get; private set; }
        public Color Error { get; private set; }
        public Color Ink { get; private set; }
        public int Padding { get; private set; }
        public int Gap { get; private set; }
        public int RowHeight { get; private set; }
        public int ModuleHeight { get; private set; }
        public int MenuWidth { get; private set; }
        public string ConfigurationError { get; private set; } = "";

        public Scope Apply(WordboundUiTheme theme)
        {
            if (!skin || Event.current.type == EventType.Layout)
            {
                int stamp = theme ? theme.Fingerprint() : 0;
                if (!skin || source != theme || fingerprint != stamp) Rebuild(theme, stamp);
            }
            return new Scope(skin);
        }

        void Rebuild(WordboundUiTheme theme, int stamp)
        {
            if (!defaults)
            {
                defaults = ScriptableObject.CreateInstance<WordboundUiTheme>();
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }
            var selected = theme ? theme : defaults;
            ConfigurationError = "";
            try { selected.Validate(); }
            catch (ArgumentException error) { ConfigurationError = error.Message; selected = defaults; }
            ReleaseSkin();
            try
            {
                skin = Object.Instantiate(GUI.skin); skin.name = "Owned Wordbound interface";
                skin.hideFlags = HideFlags.HideAndDontSave;
                skin.font = selected.BodyFont ? selected.BodyFont : Resources.Load<Font>("Fonts/Nunito-ExtraBold");
                var headingFont = selected.HeadingFont ? selected.HeadingFont : Resources.Load<Font>("Fonts/Nunito-Black");
                var panel = Texture(selected.Panel, selected.Text, selected.CornerRadius, false);
                var normal = Texture(selected.Button, selected.Muted, selected.CornerRadius, true);
                var hover = Texture(selected.Hover, selected.Text, selected.CornerRadius, true);
                var focused = Texture(selected.Button, selected.Focus, selected.CornerRadius, true, 2);
                var active = Texture(selected.Hover, selected.Focus, selected.CornerRadius, true, 2);
                var primary = Texture(selected.Primary, selected.Primary, selected.CornerRadius, false);
                var primaryFocus = Texture(selected.Primary, selected.Focus, selected.CornerRadius, true, 2);
                var disabled = Texture(selected.Disabled, selected.Disabled, selected.CornerRadius, false);
                Configure(skin.label, selected, null, selected.Text); skin.label.padding = new RectOffset(0, 0, 2, 2);
                Configure(skin.box, selected, panel, selected.Text);
                skin.box.padding = new RectOffset(selected.Padding, selected.Padding, selected.Padding, selected.Padding);
                Configure(skin.button, selected, normal, selected.Text);
                skin.button.padding = new RectOffset(10, 10, 5, 5); skin.button.wordWrap = true;
                skin.button.hover.background = hover; skin.button.active.background = active; skin.button.focused.background = focused;
                skin.button.onNormal.background = hover; skin.button.onHover.background = active;
                skin.button.onActive.background = active; skin.button.onFocused.background = active;
                foreach (var field in new[] { skin.textField, skin.textArea })
                {
                    Configure(field, selected, normal, selected.Text); field.padding = new RectOffset(8, 8, 6, 6);
                    field.focused.background = focused; field.active.background = focused;
                }
                foreach (var state in States(skin.toggle)) state.textColor = selected.Text;
                skin.toggle.font = skin.font; skin.toggle.fontSize = selected.BodySize;
                foreach (var track in new[] { skin.verticalScrollbar, skin.horizontalScrollbar })
                    foreach (var state in States(track)) state.background = disabled;
                foreach (var thumb in new[] { skin.verticalScrollbarThumb, skin.horizontalScrollbarThumb })
                    foreach (var state in States(thumb)) state.background = primary;
                skin.settings.cursorColor = selected.Text; skin.settings.selectionColor = selected.Hover;
                Panel = skin.box;
                Heading = new GUIStyle(skin.label) { font = headingFont, fontSize = selected.HeadingSize, wordWrap = true };
                Wrapped = new GUIStyle(skin.label) { wordWrap = true };
                Primary = new GUIStyle(skin.button);
                foreach (var state in States(Primary)) { state.background = primary; state.textColor = selected.PrimaryText; }
                Primary.hover.background = primaryFocus; Primary.focused.background = primaryFocus; Primary.active.background = primaryFocus;
                Selected = new GUIStyle(skin.button);
                foreach (var state in States(Selected)) state.background = active;
                DisabledText = new GUIStyle(skin.button);
                foreach (var state in States(DisabledText)) { state.background = disabled; state.textColor = selected.DisabledText; }
                Error = selected.Error; Ink = selected.Text; Padding = selected.Padding; Gap = selected.Gap;
                RowHeight = selected.RowHeight; ModuleHeight = selected.ModuleHeight; MenuWidth = selected.MenuWidth;
                source = theme; fingerprint = stamp; Generation++;
            }
            catch { ReleaseSkin(); throw; }
        }

        void Configure(GUIStyle style, WordboundUiTheme theme, Texture2D background, Color text)
        {
            style.font = skin.font; style.fontSize = theme.BodySize; style.fontStyle = FontStyle.Normal;
            style.fixedHeight = 0; style.margin = new RectOffset(2, 2, theme.Gap / 2, theme.Gap / 2);
            style.border = new RectOffset(12, 12, 12, 12);
            foreach (var state in States(style)) { state.background = background; state.scaledBackgrounds = null; state.textColor = text; }
        }

        public bool Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            bool clicked = GUILayout.Button(content, style ?? skin.button, options);
            if (!GUI.enabled && Event.current.type == EventType.Repaint)
            {
                bool wasEnabled = GUI.enabled;
                try { GUI.enabled = true; DisabledText.Draw(GUILayoutUtility.GetLastRect(), content, false, false, false, false); }
                finally { GUI.enabled = wasEnabled; }
            }
            return clicked;
        }

        public Rect ToolbarRect(Vector2 viewport) => new Rect(8, 8, Mathf.Max(1, Mathf.Min(520, viewport.x - 16)), ToolbarHeight);
        public float ChromeHeight => RowHeight + Padding * 2 + Gap;
        public float ToolbarHeight => RowHeight * 2 + Padding * 2 + Gap * 3;
        public Rect MenuRect(float width, float height) => new Rect(Mathf.Max(12, (width - Mathf.Min(MenuWidth, width - 24)) * .5f),
            24, Mathf.Max(1, Mathf.Min(MenuWidth, width - 24)), Mathf.Max(1, height - 48));

        Texture2D Texture(Color fill, Color edge, int radius, bool border, int edgeWidth = 1)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "Owned Wordbound interface fill", hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            textures.Add(texture);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = Mathf.Min(x + .5f, size - x - .5f), py = Mathf.Min(y + .5f, size - y - .5f);
                float inset = Mathf.Min(px, py);
                if (radius > 0 && px < radius && py < radius)
                    inset = radius - Mathf.Sqrt((radius - px) * (radius - px) + (radius - py) * (radius - py));
                Color value = border && inset < edgeWidth + .5f ? edge : fill;
                value.a *= Mathf.Clamp01(inset + .5f); pixels[y * size + x] = value;
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        static GUIStyleState[] States(GUIStyle style) => new[] { style.normal, style.hover, style.active, style.focused,
            style.onNormal, style.onHover, style.onActive, style.onFocused };
        void ReleaseSkin()
        {
            if (skin) Release(skin); skin = null;
            foreach (var texture in textures) if (texture) Release(texture);
            textures.Clear();
        }
        public void Dispose() { ReleaseSkin(); if (defaults) Release(defaults); defaults = null; source = null; }
        static void Release(Object owned) { if (Application.isPlaying) Object.Destroy(owned); else Object.DestroyImmediate(owned); }

        public readonly struct Scope : IDisposable
        {
            readonly GUISkin previousSkin;
            readonly Color colour, background, content;
            readonly bool enabled;
            readonly Matrix4x4 matrix;
            public Scope(GUISkin next)
            {
                previousSkin = GUI.skin; colour = GUI.color; background = GUI.backgroundColor;
                content = GUI.contentColor; enabled = GUI.enabled; matrix = GUI.matrix;
                GUI.skin = next; GUI.color = GUI.backgroundColor = GUI.contentColor = Color.white;
            }
            public void Dispose()
            {
                GUI.skin = previousSkin; GUI.color = colour; GUI.backgroundColor = background;
                GUI.contentColor = content; GUI.enabled = enabled; GUI.matrix = matrix;
            }
        }
    }
}
