using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundStoryPlanTests
    {
        WordboundFlagshipContent content;
        ResolvedCourse course;
        ModeDefinition mode;
        WordboundStoryPlan plan;
        MatchProgress progress;

        [OneTimeSetUp]
        public void Content()
        {
            var items = WordboundPuzzleGuidePlanTests.Items();
            content = WordboundFlagshipContent.Create(items);
            mode = ModeDefinition.For("TeamRace");
            var result = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(result.Success);
            course = result.Manifest;
            plan = new WordboundStoryPlan(course, items);
        }

        [SetUp]
        public void Reset() => progress = WordboundPuzzleGuidePlanTests.Progress(course, mode);

        [Test]
        public void NoticesComeOnlyFromTheSelectedRouteAndExactAuthoredPositions()
        {
            Assert.IsNotEmpty(plan.Notices);
            foreach (var notice in plan.Notices)
            {
                var lap = course.Laps[notice.Lap - 1];
                var checkpoint = lap.Checkpoints[notice.Checkpoint];
                Assert.AreEqual(course.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId).Position,
                    notice.Position);
                Assert.AreEqual(content.Story.Single(b => b.Lap == notice.Lap && b.CheckpointId == checkpoint.Id).Clue,
                    notice.Clue);
            }
            var unselected = content.Story.Where(b => !course.Laps[b.Lap - 1].Checkpoints.Any(c => c.Id == b.CheckpointId));
            foreach (var beat in unselected)
                Assert.IsFalse(plan.Notices.Any(n => n.Id == beat.Lap + ":" + beat.CheckpointId));
        }

        [Test]
        public void JournalNeedsBothReachedCheckpointAndItsOwnTeamNarrativeFlag()
        {
            string[] flags = { MatchStateValidation.NarrativeFlag(0, 0), MatchStateValidation.NarrativeFlag(1, 0) };
            Assert.IsEmpty(plan.Journal(progress.Capture(), flags, "a"));
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            var entries = plan.Journal(progress.Capture(), flags, "a");
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(1, entries[0].Lap);
            Assert.AreEqual(0, entries[0].Checkpoint);
            Assert.IsEmpty(plan.Journal(progress.Capture(), flags, "b"));
            Assert.IsEmpty(plan.Journal(progress.Capture(), new[] { flags[1] }, "a"));
        }

        [Test]
        public void FutureFlagsCannotRevealUnreachedCluesOrAnotherTeamProgress()
        {
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            string[] flags = Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray();
            var entries = plan.Journal(progress.Capture(), flags, "a");
            Assert.AreEqual(1, entries.Count);
            Assert.IsTrue(entries.All(n => n.Lap == 1));
            Assert.IsEmpty(plan.Journal(progress.Capture(), flags, "b"));
        }

        [Test]
        public void RestoredProgressReconstructsTheSameJournalWithoutMutatingProgress()
        {
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            var saved = progress.Capture();
            var flags = new[] { MatchStateValidation.NarrativeFlag(0, 0) };
            var first = plan.Journal(saved, flags, "a").Select(n => n.Id).ToArray();
            var recreated = new WordboundStoryPlan(course, WordboundPuzzleGuidePlanTests.Items());
            CollectionAssert.AreEqual(first, recreated.Journal(saved, flags, "a").Select(n => n.Id));
            Assert.AreEqual(saved.Tick, progress.Tick);
            Assert.AreEqual(1, progress.Team(0).NextCheckpoint);
        }

        [Test]
        public void UnknownReaderIsRejected()
            => Assert.Throws<ArgumentException>(() => plan.Journal(progress.Capture(), Array.Empty<string>(), "outsider"));

        [Test]
        public void ContextualCueNeedsReachedOwnTeamProgressAndShowsOnlyLatestClue()
        {
            string[] flags = Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray();
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            var first = plan.ContextualCue(progress.Capture(), flags, "a");
            Assert.AreEqual(plan.Journal(progress.Capture(), flags, "a").Single().Id, first.Id);
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "b"));
            Assert.IsNull(plan.ContextualCue(progress.Capture(), Array.Empty<string>(), "a"));
            while (plan.Journal(progress.Capture(), flags, "a").Count < 2)
                WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            var latest = plan.ContextualCue(progress.Capture(), flags, "a");
            Assert.AreEqual(plan.Journal(progress.Capture(), flags, "a").Last().Id, latest.Id);
            Assert.AreNotEqual(first.Id, latest.Id);
            Assert.AreEqual(2, plan.Journal(progress.Capture(), flags, "a").Count);
        }

        [Test]
        public void LapTransitionKeepsJournalButRemovesPreviousChapterCueUntilNextClueIsEarned()
        {
            string[] flags = Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray();
            while (progress.Team(0).NextCheckpoint < course.Laps[0].Checkpoints.Count)
                WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            Assert.IsNotNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            Assert.IsTrue(progress.TakeKeepsake("a"));
            progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17,
                new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", course.Laps[0].FinishModuleId) });
            Assert.AreEqual(1, progress.Team(0).Lap);
            Assert.IsNotEmpty(plan.Journal(progress.Capture(), flags, "a"));
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            Assert.AreEqual(2, plan.ContextualCue(progress.Capture(), flags, "a").Lap);
        }

        [Test]
        public void DisconnectMigrationAndReturnHideWorldCueWithoutErasingJournal()
        {
            var flags = new[] { MatchStateValidation.NarrativeFlag(0, 0) };
            WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
            string clue = plan.ContextualCue(progress.Capture(), flags, "a").Id;
            long clock = progress.Capture().WallClockMs;
            Assert.IsTrue(progress.Disconnect("a", clock + 1));
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            Assert.AreEqual(clue, plan.Journal(progress.Capture(), flags, "a").Single().Id);
            Assert.IsTrue(progress.Reconnect("a", clock + 2));
            Assert.AreEqual(clue, plan.ContextualCue(progress.Capture(), flags, "a").Id);
            progress.BeginMigration();
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            progress.CompleteMigration(clock + 3);
            Assert.AreEqual(clue, plan.ContextualCue(progress.Capture(), flags, "a").Id);
            progress.Return("a", clock + 4);
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            Assert.AreEqual(clue, plan.Journal(progress.Capture(), flags, "a").Single().Id);
        }

        [Test]
        public void ConfirmedCompletionHidesWorldCueAndRetainsAllEarnedJournalEntries()
        {
            string[] flags = Enumerable.Range(0, 3).Select(l => MatchStateValidation.NarrativeFlag(0, l)).ToArray();
            for (int lap = 0; lap < 3; lap++)
            {
                while (progress.Team(0).NextCheckpoint < course.Laps[lap].Checkpoints.Count)
                    WordboundPuzzleGuidePlanTests.StepCheckpoint(progress, course);
                progress.TakeKeepsake("a");
                progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17,
                    new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", course.Laps[lap].FinishModuleId, lap) });
            }
            Assert.IsNotNull(progress.Result);
            Assert.IsNull(plan.ContextualCue(progress.Capture(), flags, "a"));
            CollectionAssert.AreEqual(plan.Notices.Select(n => n.Id), plan.Journal(progress.Capture(), flags, "a").Select(n => n.Id));
        }

        [Test]
        public void ContextualSnapshotRejectsAFrameFromAnotherExactCourse()
        {
            var seats = new[] { new WordboundSeat("a", 0, 0), new WordboundSeat("b", 1, 0) };
            MatchSnapshot Frame(string source, string manifest)
            {
                var binding = new MatchBinding("story", MatchSnapshotCodec.ProtocolVersion, mode.Hash, source, manifest,
                    new string('a', 64), new string('b', 64), seats);
                return new MatchSnapshot(binding, 0, 1, 1, progress.Capture(),
                    new Wreckabulary.Rules.Economy(WordboundPuzzleGuidePlanTests.Items(), new Wreckabulary.Rules.GameRules()).Capture(),
                    new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(),
                    Array.Empty<MatchActorState>(), Array.Empty<WorldBodyState>(), Array.Empty<MatchResourceState>(),
                    Array.Empty<MatchMechanismState>(), Array.Empty<MatchRandomState>(), Array.Empty<string>());
            }
            Assert.Throws<ArgumentException>(() => plan.ContextualCue(Frame(new string('c', 64), course.Hash), "a"));
            Assert.Throws<ArgumentException>(() => plan.ContextualCue(Frame(course.SourceHash, new string('c', 64)), "a"));
            Assert.Throws<ArgumentException>(() => plan.ContextualCue(progress.Capture(), Array.Empty<string>(), "outsider"));
        }
    }
}
