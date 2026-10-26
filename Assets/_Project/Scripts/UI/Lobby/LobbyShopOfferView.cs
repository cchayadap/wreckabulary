using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyShopOfferView : MonoBehaviour
    {
        public Button Preview;
        public RectTransform Face;
        public RectTransform Art;
        public TextMeshProUGUI Title;
        public Image Outline;
        public LobbyHint Hint;
        public LobbyShopActionView Action;
        public Sprite Coin;
        public Sprite Check;
        public Color PreviewColour = LobbyKit.Sun;
        public Color EquippedColour = LobbyKit.Cyan;
        UnityAction previewListener;
        bool worn;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            LobbyShopLayout.Require(this, Preview, nameof(Preview));
            LobbyShopLayout.Require(this, Face, nameof(Face));
            LobbyShopLayout.Require(this, Art, nameof(Art));
            LobbyShopLayout.Require(this, Title, nameof(Title));
            LobbyShopLayout.Require(this, Outline, nameof(Outline));
            LobbyShopLayout.Require(this, Hint, nameof(Hint));
            LobbyShopLayout.Require(this, Action, nameof(Action));
            Action.Validate();
            if (!Coin || !Check || Preview == Action.Button || Preview.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Shop cards need separate preview/action buttons, saved icons and runtime callbacks only.");
        }

        public void Bind(string id, string title, string description, bool owned, bool equipped, int price,
            bool spotlight, Action preview, Action action)
        {
            Validate();
            if (previewListener != null) Preview.onClick.RemoveListener(previewListener);
            previewListener = () => preview();
            Preview.onClick.AddListener(previewListener);
            name = "Offer " + id;
            Title.text = title;
            Hint.Text = (description ?? "").Replace("\n", " · ");
            worn = equipped;
            Action.Bind((worn ? "Wearing " : owned ? "Wear " : "Buy ") + id,
                worn ? "EQUIPPED" : owned ? "EQUIP" : price == 0 ? "FREE" : price.ToString(),
                worn ? null : action, worn, worn ? Check : owned ? null : Coin);
            SetPreview(spotlight);
        }

        public void SetPreview(bool selected)
        {
            Outline.enabled = worn || selected;
            Outline.color = selected ? PreviewColour : EquippedColour;
        }

        void OnDestroy()
        {
            if (Preview && previewListener != null) Preview.onClick.RemoveListener(previewListener);
        }
    }
}
