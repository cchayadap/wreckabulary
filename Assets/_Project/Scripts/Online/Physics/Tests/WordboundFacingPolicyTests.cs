using NUnit.Framework;
using Wreckabulary.Online;

namespace Wreckabulary.Tests
{
    public sealed class WordboundFacingPolicyTests
    {
        [TestCase(float.NaN, true)]
        [TestCase(float.PositiveInfinity, true)]
        [TestCase(float.NegativeInfinity, true)]
        [TestCase(-180.001f, true)]
        [TestCase(180.001f, true)]
        [TestCase(float.NaN, false)]
        public void InvalidFacingCannotCarryMovementOrJump(float yaw, bool enabled)
        {
            var input = new WordboundMovementInput
                { Epoch = 3, MoveX = 1, Jump = true, HasFacing = enabled, FacingYaw = yaw };
            Assert.IsFalse(WordboundPhysicsPolicy.TryMovement(input, 3, out var direction));
            Assert.AreEqual(0, direction.DistanceSquared(default));
        }

        [TestCase(-180f)]
        [TestCase(-90f)]
        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void BoundedFacingCanBeSubmittedWhileStationaryAndRetainsEpochChecks(float yaw)
        {
            var input = new WordboundMovementInput { Epoch = 3, HasFacing = true, FacingYaw = yaw };
            Assert.IsTrue(WordboundPhysicsPolicy.TryMovement(input, 3, out var direction));
            Assert.AreEqual(0, direction.DistanceSquared(default));
            Assert.IsFalse(WordboundPhysicsPolicy.TryMovement(input, 2, out _));
        }

        [Test]
        public void ExistingDefaultInputKeepsMovementFacingAndDiagonalClamp()
        {
            var input = new WordboundMovementInput { Epoch = 3, MoveX = 1, MoveZ = 1 };
            Assert.IsFalse(input.HasFacing);
            Assert.IsTrue(WordboundPhysicsPolicy.TryMovement(input, 3, out var direction));
            Assert.That(direction.DistanceSquared(default), Is.EqualTo(1).Within(.00001f));
        }
    }
}
