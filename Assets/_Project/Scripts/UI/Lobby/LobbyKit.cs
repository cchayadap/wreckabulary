using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public static class LobbyKit
    {
        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);

        public static float WebAlpha(float alpha) => 1f - Mathf.Pow(1f - Mathf.Clamp01(alpha), 2.2f);

        public static Color Web(uint rgb, float alpha) => Hex(rgb, WebAlpha(alpha));

        public static Color Mist(float alpha) => Hex(0x7d80bb, alpha);

        public static readonly Color Navy = Hex(0x0b0e45), Navy2 = Hex(0x151c7a), Cyan = Hex(0x2fe6ff);
        public static readonly Color Sun = Hex(0xffd21f), Sun2 = Hex(0xffad00), SunHi = Hex(0xffe55c), PlayHi = Hex(0xfff06a);
        public static readonly Color Hot = Hex(0xff5a1f), Lime = Hex(0x7be03a);
        public static readonly Color WoodHi = Hex(0xffd998), Wood = Hex(0xf2b25c), WoodLo = Hex(0xe38f34), Cocoa = Hex(0x3b2314), WoodInk = Hex(0x4a2a14);
        public static readonly Color CardSub = Hex(0x4a5290), Owned = Hex(0x2fa84f), Short = Hex(0xe6e9f7), Focus = Hex(0xedab51);
        public static readonly Color CoinRim = Hex(0xf0a400), CoinRimHi = Hex(0xffe066), CoinInk = Hex(0x8a5a10);

        public static readonly Color Panel = Hex(0x0a0e52);
        public static readonly Color PanelTop = Hex(0x3159ff), PanelBottom = Hex(0x18229e);
        public static readonly Color ScrimNavy = Hex(0x06072f);
        public static readonly Color Card = Mist(.122f);
        public static readonly Color Line = Mist(.188f);
        public static readonly Color TabIdle = Web(0x0a0e52, .79f), TabHover = Web(0x1d2bb0, .9f), TabPress = Hex(0x142092);
        public static readonly Color ChipFill = Web(0x0a0e52, .85f), ChipEdge = Mist(.25f);
        public static readonly Color PillFill = Mist(.133f), PillEdge = Mist(.333f);
        public static readonly Color Cream = Color.white;
        public static readonly Color Muted = Hex(0xdfe6ff);
        public static readonly Color Faded = new Color(1f, 1f, 1f, .5f);
        public static readonly Color Honey = Sun, Tomato = Hot;
        public static readonly Color Shade = Web(0x04052a, .6f);
        public const int TabRadius = 7;
        public static readonly Color Track = Web(0x0b0e45, .45f);
        public static readonly Color LimeHi = Hex(0xa6f56b), LimeLo = Hex(0x4cbf1d);
        public static readonly Color Picked = Hex(0xfff4b8);

        public enum Edge { Top, Right, Bottom }

        public enum Ink { Plain, Stroke, Drop }

        public static RectTransform Rect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Place(this RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        public static RectTransform Pin(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Fill(this RectTransform rect) => rect.Place(Vector2.zero, Vector2.one);

        public static Image Paint(this RectTransform rect, Color colour, bool rounded = false) => rect.Paint(colour, rounded ? 10 : -1);

        public static Image Paint(this RectTransform rect, Color colour, int radius)
        {
            if (!rect.TryGetComponent(out Image image)) image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            if (radius >= 0) { image.sprite = LobbyIcons.RoundedSprite(radius); image.type = Image.Type.Sliced; }
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
        {
            var label = Rect(parent, "Text").gameObject.AddComponent<TextMeshProUGUI>();
            var font = (style & FontStyles.Bold) != 0 ? LobbyFonts.Black : LobbyFonts.Body;
            if (font) { label.font = font; style &= ~FontStyles.Bold; }
            else if (GameAssets.I && GameAssets.I.font) label.font = GameAssets.I.font;
            label.text = text; label.fontSize = size; label.color = colour; label.alignment = align;
            label.fontStyle = style; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static TextMeshProUGUI Display(Transform parent, string text, float size, Color colour,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Ink ink = Ink.Plain)
        {
            var label = Text(parent, text, size, colour, align);
            var font = LobbyFonts.Display;
            if (!font) { label.fontStyle = FontStyles.Bold; return label; }
            label.font = font;
            var material = ink == Ink.Stroke ? LobbyFonts.Stroke : ink == Ink.Drop ? LobbyFonts.Drop : null;
            if (material) label.fontSharedMaterial = material;
            return label;
        }

        public static TextMeshProUGUI Caps(Transform parent, string text, float size = 15, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var label = Text(parent, Upper(text), size, Cyan, align, FontStyles.Bold);
            label.characterSpacing = 6;
            return label;
        }

        public static Image Icon(Transform parent, string icon, Color colour)
        {
            var image = Rect(parent, "Icon " + icon).gameObject.AddComponent<Image>();
            image.sprite = LobbyIcons.Get(icon); image.color = colour; image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        public static Image Face(RectTransform rect, Color fill, int radius, Color? edge = null, int edgeWidth = 0, float drop = 0,
            Color? to = null, float skew = 0, Color? dropColour = null)
        {
            var image = rect.Paint(to.HasValue ? Color.white : fill, radius);
            image.raycastTarget = false;
            if (skew != 0) rect.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            if (to.HasValue) rect.gameObject.AddComponent<LobbyGradient>().Set(fill, to.Value);
            if (drop > 0) Drop(rect, drop, dropColour ?? Navy);
            if (edge.HasValue && edgeWidth > 0)
            {
                var frame = Frame(rect, edge.Value, radius, edgeWidth);
                if (skew != 0) frame.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            }
            return image;
        }

        public static Shadow Drop(RectTransform rect, float depth, Color colour)
        {
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = colour;
            shadow.effectDistance = new Vector2(0, -depth);
            shadow.useGraphicAlpha = false;
            return shadow;
        }

        public static Image Frame(RectTransform rect, Color colour, int radius, int width, string name = "Edge", float outset = 0)
        {
            var frame = Rect(rect, name).Place(Vector2.zero, Vector2.one, new Vector2(-outset, -outset), new Vector2(outset, outset));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = frame.gameObject.AddComponent<Image>();
            image.sprite = LobbyIcons.FrameSprite(Mathf.Max(0, radius + Mathf.RoundToInt(outset)), width);
            image.type = Image.Type.Sliced;
            image.color = colour; image.raycastTarget = false;
            return image;
        }

        public static Image Ring(RectTransform rect, Color colour, int radius, int width) => Frame(rect, colour, radius, width, "Ring", width);

        public static LobbyGradient Gradient(Graphic graphic, Color from, Color to, bool horizontal = false)
        {
            graphic.color = Color.white;
            var gradient = graphic.gameObject.AddComponent<LobbyGradient>();
            gradient.Set(from, to, horizontal);
            return gradient;
        }

        public static Button Button(Transform parent, string name, Color colour, Action click, bool rounded = true) =>
            Button(parent, name, colour, click, rounded ? 12 : -1);

        public static Button Button(Transform parent, string name, Color colour, Action click, int radius,
            Color? edge = null, int edgeWidth = 0, float drop = 0, Color? to = null, float skew = 0)
        {
            var rect = Rect(parent, name);
            var hit = rect.Paint(Color.clear);
            var body = Rect(rect, "Body").Fill();
            Face(body, colour, radius, edge, edgeWidth, drop, to, skew);
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            var press = rect.gameObject.AddComponent<LobbyPress>();
            press.Body = body;
            if (drop > 0) { press.Drop = body.GetComponent<Shadow>(); press.DropRest = drop; }
            var ring = Frame(body, Focus, Mathf.Max(radius, 0), 4, "Focus", 5);
            if (skew != 0) ring.gameObject.AddComponent<LobbySkew>().Degrees = skew;
            ring.enabled = false;
            press.Ring = ring;
            button.onClick.AddListener(() => click?.Invoke());
            return button;
        }

        public static RectTransform Body(this Selectable button) =>
            button.TryGetComponent(out LobbyPress press) && press.Body ? press.Body : (RectTransform)button.transform;

        public static Image FaceOf(this Selectable button) => button.Body().GetComponent<Image>();

        public static Button LabelButton(Transform parent, string title, Color colour, Color text, float size, Action click)
        {
            var button = Button(parent, title, colour, click);
            Display(button.Body(), title, size, text).rectTransform.Fill();
            return button;
        }

        public static Button Pill(Transform parent, string name, string text, float size, Action click)
        {
            var button = Button(parent, name, PillFill, click, 10, PillEdge, 2);
            var body = button.Body();
            var label = Display(body, Upper(text), size, Cream);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
            var face = body.GetComponent<Image>();
            var edge = body.Find("Edge").GetComponent<Image>();
            button.GetComponent<LobbyPress>().Hot = hot =>
            {
                face.color = hot ? Sun : PillFill;
                edge.color = hot ? Navy : PillEdge;
                label.color = hot ? Navy : Cream;
            };
            return button;
        }

        public static Button Danger(Transform parent, string title, float size, Action click)
        {
            var button = Button(parent, title, Hot, click, 12, Navy, 3, 4);
            Display(button.Body(), title, size, Cream, TextAlignmentOptions.Center, Ink.Stroke).rectTransform.Fill();
            return button;
        }

        public static Button Primary(Transform parent, string name, string word, Action click, float tile = 62, float arrow = 38)
        {
            var button = Button(parent, name, PlayHi, click, 18, Navy, 5, 10, Sun2);
            var press = button.GetComponent<LobbyPress>();
            press.Sink = 7f; press.DropPressed = 2f;
            var content = Row(button.Body(), "Content", 7);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            for (int i = 0; i < word.Length; i++)
                LetterTile(content, word[i], tile, (i - (word.Length - 1) / 2f) * 3f);
            Rect(content, "Gap").Size(7, tile);
            Icon(content, LobbyIcons.Arrow, Navy).Size(arrow, arrow);
            return button;
        }

        public static RectTransform LetterTile(Transform parent, char letter, float size, float degrees = 0) =>
            LetterTile(parent, letter.ToString(), size, degrees);

        public static RectTransform LetterTile(Transform parent, string letter, float size, float degrees = 0)
        {
            var tile = Rect(parent, "Tile " + letter);
            tile.Size(size, size);
            int radius = Mathf.RoundToInt(size * .22f);
            Face(tile, WoodHi, radius, Cocoa, 2, 3, WoodLo, dropColour: Cocoa);
            var shine = Rect(tile, "Shine").Place(new Vector2(0, 1), Vector2.one, new Vector2(size * .2f, -size * .14f), new Vector2(-size * .2f, -size * .1f));
            shine.Paint(new Color(1, 1, 1, .6f), 1).raycastTarget = false;
            Display(tile, letter, size * .7f, WoodInk).rectTransform.Fill();
            tile.localRotation = Quaternion.Euler(0, 0, -degrees);
            return tile;
        }

        public static RectTransform Coin(Transform parent, float size)
        {
            var coin = Rect(parent, "Coin");
            coin.Size(size, size);
            Disc(coin, "Rim", size * 22f / 24f, CoinInk);
            Disc(coin, "Face", size * 18.8f / 24f, Sun);
            Disc(coin, "Ring", size * 15.6f / 24f, CoinRim);
            Disc(coin, "Middle", size * 13.2f / 24f, CoinRimHi);
            var w = Icon(coin, LobbyIcons.CoinW, CoinInk);
            w.rectTransform.Fill();
            return coin;
        }

        public static Image Disc(Transform parent, string name, float diameter, Color colour)
        {
            var disc = Rect(parent, name).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter, diameter));
            var image = disc.Paint(colour, Mathf.Max(1, Mathf.RoundToInt(diameter / 2f)));
            image.raycastTarget = false;
            return image;
        }

        public static RawImage ItemImage(Transform parent, string id, float size, float degrees = 0)
        {
            var texture = Resources.Load<Texture2D>("UI/Items/" + id);
            if (!texture) return null;
            var rect = Rect(parent, "Art " + id).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            rect.localRotation = Quaternion.Euler(0, 0, degrees);
            var raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = texture; raw.raycastTarget = false;
            return raw;
        }

        public static Color ModeColour(string mode) => mode switch
        {
            "Dibs" => Hex(0xef5b2b),
            "Duos" => Hex(0x3fa9dd),
            "MovingOut" => Hex(0x6fa957),
            "MovingDay" => Hex(0x9471dc),
            "Tutorial" => Hex(0xf2b230),
            "Workshop" => Hex(0x22b8a5),
            "Room" => Hex(0xff3d9e),
            _ => Sun,
        };

        public static string ModeArt(string mode) => mode switch
        {
            "Dibs" => "BAT",
            "Duos" => "BALL",
            "MovingDay" => "SOFA",
            "Tutorial" => "BOOK",
            "Workshop" => "HAMMER",
            "Room" => "CAKE",
            _ => "BOX",
        };

        public static LobbyTab Tab(Transform parent, string title, string icon, Action click, float width = 200, float height = 48, float size = 24,
            float skew = 10)
        {
            var button = Button(parent, title, TabIdle, click, TabRadius, Line, 2, 0, null, skew);
            var body = button.Body();
            var gradient = body.gameObject.AddComponent<LobbyGradient>();
            gradient.Set(SunHi, Sun2);
            gradient.enabled = false;
            var drop = Drop(body, 5, Navy);
            drop.enabled = false;
            var content = Row(body, "Content", 8);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(22, 22, 0, 0);
            Image glyph = null;
            if (icon != null) { glyph = Icon(content, icon, Cream); glyph.Size(22, 22); }
            var label = Display(content, Upper(title), size, Cream);
            label.characterSpacing = 3;
            label.Size(-1, height);
            button.Size(width, height);
            var tab = button.gameObject.AddComponent<LobbyTab>();
            tab.Init(button, body.GetComponent<Image>(), body.Find("Edge").GetComponent<Image>(), gradient, drop, label, glyph);
            return tab;
        }

        public static Button IconButton(Transform parent, string icon, string hint, Action click)
        {
            var button = Button(parent, hint, Color.white, click, 26, Navy, 4, 4);
            var body = button.Body();
            var glyph = Icon(body, icon, Navy);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            var face = body.GetComponent<Image>();
            var press = button.GetComponent<LobbyPress>();
            press.Tilt = 8f;
            press.Hot = hot => face.color = hot ? Sun : Color.white;
            button.gameObject.AddComponent<LobbyHint>().Text = hint;
            return button;
        }

        public static RectTransform Segment(Transform parent, string name)
        {
            var pill = Row(parent, name, 2, 4);
            pill.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            Face(pill, TabIdle, 12, Line, 2);
            return pill;
        }

        public static LobbyTab IconTab(Transform parent, string icon, string name, Action click, Color? hover = null)
        {
            var button = Button(parent, name, Color.clear, click, 10, Navy, 2);
            var body = button.Body();
            var gradient = body.gameObject.AddComponent<LobbyGradient>();
            gradient.Set(SunHi, Sun2);
            gradient.enabled = false;
            var drop = Drop(body, 3, Navy);
            drop.enabled = false;
            var glyph = Icon(body, icon, Cream);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(9, 9), new Vector2(-9, -9));
            button.Size(44, 44);
            button.GetComponent<LobbyPress>().Tilt = 8f;
            button.gameObject.AddComponent<LobbyHint>().Text = name;
            var tab = button.gameObject.AddComponent<LobbyTab>();
            tab.Style = LobbyTab.Look.Cell;
            tab.Radius = 10;
            if (hover.HasValue) tab.HoverColour = hover.Value;
            tab.Init(button, body.GetComponent<Image>(), body.Find("Edge").GetComponent<Image>(), gradient, drop, null, glyph);
            return tab;
        }

        static readonly System.Collections.Generic.Dictionary<(Edge, int, float, int), Sprite> scrims =
            new System.Collections.Generic.Dictionary<(Edge, int, float, int), Sprite>();

        public static Image Scrim(Transform parent, string name, Edge edge, int length, float webAlpha, int flat = 0)
        {
            var rect = Rect(parent, name);
            switch (edge)
            {
                case Edge.Top: rect.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -length), Vector2.zero); break;
                case Edge.Bottom: rect.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, length)); break;
                default: rect.Place(new Vector2(1, 0), Vector2.one, new Vector2(-length, 0), Vector2.zero); break;
            }
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ScrimSprite(edge, length, webAlpha, flat);
            image.color = ScrimNavy;
            image.raycastTarget = false;
            return image;
        }

        static Sprite ScrimSprite(Edge edge, int length, float webAlpha, int flat)
        {
            var key = (edge, length, webAlpha, flat);
            if (scrims.TryGetValue(key, out var sprite) && sprite) return sprite;
            const int Texels = 256;
            bool vertical = edge != Edge.Right;
            var texture = new Texture2D(vertical ? 1 : Texels, vertical ? Texels : 1, TextureFormat.RGBA32, false)
            { name = "Lobby scrim " + edge, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Texels];
            for (int i = 0; i < Texels; i++)
            {
                float t = (i + .5f) / Texels;
                float inward = (edge == Edge.Bottom ? t : 1f - t) * length;
                float fall = inward <= flat ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (inward - flat) / Mathf.Max(1f, length - flat));
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(WebAlpha(webAlpha * fall) * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            sprite.name = texture.name;
            scrims[key] = sprite;
            return sprite;
        }

        public static Image PanelFace(RectTransform rect, int radius, int edgeWidth = 4, float drop = 6) =>
            Face(rect, PanelTop, radius, Navy, edgeWidth, drop, PanelBottom);

        public static Button PickCard(Transform parent, string name, Color accent, string caps, string title, string detail,
            Action click, float art, Color? detailColour = null)
        {
            var button = Button(parent, name, PanelTop, click, 17, Navy, 4, 6, PanelBottom);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Sink = 3f; press.DropPressed = 2f;
            var body = button.Body();
            var square = Rect(body, "Art").Pin(new Vector2(0, .5f), new Vector2(14, 0), new Vector2(art, art));
            Face(square, accent, 14, Navy, 3);
            var chip = Rect(body, "Change chip").Pin(new Vector2(1, .5f), new Vector2(-16, 0), new Vector2(104, 34));
            chip.Paint(Hex(0x4157d4), 10).raycastTarget = false;
            Frame(chip, Hex(0x6d7ede), 10, 2);
            var change = Display(chip, "CHANGE", 17, Cream);
            change.characterSpacing = 2;
            change.rectTransform.Fill();
            var words = Column(body, "Words", 0);
            words.Place(Vector2.zero, Vector2.one, new Vector2(14 + art + 16, 8), new Vector2(-132, -8));
            var column = words.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleLeft;
            var kicker = Text(words, Upper(caps), 13, new Color(1f, 1f, 1f, .75f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            kicker.characterSpacing = 8;
            kicker.Size(-1, 18);
            var head = Display(words, title, 32, Cream, TextAlignmentOptions.MidlineLeft, Ink.Stroke);
            head.enableAutoSizing = true; head.fontSizeMin = 22; head.fontSizeMax = 32;
            head.Size(-1, 38);
            if (!string.IsNullOrEmpty(detail))
                Text(words, detail, 16, detailColour ?? Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 22);
            return button;
        }

        public static string Upper(string text) => (text ?? "").ToUpperInvariant();

        public static RectTransform Column(Transform parent, string name, float spacing, int padding = 0)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform Row(Transform parent, string name, float spacing, int padding = 0)
        {
            var rect = Rect(parent, name);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing; layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false; layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return rect;
        }

        public static RectTransform Grid(Transform parent, string name, Vector2 cell, float spacing)
        {
            var rect = Rect(parent, name);
            var grid = rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell; grid.spacing = new Vector2(spacing, spacing);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            return rect;
        }

        public static RectTransform FitRow(Transform parent, string name, Vector2 cell, float spacing, int count)
        {
            var area = Rect(parent, name).Size(-1, cell.y);
            var row = Grid(area, "Cards", cell, spacing);
            var grid = row.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
            row.anchoredPosition = Vector2.zero;
            row.sizeDelta = new Vector2(Mathf.Max(1, count) * (cell.x + spacing) - spacing, cell.y);
            area.gameObject.AddComponent<LobbyFit>().Target = row;
            return row;
        }

        public static T Size<T>(this T component, float width, float height = -1, float flexibleWidth = -1) where T : Component
        {
            if (!component.TryGetComponent(out LayoutElement element)) element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width; element.preferredHeight = height;
            element.flexibleWidth = flexibleWidth >= 0 ? flexibleWidth : width >= 0 ? 0 : -1;
            element.flexibleHeight = height >= 0 ? 0 : -1;
            if (width >= 0) element.minWidth = width;
            if (height >= 0) element.minHeight = height;
            return component;
        }

        public static TextMeshProUGUI Heading(Transform parent, string text)
        {
            var label = Display(parent, Upper(text), 24, Cyan, TextAlignmentOptions.BottomLeft);
            label.characterSpacing = 3;
            label.Size(-1, 34);
            return label;
        }

        public static RectTransform Scroll(Transform parent, string name, float spacing)
        {
            var view = Rect(parent, name).Fill();
            view.Paint(new Color(0, 0, 0, 0));
            view.gameObject.AddComponent<RectMask2D>();
            var content = Column(view, "Content", spacing);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = view; scroll.content = content;
            view.gameObject.AddComponent<LobbyScrollFollow>();
            return content;
        }

        public static Button Chip(Transform parent, string title, bool on, Action click, bool locked = false, float size = 22)
        {
            var button = on
                ? Button(parent, title, SunHi, click, 10, Navy, 3, 4, Sun2)
                : Button(parent, title, Card, click, 10, ChipEdge, 2);
            var label = Display(button.Body(), title, size, on ? Navy : locked ? Faded : Cream);
            label.characterSpacing = 1;
            label.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            if (on) button.GetComponent<LobbyPress>().Lift = 0f;
            return button;
        }

        public static TextMeshProUGUI SectionLabel(Transform parent, string text)
        {
            var label = Display(parent, Upper(text), 20, Cyan, TextAlignmentOptions.BottomLeft);
            label.characterSpacing = 2;
            label.Size(-1, 28);
            return label;
        }

        public static RectTransform Segmented(Transform parent, string name, IReadOnlyList<(string id, string label)> choices, string picked,
            Action<string> pick, float height = 48, float size = 18)
        {
            var track = Row(parent, name, 4, 4);
            track.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            Face(track, Track, 14);
            track.Size(-1, height);
            foreach (var (id, label) in choices)
            {
                bool on = id == picked;
                var button = on ? Button(track, id, SunHi, () => pick(id), 10, Navy, 3, 3, Sun2) : Button(track, id, Color.clear, () => pick(id), 10);
                button.Size(-1, -1, 1);
                var text = Display(button.Body(), Upper(label), size, on ? Navy : Cream);
                text.characterSpacing = 1;
                text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size;
                text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
                var press = button.GetComponent<LobbyPress>();
                if (on) press.Lift = 0f;
                else
                {
                    var face = button.FaceOf();
                    press.Hot = hot => face.color = hot ? TabHover : Color.clear;
                }
            }
            return track;
        }

        public static Button ToggleCard(Transform parent, string name, string icon, string label, bool on, Action click)
        {
            var button = Button(parent, name, on ? Picked : Color.white, click, 14, Navy, 3, 4);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 3f; press.Tilt = 1f;
            if (on) press.Lean = 1.5f;
            var body = button.Body();
            Icon(body, icon, Navy).rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -12), new Vector2(34, 34));
            var text = Display(body, label, 17, Navy);
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 17;
            text.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 34));
            var box = Rect(body, "Tick").Pin(Vector2.one, new Vector2(-8, -8), new Vector2(22, 22));
            Face(box, on ? Sun : Color.white, 6, Navy, 2);
            if (on) Icon(box, LobbyIcons.Check, Navy).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            return button;
        }

        public static Button Confirm(Transform parent, string name, string text, Action click)
        {
            var button = Button(parent, name, LimeHi, click, 16, Navy, 3, 5, LimeLo);
            var press = button.GetComponent<LobbyPress>();
            press.Sink = 3f; press.DropPressed = 2f;
            var body = button.Body();
            var shine = Rect(body, "Shine").Place(new Vector2(0, 1), Vector2.one, new Vector2(16, -13), new Vector2(-16, -8));
            shine.Paint(new Color(1f, 1f, 1f, .45f), 2).raycastTarget = false;
            var content = Row(body, "Content", 10);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            var label = Display(content, Upper(text), 24, Navy);
            label.characterSpacing = 2;
            label.Size(-1, 34);
            Icon(content, LobbyIcons.Check, Navy).Size(24, 24);
            return button;
        }

        public static Button PriceTag(Transform parent, string name, int price, bool poor, Action click, float size = 18)
        {
            var button = Button(parent, name, poor ? Short : Color.white, click, 10, Navy, 2, 3);
            var press = button.GetComponent<LobbyPress>();
            press.Tilt = 2f;
            var content = Row(button.Body(), "Content", 6);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            Coin(content, size + 4);
            var ink = poor ? new Color(Navy.r, Navy.g, Navy.b, .5f) : Navy;
            Display(content, price.ToString("N0", CultureInfo.InvariantCulture), size, ink).Size(-1, size + 8);
            if (!poor)
            {
                var face = button.FaceOf();
                press.Hot = hot => face.color = hot ? Sun : Color.white;
            }
            return button;
        }

        public static RectTransform Kbd(Transform parent, string key)
        {
            var cap = Rect(parent, "Key " + key);
            Face(cap, Color.white, 6, Navy, 2, 2);
            cap.Size(Mathf.Max(30f, 16f + 11f * key.Length), 28);
            Display(cap, key, 15, Navy).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, 0));
            return cap;
        }

        public static void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }
}
