using System;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class CourseSensorBoundaryTests
    {
        static CourseBox Sensor(int floorMm) => new CourseBox(new CoursePoint(38750, floorMm, -19450),
            new CoursePoint(39250, floorMm + 900, -18950));
        static bool Inside(CourseBox box, float y, float x = 39, float z = -19.2f) =>
            CourseSensorBoundary.ContainsFootPoint(box, x, y, z);

        [TestCase(0), TestCase(16000), TestCase(-16000), TestCase(-CourseLimits.VerticalMm),
            TestCase(CourseLimits.VerticalMm - 900)]
        public void FloorContactAcceptsBoundaryInsideAndSmallSolverUndershoot(int floorMm)
        {
            var box = Sensor(floorMm);
            float floor = floorMm / 1000f;
            Assert.IsTrue(Inside(box, floor));
            Assert.IsTrue(Inside(box, floor + .0001f));
            Assert.IsTrue(Inside(box, floor - .00002f));
            Assert.IsFalse(Inside(box, floor - .0011f));
            Assert.IsFalse(Inside(box, floor - .05f));
            float top = box.Max.Y / 1000f;
            Assert.IsTrue(Inside(box, top));
            Assert.IsFalse(Inside(box, Next(top, true)), "The upper plane has no contact tolerance.");
            Assert.IsFalse(Inside(box, top + .1f));
        }

        [TestCase(0), TestCase(16000), TestCase(-16000), TestCase(-CourseLimits.VerticalMm),
            TestCase(CourseLimits.VerticalMm - 900)]
        public void LastRepresentableAcceptedPointNeverExceedsOneMillimetre(int floorMm)
        {
            var box = Sensor(floorMm);
            double cutoff = (floorMm - CourseSensorBoundary.FloorContactToleranceMm) / 1000d;
            float accepted = (float)cutoff;
            if (accepted < cutoff) accepted = Next(accepted, true);
            float rejected = Next(accepted, false);
            Assert.LessOrEqual(floorMm - (double)accepted * 1000, 1d);
            Assert.Greater(floorMm - (double)rejected * 1000, 1d);
            Assert.IsTrue(Inside(box, accepted));
            Assert.IsFalse(Inside(box, rejected));
        }

        [TestCase(1), TestCase(-1)]
        public void HorizontalEdgesAtCourseLimitKeepExactContainment(int sign)
        {
            int edge = sign * CourseLimits.HorizontalMm;
            int min = sign > 0 ? edge - 1000 : edge;
            int max = sign > 0 ? edge : edge + 1000;
            var box = new CourseBox(new CoursePoint(min, -16000, min), new CoursePoint(max, -15100, max));
            float low = min / 1000f, high = max / 1000f, center = (low + high) / 2;
            Assert.IsTrue(Inside(box, -16.00002f, low, low));
            Assert.IsTrue(Inside(box, -16.00002f, high, high));
            Assert.IsFalse(Inside(box, -16, Next(low, false), center));
            Assert.IsFalse(Inside(box, -16, Next(high, true), center));
            Assert.IsFalse(Inside(box, -16, center, Next(low, false)));
            Assert.IsFalse(Inside(box, -16, center, Next(high, true)));
        }

        [TestCase(float.NaN), TestCase(float.PositiveInfinity), TestCase(float.NegativeInfinity)]
        public void NonFiniteCoordinatesAreRejected(float value)
        {
            var box = Sensor(16000);
            Assert.IsFalse(Inside(box, value));
            Assert.IsFalse(Inside(box, 16, value));
            Assert.IsFalse(Inside(box, 16, 39, value));
        }

        [Test]
        public void MeasuredWindUndershootRetainsThreeDimensionalTarget()
        {
            var box = Sensor(16000);
            var position = new WorldVector(39, 15.9999886f, -19.2f);
            Assert.Less(position.Y, 16);
            Assert.IsTrue(CourseSensorBoundary.ContainsFootPoint(box, position));
            Assert.IsFalse(Inside(box, position.Y, 39, -19.4501f));
            Assert.IsFalse(Inside(box, 15.9989f));
            Assert.IsFalse(Inside(box, 16.901f));
        }

        [Test]
        public void CraftedReceiverReservationUsesTheSameFloorBoundaryAndEligibility()
        {
            var authored = new CoursePhysicalSite { WindReceiverRecipeId = "STOOL", ReceiverStartBounds = Sensor(16000) };
            var site = (ResolvedCoursePhysicalSite)Activator.CreateInstance(typeof(ResolvedCoursePhysicalSite),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { authored }, null);
            var method = typeof(WordboundEntityBudget).GetMethod("ReceiverCandidate", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var item = new ItemInstance();
            void Set(string property, object value) => typeof(ItemInstance).GetProperty(property).SetValue(item, value);
            Set("Word", "STOOL"); Set("Origin", ItemOrigin.Crafted); Set("State", ItemState.World);
            var body = new WorldBodyState("item:1", WorldBodyKind.Item, new WorldVector(39, 15.9999886f, -19.2f));
            bool Reserved(WorldBodyState value) => (bool)method.Invoke(null, new object[] { site, value, item });
            Assert.IsTrue(Reserved(body));
            Assert.IsFalse(Reserved(body.At(new WorldVector(39, 15.9989f, -19.2f))));
            Assert.IsFalse(Reserved(body.At(new WorldVector(39, 16.901f, -19.2f))));
            Assert.IsFalse(Reserved(body.At(new WorldVector(39.251f, 16, -19.2f))));
            Assert.IsFalse(Reserved(body.At(body.Position, active: false)));
            Set("State", ItemState.Held); Assert.IsFalse(Reserved(body));
            Set("State", ItemState.World); Set("Origin", ItemOrigin.Map); Assert.IsFalse(Reserved(body));
            Set("Origin", ItemOrigin.Crafted); Set("Word", "BED"); Assert.IsFalse(Reserved(body));
        }

        static float Next(float value, bool upward)
        {
            if (value == 0) return upward ? float.Epsilon : -float.Epsilon;
            int bits = BitConverter.SingleToInt32Bits(value);
            return BitConverter.Int32BitsToSingle(bits + ((value > 0) == upward ? 1 : -1));
        }
    }
}
