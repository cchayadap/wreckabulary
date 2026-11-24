using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class NewHouseItemsTests
    {
        static readonly HouseTuning Tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
        static readonly ItemCatalogue Items = Tuned();

        static ItemCatalogue House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static ItemCatalogue Tuned()
        {
            var items = House();
            HouseTuning.FromJson(TestData.Read("items_house.json")).Apply(items, TestData.LoadRuleBook());
            return items;
        }

        [TestCase("MUG"), TestCase("VASE"), TestCase("BOOK"), TestCase("BOX"), TestCase("BOW"), TestCase("CHEST")]
        public void TheModelledItemsAreNowCraftableInHouseModes(string id)
        {
            var item = Items.Get(id);
            Assert.IsTrue(item.Enabled, id);
            Assert.IsEmpty(Items.Validate(10).Where(p => p.StartsWith(id + ":")), id);
        }

        [Test]
        public void TheSharedCatalogueStillKeepsThemSwitchedOff()
        {
            var shared = TestData.Catalogue();
            foreach (string id in new[] { "MUG", "VASE", "BOOK", "BOX", "BOW", "CHEST" })
                Assert.IsFalse(shared.Get(id).Enabled, id + " stays off in items.json so online play is unchanged");
        }

        [Test]
        public void TheBowGrowsStrongerAsItDraws()
        {
            var bow = Items.Get("BOW").Ranged;
            Assert.IsNotNull(bow);
            Assert.AreEqual(0f, bow.Power(0f));
            Assert.AreEqual(1f, bow.Power(bow.DrawSeconds));
            Assert.AreEqual(1f, bow.Power(bow.DrawSeconds * 3f), "power stops at a full draw");
            Assert.AreEqual(bow.Damage, bow.DamageAt(0f));
            Assert.AreEqual(bow.FullDamage, bow.DamageAt(1f));
            Assert.Greater(bow.SpeedAt(1f), bow.SpeedAt(.5f));
            Assert.LessOrEqual(bow.FullDamage, RecipeDifficulty.MaxThrownDamage(RecipeDifficulty.TierOf("BOW")), "a full draw stays inside its tier's shot cap");
        }

        [Test]
        public void ARangedItemNeedsItsStatsAndAStashNeedsTheUtilityFamily()
        {
            var bow = Items.Get("BOW");
            var stats = bow.Ranged;
            bow.Ranged = null;
            try { Assert.That(Items.Validate(10), Has.Some.EqualTo("BOW: is ranged without ranged stats")); }
            finally { bow.Ranged = stats; }

            var chest = Items.Get("CHEST");
            chest.Family = HandlingFamily.DeployCover;
            try { Assert.That(Items.Validate(10), Has.Some.EqualTo("CHEST: a stash must use the Utility family")); }
            finally { chest.Family = HandlingFamily.Utility; }
        }

        [Test]
        public void TheNewItemsPlayTheRolesTheirCardsDescribe()
        {
            Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Items.Get("BOW")));
            Assert.AreEqual(SpellRole.Weapon, SpellChoice.RoleOf(Items.Get("BOOK")));
            Assert.AreEqual(SpellRole.Shot, SpellChoice.RoleOf(Items.Get("VASE")));
            Assert.AreEqual(SpellRole.Heal, SpellChoice.RoleOf(Items.Get("MUG")));
            Assert.AreEqual(SpellRole.Gadget, SpellChoice.RoleOf(Items.Get("CHEST")));
            Assert.IsTrue(BotTactics.Shoots(Items.Get("BOW")));
            Assert.IsFalse(BotTactics.Shoots(Items.Get("ARROW")), "a thrown arrow is not a bow");
        }

        [Test]
        public void CardsShowTraitsFirstAndOtherwiseWhatTheItemDoes()
        {
            CollectionAssert.AreEqual(new[] { "Heavy", "Daze 3rd" }, RecipeFacts.Chips(Items.Get("BOOK"), Tuning).Select(c => c.Label));
            CollectionAssert.AreEqual(new[] { "Heavy", "Crush 3rd" }, RecipeFacts.Chips(Items.Get("HAMMER"), Tuning).Select(c => c.Label));
            CollectionAssert.AreEqual(new[] { "Daze 40%" }, RecipeFacts.Chips(Items.Get("VASE"), Tuning).Select(c => c.Label));
            CollectionAssert.AreEqual(new[] { "+22 HP" }, RecipeFacts.Chips(Items.Get("MUG"), Tuning).Select(c => c.Label));
            CollectionAssert.AreEqual(new[] { "Team stash" }, RecipeFacts.Chips(Items.Get("CHEST"), Tuning).Select(c => c.Label));
            CollectionAssert.AreEqual(new[] { "Hold to draw" }, RecipeFacts.Chips(Items.Get("BOW"), Tuning).Select(c => c.Label));
            Assert.IsEmpty(RecipeFacts.Chips(Items.Get("BAT"), Tuning), "a plain word carries nothing extra");
            Assert.AreEqual(FactKind.Daze, RecipeFacts.Chips(Items.Get("VASE"), Tuning)[0].Kind);
        }

        [Test]
        public void EveryCraftableCardFitsTwoChips()
        {
            foreach (var item in Items.Enabled)
                Assert.LessOrEqual(RecipeFacts.Chips(item, Tuning).Count, RecipeFacts.MaxChips, item.Id);
        }

        [Test]
        public void StatBarsCompareAgainstTheStrongestCraftableItem()
        {
            var bars = RecipeFacts.Bars(Items.Get("HAMMER"), Items.All, Tuning);
            CollectionAssert.AreEqual(new[] { "DMG", "REACH" }, bars.Select(b => b.Label));
            Assert.AreEqual(1f, bars[0].Fill, 1e-4f, "HAMMER has the highest melee damage a second");
            var spear = RecipeFacts.Bars(Items.Get("SPEAR"), Items.All, Tuning);
            Assert.Greater(spear[1].Fill, bars[1].Fill, "SPEAR reaches further than HAMMER");
            CollectionAssert.AreEqual(new[] { "DMG", "SPEED" }, RecipeFacts.Bars(Items.Get("BOW"), Items.All, Tuning).Select(b => b.Label));
            Assert.IsEmpty(RecipeFacts.Bars(Items.Get("CHEST"), Items.All, Tuning));
            foreach (var item in Items.Enabled)
                foreach (var bar in RecipeFacts.Bars(item, Items.All, Tuning))
                    Assert.That(bar.Fill, Is.InRange(0f, 1f), item.Id);
        }

        [Test]
        public void TheNewItemsKeepTheHouseBalanceRules()
        {
            var rules = TestData.LoadRuleBook();
            var items = House();
            Tuning.Apply(items, rules);
            Assert.IsEmpty(Tuning.Validate(items, rules.Defaults));
            Assert.IsEmpty(items.Validate(rules.Defaults.MaxLetters));
        }
    }
}
