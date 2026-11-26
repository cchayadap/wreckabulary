using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class BagRowBookTests
    {
        const string Bag = "{\"rows\": 1}";

        static string Entry(string id, string family = "Helper", bool consumable = true, int hands = 1, string bag = Bag, string extra = "") =>
            "{\"id\": \"" + id + "\", \"category\": \"Utilities\", \"tier\": \"Expanded\", \"enabled\": true, \"consumable\": " + (consumable ? "true" : "false") +
            ", \"model\": \"Items/" + id + "\", \"heldScale\": 0.8, \"grip\": [0, 0.1, 0], \"size\": [0.4, 0.36, 0.22], \"family\": \"" + family +
            "\", \"hands\": " + hands + ", \"durability\": " + (consumable ? 0 : 20) + (bag != null ? ", \"bag\": " + bag : "") + extra + "}";

        static string HouseFile(params string[] entries) => "{\"items\": [" + string.Join(", ", entries) + "]}";

        static List<string> Problems(string json)
        {
            var items = ItemCatalogue.FromJson(json, "test.json");
            return BagRowBook.FromJson(json, "test.json").Validate(items, HelperBook.FromJson(json, "test.json"));
        }

        [Test]
        public void TheBagBlockIsReadAndATestItemValidates()
        {
            var book = BagRowBook.FromJson(HouseFile(Entry("QABAG")), "test.json");
            Assert.AreEqual(1, book.Count);
            Assert.IsTrue(book.TryGet("QABAG", out var spec));
            Assert.AreEqual(1, spec.Rows);
            Assert.IsTrue(book.Holds("QABAG"));
            Assert.IsFalse(book.Holds("BAT"));
            Assert.IsFalse(book.Holds((string)null));
            CollectionAssert.IsEmpty(Problems(HouseFile(Entry("QABAG"))));
        }

        [Test]
        public void TheSharedCatalogueSeesAStatLessConsumableAndNoHelperProblem()
        {
            string json = HouseFile(Entry("QABAG"));
            var items = ItemCatalogue.FromJson(json, "test.json");
            var item = items.Get("QABAG");
            Assert.AreEqual(HandlingFamily.Helper, item.Family);
            Assert.IsTrue(item.Consumable);
            Assert.IsNull(item.Use);
            Assert.IsNull(item.Deploy);
            CollectionAssert.IsEmpty(items.Validate(10));
            var helpers = HelperBook.FromJson(json, "test.json");
            Assert.AreEqual(0, helpers.Count);
            CollectionAssert.IsEmpty(helpers.Validate(items, BagRowBook.FromJson(json, "test.json")), "a bag item needs no helper block");
            Assert.IsTrue(helpers.Validate(items).Any(p => p.Contains("without a helper block")), "without the bag book it would be a broken helper");
        }

        [Test]
        public void ABadBlockThrowsAtLoad()
        {
            Assert.Throws<FormatException>(() => BagRowBook.FromJson(HouseFile(Entry("QABAG", bag: "{\"rows\": 1, \"size\": 2}")), "test.json"));
            Assert.Throws<FormatException>(() => BagRowBook.FromJson(HouseFile(Entry("QABAG", bag: "{}")), "test.json"));
            Assert.Throws<FormatException>(() => BagRowBook.FromJson(HouseFile(Entry("QABAG", bag: "{\"rows\": 1.5}")), "test.json"));
            Assert.Throws<FormatException>(() => BagRowBook.FromJson(HouseFile(Entry("QABAG", bag: "3")), "test.json"));
        }

        [Test]
        public void AWrongEntryIsRefusedByValidation()
        {
            StringAssert.Contains("1 to 1 rows", string.Join("\n", Problems(HouseFile(Entry("QABAG", bag: "{\"rows\": 2}")))));
            StringAssert.Contains("Helper family", string.Join("\n", Problems(HouseFile(Entry("QABAG", family: "Thrown",
                extra: ", \"thrown\": {\"damage\": 5, \"speed\": 10}")))));
            StringAssert.Contains("consumable", string.Join("\n", Problems(HouseFile(Entry("QABAG", consumable: false)))));
            StringAssert.Contains("one hand", string.Join("\n", Problems(HouseFile(Entry("QABAG", hands: 2)))));
            StringAssert.Contains("only through its bag block", string.Join("\n", Problems(HouseFile(Entry("QABAG",
                extra: ", \"use\": {\"effect\": \"Heal\", \"amount\": 10}")))));
            StringAssert.Contains("10-slot bag", string.Join("\n", Problems(HouseFile(Entry("QABAGQABAGQ")))));
            string both = HouseFile(Entry("QABAG", extra: ", \"helper\": {\"body\": \"Stays\", \"pick\": \"Nearest\", \"bites\": \"Enemies\", \"attack\": \"None\", \"radius\": 2, \"seconds\": 15, \"health\": 20}"));
            StringAssert.Contains("can't also be a helper", string.Join("\n", Problems(both)));
            var lonely = new BagRowBook();
            lonely.Add(new BagRowSpec { Id = "QABAG", Rows = 1 });
            StringAssert.Contains("names no enabled recipe", string.Join("\n", lonely.Validate(new ItemCatalogue())));
        }

        [Test]
        public void ABookCanAddAndRemoveTestSpecs()
        {
            var book = new BagRowBook();
            book.Add(new BagRowSpec { Id = "QABAG" });
            Assert.Throws<ArgumentException>(() => book.Add(new BagRowSpec { Id = "QABAG" }));
            Assert.IsTrue(book.Remove("QABAG"));
            Assert.IsFalse(book.Remove("QABAG"));
            Assert.AreEqual(0, book.All.Count);
        }

        [Test]
        public void EveryHouseModeOffersItButNotTheHubOrACourse()
        {
            foreach (var mode in HouseModes.All) Assert.IsTrue(BagRowBook.Offered(mode.Id), mode.Id);
            foreach (string mode in new[] { "Hub", "Default", "TimeRun", "Freestyle", null, "" })
                Assert.IsFalse(BagRowBook.Offered(mode), mode ?? "null");
        }

        [Test]
        public void TheCardLineSaysWhatItDoesAndThatItTakesNoHand()
        {
            var book = new BagRowBook();
            book.Add(new BagRowSpec { Id = "QABAG" });
            var item = new ItemDefinition { Id = "QABAG", Family = HandlingFamily.Helper };
            Assert.AreEqual("Adds a row of letter slots for this round. Takes no hand.", book.CardLine(item));
            Assert.IsNull(book.CardLine(new ItemDefinition { Id = "BAT", Family = HandlingFamily.MeleeSwing }));
            Assert.IsNull(book.CardLine(null));
        }

        [Test]
        public void TheRealHouseFileValidatesAndTheSharedFileHasNoBagBlock()
        {
            string house = TestData.Read("items_house.json");
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(house, "items_house.json"));
            var bags = BagRowBook.FromJson(house, "items_house.json");
            CollectionAssert.IsEmpty(bags.Validate(items, HelperBook.FromJson(house, "items_house.json")));
            CollectionAssert.IsEmpty(HelperBook.FromJson(house, "items_house.json").Validate(items, bags));
            Assert.AreEqual(0, BagRowBook.FromJson(TestData.Read("items.json"), "items.json").Count, "items.json stays free of house-only blocks");
            foreach (var spec in bags.All) Assert.AreEqual(HandlingFamily.Helper, items.Get(spec.Id).Family, spec.Id);
        }

        [Test]
        public void BagRulesStayOutOfOnlineCode()
        {
            string scripts = Path.Combine(TestData.Root, "Assets", "_Project", "Scripts");
            foreach (string tree in new[] { "Online", Path.Combine("Rules", "Wordbound"), "WordboundRuntime" })
            {
                string folder = Path.Combine(scripts, tree);
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    Assert.IsFalse(text.Contains("BagRow") || text.Contains("BonusRows") || text.Contains("ClearAllBonusRows"), file);
                }
            }
        }
    }
}
