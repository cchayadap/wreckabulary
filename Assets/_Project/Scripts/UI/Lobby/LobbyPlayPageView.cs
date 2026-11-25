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
        public int MaximumColumns = 3, Basis;
        public bool FillPage;
        GridLayoutGroup grid;
        bool fitting;
        Vector2 viewportSize;

        public static float HeightFor(float width) => Mathf.Max(0f, width - Inset * 2) / PictureAspect + CaptionHeight + Inset;

        public static float WidthFor(float height) => Mathf.Max(0f, height - CaptionHeight - Inset) * PictureAspect + Inset * 2;

        public static (int columns, Vector2 cell) Layout(float available, float spacing, float minimumWidth, int maximumColumns, float viewportHeight) =>
            Layout(available, spacing, minimumWidth, maximumColumns, viewportHeight, 0);

        public static (int columns, Vector2 cell) Layout(float available, float spacing, float minimumWidth, int maximumColumns, float viewportHeight, int count) =>
            Layout(available, spacing, minimumWidth, maximumColumns, viewportHeight, count, 0);

        public static (int columns, Vector2 cell) Layout(float available, float spacing, float minimumWidth, int maximumColumns, float viewportHeight, int count, int basis)
        {
            int fit = Mathf.Clamp(Mathf.FloorToInt((available + spacing) / (Mathf.Min(PreferredWidth, minimumWidth) + spacing)), 1, Mathf.Max(1, maximumColumns));
            int shape = basis > 0 ? basis : count;
            int columns = shape > 0 ? Balanced(shape, fit) : fit;
            float width = Mathf.Max(1f, Mathf.Min(PreferredWidth, (available - (columns - 1) * spacing) / columns));
            float height = HeightFor(width);
            if (viewportHeight > 0 && height > viewportHeight - ViewportMargin)
            {
                height = Mathf.Max(ShortestCard, viewportHeight - ViewportMargin);
                width = Mathf.Max(1f, Mathf.Min(width, WidthFor(height)));
            }
            if (count > 0) columns = Mathf.Min(columns, count);
            return (columns, new Vector2(width, height));
        }

        public static int Balanced(int count, int fit)
        {
            fit = Mathf.Max(1, fit);
            int rows = Mathf.CeilToInt(Mathf.Max(1, count) / (float)fit);
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, count) / (float)rows), 1, fit);
        }

        public static int Rows(int count, int columns) => Mathf.CeilToInt(Mathf.Max(0, count) / (float)Mathf.Max(1, columns));

        int Cards
        {
            get
            {
                int count = 0;
                foreach (Transform child in transform) if (child.gameObject.activeSelf) count++;
                return count;
            }
        }

        float Room(ScrollRect scroll)
        {
            var content = scroll.content;
            var mine = transform;
            while (mine.parent && mine.parent != content) mine = mine.parent;
            if (mine.parent != content) return 0f;
            content.TryGetComponent(out VerticalLayoutGroup column);
            float above = column ? column.padding.top : 0f;
            for (int i = 0; i < mine.GetSiblingIndex(); i++)
            {
                var child = (RectTransform)content.GetChild(i);
                if (!child.gameObject.activeSelf || child.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
                above += child.rect.height + (column ? column.spacing : 0f);
            }
            float below = column ? column.padding.bottom : 0f;
            return viewportSize.y - above - below;
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
            int count = Cards;
            var (columns, cell) = Layout(rect.rect.width, grid.spacing.x, MinimumWidth, MaximumColumns, viewportSize.y, count, Basis);
            if (FillPage && scroll && scroll.content)
            {
                var plan = Wreckabulary.Rules.PlaceGrid.Fit(count, rect.rect.width, Room(scroll), grid.spacing.x, Inset, CaptionHeight, MinimumWidth);
                if (plan.Fits) { columns = plan.Columns; cell = new Vector2(plan.Width, plan.Height); }
            }
            int rows = Mathf.Max(1, Rows(count, columns));
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

    public sealed class LobbyPlayModeBoard : MonoBehaviour
    {
        public LobbyPlayModeFamilies Sections;
        public TMP_Text Goal, Access;
        public float GoalHeight = 30, GoalGap = 6, AccessHeight = 44;
        float arrangedWidth = -1f, arrangedTiles = -1f;
        bool fitting;

        public bool Beside => false;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            float width = LobbyPlayColumnFlow.WidthIn(transform);
            if (fitting || !Sections || width <= 0) return;
            if (Mathf.Approximately(width, arrangedWidth) && Mathf.Approximately(Sections.Height, arrangedTiles)) return;
            fitting = true;
            try { Arrange(width); arrangedWidth = width; arrangedTiles = Sections.Height; }
            finally { fitting = false; }
        }

        void Arrange(float width)
        {
            var sections = (RectTransform)Sections.transform;
            LobbyPlayModeFamilies.Set(sections, Vector2.zero, new Vector2(width, Mathf.Max(1f, sections.sizeDelta.y)));
            Sections.Fit();
            float height = Sections.Height;
            LobbyPlayModeFamilies.Set(sections, Vector2.zero, new Vector2(width, height));
            if (Goal) height = PlaceLine(Goal, width, height, GoalHeight, GoalGap);
            if (Access) height = PlaceLine(Access, width, height, AccessHeight, 0f);
            if (!TryGetComponent(out LayoutElement element)) element = gameObject.AddComponent<LayoutElement>();
            if (Mathf.Approximately(element.preferredHeight, height)) return;
            element.preferredHeight = element.minHeight = height;
            if (transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        static float PlaceLine(TMP_Text line, float width, float height, float lineHeight, float gap)
        {
            if (line.alignment != TextAlignmentOptions.MidlineLeft) line.alignment = TextAlignmentOptions.MidlineLeft;
            LobbyPlayModeFamilies.Set(line.rectTransform, new Vector2(0, -height - gap), new Vector2(width, lineHeight));
            if (line.TryGetComponent(out LobbyPlayGoalInfo info)) info.Fit();
            return height + gap + lineHeight;
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

    public sealed class LobbyPlayTilePlan
    {
        public int Rows { get; }
        public float Width { get; }
        public float Height { get; }
        public float BandHeight { get; }
        public IReadOnlyList<int> Columns { get; }
        public IReadOnlyList<int> Line { get; }
        public IReadOnlyList<float> Left { get; }
        public IReadOnlyList<float> Top { get; }

        public LobbyPlayTilePlan(int rows, float width, float height, float band, int[] columns, int[] line, float[] left, float[] top)
        {
            Rows = rows; Width = width; Height = height; BandHeight = band; Columns = columns; Line = line; Left = left; Top = top;
        }

        public (int column, int row) Cell(int group, int entry) => (entry % Mathf.Max(1, Columns[group]), entry / Mathf.Max(1, Columns[group]));

        public Vector2 Position(int group, int entry)
        {
            var (column, row) = Cell(group, entry);
            return new Vector2(Left[group] + column * (Width + LobbyPlayModeFamilies.Gutter),
                -(Top[group] + LobbyPlayModeFamilies.Head + row * (Height + LobbyPlayModeFamilies.Gutter)));
        }

        public float GroupWidth(int group) => Columns[group] * Width + (Columns[group] - 1) * LobbyPlayModeFamilies.Gutter;

        public int RowsOf(int group, int count) => Mathf.CeilToInt(Mathf.Max(1, count) / (float)Mathf.Max(1, Columns[group]));

        public float GroupHeight(int group, int count) =>
            LobbyPlayModeFamilies.Head + RowsOf(group, count) * Height + Mathf.Max(0, RowsOf(group, count) - 1) * LobbyPlayModeFamilies.Gutter;
    }

    public sealed class LobbyPlayModeFamilies : MonoBehaviour
    {
        public const string Caption = "Family caption";
        public const float Gutter = 12f, GroupGap = 28f, CaptionHeight = 22f, CaptionGap = 4f, Head = CaptionHeight + CaptionGap;
        public const float MinimumTile = 120f, MaximumTile = 200f, TileHeight = 112f, LineGap = 16f;
        public float MinimumWidth = MinimumTile;
        float arrangedWidth = -1f, arrangedMinimum = -1f;
        int arrangedCount = -1;
        bool fitting;

        public float Height { get; private set; }

        public LobbyPlayTilePlan Plan { get; private set; }

        public static LobbyPlayTilePlan Layout(IReadOnlyList<int> counts, float width) => Layout(counts, width, MinimumTile);

        public static LobbyPlayTilePlan Layout(IReadOnlyList<int> counts, float width, float minimum)
        {
            minimum = Mathf.Max(MinimumTile, minimum);
            float widest = Mathf.Max(MaximumTile, minimum);
            int groups = counts.Count;
            if (groups == 0 || width <= 0) return new LobbyPlayTilePlan(0, 0f, 0f, 0f, new int[groups], new int[groups], new float[groups], new float[groups]);
            int most = Mathf.Max(1, counts.Max());
            for (int rows = 1; rows <= most; rows++)
            {
                var columns = counts.Select(count => Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, count) / (float)rows))).ToArray();
                float tile = TileWidth(width, columns.Sum(), groups);
                if (tile >= minimum) return Place(counts, columns, new int[groups], rows, Mathf.Floor(Mathf.Min(widest, tile)));
            }
            var single = counts.Select(_ => 1).ToArray();
            int perLine = Mathf.Clamp(Mathf.FloorToInt((width + GroupGap) / (minimum + GroupGap)), 1, groups);
            var lines = Enumerable.Range(0, groups).Select(group => group / perLine).ToArray();
            float narrow = Mathf.Floor(Mathf.Clamp((width - GroupGap * (perLine - 1)) / perLine, 1f, widest));
            return Place(counts, single, lines, most, narrow);
        }

        static float TileWidth(float width, int columns, int groups) =>
            (width - Gutter * (columns - groups) - GroupGap * (groups - 1)) / Mathf.Max(1, columns);

        static LobbyPlayTilePlan Place(IReadOnlyList<int> counts, int[] columns, int[] lines, int rows, float tile)
        {
            int groups = counts.Count;
            var left = new float[groups]; var top = new float[groups];
            float y = 0f, x = 0f, tallest = 0f;
            for (int group = 0; group < groups; group++)
            {
                if (group > 0 && lines[group] != lines[group - 1]) { y += tallest + LineGap; x = 0f; tallest = 0f; }
                left[group] = x; top[group] = y;
                int used = Mathf.CeilToInt(Mathf.Max(1, counts[group]) / (float)columns[group]);
                tallest = Mathf.Max(tallest, Head + used * TileHeight + (used - 1) * Gutter);
                x += columns[group] * tile + (columns[group] - 1) * Gutter + GroupGap;
            }
            return new LobbyPlayTilePlan(rows, tile, TileHeight, y + tallest, columns, lines, left, top);
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            float width = ((RectTransform)transform).rect.width;
            if (fitting || width <= 0) return;
            int count = Families().Sum(family => Tiles(family).Count);
            if (count == arrangedCount && Mathf.Approximately(width, arrangedWidth) && Mathf.Approximately(MinimumWidth, arrangedMinimum)) return;
            fitting = true;
            try
            {
                Arrange(width);
                arrangedWidth = width; arrangedCount = count; arrangedMinimum = MinimumWidth;
            }
            finally { fitting = false; }
        }

        List<RectTransform> Families() =>
            transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();

        static List<RectTransform> Tiles(Transform family) =>
            family.Cast<Transform>().Where(child => child.name != Caption && child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();

        void Arrange(float width)
        {
            var families = Families();
            var tiles = families.Select(Tiles).ToList();
            Plan = Layout(tiles.Select(list => list.Count).ToArray(), width, MinimumWidth);
            for (int group = 0; group < families.Count; group++)
            {
                var origin = new Vector2(Plan.Left[group], -Plan.Top[group]);
                float groupWidth = Plan.GroupWidth(group);
                Set(families[group], origin, new Vector2(groupWidth, Plan.GroupHeight(group, tiles[group].Count)));
                if (families[group].Find(Caption) is RectTransform caption) Set(caption, Vector2.zero, new Vector2(groupWidth, CaptionHeight));
                for (int entry = 0; entry < tiles[group].Count; entry++)
                    Set(tiles[group][entry], Plan.Position(group, entry) - origin, new Vector2(Plan.Width, Plan.Height));
            }
            Height = Plan.BandHeight;
            if (!TryGetComponent(out LayoutElement element)) element = gameObject.AddComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, Height)) { element.preferredHeight = Height; element.minHeight = Height; }
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
