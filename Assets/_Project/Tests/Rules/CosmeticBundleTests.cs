using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class CosmeticBundleTests
    {
        [Test]
        public void EveryCollectedLookUsesValidNativePiecesAndRetainsOtherRecipeFinishes()
        {
            var wardrobe = TestData.Wardrobe();
            var original = wardrobe.Default.Clone();
            original.ItemSkins["FAN"] = "Arcade";
            string before = original.Serialize();
            foreach (var bundle in CosmeticBundles.All)
            {
                var next = bundle.Apply(original, wardrobe);
                Assert.IsNotNull(next, bundle.Id);
                Assert.IsTrue(wardrobe.Fits(next), bundle.Id);
                Assert.AreEqual("Arcade", next.SkinFor("FAN"), "an unrelated recipe keeps its choice");
                Assert.AreEqual(before, original.Serialize(), "a preview never mutates the equipped outfit");
                foreach (var skin in next.ItemSkins)
                    Assert.IsTrue(TestData.Catalogue().Get(skin.Key).HasSkin(skin.Value));
            }
        }

        [Test]
        public void ExistingPiecesAndFreeCollectionsRemainOwnedWithoutPurchasing()
        {
            var career = new Career();
            foreach (var piece in TestData.Wardrobe().Pieces) Assert.IsTrue(career.Owns("piece", piece.Id), piece.Id);
            foreach (var bundle in CosmeticBundles.All.Where(b => b.Price == 0)) Assert.IsTrue(career.Owns("look", bundle.Id), bundle.Id);
            Assert.IsTrue(career.Owns("theme", "sunroom"));
            Assert.AreEqual(0, career.Coins);
            Assert.IsEmpty(career.Owned, "free native pieces do not need a migration purchase");
            Assert.IsTrue(Career.Shop.All(o => o.Price > 0), "only paid offers enter checkout");
        }

        [Test]
        public void ABoughtBundleChargesOnlyMissingContentAndSurvivesReload()
        {
            var career = new Career { Coins = 1000 };
            Assert.IsNull(career.Buy("colour:bubblegum"));
            Assert.AreEqual(250, career.PriceOf(Career.Shop.First(o => o.Id == "look:candy")));
            Assert.IsNull(career.Buy("look:candy"));
            Assert.AreEqual(600, career.Coins);
            var loaded = Career.Deserialize(career.Serialize());
            Assert.IsTrue(loaded.Owns("look", "candy"));
            Assert.IsTrue(loaded.Owns("colour", "bubblegum"));
            Assert.IsTrue(loaded.Owns("skin", "Candy"));
            Assert.AreEqual("You already own it.", loaded.Buy("look:candy"));
            Assert.AreEqual(600, loaded.Coins);
            Assert.AreEqual(loaded.Owned.Count, loaded.Owned.Distinct().Count());
        }

        [Test]
        public void InsufficientFundsNeverPartiallyBuyABundle()
        {
            var career = new Career { Coins = 399 };
            string before = career.Serialize();
            Assert.IsNotNull(career.Buy("look:candy"));
            Assert.AreEqual(before, career.Serialize());
            Assert.IsFalse(career.Owns("skin", "Candy"));
            Assert.IsFalse(career.Owns("colour", "bubblegum"));
        }

        [Test]
        public void IndividualUnlocksAlsoOwnTheirCompleteLook()
        {
            var career = new Career { Coins = 700 };
            Assert.IsNull(career.Buy("skin:Arcade"));
            Assert.IsNull(career.Buy("colour:grape"));
            Assert.IsTrue(career.Owns("look", "arcade"));
            Assert.AreEqual(0, career.PriceOf(Career.Shop.First(o => o.Id == "look:arcade")));
            Assert.AreEqual("You already own it.", career.Buy("look:arcade"));
            Assert.AreEqual(150, career.Coins);
        }

        [Test]
        public void ThemesAreRealPersistentPurchases()
        {
            var career = new Career { Coins = 650 };
            Assert.IsNull(career.Buy("theme:candy"));
            Assert.IsNull(career.Buy("theme:lantern"));
            var loaded = Career.Deserialize(career.Serialize());
            Assert.AreEqual(0, loaded.Coins);
            Assert.IsTrue(loaded.Owns("theme", "candy"));
            Assert.IsTrue(loaded.Owns("theme", "lantern"));
            Assert.IsFalse(loaded.Owns("theme", "invented"));
        }

        [Test]
        public void ARecipeFinishRequiresOwnershipAndOnlyChangesTheSelectedRecipe()
        {
            var items = TestData.Catalogue();
            var current = TestData.Wardrobe().Default.Clone();
            current.ItemSkins["BAT"] = "Classic";
            var career = new Career { Coins = 400 };
            Assert.IsNull(CosmeticBundles.WithItemSkin(current, "SHIELD", "Arcade", career, items));
            Assert.IsNull(career.Buy("skin:Arcade"));
            Assert.IsNull(CosmeticBundles.WithItemSkin(current, "UNKNOWN", "Arcade", career, items));
            Assert.IsNull(CosmeticBundles.WithItemSkin(current, "SHIELD", "Unknown", career, items));
            var next = CosmeticBundles.WithItemSkin(current, "SHIELD", "Arcade", career, items);
            Assert.IsNotNull(next);
            Assert.AreEqual("Classic", next.SkinFor("BAT"));
            Assert.AreEqual("Arcade", next.SkinFor("SHIELD"));
            Assert.IsNull(current.SkinFor("SHIELD"));
            Assert.AreEqual(next.Serialize(), Outfit.Deserialize(next.Serialize()).Serialize());
        }
    }
}
