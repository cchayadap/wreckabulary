using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPuzzleGuidePlanTests
    {
        WordboundFlagshipContent content;
        ResolvedCourse course;
        ModeDefinition mode;
        MatchProgress progress;

        internal static ItemCatalogue Items()
        {
            string copied = Path.Combine(TestContext.CurrentContext.TestDirectory, "GuideCatalogue.json");
            if (File.Exists(copied)) return ItemCatalogue.FromJson(File.ReadAllText(copied));
            return ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
                "Assets/_Project/Data/Config/items.json")));
        }

        [OneTimeSetUp]
        public void Content()
        {
            content = WordboundFlagshipContent.Create(Items());
            mode = ModeDefinition.For("TeamRace");
            var result = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
            course = result.Manifest;
        }

        [SetUp]
        public void Reset() => progress = Progress(course, mode);

        internal static MatchProgress Progress(ResolvedCourse course, ModeDefinition mode) =>
            new MatchProgress(mode, mode.Id == "TeamRace" ?
                new[] { new WordboundSeat("a", 0, 0), new WordboundSeat("b", 1, 0) } :
                new[] { new WordboundSeat("a", 0, 0) }, MatchStateValidation.Route(course));

        internal static void StepCheckpoint(MatchProgress progress, ResolvedCourse course, string actor = "a")
        {
            var team = progress.Team(progress.Player(actor).Team);
            var checkpoint = course.Laps[team.Lap].Checkpoints[team.NextCheckpoint];
            var events = checkpoint.PuzzleIds.Select(id =>
                new ProgressEvent(ProgressEventKind.PuzzleSatisfied, actor, id, team.Lap)).ToList();
            events.Add(new ProgressEvent(ProgressEventKind.CheckpointReached, actor, checkpoint.Id, team.Lap));
            progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17, events);
        }

        internal static void SeekFamily(MatchProgress progress, ResolvedCourse course, string family)
        {
            while (progress.Team(0).NextCheckpoint < course.Laps[0].Checkpoints.Count)
            {
                var checkpoint = course.Laps[0].Checkpoints[progress.Team(0).NextCheckpoint];
                if (checkpoint.PuzzleIds.Any(id => course.Puzzles.Single(p => p.Id == id).PresetId == family)) return;
                StepCheckpoint(progress, course);
            }
            Assert.Fail("Fixture has no selected checkpoint for " + family);
        }

        [TestCase("CollectLetters"), TestCase("PlaceObject"), TestCase("RedirectWind")]
        [TestCase("TimedMechanism"), TestCase("BuildRoute"), TestCase("BalanceWeight")]
        public void EachFamilyUsesExactAuthoredVolumesAndOnlyCurrentLocalTeamSites(string family)
        {
            SeekFamily(progress, course, family);
            var state = progress.Capture();
            var checkpoint = course.Laps[0].Checkpoints[progress.Team(0).NextCheckpoint];
            var site = course.PhysicalSites.Single(s => s.Lap == 1 && checkpoint.PuzzleIds.Contains(s.PuzzleId));
            var markers = WordboundPuzzleGuidePlan.Build(course, state, "a");
            var socket = markers.Single(m => m.Kind == WordboundGuideKind.Socket);
            Assert.AreEqual(site.Position, socket.Position); Assert.AreEqual(site.Yaw, socket.Yaw);
            Assert.AreEqual(site.SocketRadiusMm, socket.RadiusMm);
            Assert.IsTrue(socket.Label.Contains(course.Puzzles.Single(p => p.Id == site.PuzzleId).RecipeId));
            Assert.IsFalse(markers.Single(m => m.Kind == WordboundGuideKind.Checkpoint).Ready);
            if (family == "BuildRoute" || family == "RedirectWind" || family == "TimedMechanism")
                Assert.AreEqual(site.ArrivalBounds, markers.Single(m => m.Kind == WordboundGuideKind.Arrival).Bounds);
            if (family == "RedirectWind")
                Assert.AreEqual(site.ReceiverStartBounds, markers.Single(m => m.Kind == WordboundGuideKind.Receiver).Bounds);
            if (family == "BalanceWeight")
                CollectionAssert.AreEqual(site.FloorPadBounds, markers.Where(m => m.Kind == WordboundGuideKind.Pad).Select(m => m.Bounds));
            if (family == "TimedMechanism")
            {
                var path = markers.Single(m => m.Kind == WordboundGuideKind.ArmPath);
                Assert.AreEqual(site.Fixtures[0].From, path.Position); Assert.AreEqual(site.Fixtures[0].To, path.End);
                Assert.Greater(path.Bounds.Max.Y, path.Bounds.Min.Y);
            }
            if (family == "CollectLetters")
            {
                var sources = course.Placements.Where(p => p.ZoneId == site.PropSourceZoneId && p.TeamIndex == 0);
                CollectionAssert.AreEqual(sources.Select(p => p.Position), markers.Where(m => m.Kind == WordboundGuideKind.Source).Select(m => m.Position));
                Assert.IsNotEmpty(sources);
            }
            var other = WordboundPuzzleGuidePlan.Build(course, state, "b");
            Assert.AreEqual("checkpoint:" + course.Laps[0].Checkpoints[0].Id, other[0].Id);
            Assert.AreEqual(0, progress.Team(1).NextCheckpoint);
        }

        [Test]
        public void SolvedPuzzleDisappearsAndCheckpointBecomesReadyWithoutChangingProgress()
        {
            SeekFamily(progress, course, "PlaceObject");
            var checkpoint = course.Laps[0].Checkpoints[progress.Team(0).NextCheckpoint];
            progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17,
                checkpoint.PuzzleIds.Select(id => new ProgressEvent(ProgressEventKind.PuzzleSatisfied, "a", id)));
            Assert.IsTrue(progress.TakeKeepsake("a"));
            var before = progress.Capture();
            var markers = WordboundPuzzleGuidePlan.Build(course, before, "a");
            Assert.AreEqual(1, markers.Count); Assert.IsTrue(markers[0].Ready);
            Assert.AreEqual("CHECKPOINT\nBring keepsake", markers[0].Label);
            Assert.AreEqual(before.Tick, progress.Tick);
            CollectionAssert.AreEqual(before.Teams[0].CompletedPuzzles, progress.Team(0).CompletedPuzzles);
        }

        [Test]
        public void ReadyRingAsksTheTeamToPickUpTheKeepsakeUntilSomeoneCarriesIt()
        {
            var ring = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a")
                .Single(m => m.Kind == WordboundGuideKind.Checkpoint);
            Assert.IsEmpty(course.Laps[0].Checkpoints[0].PuzzleIds);
            Assert.AreEqual(WordboundPuzzleGuidePlan.PickUpKeepsake, ring.Label);
            Assert.IsFalse(ring.Ready);
            Assert.IsTrue(progress.TakeKeepsake("a"));
            ring = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a").Single(m => m.Kind == WordboundGuideKind.Checkpoint);
            Assert.AreEqual("CHECKPOINT\nBring keepsake", ring.Label);
            Assert.IsTrue(ring.Ready);
            Assert.AreEqual(WordboundPuzzleGuidePlan.PickUpKeepsake, WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "b")
                .Single(m => m.Kind == WordboundGuideKind.Checkpoint).Label, "Only the carrying team stops seeing the pickup prompt.");
            SeekFamily(progress, course, "PlaceObject");
            Assert.AreEqual("CHECKPOINT\nSolve puzzle first", WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a")
                .Single(m => m.Kind == WordboundGuideKind.Checkpoint).Label);
            Assert.IsTrue(progress.DropKeepsake("a"));
            while (progress.Team(0).NextCheckpoint < course.Laps[0].Checkpoints.Count) StepCheckpoint(progress, course);
            var finish = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a").Single();
            Assert.AreEqual(WordboundGuideKind.Finish, finish.Kind);
            Assert.AreEqual(WordboundPuzzleGuidePlan.PickUpKeepsake, finish.Label);
            Assert.IsTrue(progress.TakeKeepsake("a"));
            Assert.AreEqual("LAP FINISH\nBring keepsake", WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a").Single().Label);
        }

        [Test]
        public void FinishUsesTheActualLastCheckpointSensorThenNextLapMarkers()
        {
            while (progress.Team(0).NextCheckpoint < course.Laps[0].Checkpoints.Count) StepCheckpoint(progress, course);
            var finish = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a").Single();
            var last = course.Laps[0].Checkpoints.Last();
            Assert.AreEqual(WordboundGuideKind.Finish, finish.Kind);
            Assert.AreEqual(course.RecoveryAnchors.Single(a => a.Id == last.RecoveryAnchorId).Position, finish.Position);
            Assert.IsTrue(progress.TakeKeepsake("a"));
            progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17,
                new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", course.Laps[0].FinishModuleId) });
            Assert.AreEqual(1, progress.Team(0).Lap);
            Assert.AreEqual("checkpoint:" + course.Laps[1].Checkpoints[0].Id,
                WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a")[0].Id);
        }

        [Test]
        public void ReturnDisconnectMigrationAndCompletionRemoveGuidance()
        {
            Assert.IsNotEmpty(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
            progress.Disconnect("a", 1);
            Assert.IsEmpty(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
            Assert.IsTrue(progress.Reconnect("a", 2));
            progress.BeginMigration();
            Assert.IsEmpty(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
            progress.CompleteMigration(3);
            progress.Return("a", 4);
            Assert.IsEmpty(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
            Reset();
            for (int lap = 0; lap < 3; lap++)
            {
                while (progress.Team(0).NextCheckpoint < course.Laps[lap].Checkpoints.Count) StepCheckpoint(progress, course);
                progress.TakeKeepsake("a");
                progress.Advance(progress.Tick + 1, (progress.Tick + 1) * 17,
                    new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", course.Laps[lap].FinishModuleId, lap) });
            }
            Assert.IsNotNull(progress.Result);
            Assert.IsEmpty(WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a"));
        }

        [Test]
        public void UnknownPlayerIsRejectedAndFreestyleDoesNotInventRaceObjectives()
        {
            Assert.Throws<ArgumentException>(() => WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "outsider"));
            var free = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("Freestyle"))).Manifest;
            Assert.IsEmpty(WordboundPuzzleGuidePlan.Build(free, progress.Capture(), "a"));
        }

        [Test]
        public void PracticeCandidateAdvancesThroughColocatedLapSitesAsTheirReadModelCompletes()
        {
            var practice = ParcelPracticeCourse();
            var sites = practice.PhysicalSites.Where(s => s.PuzzleId == "SortingBench")
                .OrderBy(s => s.Hash, StringComparer.Ordinal).ToArray();
            Assert.AreEqual(3, sites.Length);
            Assert.IsTrue(sites.All(s => s.Position.Equals(sites[0].Position)));
            var position = Metres(sites[0].Position);
            for (int completed = 0; completed < sites.Length; completed++)
            {
                var frame = PracticeReadModel(practice, position, sites.Take(completed).Select(s => s.Hash).ToArray());
                var next = WordboundPracticeGuidePlan.Nearest(practice, frame, "a");
                Assert.AreEqual(sites[completed].Hash, next.Hash);
                Assert.IsFalse(WordboundPracticeGuidePlan.Completed(frame, next));
                Assert.AreNotEqual(frame.Practice.SiteHash, next.Hash);
                Assert.AreEqual("Practice this station", WordboundPracticeGuidePlan.Status(frame, next));
                Assert.IsTrue(WordboundPracticeGuidePlan.Build(practice, frame, "a")
                    .Any(m => m.Id == "practice:" + next.Hash && m.Ready));
            }
            var allHereDone = PracticeReadModel(practice, position, sites.Select(s => s.Hash).ToArray());
            var completedFallback = WordboundPracticeGuidePlan.Nearest(practice, allHereDone, "a");
            Assert.AreEqual(sites[0].Hash, completedFallback.Hash);
            Assert.IsTrue(WordboundPracticeGuidePlan.Completed(allHereDone, completedFallback));
            StringAssert.StartsWith("Completed this session", WordboundPracticeGuidePlan.Status(allHereDone, completedFallback));
        }

        [Test]
        public void PracticeSelectionStaysInRangeAndFallsBackOnlyWhenThatRangeIsComplete()
        {
            var practice = ParcelPracticeCourse();
            var sorting = practice.PhysicalSites.Where(s => s.PuzzleId == "SortingBench")
                .OrderBy(s => s.Hash, StringComparer.Ordinal).ToArray();
            var office = practice.PhysicalSites.Where(s => s.PuzzleId == "OfficeSofa")
                .OrderBy(s => s.Hash, StringComparer.Ordinal).ToArray();
            var position = Metres(sorting[0].Position);
            var frame = PracticeReadModel(practice, position, sorting.Select(s => s.Hash).ToArray());
            Assert.AreEqual(sorting[0].Hash, WordboundPracticeGuidePlan.Nearest(practice, frame, "a", 3).Hash);
            Assert.AreEqual(office[0].Hash, WordboundPracticeGuidePlan.Nearest(practice, frame, "a", 20).Hash);
            var allDone = PracticeReadModel(practice, position, practice.PhysicalSites.Select(s => s.Hash).ToArray());
            Assert.AreEqual(sorting[0].Hash, WordboundPracticeGuidePlan.Nearest(practice, allDone, "a", 20).Hash);
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice,
                PracticeReadModel(practice, new WorldVector(-3, 0, -3)), "a", .1f));
        }

        [Test]
        public void PracticeCandidateRetainsIdentityActorAndRangeGuards()
        {
            var practice = ParcelPracticeCourse(); var position = Metres(practice.PhysicalSites[0].Position);
            var frame = PracticeReadModel(practice, position);
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, frame, "outsider"));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, frame, "a", float.NaN));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, frame, "a", -1));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, frame, "a", 31));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, PracticeReadModel(practice, position, stale: true), "a"));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, PracticeReadModel(practice, position, active: false), "a"));
            Assert.IsNull(WordboundPracticeGuidePlan.Nearest(practice, PracticeReadModel(practice, position, disconnected: true), "a"));
        }

        static ResolvedCourse ParcelPracticeCourse()
        {
            var factory = WordboundParcelServiceContent.Create(Items());
            var result = factory.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("Freestyle")));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues)); return result.Manifest;
        }

        static WorldVector Metres(CoursePoint position) => new WorldVector(position.X / 1000f, position.Y / 1000f, position.Z / 1000f);

        static MatchSnapshot PracticeReadModel(ResolvedCourse practice, WorldVector position, string[] completed = null,
            bool stale = false, bool active = true, bool disconnected = false)
        {
            completed ??= Array.Empty<string>();
            var practiceMode = ModeDefinition.For("Freestyle"); var seats = new[] { new WordboundSeat("a", 0, 0) };
            var state = Progress(practice, practiceMode); if (disconnected) state.Disconnect("a", 1);
            var binding = new MatchBinding("practice-guide", MatchSnapshotCodec.ProtocolVersion, practiceMode.Hash,
                stale ? new string('c', 64) : practice.SourceHash, practice.Hash, new string('a', 64), new string('b', 64), seats);
            return new MatchSnapshot(binding, 0, 1, 1, state.Capture(), new Economy(Items(), new GameRules()).Capture(),
                new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(), Array.Empty<MatchActorState>(),
                new[] { new WorldBodyState("player:0", WorldBodyKind.Player, position, sourceId: "a", active: active) },
                Array.Empty<MatchResourceState>(), practice.PhysicalSites.Select(s =>
                    new MatchMechanismState(s.PuzzleId, 0, s.Lap - 1, completed.Contains(s.Hash))),
                Array.Empty<MatchRandomState>(), Array.Empty<string>());
        }

        [Test]
        public void SnapshotOverloadRejectsDifferentManifestSourceAndRoster()
        {
            var seats = new[] { new WordboundSeat("a", 0, 0), new WordboundSeat("b", 1, 0) };
            MatchSnapshot Frame(string source, string manifest, WorldBodyState[] bodies = null)
            {
                var binding = new MatchBinding("guide", MatchSnapshotCodec.ProtocolVersion, mode.Hash, source, manifest,
                    new string('a', 64), new string('b', 64), seats);
                return new MatchSnapshot(binding, 0, 1, 1, progress.Capture(),
                    new Economy(Items(), new GameRules()).Capture(),
                    new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(),
                    Array.Empty<MatchActorState>(), bodies ?? Array.Empty<WorldBodyState>(), Array.Empty<MatchResourceState>(),
                    Array.Empty<MatchMechanismState>(), Array.Empty<MatchRandomState>(), Array.Empty<string>());
            }
            Assert.IsNotEmpty(WordboundPuzzleGuidePlan.Build(course, Frame(course.SourceHash, course.Hash), "a"));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleGuidePlan.Build(course, Frame(new string('c', 64), course.Hash), "a"));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleGuidePlan.Build(course, Frame(course.SourceHash, new string('c', 64)), "a"));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleGuidePlan.Build(course, Frame(course.SourceHash, course.Hash), "outsider"));
            SeekFamily(progress, course, "CollectLetters");
            var sourceMarker = WordboundPuzzleGuidePlan.Build(course, progress.Capture(), "a")
                .First(m => m.Kind == WordboundGuideKind.Source);
            Assert.IsFalse(WordboundPuzzleGuidePlan.Build(course, Frame(course.SourceHash, course.Hash), "a")
                .Any(m => m.Kind == WordboundGuideKind.Source));
            var live = new WorldBodyState("prop", WorldBodyKind.Item, default, sourceId: sourceMarker.SourceId);
            Assert.AreEqual(sourceMarker.SourceId, WordboundPuzzleGuidePlan.Build(course,
                Frame(course.SourceHash, course.Hash, new[] { live }), "a").Single(m => m.Kind == WordboundGuideKind.Source).SourceId);
            Assert.IsFalse(WordboundPuzzleGuidePlan.Build(course,
                Frame(course.SourceHash, course.Hash, new[] { live.At(default, active: false) }), "a")
                .Any(m => m.Kind == WordboundGuideKind.Source));
        }
    }
}
