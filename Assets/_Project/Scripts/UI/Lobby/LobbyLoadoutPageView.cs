using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyLoadoutPageView : MonoBehaviour
    {
        public RectTransform Panel;
        public RectTransform LockerBody;
        public RectTransform RecipesBody;
        public RectTransform RecipeDetail;
        public ScrollRect RecipeScroll;
        public GridLayoutGroup RecipeGrid;
        public Button Close;
        public Image CloseGlyph;
        public Color CloseIdleInk = LobbyKit.Navy;
        public Color CloseActiveInk = LobbyKit.Cyan;
        public LobbyShopActionView LockerTab;
        public LobbyShopActionView RecipesTab;
        public LobbyShopActionView Finish;
        UnityAction closeListener;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var reference in new Component[] { Panel, LockerBody, RecipesBody, RecipeDetail, RecipeScroll,
                RecipeGrid, Close, CloseGlyph, LockerTab, RecipesTab, Finish })
                LobbyShopLayout.Require(this, reference, "Loadout binding");
            if (LockerBody == RecipesBody || LockerBody.IsChildOf(RecipesBody) || RecipesBody.IsChildOf(LockerBody) ||
                !RecipeDetail.IsChildOf(RecipesBody) || !RecipeGrid.transform.IsChildOf(RecipesBody) ||
                !RecipeScroll.transform.IsChildOf(RecipesBody) || RecipeGrid.transform.IsChildOf(RecipeDetail) ||
                RecipeScroll.transform.IsChildOf(RecipeDetail) || RecipeDetail.IsChildOf(RecipeScroll.transform) ||
                Close.transform.IsChildOf(RecipeDetail) || CloseGlyph.transform.IsChildOf(RecipeDetail) ||
                LockerTab.transform.IsChildOf(RecipeDetail) || RecipesTab.transform.IsChildOf(RecipeDetail) ||
                !Finish.transform.IsChildOf(RecipesBody) || Finish.transform.IsChildOf(RecipeDetail) ||
                !RecipeScroll.content || !RecipeScroll.content.IsChildOf(RecipeScroll.transform) || !RecipeGrid.transform.IsChildOf(RecipeScroll.content) ||
                !Close.GetComponent<LobbyPress>() || Close.onClick.GetPersistentEventCount() != 0 ||
                LockerTab == RecipesTab || LockerTab == Finish || RecipesTab == Finish)
                throw new InvalidOperationException("Loadout needs separate Locker/Recipes bodies, independent finish control and a valid recipe scroll.");
            if (!LobbyShopLayout.Finite(RecipeGrid.cellSize) || RecipeGrid.cellSize.x < 80 || RecipeGrid.cellSize.y < 80 ||
                RecipeGrid.cellSize.x > 800 || RecipeGrid.cellSize.y > 800)
                throw new InvalidOperationException("Loadout recipe grid dimensions must be between 80 and 800 UI units.");
            LockerTab.Validate(); RecipesTab.Validate(); Finish.Validate();
        }

        public void Bind(bool recipes, Action close, Action<bool> choose, Action finish)
        {
            Validate();
            if (closeListener != null) Close.onClick.RemoveListener(closeListener);
            closeListener = () => close();
            Close.onClick.AddListener(closeListener);
            Close.GetComponent<LobbyPress>().Hot -= SetCloseHot;
            Close.GetComponent<LobbyPress>().Hot += SetCloseHot;
            SetCloseHot(false);
            LockerTab.Bind("LOCKER", LockerTab.Label.text, () => choose(false), !recipes);
            RecipesTab.Bind("RECIPES", RecipesTab.Label.text, () => choose(true), recipes);
            Finish.Bind("Recipe finish", Finish.Label.text, finish, false);
            LockerBody.gameObject.SetActive(!recipes);
            RecipesBody.gameObject.SetActive(recipes);
        }

        void SetCloseHot(bool hot) => CloseGlyph.color = hot ? CloseActiveInk : CloseIdleInk;

        void OnDestroy()
        {
            if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener);
            if (Close && Close.GetComponent<LobbyPress>()) Close.GetComponent<LobbyPress>().Hot -= SetCloseHot;
        }
    }
}
