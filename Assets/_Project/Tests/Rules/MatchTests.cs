using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class MatchTests
    {
        static Combatant C(int id, int team, LifeState life) => new Combatant(id, team, life);

        [Test]
        public void TeamsForFreeForAllDuosAndCoop()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Teams.Assign(4, 1));
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, Teams.Assign(4, 2));
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, Teams.Assign(3, 4), "co-op: everyone on one team");
            Assert.IsFalse(Teams.AreTeammates(Teams.NoTeam, Teams.NoTeam), "no-team hazards are nobody's teammate");
        }

        [Test]
        public void LastPlayerStandingWins()
        {
            var r = WinCheck.Evaluate(new[] { C(0, 0, LifeState.Eliminated), C(1, 1, LifeState.Alive), C(2, 2, LifeState.Eliminated) });
            Assert.AreEqual(RoundState.Won, r.State);
            Assert.AreEqual(1, r.WinningTeam);
            Assert.AreEqual(RoundState.Ongoing, WinCheck.Evaluate(new[] { C(0, 0, LifeState.Alive), C(1, 1, LifeState.Alive) }).State);
        }

        [Test]
        public void SimultaneousKnockoutIsADraw()
        {
            var r = WinCheck.Evaluate(new[] { C(0, 0, LifeState.Eliminated), C(1, 1, LifeState.Eliminated) });
            Assert.AreEqual(RoundState.Draw, r.State);
            Assert.AreEqual(Teams.NoTeam, r.WinningTeam);
        }

        [Test]
        public void ADownedTeamWithSomeoneStandingIsStillIn()
        {
            var r = WinCheck.Evaluate(new[] { C(0, 0, LifeState.Downed), C(2, 0, LifeState.Alive), C(1, 1, LifeState.Alive), C(3, 1, LifeState.Eliminated) });
            Assert.AreEqual(RoundState.Ongoing, r.State);
            var all = new[] { C(0, 0, LifeState.Downed), C(2, 0, LifeState.Downed), C(1, 1, LifeState.Alive), C(3, 1, LifeState.Downed) };
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, WinCheck.Unrevivable(all), "nobody on team 0 can revive them");
            Assert.AreEqual(1, WinCheck.Evaluate(all).WinningTeam);
        }

        [Test]
        public void FirstToThreeRoundsWinsAndDrawsCountForNobody()
        {
            var score = new MatchScore(3);
            score.Record(new RoundOutcome(RoundState.Won, 1));
            score.Record(new RoundOutcome(RoundState.Draw, Teams.NoTeam));
            score.Record(new RoundOutcome(RoundState.Won, 0));
            score.Record(new RoundOutcome(RoundState.Won, 1));
            Assert.IsFalse(score.IsOver);
            score.Record(new RoundOutcome(RoundState.Won, 1));
            Assert.IsTrue(score.IsOver);
            Assert.AreEqual(1, score.MatchWinner);
            Assert.AreEqual(5, score.RoundsPlayed);
            Assert.AreEqual(1, score.Wins(0));
            Assert.Throws<InvalidOperationException>(() => score.Record(new RoundOutcome(RoundState.Won, 0)));
            Assert.Throws<ArgumentException>(() => new MatchScore(3).Record(new RoundOutcome(RoundState.Ongoing, Teams.NoTeam)));
        }
    }
}
