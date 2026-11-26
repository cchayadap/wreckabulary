using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using static Wreckabulary.Rules.Tests.HelperTestSpecs;

namespace Wreckabulary.Rules.Tests
{
    public class HelperBookTests
    {
        const string RobotBlock = "{\"body\": \"Walks\", \"pick\": \"Aimed\", \"bites\": \"Enemies\", \"attack\": \"Swing\", \"damage\": 8, \"range\": 1.1, \"windup\": 0.45, \"cooldown\": 0.75, \"knockback\": 3, \"speed\": 3.0, \"sight\": 8, \"leash\": 10, \"radius\": 0, \"seconds\": 10, \"strikes\": 3, \"health\": 22, \"slow\": 0, \"slowSeconds\": 0}";

        static string Entry(string id, string family = "Helper", bool consumable = true, int hands = 1, string helper = RobotBlock, string extra = "") =>
            "{\"id\": \"" + id + "\", \"category\": \"Combat\", \"tier\": \"Expanded\", \"enabled\": true, \"consumable\": " + (consumable ? "true" : "false") +
            ", \"model\": \"Items/" + id + "\", \"heldScale\": 0.8, \"grip\": [0, 0.1, 0], \"size\": [0.32, 0.42, 0.28], \"family\": \"" + family +
            "\", \"hands\": " + hands + ", \"durability\": " + (consumable ? 0 : 20) + (helper != null ? ", \"helper\": " + helper : "") + extra + "}";

        static string HouseFile(params string[] entries) => "{\"items\": [" + string.Join(", ", entries) + "]}";

        static System.Collections.Generic.List<string> Problems(string json)
        {
            var items = ItemCatalogue.FromJson(json, "test.json");
            return HelperBook.FromJson(json, "test.json").Validate(items);
        }

        [Test]
        public void TheHelperBlockIsReadFieldByField()
        {
            var book = HelperBook.FromJson(HouseFile(Entry("ROBOT")), "test.json");
            Assert.AreEqual(1, book.Count);
            Assert.IsTrue(book.TryGet("ROBOT", out var spec));
            var want = Robot();
            Assert.AreEqual(want.Body, spec.Body);
            Assert.AreEqual(want.Pick, spec.Pick);
            Assert.AreEqual(want.Bites, spec.Bites);
            Assert.AreEqual(want.Attack, spec.Attack);
            Assert.AreEqual(want.Damage, spec.Damage, 1e-4f);
            Assert.AreEqual(want.Range, spec.Range, 1e-4f);
            Assert.AreEqual(want.Windup, spec.Windup, 1e-4f);
            Assert.AreEqual(want.Cooldown, spec.Cooldown, 1e-4f);
            Assert.AreEqual(want.Speed, spec.Speed, 1e-4f);
            Assert.AreEqual(want.Sight, spec.Sight, 1e-4f);
            Assert.AreEqual(want.Leash, spec.Leash, 1e-4f);
            Assert.AreEqual(want.Seconds, spec.Seconds, 1e-4f);
            Assert.AreEqual(want.Strikes, spec.Strikes);
            Assert.AreEqual(want.Health, spec.Health, 1e-4f);
            Assert.IsFalse(spec.Slows);
            CollectionAssert.IsEmpty(Problems(HouseFile(Entry("ROBOT"))));
        }

        [Test]
        public void TheSharedCatalogueReadsAHelperEntryAsAConsumableHelperWithNoStats()
        {
            var item = ItemCatalogue.FromJson(HouseFile(Entry("ROBOT")), "test.json").Get("ROBOT");
            Assert.AreEqual(HandlingFamily.Helper, item.Family);
            Assert.IsTrue(item.Consumable);
            Assert.IsNull(item.Melee);
            Assert.IsNull(item.Deploy);
            CollectionAssert.IsEmpty(ItemCatalogue.FromJson(HouseFile(Entry("ROBOT")), "test.json").Validate(10));
        }

        [Test]
        public void AHelperRecipeWithoutAHelperBlockIsRefused()
        {
            Assert.That(Problems(HouseFile(Entry("ROBOT", helper: null))), Has.Some.Contains("without a helper block"));
        }

        [Test]
        public void AHelperBlockWithoutAnEnabledRecipeIsRefused()
        {
            var book = HelperBook.FromJson(HouseFile(Entry("ROBOT")), "test.json");
            var other = ItemCatalogue.FromJson(HouseFile(Entry("RABBIT", helper: null).Replace("\"Helper\"", "\"Thrown\"").Replace("\"durability\": 0", "\"durability\": 0, \"thrown\": {\"damage\": 5, \"speed\": 10}")), "test.json");
            Assert.That(book.Validate(other), Has.Some.Contains("names no enabled recipe"));
        }

        [Test]
        public void AHelperBlockOnAnotherFamilyIsRefused()
        {
            string json = HouseFile(Entry("ROBOT", family: "Thrown", extra: ", \"thrown\": {\"damage\": 5, \"speed\": 10}"));
            Assert.That(Problems(json), Has.Some.Contains("needs the Helper family"));
        }

        [Test]
        public void AHelperMustBeConsumableAndOneHanded()
        {
            Assert.That(Problems(HouseFile(Entry("ROBOT", consumable: false))), Has.Some.Contains("must be consumable"));
            Assert.That(Problems(HouseFile(Entry("ROBOT", hands: 2))), Has.Some.Contains("one hand"));
        }

        [Test]
        public void AHelperActsOnlyThroughItsBlock()
        {
            Assert.That(Problems(HouseFile(Entry("ROBOT", extra: ", \"thrown\": {\"damage\": 5, \"speed\": 10}"))), Has.Some.Contains("only through its helper block"));
        }

        [Test]
        public void UnknownHelperFieldsAndValuesAreRefusedWhenTheFileIsRead()
        {
            Assert.Throws<FormatException>(() => HelperBook.FromJson(HouseFile(Entry("ROBOT", helper: RobotBlock.Replace("\"slow\"", "\"slowness\""))), "test.json"));
            Assert.Throws<FormatException>(() => HelperBook.FromJson(HouseFile(Entry("ROBOT", helper: RobotBlock.Replace("\"Walks\"", "\"Flies\""))), "test.json"));
        }

        [Test]
        public void CapsAreCheckedWhenTheBookIsValidated()
        {
            Assert.That(Problems(HouseFile(Entry("ROBOT", helper: RobotBlock.Replace("\"damage\": 8", "\"damage\": 13")))), Has.Some.Contains("strike damage"));
        }

        [Test]
        public void TheRealHouseFileValidatesAndStaysOutOfTheSharedFile()
        {
            string house = TestData.Read("items_house.json");
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(house, "items_house.json"));
            var book = HelperBook.FromJson(house);
            CollectionAssert.IsEmpty(book.Validate(items, BagRowBook.FromJson(house)), "bag items need no helper block");
            Assert.AreEqual(0, HelperBook.FromJson(TestData.Read("items.json"), "items.json").Count, "online items.json never carries a helper block");
            Assert.IsFalse(TestData.Read("items.json").Contains("\"Helper\""), "no shared recipe uses the Helper family");
        }

        [Test]
        public void ModesOnlyOfferTheHelpersThatHaveAFairTarget()
        {
            var anyone = Chomper();
            var chaser = Robot();
            var trap = Spikes();
            foreach (string mode in new[] { HouseModes.Dibs, HouseModes.FreeForAll, HouseModes.War, HouseModes.Tutorial })
            {
                Assert.IsTrue(HelperBook.AllowedIn(mode, chaser), mode);
                Assert.IsTrue(HelperBook.AllowedIn(mode, anyone), mode);
                Assert.IsTrue(HelperBook.AllowedIn(mode, trap), mode);
            }
            Assert.IsTrue(HelperBook.AllowedIn(HouseModes.Duos, chaser));
            Assert.IsFalse(HelperBook.AllowedIn(HouseModes.Duos, anyone), "a bite-anyone helper would hit your partner");
            foreach (string mode in new[] { HouseModes.MovingOut, HouseModes.MovingDay, HouseModes.Escape })
            {
                Assert.IsFalse(HelperBook.AllowedIn(mode, chaser), mode + ": nobody to chase");
                Assert.IsFalse(HelperBook.AllowedIn(mode, anyone), mode);
                Assert.IsTrue(HelperBook.AllowedIn(mode, trap), mode + ": traps still change the route");
                Assert.IsFalse(HelperBook.AllowedIn(mode, Cracker()), mode);
            }
            foreach (string mode in new[] { HouseModes.ShoppingList, HouseModes.RiddleRush, "Hub" })
                Assert.IsFalse(HelperBook.AllowedIn(mode, trap), mode + " is a word mode or the hub");
            Assert.IsTrue(HelperBook.AllowedIn(HouseModes.Escape, null), "walls are offered everywhere");
        }

        [Test]
        public void HelpersWorkOnlyInALiveRoundOrTheTutorial()
        {
            Assert.IsTrue(HelperBook.LiveNow(HouseModes.Dibs, true));
            Assert.IsFalse(HelperBook.LiveNow(HouseModes.Dibs, false), "warm-up, showcase, countdown and round over");
            Assert.IsTrue(HelperBook.LiveNow(HouseModes.Tutorial, false));
        }

        [Test]
        public void TheCardLineIsBuiltFromTheSpecSoItNeverLies()
        {
            Assert.AreEqual("Chases the one you aim at", Robot().CardLine());
            Assert.AreEqual("Chases the nearest foe", Rabbit().CardLine());
            Assert.AreEqual("Follows you and bites whoever hurts you", Terrier().CardLine());
            Assert.AreEqual("Guards this spot from foes", Blaster().CardLine());
            Assert.AreEqual("Chases anyone near. Even you!", Chomper().CardLine());
            Assert.AreEqual("Snaps at foes who step on it", Trap().CardLine());
            StringAssert.Contains("8 x 3", Robot().Details());
            StringAssert.Contains("slows to 60%", Trap().Details());
            var cone = new ItemDefinition { Id = "CONE", Enabled = true, Family = HandlingFamily.DeployCover, Durability = 30,
                Deploy = new DeployStats { Effect = DeployEffect.Cover, FootprintX = 1.4f, FootprintZ = .3f, LifetimeSeconds = 8f } };
            Assert.IsTrue(HelperBook.IsWall(cone));
            Assert.AreEqual("Blocks the way for 8 s", HelperBook.WallLine(cone));
            cone.Deploy.LifetimeSeconds = 0f;
            Assert.IsFalse(HelperBook.IsWall(cone), "a cover without a lifetime is today's permanent cover");
            var book = new HelperBook();
            book.Add(Robot());
            Assert.AreEqual("Chases the one you aim at", book.CardLine(new ItemDefinition { Id = "ROBOT", Family = HandlingFamily.Helper }));
            Assert.IsNull(book.CardLine(new ItemDefinition { Id = "BAT", Family = HandlingFamily.MeleeSwing }));
        }

        [Test]
        public void TheHelperFamilyIsAppendedLastSoNoSharedOrdinalMoves()
        {
            var names = Enum.GetNames(typeof(HandlingFamily));
            Assert.AreEqual("Helper", names[names.Length - 1]);
            Assert.AreEqual(13, (int)HandlingFamily.Helper);
            Assert.AreEqual(12, (int)HandlingFamily.Utility);
        }

        [Test]
        public void HelperRulesStayOutOfOnlineCode()
        {
            string scripts = Path.Combine(TestData.Root, "Assets", "_Project", "Scripts");
            foreach (string tree in new[] { "Online", Path.Combine("Rules", "Wordbound"), "WordboundRuntime" })
            {
                string folder = Path.Combine(scripts, tree);
                if (!Directory.Exists(folder)) continue;
                foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    string text = System.IO.File.ReadAllText(file);
                    Assert.IsFalse(text.Contains("HelperSpec") || text.Contains("HelperBook") || text.Contains("HandlingFamily.Helper"), file);
                }
            }
        }

        [Test]
        public void ABookCanAddAndRemoveTestSpecs()
        {
            var book = new HelperBook();
            book.Add(Robot());
            Assert.Throws<ArgumentException>(() => book.Add(Robot()));
            Assert.IsTrue(book.Remove("ROBOT"));
            Assert.IsFalse(book.TryGet("ROBOT", out _));
            Assert.AreEqual(0, book.All.Count());
        }
    }
}
