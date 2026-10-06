using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CatalogueTests
    {
        static readonly string[] Core12 = { "BAT", "BLADE", "LAMP", "BALL", "PLATE", "TABLE", "BED", "MAT", "SOFA", "SOAP", "FOAM", "BOMB" };

        ItemCatalogue catalogue;

        [SetUp]
        public void Load() => catalogue = TestData.Catalogue();

        [Test]
        public void ShippedCatalogueIsValid()
        {
            Assert.IsEmpty(catalogue.Validate(TestData.RulesFor("Dibs").MaxLetters));
        }

        [Test]
        public void ExactlyTheCore12AreEnabled()
        {
            CollectionAssert.AreEquivalent(Core12, catalogue.Enabled.Select(i => i.Id));
        }

        [Test]
        public void EveryModelledItemIsListedWithItsModel()
        {
            var modelled = catalogue.All.Where(i => i.Model != null).ToList();
            Assert.AreEqual(40, modelled.Count, "the Vault v2 pack has 40 item models");
            foreach (var item in modelled)
            {
                Assert.AreEqual("Items/" + item.Id, item.Model);
                Assert.IsTrue(item.HeldScale > 0f && item.HeldScale <= 1f, $"{item.Id} held scale {item.HeldScale}");
                CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Arcade" }, item.Skins, item.Id);
            }
        }

        [Test]
        public void ItemsWithoutAModelStayDisabled()
        {
            var unmodelled = catalogue.All.Where(i => i.Model == null).ToList();
            Assert.IsNotEmpty(unmodelled);
            foreach (var item in unmodelled)
            {
                Assert.IsFalse(item.Enabled, item.Id);
                Assert.AreEqual(ItemTier.Legacy, item.Tier, item.Id);
            }
        }

        [TestCase("BAT", "ABT")]
        [TestCase("BALL", "ABLL")]
        [TestCase("BOMB", "BBMO")]
        [TestCase("TABLE", "ABELT")]
        [TestCase("SOFA", "AFOS")]
        public void RecipeIsTheWordsLettersWithRepeats(string id, string letters)
        {
            Assert.AreEqual(letters, catalogue.Get(id).Letters.ToString());
        }

        [Test]
        public void NoRecipeNeedsMoreThanABagHolds()
        {
            int max = TestData.RulesFor("Dibs").MaxLetters;
            foreach (var item in catalogue.All) Assert.LessOrEqual(item.Letters.Count, max, item.Id);
        }

        [Test]
        public void ConsumablesSpendTheirLettersAndReusablesHaveDurability()
        {
            CollectionAssert.AreEquivalent(new[] { "SOAP", "FOAM", "BOMB" }, catalogue.Enabled.Where(i => i.Consumable).Select(i => i.Id));
            foreach (var item in catalogue.Enabled.Where(i => !i.Consumable)) Assert.Greater(item.Durability, 0, item.Id);
        }

        [Test]
        public void EachCoreItemHasItsBehaviourData()
        {
            Assert.IsNotNull(catalogue.Get("BAT").Melee);
            Assert.IsNotNull(catalogue.Get("BLADE").Melee);
            Assert.AreEqual(HandlingFamily.MeleeThrust, catalogue.Get("LAMP").Family);
            Assert.Greater(catalogue.Get("LAMP").Melee.Reach, catalogue.Get("BAT").Melee.Reach, "LAMP reaches further than BAT");
            Assert.Greater(catalogue.Get("BLADE").Melee.Damage, catalogue.Get("BAT").Melee.Damage, "BLADE hits harder than BAT");
            Assert.Greater(catalogue.Get("BAT").Melee.Knockback, catalogue.Get("BLADE").Melee.Knockback, "BAT knocks back more");
            Assert.IsTrue(catalogue.Get("BALL").Thrown.Recoverable);
            Assert.AreEqual(120f, catalogue.Get("PLATE").Shield.FrontArcDegrees);
            Assert.AreEqual(DeployEffect.Cover, catalogue.Get("TABLE").Deploy.Effect);
            Assert.AreEqual(DeployEffect.JumpPad, catalogue.Get("BED").Deploy.Effect);
            Assert.AreEqual(DeployEffect.SpeedStrip, catalogue.Get("MAT").Deploy.Effect);
            Assert.Greater(catalogue.Get("SOFA").Durability, catalogue.Get("TABLE").Durability, "SOFA is the heavier cover");
            Assert.AreEqual(DeployEffect.SlipZone, catalogue.Get("SOAP").Deploy.Effect);
            Assert.AreEqual(UseEffect.Bubble, catalogue.Get("FOAM").Use.Effect);
            Assert.Greater(catalogue.Get("BOMB").Thrown.FuseSeconds, 1f, "the BOMB is clearly telegraphed");
        }

        [Test]
        public void DeployFootprintMatchesTheModelsFloorSize()
        {
            foreach (var item in catalogue.All.Where(i => i.Deploy != null))
            {
                Assert.AreEqual(item.Size[0], item.Deploy.FootprintX, 1e-4, item.Id);
                Assert.AreEqual(item.Size[2], item.Deploy.FootprintZ, 1e-4, item.Id);
            }
        }

        [Test]
        public void CardsPutCraftableRecipesFirst()
        {
            var cards = catalogue.Cards(LetterBag.FromWord("BATL"));
            Assert.AreEqual(12, cards.Count);
            Assert.AreEqual("BAT", cards[0].Item.Id);
            Assert.IsTrue(cards[0].Craftable);
            Assert.AreEqual("BALL", cards[1].Item.Id, "BALL is one L short");
            Assert.AreEqual(1, cards[1].Missing.Count);
            Assert.AreEqual(1, cards[1].Have('L'), "shows one of the two Ls");
            Assert.IsFalse(cards.Skip(1).Any(c => c.Craftable));
        }

        [Test]
        public void ValidationCatchesBrokenData()
        {
            var bad = new ItemCatalogue();
            bad.Add(new ItemDefinition { Id = "BAT", Enabled = true, Family = HandlingFamily.MeleeSwing, Model = "Items/BAT", Durability = 5 });
            bad.Add(new ItemDefinition { Id = "PLATE", Enabled = true, Family = HandlingFamily.Shield, Durability = 5 });
            bad.Add(new ItemDefinition { Id = "SOAP", Enabled = false, Skins = { "Candy" } });
            bad.Get("SOAP").Skins.Remove("Classic");
            var problems = bad.Validate(10);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BAT:") && p.Contains("melee")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("PLATE:") && p.Contains("model")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("PLATE:") && p.Contains("shield")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("SOAP:") && p.Contains("Classic")), string.Join("\n", problems));
            Assert.IsTrue(new ItemCatalogue().Validate(10).Count == 0);
            var tiny = TestData.Catalogue().Validate(3);
            Assert.IsTrue(tiny.Any(p => p.StartsWith("TABLE:") && p.Contains("bag holds 3")));
        }

        [Test]
        public void DuplicateOrBadIdsAreRejected()
        {
            var c = new ItemCatalogue();
            c.Add(new ItemDefinition { Id = "BAT" });
            Assert.Throws<ArgumentException>(() => c.Add(new ItemDefinition { Id = "BAT" }));
            Assert.Throws<ArgumentException>(() => c.Add(new ItemDefinition { Id = "Bat" }));
            Assert.Throws<ArgumentException>(() => c.Add(new ItemDefinition { Id = "" }));
        }

        [Test]
        public void JsonErrorsNameTheFileAndPlace()
        {
            var e = Assert.Throws<FormatException>(() => ItemCatalogue.FromJson("{\"items\": [{\"id\": \"BAT\", \"category\": \"Weapons\", \"tier\": \"Core\"}]}", "test.json"));
            StringAssertContains(e.Message, "test.json");
            StringAssertContains(e.Message, "category");
            var e2 = Assert.Throws<FormatException>(() => ItemCatalogue.FromJson("{\"items\": [}", "broken.json"));
            StringAssertContains(e2.Message, "broken.json:1:");
        }

        static void StringAssertContains(string text, string part) =>
            Assert.IsTrue(text.Contains(part), $"expected \"{text}\" to contain \"{part}\"");
    }
}
