using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    /// <summary>Pinwheel House and the clear-out (brief §6: rooms close without trapping anyone).</summary>
    [TestFixture]
    public class HouseTests
    {
        HouseLayout house;

        [SetUp]
        public void Load() => house = TestData.House();

        [Test]
        public void TheShippedHouseIsValid()
        {
            Assert.IsEmpty(house.Validate(TestData.Catalogue()));
            Assert.AreEqual(5, house.Rooms.Count);
            Assert.AreEqual(4, house.Spawns.Count);
        }

        [Test]
        public void EveryOuterRoomHasThreeExits()
        {
            var g = house.Graph();
            foreach (var room in house.Rooms.Where(r => r.Name != "Playroom"))
                Assert.AreEqual(3, g.Neighbours(room.Name).Count(), room.Name);
            Assert.AreEqual(4, g.Neighbours("Playroom").Count());
        }

        [Test]
        public void EverySpawnIsInADifferentOuterRoom()
        {
            var rooms = house.Spawns.Select(s => s.Room).ToList();
            Assert.AreEqual(rooms.Count, rooms.Distinct().Count());
            CollectionAssert.DoesNotContain(rooms, "Playroom");
            foreach (var s in house.Spawns) Assert.AreEqual(s.Room, house.RoomAt(s.X, s.Z));
        }

        [Test]
        public void EverySpawnRoomCanMakeEveryCoreItem()
        {
            var catalogue = TestData.Catalogue();
            foreach (var spawn in house.Spawns)
            {
                var letters = house.LettersIn(spawn.Room);
                foreach (var item in catalogue.Enabled)
                    Assert.IsTrue(letters.Contains(item.Letters), $"{spawn.Room} can't make {item.Id}; it has {letters}");
            }
        }

        [Test]
        public void EveryClosingOrderOfTheOuterRoomsIsSafe()
        {
            var g = house.Graph();
            var outer = house.Rooms.Select(r => r.Name).Where(n => !house.NeverClose.Contains(n)).ToList();
            int checkedOrders = 0;
            foreach (var order in Permutations(outer))
            {
                Assert.IsEmpty(g.CheckClosureOrder(order, house.NeverClose), string.Join(" > ", order));
                checkedOrders++;
            }
            Assert.AreEqual(24, checkedOrders, "4 outer rooms close in 24 possible orders");
        }

        [Test]
        public void TheShippedOrdersCoverTheClearOutModes()
        {
            CollectionAssert.AreEquivalent(new[] { "Dibs", "Duos", "MovingOut" }, house.ClearOutOrders.Keys);
            CollectionAssert.DoesNotContain(house.ClearOutOrders["MovingOut"], house.ExtractionRoom, "the van's room stays open");
        }

        [Test]
        public void UnsafeOrdersAreCaught()
        {
            var g = new RoomGraph();
            foreach (string r in new[] { "A", "B", "C", "Hub" }) g.AddRoom(r);
            g.AddDoor("A", "B");
            g.AddDoor("B", "Hub");
            g.AddDoor("C", "Hub");
            var never = new[] { "Hub" };
            Assert.IsEmpty(g.CheckClosureOrder(new[] { "A", "B", "C" }, never));
            Assert.IsNotEmpty(g.CheckClosureOrder(new[] { "B" }, never), "closing B cuts A off");
            Assert.IsNotEmpty(g.CheckClosureOrder(new[] { "Hub" }, never), "the hub never closes");
            Assert.IsNotEmpty(g.CheckClosureOrder(new[] { "A", "A" }, never), "a room can't close twice");
            Assert.IsNotEmpty(g.CheckClosureOrder(new[] { "Attic" }, never), "unknown room");
        }

        [Test]
        public void BrokenLayoutsAreReported()
        {
            var bad = TestData.House();
            bad.Doors[0].Width = 0.5f;
            bad.Doors[1].X += 3f;
            bad.Furniture[0].Room = "Garage";
            bad.Furniture[1].Word = "SPACESHIP";
            bad.Rooms[0].MaxX += 1f;
            var problems = bad.Validate(TestData.Catalogue());
            string all = string.Join("\n", problems);
            Assert.IsTrue(problems.Any(p => p.Contains("wide")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("isn't on a wall")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("Garage")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("SPACESHIP")), all);
            Assert.IsTrue(problems.Any(p => p.Contains("overlap")), all);
        }

        [Test]
        public void FurnitureKeepsDoorwaysClear()
        {
            var crowded = TestData.House();
            var door = crowded.Doors[0];
            crowded.Furniture.Add(new FurniturePlacement { Word = "CHAIR", Room = door.A, X = door.X, Z = door.Z + (crowded.Room(door.A).MaxZ > door.Z ? 0.5f : -0.5f) });
            Assert.IsTrue(crowded.Validate(TestData.Catalogue()).Any(p => p.Contains("doorway")));
        }

        [Test]
        public void TheClearOutWarnsFillsAndCloses()
        {
            var rules = TestData.RulesFor("Dibs");
            var s = new ClearOutSchedule(rules, house.ClearOutOrders["Dibs"]);
            string first = house.ClearOutOrders["Dibs"][0];
            string second = house.ClearOutOrders["Dibs"][1];
            Assert.AreEqual(RoomPhase.Safe, s.PhaseOf(first, 34.9));
            Assert.AreEqual(RoomPhase.Warning, s.PhaseOf(first, 35));
            Assert.AreEqual(RoomPhase.Filling, s.PhaseOf(first, 45));
            Assert.AreEqual(RoomPhase.Closed, s.PhaseOf(first, 51));
            Assert.AreEqual(RoomPhase.Safe, s.PhaseOf(second, 50));
            Assert.AreEqual(RoomPhase.Filling, s.PhaseOf(second, 70));
            Assert.AreEqual(RoomPhase.Safe, s.PhaseOf("Playroom", 1000), "the playroom never closes");
            Assert.AreEqual(0f, s.DamagePerSecond(first, 44.9));
            Assert.AreEqual(8f, s.DamagePerSecond(first, 45), 1e-5);
            Assert.AreEqual(12f, s.DamagePerSecond(first, 47), 1e-5, "and it grows");
            Assert.AreEqual(10, s.SecondsUntilFill(first, 35), 1e-9);
            var lastClose = s.Closures.Last().ClosedAt;
            Assert.Less(lastClose, rules.RoundTimeLimitSeconds, "every room is packed before the time limit");
            CollectionAssert.AreEqual(new[] { "Playroom" }, s.OpenRooms(house.Rooms.Select(r => r.Name), lastClose).ToList());
        }

        [Test]
        public void NoClearOutWhenTheModeTurnsItOff()
        {
            var s = new ClearOutSchedule(TestData.RulesFor("MovingDay"), house.ClearOutOrders["Dibs"]);
            Assert.AreEqual(0, s.Closures.Count);
            Assert.AreEqual(RoomPhase.Safe, s.PhaseOf("Kitchen", 500));
        }

        static IEnumerable<List<string>> Permutations(List<string> items)
        {
            if (items.Count <= 1)
            {
                yield return new List<string>(items);
                yield break;
            }
            for (int i = 0; i < items.Count; i++)
            {
                var rest = new List<string>(items);
                rest.RemoveAt(i);
                foreach (var tail in Permutations(rest))
                {
                    tail.Insert(0, items[i]);
                    yield return tail;
                }
            }
        }
    }
}
