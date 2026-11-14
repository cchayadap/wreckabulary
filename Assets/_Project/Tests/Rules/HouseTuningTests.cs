using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HouseTuningTests
    {
        const string SharedRulesSha = "f183d9b79d1b438a78fb682455c6ce5e6b15cbd09d47b0831b3af47b2f697d4c";

        static string HouseJson => TestData.Read("items_house.json");

        static (ItemCatalogue items, RuleBook rules, HouseTuning tuning) House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(HouseJson, "items_house.json"));
            var rules = TestData.LoadRuleBook();
            var tuning = HouseTuning.FromJson(HouseJson);
            tuning.Apply(items, rules);
            return (items, rules, tuning);
        }

        [Test]
        public void TierComesFromHowHardTheWordIsToSpell()
        {
            Assert.AreEqual(5, RecipeDifficulty.Score("BAT"));
            Assert.AreEqual(13, RecipeDifficulty.Score("CABINET"), "letter values plus a point per letter past five");
            Assert.AreEqual(RecipeTier.Common, RecipeDifficulty.TierOf("BAT"));
            Assert.AreEqual(RecipeTier.Uncommon, RecipeDifficulty.TierOf("BLADE"));
            Assert.AreEqual(RecipeTier.Rare, RecipeDifficulty.TierOf("PILLOW"));
            Assert.AreEqual(RecipeTier.Epic, RecipeDifficulty.TierOf("HAMMER"));
            Assert.Greater(RecipeDifficulty.Score("BOX"), RecipeDifficulty.Score("BAT"), "a rare letter (X) makes a short word harder");
        }

        [Test]
        public void ShippedTuningFitsEveryTier()
        {
            var (items, rules, tuning) = House();
            CollectionAssert.IsEmpty(tuning.Validate(items, rules.Defaults));
            foreach (var item in items.Enabled.Where(i => RecipeDifficulty.TierOf(i.Id) == RecipeTier.Common))
                CollectionAssert.IsEmpty(tuning.TraitsOf(item.Id), item.Id + " is a simple word: an honest item without tricks");
            Assert.IsNotEmpty(tuning.TraitsOf("HAMMER"), "an Epic word earns its extras");
        }

        [Test]
        public void SimpleWordsHitHonestlyAndHardWordsHitWithExtras()
        {
            var (items, _, tuning) = House();
            float Dps(string id) => items.Get(id).Melee.Damage / items.Get(id).Melee.Cycle;
            Assert.LessOrEqual(Dps("BAT"), RecipeDifficulty.MaxMeleeDps(RecipeTier.Common));
            Assert.Less(items.Get("BAT").Melee.Damage, items.Get("BLADE").Melee.Damage, "BLADE still cuts deeper than BAT");
            Assert.IsNotNull(tuning.Trait("BLADE", TraitKind.Bleed), "a blade leaves a cut");
            Assert.IsNotNull(tuning.Trait("HAMMER", TraitKind.Heavy));
            Assert.IsNotNull(tuning.Trait("PILLOW", TraitKind.Daze), "a hard word, even a soft item, earns a trick");
        }

        [Test]
        public void FistsBarelyHurtPlayersButStillSmashFurniture()
        {
            var (_, rules, _) = House();
            var shared = TestData.LoadRuleBook();
            foreach (var mode in rules.Modes)
            {
                Assert.Less(rules.For(mode).Unarmed.Damage, shared.For(mode).Unarmed.Damage, mode + ": spell a weapon to really fight");
                Assert.GreaterOrEqual(rules.For(mode).Unarmed.Damage, HouseTuning.UnarmedFloor, mode);
                Assert.AreEqual(shared.For(mode).Unarmed.BreakPower, rules.For(mode).Unarmed.BreakPower, mode + ": letters still come from punching furniture");
            }
        }

        [Test]
        public void WordboundAndOnlineKeepTheSharedNumbers()
        {
            using var sha = SHA256.Create();
            var bytes = File.ReadAllBytes(Path.Combine(TestData.Root, "Assets/_Project/Data/Config/rules.json"));
            Assert.AreEqual(SharedRulesSha, string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2"))), "tune House fists in items_house.json, not rules.json");
            House();
            Assert.AreEqual(14f, TestData.Catalogue().Get("BAT").Melee.Damage, "a fresh parse of items.json (what Wordbound reads) is untouched");
            Assert.AreEqual(8f, TestData.LoadRuleBook().For("Dibs").Unarmed.Damage);
        }

        [Test]
        public void ValidationRefusesTricksAWordHasNotEarned()
        {
            var items = TestData.Catalogue();
            var rules = TestData.LoadRuleBook();
            var cheat = HouseTuning.FromJson(@"{ ""items"": [], ""tuning"": { ""unarmed"": { ""damage"": 0 }, ""items"": {
                ""BAT"": { ""traits"": [ { ""kind"": ""Bleed"", ""amount"": 2, ""seconds"": 3 } ] },
                ""HAMMER"": { ""traits"": [ { ""kind"": ""Heavy"", ""amount"": 3 } ] },
                ""KITE"": { ""traits"": [ { ""kind"": ""Swift"", ""amount"": 1.1 } ] } } } }");
            cheat.Apply(items, rules);
            var problems = cheat.Validate(items, rules.Defaults);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BAT is Common")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("HAMMER: heavy")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("'KITE'")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("Bare hands")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.StartsWith("BAT is Common: 24.1")), "untuned BAT is too strong for a Common word: " + string.Join("\n", problems));
        }

        [Test]
        public void ShippedItemsUseEveryKindOfDelivery()
        {
            var (items, _, tuning) = House();
            var used = items.Enabled.SelectMany(i => tuning.TraitsOf(i.Id)).Select(t => t.Delivery).Distinct().ToList();
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(TraitDelivery)), used);
            Assert.IsTrue(items.Enabled.Any(i => tuning.TraitsOf(i.Id).Count == 0), "plain items with no effect remain");
        }

        [Test]
        public void EachTraitSaysWhatItDoesInOneLine()
        {
            var (_, _, tuning) = House();
            Assert.AreEqual("", tuning.Describe("BAT"));
            StringAssert.StartsWith("every 3rd hit crushes", tuning.Trait("HAMMER", TraitKind.Crush).Describe());
            StringAssert.StartsWith("50% chance", tuning.Trait("SPEAR", TraitKind.Daze).Describe());
            StringAssert.StartsWith("while held", tuning.Trait("SHIELD", TraitKind.Guard).Describe());
            StringAssert.Contains("deepen", tuning.Trait("BLADE", TraitKind.Bleed).Describe());
            StringAssert.Contains(" · ", tuning.Describe("HAMMER"));
        }

        [Test]
        public void ValidationRefusesBrokenDeliveries()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(HouseJson, "items_house.json"));
            var rules = TestData.LoadRuleBook();
            var bad = HouseTuning.FromJson(@"{ ""items"": [], ""tuning"": { ""items"": {
                ""HAMMER"": { ""traits"": [ { ""kind"": ""Crush"", ""amount"": 2, ""every"": 2 } ] },
                ""RACK"": { ""traits"": [ { ""kind"": ""Crush"", ""amount"": 1.5, ""every"": 1 } ] },
                ""PLATE"": { ""traits"": [ { ""kind"": ""Swift"", ""amount"": 1.1, ""chance"": 0.5 } ] },
                ""SPEAR"": { ""traits"": [ { ""kind"": ""Daze"", ""amount"": 0.7, ""seconds"": 1, ""stacks"": 2 } ] },
                ""TOASTER"": { ""traits"": [ { ""kind"": ""Daze"", ""amount"": 0.7, ""seconds"": 1, ""chance"": 0 } ] },
                ""BLADE"": { ""traits"": [ { ""kind"": ""Bleed"", ""amount"": 3, ""seconds"": 3, ""stacks"": 3 } ] },
                ""BARREL"": { ""traits"": [ { ""kind"": ""Crush"", ""amount"": 1.5, ""every"": 2 } ] } } } }");
            bad.Apply(items, rules);
            var problems = bad.Validate(items, rules.Defaults);
            string all = string.Join("\n", problems);
            Assert.IsTrue(problems.Any(p => p.StartsWith("HAMMER is Epic: 33.")), "a crush counts against the ceiling:\n" + all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("RACK: crush")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("PLATE: a held trait is constant")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("SPEAR: only a bleed can stack")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("TOASTER: chance")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BLADE: a bleed deepens")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BARREL: crush is a melee build-up")), all);
        }

        [Test]
        public void TuningCannotInventStats()
        {
            var items = TestData.Catalogue();
            var bad = HouseTuning.FromJson(@"{ ""items"": [], ""tuning"": { ""items"": { ""BALL"": { ""melee"": { ""damage"": 5 } } } } }");
            Assert.Throws<ArgumentException>(() => bad.Apply(items, TestData.LoadRuleBook()));
        }

        static HouseLayout[] Houses() => Directory.GetFiles(Path.Combine(TestData.Root, "Assets/_Project/Data/Config"), "house_*.json")
            .Select(file => HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file))).ToArray();

        [Test]
        public void HouseRoundsRunLongerInTheBiggerHousesWhileWordboundKeepsItsTimers()
        {
            var (_, rules, _) = House();
            var shared = TestData.LoadRuleBook();
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut", "MovingDay", "Escape" })
                Assert.Greater(rules.For(mode).RoundTimeLimitSeconds, shared.For(mode).RoundTimeLimitSeconds, mode + " gives the crew time to cross the bigger houses");
            foreach (string mode in new[] { "MovingOut", "Escape" })
                Assert.Greater(rules.For(mode).ClearOutFirstAt, shared.For(mode).ClearOutFirstAt, mode + " lets the crew spread out before the Movers start");
            Assert.AreEqual(shared.For("Dibs").ClearOutDamagePerSecond, rules.For("Dibs").ClearOutDamagePerSecond, "only timings follow the size");
            Assert.AreEqual(150f, TestData.LoadRuleBook().For("Dibs").RoundTimeLimitSeconds, "a fresh parse of rules.json (what Wordbound reads) is untouched");
        }

        [Test]
        public void TheMoversPackEveryLastStandingRoomOneAtATimeBeforeTheRoundEnds()
        {
            var (_, rules, _) = House();
            foreach (var house in Houses())
                foreach (string mode in new[] { "Dibs", "Duos" })
                {
                    var r = rules.For(mode);
                    var schedule = new ClearOutSchedule(r, house.ClearOutOrders[mode]);
                    Assert.Less(schedule.Closures.Last().ClosedAt, r.RoundTimeLimitSeconds, $"{house.Name} {mode}: every listed room is packed in time");
                    Assert.GreaterOrEqual(r.ClearOutInterval, r.ClearOutWarnSeconds + r.ClearOutFillSeconds, mode + ": one room is packed before the next warning");
                }
        }

        [Test]
        public void TuningOnlyStretchesTimersOfKnownModes()
        {
            Assert.Throws<FormatException>(() => HouseTuning.FromJson(@"{ ""tuning"": { ""modes"": { ""Dibs"": { ""maxHealth"": 500 } } } }"));
            Assert.Throws<FormatException>(() => HouseTuning.FromJson(@"{ ""tuning"": { ""modes"": { ""Dibs"": { ""clearOut"": { ""damagePerSecond"": 50 } } } } }"));
            Assert.Throws<FormatException>(() => HouseTuning.FromJson(@"{ ""tuning"": { ""modes"": { ""Dibs"": { ""roundTimeLimit"": 0 } } } }"));
            var picnic = HouseTuning.FromJson(@"{ ""tuning"": { ""modes"": { ""Picnic"": { ""roundTimeLimit"": 60 } } } }");
            Assert.Throws<ArgumentException>(() => picnic.Apply(TestData.Catalogue(), TestData.LoadRuleBook()));
            var rules = TestData.LoadRuleBook();
            HouseTuning.FromJson(@"{ ""tuning"": { ""modes"": { ""Escape"": { ""clearOut"": { ""interval"": 50 } } } } }").Apply(TestData.Catalogue(), rules);
            Assert.AreEqual(50f, rules.For("Escape").ClearOutInterval);
            Assert.AreEqual(TestData.LoadRuleBook().For("Escape").ClearOutFirstAt, rules.For("Escape").ClearOutFirstAt, "what the tuning leaves out stays as rules.json says");
            Assert.AreEqual(TestData.LoadRuleBook().For("Dibs").ClearOutInterval, rules.For("Dibs").ClearOutInterval, "other modes keep their timers");
        }

        [Test]
        public void LettersInPlayFollowTheFloorArea()
        {
            var tuning = HouseTuning.FromJson(HouseJson);
            var houses = Houses().OrderBy(h => h.FloorArea).ToArray();
            for (int i = 1; i < houses.Length; i++)
                Assert.GreaterOrEqual(tuning.LettersInPlay(houses[i], 26), tuning.LettersInPlay(houses[i - 1], 26), houses[i].Name + " has more floor to fill");
            foreach (var house in houses)
            {
                int letters = tuning.LettersInPlay(house, 26);
                Assert.Greater(letters, 26, house.Name + " keeps more letters in play than the old 26");
                Assert.LessOrEqual(letters, tuning.MaxLetters);
            }
            var cupboard = new HouseLayout();
            cupboard.Rooms.Add(new RoomBox { Name = "Cupboard", MinX = 0f, MinZ = 0f, MaxX = 2f, MaxZ = 2f });
            Assert.AreEqual(tuning.MinLetters, tuning.LettersInPlay(cupboard, 26), "a tiny house still gets the minimum");
            Assert.AreEqual(26, HouseTuning.FromJson("{}").LettersInPlay(houses[0], 26), "without House tuning the scene's number stands");
            Assert.AreEqual(17, tuning.LettersInPlay(null, 17));
        }
    }
}
