using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundDirectorTests
    {
        const int Total = 39;
        const long Limit = 1500L * ModeDefinition.TickRate;
        const long Midrace = 30000;

        static ModeDefinition Race(string ruleset = "DeadLetter.Rush") => ModeDefinition.For("TeamRace", RaceFormat.Duo, ruleset);

        static WordboundDirectorTeam Ahead(int trail = 0, int lead = 0, int express = 0, int inspection = 0) =>
            new WordboundDirectorTeam(0, 12, new long[] { 24000, 26000, 28000, 29000 }, false, trail, lead, express, inspection);

        static WordboundDirectorTeam Behind(int trail = 0, int lead = 0, int express = 0, int inspection = 0) =>
            new WordboundDirectorTeam(1, 6, new long[] { 18000, 22000, 26000, 29000 }, false, trail, lead, express, inspection);

        static WordboundDirectorTeam[] Pair(long tick, int trail = 0, int lead = 0) => new[]
        {
            new WordboundDirectorTeam(0, 5, new[] { tick - 4000, tick - 3000, tick - 2000, tick - 1000 }, false, 0, lead),
            new WordboundDirectorTeam(1, 1, new[] { tick - 500 }, false, trail)
        };

        [Test]
        public void TotalObjectivesCountsEveryCheckpointAndLapFinish()
        {
            Assert.AreEqual(39, WordboundRaceDirector.TotalObjectives(new[] { 12, 12, 12 }));
            Assert.AreEqual(36, WordboundRaceDirector.TotalObjectives(new[] { 10, 11, 12 }));
            Assert.Throws<ArgumentException>(() => WordboundRaceDirector.TotalObjectives(Array.Empty<int>()));
            Assert.Throws<ArgumentException>(() => WordboundRaceDirector.TotalObjectives(new[] { 3, -1 }));
        }

        [Test]
        public void EtaUsesThePlannedPaceBeforeTheFirstObjective()
        {
            var fresh = new WordboundDirectorTeam(0, 0, Array.Empty<long>(), false);
            Assert.AreEqual(600 + 39 * (Limit / Total), WordboundRaceDirector.Eta(600, Total, Limit, fresh));
            Assert.AreEqual(90573, WordboundRaceDirector.Eta(600, Total, Limit, fresh));
        }

        [Test]
        public void EtaUsesOverallPaceUntilFourObjectives()
        {
            var team = new WordboundDirectorTeam(0, 2, new long[] { 2000, 5000 }, false);
            Assert.AreEqual(5000 + 37 * 2500, WordboundRaceDirector.Eta(5000, Total, Limit, team));
        }

        [Test]
        public void EtaBlendsThreePartsRecentPaceWithOnePartOverall()
        {
            var team = new WordboundDirectorTeam(0, 6, new long[] { 6000, 8000, 10000, 11000 }, false);
            Assert.AreEqual(12000 + 33 * ((3 * 1500 + 2000) / 4), WordboundRaceDirector.Eta(12000, Total, Limit, team));
            Assert.AreEqual(77250, WordboundRaceDirector.Eta(Midrace, Total, Limit, Ahead()));
            Assert.AreEqual(145500, WordboundRaceDirector.Eta(Midrace, Total, Limit, Behind()));
            var finished = new WordboundDirectorTeam(0, Total, new long[] { 70000, 71000, 72000, 73000 }, true);
            Assert.AreEqual(74000, WordboundRaceDirector.Eta(74000, Total, Limit, finished));
        }

        [TestCase(-3000, 0, 0)]
        [TestCase(0, 0, 0)]
        [TestCase(1200, 0, 0)]
        [TestCase(1239, 0, 0)]
        [TestCase(1240, 1, 0)]
        [TestCase(1800, 15, 0)]
        [TestCase(1854, 16, 1)]
        [TestCase(4200, 75, 44)]
        [TestCase(7200, 150, 100)]
        [TestCase(100000, 150, 100)]
        public void BiasTargetsClampBetweenThresholdAndMaximum(long gap, int trail, int lead)
        {
            Assert.AreEqual(trail, WordboundRaceDirector.TrailTarget(gap));
            Assert.AreEqual(lead, WordboundRaceDirector.LeadTarget(gap));
        }

        [TestCase(0, 150, 50)]
        [TestCase(50, 150, 100)]
        [TestCase(100, 150, 150)]
        [TestCase(150, 0, 100)]
        [TestCase(30, 40, 40)]
        [TestCase(100, 75, 75)]
        [TestCase(20, 0, 0)]
        [TestCase(0, 0, 0)]
        public void SlewMovesAtMostFiftyPermillePerDraw(int previous, int target, int expected)
        {
            Assert.AreEqual(expected, WordboundRaceDirector.Slew(previous, target));
        }

        [Test]
        public void PlanHelpsTheTrailerAndSlowsTheLeader()
        {
            var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Behind(), Ahead() });
            Assert.IsTrue(plan.Active);
            Assert.AreEqual(1, plan.TrailTeam);
            Assert.AreEqual(0, plan.LeadTeam);
            Assert.AreEqual(145500 - 77250, plan.GapTicks);
            Assert.AreEqual(50, plan.ExpressPermille);
            Assert.AreEqual(50, plan.InspectionPermille);
            CollectionAssert.AreEqual(new[] { 0, 1 }, plan.Teams.Select(t => t.Team).ToArray());
            CollectionAssert.AreEqual(new[] { 77250L, 145500L }, plan.Teams.Select(t => t.EtaTick).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 50 }, plan.Teams.Select(t => t.TrailBias).ToArray());
            CollectionAssert.AreEqual(new[] { 50, 0 }, plan.Teams.Select(t => t.LeadBias).ToArray());
        }

        [Test]
        public void BiasesRampToTheirCapsOneSlewStepPerDraw()
        {
            int trail = 0, lead = 0;
            var express = new int[4];
            var inspection = new int[4];
            for (int draw = 0; draw < 4; draw++)
            {
                var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Ahead(lead: lead), Behind(trail: trail) });
                trail = plan.Teams[1].TrailBias;
                lead = plan.Teams[0].LeadBias;
                express[draw] = plan.ExpressPermille;
                inspection[draw] = plan.InspectionPermille;
            }
            CollectionAssert.AreEqual(new[] { 50, 100, 150, 150 }, express);
            CollectionAssert.AreEqual(new[] { 50, 100, 100, 100 }, inspection);
        }

        [Test]
        public void ALeadChangeHandsTheBiasOverGradually()
        {
            var nowAhead = new WordboundDirectorTeam(1, 12, new long[] { 24000, 26000, 28000, 29000 }, false, 150, 0);
            var nowBehind = new WordboundDirectorTeam(0, 6, new long[] { 18000, 22000, 26000, 29000 }, false, 0, 100);
            var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { nowBehind, nowAhead });
            Assert.AreEqual(0, plan.TrailTeam);
            Assert.AreEqual(1, plan.LeadTeam);
            CollectionAssert.AreEqual(new[] { 50, 100 }, plan.Teams.Select(t => t.TrailBias).ToArray());
            CollectionAssert.AreEqual(new[] { 50, 50 }, plan.Teams.Select(t => t.LeadBias).ToArray());
            Assert.AreEqual(50, plan.ExpressPermille);
            Assert.AreEqual(50, plan.InspectionPermille);
        }

        [Test]
        public void EqualEtasGiveNoBias()
        {
            var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[]
            {
                new WordboundDirectorTeam(0, 12, new long[] { 24000, 26000, 28000, 29000 }, false, 120, 0),
                new WordboundDirectorTeam(1, 12, new long[] { 24000, 26000, 28000, 29000 }, false, 0, 80)
            });
            Assert.IsTrue(plan.Active);
            Assert.AreEqual(-1, plan.TrailTeam);
            Assert.AreEqual(-1, plan.LeadTeam);
            Assert.AreEqual(0, plan.GapTicks);
            Assert.AreEqual(0, plan.ExpressPermille);
            Assert.AreEqual(0, plan.InspectionPermille);
            CollectionAssert.AreEqual(new[] { 70, 0 }, plan.Teams.Select(t => t.TrailBias).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 30 }, plan.Teams.Select(t => t.LeadBias).ToArray());
        }

        [TestCase(7199, false)]
        [TestCase(7200, true)]
        [TestCase(85500, true)]
        [TestCase(85501, false)]
        public void DirectorWaitsTwoMinutesAndNeverStartsInTheLastMinute(long tick, bool active)
        {
            var plan = WordboundRaceDirector.Plan(Race(), tick, Total, Pair(tick, 100, 100));
            Assert.AreEqual(active, plan.Active);
            Assert.AreEqual(active, WordboundRaceDirector.Active(Race(), tick, Pair(tick)));
            Assert.AreEqual(active ? 1 : -1, plan.TrailTeam);
            Assert.AreEqual(active ? 150 : 0, plan.ExpressPermille);
            Assert.AreEqual(active ? 100 : 0, plan.InspectionPermille);
            Assert.AreEqual(active ? 150 : 50, plan.Teams[1].TrailBias);
            Assert.AreEqual(active ? 100 : 50, plan.Teams[0].LeadBias);
        }

        [TestCase("TimeRun", RaceFormat.Solo, "DeadLetter.Rush")]
        [TestCase("TeamRace", RaceFormat.Duo, "DeadLetter.Rush.Steady")]
        [TestCase("BrawlRace", RaceFormat.Squad, "Unspeller.Storm.Steady")]
        [TestCase("Freestyle", RaceFormat.Solo, "DeadLetter.Free")]
        [TestCase("FreeForAll", RaceFormat.Solo, "Classic")]
        [TestCase("TeamRace", RaceFormat.Solo, "Classic")]
        public void OffSwitchesKeepTheDirectorSilent(string modeId, RaceFormat format, string ruleset)
        {
            var mode = ModeDefinition.For(modeId, format, ruleset);
            var plan = WordboundRaceDirector.Plan(mode, Midrace, Total, Pair(Midrace, 150, 100));
            Assert.IsFalse(WordboundRaceDirector.Enabled(mode));
            Assert.IsFalse(WordboundRaceDirector.Active(mode, Midrace, Pair(Midrace)));
            Assert.IsFalse(plan.Active);
            Assert.AreEqual(-1, plan.TrailTeam);
            Assert.AreEqual(-1, plan.LeadTeam);
            Assert.AreEqual(0, plan.ExpressPermille);
            Assert.AreEqual(0, plan.InspectionPermille);
            Assert.IsTrue(plan.Teams.All(t => t.TrailBias == 0 && t.LeadBias == 0 && t.EtaTick == 0));
        }

        [TestCase("TeamRace", "DeadLetter.Rush")]
        [TestCase("BrawlRace", "House.LightsOut")]
        [TestCase("BrawlRace", "Unspeller.Storm.G1")]
        public void TwoTeamRacesWithoutSteadyEnableTheDirector(string modeId, string ruleset)
        {
            Assert.IsTrue(WordboundRaceDirector.Enabled(ModeDefinition.For(modeId, RaceFormat.Duo, ruleset)));
        }

        [Test]
        public void AFinishedTeamStopsTheDirector()
        {
            var done = new WordboundDirectorTeam(0, Total, new long[] { 26000, 27000, 28000, 29000 }, true, 0, 100);
            var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { done, Behind(trail: 150) });
            Assert.IsFalse(plan.Active);
            Assert.AreEqual(0, plan.ExpressPermille);
            Assert.AreEqual(0, plan.InspectionPermille);
            Assert.AreEqual(100, plan.Teams[1].TrailBias);
            Assert.AreEqual(50, plan.Teams[0].LeadBias);
        }

        [Test]
        public void UsedCardsReachTheirPerMatchCaps()
        {
            var expressSpent = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Ahead(), Behind(express: 4) });
            Assert.AreEqual(0, expressSpent.ExpressPermille);
            Assert.AreEqual(50, expressSpent.Teams[1].TrailBias);
            Assert.AreEqual(50, expressSpent.InspectionPermille);
            var inspectionSpent = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Ahead(inspection: 3), Behind(express: 3) });
            Assert.AreEqual(0, inspectionSpent.InspectionPermille);
            Assert.AreEqual(50, inspectionSpent.ExpressPermille);
            Assert.AreEqual(4, Race().Ruleset.Deck.ExpressPerMatch);
            Assert.AreEqual(15 * ModeDefinition.TickRate,
                Race().Ruleset.Deck.InspectionPerMatch * WordboundRaceDirector.InspectionHoldTicks);
        }

        [Test]
        public void DirectorCardsNeverTargetTheLapThreeFinish()
        {
            var leaderOnFinalLeg = WordboundRaceDirector.Plan(Race(), 80000, Total, new[]
            {
                new WordboundDirectorTeam(0, Total - 1, new long[] { 76000, 77000, 78000, 79000 }, false),
                new WordboundDirectorTeam(1, 20, new long[] { 70000, 72000, 74000, 76000 }, false)
            });
            Assert.IsTrue(leaderOnFinalLeg.Active);
            Assert.AreEqual(0, leaderOnFinalLeg.LeadTeam);
            Assert.AreEqual(0, leaderOnFinalLeg.InspectionPermille);
            Assert.AreEqual(50, leaderOnFinalLeg.ExpressPermille);
            var bothOnFinalLeg = WordboundRaceDirector.Plan(Race(), 80000, Total, new[]
            {
                new WordboundDirectorTeam(0, Total - 1, new long[] { 76000, 77000, 78000, 79000 }, false),
                new WordboundDirectorTeam(1, Total - 1, new long[] { 60000, 70000, 78000, 79500 }, false)
            });
            Assert.AreEqual(1, bothOnFinalLeg.TrailTeam);
            Assert.AreEqual(0, bothOnFinalLeg.ExpressPermille);
            Assert.AreEqual(0, bothOnFinalLeg.InspectionPermille);
        }

        [Test]
        public void PlanFeedsTheForecastOddsAndTargets()
        {
            var plan = WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Ahead(lead: 100), Behind(trail: 150) });
            Assert.AreEqual(150, plan.ExpressPermille);
            Assert.AreEqual(100, plan.InspectionPermille);
            var deck = Race().Ruleset.Deck;
            var stream = WordboundStream.ForStream(5UL, "story.deck");
            bool express = false, inspection = false;
            for (int draw = 0; draw < 200; draw++)
            {
                var next = WordboundEventDeck.Draw(deck, draw, WordboundCard.None, plan, ref stream);
                Assert.AreEqual(150, next.OddsPermille[WordboundEventDeck.Index(WordboundCard.ExpressChute)]);
                Assert.AreEqual(100, next.OddsPermille[WordboundEventDeck.Index(WordboundCard.InspectionDelay)]);
                if (next.Card == WordboundCard.ExpressChute) { express = true; Assert.AreEqual(1, next.TargetTeam); }
                if (next.Card == WordboundCard.InspectionDelay) { inspection = true; Assert.AreEqual(0, next.TargetTeam); }
            }
            Assert.IsTrue(express && inspection);
        }

        [Test]
        public void MalformedInputsAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new WordboundDirectorTeam(0, 5, new long[] { 1, 2, 3 }, false));
            Assert.Throws<ArgumentException>(() => new WordboundDirectorTeam(0, 2, new long[] { 5, 4 }, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WordboundDirectorTeam(0, 1, new long[] { 1 }, false, -1));
            Assert.Throws<ArgumentException>(() => WordboundRaceDirector.Plan(Race(), Midrace, Total, new[] { Ahead(), Ahead() }));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundRaceDirector.Eta(20000, Total, Limit, Ahead()));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundRaceDirector.Eta(Midrace, 0, Limit, Ahead()));
        }
    }
}
