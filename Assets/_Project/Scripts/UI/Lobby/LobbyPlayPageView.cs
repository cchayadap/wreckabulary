using System;
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
                var fit = Panel.GetComponent<LobbyCompactPanel>() ?? Panel.gameObject.AddComponent<LobbyCompactPanel>();
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
            ((RectTransform)Close.transform).Pin(Vector2.one, new Vector2(-16, -16), new Vector2(40, 40));
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
        public float MinimumWidth = 264, CardHeight = 136;
        public float PreviewAspectRatio;
        public int MaximumColumns = 5;
        GridLayoutGroup grid;
        bool fitting;
        Vector2 viewportSize;

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
            int columns = Mathf.Clamp(Mathf.FloorToInt((rect.rect.width + grid.spacing.x) / (MinimumWidth + grid.spacing.x)),
                1, Mathf.Max(1, MaximumColumns));
            float width = (rect.rect.width - (columns - 1) * grid.spacing.x) / columns;
            float cardHeight = CardHeight;
            var scroll = PreviewAspectRatio > 0 ? GetComponentInParent<ScrollRect>() : null;
            var nextViewportSize = scroll && scroll.viewport ? scroll.viewport.rect.size : Vector2.zero;
            bool changed = nextViewportSize != viewportSize;
            viewportSize = nextViewportSize;
            if (PreviewAspectRatio > 0)
            {
                cardHeight = Mathf.Max(cardHeight, (width - 36) / PreviewAspectRatio + 106);
                if (scroll && scroll.viewport && scroll.viewport.rect.height > 0)
                    cardHeight = Mathf.Min(cardHeight, Mathf.Max(140, scroll.viewport.rect.height - 28));
            }
            var cell = new Vector2(width, cardHeight);
            int rows = Mathf.CeilToInt(transform.childCount / (float)columns);
            float height = rows * cardHeight + (rows - 1) * grid.spacing.y;
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

    public sealed class LobbyPlayPreviewFit : MonoBehaviour
    {
        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();
        void Fit()
        {
            var area = transform.parent as RectTransform;
            if (!area || area.rect.width <= 0 || area.rect.height <= 0) return;
            float scale = Mathf.Min(area.rect.width / 500, area.rect.height / 281.25f);
            var size = Vector3.one * scale;
            if (transform.localScale != size) transform.localScale = size;
        }
    }

    public sealed class LobbyPlayModeDock : MonoBehaviour
    {
        public RectTransform Choices;
        public float MaximumWidth = 1160;
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
                Place(row, width, height);
                var grid = GetComponent<LobbyPlayCardGrid>();
                if (grid) grid.Fit();
                height = Mathf.Max(44, layout ? layout.preferredHeight : row.rect.height);
                Place(row, width, height);
                var choicesTop = new Vector2(Choices.offsetMax.x, -height - 12);
                if (Choices.offsetMax != choicesTop) Choices.offsetMax = choicesTop;
            }
            finally { fitting = false; }
        }

        static void Place(RectTransform row, float width, float height)
        {
            var anchor = new Vector2(.5f, 1);
            var minimum = new Vector2(-width * .5f, -height); var maximum = new Vector2(width * .5f, 0);
            if (row.anchorMin != anchor || row.anchorMax != anchor || row.offsetMin != minimum || row.offsetMax != maximum)
                row.Place(anchor, anchor, minimum, maximum);
        }
    }
}
