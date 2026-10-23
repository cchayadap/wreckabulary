using System;
using System.Collections.Generic;
using System.Globalization;
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

        static ResolvedCoursePhysicalSite FixtureSite(WordboundAuthorityTests.Fixture f, WorldBodyState body) =>
            f.Source.Course.PhysicalSites.Single(site => site.Fixtures.Any(fixture => fixture.Id == body.SourceId));

        static MatchMechanismState FixtureMechanism(WordboundAuthorityTests.Fixture f, MatchSnapshot snapshot, WorldBodyState body)
        {
            var site = FixtureSite(f, body);
            return snapshot.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Lap == site.Lap - 1 && m.Team == body.OwnerTeam);
        }

        [TestCase("en-US"), TestCase("fr-FR"), TestCase("ar-SA")]
        public void MechanismUniquenessRetainsInvariantIdentityAcrossCultures(string cultureName)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var f = WordboundSnapshotCodecTests.Create("TimeRun");
                string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
                var valid = f.Snapshot();
                Assert.AreEqual("", Check(valid));
                Assert.GreaterOrEqual(f.Mechanisms.Count, 2);
                string retained = MatchSnapshotCodec.Export(valid);
                var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
                culture.NumberFormat.NativeDigits = new[] { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
                CultureInfo.CurrentCulture = culture;

                void Pair(MatchMechanismState first, MatchMechanismState second)
                {
                    for (int i = 0; i < f.Mechanisms.Count; i++)
                        f.Mechanisms[i] = new MatchMechanismState("probe:padding:" + i.ToString(CultureInfo.InvariantCulture), 0, 0);
                    f.Mechanisms[0] = first; f.Mechanisms[1] = second;
                    string Key(MatchMechanismState row) => row.PuzzleId + "/" + row.Team.ToString(CultureInfo.InvariantCulture) +
                        "/" + row.Lap.ToString(CultureInfo.InvariantCulture);
                    bool duplicate = f.Mechanisms.Select(Key).Distinct(StringComparer.Ordinal).Count() != f.Mechanisms.Count;
                    string expected = duplicate ? "Duplicate mechanism state." : "Mechanism has an unknown puzzle, team or lap.";
                    var changed = f.Snapshot();
                    Assert.AreEqual(expected, Check(changed), Key(first) + " versus " + Key(second));
                    Assert.AreEqual(expected, Check(changed));
                }

                for (int team = 0; team < 8; team++)
                    for (int lap = 0; lap < 3; lap++)
                    {
                        var first = new MatchMechanismState("probe", team, lap);
                        Pair(first, new MatchMechanismState("probe", team, lap, phaseTick: 1));
                        Pair(first, new MatchMechanismState("probe", (team + 1) % 8, lap));
                        Pair(first, new MatchMechanismState("probe", team, (lap + 1) % 3));
                    }
                foreach (string id in new[] { "Probe", "probe-", "probe.", "probe:", "probe_", "probe0" })
                    Pair(new MatchMechanismState("probe", 0, 0), new MatchMechanismState(id, 0, 0));
                Assert.AreEqual("", Check(valid));
                Assert.AreEqual(retained, MatchSnapshotCodec.Export(valid));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void MechanismIdentityIgnoresDifferentEvidenceWithoutMovingItsRejection(string mode)
        {
            var f = PhysicalFixture(mode);
            var valid = f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            string retained = MatchSnapshotCodec.Export(valid);
            var site = f.Source.Course.PhysicalSites.First(value => value.Fixtures.Count == 0);
            var row = valid.Mechanisms.First(value => value.PuzzleId == site.PuzzleId && value.Lap == site.Lap - 1);
            Assert.IsFalse(row.Solved);
            Assert.AreEqual(-1, row.ActivatedTick);
            var changed = new MatchMechanismState(row.PuzzleId, row.Team, row.Lap,
                phaseTick: 1, physical: row.Physical);
            var laterFailure = ReplacePhysical(valid, mechanisms: valid.Mechanisms.Select(value => ReferenceEquals(value, row) ? changed : value));
            Assert.AreEqual("Mechanism completion or timing disagrees with ordered progress.", CheckPhysical(f, laterFailure));
            var duplicate = ReplacePhysical(valid, mechanisms: valid.Mechanisms.Concat(new[] { changed }));
            Assert.AreEqual("Duplicate mechanism state.", CheckPhysical(f, duplicate));
            Assert.AreEqual("Duplicate mechanism state.", CheckPhysical(f, duplicate));
            Assert.AreEqual("Snapshot must include every static source, team and lap exactly once.",
                CheckPhysical(f, ReplacePhysical(duplicate, resources: Array.Empty<MatchResourceState>())));
            Assert.AreEqual("", CheckPhysical(f, valid));
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(valid));
        }

        [Test]
        public void MechanismDuplicateStillPrecedesTheNonPuzzleModeRejection()
        {
            var f = WordboundSnapshotCodecTests.Create("FreeForAll");
            string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
            var valid = f.Snapshot();
            Assert.AreEqual("", Check(valid));
            Assert.IsEmpty(valid.Mechanisms);
            string retained = MatchSnapshotCodec.Export(valid);
            var row = new MatchMechanismState("probe", 0, 0);
            Assert.AreEqual("This mode has no puzzle mechanisms.", Check(ReplacePhysical(valid, mechanisms: new[] { row })));
            var duplicate = ReplacePhysical(valid, mechanisms: new[] { row, new MatchMechanismState("probe", 0, 0, phaseTick: 1) });
            Assert.AreEqual("Duplicate mechanism state.", Check(duplicate));
            Assert.AreEqual("Duplicate mechanism state.", Check(duplicate));
            Assert.AreEqual("", Check(valid));
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(valid));
        }

        [TestCase("missing"), TestCase("referenced-duplicate"), TestCase("unreferenced-duplicate")]
        public void FixtureMechanismLookupRetainsQueryLocalMissingAndDuplicateRejections(string mutation)
        {
            var f = PhysicalFixture("TeamRace");
            var valid = f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            string before = MatchSnapshotCodec.Export(valid);
            var fixture = valid.Bodies.First(b => b.Id.StartsWith("fixture:", StringComparison.Ordinal));
            var selected = FixtureMechanism(f, valid, fixture);
            if (mutation == "unreferenced-duplicate")
            {
                var site = f.Source.Course.PhysicalSites.First(s => s.Fixtures.Count == 0);
                selected = valid.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Lap == site.Lap - 1 && m.Team == fixture.OwnerTeam);
            }
            var mechanisms = valid.Mechanisms.ToList();
            if (mutation == "missing") mechanisms.Remove(selected);
            else mechanisms.Add(selected);
            var invalid = ReplacePhysical(valid, mechanisms: mechanisms);
            string expected = mutation == "missing" ? "Fixture pose differs from its canonical physical phase." :
                mutation == "unreferenced-duplicate" ? "Duplicate mechanism state." :
                Assert.Throws<InvalidOperationException>(() => invalid.Mechanisms.SingleOrDefault(m =>
                    m.PuzzleId == selected.PuzzleId && m.Lap == selected.Lap && m.Team == selected.Team)).Message;
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(valid));
        }

        [TestCase("identity"), TestCase("bounds"), TestCase("earlier-pose"), TestCase("resources")]
        public void FixtureMechanismLookupDoesNotMoveDuplicateRejectionAheadOfEarlierErrors(string mutation)
        {
            var f = PhysicalFixture("TeamRace");
            var valid = f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            var fixtures = valid.Bodies.Where(b => b.Id.StartsWith("fixture:", StringComparison.Ordinal)).ToArray();
            var first = fixtures[0];
            var target = FixtureMechanism(f, valid, first);
            if (mutation == "earlier-pose")
                target = fixtures.Select(body => FixtureMechanism(f, valid, body)).First(m =>
                    m.PuzzleId != target.PuzzleId || m.Lap != target.Lap || m.Team != target.Team);
            var changed = mutation == "identity" ? first.At(first.Position, kinematic: false) :
                mutation == "bounds" ? first.At(new WorldVector(10001, first.Position.Y, first.Position.Z)) :
                first.At(new WorldVector(first.Position.X + .1f, first.Position.Y, first.Position.Z));
            var invalid = ReplacePhysical(valid,
                bodies: valid.Bodies.Select(body => ReferenceEquals(body, first) ? changed : body),
                mechanisms: valid.Mechanisms.Concat(new[] { target }),
                resources: mutation == "resources" ? Array.Empty<MatchResourceState>() : null);
            string expected = mutation == "identity" ? "Unbound physical fixture body." :
                mutation == "bounds" ? "World body exceeds physical bounds or names an absent team." :
                mutation == "resources" ? "Snapshot must include every static source, team and lap exactly once." :
                "Fixture pose differs from its canonical physical phase.";
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("Freestyle")]
        public void FixtureMechanismLookupUsesEachSnapshotsExactMovingPhase(string mode)
        {
            var f = PhysicalFixture(mode);
            var before = f.State;
            string retained = MatchSnapshotCodec.Export(before);
            Assert.AreEqual("", CheckPhysical(f, before));
            Assert.IsTrue(f.Step().Advanced);
            var advanced = f.State;
            Assert.AreEqual("", CheckPhysical(f, advanced));
            var body = advanced.Bodies.First(b => b.Id.StartsWith("fixture:", StringComparison.Ordinal));
            var site = FixtureSite(f, body);
            var mechanism = FixtureMechanism(f, advanced, body);
            var moved = new MatchMechanismState(mechanism.PuzzleId, mechanism.Team, mechanism.Lap,
                mechanism.Solved, mechanism.ActivatedTick, mechanism.PhaseTick, mechanism.ProgressTicks,
                mechanism.ItemEntityId, mechanism.Physical.WithMotion(CourseFixtureMotion.UnitsPerTick));
            var moving = ReplacePhysical(advanced,
                bodies: advanced.Bodies.Select(current => {
                    var fixture = site.Fixtures.SingleOrDefault(value => value.Id == current.SourceId);
                    return fixture != null && current.OwnerTeam == body.OwnerTeam ?
                        current.At(CourseFixtureMotion.Position(fixture, moved.Physical.MotionUnits)) : current;
                }),
                mechanisms: advanced.Mechanisms.Select(current => ReferenceEquals(current, mechanism) ? moved : current));
            var oldBody = before.Bodies.Single(current => current.Id == body.Id);
            var movedBody = moving.Bodies.Single(current => current.Id == body.Id);
            Assert.GreaterOrEqual(movedBody.Position.DistanceSquared(oldBody.Position), .000001f);
            string movingBefore = MatchSnapshotCodec.Export(moving);
            Assert.AreEqual("", CheckPhysical(f, moving));
            var stalePose = ReplacePhysical(moving, bodies: moving.Bodies.Select(current =>
                ReferenceEquals(current, movedBody) ? current.At(oldBody.Position) : current));
            Assert.AreEqual("Fixture pose differs from its canonical physical phase.", CheckPhysical(f, stalePose));
            Assert.AreEqual("", CheckPhysical(f, moving));
            Assert.AreEqual("", CheckPhysical(f, advanced));
            Assert.AreEqual("", CheckPhysical(f, before));
            Assert.AreEqual(movingBefore, MatchSnapshotCodec.Export(moving));
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(before));
        }

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

        [TestCase("en-US"), TestCase("fr-FR"), TestCase("ar-SA")]
        public void ResourceUniquenessRetainsInvariantIdentityAcrossCultures(string cultureName)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                var f = WordboundSnapshotCodecTests.Create("TimeRun");
                string Check(MatchSnapshot snapshot) => MatchStateValidation.Validate(snapshot, f.Binding, f.Mode, f.Course, f.Catalogue, f.Rules);
                var valid = f.Snapshot();
                Assert.AreEqual("", Check(valid));
                Assert.GreaterOrEqual(f.Resources.Count, 2);
                string retained = MatchSnapshotCodec.Export(valid);
                var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
                culture.NumberFormat.NegativeSign = "";
                culture.NumberFormat.NativeDigits = new[] { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
                CultureInfo.CurrentCulture = culture;

                void Pair(MatchResourceState first, MatchResourceState second)
                {
                    for (int i = 0; i < f.Resources.Count; i++)
                        f.Resources[i] = new MatchResourceState("probe:padding:" + i.ToString(CultureInfo.InvariantCulture), 0, 0);
                    f.Resources[0] = first; f.Resources[1] = second;
                    string Key(MatchResourceState row) => row.SourceId + "/" + row.Team.ToString(CultureInfo.InvariantCulture) +
                        "/" + row.Lap.ToString(CultureInfo.InvariantCulture);
                    bool duplicate = f.Resources.Select(Key).Distinct(StringComparer.Ordinal).Count() != f.Resources.Count;
                    string expected = duplicate ? "Snapshot must include every static source, team and lap exactly once." :
                        "Recovery source, team, lap or deadline is invalid.";
                    var changed = f.Snapshot();
                    Assert.AreEqual(expected, Check(changed), Key(first) + " versus " + Key(second));
                    Assert.AreEqual(expected, Check(changed), "A previous validation must not cache this result.");
                }

                for (int team = -1; team <= 7; team++)
                    for (int lap = 0; lap < 3; lap++)
                    {
                        var first = new MatchResourceState("probe", team, lap);
                        Pair(first, new MatchResourceState("probe", team, lap, 9, 0));
                        Pair(first, new MatchResourceState("probe", team == 7 ? -1 : team + 1, lap));
                        Pair(first, new MatchResourceState("probe", team, (lap + 1) % 3));
                    }
                foreach (string id in new[] { "Probe", "probe-", "probe.", "probe:", "probe_", "probe0" })
                    Pair(new MatchResourceState("probe", -1, 0), new MatchResourceState(id, -1, 0));
                Pair(new MatchResourceState("probe", -1, 0), new MatchResourceState("probe", 1, 0));
                Assert.AreEqual("", Check(valid));
                Assert.AreEqual(retained, MatchSnapshotCodec.Export(valid));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestCase("TimeRun", true), TestCase("Freestyle", false)]
        public void DuplicateResourceIdentityRejectsDifferentMutableFieldsBeforeLaterFailures(string mode, bool race)
        {
            var f = PhysicalFixture(mode);
            Assert.AreEqual(race, f.Source.Mode.IsRace);
            string Check(MatchSnapshot snapshot) => CheckPhysical(f, snapshot);
            var valid = f.State;
            Assert.AreEqual("", Check(valid));
            Assert.GreaterOrEqual(valid.Resources.Count, 2);
            var original = valid.Resources.ToArray();
            var changed = original.ToArray();
            var row = original[0];
            changed[1] = new MatchResourceState(row.SourceId, row.Team, row.Lap, row.Generation + 3, 0);
            var bodies = valid.Bodies.Concat(new[] { new WorldBodyState("tile:bad", WorldBodyKind.Tile, default,
                ownerTeam: row.Team, protectedResource: true, sourceId: row.SourceId) }).ToArray();
            var duplicate = ReplacePhysical(valid, bodies: bodies, resources: changed);
            const string identity = "Snapshot must include every static source, team and lap exactly once.";
            Assert.AreEqual(identity, Check(duplicate));
            Assert.AreEqual(identity, Check(duplicate));
            Assert.AreEqual("World ID suffix is invalid.", Check(ReplacePhysical(valid, bodies: bodies)),
                "The later failure must be real once identity is repaired.");
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

        [TestCase(WorldBodyKind.Item, "generation"), TestCase(WorldBodyKind.Tile, "generation")]
        [TestCase(WorldBodyKind.Item, "inactive-lap"), TestCase(WorldBodyKind.Tile, "inactive-lap")]
        [TestCase(WorldBodyKind.Item, "rival"), TestCase(WorldBodyKind.Tile, "rival")]
        [TestCase(WorldBodyKind.Item, "protection"), TestCase(WorldBodyKind.Tile, "protection")]
        [TestCase(WorldBodyKind.Item, "unknown"), TestCase(WorldBodyKind.Tile, "unknown")]
        [TestCase(WorldBodyKind.Item, "unsourced"), TestCase(WorldBodyKind.Tile, "unsourced")]
        public void BodySourceMembershipRetainsEveryIdentityAndDemandCheck(WorldBodyKind kind, string mutation)
        {
            var f = PhysicalFixture("TeamRace");
            var valid = kind == WorldBodyKind.Item ? ProtectedItemSnapshot(f) : f.State;
            Assert.AreEqual("", CheckPhysical(f, valid));
            string before = MatchSnapshotCodec.Export(valid);
            var body = ProtectedSourceBody(valid, kind);
            var changed = SourceBody(body,
                owner: mutation == "rival" ? 1 - body.OwnerTeam : body.OwnerTeam,
                protection: mutation != "protection",
                source: mutation == "unknown" ? "unbound-source" : mutation == "unsourced" ? "" : body.SourceId,
                generation: mutation == "generation" ? body.RecoveryGeneration + 3 :
                    mutation == "inactive-lap" ? body.RecoveryGeneration + 1 : body.RecoveryGeneration);
            if (mutation == "inactive-lap")
                Assert.IsTrue(valid.Resources.Any(resource => resource.SourceId == body.SourceId && resource.Team == body.OwnerTeam &&
                    resource.Generation == changed.RecoveryGeneration), "A real matching but inactive resource must reach the demand predicate.");
            var invalid = ReplacePhysical(valid, bodies: valid.Bodies.Select(value => value.Id == body.Id ? changed : value));
            string expected = mutation == "unknown" ? "World source does not match an approved placement." : mutation == "unsourced" ?
                "Unsourced loose object has invalid protection or ownership." : "Protected world object disagrees with its recovery source.";
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(valid));
        }

        [TestCase(false), TestCase(true)]
        public void MissingOrDuplicateResourcesStillFailBeforeAnInvalidBodySource(bool duplicate)
        {
            var f = PhysicalFixture("TeamRace");
            var valid = ProtectedItemSnapshot(f);
            Assert.AreEqual("", CheckPhysical(f, valid));
            var body = ProtectedSourceBody(valid, WorldBodyKind.Item);
            var bodies = valid.Bodies.Select(value => value.Id == body.Id ? SourceBody(body, generation: body.RecoveryGeneration + 3) : value).ToArray();
            Assert.AreEqual("Protected world object disagrees with its recovery source.", CheckPhysical(f, ReplacePhysical(valid, bodies: bodies)));
            var resources = valid.Resources.ToList();
            if (duplicate) resources[1] = resources[0];
            else resources.RemoveAt(0);
            var invalid = ReplacePhysical(valid, bodies: bodies, resources: resources);
            const string expected = "Snapshot must include every static source, team and lap exactly once.";
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual(expected, CheckPhysical(f, invalid));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [Test]
        public void PhysicalBoundsStillRejectBeforeACompetingBodySourceMismatch()
        {
            var f = PhysicalFixture("TeamRace");
            var valid = ProtectedItemSnapshot(f);
            var body = ProtectedSourceBody(valid, WorldBodyKind.Item);
            string Check(WorldBodyState changed) => CheckPhysical(f,
                ReplacePhysical(valid, bodies: valid.Bodies.Select(value => value.Id == body.Id ? changed : value)));
            Assert.AreEqual("", CheckPhysical(f, valid));
            Assert.AreEqual("Protected world object disagrees with its recovery source.", Check(SourceBody(body, generation: body.RecoveryGeneration + 3)));
            const string bounds = "World body exceeds physical bounds or names an absent team.";
            Assert.AreEqual(bounds, Check(SourceBody(body, position: new WorldVector(10001, 0, 0))));
            Assert.AreEqual(bounds, Check(SourceBody(body, position: new WorldVector(10001, 0, 0), generation: body.RecoveryGeneration + 3)));
            Assert.AreEqual("", CheckPhysical(f, valid));
        }

        [Test]
        public void ResourceGenerationReplacementRequiresMatchingBodiesOnEveryValidationCall()
        {
            var f = PhysicalFixture("TeamRace");
            var old = f.State;
            Assert.AreEqual("", CheckPhysical(f, old));
            var body = ProtectedSourceBody(old, WorldBodyKind.Tile);
            var row = old.Resources.Single(resource => resource.SourceId == body.SourceId && resource.Team == body.OwnerTeam &&
                resource.Generation == body.RecoveryGeneration);
            var bodies = old.Bodies.Select(value => value.SourceId == body.SourceId && value.OwnerTeam == body.OwnerTeam &&
                value.RecoveryGeneration == row.Generation ? SourceBody(value, generation: row.Generation + 3) : value).ToArray();
            Assert.Greater(bodies.Count(value => value.SourceId == body.SourceId && value.RecoveryGeneration == row.Generation + 3), 1,
                "The valid authored batch must exercise multiple bodies sharing one source group.");
            var resources = old.Resources.Select(value => ReferenceEquals(value, row) ?
                new MatchResourceState(row.SourceId, row.Team, row.Lap, row.Generation + 3, row.RecoveryAtTick) : value).ToArray();
            const string mismatch = "Protected world object disagrees with its recovery source.";
            Assert.AreEqual(mismatch, CheckPhysical(f, ReplacePhysical(old, resources: resources)));
            Assert.AreEqual(mismatch, CheckPhysical(f, ReplacePhysical(old, bodies: bodies)));
            var current = ReplacePhysical(old, bodies: bodies, resources: resources);
            string oldBytes = MatchSnapshotCodec.Export(old), currentBytes = MatchSnapshotCodec.Export(current);
            Assert.AreEqual("", CheckPhysical(f, current));
            Assert.AreEqual("", CheckPhysical(f, old));
            Assert.AreEqual("", CheckPhysical(f, current));
            Assert.AreEqual(oldBytes, MatchSnapshotCodec.Export(old));
            Assert.AreEqual(currentBytes, MatchSnapshotCodec.Export(current));
        }

        [Test]
        public void RealPracticeSelectionChangesDemandWithoutReusingPriorSourceMembership()
        {
            var f = PhysicalFixture("Freestyle");
            var first = f.Source.Course.PhysicalSites.First(site => site.RecoverySupplyId != "");
            var second = f.Source.Course.PhysicalSites.First(site => site.RecoverySupplyId != "" &&
                site.RecoverySupplyId != first.RecoverySupplyId && site.PuzzleId != first.PuzzleId &&
                !f.Source.Course.Puzzles.Single(puzzle => puzzle.Id == site.PuzzleId).RecoverySupplyIds.Contains(first.RecoverySupplyId));
            CommandReceipt Select(ResolvedCoursePhysicalSite site) => f.Step(f.Command("a", WordboundAction.PracticePuzzle, site.Hash),
                new[] { f.Pose("a", new WorldVector(site.Position.X / 1000f, site.Position.Y / 1000f, site.Position.Z / 1000f)) }).Receipts.Single();
            Assert.AreEqual("Ok", Select(first).Effect.Code);
            var old = f.State;
            string sourceId = f.Source.Course.Supplies.Single(source => source.SourceId == first.RecoverySupplyId).Id;
            Assert.IsTrue(old.Bodies.Any(body => body.Kind == WorldBodyKind.Tile && body.SourceId == sourceId));
            Assert.AreEqual("", CheckPhysical(f, old));
            Assert.AreEqual("Ok", Select(second).Effect.Code);
            var current = f.State;
            Assert.IsFalse(current.Bodies.Any(body => body.Kind == WorldBodyKind.Tile && body.SourceId == sourceId));
            Assert.AreEqual("", CheckPhysical(f, current));
            var stale = new MatchSnapshot(current.Binding, current.Epoch, current.Sequence, current.NextWorldEntityId,
                current.Progress, old.Economy, current.Commands, old.Actors, old.Bodies, old.Resources, old.Mechanisms,
                old.RandomStreams, old.NarrativeFlags, old.ThrownFlights, current.Practice);
            Assert.AreEqual("Protected world object disagrees with its recovery source.", CheckPhysical(f, stale),
                "A matching resource key alone must not admit the preceding station's now-undemanded loose bodies.");
            string oldBytes = MatchSnapshotCodec.Export(old), currentBytes = MatchSnapshotCodec.Export(current);
            Assert.AreEqual("", CheckPhysical(f, old));
            Assert.AreEqual("", CheckPhysical(f, current));
            Assert.AreEqual(oldBytes, MatchSnapshotCodec.Export(old));
            Assert.AreEqual(currentBytes, MatchSnapshotCodec.Export(current));
        }

        static MatchSnapshot ProtectedItemSnapshot(WordboundAuthorityTests.Fixture f)
        {
            var before = f.State;
            Assert.AreEqual("", CheckPhysical(f, before));
            string original = MatchSnapshotCodec.Export(before);
            var supply = f.Source.Course.Supplies.FirstOrDefault(source => source.Protected && source.TeamIndex == 0 &&
                f.Source.Mode.Recipes.Contains(source.Letters) &&
                before.Bodies.Count(body => body.Kind == WorldBodyKind.Tile && body.SourceId == source.Id &&
                    body.OwnerTeam == source.TeamIndex && body.RecoveryGeneration == 0) == source.Letters.Length);
            Assert.IsNotNull(supply, "The authored fixture must provide a complete protected recipe batch for the Item source cases.");
            var resource = before.Resources.Single(row => row.SourceId == supply.Id && row.Team == supply.TeamIndex && row.Generation == 0);
            var tiles = before.Bodies.Where(body => body.Kind == WorldBodyKind.Tile && body.SourceId == supply.Id &&
                body.OwnerTeam == supply.TeamIndex && body.RecoveryGeneration == resource.Generation).ToArray();
            var economy = new Economy(f.Source.Catalogue, f.Source.Rules);
            foreach (var player in before.Economy.Players) economy.AddPlayer(player.PlayerId, player.Team);
            economy.Restore(before.Economy);
            int crafter = before.Economy.Players.First(player => player.Team == supply.TeamIndex).PlayerId;
            foreach (var tile in tiles)
                Assert.IsTrue(economy.CollectTile(crafter, int.Parse(tile.Id.Substring("tile:".Length))).Ok);
            Assert.IsTrue(economy.Player(crafter).Letters.Equals(LetterBag.FromWord(supply.Letters)));
            Assert.IsTrue(economy.BeginCraft(crafter, supply.Letters, 0).Ok);
            var completion = economy.CompleteCraft(crafter, economy.Player(crafter).Craft.ReadyAt);
            Assert.IsTrue(completion.Ok);
            var item = completion.Item;
            int slot = Array.IndexOf(economy.Player(crafter).Slots, item.Id);
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsTrue(economy.Drop(crafter, slot).Ok);
            Assert.AreEqual(ItemOrigin.Crafted, item.Origin);
            Assert.AreEqual(ItemState.World, item.State);
            var removed = new HashSet<string>(tiles.Select(tile => tile.Id), StringComparer.Ordinal);
            var body = new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, tiles[0].Position,
                ownerTeam: supply.TeamIndex, protectedResource: true, sourceId: supply.Id, recoveryGeneration: resource.Generation);
            var snapshot = new MatchSnapshot(before.Binding, before.Epoch, before.Sequence, before.NextWorldEntityId + 1,
                before.Progress, economy.Capture(), before.Commands, before.Actors,
                before.Bodies.Where(value => !removed.Contains(value.Id)).Concat(new[] { body }), before.Resources,
                before.Mechanisms, before.RandomStreams, before.NarrativeFlags, before.ThrownFlights, before.Practice);
            Assert.AreEqual("", CheckPhysical(f, snapshot), "The constructed protected Item snapshot must pass every validation phase before mutation.");
            Assert.AreEqual(original, MatchSnapshotCodec.Export(before));
            return snapshot;
        }

        static WorldBodyState ProtectedSourceBody(MatchSnapshot snapshot, WorldBodyKind kind) => snapshot.Bodies.First(body =>
            body.Kind == kind && body.Protected && body.SourceId != "" && snapshot.Resources.Any(resource =>
                resource.SourceId == body.SourceId && resource.Team == body.OwnerTeam && resource.Generation == body.RecoveryGeneration));

        static WorldBodyState SourceBody(WorldBodyState body, int? owner = null, bool? protection = null, string source = null,
            int? generation = null, WorldVector? position = null) => new WorldBodyState(body.Id, body.Kind, position ?? body.Position,
                body.RotationXyz, body.RotationW, body.Velocity, body.AngularVelocity, body.Sleeping, body.Kinematic, body.Active,
                owner ?? body.OwnerTeam, protection ?? body.Protected, source ?? body.SourceId, generation ?? body.RecoveryGeneration);
    }
}
