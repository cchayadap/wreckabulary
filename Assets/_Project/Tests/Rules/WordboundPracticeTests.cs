using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPracticeTests
    {
        static WordboundAuthorityTests.Fixture Create()
        {
            var f = WordboundAuthorityTests.Create();
            var source = f.Source;
            var draft = source.Course.CopyDocument();
            draft.Schema = 2;
            draft.SupportedModes.Add("Freestyle");
            foreach (var connection in draft.Connections) connection.GatePuzzleId = "";
            foreach (var puzzle in draft.Puzzles) puzzle.PresetId = "PlaceObject";
            draft.PhysicalSites.Add(new CoursePhysicalSite
            {
                Lap = 1, PuzzleId = "BedBridge", CheckpointId = "Begin", RecoverySupplyId = "BedSupply",
                Position = new CoursePoint(0, 0, 0)
            });
            draft.PhysicalSites.Add(new CoursePhysicalSite
            {
                Lap = 2, PuzzleId = "FanDoor", CheckpointId = "UpperA", RecoverySupplyId = "FanSupply",
                Position = new CoursePoint(0, 6000, 0)
            });
            draft.PhysicalSites.Add(new CoursePhysicalSite
            {
                Lap = 3, PuzzleId = "BedBridge", CheckpointId = "Begin", RecoverySupplyId = "BedSupply",
                Position = new CoursePoint(0, 0, 1000)
            });
            source.Mode = ModeDefinition.For("Freestyle");
            source.Rules = source.Mode.CreateGameplayRules(source.Rules);
            var result = CourseResolver.Resolve(draft, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest(source.Course.Seed, MatchStateValidation.Requirements(source.Mode)));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
            source.Course = result.Manifest;
            source.Binding = new MatchBinding("practice", MatchSnapshotCodec.ProtocolVersion, source.Mode.Hash,
                source.Course.SourceHash, source.Course.Hash, new string('a', 64), new string('b', 64),
                new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 0, 1) });
            f.Match = new AuthoritativeMatch(source.Binding, source.Mode, source.Course, CourseTemplates.ProofCatalogue(),
                source.Catalogue, source.Rules, f.World);
            return f;
        }

        static WorldVector Position(CoursePoint point) => new WorldVector(point.X / 1000f, point.Y / 1000f, point.Z / 1000f);
        static CommandReceipt Select(WordboundAuthorityTests.Fixture f, int authoredLap, string actor = "a")
        {
            var site = f.Source.Course.PhysicalSites.Single(s => s.Lap == authoredLap);
            return f.Step(f.Command(actor, WordboundAction.PracticePuzzle, site.Hash),
                new[] { f.Pose(actor, Position(site.Position)) }).Receipts.Single();
        }
        static string Craft(WordboundAuthorityTests.Fixture f, int lap)
        {
            var site = f.Source.Course.PhysicalSites.Single(s => s.Lap == lap);
            var puzzle = f.Source.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
            string source = f.Source.Course.Supplies.Single(s => s.SourceId == site.RecoverySupplyId).Id;
            foreach (var tile in f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId == source).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: puzzle.RecipeId)).Receipts.Single().Effect.Code);
            f.Advance(61);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0),
                new[] { f.Pose("a", Position(site.Position)) }).Receipts.Single().Effect.Code);
            return "item:" + f.State.Economy.Items.Last(i => i.Origin == ItemOrigin.Crafted && i.Word == puzzle.RecipeId).Id;
        }
        static void CompleteEvidence(WordboundAuthorityTests.Fixture f, int lap, string item)
        {
            var site = f.Source.Course.PhysicalSites.Single(s => s.Lap == lap);
            long start = f.Match.Tick + 1;
            foreach (var stage in new[] { MatchPhysicalStage.Armed, MatchPhysicalStage.Complete })
                f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                {
                    new HostMechanismObservation(site.PuzzleId, 0, lap - 1, true, start, stage == MatchPhysicalStage.Armed ? 1 : 2,
                        item, new MatchPhysicalEvidence(stage, "a", "", f.Match.Tick + 1, site.Hash))
                });
        }
        static MatchSnapshot Copy(MatchSnapshot s, MatchPracticeState practice = null, MatchMechanismState[] mechanisms = null) =>
            new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId, s.Progress, s.Economy, s.Commands,
                s.Actors, s.Bodies, s.Resources, mechanisms ?? s.Mechanisms.ToArray(), s.RandomStreams,
                s.NarrativeFlags, s.ThrownFlights, practice ?? s.Practice);

        [Test]
        public void PracticeStartsWithoutRaceRouteOrUnrequestedRecoveryMaterial()
        {
            var f = Create();
            Assert.AreEqual(0, f.Source.Course.Laps.Count);
            Assert.AreEqual(3, f.State.Mechanisms.Count);
            Assert.AreEqual("", f.State.Practice.SiteHash);
            Assert.IsTrue(f.State.Bodies.Any(b => b.Id == "keepsake:0"));
            Assert.IsFalse(f.State.Bodies.Any(b => f.Source.Course.Supplies.Any(s => s.Id == b.SourceId)));
            f.Ack();
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void AnyAuthoredStationCanBeSelectedWithoutRacePrerequisites(int lap)
        {
            var f = Create();
            Assert.AreEqual("Ok", Select(f, lap).Effect.Code);
            var site = f.Source.Course.PhysicalSites.Single(s => s.Lap == lap);
            Assert.AreEqual(site.Hash, f.State.Practice.SiteHash);
            Assert.AreEqual(1, f.State.Practice.SelectedTick);
            string supply = f.Source.Course.Supplies.Single(s => s.SourceId == site.RecoverySupplyId).Id;
            Assert.AreEqual(f.Source.Course.Puzzles.Single(p => p.Id == site.PuzzleId).RequiredLetters.Length,
                f.State.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == supply));
            Assert.AreEqual(0, f.State.Progress.Teams[0].OrderedObjectives);
            f.Ack();
        }

        [TestCase("far"), TestCase("occluded"), TestCase("unknown")]
        public void PracticeSelectionRequiresExactHostReachAndVisibility(string reason)
        {
            var f = Create();
            var site = f.Source.Course.PhysicalSites[0];
            f.World.Visible = reason != "occluded";
            var position = reason == "far" ? new WorldVector(100, 0, 0) : Position(site.Position);
            var receipt = f.Step(f.Command("a", WordboundAction.PracticePuzzle,
                reason == "unknown" ? new string('f', 64) : site.Hash), new[] { f.Pose("a", position) }).Receipts.Single();
            Assert.AreEqual(reason == "unknown" ? "UnknownPractice" : "OutOfReach", receipt.Effect.Code);
            Assert.AreEqual("", f.State.Practice.SiteHash);
        }

        [Test]
        public void SelectionSwitchRetiresOnlyLooseOldRecoveryAndPreservesCollectedValue()
        {
            var f = Create(); Select(f, 1);
            string old = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply").Id;
            f.Collect("a", f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.SourceId == old).Id);
            string carried = f.State.Economy.Players.Single(p => p.PlayerId == 0).Letters.ToString();
            Assert.AreEqual("Ok", Select(f, 2, "b").Effect.Code);
            Assert.IsFalse(f.State.Bodies.Any(b => b.Kind == WorldBodyKind.Tile && b.SourceId == old));
            Assert.AreEqual(carried, f.State.Economy.Players.Single(p => p.PlayerId == 0).Letters.ToString());
            f.Ack();
        }

        [Test]
        public void RecipeAndHostBooleanStillRequirePhysicalEvidenceInPractice()
        {
            var f = Create(); Select(f, 1); string item = Craft(f, 1);
            Assert.AreEqual("MechanismIncomplete", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item))
                .Receipts.Single().Effect.Code);
            f.Ack();
        }

        [Test]
        public void CompletionAndSelectionSurviveMigrationWithoutRaceProgressOrRepeatRewards()
        {
            var f = Create(); Select(f, 2); string item = Craft(f, 2); CompleteEvidence(f, 2, item);
            var stamp = f.Match.PublishSnapshot(out string json);
            var restored = f.Source.Import(json);
            Assert.IsTrue(restored.Success, restored.Error);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(restored.Snapshot, stamp, 1, f.State.Progress.WallClockMs + 100);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "FanDoor", value: item)).Receipts.Single().Effect.Code);
            Assert.IsTrue(f.State.Mechanisms.Single(m => m.PuzzleId == "FanDoor").Solved);
            Assert.AreEqual("AlreadySolved", Select(f, 2).Effect.Code);
            Assert.AreEqual(0, f.State.Progress.Teams[0].OrderedObjectives);
            Assert.AreEqual(0, f.State.Progress.Teams[0].CompletedPuzzles.Count);
            Assert.AreEqual(0, f.State.NarrativeFlags.Count);
            Assert.IsNull(f.State.Progress.Result);
            Assert.AreEqual(0, f.Source.Mode.TimeLimitSeconds);
            f.Ack();
        }

        [TestCase("unknown"), TestCase("future"), TestCase("cleared-active")]
        public void RehashedSelectionCannotDetachActivePhysicalEvidence(string mutation)
        {
            var f = Create(); Select(f, 1); string item = Craft(f, 1); CompleteEvidence(f, 1, item);
            var state = f.State;
            var changed = mutation == "unknown" ? new MatchPracticeState(new string('f', 64), 1) :
                mutation == "future" ? new MatchPracticeState(state.Practice.SiteHash, state.Progress.Tick + 1) : MatchPracticeState.Empty;
            Assert.IsFalse(f.Source.Import(MatchSnapshotCodec.Export(Copy(state, changed))).Success);
        }

        [Test]
        public void FailedPracticeSolveRollsBackSelectionEvidenceInventoryAndReceipt()
        {
            var f = Create(); Select(f, 1); string item = Craft(f, 1); CompleteEvidence(f, 1, item);
            var retained = f.State;
            string before = MatchSnapshotCodec.Export(retained);
            f.World.ThrowQuery = true;
            var command = f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item);
            Assert.Throws<InvalidOperationException>(() => f.Step(command));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            f.World.ThrowQuery = false;
            Assert.AreEqual("Ok", f.Step(command).Receipts.Single().Effect.Code);
            string bed = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply").Id;
            Assert.IsFalse(f.State.Bodies.Any(b => b.Kind == WorldBodyKind.Tile && b.SourceId == bed));
            Assert.AreEqual("Ok", Select(f, 2, "b").Effect.Code);
            string fan = f.Source.Course.Supplies.Single(s => s.SourceId == "FanSupply").Id;
            Assert.AreEqual(3, f.State.Bodies.Count(b => b.Kind == WorldBodyKind.Tile && b.SourceId == fan));
            Assert.IsEmpty(f.State.Progress.Teams[0].CompletedPuzzles);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(retained));
            f.Ack();
        }

        [Test]
        public void SharedKeepsakeTakeDropAndRecoveryRemainCanonicalWithoutRace()
        {
            var f = Create(); var body = f.State.Bodies.Single(b => b.Id == "keepsake:0");
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.TakeKeepsake),
                new[] { f.Pose("a", body.Position) }).Receipts.Single().Effect.Code);
            Assert.AreEqual("a", f.State.Progress.Teams[0].KeepsakeCarrier);
            f.Ack();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.DropKeepsake)).Receipts.Single().Effect.Code);
            f.Advance(600);
            Assert.IsNull(f.State.Progress.Teams[0].KeepsakeCarrier);
            Assert.AreEqual(-1, f.State.Progress.Teams[0].KeepsakeRecoveryAt);
            f.Ack();
        }

        [Test]
        public void SchemaThreeRequiresPracticeAndRejectsOldCheckpointVersions()
        {
            var f = Create();
            Assert.AreEqual(3, MatchSnapshot.Schema);
            Assert.AreEqual(3, MatchSnapshotCodec.CanonicalVersion);
            Assert.AreEqual("wordbound-v3", MatchSnapshotCodec.ProtocolVersion);
            string json = MatchSnapshotCodec.Export(f.State);
            StringAssert.Contains("\"practice\":{\"selectedTick\":\"-1\",\"siteHash\":\"\"}", json);
            var old = f.Source.Import(json.Replace("\"schema\":3", "\"schema\":2")
                .Replace("\"canonicalVersion\":3", "\"canonicalVersion\":2"));
            Assert.IsFalse(old.Success);
            StringAssert.Contains("unsupported snapshot format or version", old.Error);
            Assert.IsFalse(f.Source.Import(json.Replace("\"practice\":{\"selectedTick\":\"-1\",\"siteHash\":\"\"},", "")).Success);
        }
    }
}
