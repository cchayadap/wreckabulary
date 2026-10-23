using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthorityTests
    {
        internal sealed class World : IWordboundWorldQuery, IWordboundReleaseQuery
        {
            public bool Visible = true, Placeable = true, Satisfied = true, ThrowQuery;
            public bool Releaseable = true;
            public WorldVector? ReleaseAt;
            public Action DuringQuery;
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => Visible;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => Placeable;
            public bool TryRelease(WorldBodyState actor, WorldBodyState item, bool throwing, out WorldVector at)
            {
                at = ReleaseAt ?? new WorldVector(actor.Position.X, actor.Position.Y + (throwing ? .75f : .08f), actor.Position.Z + 1.5f);
                return Releaseable;
            }
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem)
            {
                if (ThrowQuery) throw new InvalidOperationException("Host query failed.");
                DuringQuery?.Invoke();
                return Satisfied;
            }
        }
        internal sealed class Fixture
        {
            public WordboundSnapshotCodecTests.Fixture Source;
            public AuthoritativeMatch Match;
            public World World = new World();
            public Dictionary<string, long> Sequences = new Dictionary<string, long> { ["a"] = 0, ["b"] = 0 };
            public AuthenticatedCommand Command(string actor, WordboundAction action, string target = "", int revision = 0,
                int slot = -1, string value = "") => new AuthenticatedCommand(actor,
                    new PlayerCommand(++Sequences[actor], Match.Epoch, action, target, revision, slot, value));
            public MatchSnapshot State => Match.Capture(1);
            public void Ack()
            {
                var stamp = Match.PublishSnapshot(out string json);
                Assert.IsTrue(Match.Acknowledge(stamp));
                var imported = Source.Import(json);
                Assert.IsTrue(imported.Success, imported.Error);
            }
            public MatchTickResult Step(AuthenticatedCommand command = null, IEnumerable<WorldBodyState> poses = null,
                IEnumerable<ProgressEvent> observations = null, IEnumerable<HostDamage> damage = null) =>
                Match.Advance(State.Progress.WallClockMs + 17, command == null ? null : new[] { command }, poses, observations, damage);
            public void Advance(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Match.Tick % 60 == 0) Ack();
                    Assert.IsTrue(Step().Advanced);
                }
            }
            public WorldBodyState Pose(string actor, WorldVector position) =>
                State.Bodies.Single(b => b.Kind == WorldBodyKind.Player && b.SourceId == actor).At(position);
            public CommandReceipt Collect(string actor, string tileId)
            {
                var tile = State.Bodies.Single(b => b.Id == tileId);
                return Step(Command(actor, WordboundAction.CollectLetter, tileId), new[] { Pose(actor, tile.Position) }).Receipts.Single();
            }
            public string CraftBed(string actor = "a")
            {
                var seat = Source.Binding.Seats.Single(s => s.PlayerId == actor);
                string source = Source.Course.Supplies.Single(s => s.SourceId == "BedSupply" && s.TeamIndex == seat.Team).Id;
                foreach (var tile in State.Bodies.Where(b => b.SourceId == source).ToArray())
                    Assert.AreEqual("Ok", Collect(actor, tile.Id).Effect.Code);
                Assert.AreEqual("Ok", Step(Command(actor, WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
                Advance(61);
                var item = State.Economy.Items.Single(i => i.Word == "BED" && i.Origin == ItemOrigin.Crafted);
                return "item:" + item.Id;
            }
        }
        internal static Fixture Create(string mode = "TimeRun")
        {
            var result = new Fixture { Source = WordboundSnapshotCodecTests.Create(mode) };
            var f = result.Source;
            result.Match = new AuthoritativeMatch(f.Binding, f.Mode, f.Course, CourseTemplates.ProofCatalogue(), f.Catalogue, f.Rules, result.World);
            return result;
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void EveryModeStartsWithACompleteImportableSnapshot(string mode)
        {
            var f = Create(mode);
            f.Match.PublishSnapshot(out string json);
            var result = f.Source.Import(json);
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(f.Source.Binding.Hash, result.Snapshot.Binding.Hash);
            Assert.IsTrue(json.Length < 100000, "Small proof snapshots should remain practical transport payloads.");
            Console.WriteLine(mode + " initial snapshot UTF-8 bytes: " + System.Text.Encoding.UTF8.GetByteCount(json));
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void TickAndPresenceMatchesTheCapturedComparisonAcrossSeatChanges(string mode)
        {
            var f = Create(mode);
            var original = f.State;
            string originalJson = MatchSnapshotCodec.Export(original);
            AssertTickAndPresence(f, null, false);
            AssertTickAndPresence(f, original, true);
            Assert.IsTrue(f.Step().Advanced);
            AssertTickAndPresence(f, original, false);
            var advanced = f.State;
            AssertTickAndPresence(f, advanced, true);
            Assert.IsTrue(f.Match.Disconnect("a", 1000));
            AssertTickAndPresence(f, advanced, false);
            var disconnected = f.State;
            AssertTickAndPresence(f, disconnected, true);
            Assert.IsFalse(f.Match.Disconnect("a", 1001));
            AssertTickAndPresence(f, disconnected, true);
            Assert.IsTrue(f.Match.Reconnect("a", 1002));
            AssertTickAndPresence(f, disconnected, false);
            AssertTickAndPresence(f, advanced, true);
            var reconnected = f.State;
            Assert.IsTrue(f.Match.Return("b", 1003));
            AssertTickAndPresence(f, reconnected, false);
            var returned = f.State;
            AssertTickAndPresence(f, returned, true);
            Assert.IsFalse(f.Match.Reconnect("b", 1004));
            AssertTickAndPresence(f, returned, true);
            Assert.AreEqual(originalJson, MatchSnapshotCodec.Export(original), "Comparison must not mutate retained frames.");
        }

        [Test]
        public void TickAndPresenceTracksRestoredEpochWithoutTreatingPauseOrPublicationAsATick()
        {
            var f = Create();
            var original = f.State;
            var stamp = f.Match.PublishSnapshot(out string json);
            AssertTickAndPresence(f, original, true);
            Assert.IsTrue(f.Match.Acknowledge(stamp));
            AssertTickAndPresence(f, original, true);
            f.Match.PauseForMigration();
            AssertTickAndPresence(f, original, true);
            Assert.IsFalse(f.Step().Advanced);
            AssertTickAndPresence(f, original, true);
            var saved = f.Source.Import(json);
            Assert.IsTrue(saved.Success, saved.Error);
            f.Match.RestoreCommitted(saved.Snapshot, stamp, 1, 1000);
            Assert.AreEqual(original.Progress.Tick, f.Match.Tick);
            AssertTickAndPresence(f, original, false);
            AssertTickAndPresence(f, f.State, true);
        }

        [Test]
        public void TickAndPresenceRejectsAnotherBindingAndDoesNotReplaceSnapshotValidation()
        {
            var f = Create();
            var state = f.State;
            var binding = state.Binding;
            var foreign = new MatchBinding("another-match", binding.ProtocolVersion, binding.ModeHash,
                binding.CourseHash, binding.ManifestHash, binding.RosterHash, binding.CatalogueHash, binding.Seats);
            MatchSnapshot Replace(MatchBinding replacement, IEnumerable<WorldBodyState> bodies, ProgressSnapshot progress = null) =>
                new MatchSnapshot(replacement, state.Epoch, state.Sequence, state.NextWorldEntityId,
                    progress ?? state.Progress, state.Economy, state.Commands, state.Actors, bodies, state.Resources,
                    state.Mechanisms, state.RandomStreams, state.NarrativeFlags, state.ThrownFlights, state.Practice);
            Assert.IsFalse(f.Match.MatchesTickPresenceAndResult(Replace(foreign, state.Bodies)));
            var shortRoster = new MatchProgress(f.Source.Mode, binding.Seats.Take(1),
                MatchStateValidation.Route(f.Source.Course)).Capture();
            Assert.IsFalse(f.Match.MatchesTickPresenceAndResult(Replace(binding, state.Bodies, shortRoster)));
            var differentRoster = new MatchProgress(f.Source.Mode,
                new[] { new WordboundSeat("a", 0), new WordboundSeat("c", 0, 1) },
                MatchStateValidation.Route(f.Source.Course)).Capture();
            Assert.IsFalse(f.Match.MatchesTickPresenceAndResult(Replace(binding, state.Bodies, differentRoster)));
            var incomplete = Replace(binding, Array.Empty<WorldBodyState>());
            AssertTickAndPresence(f, incomplete, true);
            Assert.IsNotEmpty(MatchStateValidation.Validate(incomplete, binding, f.Source.Mode, f.Source.Course,
                f.Source.Catalogue, f.Source.Rules), "Tick/presence equality is not a trusted-snapshot admission check.");
            AssertTickAndPresence(f, state, true);
        }

        static void AssertTickAndPresence(Fixture f, MatchSnapshot previous, bool expected)
        {
            var captured = f.State;
            bool legacy = previous != null && previous.Epoch == captured.Epoch &&
                previous.Progress.Tick == captured.Progress.Tick && previous.Progress.Players.Zip(captured.Progress.Players,
                    (a, b) => a.Id == b.Id && a.Connected == b.Connected && a.Returned == b.Returned).All(same => same);
            Assert.AreEqual(expected, legacy, "The independent original captured-frame comparator must reach the expected state.");
            Assert.AreEqual(legacy, f.Match.MatchesTickPresenceAndResult(previous));
            Assert.AreEqual(legacy, f.Match.MatchesTickPresenceAndResult(previous), "Repeated queries must not mutate or cache dynamic state.");
        }

        [Test]
        public void AuthoredFurnitureRotationSurvivesTheInitialRecoverySnapshot()
        {
            var f = Create("Freestyle");
            var placement = f.Source.Course.Placements.Single(p => p.ZoneId == "FixedStool");
            Assert.AreEqual(90, placement.Yaw);
            f.Match.PublishSnapshot(out string json);
            var restored = f.Source.Import(json);
            Assert.IsTrue(restored.Success, restored.Error);
            var body = restored.Snapshot.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id);
            Assert.AreEqual(1f, 2f * body.RotationXyz.Y * body.RotationW, .000001f,
                "The stool's authored forward direction must point along positive X.");
            Assert.AreEqual(0f, 1f - 2f * body.RotationXyz.Y * body.RotationXyz.Y, .000001f);
        }

        [Test]
        public void ContestedPickupUsesStableActorOrderAndReplayHasOneEffect()
        {
            var f = Create("Freestyle");
            var item = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            int revision = f.State.Economy.Items.Single(i => "item:" + i.Id == item.Id).Revision;
            var a = f.Command("a", WordboundAction.PickUp, item.Id, revision);
            var b = f.Command("b", WordboundAction.PickUp, item.Id, revision);
            var result = f.Match.Advance(17, new[] { b, a }, new[] { f.Pose("a", item.Position), f.Pose("b", item.Position) });
            Assert.AreEqual("a", result.Receipts[0].ActorId);
            Assert.AreEqual("Ok", result.Receipts[0].Effect.Code);
            Assert.AreEqual("OutOfReach", result.Receipts[1].Effect.Code);
            var duplicate = f.Step(a).Receipts.Single();
            Assert.AreEqual(result.Receipts[0].Tick, duplicate.Tick);
            Assert.AreEqual(0, f.State.Economy.Items.Single(i => "item:" + i.Id == item.Id).HolderId);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void RaceResourcesRejectRivalPickupEvenAtTheSamePosition(bool brawl)
        {
            var f = Create(brawl ? "BrawlRace" : "TeamRace");
            var tile = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.OwnerTeam == 0);
            Assert.AreEqual("OutOfReach", f.Collect("b", tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            f.Ack();
        }

        [Test]
        public void PickupRequiresAuthoritativeRangeAndLineOfSight()
        {
            var f = Create();
            var tile = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile);
            Assert.AreEqual("OutOfReach", f.Step(f.Command("a", WordboundAction.CollectLetter, tile.Id)).Receipts.Single().Effect.Code);
            f.World.Visible = false;
            Assert.AreEqual("OutOfReach", f.Collect("a", tile.Id).Effect.Code);
            f.World.Visible = true;
            Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
        }

        [Test]
        public void HostPoseCannotChangeOwnershipOrSourceMetadata()
        {
            var f = Create("TeamRace");
            var tile = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.OwnerTeam == 0);
            var forged = new WorldBodyState(tile.Id, tile.Kind, tile.Position, ownerTeam: 1);
            f.Step(poses: new[] { forged });
            var after = f.State.Bodies.Single(b => b.Id == tile.Id);
            Assert.AreEqual(tile.OwnerTeam, after.OwnerTeam);
            Assert.AreEqual(tile.SourceId, after.SourceId);
            Assert.AreEqual(tile.Protected, after.Protected);
            f.Ack();
        }

        [Test]
        public void CraftReservationCancellationCompletionAndPuzzleSpendConserveLetters()
        {
            var f = Create();
            var supply = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == supply.Id).ToArray()) f.Collect("a", tile.Id);
            f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED"));
            Assert.AreEqual("BDE", f.State.Economy.Players[0].Reserved.ToString());
            f.Step(f.Command("a", WordboundAction.CancelCraft));
            Assert.AreEqual("BDE", f.State.Economy.Players[0].Letters.ToString());
            Assert.IsNull(f.State.Economy.Players[0].Craft);
            f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED"));
            f.Advance(61);
            var item = f.State.Economy.Items.Single(i => i.Word == "BED" && i.Origin == ItemOrigin.Crafted);
            f.World.Placeable = false;
            Assert.AreEqual("BlockedPlacement", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            f.World.Placeable = true;
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            f.World.Satisfied = false;
            Assert.AreEqual("MechanismIncomplete", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: "item:" + item.Id)).Receipts.Single().Effect.Code);
            f.World.Satisfied = true;
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: "item:" + item.Id)).Receipts.Single().Effect.Code);
            Assert.AreEqual(3, f.State.Economy.Spent);
            Assert.IsTrue(f.State.Economy.Items.Single(i => i.Id == item.Id).IsGone);
            Assert.IsTrue(f.State.Progress.Teams[0].CompletedPuzzles.Contains("BedBridge"));
            f.Ack();
        }

        [Test]
        public void ClientStylePuzzleCompletionObservationCannotOpenAWordGate()
        {
            var f = Create();
            f.Step(observations: new[] { new ProgressEvent(ProgressEventKind.PuzzleSatisfied, "a", "BedBridge") });
            Assert.AreEqual(0, f.State.Progress.Teams[0].CompletedPuzzles.Count);
            Assert.AreEqual(0, f.State.Progress.Teams[0].NextCheckpoint);
            f.Ack();
        }

        [Test]
        public void FailedHostQueryRollsBackCommandsInventoryAndWorldTogether()
        {
            var f = Create();
            string item = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Deploy, slot: 0));
            var beforeFrame = f.State;
            string before = MatchSnapshotCodec.Export(beforeFrame);
            f.World.ThrowQuery = true;
            var request = f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item);
            Assert.Throws<InvalidOperationException>(() => f.Step(request));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            AssertTickAndPresence(f, beforeFrame, true);
            f.World.ThrowQuery = false;
            Assert.AreEqual("Ok", f.Step(request).Receipts.Single().Effect.Code);
            AssertTickAndPresence(f, beforeFrame, false);
            f.Ack();
        }

        [Test]
        public void FallingCancelsCraftDropsItsReservationAndRespawnsAtTheSafeAnchor()
        {
            var f = Create();
            var supply = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == supply.Id).ToArray()) f.Collect("a", tile.Id);
            f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED"));
            f.Step(poses: new[] { f.Pose("a", new WorldVector(0, -30, 0)) });
            Assert.IsTrue(f.State.Progress.Players[0].Eliminated);
            Assert.IsNull(f.State.Economy.Players[0].Craft);
            Assert.AreEqual(0, f.State.Economy.Players[0].LetterCount);
            Assert.IsTrue(f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Tile).All(b => b.Position.Y >= 0));
            f.Advance(120);
            Assert.IsFalse(f.State.Progress.Players[0].Eliminated);
            Assert.AreEqual(1, f.State.Progress.Players[0].RecoveryGeneration);
            Assert.AreEqual(f.Source.Rules.MaxHealth, f.State.Actors[0].Health);
            f.Ack();
        }

        [Test]
        public void ReturnDuringCraftPreservesRetainedSnapshotAndCanonicalLedgerCounts()
        {
            var f = Create();
            var source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == source.Id).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED")).Receipts.Single().Effect.Code);
            var retained = f.State;
            string before = MatchSnapshotCodec.Export(retained);
            Assert.IsNotNull(retained.Economy.Players[0].Craft);
            Assert.AreEqual(3, retained.Economy.Players[0].Reserved.Count);
            Assert.IsTrue(f.Match.Return("a", retained.Progress.WallClockMs + 17));
            var returned = f.State;
            Assert.IsTrue(returned.Progress.Players.Single(p => p.Id == "a").Returned);
            Assert.IsNull(returned.Economy.Players[0].Craft);
            Assert.AreEqual(0, returned.Economy.Players[0].LetterCount);
            Assert.AreEqual(retained.Economy.Items.Count, returned.Economy.Items.Count);
            Assert.AreEqual(retained.Economy.Tiles.Count + 3, returned.Economy.Tiles.Count);
            Assert.AreEqual(retained.Economy.Minted, returned.Economy.Minted);
            Assert.AreEqual(retained.Economy.Spent, returned.Economy.Spent);
            Assert.AreEqual(before, MatchSnapshotCodec.Export(retained));
            f.Ack();
        }

        [Test]
        public void FreezeBoundsUnacknowledgedTimeAndRejectsWrongHashAcknowledgement()
        {
            var f = Create();
            for (int i = 0; i < 120; i++) Assert.IsTrue(f.Step().Advanced);
            Assert.IsTrue(f.Match.IsPaused);
            Assert.IsFalse(f.Step().Advanced);
            var stamp = f.Match.PublishSnapshot(out _);
            Assert.IsFalse(f.Match.Acknowledge(new SnapshotStamp(stamp.MatchId, stamp.IdentityHash, stamp.Epoch,
                stamp.Sequence, stamp.Tick, new string('a', 64))));
            Assert.IsTrue(f.Match.IsPaused);
            Assert.IsTrue(f.Match.Acknowledge(stamp));
            Assert.IsTrue(f.Step().Advanced);
        }

        [Test]
        public void MigrationRestoresOnlyCommittedStatePreservingReceiptAndReconnectDeadline()
        {
            var f = Create();
            var tile = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Tile);
            var request = f.Command("a", WordboundAction.CollectLetter, tile.Id);
            f.Step(request, new[] { f.Pose("a", tile.Position) });
            f.Match.Disconnect("b", 1000);
            var stamp = f.Match.PublishSnapshot(out string json);
            var saved = f.Source.Import(json);
            Assert.IsTrue(saved.Success, saved.Error);
            f.Step();
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(saved.Snapshot, stamp, 1, 2000);
            Assert.AreEqual(1, f.Match.Epoch);
            Assert.AreEqual(stamp.Tick, f.Match.Tick);
            Assert.AreEqual(121000, f.State.Progress.Players.Single(p => p.Id == "b").ReconnectDeadlineMs);
            var repeated = new AuthenticatedCommand("a", new PlayerCommand(request.Command.Sequence, 1,
                request.Command.Action, request.Command.Target));
            var receipt = f.Step(repeated).Receipts.Single();
            Assert.AreEqual(1, receipt.Tick);
            Assert.AreEqual(1, f.State.Economy.Players[0].LetterCount);
            Assert.IsFalse(f.Match.Reconnect("b", 121000));
            f.Ack();
        }

        [Test]
        public void MigrationRejectsAWellFormedButUncommittedSnapshotWithoutMutation()
        {
            var f = Create();
            var stamp = f.Match.PublishSnapshot(out _);
            f.Step();
            var provisional = f.Match.Capture(stamp.Sequence);
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.RestoreCommitted(provisional, stamp, 1, 2000));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void ReplayedMigrationCannotReuseAnEpochOrRollBackProcessedCommands()
        {
            var f = Create();
            var stamp = f.Match.PublishSnapshot(out string json);
            var saved = f.Source.Import(json).Snapshot;
            f.Match.RestoreCommitted(saved, stamp, 1, 1000);
            f.Step(f.Command("a", WordboundAction.CancelCraft));
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.RestoreCommitted(saved, stamp, 1, 2000));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void HostCallbacksCannotPublishOrCaptureAPartiallyAppliedTick()
        {
            var f = Create();
            string item = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Deploy, slot: 0));
            var stamp = f.Match.PublishSnapshot(out _);
            var completed = f.State;
            bool observed = false;
            f.World.DuringQuery = () =>
            {
                observed = true;
                Assert.Throws<InvalidOperationException>(() => f.Match.Capture(99));
                Assert.Throws<InvalidOperationException>(() => f.Match.MatchesTickPresenceAndResult(completed));
                Assert.Throws<InvalidOperationException>(() => f.Match.MatchesTickPresenceAndResult(null));
                Assert.Throws<InvalidOperationException>(() => f.Match.PublishSnapshot(out _));
                Assert.IsFalse(f.Match.Acknowledge(stamp));
            };
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item)).Receipts.Single().Effect.Code);
            Assert.IsTrue(observed);
            Assert.IsTrue(f.Match.Acknowledge(stamp));
            f.Ack();
        }

        [TestCase("TeamRace", false), TestCase("BrawlRace", true), TestCase("FreeForAll", true), TestCase("Freestyle", false)]
        public void CombatUsesModePolicyAndRespawnProtection(string mode, bool damageExpected)
        {
            var f = Create(mode);
            f.Step(poses: new[] { f.Pose("a", default), f.Pose("b", new WorldVector(1, 0, 0)) },
                damage: new[] { new HostDamage("a", "player:1", 100) });
            Assert.AreEqual(f.Source.Rules.MaxHealth, f.State.Actors[1].Health);
            f.Advance(120);
            f.Step(damage: new[] { new HostDamage("a", "player:1", 100, 60) });
            Assert.AreEqual(damageExpected, f.State.Progress.Players[1].Eliminated);
            Assert.AreEqual(mode == "FreeForAll" ? 1 : 0, f.State.Progress.Players[0].Knockouts);
            f.Ack();
        }

        [Test]
        public void PartialSupplyRecoveryReusesRemainingTilesAndMintsOnlyMissingLetters()
        {
            var f = Create();
            var source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            var original = f.State.Bodies.Where(b => b.SourceId == source.Id).ToArray();
            f.Collect("a", original[0].Id);
            f.Advance(601);
            var recovered = f.State.Bodies.Where(b => b.SourceId == source.Id && b.Active).ToArray();
            Assert.AreEqual(3, recovered.Length);
            Assert.AreEqual(2, recovered.Count(b => original.Any(o => o.Id == b.Id)));
            Assert.IsTrue(recovered.All(b => b.RecoveryGeneration == 3));
            Assert.AreEqual(3, f.State.Resources.Single(r => r.SourceId == source.Id && r.Lap == 0).Generation);
            Assert.AreEqual(1, f.State.Economy.Players[0].LetterCount);
            f.Ack();
        }

        [Test]
        public void MidCraftMigrationPreservesReservationAndCreatesExactlyOneItem()
        {
            var f = Create();
            var source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == source.Id).ToArray()) f.Collect("a", tile.Id);
            var request = f.Command("a", WordboundAction.BeginCraft, value: "BED");
            f.Step(request);
            var stamp = f.Match.PublishSnapshot(out string json);
            var snapshot = f.Source.Import(json).Snapshot;
            double ready = snapshot.Economy.Players[0].Craft.ReadyAt;
            f.Advance(10);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(snapshot, stamp, 1, f.State.Progress.WallClockMs + 2000);
            Assert.AreEqual(ready, f.State.Economy.Players[0].Craft.ReadyAt);
            var replay = new AuthenticatedCommand("a", new PlayerCommand(request.Command.Sequence, 1,
                request.Command.Action, value: request.Command.Value));
            Assert.AreEqual("Ok", f.Step(replay).Receipts.Single().Effect.Code);
            f.Advance(61);
            Assert.AreEqual(1, f.State.Economy.Items.Count(i => i.Origin == ItemOrigin.Crafted));
            Assert.IsNull(f.State.Economy.Players[0].Craft);
            Assert.AreEqual(0, f.State.Economy.Players[0].LetterCount);
            f.Ack();
        }

        [Test]
        public void ThreeLapRaceKeepsStaticGeometryAndConfirmsOnlyTheResultSnapshot()
        {
            var f = Create();
            var keepsake = f.State.Bodies.Single(b => b.Kind == WorldBodyKind.Keepsake);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.TakeKeepsake),
                new[] { f.Pose("a", keepsake.Position) }).Receipts.Single().Effect.Code);
            var platformIds = f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Platform).Select(b => b.Id).ToArray();
            SnapshotStamp earlier = null;
            for (int lap = 0; lap < 3; lap++)
            {
                foreach (var checkpoint in f.Source.Course.Laps[lap].Checkpoints)
                {
                    foreach (string puzzleId in checkpoint.PuzzleIds)
                    {
                        var puzzle = f.Source.Course.Puzzles.Single(p => p.Id == puzzleId);
                        string sourceId = f.Source.Course.Supplies.Single(s => s.SourceId == puzzle.RecoverySupplyIds[0]).Id;
                        int generation = f.State.Resources.Single(r => r.SourceId == sourceId && r.Lap == lap).Generation;
                        foreach (var tile in f.State.Bodies.Where(b => b.SourceId == sourceId && b.RecoveryGeneration == generation).ToArray())
                            Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
                        Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: puzzle.RecipeId)).Receipts.Single().Effect.Code);
                        f.Advance(61);
                        var item = f.State.Economy.Items.Last(i => i.Origin == ItemOrigin.Crafted && i.Word == puzzle.RecipeId);
                        Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
                        Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, puzzle.Id, value: "item:" + item.Id)).Receipts.Single().Effect.Code);
                    }
                    var module = f.Source.Course.Geometry.Single(m => m.Id == checkpoint.ModuleId);
                    f.Step(poses: new[] { f.Pose("a", new WorldVector(module.Position.X / 1000f, module.Position.Y / 1000f, module.Position.Z / 1000f)) },
                        observations: new[] { new ProgressEvent(ProgressEventKind.CheckpointReached, "a", checkpoint.Id, lap) });
                }
                earlier = f.Match.PublishSnapshot(out _);
                f.Step(observations: new[] { new ProgressEvent(ProgressEventKind.FinishReached, "a", f.Source.Course.Laps[lap].FinishModuleId, lap) });
                Assert.AreEqual(lap + 1, f.State.Progress.Teams[0].Lap);
                if (lap < 2) f.Ack();
                if (lap == 0)
                {
                    Assert.IsTrue(f.State.Resources.Where(r => r.Lap == 0).All(r => r.RecoveryAtTick == -1));
                    f.Advance(601);
                    f.Ack();
                }
            }
            Assert.AreEqual("Win", f.State.Progress.Result.Outcome);
            Assert.IsFalse(f.State.Progress.Result.Confirmed);
            var unconfirmed = f.State;
            string retainedResult = MatchSnapshotCodec.Export(unconfirmed);
            Assert.IsTrue(f.Match.MatchesTickPresenceAndResult(unconfirmed));
            Assert.IsTrue(f.Match.Acknowledge(earlier));
            Assert.IsFalse(f.State.Progress.Result.Confirmed);
            Assert.IsTrue(f.Match.MatchesTickPresenceAndResult(unconfirmed), "An acknowledgement before the result must not change its publication.");
            CollectionAssert.AreEqual(platformIds, f.State.Bodies.Where(b => b.Kind == WorldBodyKind.Platform).Select(b => b.Id).ToArray());
            f.Ack();
            var confirmed = f.State;
            Assert.IsTrue(confirmed.Progress.Result.Confirmed);
            Assert.AreEqual(unconfirmed.Progress.Tick, confirmed.Progress.Tick);
            Assert.IsFalse(f.Match.MatchesTickPresenceAndResult(unconfirmed), "Durable result confirmation must refresh the host frame without another tick.");
            Assert.IsTrue(f.Match.MatchesTickPresenceAndResult(confirmed));
            Assert.AreEqual(retainedResult, MatchSnapshotCodec.Export(unconfirmed), "The previously published frame must remain unchanged.");
            f.Ack();
            Assert.IsTrue(f.Match.MatchesTickPresenceAndResult(confirmed), "Repeated durable confirmation must not trigger another frame refresh.");
            Assert.IsFalse(f.Step().Advanced);
        }

        [Test]
        public void DisconnectDropsKeepsakeAndRecoversItOnceWhilePreservingTheSeat()
        {
            var f = Create();
            var keepsake = f.State.Bodies.Single(b => b.Kind == WorldBodyKind.Keepsake);
            f.Step(f.Command("a", WordboundAction.TakeKeepsake), new[] { f.Pose("a", keepsake.Position) });
            f.Match.Disconnect("a", 1000);
            f.Advance(601);
            Assert.AreEqual(1, f.State.Progress.Teams[0].RecoveryGeneration);
            Assert.IsNull(f.State.Progress.Teams[0].KeepsakeCarrier);
            Assert.AreEqual(1, f.State.Bodies.Single(b => b.Kind == WorldBodyKind.Keepsake).RecoveryGeneration);
            Assert.IsTrue(f.Match.Reconnect("a", f.State.Progress.WallClockMs + 1));
            Assert.AreEqual("a", f.State.Bodies.Single(b => b.Id == "player:0").SourceId);
            f.Ack();
        }
    }
}
