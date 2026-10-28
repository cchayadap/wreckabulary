using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        UnityAction closeListener;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var role in new Component[] { Panel, Body, Queues, Choices, Summary, Title, Subtitle, Close })
                LobbyShopLayout.Require(this, role, "Play frame role");
            if (Title == Subtitle) throw new InvalidOperationException("Play title and subtitle must be separate text roles.");
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
            Panel.Fill();
            Title.alignment = TextAlignmentOptions.MidlineLeft;
            Title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(ContentInset.x, -HeaderHeight + 16), new Vector2(ContentInset.x + 160, -ContentInset.y));
            Subtitle.gameObject.SetActive(false);
            Queues.Place(Vector2.one, Vector2.one, new Vector2(-664, -HeaderHeight + 16), new Vector2(-64, -ContentInset.y));
            ((RectTransform)Close.transform).Pin(Vector2.one, new Vector2(-16, -16), new Vector2(40, 40));
            Body.Place(Vector2.zero, Vector2.one, ContentInset, new Vector2(-ContentInset.x, -HeaderHeight));
            Choices.Place(Vector2.zero, Vector2.one, new Vector2(0, SummaryHeight + 12), Vector2.zero);
            Summary.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, SummaryHeight));
        }

        void OnDestroy() { if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener); }
    }


    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LobbyPlayCardGrid : MonoBehaviour
    {
        public float MinimumWidth = 264, CardHeight = 136;
        public int MaximumColumns = 5;
        GridLayoutGroup grid;

        void OnEnable() { grid = GetComponent<GridLayoutGroup>(); Fit(); }
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        void Fit()
        {
            if (!grid) return;
            var rect = (RectTransform)transform;
            if (rect.rect.width <= 0 || transform.childCount == 0) return;
            int columns = Mathf.Clamp(Mathf.FloorToInt((rect.rect.width + grid.spacing.x) / (MinimumWidth + grid.spacing.x)),
                1, Mathf.Max(1, MaximumColumns));
            var cell = new Vector2((rect.rect.width - (columns - 1) * grid.spacing.x) / columns, CardHeight);
            int rows = Mathf.CeilToInt(transform.childCount / (float)columns);
            float height = rows * CardHeight + (rows - 1) * grid.spacing.y;
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            if (grid.constraintCount != columns) grid.constraintCount = columns;
            if (grid.cellSize != cell) grid.cellSize = cell;
            var size = GetComponent<LayoutElement>();
            if (!size || !Mathf.Approximately(size.preferredHeight, height)) rect.Size(-1, height);
        }
    }
}
