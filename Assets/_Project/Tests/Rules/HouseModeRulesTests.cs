using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class HouseModeRulesTests
    {
        static RuleBook Layered()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            var book = TestData.LoadRuleBook();
            book.Layer(TestData.Read("modes_house.json"));
            HouseTuning.FromJson(TestData.Read("items_house.json")).Apply(items, book);
            return book;
        }

        static string Modes(string body) => "{ \"modes\": { " + body + " } }";

        [Test]
        public void HouseModesLayerOverRulesJsonAndStayValid()
        {
            var book = Layered();
            Assert.IsEmpty(book.Validate());
            CollectionAssert.AreEquivalent(new[] { "Deathmatch", "War", "ShoppingList", "RiddleRush", "BossFight" }, book.LayeredModes);
            foreach (var mode in HouseModes.All)
                Assert.IsTrue(book.Modes.Contains(mode.Id), $"{mode.Id} needs rules in rules.json or modes_house.json");
        }

        [Test]
        public void FreeForAllRespawnsRacesToTenAndKeepsTheMoversOff()
        {
            var r = Layered().For(HouseModes.FreeForAll);
            Assert.AreEqual(10, r.KnockoutsToWin);
            Assert.AreEqual(3f, r.RespawnSeconds);
            Assert.AreEqual(240f, r.RoundTimeLimitSeconds);
            Assert.AreEqual(1, r.RoundsToWin);
            Assert.AreEqual(1, r.TeamSize);
            Assert.IsTrue(r.FriendlyFire);
            Assert.IsFalse(r.DownedEnabled);
            Assert.IsFalse(r.ClearOutEnabled);
            Assert.AreEqual(Layered().For("Dibs").MaxHealth, r.MaxHealth, "everything else comes from its base");
        }

        [Test]
        public void WarSharesTwelveLivesWithoutReviveOrFriendlyFire()
        {
            var r = Layered().For(HouseModes.War);
            Assert.AreEqual(12, r.TeamLives);
            Assert.AreEqual(20f, r.HoldBonusSeconds);
            Assert.AreEqual(3f, r.RespawnSeconds);
            Assert.AreEqual(300f, r.RoundTimeLimitSeconds);
            Assert.AreEqual(4, r.TeamSize);
            Assert.IsFalse(r.FriendlyFire);
            Assert.IsFalse(r.DownedEnabled);
            Assert.IsFalse(r.ClearOutEnabled);
        }

        [Test]
        public void BossFightIsOneTeamWithoutReviveOrMovers()
        {
            var r = Layered().For(HouseModes.BossFight);
            Assert.AreEqual(4, r.TeamSize);
            Assert.IsFalse(r.DownedEnabled);
            Assert.IsFalse(r.FriendlyFire);
            Assert.AreEqual(3f, r.RespawnSeconds);
            Assert.AreEqual(300f, r.RoundTimeLimitSeconds);
            Assert.AreEqual(16, r.MaxLetters);
            Assert.AreEqual(1, r.RoundsToWin);
            Assert.IsFalse(r.ClearOutEnabled);
        }

        [Test]
        public void LayeringLeavesEveryRulesJsonModeAsItWas()
        {
            var plain = TestData.HouseRuleBook();
            var layered = Layered();
            var fields = typeof(GameRules).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType.IsValueType || f.FieldType == typeof(string));
            foreach (string mode in plain.Modes)
                foreach (var field in fields)
                    Assert.AreEqual(field.GetValue(plain.For(mode)), field.GetValue(layered.For(mode)), $"{mode}.{field.Name}");
            CollectionAssert.AreEquivalent(plain.Modes, layered.Modes.Except(layered.LayeredModes));
        }

        [Test]
        public void RulesJsonHasNoHouseOnlyKeys()
        {
            string rules = TestData.Read("rules.json");
            foreach (string key in new[] { "knockoutsToWin", "teamLives", "holdBonus", "\"settings\"", "\"base\"" })
                StringAssert.DoesNotContain(key, rules, "House-only settings live in modes_house.json.");
        }

        [Test]
        public void AHouseModeCannotRedefineOrInventItsBase()
        {
            var book = TestData.LoadRuleBook();
            StringAssert.Contains("rules.json mode", Assert.Throws<FormatException>(() => book.Layer(Modes("\"Dibs\": { \"respawn\": 3 }"))).Message);
            StringAssert.Contains("base 'Nope'", Assert.Throws<FormatException>(() => book.Layer(Modes("\"X\": { \"base\": \"Nope\" }"))).Message);
            StringAssert.Contains("'colour'", Assert.Throws<FormatException>(() => book.Layer(Modes("\"Y\": { \"colour\": 3 }"))).Message);
            StringAssert.Contains("'rounds'", Assert.Throws<FormatException>(() => book.Layer("{ \"rounds\": 1, \"modes\": {} }")).Message);
            Assert.Throws<FormatException>(() => RuleBook.FromJson("{ \"defaults\": { \"base\": \"Dibs\" }, \"modes\": {} }"), "base is a House-file key only");
        }

        [Test]
        public void HouseOnlyTargetsStayOutOfTheSharedRulesFileAndItsPublicFields()
        {
            foreach (string key in new[] { "knockoutsToWin", "teamLives", "holdBonus", "settings" })
                StringAssert.Contains("modes_house.json", Assert.Throws<FormatException>(() =>
                    RuleBook.FromJson("{ \"defaults\": {}, \"modes\": { \"Dibs\": { \"" + key + "\": 1 } } }")).Message, key);
            foreach (string name in new[] { "KnockoutsToWin", "TeamLives", "HoldBonusSeconds" })
                Assert.IsNull(typeof(GameRules).GetField(name, BindingFlags.Public | BindingFlags.Instance), name + " must not join the online rules fingerprint");
            var book = Layered();
            Assert.AreEqual(10, book.For("Deathmatch").KnockoutsToWin);
            Assert.Greater(book.For("War").TeamLives, 0);
        }

        [Test]
        public void FreeSettingsCarryNumbersForWordGames()
        {
            var book = TestData.LoadRuleBook();
            book.Layer(Modes("\"List\": { \"base\": \"Dibs\", \"settings\": { \"items\": 5, \"pace\": 1.5 } }, \"Riddle\": { \"base\": \"List\" }"));
            var list = book.For("List");
            Assert.AreEqual(5, list.Setting("items", 0));
            Assert.AreEqual(1.5f, list.Setting("pace", 0f));
            Assert.AreEqual(7, list.Setting("missing", 7));
            Assert.AreEqual(5, book.For("Riddle").Setting("items", 0), "a House mode may build on another");
            book.For("Riddle").SetSetting("items", 9);
            Assert.AreEqual(5, list.Setting("items", 0), "clones never share settings");
            Assert.AreEqual(0, book.For("Dibs").Setting("items", 0));
        }

        [Test]
        public void NegativeHouseNumbersAreRejected()
        {
            var book = TestData.LoadRuleBook();
            book.Layer(Modes("\"Bad\": { \"base\": \"Dibs\", \"teamLives\": -1 }"));
            Assert.IsTrue(book.Validate().Any(p => p.StartsWith("Bad:") && p.Contains("teamLives")));
        }
    }
}
