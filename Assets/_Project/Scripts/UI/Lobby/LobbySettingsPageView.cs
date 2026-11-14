using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbySettingsPageView : MonoBehaviour
    {
        public RectTransform Panel, Body, Tabs, Content, Confirmation;
        public TextMeshProUGUI Title;
        public Button Close;
        [Min(720)] public float MaximumWidth = 1280;
        UnityAction closeListener;

        public void FitHeight(float height)
        {
            if (float.IsNaN(height) || float.IsInfinity(height)) throw new ArgumentOutOfRangeException(nameof(height));
            if (!Panel.TryGetComponent(out LobbyCompactPanel fit)) fit = Panel.gameObject.AddComponent<LobbyCompactPanel>();
            fit.MaximumSize = new Vector2(MaximumWidth, Mathf.Clamp(height, 480, 1000));
            fit.Fit();
        }

        public void Validate()
        {
            if (float.IsNaN(MaximumWidth) || float.IsInfinity(MaximumWidth) || MaximumWidth < 720 || MaximumWidth > 1920)
                throw new InvalidOperationException("Settings maximum width must be between 720 and 1920 UI units.");
            LobbyShopLayout.ValidateTree(this);
            foreach (var role in new Component[] { Panel, Body, Tabs, Content, Confirmation, Title, Close })
                LobbyShopLayout.Require(this, role, "Settings frame role");
            var regions = new[] { Tabs, Content, Confirmation };
            for (int i = 0; i < regions.Length; i++)
                for (int j = i + 1; j < regions.Length; j++)
                    if (regions[i].IsChildOf(regions[j]) || regions[j].IsChildOf(regions[i]))
                        throw new InvalidOperationException("Settings dynamic regions must not contain one another.");
            if (!Title.transform.IsChildOf(Panel) || !Close.transform.IsChildOf(Panel) ||
                regions.Any(region => Title.transform.IsChildOf(region) || Close.transform.IsChildOf(region)))
                throw new InvalidOperationException("Settings title and Close must stay in the panel header, outside dynamic regions.");
            var reservation = Confirmation.GetComponent<LayoutElement>();
            if (new[] { Panel, Body, Tabs, Content, Confirmation }.Distinct().Count() != 5 ||
                !Body.IsChildOf(Panel) || !Tabs.IsChildOf(Body) || !Content.IsChildOf(Body) || !Confirmation.IsChildOf(Body) ||
                Content.parent != Confirmation.parent || !Content.parent.GetComponent<VerticalLayoutGroup>() ||
                !Tabs.GetComponent<VerticalLayoutGroup>() || !Content.GetComponent<LayoutElement>() ||
                Content.GetComponent<LayoutElement>().flexibleHeight <= 0 || Content.GetComponent<LayoutElement>().ignoreLayout || !reservation ||
                reservation.ignoreLayout || reservation.flexibleHeight > 0 || reservation.minHeight > reservation.preferredHeight ||
                float.IsNaN(reservation.preferredHeight) || float.IsInfinity(reservation.preferredHeight) ||
                reservation.preferredHeight < 64 || reservation.preferredHeight > 200 ||
                !Close.GetComponent<LobbyPress>() || Close.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Settings needs separate tab/content regions and a 64–200 unit confirmation reservation in their vertical layout.");
        }

        public void Bind(Action close, bool confirmation)
        {
            Validate();
            if (closeListener != null) Close.onClick.RemoveListener(closeListener);
            closeListener = () => close(); Close.onClick.AddListener(closeListener);
            Confirmation.gameObject.SetActive(confirmation);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)Content.parent);
        }

        void OnDestroy() { if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener); }
    }
}
