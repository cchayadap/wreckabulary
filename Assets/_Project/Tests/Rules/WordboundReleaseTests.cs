using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundReleaseTests
    {
        [TestCase(WordboundAction.Drop), TestCase(WordboundAction.Throw)]
        public void BlockedReleasePreservesHeldItemRevisionSlotSelectionAndPose(WordboundAction action)
        {
            var f = WordboundAuthorityTests.Create(); string id = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Equip, slot: 0));
            var before = f.State;
            f.World.Releaseable = false;
            Assert.AreEqual("BlockedRelease", f.Step(f.Command("a", action, slot: 0)).Receipts.Single().Effect.Code);
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == id);
            Assert.AreEqual(ItemState.Held, item.State);
            Assert.AreEqual(before.Economy.Items.Single(i => i.Id == item.Id).Revision, item.Revision);
            Assert.AreEqual(item.Id, f.State.Economy.Players[0].Slots[0]);
            Assert.AreEqual(0, f.State.Actors.Single(a => a.PlayerId == "a").EquippedSlot);
            Assert.IsFalse(f.State.Bodies.Single(b => b.Id == id).Active);
            Assert.AreEqual(0, before.Bodies.Single(b => b.Id == id).Position.DistanceSquared(f.State.Bodies.Single(b => b.Id == id).Position));
            f.Ack();
        }

        [Test]
        public void BlockedCarriedFurnitureStaysCarriedUntilAHostClearanceExists()
        {
            var f = WordboundAuthorityTests.Create("Freestyle");
            var body = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == body.Id);
            f.Step(f.Command("a", WordboundAction.PickUp, body.Id, item.Revision), new[] { f.Pose("a", body.Position) });
            f.World.Releaseable = false;
            Assert.AreEqual("BlockedRelease", f.Step(f.Command("a", WordboundAction.Drop)).Receipts.Single().Effect.Code);
            Assert.AreEqual(item.Id, f.State.Economy.Players[0].CarriedFurniture);
            f.World.Releaseable = true;
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Drop)).Receipts.Single().Effect.Code);
            Assert.AreEqual(-1, f.State.Economy.Players[0].CarriedFurniture);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == body.Id).Active);
            f.Ack();
        }

        [TestCase(WordboundAction.Drop), TestCase(WordboundAction.Throw)]
        public void HostSafePositionAndFacingAreCanonicalAndReplayCreatesNoSecondRelease(WordboundAction action)
        {
            var f = WordboundAuthorityTests.Create(); string id = f.CraftBed();
            var actor = f.State.Bodies.Single(b => b.Id == "player:0");
            float half = (float)Math.Sqrt(.5);
            var facing = new WorldBodyState(actor.Id, actor.Kind, actor.Position, new WorldVector(0, half, 0), half,
                ownerTeam: actor.OwnerTeam, sourceId: actor.SourceId);
            f.World.ReleaseAt = new WorldVector(actor.Position.X + 2, actor.Position.Y + .75f, actor.Position.Z);
            var command = f.Command("a", action, slot: 0);
            Assert.AreEqual("Ok", f.Step(command, new[] { facing }).Receipts.Single().Effect.Code);
            var released = f.State.Bodies.Single(b => b.Id == id);
            Assert.AreEqual(0, released.Position.DistanceSquared(f.World.ReleaseAt.Value));
            Assert.AreEqual(half, released.RotationXyz.Y);
            Assert.AreEqual(half, released.RotationW);
            Assert.AreEqual(action == WordboundAction.Throw ? 8 : 0, released.Velocity.X, .00001);
            Assert.IsTrue(released.Active); Assert.IsFalse(released.Kinematic);
            int revision = f.State.Economy.Items.Single(i => "item:" + i.Id == id).Revision;
            f.Step(command);
            Assert.AreEqual(revision, f.State.Economy.Items.Single(i => "item:" + i.Id == id).Revision);
            Assert.AreEqual(1, f.State.Bodies.Count(b => b.Id == id));
            f.Ack();
        }

        [TestCase(10001), TestCase(20)]
        public void OutOfBoundsHostPlacementCannotMoveAnItemOutsideTheReleaseDomain(float offset)
        {
            var f = WordboundAuthorityTests.Create(); string id = f.CraftBed();
            var actor = f.State.Bodies.Single(b => b.Id == "player:0");
            f.World.ReleaseAt = new WorldVector(actor.Position.X + offset, actor.Position.Y, actor.Position.Z);
            Assert.AreEqual("BlockedRelease", f.Step(f.Command("a", WordboundAction.Drop, slot: 0)).Receipts.Single().Effect.Code);
            Assert.AreEqual(ItemState.Held, f.State.Economy.Items.Single(i => "item:" + i.Id == id).State);
        }

        sealed class NoReleaseWorld : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => true;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem) => true;
        }
        [Test]
        public void MissingPhysicalReleaseCapabilityFailsClosedAfterRecovery()
        {
            var f = WordboundAuthorityTests.Create(); string id = f.CraftBed();
            var stamp = f.Match.PublishSnapshot(out string json); f.Match.Acknowledge(stamp);
            var snapshot = f.Source.Import(json).Snapshot;
            var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, new NoReleaseWorld());
            long now = snapshot.Progress.WallClockMs + 1000;
            restored.RestoreCommitted(snapshot, stamp, 1, now);
            var command = new AuthenticatedCommand("a", new PlayerCommand(f.Sequences["a"] + 1, 1, WordboundAction.Drop, slot: 0));
            var result = restored.Advance(now + 17, new[] { command });
            Assert.AreEqual("BlockedRelease", result.Receipts.Single().Effect.Code);
            Assert.AreEqual(ItemState.Held, restored.Capture(1).Economy.Items.Single(i => "item:" + i.Id == id).State);
        }
    }
}
