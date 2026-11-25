using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class RecentPlayersTests
    {
        const long Now = 1_800_000_000;
        const long Hour = 3600, Day = 86400;

        static RecentMatch Match(string mode, long endedAt, params (string name, int seat, bool you)[] players) =>
            new RecentMatch(mode, "pinwheel", endedAt, players.Select(p => new RecentPlayer(p.name, p.seat, p.you)));

        [Test]
        public void MatchesAreKeptNewestFirstAndCapped()
        {
            var recent = new RecentPlayers();
            for (int i = 0; i < RecentPlayers.MatchesKept + 5; i++)
                Assert.IsTrue(recent.Add(Match("Dibs", Now - (RecentPlayers.MatchesKept + 5 - i) * 60, ("Sam", 0, true)), Now));
            Assert.IsFalse(recent.Add(Match("Dibs", Now - 365 * 60, ("Sam", 0, true)), Now), "an older match than all kept ones falls off at once");
            Assert.AreEqual(RecentPlayers.MatchesKept, recent.Matches.Count, "only the newest matches stay");
            for (int i = 1; i < recent.Matches.Count; i++)
                Assert.Greater(recent.Matches[i - 1].EndedAt, recent.Matches[i].EndedAt, "newest first");
            Assert.AreEqual(Now - 60, recent.Matches[0].EndedAt);
        }

        [Test]
        public void AMatchOlderThanThirtyDaysIsDroppedAndTheSameMatchIsNotStoredTwice()
        {
            var recent = new RecentPlayers();
            Assert.IsFalse(recent.Add(Match("Dibs", Now - 31 * Day, ("Sam", 0, true)), Now), "too old to keep");
            Assert.IsTrue(recent.Add(Match("Dibs", Now - Hour, ("Sam", 0, true)), Now));
            Assert.IsFalse(recent.Add(Match("Dibs", Now - Hour, ("Sam", 0, true)), Now), "one finished match is one entry");
            Assert.AreEqual(1, recent.Matches.Count);
            recent.Tidy(Now + 31 * Day);
            Assert.AreEqual(0, recent.Matches.Count, "entries age out");
        }

        [Test]
        public void AMatchWithNoHumansOrNoModeIsNotStored()
        {
            var recent = new RecentPlayers();
            Assert.IsFalse(recent.Add(Match("Dibs", Now), Now));
            Assert.IsFalse(recent.Add(Match("", Now, ("Sam", 0, true)), Now));
            Assert.IsFalse(recent.Add(null, Now));
            Assert.IsFalse(recent.Add(Match("Dibs", Now, ("   ", 1, false)), Now), "a blank name is not a player");
        }

        [Test]
        public void PlayersAreOrderedBySeatOnePerSeatAndAtMostFour()
        {
            var match = Match("Duos", Now, ("Cleo", 2, false), ("Sam", 0, true), ("Dup", 2, false), ("Ana", 1, false), ("Bo", 3, false), ("Extra", 4, false));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, match.Players.Select(p => p.Seat).ToArray());
            CollectionAssert.AreEqual(new[] { "Sam", "Ana", "Cleo", "Bo" }, match.Players.Select(p => p.Name).ToArray());
            Assert.AreEqual(RecentPlayers.PlayersKept, match.Players.Count);
        }

        [Test]
        public void NamesAreCleanedAndShort()
        {
            var player = new RecentPlayer("  A\u0001very very very long player name here  ", 1, false);
            Assert.LessOrEqual(player.Name.Length, RecentPlayers.NameLength);
            Assert.IsFalse(player.Name.Any(char.IsControl));
            Assert.AreEqual("A", player.Initial);
            Assert.AreEqual(RecentPlayers.HighestSeat, new RecentPlayer("Sam", 99, false).Seat);
            Assert.AreEqual(0, new RecentPlayer("Sam", -3, false).Seat);
        }

        [Test]
        public void SaveAndLoadRoundTripWithQuotesAndOrder()
        {
            var recent = new RecentPlayers();
            recent.Add(Match("Dibs", Now - Day, ("Sam \"the\" \\ host", 0, true), ("Player 2", 1, false)), Now);
            recent.Add(Match("Escape", Now - Hour, ("Sam", 0, true)), Now);
            var again = RecentPlayers.Load(recent.Save(), Now);
            Assert.AreEqual(recent.Save(), again.Save());
            Assert.AreEqual("Escape", again.Matches[0].Mode);
            Assert.AreEqual("Sam \"the\" \\ host", again.Matches[1].Players[0].Name);
            Assert.IsTrue(again.Matches[1].Players[0].You);
            Assert.AreEqual(1, again.Matches[1].Players[1].Seat);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1,2]")]
        [TestCase("{\"version\":2,\"matches\":[]}")]
        [TestCase("{\"version\":1,\"matches\":7}")]
        public void BadSavesLoadAsAnEmptyList(string text)
        {
            Assert.AreEqual(0, RecentPlayers.Load(text, Now).Matches.Count);
        }

        [Test]
        public void ABrokenEntrySkipsOnlyThatEntry()
        {
            string text = "{\"version\":1,\"matches\":[{\"mode\":5},{\"mode\":\"Dibs\",\"map\":\"pinwheel\",\"endedAt\":" + (Now - 60) +
                ",\"players\":[{\"name\":\"Sam\",\"seat\":0,\"you\":true},{\"name\":7}]}]}";
            var recent = RecentPlayers.Load(text, Now);
            Assert.AreEqual(1, recent.Matches.Count);
            Assert.AreEqual(1, recent.Matches[0].Players.Count);
        }

        [Test]
        public void PeopleListsOthersOnceNewestFirst()
        {
            var recent = new RecentPlayers();
            recent.Add(Match("Dibs", Now - Day, ("Sam", 0, true), ("Ana", 1, false), ("Bo", 2, false)), Now);
            recent.Add(Match("Duos", Now - Hour, ("Sam", 0, true), ("ana", 2, false)), Now);
            CollectionAssert.AreEqual(new[] { "ana", "Bo" }, recent.People().Select(p => p.Name).ToArray(), "you are not listed and names repeat once");
            recent.Clear();
            Assert.AreEqual(0, recent.People().Count);
        }

        [TestCase(0, RecentPlayers.JustNow)]
        [TestCase(59, RecentPlayers.JustNow)]
        [TestCase(60, "1 min ago")]
        [TestCase(3599, "59 min ago")]
        [TestCase(3600, "1 h ago")]
        [TestCase(86399, "23 h ago")]
        [TestCase(86400, RecentPlayers.Yesterday)]
        [TestCase(3 * 86400, "3 days ago")]
        [TestCase(-50, RecentPlayers.JustNow)]
        public void WhenReadsPlainly(long ago, string expected)
        {
            Assert.AreEqual(expected, RecentPlayers.When(Now - ago, Now));
        }

        [Test]
        public void NamesLineShowsYouAsAWord()
        {
            var match = Match("Dibs", Now, ("Sam", 0, true), ("Player 2", 1, false));
            Assert.AreEqual("You, Player 2", RecentPlayers.Names(match, "You"));
            Assert.AreEqual("Sam, Player 2", RecentPlayers.Names(match, null));
            Assert.AreEqual("", RecentPlayers.Names(null, "You"));
        }
    }
}
