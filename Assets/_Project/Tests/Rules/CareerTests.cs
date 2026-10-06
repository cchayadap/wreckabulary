using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CareerTests
    {
        [Test]
        public void AMatchPaysCoinsAndXpAndKeepsTheBestScore()
        {
            var career = new Career();
            var win = Career.Reward("Dibs", "pinwheel", true, true, broken: 6, crafted: 2, damage: 40, endedAt: 100);
            Assert.AreEqual(6 * 10 + 2 * 25 + 40 + 150, win.Score);
            Assert.IsTrue(career.Record(win));
            var loss = Career.Reward("Dibs", "pinwheel", true, false, 1, 0, 0, 200);
            Assert.IsFalse(career.Record(loss), "a lower score is not a new best");
            Assert.AreEqual(win.Score, career.Bests["Dibs"]);
            Assert.AreEqual(win.Coins + loss.Coins, career.Coins);
            Assert.AreEqual(2, career.Matches);
            Assert.AreEqual(1, career.Wins);
            Assert.AreEqual(loss, career.History[0], "newest first");
            Assert.Greater(loss.Coins, 0, "even a quick loss pays something");
        }

        [Test]
        public void AFirstMatchThatScoresNothingStillCountsAsPlayed()
        {
            var career = new Career();
            var blank = Career.Reward("Duos", "pinwheel", true, false, 0, 0, 0, 100);
            Assert.AreEqual(0, blank.Score);
            Assert.IsFalse(career.Record(blank), "nothing to celebrate");
            Assert.AreEqual(0, career.Bests["Duos"], "but the mode shows as played");
            Assert.AreEqual(0, Career.Deserialize(career.Serialize()).Bests["Duos"], "and still after a reload");
            Assert.IsTrue(career.Record(Career.Reward("Duos", "pinwheel", true, false, 1, 0, 0, 200)), "the first real score is a best");
        }

        [Test]
        public void LevelsNeedMoreXpEachTime()
        {
            var career = new Career();
            Assert.AreEqual(1, career.Level);
            career.Xp = Career.XpToNext(1) - 1;
            Assert.AreEqual(1, career.Level);
            career.Xp = Career.XpToNext(1);
            Assert.AreEqual(2, career.Level);
            Assert.AreEqual(0, career.XpIntoLevel);
            career.Xp += Career.XpToNext(2) + 5;
            Assert.AreEqual(3, career.Level);
            Assert.AreEqual(5, career.XpIntoLevel);
            Assert.Greater(Career.XpToNext(3), Career.XpToNext(2));
        }

        [Test]
        public void HistoryKeepsOnlyTheNewestMatches()
        {
            var career = new Career();
            for (int i = 0; i < Career.HistoryLength + 5; i++)
                career.Record(Career.Reward("Duos", "courtyard", true, false, i, 0, 0, i));
            Assert.AreEqual(Career.HistoryLength, career.History.Count);
            Assert.AreEqual((long)Career.HistoryLength + 4, career.History[0].EndedAt);
            Assert.AreEqual(Career.HistoryLength + 5, career.Matches, "the totals still count every match");
        }

        [Test]
        public void TheCartSellsOnlyWhatYouCanAffordOnce()
        {
            var career = new Career { Coins = 130 };
            Assert.IsFalse(career.Owns("colour", "sky"));
            Assert.IsTrue(career.Owns("colour", "tomato"), "starter colours are free");
            Assert.IsTrue(career.Owns("skin", "Classic"));
            Assert.IsTrue(career.Buy("skin:Candy").Contains("more coins"));
            Assert.IsNull(career.Buy("colour:sky"));
            Assert.AreEqual(10, career.Coins);
            Assert.IsTrue(career.Owns("colour", "sky"));
            Assert.AreEqual("You already own it.", career.Buy("colour:sky"));
            Assert.AreEqual("That isn't in the shop.", career.Buy("colour:gold"));
            Assert.IsTrue(Career.Shop.All(s => s.Price > 0));
        }

        [Test]
        public void LooksWornBeforeTheShopStayUnlocked()
        {
            var career = new Career();
            Assert.IsTrue(career.Keep("Arcade", "grape"), "something new to save");
            Assert.IsTrue(career.Owns("skin", "Arcade"));
            Assert.IsTrue(career.Owns("colour", "grape"));
            Assert.IsFalse(career.Keep("Arcade", "grape"), "nothing new the second time");
            Assert.IsFalse(career.Keep("Classic", "tomato"));
            Assert.AreEqual(2, career.Owned.Count, "free looks are not added to the owned list");
        }

        [Test]
        public void ACareerSurvivesSavingAndLoading()
        {
            var career = new Career { Name = "Tiles \"T\" McGee", Coins = 42, Xp = 350 };
            career.Owned.Add("skin:Candy");
            career.Record(Career.Reward("MovingDay", "pinwheel", true, true, 3, 1, 12, 1700000000));
            var loaded = Career.Deserialize(career.Serialize());
            Assert.AreEqual("Tiles \"T\" McGee", loaded.Name);
            Assert.AreEqual(career.Coins, loaded.Coins);
            Assert.AreEqual(career.Xp, loaded.Xp);
            Assert.AreEqual(1, loaded.Matches);
            Assert.AreEqual(1, loaded.Wins);
            CollectionAssert.AreEqual(career.Owned, loaded.Owned);
            Assert.AreEqual(career.Bests["MovingDay"], loaded.Bests["MovingDay"]);
            Assert.AreEqual(1, loaded.History.Count);
            var a = career.History[0];
            var b = loaded.History[0];
            Assert.AreEqual(a.Mode, b.Mode);
            Assert.AreEqual(a.Map, b.Map);
            Assert.AreEqual(a.Won, b.Won);
            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.EndedAt, b.EndedAt);
        }

        [Test]
        public void ABrokenSaveFallsBackFieldByField()
        {
            Assert.AreEqual(0, Career.Deserialize("not json").Coins);
            Assert.AreEqual(0, Career.Deserialize("").Coins);
            var loaded = Career.Deserialize(
                "{\"name\":\"x\",\"coins\":-5,\"xp\":\"lots\",\"matches\":3,\"wins\":9,\"owned\":[\"colour:sky\",\"cheat:gold\"]," +
                "\"bests\":{\"Dibs\":120,\"Duos\":\"high\",\"MovingOut\":-4},\"history\":[{\"mode\":\"Dibs\",\"score\":120,\"endedAt\":1e300},{\"score\":5}]}");
            Assert.AreEqual("Housemate", loaded.Name, "a one-letter name is replaced");
            Assert.AreEqual(0, loaded.Coins);
            Assert.AreEqual(0, loaded.Xp);
            Assert.AreEqual(3, loaded.Wins, "wins never exceed matches");
            CollectionAssert.AreEqual(new[] { "colour:sky" }, loaded.Owned, "unknown purchases are dropped");
            Assert.AreEqual(120, loaded.Bests["Dibs"]);
            Assert.IsFalse(loaded.Bests.ContainsKey("Duos"));
            Assert.IsFalse(loaded.Bests.ContainsKey("MovingOut"), "a negative best is dropped");
            Assert.AreEqual(1, loaded.History.Count, "a match without a mode is dropped");
            Assert.AreEqual(Career.LatestTime, loaded.History[0].EndedAt, "a time past the year 9999 is clamped");
        }
    }
}
