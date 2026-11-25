using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.BossFight
{
    public sealed class BossFightHud : MonoBehaviour
    {
        public const string RootName = "Boss Fight panel";
        const float Pad = 10f, Width = 600f, TitleHeight = 30f, RowHeight = 34f, ClueHeight = 26f, Gap = 6f;
        const float Tile = 28f, TileStep = 32f, WordGap = 22f, BarHeight = 18f, TopGap = 8f, ChipClear = 89f, BannerClear = 224f;
        const float TitleFont = 24f, LetterFont = 20f, ClueFont = 20f, MinFont = 15f, CrackedAlpha = .45f;
        const int PanelRadius = 16, TileRadius = 6, BarRadius = 8;
        const float Height = Pad + TitleHeight + Gap + RowHeight + Gap + ClueHeight + Pad;
        static readonly Color Backing = Hex(0x10292b, .62f), Gold = Hex(0xffe7b8), OnGame = Hex(0xfff7e8);
        static readonly Color TileGot = Hex(0xe8c48b), TileGotInk = Hex(0x3b2614), TileMissingInk = Hex(0xffffff, .65f), TileRim = Hex(0xfff4e2, .55f);
        static readonly Color Ring = Hex(0xffd46a), Cracked = Hex(0xb7dfc8), Track = Hex(0x0b1f1f, .78f), Life = Hex(0xff8a6b);

        GameHud hud;
        BossFightDirector director;
        RectTransform root, tray, bar, barFill;
        TextMeshProUGUI title, clue;
        readonly List<(Image face, Image rim, TextMeshProUGUI letter)> tiles = new();
        readonly List<char> spare = new();
        readonly StringBuilder text = new();
        long shownKey = long.MinValue;

        public RectTransform Root => root;
        public bool Shown => root && root.gameObject.activeSelf;
        public string TitleText => title ? title.text : "";
        public string ClueText => clue ? clue.text : "";
        public bool HealthShown => bar && bar.gameObject.activeSelf;
        public float HealthFill => bar && barFill && bar.sizeDelta.x > 0f ? barFill.sizeDelta.x / bar.sizeDelta.x : 0f;
        public string TileText { get; private set; } = "";

        public static BossFightHud Attach(GameHud hud, BossFightDirector source)
        {
            if (!hud) return null;
            if (!hud.TryGetComponent(out BossFightHud view)) view = hud.gameObject.AddComponent<BossFightHud>();
            view.hud = hud;
            view.director = source;
            return view;
        }

        void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
        }

        bool Build()
        {
            if (root) return true;
            if (!hud || !hud.UiCanvas) return false;
            var canvas = hud.UiCanvas.transform;
            var parent = canvas.Find("Safe HUD") ?? canvas;
            root = Rect(RootName, parent, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(Width, Height));
            Paint(root.gameObject, LobbyIcons.RoundedSprite(PanelRadius), Backing);
            title = Text("Name", root, LobbyFonts.Black, TitleFont, Gold, TextAlignmentOptions.Center);
            Row(title.rectTransform, Pad, TitleHeight);
            tray = Rect("Shield words", root, new Vector2(.5f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(Pad + TitleHeight + Gap)), new Vector2(0f, RowHeight));
            bar = Rect("Boss health", root, new Vector2(.5f, 1f), new Vector2(.5f, .5f), new Vector2(0f, -(Pad + TitleHeight + Gap + RowHeight * .5f)), new Vector2(Width - 4f * Pad, BarHeight));
            Paint(bar.gameObject, LobbyIcons.RoundedSprite(BarRadius), Track);
            barFill = Rect("Fill", bar, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(Width - 4f * Pad, BarHeight));
            Paint(barFill.gameObject, LobbyIcons.RoundedSprite(BarRadius), Life);
            bar.gameObject.SetActive(false);
            clue = Text("Clue", root, LobbyFonts.Body, ClueFont, OnGame, TextAlignmentOptions.Center);
            Row(clue.rectTransform, Pad + TitleHeight + Gap + RowHeight + Gap, ClueHeight);
            root.gameObject.SetActive(false);
            return true;
        }

        void LateUpdate()
        {
            if (!hud || !director || !Build()) return;
            bool shown = director.Revealed && !hud.ResultShown && !hud.BagOpen;
            if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
            if (!shown) return;
            float top = Mathf.Max(hud.BarShown ? hud.BarBottom + PlayerBar.Clearance : hud.TopInset + TopGap, hud.TopInset + (hud.BannerShown ? BannerClear : ChipClear));
            var spot = new Vector2(0f, -top);
            if (root.anchoredPosition != spot) root.anchoredPosition = spot;
            var you = hud.LocalPlayer;
            long key = Key(you);
            if (key == shownKey) return;
            shownKey = key;
            Refresh(you);
        }

        long Key(PlayerController you)
        {
            unchecked
            {
                var boss = director.Boss;
                long key = director.Layer * 31L + (boss ? boss.LayersRemaining : 0);
                key = key * 31 + (boss ? Mathf.CeilToInt(boss.Health) : 0);
                key = key * 31 + (director.Clue?.GetHashCode() ?? 0);
                key = key * 31 + (int)director.Current;
                if (you && you.Inventory)
                {
                    var letters = you.Inventory.Letters;
                    for (int i = 0; i < letters.Count; i++) key = key * 31 + letters[i];
                }
                return key;
            }
        }

        void Refresh(PlayerController you)
        {
            var boss = director.Boss;
            bool open = boss && boss.Vulnerable;
            title.text = BossFightTuning.Name;
            clue.text = director.Clue ?? "";
            tray.gameObject.SetActive(!open);
            bar.gameObject.SetActive(open);
            if (open)
            {
                float ratio = boss.MaxHealth > 0f ? Mathf.Clamp01(boss.Health / boss.MaxHealth) : 0f;
                barFill.sizeDelta = new Vector2(bar.sizeDelta.x * ratio, BarHeight);
                return;
            }
            ShowWords(you, boss ? boss.LayersRemaining : BossFightTuning.Words.Count);
        }

        void ShowWords(PlayerController you, int remaining)
        {
            int total = 0;
            foreach (string word in BossFightTuning.Words) total += word.Length;
            while (tiles.Count < total) MakeTile();
            spare.Clear();
            if (you && you.Inventory)
                foreach (char c in you.Inventory.Letters) spare.Add(char.ToUpperInvariant(c));
            int cracked = BossFightTuning.Words.Count - remaining;
            text.Clear();
            float x = 0f;
            int at = 0;
            for (int w = 0; w < BossFightTuning.Words.Count; w++)
            {
                string word = BossFightTuning.Words[w];
                bool done = w < cracked, current = w == director.Layer && director.Spelling;
                for (int i = 0; i < word.Length; i++, at++)
                {
                    var (face, rim, letter) = tiles[at];
                    face.gameObject.SetActive(true);
                    face.rectTransform.anchoredPosition = new Vector2(x, 0f);
                    x += TileStep;
                    letter.text = word[i].ToString();
                    bool got = false;
                    if (current)
                    {
                        int held = spare.IndexOf(word[i]);
                        got = held >= 0;
                        if (got) spare.RemoveAt(held);
                    }
                    face.color = done ? new Color(Cracked.r, Cracked.g, Cracked.b, CrackedAlpha) : got ? TileGot : Color.clear;
                    rim.enabled = !done && !got;
                    rim.color = current ? Ring : TileRim;
                    letter.color = done || got ? TileGotInk : TileMissingInk;
                    text.Append(done || got ? word[i] : '_');
                }
                x += WordGap - (TileStep - Tile);
                if (w < BossFightTuning.Words.Count - 1) text.Append(' ');
            }
            TileText = text.ToString();
            for (int i = at; i < tiles.Count; i++) tiles[i].face.gameObject.SetActive(false);
            float wide = x - WordGap;
            tray.anchoredPosition = new Vector2(-wide * .5f, tray.anchoredPosition.y);
            tray.sizeDelta = new Vector2(wide, RowHeight);
        }

        void MakeTile()
        {
            var face = Rect("Tile " + (tiles.Count + 1), tray, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(Tile, RowHeight));
            var faceImage = Paint(face.gameObject, LobbyIcons.RoundedSprite(TileRadius), TileGot);
            var rim = Rect("Rim", face, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            rim.anchorMax = Vector2.one; rim.offsetMin = rim.offsetMax = Vector2.zero;
            var rimImage = Paint(rim.gameObject, LobbyIcons.FrameSprite(TileRadius, 1), TileRim);
            var letter = Text("Letter", face, LobbyFonts.Black, LetterFont, TileMissingInk, TextAlignmentOptions.Center);
            letter.rectTransform.anchorMin = Vector2.zero; letter.rectTransform.anchorMax = Vector2.one;
            letter.rectTransform.offsetMin = letter.rectTransform.offsetMax = Vector2.zero;
            tiles.Add((faceImage, rimImage, letter));
        }

        static void Row(RectTransform rt, float top, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(Width - 2f * Pad, height);
        }

        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        static RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        static Image Paint(GameObject host, Sprite sprite, Color colour)
        {
            var image = host.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = colour; image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI Text(string label, Transform parent, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rt = Rect(label, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var chosen = font ? font : GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
            if (chosen) t.font = chosen;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Min(MinFont, fontSize); t.fontSizeMax = fontSize;
            t.raycastTarget = false;
            return t;
        }
    }
}
