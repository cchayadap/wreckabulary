using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class StoreyTests
    {
        const string TwoStoreys = @"{
 ""name"": ""Two Storeys"",
 ""rooms"": [
  { ""name"": ""Hall"", ""bounds"": [0, 0, 6, 8] },
  { ""name"": ""Den"", ""bounds"": [6, 0, 12, 8] },
  { ""name"": ""Landing"", ""bounds"": [0, 0, 6, 8], ""floorY"": 3 },
  { ""name"": ""Loft"", ""bounds"": [6, 0, 12, 8], ""floorY"": 3 }
 ],
 ""doors"": [
  { ""between"": [""Hall"", ""Den""], ""at"": [6, 4] },
  { ""between"": [""Landing"", ""Loft""], ""at"": [6, 4] }
 ],
 ""stairs"": [
  { ""between"": [""Hall"", ""Landing""], ""from"": [1, 1], ""to"": [1, 5.5] }
 ],
 ""spawns"": [
  { ""room"": ""Den"", ""at"": [9, 4] },
  { ""room"": ""Loft"", ""at"": [9, 4] }
 ],
 ""furniture"": [
  { ""word"": ""CHAIR"", ""room"": ""Den"", ""at"": [10, 6.5] },
  { ""word"": ""CHAIR"", ""room"": ""Loft"", ""at"": [10, 6.5] }
 ],
 ""neverClose"": [""Hall"", ""Landing""],
 ""lobbyRoom"": ""Den"",
 ""clearOutOrders"": { ""Dibs"": [""Loft"", ""Den""] }
}";

        static HouseLayout House() => HouseLayout.FromJson(TwoStoreys, "two-storeys");

        static string ProblemsWith(Action<HouseLayout> change)
        {
            var house = House();
            change(house);
            return string.Join("\n", house.Validate(TestData.Catalogue()));
        }

        static void Reports(string expected, string problems) =>
            Assert.IsTrue(problems.Contains(expected), $"expected \"{expected}\" in:\n{problems}");

        [Test]
        public void StairsAndTheLobbyRoomAreReadFromTheMap()
        {
            var house = House();
            Assert.IsEmpty(house.Validate(TestData.Catalogue()));
            Assert.AreEqual("Den", house.LobbyRoom);
            var stairs = house.Stairs.Single();
            Assert.AreEqual(1.6f, stairs.Width, "stairs are 1.6 m wide unless the map says otherwise");
            Assert.AreEqual(4.5, stairs.Run, 1e-4);
            Assert.IsTrue(stairs.Covers(1f, 3f));
            Assert.IsFalse(stairs.Covers(2f, 3f));
            Assert.IsNull(TestData.House().LobbyRoom);
        }

        [Test]
        public void StackedRoomsAreValidButRoomsOnOneStoreyMayNotOverlap()
        {
            string problems = ProblemsWith(h => h.Room("Den").MinX = 5f);
            Reports("rooms Hall and Den overlap", problems);
            Assert.IsFalse(problems.Contains("Landing overlap") || problems.Contains("Loft overlap"), problems);
        }

        [Test]
        public void HeightPicksTheStorey()
        {
            var house = House();
            Assert.AreEqual("Den", house.RoomAt(9f, 0f, 4f));
            Assert.AreEqual("Loft", house.RoomAt(9f, 3f, 4f));
            Assert.AreEqual("Den", house.RoomAt(9f, 4f), "without a height, the ground floor");
            Assert.AreEqual("Den", house.RoomAt(9f, -2f, 4f), "below every floor, the lowest room");
            Assert.AreEqual("Hall", house.RoomAt(1f, 1.5f, 3f), "halfway up the stairs you're still downstairs");
            Assert.AreEqual("Landing", house.RoomAt(1f, 2.8f, 5.3f), "near the top you're upstairs");
            Assert.IsNull(house.RoomAt(20f, 0f, 4f));
            CollectionAssert.AreEqual(new[] { 0f, 3f }, house.StoreyFloors());
            Assert.AreEqual(0, house.StoreyOf(house.Room("Den")));
            Assert.AreEqual(1, house.StoreyOf(house.Room("Loft")));
            Assert.AreEqual(0, house.StoreyAt(1.5f));
            Assert.AreEqual(1, house.StoreyAt(2.8f));
            Assert.AreEqual(0, TestData.House().StoreyAt(5f), "a one-floor house has only the ground floor");
        }

        [Test]
        public void StoreysHavePlayerFacingNames()
        {
            var house = House();
            Assert.AreEqual("GROUND FLOOR", house.StoreyLabel(0));
            Assert.AreEqual("UPSTAIRS", house.StoreyLabel(1), "a two-storey house just has an upstairs");
            Assert.AreEqual("Loft (upstairs)", house.WithStorey("Loft"));
            Assert.AreEqual("Den (ground floor)", house.WithStorey("Den"));
            Assert.AreEqual("Attic", house.WithStorey("Attic"), "an unknown room keeps its name");
            Assert.AreEqual("Kitchen", TestData.House().WithStorey("Kitchen"), "one-floor houses don't mention floors");
            house.Rooms.Add(new RoomBox { Name = "Attic", MinX = 0f, MinZ = 0f, MaxX = 6f, MaxZ = 8f, FloorY = 6f });
            Assert.AreEqual("1ST FLOOR", house.StoreyLabel(1));
            Assert.AreEqual("2ND FLOOR", house.StoreyLabel(2));
            Assert.AreEqual("3RD FLOOR", house.StoreyLabel(3));
            Assert.AreEqual("11TH FLOOR", house.StoreyLabel(11));
            Assert.AreEqual("21ST FLOOR", house.StoreyLabel(21));
            Assert.AreEqual("Attic (2nd floor)", house.WithStorey("Attic"));
        }

        [Test]
        public void StairsJoinStoreysButDoorsMayNot()
        {
            var house = House();
            var g = house.Graph();
            CollectionAssert.Contains(g.Neighbours("Hall").ToList(), "Landing");
            Assert.IsEmpty(g.CheckClosureOrder(house.ClearOutOrders["Dibs"], house.NeverClose));
            Reports("joins two storeys", ProblemsWith(h => h.Doors.Add(new Doorway { A = "Den", B = "Loft", X = 12f, Z = 4f, Width = 1.4f })));
            Reports("not every room can be reached", ProblemsWith(h => h.Stairs.Clear()));
        }

        [Test]
        public void BadStairsAreReported()
        {
            Reports("unknown room 'Attic'", ProblemsWith(h => h.Stairs[0].Upper = "Attic"));
            Reports("higher storey", ProblemsWith(h => { h.Stairs[0].Lower = "Landing"; h.Stairs[0].Upper = "Hall"; }));
            Reports("at most 35", ProblemsWith(h => h.Stairs[0].ToZ = 3f));
            Reports("straight along X or Z", ProblemsWith(h => h.Stairs[0].ToX = 2f));
            Reports("a player needs 0.9 m", ProblemsWith(h => h.Stairs[0].Width = 0.6f));
            Reports("inside both Hall and Landing", ProblemsWith(h => { h.Stairs[0].FromX = 0.3f; h.Stairs[0].ToX = 0.3f; }));
            Reports("step on and off", ProblemsWith(h => { h.Stairs[0].FromZ = 3.5f; h.Stairs[0].ToZ = 8f; }));
            Reports("CHAIR at (1, 3) stands on the stairs", ProblemsWith(h => h.Furniture.Add(new FurniturePlacement { Word = "CHAIR", Room = "Landing", X = 1f, Z = 3f })));
            Reports("of an end of the stairs", ProblemsWith(h => h.Furniture.Add(new FurniturePlacement { Word = "CHAIR", Room = "Hall", X = 2.2f, Z = 0.6f })));
            Reports("is on the stairs", ProblemsWith(h => h.Spawns[0] = new SpawnPoint { Room = "Hall", X = 1f, Z = 3f }));
            Reports("lobby room Attic", ProblemsWith(h => h.LobbyRoom = "Attic"));
        }

        static void WithAttic(HouseLayout h, Stairway up)
        {
            h.Rooms.Add(new RoomBox { Name = "Attic", MinX = 0f, MinZ = 0f, MaxX = 6f, MaxZ = 8f, FloorY = 6f });
            h.Stairs.Add(up);
        }

        static Stairway Flight(string lower, string upper, float fromX, float fromZ, float toX, float toZ) =>
            new Stairway { Lower = lower, Upper = upper, FromX = fromX, FromZ = fromZ, ToX = toX, ToZ = toZ, Width = 1.6f };

        [Test]
        public void FlightsJoinNeighbouringFloorsAndKeepOutOfEachOthersWay()
        {
            var switchback = Flight("Landing", "Attic", 4.5f, 5.5f, 4.5f, 1f);
            Assert.IsEmpty(ProblemsWith(h => WithAttic(h, switchback)));
            var house = House();
            WithAttic(house, switchback);
            CollectionAssert.AreEqual(new[] { 0f, 3f, 6f }, house.StoreyFloors());
            Assert.AreEqual("Attic", house.RoomAt(1f, 6f, 4f));
            Assert.AreEqual("Landing", house.RoomAt(1f, 3f, 4f));
            Assert.AreEqual("Hall", house.RoomAt(1f, 0f, 4f));
            Assert.AreEqual("Landing", house.RoomAt(4.5f, 4.5f, 3.25f), "halfway up the second flight you're still on the landing");
            Assert.AreEqual(1, house.StoreyAt(4.5f));
            Assert.AreEqual(2, house.StoreyAt(5.8f));
            Assert.IsTrue(house.Graph().Connected(new[] { "Hall", "Landing", "Attic" }));

            Reports("pass through the floor of Landing", ProblemsWith(h => WithAttic(h, Flight("Hall", "Attic", 4.5f, 1f, 4.5f, 7f))));
            Reports("stairs Hall-Landing overlap the stairs Landing-Attic", ProblemsWith(h => WithAttic(h, Flight("Landing", "Attic", 1f, 5.5f, 1f, 1f))));
            Reports("stairs Hall-Landing step on or off over the stairs Landing-Attic", ProblemsWith(h => WithAttic(h, Flight("Landing", "Attic", .5f, 6.8f, 5f, 6.8f))));
            Assert.IsFalse(ProblemsWith(h => WithAttic(h, Flight("Landing", "Attic", .5f, 6.8f, 5f, 6.8f))).Contains("overlap"), "beside it, not on it");
        }

        [Test]
        public void EveryShippedMapPutsItsPeopleAndThingsInTheirOwnRoomsByHeight()
        {
            string folder = Path.Combine(TestData.Root, "Assets/_Project/Data/Config");
            foreach (string file in Directory.GetFiles(folder, "house_*.json"))
            {
                string map = Path.GetFileName(file);
                var house = HouseLayout.FromJson(File.ReadAllText(file), map);
                float Floor(string room) => house.Room(room).FloorY;
                foreach (var s in house.Spawns) Assert.AreEqual(s.Room, house.RoomAt(s.X, Floor(s.Room), s.Z), $"{map}: spawn at ({s.X}, {s.Z})");
                foreach (var k in house.Keepsakes) Assert.AreEqual(k.Room, house.RoomAt(k.X, Floor(k.Room), k.Z), $"{map}: keepsake at ({k.X}, {k.Z})");
                foreach (var f in house.Furniture) Assert.AreEqual(f.Room, house.RoomAt(f.X, Floor(f.Room) + f.Y, f.Z), $"{map}: {f.Word} at ({f.X}, {f.Z})");
                if (house.ExtractionRoom != null)
                    Assert.AreEqual(house.ExtractionRoom, house.RoomAt(house.ExtractionX, Floor(house.ExtractionRoom), house.ExtractionZ), $"{map}: the van");
            }
        }
    }
}
