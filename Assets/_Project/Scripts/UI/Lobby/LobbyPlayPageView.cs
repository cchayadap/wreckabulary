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

        void OnDestroy() { if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener); }
    }
}
