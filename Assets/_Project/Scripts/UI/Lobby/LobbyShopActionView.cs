using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyShopActionView : MonoBehaviour
    {
        public Button Button;
        public TextMeshProUGUI Label;
        public Image Icon;
        public Image Underline;
        public LobbyPress Press;
        public Color IdleInk = LobbyKit.Navy;
        public Color ActiveInk = LobbyKit.Cyan;
        UnityAction listener;
        bool selected;

        public void Validate()
        {
            LobbyShopLayout.Require(this, Button, nameof(Button));
            LobbyShopLayout.Require(this, Label, nameof(Label));
            LobbyShopLayout.Require(this, Icon, nameof(Icon));
            LobbyShopLayout.Require(this, Underline, nameof(Underline));
            LobbyShopLayout.Require(this, Press, nameof(Press));
            LobbyShopLayout.Require(this, Press.Body, "motion container");
            LobbyShopLayout.Require(this, Press.Ring, "focus ring");
            if (Button.gameObject != gameObject || Press.gameObject != gameObject || Button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Shop actions use their own button and runtime controller callbacks only.");
        }

        public void Bind(string objectName, string text, Action action, bool isSelected, Sprite icon = null)
        {
            Validate();
            Unbind();
            name = objectName;
            Label.text = text;
            selected = isSelected;
            Icon.sprite = icon;
            Icon.gameObject.SetActive(icon);
            Button.interactable = action != null;
            if (action != null) { listener = () => action(); Button.onClick.AddListener(listener); }
            Press.Hot += SetHot;
            SetHot(false);
        }

        void SetHot(bool hot)
        {
            bool active = selected || hot;
            Underline.enabled = active;
            Label.color = active ? ActiveInk : IdleInk;
            Icon.color = Label.color;
        }

        void Unbind()
        {
            if (Button && listener != null) Button.onClick.RemoveListener(listener);
            listener = null;
            if (Press) Press.Hot -= SetHot;
        }

        void OnDestroy() => Unbind();
    }
}
