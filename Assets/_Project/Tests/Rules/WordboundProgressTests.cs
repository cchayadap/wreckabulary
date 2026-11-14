using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundProgressTests
    {
        static RaceLap[] Route() => Enumerable.Range(0, 3).Select(n =>
            new RaceLap("start", "finish", "lap" + n,
                new RaceCheckpoint("first", "puzzle"), new RaceCheckpoint("second"))).ToArray();

        static MatchProgress Race(string mode = "TeamRace", RaceFormat format = RaceFormat.Solo) =>
            new MatchProgress(ModeDefinition.For(mode, format),
                mode == "TimeRun" ? new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 0, 1) } :
                new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) }, Route());

        static ProgressEvent E(ProgressEventKind kind, string player, string target = "", int lap = 0) =>
            new ProgressEvent(kind, player, target, lap);

        static void Step(MatchProgress match, params ProgressEvent[] events) =>
            match.Advance(match.Tick + 1, match.WallClockMs + 16, events);

        static void IdleUntil(MatchProgress match, long tick)
        {
            while (match.Tick < tick && match.Result == null) Step(match);
        }

        static void FinishLap(MatchProgress match, string player, int lap)
        {
            Step(match, E(ProgressEventKind.PuzzleSatisfied, player, "puzzle", lap),
                E(ProgressEventKind.CheckpointReached, player, "first", lap));
            Step(match, E(ProgressEventKind.CheckpointReached, player, "second", lap),
                E(ProgressEventKind.FinishReached, player, "finish", lap));
        }

        [Test]
        public void CheckpointsAndPuzzlesCannotBeSkippedOrBorrowedFromAnotherTeam()
        {
            var match = Race();
            match.TakeKeepsake("a");
            Step(match, E(ProgressEventKind.CheckpointReached, "a", "second"),
                E(ProgressEventKind.FinishReached, "a", "finish"),
                E(ProgressEventKind.PuzzleSatisfied, "b", "puzzle"));
            Assert.AreEqual(0, match.Team(0).NextCheckpoint);
            Step(match, E(ProgressEventKind.CheckpointReached, "a", "first"));
            Assert.AreEqual(0, match.Team(0).NextCheckpoint);
            Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "puzzle"),
                E(ProgressEventKind.CheckpointReached, "a", "first"));
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
            Step(match, E(ProgressEventKind.CheckpointReached, "a", "second", 1));
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
        }

        [Test]
        public void KeepsakeCarrierCanFinishWithoutWaitingForOtherTeammates()
        {
            var match = Race("TimeRun");
            FinishLap(match, "a", 0);
            Assert.AreEqual(0, match.Team(0).Lap);
            Assert.IsTrue(match.TakeKeepsake("b"));
            Step(match, E(ProgressEventKind.FinishReached, "a", "finish"));
            Assert.AreEqual(0, match.Team(0).Lap);
            Step(match, E(ProgressEventKind.FinishReached, "b", "finish"));
            Assert.AreEqual(1, match.Team(0).Lap);
            FinishLap(match, "b", 1);
            FinishLap(match, "b", 2);
            Assert.AreEqual("Win", match.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, match.Result.WinningTeams);
            Assert.IsFalse(match.Result.Confirmed);
        }

        [Test]
        public void TeamsAdvanceIndependentlyAndSimultaneousFinishesDraw()
        {
            var match = Race();
            match.TakeKeepsake("a");
            match.TakeKeepsake("b");
            FinishLap(match, "a", 0);
            FinishLap(match, "a", 1);
            Assert.AreEqual(0, match.Team(1).Lap);
            FinishLap(match, "b", 0);
            FinishLap(match, "b", 1);
            Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "puzzle", 2),
                E(ProgressEventKind.PuzzleSatisfied, "b", "puzzle", 2),
                E(ProgressEventKind.CheckpointReached, "a", "first", 2),
                E(ProgressEventKind.CheckpointReached, "b", "first", 2));
            Step(match, E(ProgressEventKind.CheckpointReached, "a", "second", 2),
                E(ProgressEventKind.CheckpointReached, "b", "second", 2),
                E(ProgressEventKind.FinishReached, "b", "finish", 2),
                E(ProgressEventKind.FinishReached, "a", "finish", 2));
            Assert.AreEqual("Draw", match.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 1 }, match.Result.WinningTeams);
        }

        [Test]
        public void LostKeepsakeReturnsOnceToLatestSafeCheckpoint()
        {
            var match = Race();
            match.TakeKeepsake("a");
            Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "puzzle"),
                E(ProgressEventKind.CheckpointReached, "a", "first"));
            Assert.IsTrue(match.RecoverPlayer("a"));
            Assert.IsFalse(match.RecoverPlayer("a"));
            IdleUntil(match, 600);
            Assert.AreEqual(0, match.Team(0).RecoveryGeneration);
            Step(match);
            Assert.AreEqual("first", match.Team(0).KeepsakeLocation);
            Assert.AreEqual(1, match.Team(0).RecoveryGeneration);
            IdleUntil(match, 700);
            Assert.AreEqual(1, match.Team(0).RecoveryGeneration);
            Assert.AreEqual(1, match.Player("a").RecoveryGeneration);
            Assert.IsTrue(match.TakeKeepsake("a"));
        }

        [Test]
        public void PickingUpDroppedKeepsakeCancelsItsPendingRecovery()
        {
            var match = Race("TimeRun");
            match.TakeKeepsake("a");
            match.DropKeepsake("a", "first");
            Assert.IsTrue(match.TakeKeepsake("b"));
            IdleUntil(match, 601);
            Assert.AreEqual("b", match.Team(0).KeepsakeCarrier);
            Assert.AreEqual(0, match.Team(0).RecoveryGeneration);
        }

        [Test]
        public void ReconnectUsesOriginalAbsoluteDeadlineAcrossMigration()
        {
            var match = Race();
            match.TakeKeepsake("a");
            Assert.IsTrue(match.Disconnect("a", 1000));
            var snapshot = match.Capture();
            match.BeginMigration();
            match.Advance(1, 60000);
            Assert.AreEqual(0, match.ElapsedTicks);
            var replacement = Race();
            replacement.Restore(snapshot);
            replacement.CompleteMigration(121001);
            Assert.IsFalse(replacement.Reconnect("a", 121001));
            Assert.IsFalse(replacement.Reconnect("stranger", 121001));
            Assert.AreEqual(121000, replacement.Player("a").ReconnectDeadlineMs);
            var withinDeadline = Race();
            withinDeadline.Restore(snapshot);
            Assert.IsTrue(withinDeadline.Reconnect("a", 120999));
            var atDeadline = Race();
            atDeadline.Restore(snapshot);
            Assert.IsFalse(atDeadline.Reconnect("a", 121000));
        }

        [Test]
        public void InvalidBatchAndForeignSnapshotLeaveProgressUntouched()
        {
            var match = Race();
            Assert.Throws<ArgumentException>(() => match.Advance(1, 999, new ProgressEvent[] { null }));
            Assert.AreEqual(0, match.WallClockMs);
            Assert.AreEqual(0, match.Tick);
            var wrongMode = Race("BrawlRace");
            Assert.Throws<ArgumentException>(() => match.Restore(wrongMode.Capture()));
            match.TakeKeepsake("a");
            FinishLap(match, "a", 0);
            var snapshot = match.Capture();
            FinishLap(match, "a", 1);
            Assert.AreEqual(1, snapshot.Teams.Single(t => t.Team == 0).Lap);
            match.Restore(snapshot);
            Assert.AreEqual(1, match.Team(0).Lap);
        }

        [Test]
        public void RouteIdentityCannotCollideThroughLegalIdDelimiters()
        {
            var mode = ModeDefinition.For("TimeRun");
            var roster = new[] { new WordboundSeat("a", 0) };
            var first = Enumerable.Range(0, 3).Select(_ =>
                new RaceLap("start", "finish", "", new RaceCheckpoint("a:b", "c"))).ToArray();
            var second = Enumerable.Range(0, 3).Select(_ =>
                new RaceLap("start", "finish", "", new RaceCheckpoint("a", "b:c"))).ToArray();
            var original = new MatchProgress(mode, roster, first);
            var other = new MatchProgress(mode, roster, second);
            Assert.Throws<ArgumentException>(() => other.Restore(original.Capture()));
            Assert.AreEqual(0, other.Tick);
        }

        [Test]
        public void PreviousTickKnockoutCannotAttackButSameTickTradeIsAllowed()
        {
            var match = new MatchProgress(ModeDefinition.For("FreeForAll"),
                new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1), new WordboundSeat("c", 2) });
            IdleUntil(match, 120);
            Step(match, E(ProgressEventKind.Knockout, "a", "b"), E(ProgressEventKind.Knockout, "b", "a"));
            Assert.AreEqual(1, match.Player("a").Knockouts);
            Assert.AreEqual(1, match.Player("b").Knockouts);
            Step(match, E(ProgressEventKind.Knockout, "a", "c"));
            Assert.IsFalse(match.Player("c").Eliminated);
            Assert.AreEqual(1, match.Player("a").Knockouts);
        }

        [Test]
        public void CombatRejectsFriendlyFireProtectionAndDuplicateKnockouts()
        {
            var match = new MatchProgress(ModeDefinition.For("FreeForAll"),
                new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) });
            Step(match, E(ProgressEventKind.Knockout, "a", "b"));
            Assert.AreEqual(0, match.Player("a").Knockouts);
            IdleUntil(match, 120);
            Step(match, E(ProgressEventKind.Knockout, "a", "b"), E(ProgressEventKind.Knockout, "a", "b"));
            Assert.AreEqual(1, match.Player("a").Knockouts);
            var coop = Race("TimeRun");
            IdleUntil(coop, 120);
            Step(coop, E(ProgressEventKind.Knockout, "a", "b"));
            Assert.IsFalse(coop.Player("b").Eliminated);
        }

        [Test]
        public void TenKnockoutsWinAndEightMinuteEqualScoresDraw()
        {
            var roster = new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) };
            var match = new MatchProgress(ModeDefinition.For("FreeForAll"), roster);
            for (int i = 0; i < 10; i++)
            {
                IdleUntil(match, 120 + 241 * i);
                Step(match, E(ProgressEventKind.Knockout, "a", "b"));
            }
            Assert.AreEqual("Win", match.Result.Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, match.Result.WinningTeams);
            var tied = new MatchProgress(ModeDefinition.For("FreeForAll"), roster);
            IdleUntil(tied, 480 * 60);
            Assert.AreEqual("Draw", tied.Result.Outcome);
        }

        [Test]
        public void RaceTimeoutRanksOrderedProgressThenEarlierTick()
        {
            var match = Race();
            Step(match, E(ProgressEventKind.PuzzleSatisfied, "b", "puzzle"),
                E(ProgressEventKind.CheckpointReached, "b", "first"));
            Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "puzzle"),
                E(ProgressEventKind.CheckpointReached, "a", "first"));
            IdleUntil(match, 1500 * 60);
            CollectionAssert.AreEqual(new[] { 1 }, match.Result.WinningTeams);
            var coop = Race("TimeRun");
            IdleUntil(coop, 1500 * 60);
            Assert.AreEqual("TimeExpired", coop.Result.Outcome);
        }

        [Test]
        public void FinishOnTimeLimitTickWinsAndFreestyleNeverTimesOut()
        {
            var race = Race("TimeRun");
            race.TakeKeepsake("a");
            FinishLap(race, "a", 0);
            FinishLap(race, "a", 1);
            Step(race, E(ProgressEventKind.PuzzleSatisfied, "a", "puzzle", 2),
                E(ProgressEventKind.CheckpointReached, "a", "first", 2));
            IdleUntil(race, 1500 * 60 - 1);
            Step(race, E(ProgressEventKind.CheckpointReached, "a", "second", 2),
                E(ProgressEventKind.FinishReached, "a", "finish", 2));
            Assert.AreEqual("Win", race.Result.Outcome);
            var free = new MatchProgress(ModeDefinition.For("Freestyle"), new[] { new WordboundSeat("a", 0) });
            IdleUntil(free, 1500 * 60 + 1);
            Assert.IsNull(free.Result);
        }
    }
}
