using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundReturnTests
    {
        [Test]
        public void ReturnCancelsReservationDropsKeepsakeAndInventoryExactlyOnce()
        {
            var f = WordboundAuthorityTests.Create();
            var source = f.Source.Course.Supplies.Single(s => s.SourceId == "BedSupply");
            foreach (var tile in f.State.Bodies.Where(b => b.SourceId == source.Id).ToArray()) f.Collect("a", tile.Id);
            f.Step(f.Command("a", WordboundAction.BeginCraft, value: "BED"));
            var keepsake = f.State.Bodies.Single(b => b.Kind == WorldBodyKind.Keepsake);
            f.Step(f.Command("a", WordboundAction.TakeKeepsake), new[] { f.Pose("a", keepsake.Position) });
            int tilesBefore = f.State.Economy.Tiles.Count;
            Assert.IsTrue(f.Match.Return("a", 1000));
            var player = f.State.Progress.Players.Single(p => p.Id == "a");
            Assert.IsTrue(player.Returned); Assert.IsFalse(player.Connected); Assert.IsFalse(player.Eliminated);
            Assert.AreEqual(-1, player.ReconnectDeadlineMs); Assert.AreEqual(-1, player.RespawnAt);
            Assert.IsNull(f.State.Progress.Teams[0].KeepsakeCarrier);
            Assert.IsNull(f.State.Economy.Players[0].Craft);
            Assert.AreEqual(0, f.State.Economy.Players[0].LetterCount);
            Assert.AreEqual(tilesBefore + 3, f.State.Economy.Tiles.Count);
            Assert.IsFalse(f.State.Bodies.Single(b => b.Id == "player:0").Active);
            string beforeRepeat = MatchSnapshotCodec.Export(f.State);
            Assert.IsFalse(f.Match.Return("a", 2000));
            Assert.AreEqual(beforeRepeat, MatchSnapshotCodec.Export(f.State));
            f.Ack();
        }

        [Test]
        public void ReturnDropsTheSameHeldItemAndNeverRestartsItsOwner()
        {
            var f = WordboundAuthorityTests.Create();
            string itemId = f.CraftBed();
            Assert.IsTrue(f.Match.Return("a", f.State.Progress.WallClockMs + 1));
            var item = f.State.Economy.Items.Single(i => "item:" + i.Id == itemId);
            Assert.AreEqual(ItemState.World, item.State); Assert.AreEqual(-1, item.HolderId);
            Assert.IsTrue(f.State.Bodies.Single(b => b.Id == itemId).Active);
            Assert.AreEqual(1, f.State.Economy.Items.Count(i => i.Origin == ItemOrigin.Crafted));
            f.Advance(121);
            var player = f.State.Progress.Players.Single(p => p.Id == "a");
            Assert.IsTrue(player.Returned); Assert.AreEqual(0, player.RecoveryGeneration);
            Assert.IsFalse(f.Match.Reconnect("a", f.State.Progress.WallClockMs + 1));
            Assert.AreEqual("NotAlive", f.Step(f.Command("a", WordboundAction.CancelCraft)).Receipts.Single().Effect.Code);
            f.Ack();
        }

        [Test]
        public void ReturnDuringRespawnCancelsTheRespawnAndSurvivesCommittedMigration()
        {
            var f = WordboundAuthorityTests.Create();
            f.Step(poses: new[] { f.Pose("a", new WorldVector(0, -30, 0)) });
            Assert.IsTrue(f.State.Progress.Players[0].Eliminated);
            Assert.IsTrue(f.Match.Return("a", 1000));
            var stamp = f.Match.PublishSnapshot(out string json);
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            Assert.IsTrue(imported.Snapshot.Progress.Players[0].Returned);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(imported.Snapshot, stamp, 1, 2000);
            Assert.IsFalse(f.Match.Reconnect("a", 2001));
            f.Advance(121);
            Assert.IsTrue(f.State.Progress.Players[0].Returned);
            Assert.IsFalse(f.State.Progress.Players[0].Eliminated);
            Assert.AreEqual(-1, f.State.Progress.Players[0].RespawnAt);
            Assert.IsFalse(f.State.Bodies.Single(b => b.Id == "player:0").Active);
            f.Ack();
        }

        [Test]
        public void ReturnPermanentlyClosesAnExistingDisconnectGrace()
        {
            var f = WordboundAuthorityTests.Create();
            Assert.IsTrue(f.Match.Disconnect("a", 1000));
            Assert.AreEqual(121000, f.State.Progress.Players[0].ReconnectDeadlineMs);
            Assert.IsTrue(f.Match.Return("a", 2000));
            Assert.AreEqual(-1, f.State.Progress.Players[0].ReconnectDeadlineMs);
            Assert.IsFalse(f.Match.Reconnect("a", 3000));
            Assert.IsFalse(f.Match.Disconnect("a", 4000));
            f.Ack();
        }

        [Test]
        public void ReturnedWireFlagCannotCoexistWithAConnectedSeat()
        {
            var f = WordboundAuthorityTests.Create();
            string json = MatchSnapshotCodec.Export(f.State).Replace("\"returned\":false", "\"returned\":true");
            var result = f.Source.Import(WordboundSnapshotCodecTests.Rehash(json));
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.Snapshot);
        }
    }
}
