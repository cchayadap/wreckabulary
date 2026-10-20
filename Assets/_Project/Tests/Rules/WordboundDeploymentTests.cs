using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundDeploymentTests
    {
        static WordboundAuthorityTests.Fixture Deploy(out string id, out AuthenticatedCommand command, float lifetime = .125f)
        {
            var f = WordboundAuthorityTests.Create();
            f.Source.Catalogue.Get("BED").Deploy.LifetimeSeconds = lifetime;
            id = f.CraftBed();
            command = f.Command("a", WordboundAction.Deploy, slot: 0);
            Assert.AreEqual("Ok", f.Step(command).Receipts.Single().Effect.Code);
            return f;
        }
        static ItemInstance Item(MatchSnapshot state, string id) => state.Economy.Items.Single(i => "item:" + i.Id == id);
        static long Expiry(WordboundAuthorityTests.Fixture f, string id) =>
            MatchStateValidation.DeploymentExpiryTick(f.Source.Catalogue.Get("BED"), Item(f.State, id).DeploymentTick);

        [Test]
        public void ExactExpiryBreaksOnceAndCommandReplayCannotDuplicateReturnedLetters()
        {
            var f = Deploy(out string id, out var command);
            Assert.AreEqual(f.Match.Tick, Item(f.State, id).DeploymentTick);
            long expiry = Expiry(f, id);
            int tiles = f.State.Economy.Tiles.Count;
            f.Advance((int)(expiry - f.Match.Tick - 1));
            Assert.AreEqual(ItemState.Deployed, Item(f.State, id).State);
            f.Step(command);
            Assert.AreEqual(expiry, f.Match.Tick);
            Assert.IsFalse(f.State.Economy.Items.Any(i => "item:" + i.Id == id));
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == id));
            Assert.AreEqual(tiles + 3, f.State.Economy.Tiles.Count);
            f.Step(command);
            Assert.AreEqual(tiles + 3, f.State.Economy.Tiles.Count);
            f.Ack();
        }

        [Test]
        public void FailedTickRollsBackExpirationAllocationAndLetterConservation()
        {
            var f = Deploy(out string id, out _);
            f.Advance((int)(Expiry(f, id) - f.Match.Tick - 1));
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17,
                hostMechanisms: new[] { new HostMechanismObservation("missing-puzzle", 0, 0, false) }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            int count = f.State.Economy.Tiles.Count;
            f.Step();
            Assert.AreEqual(count + 3, f.State.Economy.Tiles.Count);
            f.Ack();
        }

        [TestCase(false), TestCase(true)]
        public void MigrationBeforeOrAtExpiryPreservesOneDeadlineAndOneBreak(bool alreadyExpired)
        {
            var f = Deploy(out string id, out _);
            long expiry = Expiry(f, id);
            f.Advance((int)(expiry - f.Match.Tick - (alreadyExpired ? 0 : 1)));
            var stamp = f.Match.PublishSnapshot(out string json);
            Assert.IsTrue(f.Match.Acknowledge(stamp));
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            int tiles = imported.Snapshot.Economy.Tiles.Count;
            var restored = new AuthoritativeMatch(f.Source.Binding, f.Source.Mode, f.Source.Course,
                CourseTemplates.ProofCatalogue(), f.Source.Catalogue, f.Source.Rules, f.World);
            long now = imported.Snapshot.Progress.WallClockMs + 30000;
            restored.RestoreCommitted(imported.Snapshot, stamp, 1, now);
            Assert.AreEqual(imported.Snapshot.Progress.Tick, restored.Tick);
            restored.Advance(now + 17);
            var frame = restored.Capture(1);
            Assert.IsFalse(frame.Economy.Items.Any(i => "item:" + i.Id == id));
            Assert.IsFalse(frame.Bodies.Any(b => b.Id == id));
            Assert.AreEqual(tiles + (alreadyExpired ? 0 : 3), frame.Economy.Tiles.Count);
            restored.Advance(now + 34);
            Assert.AreEqual(frame.Economy.Tiles.Count, restored.Capture(1).Economy.Tiles.Count);
            Assert.IsTrue(f.Source.Import(MatchSnapshotCodec.Export(restored.Capture(1))).Success);
        }

        [Test]
        public void PickupClearsActivationAndRedeployStartsANewCanonicalDeadline()
        {
            var f = Deploy(out string id, out _);
            long old = Expiry(f, id);
            var body = f.State.Bodies.Single(b => b.Id == id);
            int revision = Item(f.State, id).Revision;
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.PickUp, id, revision),
                new[] { f.Pose("a", body.Position) }).Receipts.Single().Effect.Code);
            Assert.AreEqual(-1, Item(f.State, id).DeploymentTick);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            long next = Expiry(f, id);
            Assert.Greater(next, old);
            f.Advance((int)(old - f.Match.Tick));
            Assert.AreEqual(ItemState.Deployed, Item(f.State, id).State);
            f.Advance((int)(next - f.Match.Tick));
            Assert.IsFalse(f.State.Economy.Items.Any(i => "item:" + i.Id == id));
            Assert.IsFalse(f.State.Bodies.Any(b => b.Id == id));
        }

        [Test]
        public void DeploymentUsesHostFacingAndSnapshotPreservesItsOrientation()
        {
            var f = WordboundAuthorityTests.Create();
            string id = f.CraftBed();
            var player = f.State.Bodies.Single(b => b.Id == "player:0");
            float half = (float)Math.Sqrt(.5);
            var facing = new WorldBodyState(player.Id, player.Kind, player.Position,
                new WorldVector(0, half, 0), half, ownerTeam: player.OwnerTeam, sourceId: player.SourceId);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0),
                new[] { facing }).Receipts.Single().Effect.Code);
            var deployed = f.State.Bodies.Single(b => b.Id == id);
            Assert.AreEqual(half, deployed.RotationXyz.Y);
            Assert.AreEqual(half, deployed.RotationW);
            Assert.Greater(deployed.Position.X, player.Position.X + 1.49f);
            f.Ack();
        }

        [Test]
        public void PermanentPadsRemainDeployedAndLegacyEconomyRetainsUnsetTick()
        {
            var f = Deploy(out string id, out _, 0);
            Assert.AreEqual(-1, Expiry(f, id));
            f.Advance(180);
            Assert.AreEqual(ItemState.Deployed, Item(f.State, id).State);
            f.Ack();
            var legacy = new Economy(TestData.Catalogue(), TestData.RulesFor("Dibs"));
            legacy.AddPlayer(0, 0); TestData.GiveLetters(legacy, 0, "BED");
            Assert.IsTrue(legacy.BeginCraft(0, "BED", 0).Ok);
            Assert.IsTrue(legacy.CompleteCraft(0, 100).Ok);
            Assert.IsTrue(legacy.Deploy(0, 0).Ok);
            Assert.AreEqual(-1, legacy.Items.Single().DeploymentTick);
            Assert.DoesNotThrow(() => legacy.Restore(legacy.Capture()));
        }

        [TestCase("missing"), TestCase("unset"), TestCase("future"), TestCase("expired"), TestCase("numeric"), TestCase("overflow")]
        public void RehashedWireRejectsMissingOrImpossibleDeploymentTime(string mutation)
        {
            var f = Deploy(out string id, out _);
            string json = MatchSnapshotCodec.Export(f.State);
            string field = "\"deploymentTick\":\"" + Item(f.State, id).DeploymentTick + "\"";
            string replacement = mutation switch
            {
                "unset" => "\"deploymentTick\":\"-1\"",
                "future" => "\"deploymentTick\":\"" + (f.Match.Tick + 1) + "\"",
                "expired" => "\"deploymentTick\":\"1\"",
                "numeric" => "\"deploymentTick\":" + Item(f.State, id).DeploymentTick,
                "overflow" => "\"deploymentTick\":\"9223372036854775808\"",
                _ => ""
            };
            json = mutation == "missing" ? json.Replace(field + ",", "") : json.Replace(field, replacement);
            Assert.IsFalse(f.Source.Import(WordboundSnapshotCodecTests.Rehash(json)).Success, mutation);
        }

        [Test]
        public void OldSchemaAndCanonicalVersionsAreHardRejectedEvenWithFreshHashes()
        {
            var f = Deploy(out _, out _);
            string json = MatchSnapshotCodec.Export(f.State);
            Assert.AreEqual(2, MatchSnapshot.Schema);
            Assert.AreEqual(2, MatchSnapshotCodec.CanonicalVersion);
            Assert.AreEqual("wordbound-v2", MatchSnapshotCodec.ProtocolVersion);
            foreach (string changed in new[] { json.Replace("\"schema\":2", "\"schema\":1"),
                json.Replace("\"canonicalVersion\":2", "\"canonicalVersion\":1") })
                Assert.IsFalse(f.Source.Import(WordboundSnapshotCodecTests.Rehash(changed)).Success);
        }
    }
}
