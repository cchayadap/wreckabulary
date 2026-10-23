using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundSnapshotValidationTests
    {
        [TestCase("elapsed"), TestCase("future-progress"), TestCase("objectives"), TestCase("safe-checkpoint")]
        [TestCase("unknown-puzzle"), TestCase("premature-result"), TestCase("journal-epoch"), TestCase("resource-state")]
        [TestCase("solved-mechanism"), TestCase("missing-keepsake"), TestCase("unknown-story")]
        public void RehashedRaceStateStillRequiresAConsistentProgression(string mutation)
        {
            var f = WordboundSnapshotCodecTests.Create("TeamRace");
            string before = MatchSnapshotCodec.Export(f.Snapshot());
            string json = before;
            switch (mutation)
            {
                case "elapsed": json = json.Replace("\"elapsedTicks\":\"0\"", "\"elapsedTicks\":\"1\""); break;
                case "future-progress": json = json.Replace("\"progressTick\":\"0\"", "\"progressTick\":\"1\""); break;
                case "objectives": json = json.Replace("\"orderedObjectives\":0", "\"orderedObjectives\":999"); break;
                case "safe-checkpoint": json = json.Replace("\"safeCheckpoint\":\"Start\"", "\"safeCheckpoint\":\"Finish\""); break;
                case "unknown-puzzle": json = json.Replace("\"completedPuzzles\":[]", "\"completedPuzzles\":[\"Unknown\"]"); break;
                case "premature-result": json = json.Replace("\"result\":[]", "\"result\":[{\"confirmed\":false,\"outcome\":\"Win\",\"tick\":\"0\",\"winners\":[0]}]"); break;
                case "journal-epoch": json = json.Replace("\"commands\":{\"epoch\":\"0\"", "\"commands\":{\"epoch\":\"1\""); break;
                case "resource-state": json = Regex.Replace(json, "\"resources\":\\[[^]]*\\]", "\"resources\":[]"); break;
                case "solved-mechanism": json = json.Replace("\"solved\":false", "\"solved\":true"); break;
                case "missing-keepsake": json = json.Replace("\"kind\":\"Keepsake\"", "\"kind\":\"Platform\""); break;
                case "unknown-story": json = json.Replace("\"narrativeFlags\":[]", "\"narrativeFlags\":[\"story:0:2\"]"); break;
            }
            Assert.AreNotEqual(before, json, mutation);
            var result = f.Import(WordboundSnapshotCodecTests.Rehash(json));
            Assert.IsFalse(result.Success, mutation); Assert.IsNull(result.Snapshot); Assert.IsNotEmpty(result.Error);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.Snapshot()), "Failed import must not mutate live state.");
        }

        [TestCase("wrong-player-source"), TestCase("absent-team"), TestCase("outside-world"), TestCase("protected-without-source")]
        [TestCase("duplicate-body"), TestCase("missing-item-body"), TestCase("orphan-tile-body")]
        public void WorldStateMustMatchRosterInventoryAndOwnership(string mutation)
        {
            var f = WordboundSnapshotCodecTests.Create();
            switch (mutation)
            {
                case "wrong-player-source":
                    f.Bodies[0] = new WorldBodyState("player:0", WorldBodyKind.Player, default, ownerTeam: 0, sourceId: "b"); break;
                case "absent-team":
                    f.Bodies[0] = new WorldBodyState("player:0", WorldBodyKind.Player, default, ownerTeam: 7, sourceId: "a"); break;
                case "outside-world":
                    f.Bodies[0] = new WorldBodyState("player:0", WorldBodyKind.Player, new WorldVector(10001, 0, 0), ownerTeam: 0, sourceId: "a"); break;
                case "protected-without-source":
                    var item = f.Economy.PlaceFurniture("STOOL");
                    f.Bodies.Add(new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, default, protectedResource: true)); break;
                case "duplicate-body": f.Bodies.Add(f.Bodies[0]); break;
                case "missing-item-body": f.Economy.PlaceFurniture("STOOL"); break;
                case "orphan-tile-body": f.Bodies.Add(new WorldBodyState("tile:1", WorldBodyKind.Tile, default)); break;
            }
            var result = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsFalse(result.Success, mutation); Assert.IsNull(result.Snapshot); Assert.IsNotEmpty(result.Error);
        }

        [Test]
        public void CleanRaceLooseResourcesMustKeepTheirTeamOwnership()
        {
            var f = WordboundSnapshotCodecTests.Create("TeamRace");
            foreach (var tile in f.Economy.MintTiles("BED").Tiles)
                Assert.IsTrue(f.Economy.CollectTile(0, tile.TileId).Ok);
            Assert.IsTrue(f.Economy.BeginCraft(0, "BED", 0).Ok);
            var item = f.Economy.CompleteCraft(0, 100).Item;
            Assert.IsTrue(f.Economy.Drop(0, 0).Ok);
            f.Bodies.Add(new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, default));
            Assert.IsFalse(f.Import(MatchSnapshotCodec.Export(f.Snapshot())).Success);
            f.Bodies[f.Bodies.Count - 1] = new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, default, ownerTeam: 0);
            var result = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void FutureLapRecoveryScopesCannotBeOmittedFromACanonicalSnapshot()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            f.Resources.RemoveAll(r => r.Lap > 0);
            var result = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Snapshot);
        }

        [TestCase(false), TestCase(true)]
        public void LiveLettersCannotBeHiddenOrReferToAFutureLap(bool futureLap)
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            var source = f.Course.Supplies[0];
            var tile = f.Economy.MintTiles(source.Letters).Tiles[0];
            foreach (var spawned in f.Economy.Capture().Tiles)
                f.Bodies.Add(new WorldBodyState("tile:" + spawned.Key, WorldBodyKind.Tile, default,
                    active: futureLap || spawned.Key != tile.TileId, ownerTeam: source.TeamIndex,
                    protectedResource: source.Protected, sourceId: source.Id, recoveryGeneration: futureLap ? 1 : 0));
            var result = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Snapshot);
        }

        [Test]
        public void DisconnectedSeatRetainsItsAbsoluteReconnectDeadlineAcrossRestore()
        {
            var f = WordboundSnapshotCodecTests.Create();
            f.Progress.Disconnect("a", 9007199254740993L);
            f.Economy.Player(0).CanAct = false;
            var result = f.Import(MatchSnapshotCodec.Export(f.Snapshot()));
            Assert.IsTrue(result.Success, result.Error);
            long deadline = f.Progress.Player("a").ReconnectDeadlineMs;
            Assert.AreEqual(deadline, result.Snapshot.Progress.Players.Single(p => p.Id == "a").ReconnectDeadlineMs);
            var restored = new MatchProgress(f.Mode, f.Binding.Seats, MatchStateValidation.Route(f.Course));
            restored.Restore(result.Snapshot.Progress);
            Assert.IsFalse(restored.Reconnect("a", deadline));
        }

        [Test]
        public void DisconnectedOrMigratingInventoryCannotClaimActionPermission()
        {
            var f = WordboundSnapshotCodecTests.Create();
            f.Progress.Disconnect("a", 0);
            Assert.IsFalse(f.Import(MatchSnapshotCodec.Export(f.Snapshot())).Success);
            f.Economy.Player(0).CanAct = false;
            Assert.IsTrue(f.Import(MatchSnapshotCodec.Export(f.Snapshot())).Success);
            f.Progress.BeginMigration();
            Assert.IsFalse(f.Import(MatchSnapshotCodec.Export(f.Snapshot())).Success);
            f.Economy.Player(1).CanAct = false;
            Assert.IsTrue(f.Import(MatchSnapshotCodec.Export(f.Snapshot())).Success);
        }

        [Test]
        public void WrongExpectedContentBindingCannotImportAnOtherwiseValidSnapshot()
        {
            var f = WordboundSnapshotCodecTests.Create();
            var changed = new MatchBinding(f.Binding.MatchId, f.Binding.ProtocolVersion, f.Binding.ModeHash,
                f.Binding.CourseHash, f.Binding.ManifestHash, f.Binding.RosterHash, new string('c', 64), f.Binding.Seats);
            var result = MatchSnapshotCodec.Import(MatchSnapshotCodec.Export(f.Snapshot()), changed, f.Mode, f.Course, f.Catalogue, f.Rules);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ProgressRestoreRejectsForgedObjectivesWithoutPartiallyRestoring()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            var malformed = f.Progress.Capture();
            typeof(TeamProgress).GetProperty(nameof(TeamProgress.OrderedObjectives)).SetValue(malformed.Teams[0], 9);
            Assert.Throws<ArgumentException>(() => f.Progress.Restore(malformed));
            Assert.AreEqual(0, f.Progress.Team(0).OrderedObjectives);
            Assert.AreEqual(0, f.Progress.Tick);
        }

        [Test]
        public void ProgressRestoreAcceptsCheckpointProgressWithTheKeepsakeStillAtStart()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            var checkpoint = f.Course.Laps[0].Checkpoints[0];
            var observations = checkpoint.PuzzleIds.Select(id => new ProgressEvent(ProgressEventKind.PuzzleSatisfied, "a", id)).ToList();
            observations.Add(new ProgressEvent(ProgressEventKind.CheckpointReached, "a", checkpoint.Id));
            f.Progress.Advance(1, 16, observations);
            Assert.AreEqual(1, f.Progress.Team(0).NextCheckpoint);
            var restored = new MatchProgress(f.Mode, f.Binding.Seats, MatchStateValidation.Route(f.Course));
            Assert.DoesNotThrow(() => restored.Restore(f.Progress.Capture()));
            Assert.AreEqual("Start", restored.Team(0).KeepsakeLocation);
        }

        [Test]
        public void CompletedRaceResultRestoresOnlyWithTheExactDerivedWinner()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            Assert.IsTrue(f.Progress.TakeKeepsake("a"));
            for (int lap = 0; lap < 3; lap++)
            {
                foreach (var checkpoint in f.Course.Laps[lap].Checkpoints)
                {
                    var observations = checkpoint.PuzzleIds.Select(id => new ProgressEvent(ProgressEventKind.PuzzleSatisfied, "a", id, lap)).ToList();
                    observations.Add(new ProgressEvent(ProgressEventKind.CheckpointReached, "a", checkpoint.Id, lap));
                    f.Progress.Advance(f.Progress.Tick + 1, f.Progress.WallClockMs + 16, observations);
                }
                f.Progress.Advance(f.Progress.Tick + 1, f.Progress.WallClockMs + 16,
                    new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", f.Course.Laps[lap].FinishModuleId, lap) });
            }
            Assert.AreEqual("Win", f.Progress.Result.Outcome);
            var restored = new MatchProgress(f.Mode, f.Binding.Seats, MatchStateValidation.Route(f.Course));
            Assert.DoesNotThrow(() => restored.Restore(f.Progress.Capture()));
            Assert.AreEqual(f.Progress.Tick, restored.Result.Tick);
            CollectionAssert.AreEqual(new[] { 0 }, restored.Result.WinningTeams);
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void WarmCourseMetadataDoesNotCertifyAnotherSnapshot(string mode)
        {
            var f = WordboundSnapshotCodecTests.Create(mode);
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var valid = f.Snapshot();
            Assert.AreEqual("", Check(valid));
            var body = f.Bodies[0];
            f.Bodies.RemoveAt(0);
            var invalid = f.Snapshot();
            string error = Check(invalid);
            Assert.IsNotEmpty(error);
            Assert.AreEqual(error, Check(invalid));
            Assert.IsFalse(f.Import(MatchSnapshotCodec.Export(invalid)).Success);
            f.Bodies.Insert(0, body);
            Assert.AreEqual("", Check(f.Snapshot()));
            Assert.AreEqual("", Check(valid));
        }

        [TestCase("health"), TestCase("stamina"), TestCase("inventory")]
        public void WarmCourseMetadataStillReadsChangedRules(string field)
        {
            var f = WordboundSnapshotCodecTests.Create();
            var snapshot = f.Snapshot();
            string Check() => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            Assert.AreEqual("", Check());
            float health = f.Rules.MaxHealth, stamina = f.Rules.MaxStamina;
            int carried = f.Rules.MaxCarried;
            if (field == "health") f.Rules.MaxHealth = 99;
            else if (field == "stamina") f.Rules.MaxStamina = 99;
            else f.Rules.MaxCarried = carried + 1;
            Assert.IsNotEmpty(Check());
            f.Rules.MaxHealth = health; f.Rules.MaxStamina = stamina; f.Rules.MaxCarried = carried;
            Assert.AreEqual("", Check());
        }

        [TestCase(false), TestCase(true)]
        public void WarmCourseMetadataStillReadsChangedPendingRecipe(bool changeLetters)
        {
            var f = WordboundSnapshotCodecTests.Create();
            Wreckabulary.Rules.Tests.TestData.GiveLetters(f.Economy, 0, "BAT");
            Assert.IsTrue(f.Economy.BeginCraft(0, "BAT", 0).Ok);
            var snapshot = f.Snapshot();
            string Check() => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            Assert.AreEqual("", Check());
            var recipe = f.Catalogue.Get("BAT");
            var letters = recipe.Letters; bool enabled = recipe.Enabled;
            if (changeLetters) recipe.Letters = LetterBag.FromWord("BED");
            else recipe.Enabled = false;
            Assert.IsNotEmpty(Check());
            recipe.Letters = letters; recipe.Enabled = enabled;
            Assert.AreEqual("", Check());
        }

        [Test]
        public void PublicRouteAndDocumentCopiesCannotMutateWarmValidationMetadata()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            var snapshot = f.Snapshot();
            string Check() => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            Assert.AreEqual("", Check());
            var route = MatchStateValidation.Route(f.Course);
            route[0] = null;
            var document = f.Course.CopyDocument();
            document.Laps.Clear(); document.Supplies.Clear(); document.Puzzles.Clear();
            Assert.AreEqual("", Check());
            Assert.IsNotNull(MatchStateValidation.Route(f.Course)[0]);
        }

        [Test]
        public void ExactCourseAndModeChecksRemainLiveAfterMetadataWarmup()
        {
            var f = WordboundSnapshotCodecTests.Create("TeamRace");
            var snapshot = f.Snapshot();
            Assert.AreEqual("", MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules));
            var equivalentMode = ModeDefinition.For("TeamRace");
            Assert.AreEqual("", MatchStateValidation.Validate(snapshot, f.Binding, equivalentMode, f.Course, f.Catalogue, f.Rules));
            Assert.IsNotEmpty(MatchStateValidation.Validate(snapshot, f.Binding, ModeDefinition.For("BrawlRace"), f.Course, f.Catalogue, f.Rules));
            Assert.IsNotEmpty(MatchStateValidation.Validate(snapshot, f.Binding, ModeDefinition.For("TeamRace", RaceFormat.Duo), f.Course, f.Catalogue, f.Rules));
            var document = f.Course.CopyDocument(); document.Id = "DifferentCourse";
            var resolved = CourseResolver.Resolve(document, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(f.Mode)));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.IsNotEmpty(MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, resolved.Manifest, f.Catalogue, f.Rules));
            Assert.IsNotEmpty(MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, null, f.Catalogue, f.Rules));
            Assert.AreEqual("", MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules));
        }

        [Test]
        public void ConcurrentValidationPublishesOnlyCompleteStaticMetadata()
        {
            var f = WordboundSnapshotCodecTests.Create("TeamRace");
            var snapshot = f.Snapshot();
            var errors = new string[16];
            System.Threading.Tasks.Parallel.For(0, errors.Length, i =>
                errors[i] = MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules));
            Assert.IsTrue(errors.All(error => error == ""), string.Join("\n", errors));
        }
    }
}
