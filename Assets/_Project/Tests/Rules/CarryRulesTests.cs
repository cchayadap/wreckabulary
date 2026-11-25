using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CarryRulesTests
    {
        [Test]
        public void AThingStraightAheadBeatsACloserOneOffToTheSide()
        {
            float ahead = CarryRules.AimScore(1f, 0f, false);
            float side = CarryRules.AimScore(.75f, 50f, false);
            Assert.Less(ahead, side);
        }

        [Test]
        public void TheNearerOfTwoThingsAheadWins()
        {
            Assert.Less(CarryRules.AimScore(.6f, 10f, false), CarryRules.AimScore(1.2f, 10f, false));
        }

        [Test]
        public void TheLastTargetStaysChosenAgainstAnAlmostEqualRival()
        {
            float kept = CarryRules.AimScore(1f, 20f, true);
            float rival = CarryRules.AimScore(.9f, 15f, false);
            Assert.Less(kept, rival);
        }

        [Test]
        public void StickinessNeverKeepsAFarTargetOverOneRightInFront()
        {
            Assert.Less(CarryRules.AimScore(.3f, 0f, false), CarryRules.AimScore(1.6f, 40f, true));
        }

        [Test]
        public void ScoresIgnoreTheSignOfTheAngle()
        {
            Assert.AreEqual(CarryRules.AimScore(1f, 30f, false), CarryRules.AimScore(1f, -30f, false), 1e-6f);
        }

        [Test]
        public void ACarriedThingWithRoomOnBothSidesDoesNotMove()
        {
            Assert.AreEqual(0f, CarryRules.SideShift(.9f, .9f, .9f), 1e-6f);
        }

        [Test]
        public void ACarriedThingSlidesAwayFromTheWallItTouches()
        {
            Assert.AreEqual(-.3f, CarryRules.SideShift(.9f, .6f, .9f), 1e-5f);
            Assert.AreEqual(.25f, CarryRules.SideShift(.9f, .9f, .65f), 1e-5f);
        }

        [Test]
        public void InATightCorridorACarriedThingCentresBetweenTheWalls()
        {
            Assert.AreEqual(-.1f, CarryRules.SideShift(.9f, .4f, .6f), 1e-5f);
            Assert.AreEqual(0f, CarryRules.SideShift(.9f, .5f, .5f), 1e-5f);
        }

        [Test]
        public void TheFirstSpotIsTheReleasePointItself()
        {
            Assert.IsTrue(CarryRules.Spot(0, 0f, 1f, .35f, out float x, out float z));
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, z);
        }

        [Test]
        public void SpotsTryInFrontFirstThenTheSidesThenBehind()
        {
            CarryRules.Spot(1, 0f, 1f, 1f, out float x, out float z);
            Assert.AreEqual(0f, x, 1e-5f); Assert.AreEqual(1f, z, 1e-5f);
            CarryRules.Spot(4, 0f, 1f, 1f, out x, out z);
            Assert.AreEqual(1f, x, 1e-5f); Assert.AreEqual(0f, z, 1e-5f);
            CarryRules.Spot(5, 0f, 1f, 1f, out x, out z);
            Assert.AreEqual(-1f, x, 1e-5f); Assert.AreEqual(0f, z, 1e-5f);
            CarryRules.Spot(8, 0f, 1f, 1f, out x, out z);
            Assert.AreEqual(0f, x, 1e-5f); Assert.AreEqual(-1f, z, 1e-5f);
        }

        [Test]
        public void SpotsFollowTheFacingAndGrowRingByRing()
        {
            CarryRules.Spot(1, 2f, 0f, .5f, out float x, out float z);
            Assert.AreEqual(.5f, x, 1e-5f); Assert.AreEqual(0f, z, 1e-5f);
            CarryRules.Spot(1 + CarryRules.SpotDirections, 2f, 0f, .5f, out x, out z);
            Assert.AreEqual(1f, x, 1e-5f); Assert.AreEqual(0f, z, 1e-5f);
        }

        [Test]
        public void EverySpotIsDistinctAndInsideTheLastRing()
        {
            var seen = new HashSet<(int, int)>();
            for (int i = 0; i < CarryRules.SpotCount; i++)
            {
                Assert.IsTrue(CarryRules.Spot(i, .3f, -.8f, .35f, out float x, out float z));
                Assert.LessOrEqual(Math.Sqrt(x * x + z * z), CarryRules.SpotRings * .35f + 1e-4f);
                Assert.IsTrue(seen.Add(((int)Math.Round(x * 1000f), (int)Math.Round(z * 1000f))), "spot " + i);
            }
            Assert.IsFalse(CarryRules.Spot(CarryRules.SpotCount, 0f, 1f, .35f, out _, out _));
            Assert.IsFalse(CarryRules.Spot(-1, 0f, 1f, .35f, out _, out _));
        }

        [Test]
        public void AZeroFacingStillGivesUsableSpots()
        {
            Assert.IsTrue(CarryRules.Spot(1, 0f, 0f, 1f, out float x, out float z));
            Assert.AreEqual(0f, x, 1e-5f); Assert.AreEqual(1f, z, 1e-5f);
        }

        [Test]
        public void ThrowSpeedIsCappedButNeverRaised()
        {
            float x = 30f, y = 0f, z = 40f;
            CarryRules.ClampSpeed(ref x, ref y, ref z, CarryRules.MaxThrowSpeed);
            Assert.AreEqual(CarryRules.MaxThrowSpeed, Math.Sqrt(x * x + y * y + z * z), 1e-3f);
            Assert.AreEqual(.75f, x / z, 1e-5f);
            float a = 3f, b = 4f, c = 0f;
            CarryRules.ClampSpeed(ref a, ref b, ref c, CarryRules.MaxThrowSpeed);
            Assert.AreEqual(3f, a); Assert.AreEqual(4f, b);
        }

        [Test]
        public void TheCapIsAboveEveryCatalogueThrowSpeed()
        {
            Assert.Greater(CarryRules.MaxThrowSpeed, 22f);
        }
    }
}
