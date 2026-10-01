using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MapObjectivesTests
    {
        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        public void BothMapsHaveValidGeometryAndEscapeOrders(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.IsEmpty(house.Validate(TestData.Catalogue()));
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut" })
            {
                Assert.IsTrue(house.ClearOutOrders.ContainsKey(mode));
                Assert.IsEmpty(house.Graph().CheckClosureOrder(house.ClearOutOrders[mode], house.NeverClose));
            }
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        public void KeepsakesArePhysicalFurnitureAndTheVanRemainsOpen(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.AreEqual(3, house.Keepsakes.Count);
            foreach (var keepsake in house.Keepsakes)
                Assert.IsTrue(house.Furniture.Any(f => f.Room == keepsake.Room && Math.Abs(f.X - keepsake.X) < .01f && Math.Abs(f.Z - keepsake.Z) < .01f),
                    $"{file}: keepsake at ({keepsake.X}, {keepsake.Z}) needs a physical prop");
            Assert.IsFalse(house.ClearOutOrders["MovingOut"].Contains(house.ExtractionRoom));
            Assert.IsTrue(house.Room(house.ExtractionRoom).Contains(house.ExtractionX, house.ExtractionZ));
            Assert.IsTrue(house.Graph().Connected(house.Rooms.Select(r => r.Name).Except(house.ClearOutOrders["MovingOut"]).ToArray()));
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        public void MovingDayChecklistUsesKnownFurnitureWordsInExistingRooms(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.AreEqual(4, house.MovingDay.Count);
            foreach (var objective in house.MovingDay)
            {
                Assert.IsTrue(TestData.Catalogue().TryGet(objective.Word, out _));
                Assert.IsTrue(house.Rooms.Any(r => r.Name == objective.Room));
            }
        }

        [Test]
        public void CourtyardOffersMoreSpaceAndWiderDoorways()
        {
            var courtyard = HouseLayout.FromJson(TestData.Read("house_courtyard.json"));
            var pinwheel = TestData.House();
            float Area(HouseLayout h) => h.Rooms.Sum(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ));
            Assert.Greater(Area(courtyard), Area(pinwheel) * 2f);
            Assert.Greater(courtyard.Doors.Min(d => d.Width), pinwheel.Doors.Min(d => d.Width));
            Assert.AreEqual(12f, courtyard.Room("Garden").MaxX - courtyard.Room("Garden").MinX);
        }
    }
}
