using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class WinterCollectionTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey,
            LobbyThemes.PreferenceKey, GameSoundPacks.PreferenceKey };
        string[] saved;
        string previewTheme;
        GameConfig previousConfig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            previewTheme = LobbyThemes.Current.Id;
            previousConfig = GameConfig.Current;
            yield return TestScenes.Reset();
            Assert.IsNotNull(SeasonalCollection.Winter, "Build Winter Collection before running native collection tests.");
            GameConfig.Use(GameConfig.Load());
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            var career = new Career();
            career.Owned.Add("skin:Candy");
            career.Owned.Add("skin:Arcade");
            PlayerPrefs.SetString(MatchTally.CareerKey, career.Serialize());
            LobbyThemes.Restore();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            GameConfig.Use(previousConfig);
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            LobbyThemes.Restore();
            LobbyThemes.Preview(previewTheme);
            PlayerPrefs.Save();
        }

        [Test]
        public void NativeLoadAddsOnlyTheTwoApprovedFinishesWithoutChangingSharedJsonParsing()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            string json = data.items.text;
            var baseline = data.Parse();
            var loaded = GameConfig.Load();
            SeasonalCollection.Winter.Apply(loaded.Items);
            SeasonalCollection.Winter.Apply(loaded.Items);
            var approved = new[] { "SHIELD", "SODA" };
            CollectionAssert.AreEquivalent(approved, loaded.Items.All.Where(item => item.HasSkin("Winter")).Select(item => item.Id));
            foreach (var item in baseline.Items.All)
            {
                Assert.IsFalse(item.HasSkin("Winter"), item.Id + " remains portable base data");
                var skins = loaded.Items.Get(item.Id).Skins;
                CollectionAssert.AreEqual(item.Skins, skins.Where(skin => skin != "Winter"), item.Id);
                Assert.AreEqual(approved.Contains(item.Id) ? 1 : 0, skins.Count(skin => skin == "Winter"), item.Id);
            }
            Assert.IsEmpty(loaded.Validate());
            Assert.AreEqual(json, data.items.text);
            Assert.IsFalse(data.Parse().Items.All.Any(item => item.HasSkin("Winter")), "A new base parse does not inherit the runtime overlay.");
        }

        [TestCase("SHIELD")]
        [TestCase("SODA")]
        public void EveryImportedModelMaterialFamilyHasAWinterFinishAndReturnsToClassic(string word)
        {
            var library = MaterialLibrary.Load();
            var model = ModelLibrary.Load().Find(GameConfig.Current.Items.Get(word).Model);
            Assert.IsNotNull(model, word);
            var source = model.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
            Assert.IsNotEmpty(source, word + " has actual imported material slots");
            foreach (var original in source)
            {
                Assert.IsNotNull(original, word + " has no empty material slot");
                string family = MaterialLibrary.FamilyOf(original.name);
                Assert.IsNotNull(family, word + ": " + original.name);
                var winter = library.ForSkin(original, "Winter");
                Assert.AreSame(SeasonalCollection.Winter.FindMaterial(family + "_Winter"), winter,
                    word + ": " + family + " must resolve the seasonal asset, not silently fall back to Classic");
                Assert.IsNotNull(winter, word + ": " + family);
                Assert.AreEqual(family + "_Winter", winter.name);
                Assert.AreSame(library.Find(family + "_Classic"), library.ForSkin(winter, "Classic"), family);
            }
            CollectionAssert.AreEqual(source, model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray(), "Lookup never changes the imported source asset.");
        }

        [UnityTest]
        public IEnumerator FreeWinterLookAndThemePersistWhilePreviewLeavesTheSoundPackAlone()
        {
            yield return OpenShop();
            var menu = LobbyMenu.Instance;
            var outfit = menu.Outfit.Clone();
            outfit.ItemSkins["BAT"] = "Candy";
            menu.Wear(outfit);
            GameSoundPacks.Select("default");
            Click("Wear look:winter");
            Assert.IsEmpty(GameConfig.Current.Wardrobe.Problems(menu.Outfit));
            Assert.AreEqual("Hoodie", menu.Outfit.PieceIn("Top"));
            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Assert.AreEqual("Candy", menu.Outfit.SkinFor("BAT"));
            Click("Wear theme:winter");
            Assert.AreEqual(0, menu.Career.Coins);
            Assert.AreEqual("winter", PlayerPrefs.GetString(LobbyThemes.PreferenceKey));
            Assert.AreEqual("default", GameSoundPacks.Selected, "Equipping the visual collection does not opt into its sound pack.");
            GameSoundPacks.Select("winter");
            Click("Offer theme:candy");
            Assert.AreEqual("candy", LobbyThemes.Current.Id);
            Assert.AreEqual("winter", GameSoundPacks.Selected);
            Assert.AreEqual("winter", PlayerPrefs.GetString(LobbyThemes.PreferenceKey));
            string equipped = menu.Outfit.Serialize();
            menu.Open(LobbyMenu.Home);
            Assert.AreEqual("winter", LobbyThemes.Current.Id);
            yield return TestScenes.Load(Session.HubScene);
            Assert.AreEqual(equipped, LobbyMenu.Instance.Outfit.Serialize());
            Assert.AreEqual("winter", LobbyThemes.Current.Id);
            Assert.AreEqual("winter", GameSoundPacks.Selected);
            Assert.IsTrue(LobbyMenu.Instance.Career.Owns("look", "winter"));
            Assert.AreEqual(0, LobbyMenu.Instance.Career.Coins);
        }

        [UnityTest]
        public IEnumerator ShopRestrictsWinterToApprovedRecipesAndRetainsOtherEquippedFinishes()
        {
            yield return OpenShop();
            var menu = LobbyMenu.Instance;
            var shop = menu.Page<ShopPage>();
            var outfit = menu.Outfit.Clone();
            outfit.ItemSkins["BAT"] = "Candy";
            outfit.ItemSkins["SODA"] = "Arcade";
            menu.Wear(outfit);
            Assert.IsTrue(shop.SelectRecipe("SHIELD"));
            yield return null;
            Click("Wear skin:Winter");
            Assert.AreEqual("Winter", menu.Outfit.SkinFor("SHIELD"));
            Assert.AreEqual("Arcade", menu.Outfit.SkinFor("SODA"));
            Assert.AreEqual("Candy", menu.Outfit.SkinFor("BAT"));
            Assert.IsTrue(shop.SelectRecipe("BAT"));
            yield return null;
            Assert.IsFalse(shop.Root.GetComponentsInChildren<Button>().Any(button => button.name == "Offer skin:Winter"));
            string before = menu.Outfit.Serialize();
            shop.Wear(new ShopOffer("skin", "Winter", "Winter", 0));
            Assert.AreEqual(before, menu.Outfit.Serialize(), "A direct UI command cannot equip an unsupported finish.");
            Assert.AreEqual(before, PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            var all = LobbyPage.WithFinish(menu.Outfit, "Winter");
            Assert.AreEqual("Candy", all.SkinFor("BAT"), "Bulk selection also retains unsupported recipes.");
            Assert.AreEqual("Winter", all.SkinFor("SHIELD"));
            Assert.AreEqual("Winter", all.SkinFor("SODA"));
            Assert.IsTrue(shop.SelectRecipe("SODA"));
            yield return null;
            Click("Wear skin:Winter");
            var reloaded = Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual("Winter", reloaded.SkinFor("SHIELD"));
            Assert.AreEqual("Winter", reloaded.SkinFor("SODA"));
            Assert.AreEqual("Candy", reloaded.SkinFor("BAT"));
            Assert.AreEqual(0, menu.Career.Coins);
        }

        static IEnumerator OpenShop()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "collection lobby ready");
            LobbyMenu.Instance.Open(LobbyMenu.Shop);
            yield return null;
        }

        static void Click(string name)
        {
            var button = LobbyMenu.Instance.Page<ShopPage>().Root.GetComponentsInChildren<Button>()
                .SingleOrDefault(candidate => candidate.name == name);
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, name);
            button.onClick.Invoke();
        }
    }
}
