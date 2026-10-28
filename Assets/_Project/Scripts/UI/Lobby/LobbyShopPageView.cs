using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyShopPageView : MonoBehaviour
    {
        [Serializable]
        public sealed class CategoryBinding
        {
            public string Id;
            public LobbyShopActionView Action;
        }
        public RectTransform Panel;
        public Button Close;
        public Image CloseGlyph;
        public Color CloseIdleInk = LobbyKit.Navy;
        public Color CloseActiveInk = LobbyKit.Cyan;
        public ScrollRect Scroll;
        public RectTransform Content;
        public GridLayoutGroup Offers;
        public TextMeshProUGUI Footnote;
        [Range(96f, 180f)] public float SelectionHeight = 96f;
        [Range(140f, 240f)] public float SelectionActionWidth = 200f;
        public CategoryBinding[] Categories = Array.Empty<CategoryBinding>();
        UnityAction closeListener;
        static readonly string[] Required = { ShopPage.Looks, ShopPage.Gear, ShopPage.Colours, ShopPage.Rooms, ShopPage.Extras };

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            LobbyShopLayout.Require(this, Panel, nameof(Panel));
            LobbyShopLayout.Require(this, Close, nameof(Close));
            LobbyShopLayout.Require(this, CloseGlyph, nameof(CloseGlyph));
            LobbyShopLayout.Require(this, Scroll, nameof(Scroll));
            LobbyShopLayout.Require(this, Content, nameof(Content));
            LobbyShopLayout.Require(this, Offers, nameof(Offers));
            LobbyShopLayout.Require(this, Footnote, nameof(Footnote));
            if (Scroll.content != Content || !Offers.transform.IsChildOf(Content) ||
                !Close.GetComponent<LobbyPress>() || Close.onClick.GetPersistentEventCount() != 0 || Categories == null || Categories.Length != Required.Length ||
                !Required.All(id => Categories.Count(binding => binding != null && binding.Id == id) == 1) ||
                Categories.Select(binding => binding.Action).Distinct().Count() != Required.Length)
                throw new InvalidOperationException("Shop page needs one bound action per category and a valid offers scroll hierarchy.");
            if (!LobbyShopLayout.Finite(Offers.cellSize) || Offers.cellSize.x < 80 || Offers.cellSize.y < 80 ||
                Offers.cellSize.x > 800 || Offers.cellSize.y > 800)
                throw new InvalidOperationException("Shop card grid dimensions must be between 80 and 800 UI units.");
            if (float.IsNaN(SelectionHeight) || float.IsInfinity(SelectionHeight) || SelectionHeight < 96 || SelectionHeight > 180 ||
                float.IsNaN(SelectionActionWidth) || float.IsInfinity(SelectionActionWidth) || SelectionActionWidth < 140 || SelectionActionWidth > 240)
                throw new InvalidOperationException("Shop selection height must be 96–180 and action width 140–240 UI units.");
            foreach (var binding in Categories)
            { LobbyShopLayout.Require(this, binding.Action, binding.Id); binding.Action.Validate(); }
        }

        public Selectable Bind(string category, Action close, Action<string> choose, string note)
        {
            Validate();
            if (closeListener != null) Close.onClick.RemoveListener(closeListener);
            closeListener = () => close();
            Close.onClick.AddListener(closeListener);
            Close.GetComponent<LobbyPress>().Hot -= SetCloseHot;
            Close.GetComponent<LobbyPress>().Hot += SetCloseHot;
            SetCloseHot(false);
            Footnote.text = note ?? "Select to preview";
            foreach (var binding in Categories)
            {
                string id = binding.Id;
                binding.Action.Bind("Shop category " + id, binding.Action.Label.text, () => choose(id), id == category);
            }
            return Categories.Single(binding => binding.Id == category).Action.Button;
        }

        void OnDestroy()
        {
            if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener);
            if (Close && Close.GetComponent<LobbyPress>()) Close.GetComponent<LobbyPress>().Hot -= SetCloseHot;
        }

        void SetCloseHot(bool hot) => CloseGlyph.color = hot ? CloseActiveInk : CloseIdleInk;
    }
}
