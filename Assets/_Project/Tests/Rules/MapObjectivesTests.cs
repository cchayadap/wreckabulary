using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MapObjectivesTests
    {
        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
        public void EveryMapHasValidGeometryAndEscapeOrders(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.IsEmpty(house.Validate(TestData.Catalogue()));
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut", "Escape" })
            {
                Assert.IsTrue(house.ClearOutOrders.ContainsKey(mode));
                Assert.IsEmpty(house.Graph().CheckClosureOrder(house.ClearOutOrders[mode], house.NeverClose));
            }
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
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
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
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

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
        public void DuosClosesAStartRoomOfEachTeamFirst(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            var teams = Teams.Assign(house.Spawns.Count, 2);
            var closing = house.ClearOutOrders["Duos"].Where(room => house.Spawns.Any(s => s.Room == room))
                .Select(room => teams[house.Spawns.FindIndex(s => s.Room == room)]).Take(2).ToArray();
            Assert.AreEqual(2, closing.Length, $"{file}: Duos closes at least two start rooms");
            Assert.AreNotEqual(closing[0], closing[1], $"{file}: the first two start rooms to close belong to different teams");
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
        public void EveryRoomHasTwoWaysOutAndEveryHouseHasABigOpenSpace(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            var graph = house.Graph();
            var names = house.Rooms.Select(r => r.Name).ToArray();
            foreach (string room in names)
            {
                Assert.GreaterOrEqual(graph.Neighbours(room).Count(), 2, $"{file}: {room} is a dead end");
                foreach (string next in graph.Neighbours(room).Where(n => string.CompareOrdinal(room, n) < 0).ToArray())
                {
                    var cut = new RoomGraph();
                    foreach (string r in names) cut.AddRoom(r);
                    foreach (var d in house.Doors.Where(d => !(d.A == room && d.B == next) && !(d.A == next && d.B == room))) cut.AddDoor(d.A, d.B);
                    foreach (var s in house.Stairs.Where(s => !(s.Lower == room && s.Upper == next) && !(s.Lower == next && s.Upper == room))) cut.AddDoor(s.Lower, s.Upper);
                    Assert.IsTrue(cut.Connected(names), $"{file}: {room} and {next} need a second route between them");
                }
            }
            Assert.IsTrue(house.Rooms.Any(r => Math.Min(r.MaxX - r.MinX, r.MaxZ - r.MinZ) >= 12f), file + " has a space at least 12 m across");
            Assert.GreaterOrEqual(house.Furniture.Count, 60, file + " is furnished for its size");
        }

        [Test]
        public void TheCityBlockIsTwoFlatsAcrossASharedHallUnderOneRoof()
        {
            var flat = HouseLayout.FromJson(TestData.Read("house_flat.json"), "house_flat.json");
            CollectionAssert.AreEqual(new[] { 0f, 3f }, flat.StoreyFloors());
            var ground = flat.Rooms.Where(r => r.FloorY == 0f).ToArray();
            Assert.IsTrue(flat.Graph().Connected(ground.Select(r => r.Name).ToArray()), "the ground floor works on its own");
            var hall = flat.Room("Hall");
            var opens = flat.Graph().Neighbours("Hall").Select(flat.Room).Where(r => r.FloorY == 0f).ToArray();
            Assert.IsTrue(opens.Any(r => r.MaxX <= hall.MinX) && opens.Any(r => r.MinX >= hall.MaxX), "both flats open onto the shared hall");
            var roof = flat.Room("Roof");
            Assert.IsTrue(roof.Outdoor);
            Assert.AreEqual(3f, roof.FloorY);
            CollectionAssert.AreEquivalent(new[] { "Hall", "LivingRoom", "Lounge" }, flat.Stairs.Where(s => s.Upper == "Roof").Select(s => s.Lower));
            CollectionAssert.Contains(flat.NeverClose, "Hall");
            Assert.IsFalse(HomeDesigner.Supports("flat"), "homes are designed only on the maps the web edition knows too");
            Assert.IsTrue(HomeDesigner.Supports("pinwheel") && HomeDesigner.Supports("courtyard"));
        }

        [Test]
        public void TheTerraceHouseHasAnUpstairsAndAnAtticReachedByTwoStairsEach()
        {
            var terrace = HouseLayout.FromJson(TestData.Read("house_terrace.json"), "house_terrace.json");
            CollectionAssert.AreEqual(new[] { 0f, 3f, 6f }, terrace.StoreyFloors());
            CollectionAssert.AreEquivalent(new[] { "Hall>Landing", "Kitchen>KidsRoom", "Landing>Attic", "Bedroom>Attic" }, terrace.Stairs.Select(s => s.Lower + ">" + s.Upper));
            var downstairs = terrace.Rooms.Where(r => r.FloorY == 0f).Select(r => r.Name).ToArray();
            Assert.IsTrue(terrace.Graph().Connected(downstairs), "the ground floor works on its own");
            Assert.IsTrue(terrace.Room("Garden").Outdoor, "the back garden");
            CollectionAssert.Contains(downstairs, "Workshop");
            var upstairs = terrace.Rooms.Where(r => r.FloorY == 3f).Select(r => r.Name).ToArray();
            Assert.IsTrue(terrace.Graph().Connected(upstairs), "upstairs works on its own");
            foreach (string room in new[] { "Hall", "Kitchen" })
                Assert.IsTrue(terrace.Graph().Connected(upstairs.Append(room).ToArray()), $"upstairs is reached from the {room}");
            foreach (string room in downstairs.Where(r => r != "Hall" && r != "Kitchen"))
                Assert.IsFalse(terrace.Graph().Connected(upstairs.Append(room).ToArray()), $"upstairs is not reached from the {room}");
            CollectionAssert.AreEquivalent(new[] { "Landing", "Bedroom" }, terrace.Graph().Neighbours("Attic"));
            Assert.AreEqual(2, terrace.Spawns.Count(s => terrace.Room(s.Room).FloorY == 0f), "two players start downstairs");
            Assert.AreEqual(2, terrace.Spawns.Count(s => terrace.Room(s.Room).FloorY == 3f), "two players start upstairs");
            Assert.AreEqual(0f, terrace.Room(terrace.ExtractionRoom).FloorY, "the van parks on the ground floor");
            CollectionAssert.AreEquivalent(new[] { "Hall", "Landing" }, terrace.NeverClose);
            Assert.IsTrue(terrace.MovingDay.Any(m => terrace.Room(m.Room).FloorY == 3f), "Moving Day sends something upstairs");
            Assert.IsFalse(HomeDesigner.Supports("terrace"));
        }

        [Test]
        public void TheWalkUpStacksThreeFloorsAndARoofOnStairsThatSwitchBackAndAFireEscape()
        {
            var walkup = HouseLayout.FromJson(TestData.Read("house_walkup.json"), "house_walkup.json");
            CollectionAssert.AreEqual(new[] { 0f, 3f, 6f, 9f }, walkup.StoreyFloors());
            CollectionAssert.AreEquivalent(new[] { "Lobby>Landing1", "Landing1>Landing2", "Landing2>RoofGarden", "Yard>FireEscape1", "FireEscape1>FireEscape2", "FireEscape2>RoofGarden" },
                walkup.Stairs.Select(s => s.Lower + ">" + s.Upper));
            Assert.IsTrue(new[] { "Yard", "FireEscape1", "FireEscape2", "RoofGarden" }.All(walkup.IsOutdoor), "the fire escape and the roof garden are outdoors");
            var first = walkup.Stairs.Single(s => s.Lower == "Lobby");
            var second = walkup.Stairs.Single(s => s.Lower == "Landing1");
            Assert.AreEqual("Landing1", first.Upper);
            Assert.AreEqual("Landing2", second.Upper);
            Assert.AreEqual(first.ToZ, second.FromZ, .01f);
            Assert.Less((first.ToZ - first.FromZ) * (second.ToZ - second.FromZ), 0f, "the flights run opposite ways");
            Assert.GreaterOrEqual(first.MinX - second.MaxX, 1.2f, "room to walk between the flights");
            var ground = walkup.Rooms.Where(r => r.FloorY == 0f).Select(r => r.Name).ToArray();
            Assert.IsTrue(walkup.Graph().Connected(ground), "the ground floor works on its own");
            var upper = walkup.Rooms.Where(r => r.FloorY > 0f).Select(r => r.Name).ToArray();
            var stairwell = new[] { "Lobby", "Landing1", "Landing2" };
            var fireEscape = new[] { "Yard", "FireEscape1", "FireEscape2" };
            Assert.IsTrue(walkup.Graph().Connected(walkup.Rooms.Select(r => r.Name).Except(fireEscape).ToArray()), "the inside stairs reach every floor");
            Assert.IsTrue(walkup.Graph().Connected(new[] { "Garage", "Yard", "FireEscape1", "RedFlat", "FireEscape2", "YellowFlat", "RoofGarden" }), "the fire escape climbs from the yard past the red and yellow flats to the roof");
            foreach (string room in ground.Except(stairwell).Except(fireEscape))
                Assert.IsFalse(walkup.Graph().Connected(upper.Except(stairwell).Except(fireEscape).Append(room).ToArray()), $"the flats are not reached straight from {room}");
            Assert.IsTrue(walkup.Spawns.All(s => walkup.Room(s.Room).FloorY > 0f), "everyone starts in a flat upstairs");
            Assert.AreEqual(2, walkup.Spawns.Count(s => walkup.Room(s.Room).FloorY == 6f), "two start on the top floor");
            Assert.AreEqual(0f, walkup.Room(walkup.ExtractionRoom).FloorY, "the van parks on the ground floor");
            CollectionAssert.AreEquivalent(new[] { "Lobby", "Landing1", "Landing2" }, walkup.NeverClose);
            Assert.AreEqual(6f, walkup.Room(walkup.MovingDay.Single(m => m.Word == "BED").Room).FloorY, "the bed goes up two flights");
            Assert.AreEqual("1ST FLOOR", walkup.StoreyLabel(1));
            Assert.AreEqual("GreenFlat (2nd floor)", walkup.WithStorey("GreenFlat"));
            Assert.IsFalse(HomeDesigner.Supports("walkup"));
        }

        [Test]
        public void CourtyardOffersMoreSpaceAndWiderDoorways()
        {
            var courtyard = HouseLayout.FromJson(TestData.Read("house_courtyard.json"));
            var pinwheel = TestData.House();
            Assert.Greater(courtyard.FloorArea, pinwheel.FloorArea);
            Assert.Greater(courtyard.Doors.Min(d => d.Width), pinwheel.Doors.Min(d => d.Width));
            var square = courtyard.Room("FountainSquare");
            Assert.IsTrue(square.Outdoor);
            Assert.AreEqual(24f, square.MaxX - square.MinX);
            CollectionAssert.AreEqual(new[] { "FountainSquare" }, courtyard.NeverClose);
            Assert.IsTrue(courtyard.Room("Orchard").Outdoor);
            Assert.IsFalse(courtyard.Room("Walkway").Outdoor, "the walkway is covered");
            Assert.AreEqual("Garage", courtyard.ExtractionRoom, "the van parks in the garage");
        }

        [Test]
        public void HousesAreTwiceAsWideAndDeepAndTheCourtyardHalfAgain()
        {
            foreach (var (file, width, depth) in new[] { ("house_pinwheel.json", 40f, 40f), ("house_courtyard.json", 48f, 48f), ("house_flat.json", 48f, 32f),
                         ("house_terrace.json", 40f, 28f), ("house_walkup.json", 36f, 24f) })
            {
                var house = HouseLayout.FromJson(TestData.Read(file), file);
                Assert.AreEqual(width, house.Rooms.Max(r => r.MaxX) - house.Rooms.Min(r => r.MinX), file);
                Assert.AreEqual(depth, house.Rooms.Max(r => r.MaxZ) - house.Rooms.Min(r => r.MinZ), file);
                foreach (var s in house.Stairs)
                {
                    Assert.AreEqual(3f, house.Room(s.Upper).FloorY - house.Room(s.Lower).FloorY, 1e-4f, file + ": a storey is still 3 m");
                    Assert.AreEqual(4.5f, s.Run, 1e-4f, file + ": stairs keep their slope");
                    Assert.GreaterOrEqual(s.Width, 1.6f, file);
                }
            }
        }
    }
}
