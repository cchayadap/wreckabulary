using System;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundFieldPolicyTests
    {
        static DeployStats Wind() => new DeployStats { Effect = DeployEffect.WindField, Radius = 3.2f, ArcDegrees = 90, Strength = 8 };
        static DeployStats Clock() => new DeployStats { Effect = DeployEffect.SlowField, Radius = 2.75f, ArcDegrees = 360, Strength = .6f };
        static WorldBodyState Source(float yaw = 0)
        {
            double half = yaw * Math.PI / 360;
            return new WorldBodyState("item:1", WorldBodyKind.Item, default,
                new WorldVector(0, (float)Math.Sin(half), 0), (float)Math.Cos(half), kinematic: true);
        }
        static WorldBodyState Target(float x, float y, float z, bool active = true, bool kinematic = false) =>
            new WorldBodyState("player:0", WorldBodyKind.Player, new WorldVector(x, y, z), active: active, kinematic: kinematic);

        [TestCase(0, 0, 3, true), TestCase(0, 0, 3.21f, false), TestCase(0, 1.26f, 1, false),
            TestCase(0, 0, -1, false), TestCase(2, 0, 0, false)]
        public void WindRequiresCatalogueRadiusVerticalBandAndForwardCone(float x, float y, float z, bool expected) =>
            Assert.AreEqual(expected, WordboundFieldPolicy.Contains(Wind(), Source(), Target(x, y, z)));

        [Test]
        public void CanonicalYawTurnsWindAndClockRemainsOmnidirectional()
        {
            Assert.IsTrue(WordboundFieldPolicy.Contains(Wind(), Source(90), Target(2, 0, 0)));
            Assert.IsFalse(WordboundFieldPolicy.Contains(Wind(), Source(90), Target(0, 0, 2)));
            Assert.IsTrue(WordboundFieldPolicy.Contains(Clock(), Source(), Target(0, 0, -2)));
            Assert.IsFalse(WordboundFieldPolicy.Contains(Clock(), Source(), Target(0, 0, 1, active: false)));
            Assert.IsFalse(WordboundFieldPolicy.Contains(Clock(), Source(), Target(0, 0, 1, kinematic: true)));
        }

        [Test]
        public void NextTickDeadlineExcludesExactExpiryAndHasNoWallClockState()
        {
            var definition = new ItemDefinition { Deploy = Clock() };
            definition.Deploy.LifetimeSeconds = 10;
            Assert.IsFalse(WordboundFieldPolicy.ActiveNextTick(definition, 100, 99));
            Assert.IsTrue(WordboundFieldPolicy.ActiveNextTick(definition, 100, 100));
            Assert.IsTrue(WordboundFieldPolicy.ActiveNextTick(definition, 100, 698));
            Assert.IsFalse(WordboundFieldPolicy.ActiveNextTick(definition, 100, 699));
            Assert.IsFalse(WordboundFieldPolicy.ActiveNextTick(definition, 100, 700));
            Assert.IsFalse(WordboundFieldPolicy.ActiveNextTick(definition, -1, 100));
            definition.Deploy.LifetimeSeconds = 0;
            Assert.IsTrue(WordboundFieldPolicy.ActiveNextTick(definition, 100, 9007199254740993));
        }

        [Test]
        public void OverlappingClocksUseOneMinimumScaleRatherThanCompoundingDrag()
        {
            var slow = WordboundFieldPolicy.Environment(Clock());
            var both = WordboundMovementEnvironment.Neutral.Combine(slow).Combine(slow);
            Assert.AreEqual(.6f, both.SpeedMultiplier);
            Assert.AreEqual(1, both.AccelerationMultiplier);
            both = both.Combine(WordboundFieldPolicy.Environment(Wind()));
            Assert.AreEqual(.6f, both.SpeedMultiplier);
            Assert.AreEqual(.1f, both.AccelerationMultiplier);
            Assert.AreEqual(1, WordboundMovementEnvironment.Neutral.SpeedMultiplier);
        }

        [Test]
        public void WindAcceleratesAtSixtyHertzCapsForwardSpeedAndPreservesVerticalMotion()
        {
            var stats = Wind();
            var velocity = WordboundFieldPolicy.WindVelocity(stats, Source(), new WorldVector(0, 5, 0));
            Assert.AreEqual(8f / 60, velocity.Z, .000001f); Assert.AreEqual(5, velocity.Y);
            velocity = WordboundFieldPolicy.WindVelocity(stats, Source(), new WorldVector(0, 5, 7.99f));
            Assert.AreEqual(8, velocity.Z, .000001f);
            velocity = WordboundFieldPolicy.WindVelocity(stats, Source(), new WorldVector(0, 5, 10));
            Assert.AreEqual(10, velocity.Z);
            velocity = WordboundFieldPolicy.WindVelocity(stats, Source(90), default);
            Assert.Greater(velocity.X, .13f); Assert.AreEqual(0, velocity.Z, .000001f);
        }

        [TestCase(0), TestCase(-1), TestCase(1.01f), TestCase(float.NaN), TestCase(float.PositiveInfinity)]
        public void InvalidMovementScalesAreRejected(float scale)
        {
            Assert.Throws<ArgumentException>(() => new WordboundMovementEnvironment(scale, 1));
            Assert.Throws<ArgumentException>(() => new WordboundMovementEnvironment(1, scale));
            Assert.Throws<ArgumentException>(() => default(WordboundMovementEnvironment).Validate());
        }

        [Test]
        public void UnsupportedOrUnboundedFieldDataFailsClosed()
        {
            var stats = Clock(); stats.Strength = 2;
            Assert.Throws<ArgumentException>(() => WordboundFieldPolicy.Environment(stats));
            stats = Wind(); stats.Radius = float.NaN;
            Assert.Throws<ArgumentException>(() => WordboundFieldPolicy.Contains(stats, Source(), Target(0, 0, 1)));
            stats = Wind(); stats.Effect = DeployEffect.Cover;
            Assert.Throws<ArgumentException>(() => WordboundFieldPolicy.Environment(stats));
        }
    }
}
