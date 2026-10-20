using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundThrownTests
    {
        static WordboundAuthorityTests.Fixture Launch(string mode = "FreeForAll", bool waitProtection = true)
        {
            var f = WordboundAuthorityTests.Create(mode);
            var document = mode == "FreeForAll" ? CourseTemplates.CombatArena() :
                mode == "Freestyle" ? CourseTemplates.FreestyleWorld() : CourseTemplates.RaceCircuit();
            document.Supplies.Add(new CourseSupply { Id = "BallSupply", ModuleId = "Start",
                Position = new CoursePoint(0, 0, -4000), Letters = "BALL" });
            var result = CourseResolver.Resolve(document, CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(f.Source.Mode)));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues));
            f.Source.Course = result.Manifest;
            f.Source.Binding = new MatchBinding("ball-match", MatchSnapshotCodec.ProtocolVersion, f.Source.Mode.Hash,
                result.Manifest.SourceHash, result.Manifest.Hash, new string('a', 64), new string('b', 64), f.Source.Binding.Seats);
            f.Match = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course, CourseTemplates.ProofCatalogue(),
                f.Source.Catalogue, f.Source.Rules, f.World);
            if (waitProtection) f.Advance(121);
            var supply = f.Source.Course.Supplies.First(s => s.SourceId == "BallSupply" && s.TeamIndex <= 0);
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == supply.Id).ToArray())
                Assert.AreEqual("Ok", f.Collect("a", tile.Id).Effect.Code);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BALL")).Receipts.Single().Effect.Code);
            f.Advance(91);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Throw, slot: 0),
                new[] { f.Pose("a", default), f.Pose("b", new WorldVector(0, 0, 2)) }).Receipts.Single().Effect.Code);
            f.Ack();
            return f;
        }
        static HostThrownContact Contact(WordboundAuthorityTests.Fixture f, string target = "player:1", float speed = 16,
            long tick = -1, int revision = -1, WorldVector? point = null)
        {
            var flight = f.State.ThrownFlights.Single();
            return new HostThrownContact(flight.ItemBodyId, revision < 0 ? flight.LaunchRevision : revision, target,
                point ?? f.State.Bodies.Single(b => b.Id == flight.ItemBodyId).Position, speed, tick < 0 ? f.Match.Tick + 1 : tick);
        }
        static void Hit(WordboundAuthorityTests.Fixture f, params HostThrownContact[] contacts) =>
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostThrownContacts: contacts);
        static float Health(WordboundAuthorityTests.Fixture f) => f.State.Actors.Single(a => a.PlayerId == "b").Health;
        static MatchSnapshot Flights(MatchSnapshot s, params MatchThrownFlight[] flights) =>
            new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId, s.Progress, s.Economy, s.Commands,
                s.Actors, s.Bodies, s.Resources, s.Mechanisms, s.RandomStreams, s.NarrativeFlags, flights);

        [TestCase("FreeForAll"), TestCase("BrawlRace")]
        public void CatalogueSpeedDamageAndKnockbackApplyOncePerFlightAndTarget(string mode)
        {
            var f = Launch(mode); var flight = f.State.ThrownFlights.Single();
            Assert.AreEqual(16, f.State.Bodies.Single(b => b.Id == flight.ItemBodyId).Velocity.Z);
            var contact = Contact(f);
            Hit(f, contact, contact);
            Assert.AreEqual(88, Health(f));
            Assert.AreEqual(1, f.State.ThrownFlights.Single().HitBodyIds.Count);
            Assert.Greater(f.State.Bodies.Single(b => b.Id == "player:1").Velocity.Z, 0);
            Hit(f, Contact(f));
            Assert.AreEqual(88, Health(f));
            f.Ack();
        }

        [TestCase("TeamRace"), TestCase("TimeRun"), TestCase("Freestyle")]
        public void CleanModeOrTeammateCannotTakeBallDamage(string mode)
        {
            var f = Launch(mode); Hit(f, Contact(f));
            Assert.AreEqual(100, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Single().HitBodyIds.Count);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void BallUsesBreakPowerAndCannotDamageRivalProtectedSupplies(bool protectedSupply)
        {
            var f = Launch("BrawlRace");
            var target = f.State.Bodies.First(b => b.Kind == (protectedSupply ? WorldBodyKind.Tile : WorldBodyKind.Item) &&
                b.Protected == protectedSupply && (!protectedSupply || b.OwnerTeam == 1));
            var item = f.State.Economy.Items.SingleOrDefault(i => "item:" + i.Id == target.Id);
            float before = item?.Durability ?? 0;
            int tiles = f.State.Economy.Tiles.Count;
            var flight = f.State.ThrownFlights.Single();
            var projectile = f.State.Bodies.Single(b => b.Id == flight.ItemBodyId);
            f.Match.Advance(f.State.Progress.WallClockMs + 17,
                hostBodyStates: new[] { projectile.At(target.Position, projectile.Velocity) },
                hostThrownContacts: new[] { Contact(f, target.Id, point: target.Position) });
            if (!protectedSupply) Assert.AreEqual(before - 1, f.State.Economy.Items.Single(i => i.Id == item.Id).Durability);
            Assert.AreEqual(tiles, f.State.Economy.Tiles.Count);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == target.Id).Active);
            Assert.AreEqual(protectedSupply ? 0 : 1, f.State.ThrownFlights.Single().HitBodyIds.Count);
            f.Ack();
        }

        [TestCase("speed"), TestCase("revision"), TestCase("point"), TestCase("self")]
        public void NonCausalOrNonDamagingContactsDoNotConsumeAHit(string invalid)
        {
            var f = Launch();
            Hit(f, Contact(f, invalid == "self" ? "player:0" : "player:1",
                speed: invalid == "speed" ? .5f : 16, revision: invalid == "revision" ? int.MaxValue : -1,
                point: invalid == "point" ? new WorldVector(50, 0, 50) : (WorldVector?)null));
            Assert.AreEqual(100, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Single().HitBodyIds.Count);
        }

        [Test]
        public void ContactOnSleepingStepHitsBeforeSettlementAndKeepsOneRecoverableItem()
        {
            var f = Launch(); string id = f.State.ThrownFlights.Single().ItemBodyId;
            var body = f.State.Bodies.Single(b => b.Id == id);
            var sleep = new WorldBodyState(body.Id, body.Kind, body.Position, body.RotationXyz, body.RotationW,
                sleeping: true, ownerTeam: body.OwnerTeam, protectedResource: body.Protected, sourceId: body.SourceId,
                recoveryGeneration: body.RecoveryGeneration);
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostBodyStates: new[] { sleep }, hostThrownContacts: new[] { Contact(f) });
            Assert.AreEqual(88, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Count);
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == id);
            Assert.AreEqual(ItemState.World, item.State); Assert.AreEqual(-1, item.ThrowerId);
            Assert.AreEqual(1, f.State.Bodies.Count(b => b.Id == id));
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void CommittedRecoveryPreservesFlightAndDoesNotRepeatAnEarlierHit(bool alreadyHit)
        {
            var f = Launch();
            if (alreadyHit) Hit(f, Contact(f));
            var stamp = f.Match.PublishSnapshot(out string json); Assert.IsTrue(f.Match.Acknowledge(stamp));
            var imported = f.Source.Import(json); Assert.IsTrue(imported.Success, imported.Error);
            var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course, CourseTemplates.ProofCatalogue(),
                f.Source.Catalogue, f.Source.Rules, f.World);
            restored.RestoreCommitted(imported.Snapshot, stamp, 1, f.State.Progress.WallClockMs + 30000);
            f.Match = restored;
            Hit(f, Contact(f));
            Assert.AreEqual(88, Health(f));
            Assert.AreEqual(1, f.State.ThrownFlights.Single().HitBodyIds.Count);
            f.Ack();
        }

        [Test]
        public void WrongStepContactRollsBackEarlierContactDamageAndLedger()
        {
            var f = Launch();
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => Hit(f, Contact(f), Contact(f, tick: f.Match.Tick + 2)));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            Hit(f, Contact(f)); Assert.AreEqual(88, Health(f));
        }

        [Test]
        public void SpawnProtectionRefusesContactDamageWithoutWritingAHit()
        {
            var f = Launch(waitProtection: false);
            Assert.Greater(f.State.Progress.Players.Single(p => p.Id == "b").ProtectedUntil, f.Match.Tick + 1);
            Hit(f, Contact(f));
            Assert.AreEqual(100, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Single().HitBodyIds.Count);
        }

        [Test]
        public void AlreadyLaunchedFlightDoesNotDependOnThrowersCurrentPositionOrConnection()
        {
            var f = Launch();
            f.Match.Disconnect("a", f.State.Progress.WallClockMs + 1);
            f.World.Visible = false;
            Hit(f, Contact(f));
            Assert.AreEqual(88, Health(f));
            f.Ack();
        }

        [TestCase("FreeForAll"), TestCase("BrawlRace")]
        public void LethalDisconnectedThrowCreditsOnlyTheResolvedHitAndDoesNotEnableOtherActions(string mode)
        {
            var f = Launch(mode);
            f.Step(damage: new[] { new HostDamage("a", "player:1", 90) });
            f.Match.Disconnect("a", f.State.Progress.WallClockMs + 1);
            var result = f.Match.Advance(f.State.Progress.WallClockMs + 17,
                commands: new[] { f.Command("a", WordboundAction.UsePuzzle, "BedBridge") },
                hostObservations: new[] {
                    new ProgressEvent(ProgressEventKind.PuzzleSatisfied, "a", "BedBridge"),
                    new ProgressEvent(ProgressEventKind.CheckpointReached, "a", "Begin")
                },
                hostThrownContacts: new[] { Contact(f) });
            Assert.AreEqual("NotAlive", result.Receipts.Single().Effect.Code);
            Assert.AreEqual(0, Health(f));
            Assert.IsTrue(f.State.Progress.Players.Single(p => p.Id == "b").Eliminated);
            Assert.AreEqual(mode == "FreeForAll" ? 1 : 0, f.State.Progress.Players.Single(p => p.Id == "a").Knockouts);
            if (mode == "BrawlRace")
            {
                Assert.AreEqual(0, f.State.Progress.Teams.Single(t => t.Team == 0).NextCheckpoint);
                Assert.IsEmpty(f.State.Progress.Teams.Single(t => t.Team == 0).CompletedPuzzles);
            }
            f.Ack();
        }

        [Test]
        public void PermanentReturnEndsTheFlightButPreservesTheRecoverableItem()
        {
            var f = Launch(); string id = f.State.ThrownFlights.Single().ItemBodyId;
            f.Match.Return("a", f.State.Progress.WallClockMs + 1);
            Assert.AreEqual(0, f.State.ThrownFlights.Count);
            Assert.AreEqual(ItemState.World, f.State.Economy.Items.Single(i => "item:" + i.Id == id).State);
            Assert.AreEqual(1, f.State.Bodies.Count(b => b.Id == id));
            int revision = f.State.Economy.Items.Single(i => "item:" + i.Id == id).Revision;
            Assert.IsFalse(f.Match.Return("a", f.State.Progress.WallClockMs + 1));
            Assert.AreEqual(revision, f.State.Economy.Items.Single(i => "item:" + i.Id == id).Revision);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void ExpiredDisconnectedSeatCannotKeepAnArmedFlightAcrossRecovery(bool migrate)
        {
            var f = Launch();
            f.Match.Disconnect("a", f.State.Progress.WallClockMs + 1);
            long deadline = f.State.Progress.Players.Single(p => p.Id == "a").ReconnectDeadlineMs;
            if (migrate)
            {
                var stamp = f.Match.PublishSnapshot(out string json); Assert.IsTrue(f.Match.Acknowledge(stamp));
                var saved = f.Source.Import(json); Assert.IsTrue(saved.Success, saved.Error);
                var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                    CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
                restored.RestoreCommitted(saved.Snapshot, stamp, 1, deadline);
                f.Match = restored;
            }
            else f.Match.Advance(deadline, hostThrownContacts: new[] { Contact(f) });
            Assert.AreEqual(100, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Count);
            Assert.AreEqual(ItemState.World, f.State.Economy.Items.Single(i => i.Word == "BALL").State);
            f.Ack();
        }

        [Test]
        public void DeadlineSettlesOnceWithoutSpendingOrDuplicatingBallLetters()
        {
            var f = Launch(); var flight = f.State.ThrownFlights.Single();
            long deadline = flight.StartedTick + WordboundThrownPolicy.LifetimeTicks;
            f.Advance((int)(deadline - f.Match.Tick - 1));
            Hit(f, Contact(f));
            Assert.AreEqual(100, Health(f));
            Assert.AreEqual(0, f.State.ThrownFlights.Count);
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == flight.ItemBodyId);
            Assert.AreEqual(ItemState.World, item.State); Assert.IsFalse(item.Spent);
            int revision = item.Revision; f.Step();
            Assert.AreEqual(revision, f.State.Economy.Items.Single(i => i.Id == item.Id).Revision);
            f.Ack();
        }

        [Test]
        public void PickingUpAndRethrowingStartsANewFlightAndRejectsOldCallbacks()
        {
            var f = Launch(); Hit(f, Contact(f));
            var old = f.State.ThrownFlights.Single();
            var body = f.State.Bodies.Single(b => b.Id == old.ItemBodyId);
            f.Step(poses: new[] { new WorldBodyState(body.Id, body.Kind, body.Position, body.RotationXyz, body.RotationW,
                sleeping: true, ownerTeam: body.OwnerTeam, protectedResource: body.Protected, sourceId: body.SourceId,
                recoveryGeneration: body.RecoveryGeneration) });
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == body.Id);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.PickUp, body.Id, item.Revision),
                new[] { f.Pose("a", body.Position) }).Receipts.Single().Effect.Code);
            Assert.AreEqual(0, f.State.ThrownFlights.Count);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Throw, slot: 0),
                new[] { f.Pose("a", default), f.Pose("b", new WorldVector(0, 0, 2)) }).Receipts.Single().Effect.Code);
            Assert.Greater(f.State.ThrownFlights.Single().LaunchRevision, old.LaunchRevision);
            Hit(f, Contact(f, revision: old.LaunchRevision));
            Assert.AreEqual(88, Health(f));
            Hit(f, Contact(f));
            Assert.AreEqual(76, Health(f));
            Assert.AreEqual(1, f.State.Economy.Items.Count(i => i.Word == "BALL"));
            f.Ack();
        }

        [TestCase("missing"), TestCase("duplicate"), TestCase("owner"), TestCase("future"), TestCase("target"),
         TestCase("expired"), TestCase("revision")]
        public void RehashedSnapshotsCannotInventOrDropFlightCausality(string mutation)
        {
            var f = Launch(); var s = f.State; var flight = s.ThrownFlights.Single();
            MatchThrownFlight[] changed = mutation == "missing" ? Array.Empty<MatchThrownFlight>() :
                mutation == "duplicate" ? new[] { flight, flight } :
                new[] { new MatchThrownFlight(flight.ItemBodyId, mutation == "revision" ? int.MaxValue : flight.LaunchRevision,
                    mutation == "future" ? s.Progress.Tick + 1 : mutation == "expired" ? 1 : flight.StartedTick,
                    mutation == "owner" ? "b" : flight.ThrowerId,
                    mutation == "target" ? new[] { "player:0" } : flight.HitBodyIds) };
            var result = f.Source.Import(MatchSnapshotCodec.Export(Flights(s, changed)));
            Assert.IsFalse(result.Success);
        }
    }
}
