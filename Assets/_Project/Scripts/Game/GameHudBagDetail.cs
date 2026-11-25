using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public partial class GameHud
    {
        public const float BookDetailDelay = .15f, BookDetailHold = .4f;
        public const string BookDetailName = "Recipe detail";
        const float DetailRowHeight = 26f, DetailSectionGap = 10f, DetailHeadLine = 26f, DetailNameTall = 36f;
        const float DetailTile = 28f, DetailTileTall = 32f, DetailTileStep = 32f, DetailTileFont = 21f, DetailLettersGap = 6f;
        const float DetailStar = 16f, DetailStarStep = 18f, DetailEffectGap = 4f, DetailRadius = 15f, DetailLineSpacing = 1.25f;

        sealed class DetailSheet
        {
            public string word;
            public RectTransform root;
            public float height;
            public Image[] tiles, rims;
            public TextMeshProUGUI[] letters;
            public TextMeshProUGUI missing;
        }

        sealed class BookCardPeek : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
        {
            public GameHud hud;
            public BookCard card;

            public void OnPointerEnter(PointerEventData data) { if (hud) hud.PeekBookCard(card, true, Touched(data)); }
            public void OnPointerExit(PointerEventData data) { if (hud) hud.PeekBookCard(card, false, Touched(data)); }
            public void OnPointerDown(PointerEventData data) { if (hud && Touched(data)) hud.PressBookCard(card, data); }
            public void OnPointerUp(PointerEventData data) { if (hud && Touched(data)) hud.PeekBookCard(card, false, true); }
            public void OnSelect(BaseEventData _) { if (hud) hud.FocusBookCard(card, true); }
            public void OnDeselect(BaseEventData _) { if (hud) hud.FocusBookCard(card, false); }

            static bool Touched(PointerEventData data) => data is ExtendedPointerEventData pointer && pointer.pointerType == UIPointerType.Touch;
        }

        sealed class BookDetailDriver : MonoBehaviour
        {
            public GameHud hud;

            void LateUpdate() { if (hud) hud.TickBookDetail(); }
            void OnDisable() { if (hud) hud.ForgetBookPeek(); }
        }

        RectTransform bookDetail, bookView, bookDetailParent;
        readonly Dictionary<string, DetailSheet> detailSheets = new();
        readonly Vector3[] detailCorners = new Vector3[4];
        DetailSheet detailShown;
        BookCard peekHover, peekFocus;
        float peekHoverSince, peekFocusSince;
        bool peekTouch;
        PointerEventData peekPress;

        public string BookDetailWord => detailShown != null && bookDetail && bookDetail.gameObject.activeInHierarchy ? detailShown.word : null;

        void BuildBookDetail(RectTransform panel)
        {
            bookDetailParent = panel;
            bookView = (RectTransform)bagBook.Find("View");
            bookDetail = Panel(BookDetailName, panel, new Vector2(.5f, .5f), Vector2.zero, new Vector2(RecipeSheet.Width, DetailRowHeight), new Vector2(0f, 1f), Teal);
            Edge(bookDetail, BagCardEdge, DetailRadius);
            AddHudShadow(bookDetail.GetComponent<Image>());
            var group = bookDetail.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            bookDetail.gameObject.SetActive(false);
            panel.gameObject.AddComponent<BookDetailDriver>().hud = this;
            foreach (var card in bookCards)
            {
                var peek = card.button.gameObject.AddComponent<BookCardPeek>();
                peek.hud = this; peek.card = card;
            }
        }

        void PeekBookCard(BookCard card, bool on, bool touch)
        {
            if (on) { peekHover = card; peekHoverSince = Time.unscaledTime; peekTouch = touch; return; }
            if (peekHover == card) { peekHover = null; peekPress = null; }
            if (peekFocus == card) peekFocus = null;
        }

        void FocusBookCard(BookCard card, bool on)
        {
            if (on) { peekFocus = card; peekFocusSince = Time.unscaledTime; }
            else if (peekFocus == card) peekFocus = null;
        }

        void PressBookCard(BookCard card, PointerEventData data)
        {
            PeekBookCard(card, true, true);
            peekPress = data;
        }

        void ForgetBookPeek()
        {
            peekHover = peekFocus = null;
            peekPress = null;
            HideBookDetail();
        }

        void TickBookDetail()
        {
            var card = peekHover ?? peekFocus;
            if (card == null || !BagOpen || BagDragging || !LocalPlayer || !BookCardInView(card)) { HideBookDetail(); return; }
            bool hovered = card == peekHover;
            float since = hovered ? peekHoverSince : peekFocusSince;
            float wait = hovered && peekTouch ? BookDetailHold : BookDetailDelay;
            if (Time.unscaledTime - since < wait)
            {
                if (detailShown != null && detailShown.word != card.word) HideBookDetail();
                return;
            }
            ShowBookDetail(card);
            if (hovered && peekTouch && peekPress != null && BookDetailWord == card.word) peekPress.eligibleForClick = false;
        }

        bool BookCardInView(BookCard card)
        {
            if (!bookView || !card.button || !card.button.gameObject.activeInHierarchy) return false;
            var rt = (RectTransform)card.button.transform;
            var centre = bookView.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            return bookView.rect.Contains(centre);
        }

        void ShowBookDetail(BookCard card)
        {
            if (detailShown == null || detailShown.word != card.word)
            {
                HideBookDetail();
                if (!detailSheets.TryGetValue(card.word, out var sheet)) detailSheets[card.word] = sheet = BuildDetailSheet(card.word);
                if (sheet == null) return;
                detailShown = sheet;
                sheet.root.gameObject.SetActive(true);
                bookDetail.sizeDelta = new Vector2(RecipeSheet.Width, sheet.height);
                bookDetail.gameObject.SetActive(true);
                bookDetail.SetAsLastSibling();
            }
            PlaceBookDetail(card);
            RefreshDetailLetters();
        }

        void HideBookDetail()
        {
            if (detailShown != null && detailShown.root) detailShown.root.gameObject.SetActive(false);
            detailShown = null;
            if (bookDetail && bookDetail.gameObject.activeSelf) bookDetail.gameObject.SetActive(false);
        }

        void PlaceBookDetail(BookCard card)
        {
            var rt = (RectTransform)card.button.transform;
            rt.GetWorldCorners(detailCorners);
            Vector2 min = bookDetailParent.InverseTransformPoint(detailCorners[0]), max = bookDetailParent.InverseTransformPoint(detailCorners[2]);
            var area = bookDetailParent.rect;
            var spot = SheetPlacement.Place(new SheetBox(min.x, min.y, max.x, max.y), RecipeSheet.Width, detailShown.height,
                new SheetBox(area.xMin, area.yMin, area.xMax, area.yMax));
            bookDetail.anchoredPosition = new Vector2(spot.Box.Left, spot.Box.Top) - area.center;
        }

        void RefreshDetailLetters()
        {
            var sheet = detailShown;
            if (sheet == null || !LocalPlayer) return;
            var inv = LocalPlayer.Inventory;
            bool free = inv && inv.SpellsFree;
            var held = RecipeSheet.Held(sheet.word, inv ? inv.Letters : null, free);
            for (int i = 0; i < held.Length; i++)
            {
                sheet.tiles[i].color = held[i] ? TileGot : Color.clear;
                sheet.rims[i].enabled = !held[i];
                sheet.letters[i].color = held[i] ? TileGotInk : TileMissingInk;
            }
            sheet.missing.text = RecipeSheet.LettersLine(RecipeSheet.Missing(held), free);
        }

        DetailSheet BuildDetailSheet(string word)
        {
            var config = GameConfig.Current;
            if (!config.Items.TryGet(word, out var item)) return null;
            var text = RecipeSheet.Build(item, config.Tuning);
            var root = Rect(word, bookDetail, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(root);
            var sheet = new DetailSheet { word = word, root = root };
            float pad = RecipeSheet.Pad, y = -pad, head = pad + RecipeSheet.Picture + RecipeSheet.HeadGap;

            var picture = Rect("Picture", root, new Vector2(0f, 1f), new Vector2(pad, y), Vector2.one * RecipeSheet.Picture).gameObject.AddComponent<RawImage>();
            picture.rectTransform.pivot = new Vector2(0f, 1f); picture.raycastTarget = false;
            Wreckabulary.UI.ItemArt.Apply(picture, word);
            var heading = DetailText("Name", root, new Vector2(head, y), RecipeSheet.KindWidth, DetailNameTall, RecipeSheet.NameFont, BagInk);
            heading.fontStyle = FontStyles.Bold; heading.text = text.Name;
            float x = head;
            for (int s = 0; s < text.Stars; s++)
            {
                var star = CreateImage("Star", root, new Vector2(0f, 1f), new Vector2(x, y - DetailNameTall - (DetailHeadLine - DetailStar) * .5f), Vector2.one * DetailStar, TierColour(text.Tier));
                star.rectTransform.pivot = new Vector2(0f, 1f); star.sprite = SparkSprite(); star.preserveAspect = true;
                x += DetailStarStep;
            }
            if (text.Stars > 0) x += DetailLettersGap;
            DetailText("Tier", root, new Vector2(x, y - DetailNameTall), RecipeSheet.KindWidth - (x - head), DetailHeadLine, RecipeSheet.KindFont, TierColour(text.Tier)).text = text.TierName;
            DetailText("Kind", root, new Vector2(head, y - DetailNameTall - DetailHeadLine), RecipeSheet.KindWidth, DetailHeadLine, RecipeSheet.KindFont, Hex(0xfff4e2, .8f)).text = text.Kind;
            y -= Mathf.Max(RecipeSheet.Picture, DetailNameTall + 2f * DetailHeadLine) + DetailSectionGap;

            foreach (var row in text.Rows)
            {
                DetailText("Label " + row.Label, root, new Vector2(pad, y), RecipeSheet.LabelWidth, DetailRowHeight, RecipeSheet.RowFont, Hex(0xfff4e2, .75f)).text = row.Label;
                DetailText("Value " + row.Label, root, new Vector2(pad + RecipeSheet.LabelWidth, y), RecipeSheet.ValueWidth, DetailRowHeight, RecipeSheet.RowFont, BagInk).text = row.Value;
                y -= DetailRowHeight;
            }
            y -= DetailSectionGap;

            for (int i = 0; i < text.Effects.Count; i++)
            {
                var effect = DetailText($"Effect {i + 1}", root, new Vector2(pad, y), RecipeSheet.TextWidth, DetailRowHeight, RecipeSheet.EffectFont, Gold);
                effect.enableAutoSizing = false;
                effect.textWrappingMode = TextWrappingModes.Normal;
                effect.text = text.Effects[i];
                float tall = Mathf.Max(DetailRowHeight, Mathf.Ceil(EffectHeight(effect, text.Effects[i])));
                effect.rectTransform.sizeDelta = new Vector2(RecipeSheet.TextWidth, tall);
                y -= tall + DetailEffectGap;
            }
            y -= DetailSectionGap - DetailEffectGap;

            if (!tileRing) tileRing = MakeShape(false, 4.4f);
            sheet.tiles = new Image[word.Length]; sheet.rims = new Image[word.Length]; sheet.letters = new TextMeshProUGUI[word.Length];
            for (int i = 0; i < word.Length; i++)
            {
                var tile = Panel($"Tile {i + 1}", root, new Vector2(0f, 1f), new Vector2(pad + i * DetailTileStep, y), new Vector2(DetailTile, DetailTileTall), new Vector2(0f, 1f), Color.clear);
                var face = sheet.tiles[i] = tile.GetComponent<Image>(); face.pixelsPerUnitMultiplier = 11f / 3.75f;
                var rim = sheet.rims[i] = CreateImage("Rim", tile, Vector2.zero, Vector2.zero, Vector2.zero, TileRim);
                Stretch(rim.rectTransform); rim.sprite = tileRing; rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
                sheet.letters[i] = Text("Letter", tile, new Vector2(.5f, .5f), Vector2.zero, new Vector2(DetailTile, DetailTileTall), DetailTileFont, TileMissingInk);
                sheet.letters[i].text = word[i].ToString();
            }
            y -= DetailTileTall + DetailLettersGap;
            sheet.missing = DetailText("Letters", root, new Vector2(pad, y), RecipeSheet.TextWidth, DetailRowHeight, RecipeSheet.RowFont, BagInk);
            y -= DetailRowHeight;
            sheet.height = -y + pad;
            root.gameObject.SetActive(false);
            return sheet;
        }

        TextMeshProUGUI DetailText(string label, RectTransform parent, Vector2 topLeft, float width, float height, float size, Color colour)
        {
            var text = Text(label, parent, new Vector2(0f, 1f), topLeft, new Vector2(width, height), size, colour, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.enableAutoSizing = true; text.fontSizeMin = RecipeSheet.MinFont; text.fontSizeMax = size;
            return text;
        }

        static float EffectHeight(TextMeshProUGUI text, string line)
        {
            float measured = text.GetPreferredValues(line, RecipeSheet.TextWidth, 0f).y;
            if (measured > 1f) return measured;
            int perLine = Mathf.Max(1, RecipeSheet.MaxChars(RecipeSheet.TextWidth, RecipeSheet.EffectFont));
            return Mathf.Ceil(line.Length / (float)perLine) * RecipeSheet.EffectFont * DetailLineSpacing;
        }
    }
}
