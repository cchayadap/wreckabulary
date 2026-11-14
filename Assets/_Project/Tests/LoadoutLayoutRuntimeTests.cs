using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class LoadoutLayoutRuntimeTests
    {
        string[] saved;
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            Assert.IsNotNull(Resources.Load<LobbyLoadoutLayout>(LobbyLoadoutLayout.ResourcePath), "Run the explicit saved Loadout setup before this integration fixture.");
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning, 2f, "lobby ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (saved != null)
                for (int i = 0; i < Keys.Length; i++)
                    if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save(); LobbyThemes.Restore();
        }

        [UnityTest]
        public IEnumerator RealLoadoutRefreshRetainsAuthoredGeometryTypographyAndTabFocus()
        {
            var layout = Resources.Load<LobbyLoadoutLayout>(LobbyLoadoutLayout.ResourcePath);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Loadout);
            var locker = menu.Page<LoadoutPage>();
            string outfit = menu.Outfit.Serialize(), savedOutfit = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            int coins = menu.Career.Coins;
            foreach (bool recipes in new[] { false, true, false, true })
            {
                locker.ShowRecipes(recipes);
                yield return null;
                var pages = locker.Root.GetComponentsInChildren<LobbyLoadoutPageView>();
                Assert.AreEqual(1, pages.Length);
                var page = pages[0];
                Assert.AreEqual(layout.Page.Panel.offsetMin, page.Panel.offsetMin);
                Assert.AreEqual(layout.Page.RecipeGrid.cellSize, page.RecipeGrid.cellSize);
                Assert.AreEqual(layout.Page.RecipeGrid.spacing, page.RecipeGrid.spacing);
                Assert.AreEqual(layout.Page.RecipeDetail.offsetMin, page.RecipeDetail.offsetMin);
                Assert.AreEqual(recipes ? "RECIPES" : "LOCKER", EventSystem.current.currentSelectedGameObject.name);
                var label = page.RecipesTab.Label; var source = layout.Page.RecipesTab.Label;
                Assert.AreSame(LobbyRuntimeTypography.ResolveFont(source.font), label.font);
                Assert.AreSame(LobbyRuntimeTypography.ResolveMaterial(source.font, source.fontSharedMaterial), label.fontSharedMaterial);
                Assert.AreNotSame(source.font, label.font);
                var tiles = locker.Root.GetComponentsInChildren<LobbyRecipeTileView>();
                Assert.AreEqual(recipes ? GameConfig.Current.Items.Enabled.Count() : 0, tiles.Length);
                foreach (var tile in tiles)
                {
                    Assert.AreEqual(layout.Recipe.Icon.rectTransform.anchoredPosition, tile.Icon.rectTransform.anchoredPosition);
                    Assert.AreEqual(layout.Recipe.Icon.rectTransform.sizeDelta, tile.Icon.rectTransform.sizeDelta);
                    Assert.AreEqual(layout.Recipe.MaximumLetterSize, tile.MaximumLetterSize);
                    Assert.AreEqual(layout.Recipe.LetterWidthBudget, tile.LetterWidthBudget);
                }
            }
            Assert.AreEqual(outfit, menu.Outfit.Serialize()); Assert.AreEqual(savedOutfit, PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual(coins, menu.Career.Coins);
        }

        [UnityTest]
        public IEnumerator RecipeHoverPinAndFinishKeepFocusAndOpenTheSelectedGearWithoutEquipping()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Loadout);
            var locker = menu.Page<LoadoutPage>();
            locker.ShowRecipes(true);
            yield return null;
            string outfit = menu.Outfit.Serialize(), savedOutfit = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            int coins = menu.Career.Coins;
            var items = GameConfig.Current.Items.Enabled.ToArray();
            Assert.Greater(items.Length, 1);
            var pinned = items.FirstOrDefault(item => item.Id == locker.Pinned) ?? items[0];
            var target = items.First(item => item.Id != pinned.Id);
            var tile = locker.Root.GetComponentsInChildren<LobbyRecipeTileView>().Single(view => view.name == "Recipe " + target.Id);
            var page = locker.Root.GetComponentInChildren<LobbyLoadoutPageView>();
            tile.Press.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.IsTrue(page.RecipeDetail.GetComponentsInChildren<RawImage>().Any(image => image.name == "Art " + target.Id));
            tile.Press.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return null;
            Assert.IsTrue(page.RecipeDetail.GetComponentsInChildren<RawImage>().Any(image => image.name == "Art " + pinned.Id));
            tile.Button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(target.Id, locker.Pinned);
            Assert.AreEqual("Recipe " + target.Id, EventSystem.current.currentSelectedGameObject.name);
            page = locker.Root.GetComponentInChildren<LobbyLoadoutPageView>();
            var finish = page.Finish.Button;
            EventSystem.current.SetSelectedGameObject(finish.gameObject);
            yield return null;
            Assert.IsTrue(finish && finish.gameObject.activeInHierarchy);
            Assert.IsFalse(finish.transform.IsChildOf(page.RecipeDetail));
            Assert.AreEqual(LoadoutPage.Blurb(target), page.RecipeDetail.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Blurb").text);
            finish.onClick.Invoke();
            yield return null;
            Assert.AreEqual(LobbyMenu.Shop, menu.Current);
            Assert.AreEqual(ShopPage.Gear, menu.Page<ShopPage>().Category);
            Assert.AreEqual(target.Id, menu.Page<ShopPage>().SelectedRecipe);
            Assert.AreEqual("Choose recipe", EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual(outfit, menu.Outfit.Serialize()); Assert.AreEqual(savedOutfit, PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual(coins, menu.Career.Coins);
        }
    }
}
