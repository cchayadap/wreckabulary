using System;
using System.Collections.Generic;
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

        static WordboundAuthorityTests.Fixture PhysicalFixture(string mode = "TimeRun")
        {
            var source = new WordboundSnapshotCodecTests.Fixture
            { Mode = ModeDefinition.For(mode), Catalogue = Wreckabulary.Rules.Tests.TestData.Catalogue() };
            source.Rules = source.Mode.CreateGameplayRules(Wreckabulary.Rules.Tests.TestData.RulesFor("Dibs"));
            var content = WordboundFlagshipContent.Create(source.Catalogue);
            var resolved = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(source.Mode));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            source.Course = resolved.Manifest;
            var seats = Enumerable.Range(0, 2).Select(i => new WordboundSeat(((char)('a' + i)).ToString(),
                i % source.Mode.TeamCount, i / source.Mode.TeamCount)).ToArray();
            source.Binding = new MatchBinding("static-lookups", MatchSnapshotCodec.ProtocolVersion, source.Mode.Hash,
                source.Course.SourceHash, source.Course.Hash, new string('a', 64), new string('b', 64), seats);
            var result = new WordboundAuthorityTests.Fixture { Source = source };
            result.Match = new AuthoritativeMatch(source.Binding, source.Mode, source.Course,
                content.Geometry, source.Catalogue, source.Rules, result.World);
            return result;
        }

        static string CheckPhysical(WordboundAuthorityTests.Fixture f, MatchSnapshot snapshot) =>
            MatchStateValidation.Validate(snapshot, f.Source.Binding, f.Source.Mode, f.Source.Course,
                f.Source.Catalogue, f.Source.Rules);

        static MatchSnapshot ReplacePhysical(MatchSnapshot snapshot, IEnumerable<WorldBodyState> bodies = null,
            IEnumerable<MatchMechanismState> mechanisms = null, IEnumerable<MatchResourceState> resources = null) => new MatchSnapshot(snapshot.Binding, snapshot.Epoch,
                snapshot.Sequence, snapshot.NextWorldEntityId, snapshot.Progress, snapshot.Economy, snapshot.Commands,
                snapshot.Actors, bodies ?? snapshot.Bodies, resources ?? snapshot.Resources, mechanisms ?? snapshot.Mechanisms,
                snapshot.RandomStreams, snapshot.NarrativeFlags, snapshot.ThrownFlights, snapshot.Practice);

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("Freestyle")]
        public void IndexedPhysicalContentAcceptsRealFramesAndPublicPhysicalValidation(string mode)
        {
            var f = PhysicalFixture(mode);
            var before = f.State;
            Assert.AreEqual("", CheckPhysical(f, before));
            foreach (var mechanism in before.Mechanisms)
                Assert.DoesNotThrow(() => MatchStateValidationPhysical.Validate(before, f.Source.Course, mechanism));
            Assert.IsTrue(f.Step().Advanced);
            var after = f.State;
            Assert.Greater(after.Progress.Tick, before.Progress.Tick);
            Assert.AreEqual("", CheckPhysical(f, after));
            Assert.AreEqual("", CheckPhysical(f, before));
            Assert.IsTrue(f.Source.Import(MatchSnapshotCodec.Export(after)).Success);
        }

        [TestCase("source", "Unbound platform body.")]
        [TestCase("owner", "Unbound physical fixture body.")]
        [TestCase("pose", "Fixture pose differs from its canonical physical phase.")]
        [TestCase("missing", "An authored physical fixture is missing.")]
        [TestCase("connection", "Unbound platform body.")]
        public void WarmStaticLookupsStillRejectChangedFixtureAndConnectionBodies(string mutation, string expected)
        {
            var f = PhysicalFixture();
            var valid = f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            var fixture = valid.Bodies.First(b => b.Id.StartsWith("fixture:", StringComparison.Ordinal));
            var bodies = valid.Bodies.ToList();
            int at = bodies.IndexOf(fixture);
            if (mutation == "missing") bodies.RemoveAt(at);
            else if (mutation == "pose") bodies[at] = fixture.At(new WorldVector(0, 100, 0));
            else if (mutation == "connection")
                bodies[at] = new WorldBodyState("platform:foreign", WorldBodyKind.Platform, fixture.Position, kinematic: true);
            else bodies[at] = new WorldBodyState(fixture.Id, fixture.Kind, fixture.Position,
                kinematic: true, ownerTeam: mutation == "owner" ? -1 : fixture.OwnerTeam, protectedResource: true,
                sourceId: mutation == "source" ? "foreign" : fixture.SourceId);
            var invalid = ReplacePhysical(valid, bodies);
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [TestCase("hash", "Physical profile or motion phase is invalid.")]
        [TestCase("phase", "Physical profile or motion phase is invalid.")]
        [TestCase("unbound", "Mechanism has unbound physical evidence.")]
        [TestCase("actor", "Physical evidence has an invalid actor or milestone tick.")]
        public void IndexedAndPublicPhysicalChecksRetainTheSameEvidenceRejection(string mutation, string expected)
        {
            var f = PhysicalFixture();
            if (mutation == "actor") Assert.IsTrue(f.Step().Advanced);
            var valid = f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            var site = f.Source.Course.PhysicalSites.First(s => s.Fixtures.Count == 0);
            var original = mutation == "unbound" ? valid.Mechanisms.First(m => m.PuzzleId == site.PuzzleId && m.Lap != site.Lap - 1) :
                valid.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Lap == site.Lap - 1);
            var evidence = new MatchPhysicalEvidence(mutation == "actor" ? MatchPhysicalStage.Armed : MatchPhysicalStage.Waiting,
                actorId: mutation == "actor" ? "foreign" : "", stageTick: mutation == "actor" ? valid.Progress.Tick : 0,
                siteHash: mutation == "hash" || mutation == "unbound" ? new string('c', 64) : site.Hash,
                motionUnits: mutation == "phase" ? 1 : 0);
            var changed = new MatchMechanismState(original.PuzzleId, original.Team, original.Lap,
                activatedTick: mutation == "actor" ? valid.Progress.Tick : -1, physical: evidence);
            var invalid = ReplacePhysical(valid, mechanisms: valid.Mechanisms.Select(m => ReferenceEquals(m, original) ? changed : m));
            Assert.AreEqual(expected, Assert.Throws<ArgumentException>(() =>
                MatchStateValidationPhysical.Validate(invalid, f.Source.Course, changed)).Message);
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [TestCase("site", "physical.count"), TestCase("fixture", "physical.fixture")]
        [TestCase("puzzle", "id.duplicate"), TestCase("module", "id.duplicate"), TestCase("connection", "id.duplicate")]
        public void DuplicateStaticKeysStillFailBeforeCourseResolution(string collection, string code)
        {
            var content = WordboundFlagshipContent.Create(Wreckabulary.Rules.Tests.TestData.Catalogue());
            var document = content.CopyDocument();
            if (collection == "site") document.PhysicalSites.Add(document.PhysicalSites[0]);
            else if (collection == "fixture")
            {
                var site = document.PhysicalSites.First(s => s.Fixtures.Count != 0);
                site.Fixtures.Add(site.Fixtures[0]);
            }
            else if (collection == "puzzle") document.Puzzles.Add(document.Puzzles[0]);
            else if (collection == "module") document.Modules.Add(document.Modules[0]);
            else document.Connections.Add(document.Connections[0]);
            var result = CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"))));
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Issues.Any(issue => issue.Code == code), string.Join("\n", result.Issues));
        }

        [Test]
        public void PhysicalIndexesStayIsolatedFromDocumentCopiesAndConcurrentReaders()
        {
            var f = PhysicalFixture();
            var valid = f.State;
            var errors = new string[8];
            System.Threading.Tasks.Parallel.For(0, errors.Length, i => errors[i] = CheckPhysical(f, valid));
            Assert.IsTrue(errors.All(error => error == ""), string.Join("\n", errors));
            var copy = f.Source.Course.CopyDocument();
            copy.PhysicalSites.Clear(); copy.Modules.Clear(); copy.Puzzles.Clear(); copy.Connections.Clear();
            Assert.AreEqual("", CheckPhysical(f, valid));
            var other = PhysicalFixture();
            Assert.AreNotSame(f.Source.Course, other.Source.Course);
            Assert.AreEqual("", CheckPhysical(other, other.State));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [TestCase("missing"), TestCase("duplicate"), TestCase("team"), TestCase("lap")]
        [TestCase("generation"), TestCase("deadline"), TestCase("inactive-lap"), TestCase("source")]
        public void PerCallResourceIndexesRetainScopeAndRecoveryRejections(string mutation)
        {
            if (mutation == "lap")
            {
                var physical = PhysicalFixture("Freestyle");
                Assert.IsFalse(physical.Source.Mode.IsRace);
                var before = physical.State;
                Assert.Greater(before.Resources.Count, 0, "The non-race lap rejection needs actual authored recovery sources.");
                Assert.AreEqual("", CheckPhysical(physical, before));
                var changed = before.Resources.ToArray();
                var source = changed[0];
                changed[0] = new MatchResourceState(source.SourceId, source.Team, 1, source.Generation, source.RecoveryAtTick);
                var wrongLap = ReplacePhysical(before, resources: changed);
                const string rejected = "Recovery source, team, lap or deadline is invalid.";
                Assert.AreEqual(rejected, CheckPhysical(physical, wrongLap));
                Assert.AreEqual(rejected, CheckPhysical(physical, wrongLap));
                Assert.AreEqual("", CheckPhysical(physical, before));
                return;
            }
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var valid = f.Snapshot();
            Assert.Greater(f.Resources.Count, 0);
            Assert.AreEqual("", Check(valid));
            int at = mutation == "inactive-lap" ? f.Resources.FindIndex(r => r.Lap == 1) : 0;
            var original = f.Resources[at];
            if (mutation == "missing") f.Resources.RemoveAt(at);
            else if (mutation == "duplicate") f.Resources[1] = original;
            else f.Resources[at] = new MatchResourceState(mutation == "source" ? "foreign" : original.SourceId,
                mutation == "team" ? original.Team + 1 : original.Team, original.Lap,
                mutation == "generation" ? original.Generation + 1 : original.Generation,
                mutation == "deadline" ? 0 : mutation == "inactive-lap" ? 1 : original.RecoveryAtTick);
            string expected = mutation == "missing" || mutation == "duplicate" ?
                "Snapshot must include every static source, team and lap exactly once." : mutation == "inactive-lap" ?
                "Inactive lap resources cannot retain a pending recovery deadline." : "Recovery source, team, lap or deadline is invalid.";
            var invalid = f.Snapshot();
            Assert.AreEqual(expected, Check(invalid));
            Assert.AreEqual(expected, Check(invalid));
            Assert.AreEqual("", Check(valid));
        }

        [TestCase("excess"), TestCase("foreign-letter"), TestCase("missing-tile")]
        [TestCase("inactive-excess"), TestCase("other-team-excess"), TestCase("other-generation-excess")]
        public void PerSourceTileLookupStillChecksEveryMatchingBodyAndLetterMultiplicity(string mutation)
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var valid = f.Snapshot();
            Assert.AreEqual("", Check(valid));
            var source = f.Course.Supplies[0];
            if (mutation == "missing-tile")
                f.Bodies.Add(new WorldBodyState("tile:999999", WorldBodyKind.Tile, default,
                    ownerTeam: source.TeamIndex, protectedResource: true, sourceId: source.Id));
            else
            {
                string word = mutation == "foreign-letter" ? Enumerable.Range('A', 26).Select(i => (char)i)
                    .First(c => !source.Letters.Contains(c)).ToString() : source.Letters + source.Letters[0];
                foreach (var tile in f.Economy.MintTiles(word).Tiles)
                    f.Bodies.Add(new WorldBodyState("tile:" + tile.TileId, WorldBodyKind.Tile, default,
                        active: mutation != "inactive-excess", ownerTeam: mutation == "other-team-excess" ? 1 : source.TeamIndex,
                        protectedResource: true, sourceId: source.Id, recoveryGeneration: mutation == "other-generation-excess" ? 3 : 0));
            }
            var invalid = f.Snapshot();
            const string expected = "Recovery source contains more loose letters than its authored batch.";
            Assert.AreEqual(expected, Check(invalid));
            Assert.AreEqual(expected, Check(invalid));
            Assert.AreEqual("", Check(valid));
        }

        [TestCase("count"), TestCase("duplicate"), TestCase("resource")]
        public void ResourceLookupConstructionDoesNotReorderCompetingFailures(string first)
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var valid = f.Snapshot();
            Assert.AreEqual("", Check(valid));
            var originals = f.Resources.ToArray();
            var resource = originals[0];
            f.Resources[0] = new MatchResourceState("foreign", resource.Team, resource.Lap, resource.Generation);
            if (first == "count") f.Resources.RemoveAt(f.Resources.Count - 1);
            else if (first == "duplicate") f.Resources[1] = f.Resources[0];
            f.Bodies.Add(new WorldBodyState("tile:bad", WorldBodyKind.Tile, default,
                ownerTeam: resource.Team, protectedResource: true, sourceId: resource.SourceId));
            string expected = first == "resource" ? "Recovery source, team, lap or deadline is invalid." :
                "Snapshot must include every static source, team and lap exactly once.";
            Assert.AreEqual(expected, Check(f.Snapshot()));
            f.Resources.Clear(); f.Resources.AddRange(originals);
            Assert.AreEqual("World ID suffix is invalid.", Check(f.Snapshot()), "The later malformed tile must remain a real competing failure.");
            Assert.AreEqual("", Check(valid));
        }

        [Test]
        public void PerCallTileLookupAcceptsExactBatchThenCollectionWithoutChangingPriorSnapshot()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var source = f.Course.Supplies[0];
            var batch = f.Economy.MintTiles(source.Letters);
            foreach (var tile in batch.Tiles)
                f.Bodies.Add(new WorldBodyState("tile:" + tile.TileId, WorldBodyKind.Tile, default,
                    ownerTeam: source.TeamIndex, protectedResource: source.Protected, sourceId: source.Id));
            var fullBatch = f.Snapshot();
            Assert.AreEqual("", Check(fullBatch));
            int collected = batch.Tiles[0].TileId;
            Assert.IsTrue(f.Economy.CollectTile(0, collected).Ok);
            Assert.AreEqual(1, f.Bodies.RemoveAll(body => body.Id == "tile:" + collected));
            var afterCollection = f.Snapshot();
            Assert.AreEqual(fullBatch.Economy.Tiles.Count - 1, afterCollection.Economy.Tiles.Count);
            Assert.AreEqual("", Check(afterCollection));
            Assert.AreEqual("", Check(fullBatch));
            Assert.AreEqual("", Check(afterCollection));
        }

        [Test]
        public void EmptyLooseBatchesKeepResolvedLetterValidationAndCurrentProgress()
        {
            var f = WordboundSnapshotCodecTests.Create("TimeRun");
            string Check() => MatchStateValidation.Validate(f.Snapshot(), f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            Assert.AreEqual(0, f.Economy.Capture().Tiles.Count);
            Assert.AreEqual("", Check());
            var document = f.Course.CopyDocument();
            document.Supplies[0].Letters = "invalid";
            var rejected = CourseResolver.Resolve(document, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest(f.Course.Seed, MatchStateValidation.Requirements(f.Mode)));
            Assert.IsFalse(rejected.Success);
            Assert.IsTrue(rejected.Issues.Any(issue => issue.Code == "letters.invalid"));
            f.Progress.Advance(1, 16);
            Assert.AreEqual("", Check());
            Assert.AreEqual("", Check());
        }
    }
}
