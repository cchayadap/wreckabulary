using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPlayPageView : MonoBehaviour
    {
        public RectTransform Panel, Body, Queues, Choices, Summary;
        public TextMeshProUGUI Title, Subtitle;
        public Button Close;
        [Min(8)] public Vector2 ContentInset = new Vector2(20, 16);
        [Range(72, 140)] public float HeaderHeight = 88;
        [Range(88, 140)] public float SummaryHeight = 88;
        [Range(600, 1100)] public float NavigationMaximumWidth = 760;
        [Range(44, 64)] public float NavigationHeight = 48;
        UnityAction closeListener;
        bool optionLayout;
        float optionWidth = -1;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var role in new Component[] { Panel, Body, Queues, Choices, Summary, Title, Subtitle, Close })
                LobbyShopLayout.Require(this, role, "Play frame role");
            if (Title == Subtitle) throw new InvalidOperationException("Play title and subtitle must be separate text roles.");
            if (!Finite(NavigationMaximumWidth) || NavigationMaximumWidth < 600 || NavigationMaximumWidth > 1100 ||
                !Finite(NavigationHeight) || NavigationHeight < 44 || NavigationHeight > 64)
                throw new InvalidOperationException("Play navigation needs finite bounded width/height.");
            var regions = new[] { Queues, Choices, Summary };
            for (int i = 0; i < regions.Length; i++)
                for (int j = i + 1; j < regions.Length; j++)
                    if (regions[i].IsChildOf(regions[j]) || regions[j].IsChildOf(regions[i]))
                        throw new InvalidOperationException("Play dynamic regions must not contain one another.");
            if (!Title.transform.IsChildOf(Panel) || !Close.transform.IsChildOf(Panel) ||
                regions.Any(region => Title.transform.IsChildOf(region) || Close.transform.IsChildOf(region)))
                throw new InvalidOperationException("Play title and Close must stay in the panel header, outside dynamic regions.");
            if (new[] { Panel, Body, Queues, Choices, Summary }.Distinct().Count() != 5 ||
                !Body.IsChildOf(Panel) || !Queues.IsChildOf(Panel) || !Choices.IsChildOf(Body) || !Summary.IsChildOf(Body) ||
                !Choices.GetComponent<VerticalLayoutGroup>() || !Close.GetComponent<LobbyPress>() ||
                Close.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Play needs separate panel, queue, choice and summary regions with a runtime-bound Close action.");
        }

        public void Bind(Action close, string subtitle, bool compact, Vector2 maximum)
        {
            Validate();
            if (closeListener != null) Close.onClick.RemoveListener(closeListener);
            closeListener = () => close(); Close.onClick.AddListener(closeListener);
            Subtitle.text = subtitle ?? "";
            if (compact)
            {
                if (!Panel.TryGetComponent(out LobbyCompactPanel fit)) fit = Panel.gameObject.AddComponent<LobbyCompactPanel>();
                fit.MaximumSize = maximum; fit.Fit();
            }
        }

        public void BindOptions(Action close, string subtitle)
        {
            Bind(close, subtitle, false, Vector2.zero);
            ArrangeOptions();
        }

        public void ArrangeOptions()
        {
            Validate();
            optionLayout = true;
            Panel.Fill();
            FitOptions();
        }

        void FitOptions()
        {
            optionWidth = Panel.rect.width;
            Title.gameObject.SetActive(false);
            Subtitle.gameObject.SetActive(false);
            float width = Mathf.Min(NavigationMaximumWidth, Mathf.Max(1, Panel.rect.width - ContentInset.x * 2 - 88));
            Queues.Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(-width * .5f, -ContentInset.y - NavigationHeight), new Vector2(width * .5f, -ContentInset.y));
            ((RectTransform)Close.transform).Pin(Vector2.one, new Vector2(-14, -14), new Vector2(44, 44));
            float header = Mathf.Max(HeaderHeight, NavigationHeight + ContentInset.y * 2);
            Body.Place(Vector2.zero, Vector2.one, ContentInset, new Vector2(-ContentInset.x, -header));
            Choices.Place(Vector2.zero, Vector2.one, new Vector2(0, SummaryHeight + 12), Vector2.zero);
            Summary.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, SummaryHeight));
        }

        void LateUpdate() { if (optionLayout && !Mathf.Approximately(optionWidth, Panel.rect.width)) FitOptions(); }

        void OnDestroy() { if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }


    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LobbyPlayCardGrid : MonoBehaviour
    {
        public const float PictureAspect = 16f / 9f, Inset = 8, CaptionHeight = 40, PreferredWidth = 384, ShortestCard = 140, ViewportMargin = 28;
        public float MinimumWidth = 264;
        public int MaximumColumns = 3;
        /// <summary>Widest a card may grow. A short list (the course maps) sets no cap so its row fills the page.</summary>
        public float WidestCard = PreferredWidth;
        GridLayoutGroup grid;
        bool fitting;
        Vector2 viewportSize;

        public static float HeightFor(float width) => Mathf.Max(0f, width - Inset * 2) / PictureAspect + CaptionHeight + Inset;

        public static (int columns, Vector2 cell) Layout(float available, float spacing, float minimumWidth, int maximumColumns, float viewportHeight,
            float widest = PreferredWidth)
        {
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + spacing) / (Mathf.Min(PreferredWidth, minimumWidth) + spacing)), 1, Mathf.Max(1, maximumColumns));
            float width = Mathf.Max(1f, Mathf.Min(widest, (available - (columns - 1) * spacing) / columns));
            float height = HeightFor(width);
            if (viewportHeight > 0) height = Mathf.Min(height, Mathf.Max(ShortestCard, viewportHeight - ViewportMargin));
            return (columns, new Vector2(width, height));
        }

        void OnEnable() { grid = GetComponent<GridLayoutGroup>(); Fit(); }
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        internal void Fit()
        {
            if (!grid || fitting) return;
            fitting = true;
            try { FitCurrent(); }
            finally { fitting = false; }
        }

        void FitCurrent()
        {
            var rect = (RectTransform)transform;
            if (rect.rect.width <= 0 || transform.childCount == 0) return;
            var scroll = GetComponentInParent<ScrollRect>();
            var nextViewportSize = scroll && scroll.viewport ? scroll.viewport.rect.size : Vector2.zero;
            bool changed = nextViewportSize != viewportSize;
            viewportSize = nextViewportSize;
            var (columns, cell) = Layout(rect.rect.width, grid.spacing.x, MinimumWidth, MaximumColumns, viewportSize.y, WidestCard);
            int rows = Mathf.CeilToInt(transform.childCount / (float)columns);
            float height = rows * cell.y + (rows - 1) * grid.spacing.y;
            if (grid.startCorner != GridLayoutGroup.Corner.UpperLeft) { grid.startCorner = GridLayoutGroup.Corner.UpperLeft; changed = true; }
            if (grid.childAlignment != TextAnchor.UpperLeft) { grid.childAlignment = TextAnchor.UpperLeft; changed = true; }
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) { grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; changed = true; }
            if (grid.constraintCount != columns) { grid.constraintCount = columns; changed = true; }
            if (grid.cellSize != cell) { grid.cellSize = cell; changed = true; }
            var size = GetComponent<LayoutElement>();
            if (!size || !Mathf.Approximately(size.preferredHeight, height)) { rect.Size(-1, height); changed = true; }
            if (changed && scroll && scroll.content)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                if (selected && selected.transform.IsChildOf(scroll.content))
                    LobbyScrollFollow.Show(scroll, (RectTransform)selected.transform);
            }
        }
    }

    public sealed class LobbyPlayModeDock : MonoBehaviour
    {
        public RectTransform Choices, Goal;
        public float MaximumWidth = 1160, GoalHeight = 30, GoalGap = 2;
        bool fitting;

        void LateUpdate() => Fit();

        public void Fit()
        {
            if (fitting || !Choices || !(transform.parent is RectTransform parent)) return;
            fitting = true;
            try
            {
                var row = (RectTransform)transform;
                var layout = GetComponent<LayoutElement>();
                float height = Mathf.Max(44, layout ? layout.preferredHeight : row.rect.height);
                float width = Mathf.Min(MaximumWidth, Mathf.Max(1, parent.rect.width));
                Place(row, width, 0, height);
                var families = GetComponentInChildren<LobbyPlayModeFamilies>();
                if (families) families.Fit();
                height = Mathf.Max(44, layout ? layout.preferredHeight : row.rect.height);
                Place(row, width, 0, height);
                float below = height;
                if (Goal)
                {
                    Place(Goal, width, height + GoalGap, GoalHeight);
                    below += GoalGap + GoalHeight;
                }
                var choicesTop = new Vector2(Choices.offsetMax.x, -below - 12);
                if (Choices.offsetMax != choicesTop) Choices.offsetMax = choicesTop;
            }
            finally { fitting = false; }
        }

        static void Place(RectTransform rect, float width, float top, float height)
        {
            var anchor = new Vector2(.5f, 1);
            var minimum = new Vector2(-width * .5f, -top - height); var maximum = new Vector2(width * .5f, -top);
            if (rect.anchorMin != anchor || rect.anchorMax != anchor || rect.offsetMin != minimum || rect.offsetMax != maximum)
                rect.Place(anchor, anchor, minimum, maximum);
        }
    }

    public sealed class LobbyPlayModeBoard : MonoBehaviour
    {
        public LobbyPlayModeFamilies Sections;
        public TMP_Text Goal, Access;
        public RectTransform Preview;
        public float Gap = 36, GoalMinimumWidth = 420, GoalHeight = 30, GoalGap = 2, AccessHeight = 44;
        public float PreviewGap = 20, PreviewMinimumWidth = 240, TextMinimumWidth = 240;
        float arrangedWidth = -1f, besideRight;
        bool fitting;

        public bool Beside { get; private set; }

        public bool PreviewShown => Preview && Preview.TryGetComponent(out CanvasGroup group) && group.alpha > 0f;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        float Available => transform.parent is RectTransform parent ? parent.rect.width : ((RectTransform)transform).rect.width;

        public void Fit()
        {
            float width = Available;
            if (fitting || !Sections || width <= 0 || Mathf.Approximately(width, arrangedWidth)) return;
            fitting = true;
            try { Arrange(width); arrangedWidth = width; }
            finally { fitting = false; }
        }

        void Arrange(float width)
        {
            float columns = Sections.NaturalWidth;
            Beside = (Goal || Access) && width - columns - Gap >= GoalMinimumWidth;
            var sections = (RectTransform)Sections.transform;
            float sectionsWidth = Beside ? columns : width;
            LobbyPlayModeFamilies.Set(sections, Vector2.zero, new Vector2(sectionsWidth, sections.sizeDelta.y));
            Sections.Fit();
            float height = Sections.Height;
            LobbyPlayModeFamilies.Set(sections, Vector2.zero, new Vector2(sectionsWidth, height));
            float besideTop = Sections.CaptionHeight;
            besideRight = 0f;
            if (Preview)
            {
                // The picture fills the space under the family captions, right of the columns, without
                // making the mode block taller than the columns, so the houses below never move.
                float room = width - columns - Gap - TextRoom() - PreviewGap;
                float previewHeight = height - besideTop;
                float previewWidth = Mathf.Min(previewHeight * LobbyPlayModePreview.Aspect, room);
                bool shown = Beside && previewWidth >= PreviewMinimumWidth;
                // Hidden through a CanvasGroup: deactivating graphics here can land inside a layout rebuild.
                if (!Preview.TryGetComponent(out CanvasGroup group)) group = Preview.gameObject.AddComponent<CanvasGroup>();
                group.alpha = shown ? 1f : 0f; group.interactable = group.blocksRaycasts = shown;
                // Hidden by the canvas group, but kept at its smallest size so its labels still fit.
                if (!shown) LobbyPlayModeFamilies.Set(Preview, new Vector2(0, -besideTop), new Vector2(PreviewMinimumWidth, PreviewMinimumWidth / LobbyPlayModePreview.Aspect));
                if (shown)
                {
                    previewHeight = previewWidth / LobbyPlayModePreview.Aspect;
                    LobbyPlayModeFamilies.Set(Preview, new Vector2(width - previewWidth, -besideTop), new Vector2(previewWidth, previewHeight));
                    besideRight = previewWidth + PreviewGap;
                }
            }
            if (Goal)
            {
                float goalRows = besideRight > 0f ? 2f : 1f;
                var wrap = goalRows > 1f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                if (Goal.textWrappingMode != wrap) Goal.textWrappingMode = wrap;
                height = PlaceLine(Goal, width, columns, height, besideTop, Sections.ButtonHeight * goalRows, GoalHeight);
                besideTop += Sections.ButtonHeight * goalRows;
            }
            if (Access) height = PlaceLine(Access, width, columns, height, besideTop, AccessHeight, AccessHeight);
            if (!TryGetComponent(out LayoutElement element)) element = gameObject.AddComponent<LayoutElement>();
            if (Mathf.Approximately(element.preferredHeight, height)) return;
            element.preferredHeight = element.minHeight = height;
            if (transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        /// <summary>Width the text beside the preview needs: the who-can-play line never wraps, so it sets the floor.</summary>
        float TextRoom()
        {
            float need = TextMinimumWidth;
            if (!Access || string.IsNullOrEmpty(Access.text)) return need;
            float line = Access.GetPreferredValues(Access.text).x;
            if (Access.TryGetComponent(out LobbyPlayGoalInfo info) && info.Icon) line += info.IconWidth + LobbyPlayGoalInfo.Gap * 2;
            return Mathf.Max(need, Mathf.Ceil(line) + 8f);
        }

        float PlaceLine(TMP_Text line, float width, float columns, float height, float besideTop, float besideHeight, float underHeight)
        {
            var alignment = Beside ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            if (line.alignment != alignment) line.alignment = alignment;
            if (Beside)
            {
                LobbyPlayModeFamilies.Set(line.rectTransform, new Vector2(columns + Gap, -besideTop), new Vector2(width - columns - Gap - besideRight, besideHeight));
                height = Mathf.Max(height, besideTop + besideHeight);
            }
            else
            {
                LobbyPlayModeFamilies.Set(line.rectTransform, new Vector2(0, -height - GoalGap), new Vector2(width, underHeight));
                height += GoalGap + underHeight;
            }
            if (line.TryGetComponent(out LobbyPlayGoalInfo info)) info.Fit();
            return height;
        }
    }

    public sealed class LobbyPlayGoalInfo : MonoBehaviour
    {
        public const float Size = 44, Gap = 2;
        public TMP_Text Line;
        public RectTransform Icon;
        public float IconWidth = Size;
        float placedWidth = -1f;
        string placedText;
        bool placedCentred;

        void LateUpdate() => Fit();

        public void Fit()
        {
            if (!Line || !Icon) return;
            var rect = Line.rectTransform.rect;
            bool centred = Line.alignment == TextAlignmentOptions.Center;
            if (rect.width <= 0 || Mathf.Approximately(rect.width, placedWidth) && placedText == Line.text && placedCentred == centred) return;
            placedWidth = rect.width; placedText = Line.text; placedCentred = centred;
            var room = new Vector4(centred ? IconWidth + Gap : 0, 0, IconWidth + Gap, 0);
            if (Line.margin != room) Line.margin = room;
            Line.ForceMeshUpdate();
            var bounds = Line.textBounds;
            float end = bounds.size.x > 0 ? bounds.max.x : centred ? 0f : rect.xMin;
            Icon.anchoredPosition = new Vector2(end - rect.center.x + Gap + IconWidth * .5f, 0f);
        }
    }

    public sealed class LobbyPlayModeFamilies : MonoBehaviour
    {
        public const string Caption = "Family caption";
        public const float NoteWidth = 200f, ButtonSlack = 12f, ContentPadding = 16f;
        public const int NoteRows = 2;
        public float ButtonWidth = 104, ButtonHeight = 52, CaptionHeight = 22;
        public float Spacing = 6, StackSpacing = 0, FamilySpacing = 28, LineSpacing = 8, OneLineScale = .8f;
        public bool Stacked;
        readonly Dictionary<Transform, float> natural = new Dictionary<Transform, float>();
        float arrangedWidth = -1f;
        int arrangedCount = -1;
        bool fitting;

        public float Height { get; private set; }

        public float NaturalWidth
        {
            get
            {
                var families = Families();
                return families.Sum(Natural) + FamilySpacing * Mathf.Max(0, families.Count - 1);
            }
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            float width = ((RectTransform)transform).rect.width;
            if (fitting || width <= 0) return;
            int count = ActiveFamilies();
            if (count == arrangedCount && Mathf.Approximately(width, arrangedWidth)) return;
            fitting = true;
            try
            {
                Arrange(width);
                arrangedWidth = width; arrangedCount = count;
            }
            finally { fitting = false; }
        }

        int ActiveFamilies()
        {
            int count = 0;
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf) count++;
            return count;
        }

        List<RectTransform> Families() =>
            transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();

        void Arrange(float width)
        {
            var families = Families();
            if (families.Count == 0) return;
            float y = 0f;
            int start = 0;
            foreach (int size in Lines(families.Select(Natural).ToList(), FamilySpacing, width, OneLineScale, families.Select(NaturalHeight).ToList()))
            {
                y += ArrangeLine(families.GetRange(start, size), width, y) + LineSpacing;
                start += size;
            }
            Height = y - LineSpacing;
            if (!TryGetComponent(out LayoutElement element)) element = gameObject.AddComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, Height)) { element.preferredHeight = Height; element.minHeight = Height; }
        }

        float ArrangeLine(List<RectTransform> line, float width, float y)
        {
            float sum = line.Sum(Natural), gaps = FamilySpacing * (line.Count - 1);
            float scale = Mathf.Min(1f, Mathf.Max(0f, width - gaps) / Mathf.Max(1f, sum));
            float x = Mathf.Max(0f, (width - sum * scale - gaps) * .5f), tallest = 0f;
            foreach (var family in line)
            {
                float familyWidth = Natural(family) * scale;
                float familyHeight = Stacked ? Stack(family, familyWidth) : Row(family, familyWidth, scale);
                Set(family, new Vector2(x, -y), new Vector2(familyWidth, familyHeight));
                tallest = Mathf.Max(tallest, familyHeight);
                x += familyWidth + FamilySpacing;
            }
            return tallest;
        }

        float Row(RectTransform family, float familyWidth, float scale)
        {
            float at = 0f;
            foreach (Transform child in family)
            {
                var item = (RectTransform)child;
                if (child.name == Caption) { Set(item, Vector2.zero, new Vector2(familyWidth, CaptionHeight)); continue; }
                float itemWidth = Choice(item) * scale;
                Set(item, new Vector2(at, -CaptionHeight), new Vector2(itemWidth, ButtonHeight));
                at += itemWidth + Spacing * scale;
            }
            return CaptionHeight + ButtonHeight;
        }

        float Stack(RectTransform family, float familyWidth)
        {
            float at = CaptionHeight;
            foreach (Transform child in family)
            {
                var item = (RectTransform)child;
                if (child.name == Caption) { Set(item, Vector2.zero, new Vector2(familyWidth, CaptionHeight)); continue; }
                float itemHeight = ItemHeight(item);
                Set(item, new Vector2(0, -at), new Vector2(familyWidth, itemHeight));
                at += itemHeight + StackSpacing;
            }
            return Mathf.Max(CaptionHeight, at - StackSpacing);
        }

        float ItemHeight(Transform item)
        {
            int rows = item.GetComponent<Selectable>() ? 1 : NoteRows;
            return rows * ButtonHeight + (rows - 1) * StackSpacing;
        }

        float NaturalHeight(Transform family)
        {
            if (!Stacked) return CaptionHeight + ButtonHeight;
            float at = CaptionHeight;
            foreach (Transform child in family)
                if (child.name != Caption) at += ItemHeight(child) + StackSpacing;
            return Mathf.Max(CaptionHeight, at - StackSpacing);
        }

        public static int[] Lines(IReadOnlyList<float> widths, float spacing, float width, float minimumScale, IReadOnlyList<float> heights = null)
        {
            int count = widths.Count;
            if (count == 0) return new int[0];
            int[] best = null;
            float widestBest = float.MaxValue, blockBest = float.MaxValue;
            for (int mask = 0; mask < 1 << (count - 1); mask++)
            {
                var sizes = new List<int>();
                float line = widths[0], widest = 0f, tallest = HeightAt(heights, 0), block = 0f;
                int members = 1;
                bool fits = true;
                for (int i = 1; i <= count && fits; i++)
                {
                    if (i < count && (mask & (1 << (i - 1))) == 0)
                    { line += widths[i]; tallest = Mathf.Max(tallest, HeightAt(heights, i)); members++; continue; }
                    float gaps = spacing * (members - 1);
                    fits = members == 1 || line * minimumScale + gaps <= width;
                    widest = Mathf.Max(widest, line + gaps);
                    block += tallest;
                    sizes.Add(members);
                    if (i < count) { line = widths[i]; tallest = HeightAt(heights, i); members = 1; }
                }
                if (!fits) continue;
                if (best == null || Better(sizes.Count, block, widest, best.Length, blockBest, widestBest))
                { best = sizes.ToArray(); widestBest = widest; blockBest = block; }
            }
            return best;
        }

        static float HeightAt(IReadOnlyList<float> heights, int index) => heights != null && index < heights.Count ? heights[index] : 0f;

        static bool Better(int lines, float block, float widest, int bestLines, float bestBlock, float bestWidest)
        {
            if (lines != bestLines) return lines < bestLines;
            if (!Mathf.Approximately(block, bestBlock)) return block < bestBlock;
            return widest < bestWidest;
        }

        float Natural(Transform family)
        {
            if (natural.TryGetValue(family, out float known)) return known;
            float choices = 0f, widest = 0f, caption = 0f; int count = 0;
            foreach (Transform child in family)
            {
                if (child.name == Caption) { caption = Preferred(child) + 2f; continue; }
                float choice = Choice((RectTransform)child);
                choices += choice; widest = Mathf.Max(widest, choice); count++;
            }
            float result = Mathf.Max(caption, Stacked ? widest : choices + Spacing * Mathf.Max(0, count - 1));
            natural[family] = result;
            return result;
        }

        float Choice(RectTransform item)
        {
            if (natural.TryGetValue(item, out float known)) return known;
            float result = item.GetComponent<Selectable>() ? Mathf.Max(ButtonWidth, Content(item) + ButtonSlack) : Stacked ? NoteWidth : Preferred(item) + 4f;
            natural[item] = result;
            return result;
        }

        static float Content(Transform item)
        {
            var row = item.GetComponentInChildren<HorizontalLayoutGroup>(true);
            if (!row) return Preferred(item) + ContentPadding;
            float width = row.padding.horizontal;
            int parts = 0;
            foreach (Transform child in row.transform)
            {
                if (!child.gameObject.activeSelf || child.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
                width += LayoutUtility.GetPreferredWidth((RectTransform)child);
                parts++;
            }
            return width + row.spacing * Mathf.Max(0, parts - 1);
        }

        static float Preferred(Transform item)
        {
            var text = item.GetComponentInChildren<TMP_Text>(true);
            return text ? text.GetPreferredValues(text.text).x : 0f;
        }

        internal static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            var corner = new Vector2(0, 1);
            if (rect.anchorMin != corner || rect.anchorMax != corner || rect.pivot != corner)
            { rect.anchorMin = rect.anchorMax = rect.pivot = corner; }
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }
    }

    public sealed class LobbyPlayGoalSwap : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public TMP_Text Goal;
        public string Original = "", Reason = "";
        bool selected;

        public bool Showing => Goal && Goal.text == Reason;

        public void OnSelect(BaseEventData data) { selected = true; Swap(true); }
        public void OnDeselect(BaseEventData data) { selected = false; Swap(false); }
        public void OnPointerEnter(PointerEventData data) => Swap(true);
        public void OnPointerExit(PointerEventData data) { if (!selected) Swap(false); }

        void OnDisable() { selected = false; Swap(false); }

        void Swap(bool on)
        {
            if (!Goal || string.IsNullOrEmpty(Reason)) return;
            if (!on && Goal.text != Reason) return;
            Goal.text = on ? Reason : Original;
            Goal.color = on ? LobbyKit.Hot : LobbyKit.Muted;
            if (Goal.TryGetComponent(out LobbyPlayGoalInfo info) && info.Icon && info.Icon.gameObject.activeSelf == on)
                info.Icon.gameObject.SetActive(!on);
        }
    }
}
