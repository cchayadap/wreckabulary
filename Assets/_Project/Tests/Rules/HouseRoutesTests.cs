using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class HouseRoutesTests
    {
        static HouseRoutes Terrace() => new(HouseLayout.FromJson(TestData.Read("house_terrace.json"), "house_terrace.json"));

        [Test]
        public void TheShortRouteUpstairsWinsOverTheLongWayRoundThroughTheKitchen()
        {
            var routes = Terrace();
            Assert.IsTrue(routes.TryFirstStep("LivingRoom", -9f, 3f, "KidsRoom", 5f, 8f, null, out var step));
            Assert.AreEqual("Hall", step.Room);
            Assert.IsNotNull(step.Door);
            Assert.Less(step.Metres, 30f);
            Assert.IsTrue(routes.TryFirstStep("Hall", -5f, 6f, "KidsRoom", 5f, 8f, null, out step));
            Assert.AreEqual("Landing", step.Room);
            Assert.AreEqual("Hall", step.Stairs.Lower);
        }

        [Test]
        public void TheNearerFlightIsTakenDownToTheKitchen()
        {
            Assert.IsTrue(Terrace().TryFirstStep("Landing", -.5f, 8f, "Kitchen", 15f, 11f, null, out var step));
            Assert.AreEqual("KidsRoom", step.Room);
            Assert.IsNotNull(step.Door);
        }

        [Test]
        public void AClosedRoomSendsTheRouteTheOtherWay()
        {
            Assert.IsTrue(Terrace().TryFirstStep("Hall", -5f, 6f, "KidsRoom", 5f, 8f, room => room == "Landing", out var step));
            Assert.AreEqual("Kitchen", step.Room);
            Assert.IsNotNull(step.Door);
        }

        [Test]
        public void NoRouteWhenEveryWayInIsClosed()
        {
            Assert.IsFalse(Terrace().TryFirstStep("Hall", 0f, 0f, "Workshop", 14f, -8f, room => room is "Dining" or "Garden", out _));
        }
    }
}
