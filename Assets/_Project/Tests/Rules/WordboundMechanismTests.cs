using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundMechanismTests
    {
        static WordboundAuthorityTests.Fixture DeployedBed(out string item)
        {
            var f = WordboundAuthorityTests.Create();
            item = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Deploy, slot: 0));
            return f;
        }

        [Test]
        public void UnsolvedMechanismPhaseSurvivesMigrationAndCompletionPreservesItsStart()
        {
            var f = DeployedBed(out string item);
            long start = f.Match.Tick + 1;
            for (int i = 1; i <= 20; i++)
                f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { new HostMechanismObservation("BedBridge", 0, 0, true, start, i, item) });
            var stamp = f.Match.PublishSnapshot(out string json);
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            var before = imported.Snapshot.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0);
            Assert.IsFalse(before.Solved);
            Assert.AreEqual(start, before.ActivatedTick);
            Assert.AreEqual(20, before.ProgressTicks);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(imported.Snapshot, stamp, 1, f.State.Progress.WallClockMs + 1000);
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, true, start, 21, item) });
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item)).Receipts.Single().Effect.Code);
            var after = f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0);
            Assert.IsTrue(after.Solved);
            Assert.AreEqual(start, after.ActivatedTick);
            Assert.AreEqual(start, after.PhaseTick);
            Assert.AreEqual(21, after.ProgressTicks);
            f.Ack();
        }

        [TestCase("future"), TestCase("backdated"), TestCase("progress"), TestCase("team"), TestCase("lap"), TestCase("puzzle")]
        public void InvalidMechanismObservationRejectsTheWholeHostFrame(string mutation)
        {
            var f = DeployedBed(out string item);
            long tick = f.Match.Tick + 1;
            var observation = new HostMechanismObservation(mutation == "puzzle" ? "Unknown" : "BedBridge",
                mutation == "team" ? 1 : 0, mutation == "lap" ? 1 : 0, true,
                mutation == "future" ? tick + 1 : mutation == "backdated" ? tick - 1 : tick,
                mutation == "progress" ? 2 : 1, item);
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17,
                hostMechanisms: new[] { observation }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void MechanismResetClearsPartialProgressAndRequiresANewCausalPhase()
        {
            var f = DeployedBed(out string item);
            long start = f.Match.Tick + 1;
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, true, start, 1, item) });
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, false) });
            var reset = f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0);
            Assert.AreEqual(-1, reset.ActivatedTick); Assert.AreEqual(0, reset.ProgressTicks);
            Assert.AreEqual("", reset.ItemEntityId);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, true, start, 1, item) }));
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, true, f.Match.Tick + 1, 1, item) });
            f.Ack();
        }

        [Test]
        public void KinematicPlatformPoseRestoresExactlyWithoutChangingAuthorityMetadata()
        {
            var f = WordboundAuthorityTests.Create();
            var platform = f.State.Bodies.First(b => b.Kind == WorldBodyKind.Platform);
            var input = new WorldBodyState(platform.Id, platform.Kind, new WorldVector(1.25f, 2.5f, 3.75f),
                velocity: new WorldVector(0, 1.25f, 0), ownerTeam: 1, sourceId: "forged", protectedResource: true);
            f.Step(poses: new[] { input });
            var moved = f.State.Bodies.Single(b => b.Id == platform.Id);
            Assert.IsTrue(moved.Kinematic); Assert.IsFalse(moved.Protected);
            Assert.AreEqual(-1, moved.OwnerTeam); Assert.AreEqual(platform.SourceId, moved.SourceId);
            var stamp = f.Match.PublishSnapshot(out string json);
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(imported.Snapshot, stamp, 1, 1000);
            var restored = f.State.Bodies.Single(b => b.Id == platform.Id);
            Assert.AreEqual(2.5f, restored.Position.Y);
            Assert.AreEqual(1.25f, restored.Velocity.Y);
            Assert.IsTrue(restored.Kinematic);
        }

        [Test]
        public void RemovingADeployedMechanismItemClearsItsUnsolvedTimer()
        {
            var f = DeployedBed(out string itemId);
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
            { new HostMechanismObservation("BedBridge", 0, 0, true, f.Match.Tick + 1, 1, itemId) });
            int revision = f.State.Economy.Items.Single(i => "item:" + i.Id == itemId).Revision;
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.PickUp, itemId, revision)).Receipts.Single().Effect.Code);
            var state = f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0);
            Assert.IsFalse(state.Solved);
            Assert.AreEqual(-1, state.ActivatedTick);
            Assert.AreEqual(0, state.ProgressTicks);
            Assert.AreEqual("", state.ItemEntityId);
            f.Ack();
        }

        [Test]
        public void SettledThrowRemainsOneItemAndCanBePickedUpWithTheNewRevision()
        {
            var f = WordboundAuthorityTests.Create();
            string itemId = f.CraftBed();
            f.Step(f.Command("a", WordboundAction.Throw, slot: 0));
            var thrown = f.State.Economy.Items.Single(i => "item:" + i.Id == itemId);
            Assert.AreEqual(ItemState.Thrown, thrown.State);
            Assert.AreEqual(0, thrown.ThrowerId);
            var body = f.State.Bodies.Single(b => b.Id == itemId);
            var sleeping = new WorldBodyState(body.Id, body.Kind, body.Position, sleeping: true);
            f.Step(poses: new[] { sleeping });
            var settled = f.State.Economy.Items.Single(i => i.Id == thrown.Id);
            Assert.AreEqual(ItemState.World, settled.State);
            Assert.AreEqual(-1, settled.ThrowerId);
            Assert.IsNull(settled.ThrowerSkin);
            Assert.AreEqual(thrown.Revision + 1, settled.Revision);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.PickUp, itemId, settled.Revision)).Receipts.Single().Effect.Code);
            Assert.AreEqual(1, f.State.Economy.Items.Count(i => i.Origin == ItemOrigin.Crafted));
            f.Ack();
        }
    }
}
