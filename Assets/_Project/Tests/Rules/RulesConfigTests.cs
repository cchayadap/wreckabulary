using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class RulesConfigTests
    {
        [Test]
        public void ShippedRulesAreValidForEveryMode()
        {
            var book = TestData.LoadRuleBook();
            Assert.IsEmpty(book.Validate());
            CollectionAssert.AreEquivalent(new[] { "Hub", "Tutorial", "Dibs", "Duos", "MovingOut", "MovingDay" }, book.Modes);
        }

        [Test]
        public void TheBriefsDefaults()
        {
            var r = TestData.RulesFor("Dibs");
            Assert.AreEqual(100f, r.MaxHealth);
            Assert.AreEqual(18, r.MaxLetters);
            Assert.AreEqual(2, r.MaxCarried);
            Assert.AreEqual(2, r.MaxDeployed);
            Assert.AreEqual(0, r.LettersDroppedPerHit, "D1: ordinary hits don't knock letters loose");
            Assert.IsTrue(r.ClearOutEnabled);
            Assert.AreEqual(3, r.RoundsToWin);
        }

        [Test]
        public void TeamModesHaveTeamsReviveAndNoFriendlyFire()
        {
            foreach (string mode in new[] { "Duos", "MovingOut" })
            {
                var r = TestData.RulesFor(mode);
                Assert.Greater(r.TeamSize, 1, mode);
                Assert.IsTrue(r.DownedEnabled, mode);
                Assert.IsFalse(r.FriendlyFire, mode);
            }
            Assert.AreEqual(2, TestData.RulesFor("Duos").TeamSize);
            Assert.IsFalse(TestData.RulesFor("Dibs").DownedEnabled);
        }

        [Test]
        public void ModesOnlyChangeWhatTheyList()
        {
            var book = TestData.LoadRuleBook();
            Assert.AreEqual(book.Defaults.CraftBaseSeconds, book.For("Duos").CraftBaseSeconds);
            Assert.AreEqual(book.Defaults.ClearOutFirstAt, book.For("Dibs").ClearOutFirstAt);
            Assert.AreNotEqual(book.Defaults.ClearOutFirstAt, book.For("MovingOut").ClearOutFirstAt);
            Assert.AreEqual(book.Defaults.ClearOutWarnSeconds, 10f);
            book.For("Duos").BleedOutSeconds[0] = 99f;
            Assert.AreEqual(20f, book.For("MovingOut").BleedOutSeconds[0], "modes don't share arrays");
        }

        [Test]
        public void CraftTimeGrowsWithTheWord()
        {
            var r = TestData.RulesFor("Dibs");
            var c = TestData.Catalogue();
            Assert.AreEqual(0.96f, r.CraftSeconds(c.Get("BAT")), 1e-5);
            Assert.AreEqual(1.2f, r.CraftSeconds(c.Get("TABLE")), 1e-5);
            Assert.AreEqual(20f, r.BleedOutFor(1));
            Assert.AreEqual(12f, r.BleedOutFor(2));
            Assert.AreEqual(6f, r.BleedOutFor(3));
            Assert.AreEqual(6f, r.BleedOutFor(9), "stays at the shortest");
        }

        [Test]
        public void TyposInRulesAreErrorsNotSilentDefaults()
        {
            var e = Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {\"maxHelth\": 90}, \"modes\": {}}", "rules.json"));
            Assert.IsTrue(e.Message.Contains("maxHelth") && e.Message.Contains("rules.json"), e.Message);
            Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {}, \"modes\": {\"X\": {\"clearOut\": {\"firstat\": 3}}}}"));
            Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {\"maxLetters\": 17.5}, \"modes\": {}}"));
        }

        [Test]
        public void BadValuesAreReported()
        {
            var book = RuleBook.FromJson("{\"defaults\": {}, \"modes\": {\"Bad\": {\"hitStunMax\": 2, \"downed\": true, \"starterLetters\": \"abc\"}}}");
            var problems = book.Validate();
            Assert.IsTrue(problems.Any(p => p.StartsWith("Bad:") && p.Contains("stun")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("teams")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("A-Z")), string.Join("\n", problems));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => book.For("Nope"));
        }
    }
}
