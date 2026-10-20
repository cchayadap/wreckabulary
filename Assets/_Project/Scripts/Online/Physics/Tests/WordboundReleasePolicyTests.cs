using System;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundReleasePolicyTests
    {
        static WorldBodyState Actor(float yaw = 0)
        {
            double angle = yaw * Math.PI / 360;
            return new WorldBodyState("player:0", WorldBodyKind.Player, default,
                new WorldVector(0, (float)Math.Sin(angle), 0), (float)Math.Cos(angle));
        }

        [Test]
        public void FullFurnitureFootprintClearsPlayerRadiusForEveryDropCandidate()
        {
            var candidates = WordboundReleasePolicy.Candidates(Actor(), new WorldVector(3, 2, 1), .35f, false);
            Assert.AreEqual(3, candidates.Count);
            foreach (var point in candidates)
            {
                double distance = Math.Sqrt(point.X * point.X + point.Z * point.Z);
                double extent = Math.Abs(point.X / distance) * 1.5 + Math.Abs(point.Z / distance) * .5;
                Assert.AreEqual(.35 + extent + .15, distance, .000001);
                Assert.AreEqual(.08f, point.Y);
            }
            Assert.AreEqual(0, candidates[0].X);
            Assert.Greater(candidates[1].X, 0); Assert.Less(candidates[2].X, 0);
        }

        [Test]
        public void ThrowUsesOnlyTheHostForwardCandidateAtHandHeight()
        {
            var candidates = WordboundReleasePolicy.Candidates(Actor(90), new WorldVector(3, 2, 1), .35f, true);
            Assert.AreEqual(1, candidates.Count);
            Assert.AreEqual(1, candidates[0].X, .000001);
            Assert.AreEqual(0, candidates[0].Z, .000001);
            Assert.AreEqual(.75f, candidates[0].Y);
        }

        [TestCase(0), TestCase(-1), TestCase(11)]
        public void InvalidColliderDimensionsAreRejected(float width) =>
            Assert.Throws<ArgumentException>(() => WordboundReleasePolicy.Candidates(Actor(), new WorldVector(width, 1, 1), .35f, false));
    }
}
