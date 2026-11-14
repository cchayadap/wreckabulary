using System;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPhysicsPolicyTests
    {
        static MatchBinding Binding() => new MatchBinding("match", "1", new string('a', 64), new string('b', 64),
            new string('c', 64), new string('d', 64), new string('e', 64),
            new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) });

        [TestCase(float.NaN, 0f), TestCase(float.PositiveInfinity, 0f), TestCase(0f, float.NegativeInfinity)]
        [TestCase(1.001f, 0f), TestCase(0f, -1.001f)]
        public void MovementRejectsUnboundedAxes(float x, float z)
        {
            Assert.IsFalse(WordboundPhysicsPolicy.TryMovement(new WordboundMovementInput
                { Epoch = 2, MoveX = x, MoveZ = z }, 2, out _));
        }

        [Test]
        public void OldEpochMovementCannotActAndDiagonalCannotExceedSpeed()
        {
            var input = new WordboundMovementInput { Epoch = 1, MoveX = 1, MoveZ = 1, Jump = true };
            Assert.IsFalse(WordboundPhysicsPolicy.TryMovement(input, 2, out _));
            Assert.IsTrue(WordboundPhysicsPolicy.TryMovement(input, 1, out var direction));
            Assert.That(direction.DistanceSquared(default), Is.EqualTo(1).Within(.00001f));
            Assert.AreEqual(0, direction.Y);
        }

        [TestCase("player:00", "a", 0), TestCase("player:2", "a", 0)]
        [TestCase("player:0", "b", 0), TestCase("player:0", "a", 1)]
        public void PlayerIdentityCannotClaimAnotherSignedSeat(string id, string actor, int team)
        {
            var body = new WorldBodyState(id, WorldBodyKind.Player, default, ownerTeam: team, sourceId: actor);
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.Actor(body, Binding()));
        }

        [Test]
        public void PlayerIdentityBindsTheCanonicalGlobalSeatIndex()
        {
            var body = new WorldBodyState("player:1", WorldBodyKind.Player, default, ownerTeam: 1, sourceId: "b");
            Assert.AreEqual("b", WordboundPhysicsPolicy.Actor(body, Binding()));
            Assert.AreEqual("", WordboundPhysicsPolicy.Actor(new WorldBodyState("item:1", WorldBodyKind.Item, default), Binding()));
        }

        [Test]
        public void RecoveryRegistrationAllowsAnAbsentSeatAndRequiresAdmissionForAnActiveSeat()
        {
            var state = new WorldBodyState("player:1", WorldBodyKind.Player, default, active: false, ownerTeam: 1, sourceId: "b");
            Assert.AreEqual("b", WordboundPhysicsPolicy.ValidateAdmission(state, Binding(), false, null));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateAdmission(state.At(default, active: true), Binding(), false, null));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateAdmission(state, Binding(), true, "a"));
            Assert.AreEqual("b", WordboundPhysicsPolicy.ValidateAdmission(state.At(default, active: true), Binding(), true, "b"));
        }

        [Test]
        public void LooseBodiesCannotAcquireAPlayersInputAuthority()
        {
            var state = new WorldBodyState("item:1", WorldBodyKind.Item, default);
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateAdmission(state, Binding(), true, "a"));
            Assert.AreEqual("", WordboundPhysicsPolicy.ValidateAdmission(state, Binding(), false, null));
        }

        [Test]
        public void RestoredLedgerPreservesExactFloatValuesUntilAnActualHostSimulationStep()
        {
            var source = new WorldBodyState("item:7", WorldBodyKind.Item,
                new WorldVector(-0f, .10000001f, 12.125f), new WorldVector(0, .38268343f, 0), .9238795f,
                new WorldVector(1.234567f, -0f, .33333334f), new WorldVector(.125f, .25f, .5f),
                sleeping: true, kinematic: true, ownerTeam: 1, protectedResource: true, sourceId: "source", recoveryGeneration: 4);
            var restored = WordboundPhysicsPolicy.Capture(source, false, default, default, 1, default, default, false);
            Assert.AreSame(source, restored);
            var simulated = WordboundPhysicsPolicy.Capture(source, true, new WorldVector(5, 6, 7), default, 1, default, default, false);
            Assert.AreEqual(source.Id, simulated.Id); Assert.AreEqual(source.Kind, simulated.Kind);
            Assert.AreEqual(source.SourceId, simulated.SourceId); Assert.AreEqual(source.OwnerTeam, simulated.OwnerTeam);
            Assert.AreEqual(source.Protected, simulated.Protected); Assert.AreEqual(source.RecoveryGeneration, simulated.RecoveryGeneration);
            Assert.AreEqual(source.Active, simulated.Active); Assert.AreEqual(source.Kinematic, simulated.Kinematic);
            Assert.AreEqual(5, simulated.Position.X); Assert.IsFalse(simulated.Sleeping);
        }

        [Test]
        public void InvalidHostPoseAndIdentityReplacementFailBeforeApplication()
        {
            var source = new WorldBodyState("item:1", WorldBodyKind.Item, default);
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateUpdate(source,
                new WorldBodyState("item:2", WorldBodyKind.Item, default)));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateUpdate(source,
                new WorldBodyState("item:1", WorldBodyKind.Tile, default)));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.Capture(source, true,
                new WorldVector(float.NaN, 0, 0), default, 1, default, default, false));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.Capture(source, true,
                default, default, 1, new WorldVector(1001, 0, 0), default, false));
        }

        [Test]
        public void TeamRacePreventsRivalPhysicalInterferenceAndBrawlProtectsCriticalResources()
        {
            var player0 = new WorldBodyState("player:0", WorldBodyKind.Player, default, ownerTeam: 0, sourceId: "a");
            var player1 = new WorldBodyState("player:1", WorldBodyKind.Player, default, ownerTeam: 1, sourceId: "b");
            var rivalItem = new WorldBodyState("item:1", WorldBodyKind.Item, default, ownerTeam: 1);
            var protectedTile = new WorldBodyState("tile:1", WorldBodyKind.Tile, default, ownerTeam: 1, protectedResource: true);
            var clean = ModeDefinition.For("TeamRace");
            var brawl = ModeDefinition.For("BrawlRace");
            Assert.IsFalse(WordboundPhysicsPolicy.ShouldCollide(clean, player0, player1));
            Assert.IsFalse(WordboundPhysicsPolicy.ShouldCollide(clean, player0, rivalItem));
            Assert.IsTrue(WordboundPhysicsPolicy.ShouldCollide(brawl, player0, player1));
            Assert.IsTrue(WordboundPhysicsPolicy.ShouldCollide(brawl, player0, rivalItem));
            Assert.IsFalse(WordboundPhysicsPolicy.ShouldCollide(brawl, player0, protectedTile));
            Assert.IsTrue(WordboundPhysicsPolicy.ShouldCollide(brawl, player1, protectedTile));
            Assert.IsFalse(WordboundPhysicsPolicy.ShouldCollide(brawl, player0, rivalItem.At(default, active: false)));
        }

        [TestCase(ItemState.Held, false, true), TestCase(ItemState.Carried, false, true)]
        [TestCase(ItemState.Deployed, true, true), TestCase(ItemState.Thrown, true, false)]
        [TestCase(ItemState.World, true, false), TestCase(ItemState.Destroyed, false, false)]
        public void EconomyModesHaveConsistentCollisionAndSimulationFlags(ItemState state, bool active, bool kinematic)
        {
            var body = new WorldBodyState("item:1", WorldBodyKind.Item, default, active: active, kinematic: kinematic);
            Assert.DoesNotThrow(() => WordboundPhysicsPolicy.ValidateItemMode(body, state));
            Assert.Throws<ArgumentException>(() => WordboundPhysicsPolicy.ValidateItemMode(body.At(default, active: !active), state));
        }
    }
}
