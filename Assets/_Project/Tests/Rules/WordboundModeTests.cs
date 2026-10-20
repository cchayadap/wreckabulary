using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundModeTests
    {
        [TestCase("TimeRun", 1, 4, 1500, false)]
        [TestCase("TeamRace", 2, 2, 1500, false)]
        [TestCase("BrawlRace", 2, 2, 1500, true)]
        [TestCase("FreeForAll", 2, 8, 480, true)]
        [TestCase("Freestyle", 1, 8, 0, false)]
        public void ModesKeepTheirApprovedLimits(string id, int min, int max, int seconds, bool combat)
        {
            var mode = ModeDefinition.For(id);
            Assert.AreEqual(min, mode.MinimumPlayers);
            Assert.AreEqual(max, mode.MaximumPlayers);
            Assert.AreEqual(seconds, mode.TimeLimitSeconds);
            Assert.AreEqual(combat, mode.EnemyDamage);
            Assert.AreEqual(13, mode.Recipes.Count);
            Assert.IsTrue(mode.Recipes.Contains("BALL"));
            Assert.IsTrue(mode.Recipes.Contains("PLATE"));
            Assert.IsFalse(mode.Recipes.Contains("ROPE"));
            Assert.IsFalse(mode.Recipes.Contains("MAGNET"));
        }

        [TestCase(RaceFormat.Solo, 2)]
        [TestCase(RaceFormat.Duo, 4)]
        [TestCase(RaceFormat.Squad, 8)]
        public void RaceFormatsHaveTwoTeams(RaceFormat format, int maximum)
        {
            foreach (var id in new[] { "TeamRace", "BrawlRace" })
            {
                var mode = ModeDefinition.For(id, format);
                Assert.AreEqual(2, mode.TeamCount);
                Assert.AreEqual(maximum, mode.MaximumPlayers);
                Assert.IsEmpty(mode.ValidateRoster(new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) }));
            }
        }

        [Test]
        public void RosterRejectsDuplicatesInvalidSeatsAndEmptyOpponent()
        {
            var mode = ModeDefinition.For("TeamRace", RaceFormat.Duo);
            Assert.IsNotEmpty(mode.ValidateRoster(new[] { new WordboundSeat("a", 0), new WordboundSeat("a", 1) }));
            Assert.IsNotEmpty(mode.ValidateRoster(new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 0) }));
            Assert.IsNotEmpty(mode.ValidateRoster(new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 0, 1) }));
            Assert.IsNotEmpty(mode.ValidateRoster(new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 2) }));
            Assert.Throws<ArgumentException>(() => ModeDefinition.For("TimeRun", RaceFormat.Duo));
        }

        [Test]
        public void OwnershipAndDamageRemainDistinctAcrossModes()
        {
            var race = ModeDefinition.For("TeamRace");
            var brawl = ModeDefinition.For("BrawlRace");
            var free = ModeDefinition.For("Freestyle");
            Assert.IsFalse(race.PlayersCollide(0, 1));
            Assert.IsFalse(race.CanInteract(0, 1, false));
            Assert.IsFalse(race.CanDamage(0, 1));
            Assert.IsTrue(brawl.PlayersCollide(0, 1));
            Assert.IsFalse(brawl.CanInteract(0, 1, true));
            Assert.IsTrue(brawl.CanInteract(0, 1, false));
            Assert.IsFalse(brawl.CanDamage(0, 0));
            Assert.IsTrue(brawl.CanDamage(0, 1));
            Assert.IsFalse(free.PublicQueue);
            Assert.IsFalse(free.HazardDamage);
            Assert.IsFalse(free.CanDamage(0, 1));
        }

        [Test]
        public void RulesDoNotChangeExistingModesAndHashesIgnoreCulture()
        {
            var original = new GameRules();
            var originalMode = original.Mode;
            var mode = ModeDefinition.For("BrawlRace", RaceFormat.Squad);
            var rules = mode.CreateGameplayRules(original);
            Assert.AreEqual(originalMode, original.Mode);
            Assert.AreEqual("BrawlRace", rules.Mode);
            Assert.IsFalse(rules.FriendlyFire);
            Assert.AreEqual(120, rules.ReconnectGraceSeconds);
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
                Assert.AreEqual(mode.Hash, ModeDefinition.For("BrawlRace", RaceFormat.Squad).Hash);
            }
            finally { CultureInfo.CurrentCulture = previous; }
            Assert.AreNotEqual(mode.Hash, ModeDefinition.For("BrawlRace", RaceFormat.Duo).Hash);
        }
    }
}
