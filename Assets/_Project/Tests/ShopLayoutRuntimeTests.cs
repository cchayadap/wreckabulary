using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class ShopLayoutRuntimeTests
    {
        string[] saved;
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            yield return TestScenes.Reset();
            Assert.IsNotNull(Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath), "Run the explicit saved Shop setup before this integration fixture.");
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
            PlayerPrefs.Save();
            LobbyThemes.Restore();
        }

        [UnityTest]
        public IEnumerator RealShopRefreshUsesSavedViewsAndKeepsLiveCategoryFocus()
        {
            var layout = Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Shop);
            var shop = menu.Page<ShopPage>();
            string outfit = menu.Outfit.Serialize();
            int coins = menu.Career.Coins;
            foreach (string category in new[] { ShopPage.Looks, ShopPage.Gear, ShopPage.Colours, ShopPage.Rooms, ShopPage.Extras, ShopPage.Looks })
            {
                Assert.IsTrue(shop.ShowCategory(category));
                yield return null;
                var pages = shop.Root.GetComponentsInChildren<LobbyShopPageView>();
                Assert.AreEqual(1, pages.Length, "Refresh leaves exactly one active authored page.");
                Assert.AreEqual(layout.Page.Panel.offsetMin, pages[0].Panel.offsetMin);
                Assert.AreEqual(layout.Page.Offers.cellSize, pages[0].Offers.cellSize);
                Assert.AreEqual("Shop category " + category, EventSystem.current.currentSelectedGameObject.name);
                foreach (var card in shop.Root.GetComponentsInChildren<LobbyShopOfferView>())
                {
                    Assert.AreEqual(layout.Offer.Art.offsetMin, card.Art.offsetMin);
                    var savedTitle = layout.Offer.Title;
                    Assert.AreSame(LobbyRuntimeTypography.ResolveFont(savedTitle.font), card.Title.font);
                    Assert.AreSame(LobbyRuntimeTypography.ResolveMaterial(savedTitle.font, savedTitle.fontSharedMaterial), card.Title.fontSharedMaterial);
                    Assert.AreNotSame(savedTitle.font, card.Title.font);
                    Assert.AreNotSame(savedTitle.fontSharedMaterial, card.Title.fontSharedMaterial);
                    Assert.AreEqual(savedTitle.fontSize, card.Title.fontSize);
                    Assert.AreEqual(savedTitle.color, card.Title.color);
                }
            }
            Assert.Greater(shop.Root.GetComponentsInChildren<LobbyShopOfferView>().Length, 0, "The Looks shelf must contain actual saved offer-card instances.");
            Assert.AreEqual(outfit, menu.Outfit.Serialize());
            Assert.AreEqual(coins, menu.Career.Coins);
            menu.Close();
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreEqual("Shop", EventSystem.current.currentSelectedGameObject.name);
        }
    }
}
