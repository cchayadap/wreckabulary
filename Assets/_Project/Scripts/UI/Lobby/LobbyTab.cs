using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyTab : MonoBehaviour
    {
        public enum Look { Slab, Cell }

        public Look Style;
        public int Radius = LobbyKit.TabRadius;
        public Color HoverColour = LobbyKit.TabHover;
        public Button Button { get; private set; }
        Image face, edge, glyph;
        LobbyGradient gradient;
        Shadow drop;
        TextMeshProUGUI label;
        LobbyPress press;
        bool on, hot, down;

        public bool On
        {
            get => on;
            set { on = value; Paint(); }
        }

        public void Init(Button button, Image face, Image edge, LobbyGradient gradient, Shadow drop, TextMeshProUGUI label, Image glyph)
        {
            Button = button; this.face = face; this.edge = edge; this.gradient = gradient; this.drop = drop;
            this.label = label; this.glyph = glyph;
            press = button.GetComponent<LobbyPress>();
            press.Hot = h => { hot = h; Paint(); };
            press.Pressed = d => { down = d; Paint(); };
            Paint();
        }

        void OnEnable() => LobbyThemes.Changed += Paint;
        void OnDisable() => LobbyThemes.Changed -= Paint;

        void Paint()
        {
            if (!face) return;
            face.color = Color.clear;
            gradient.enabled = false;
            drop.enabled = false;
            edge.enabled = on || hot || down;
            edge.type = Image.Type.Simple;
            edge.sprite = null;
            var underline = edge.rectTransform;
            underline.anchorMin = Vector2.zero;
            underline.anchorMax = new Vector2(1, 0);
            underline.offsetMin = new Vector2(8, 1);
            underline.offsetMax = new Vector2(-8, on ? 5 : 3);
            var accent = LobbyThemes.Current.Accent;
            edge.color = accent;
            var ink = on || hot ? LobbyKit.Cyan : LobbyKit.Navy;
            if (label) label.color = ink;
            if (glyph) glyph.color = ink;
            press.Scale = 1f;
            press.Lift = on ? 0f : 2f;
        }
    }
}
