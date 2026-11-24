using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class HouseModesTests
    {
        [Test]
        public void EveryModeHasOneUniqueIdANameAGoalAndAFamily()
        {
            Assert.AreEqual(HouseModes.All.Count, HouseModes.All.Select(m => m.Id).Distinct().Count());
            Assert.AreEqual(HouseModes.All.Count, HouseModes.All.Select(m => m.Name).Distinct().Count());
            foreach (var mode in HouseModes.All)
            {
                Assert.IsNotEmpty(mode.Name, mode.Id);
                Assert.IsNotEmpty(mode.Goal, mode.Id);
                Assert.IsNotEmpty(mode.Tag, mode.Id);
                Assert.IsNotEmpty(mode.Blurb, mode.Id);
                Assert.That(mode.MinSeats, Is.InRange(1, mode.MaxSeats), mode.Id);
                Assert.That(mode.BotSeats, Is.InRange(0, mode.MaxSeats), mode.Id);
            }
        }

        [TestCase("Dibs", "Last One Standing"), TestCase("Duos", "Duos"), TestCase("MovingOut", "Pack the Van"), TestCase("MovingDay", "Furnish the House")]
        [TestCase("Escape", "Escape the House"), TestCase("Tutorial", "Tutorial"), TestCase("Deathmatch", "Free-for-All"), TestCase("War", "Team Battle")]
        [TestCase("ShoppingList", "Spell the List"), TestCase("RiddleRush", "Riddles"), TestCase("BossFight", "Boss Fight")]
        public void PlayerFacingNamesComeFromOnePlace(string id, string name) => Assert.AreEqual(name, HouseModes.Name(id));

        [Test]
        public void LobbyFamiliesHoldTheirModesInOrder()
        {
            Assert.AreEqual(new[] { "Dibs", "Duos", "Deathmatch", "War" }, HouseModes.InFamily(HouseModeFamily.Classic).Select(m => m.Id).ToArray());
            Assert.AreEqual(new[] { "MovingOut", "MovingDay", "Escape" }, HouseModes.InFamily(HouseModeFamily.Coop).Select(m => m.Id).ToArray());
            Assert.AreEqual(new[] { "ShoppingList", "RiddleRush", "BossFight" }, HouseModes.InFamily(HouseModeFamily.Arcade).Select(m => m.Id).ToArray());
            Assert.AreEqual(new[] { "Tutorial" }, HouseModes.InFamily(HouseModeFamily.Training).Select(m => m.Id).ToArray());
            Assert.AreEqual(new[] { "Classic", "Co-op", "Arcade", "Training" }, HouseModes.Families.Select(HouseModes.FamilyName).ToArray());
            Assert.AreEqual(Enum.GetValues(typeof(HouseModeFamily)).Cast<HouseModeFamily>().ToArray(), HouseModes.Families.ToArray());
        }

        [Test]
        public void EachPracticeGroupListsItsHouseModesThenItsSoloCourse()
        {
            Assert.AreEqual(new[] { "Dibs", "Duos", "Deathmatch", "War" }, HouseModes.ModesIn(HouseModeFamily.Classic));
            Assert.AreEqual(new[] { "MovingOut", "MovingDay", "Escape" }, HouseModes.ModesIn(HouseModeFamily.Coop));
            Assert.AreEqual(new[] { "ShoppingList", "RiddleRush", "BossFight", "TimeRun" }, HouseModes.ModesIn(HouseModeFamily.Arcade));
            Assert.AreEqual(new[] { "Tutorial", "Freestyle" }, HouseModes.ModesIn(HouseModeFamily.Training));
            var grouped = HouseModes.Families.SelectMany(HouseModes.ModesIn).ToArray();
            Assert.AreEqual(grouped.Length, grouped.Distinct().Count(), "every mode sits in exactly one group");
            CollectionAssert.IsSubsetOf(HouseModes.All.Select(m => m.Id), grouped, "every house mode has a group");
            foreach (var family in HouseModes.Families)
                foreach (string mode in HouseModes.ModesIn(family)) Assert.AreEqual(family, HouseModes.FamilyOf(mode), mode);
            Assert.IsFalse(HouseModes.Has("TimeRun") || HouseModes.Has("Freestyle"), "courses stay Wordbound modes");
            Assert.AreEqual(HouseModeFamily.Classic, HouseModes.FamilyOf("Nope"), "an unknown mode falls back to the first group");
            Assert.AreEqual(HouseModeFamily.Classic, HouseModes.FamilyOf(null));
        }

        [Test]
        public void FreeForAllUsesItsOwnIdBesideTheOnlineMode()
        {
            Assert.AreEqual("Deathmatch", HouseModes.FreeForAll);
            Assert.IsFalse(HouseModes.Has("FreeForAll"), "FreeForAll is the online Wordbound mode id.");
        }

        [Test]
        public void SeatsTeamsAndBotsMatchEachMode()
        {
            var war = HouseModes.Find(HouseModes.War);
            Assert.AreEqual(2, war.Sides);
            Assert.AreEqual(8, war.MaxSeats);
            Assert.AreEqual(8, war.BotSeats);
            Assert.IsTrue(war.Teams);
            Assert.AreEqual(4, HouseModes.BotSeats(HouseModes.FreeForAll));
            Assert.AreEqual(0, HouseModes.Find(HouseModes.FreeForAll).Sides);
            Assert.IsTrue(HouseModes.Find(HouseModes.Duos).Teams);
            foreach (string coop in new[] { HouseModes.MovingOut, HouseModes.MovingDay, HouseModes.Escape })
            {
                Assert.IsTrue(HouseModes.Find(coop).Coop, coop);
                Assert.AreEqual(0, HouseModes.BotSeats(coop), coop);
            }
            Assert.AreEqual(4, HouseModes.BotSeats("Hub"), "Unknown modes keep the old four-seat fill.");
            Assert.IsTrue(HouseModes.Rounds("Dibs") && HouseModes.Rounds("Duos"));
            Assert.IsFalse(HouseModes.Rounds("Deathmatch") || HouseModes.Rounds("War") || HouseModes.Rounds("Escape"));
        }

        [Test]
        public void BlurbsFillTheirNumbersFromTheRules()
        {
            var rules = new GameRules { RoundsToWin = 3, KnockoutsToWin = 10, TeamLives = 12, RespawnSeconds = 3f };
            Assert.AreEqual("Everyone against everyone. You come back three seconds after a knockout. First to 10 knockouts wins, or the most when time runs out.",
                HouseModes.Blurb(HouseModes.FreeForAll, rules));
            StringAssert.StartsWith("Two teams with 12 lives each.", HouseModes.Blurb(HouseModes.War, rules));
            StringAssert.Contains("First to three takes the house.", HouseModes.Blurb(HouseModes.Dibs, rules));
            foreach (var mode in HouseModes.All) StringAssert.DoesNotContain("{", HouseModes.Blurb(mode.Id, rules), mode.Id);
            Assert.AreEqual("", HouseModes.Blurb("Hub", rules));
        }

        [Test]
        public void UnknownModesFallBackQuietly()
        {
            Assert.IsNull(HouseModes.Find("Nope"));
            Assert.IsNull(HouseModes.Find(null));
            Assert.AreEqual("Nope", HouseModes.Name("Nope"));
            Assert.AreEqual("", HouseModes.Goal("Hub"));
            Assert.IsFalse(HouseModes.Has(null));
        }
    }
}
