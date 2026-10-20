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
    }
}
