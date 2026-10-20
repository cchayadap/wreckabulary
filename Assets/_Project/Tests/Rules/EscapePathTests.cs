using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class EscapePathTests
    {
        static HouseLayout House()
        {
            var house = new HouseLayout();
            foreach (string name in new[] { "Entry", "Hall", "Landing", "Loft", "Isolated" })
                house.Rooms.Add(new RoomBox { Name = name });
            house.Doors.Add(new Doorway { A = "Entry", B = "Hall" });
            house.Stairs.Add(new Stairway { Lower = "Hall", Upper = "Landing" });
            house.Doors.Add(new Doorway { A = "Landing", B = "Loft" });
            return house;
        }

        [TestCase("Entry", "Hall", "Entry|Hall")]
        [TestCase("Entry", "Landing", "Entry|Hall")]
        [TestCase("Entry", "Loft", "Entry|Hall,Landing|Loft")]
        [TestCase("Loft", "Entry", "Landing|Loft,Entry|Hall")]
        [TestCase("Hall", "Loft", "Landing|Loft")]
        [TestCase("Loft", "Hall", "Landing|Loft")]
        public void DoorPathCrossesStairsWithoutLosingEarlierDoors(string from, string to, string expected)
        {
            var path = Escape.PathDoors(House(), from, to);
            Assert.AreEqual(expected, string.Join(",", path.Select(door => Escape.DoorKey(door.A, door.B))));
        }

        [TestCase("Hall", "Landing")]
        [TestCase("Landing", "Hall")]
        [TestCase("Entry", "Entry")]
        [TestCase("Entry", "Isolated")]
        [TestCase("Missing", "Loft")]
        [TestCase("Entry", "Missing")]
        public void DoorPathDoesNotInventDoors(string from, string to)
        {
            Assert.IsEmpty(Escape.PathDoors(House(), from, to));
        }
    }
}
