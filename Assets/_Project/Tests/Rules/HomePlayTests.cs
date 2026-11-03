using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HomePlayTests
    {
        static HouseLayout Map(string id) => HouseLayout.FromJson(TestData.Read("house_" + id + ".json"), "house_" + id + ".json");

        static HomeDesigner Designer() => new HomeDesigner(Map, TestData.Catalogue());

        [Test]
        public void OnlyLastTeamStandingModesTakeACustomHome()
        {
            CollectionAssert.AreEqual(new[] { "Dibs", "Duos" }, HomeDesigner.PlayableModes);
            foreach (string mode in new[] { "Dibs", "Duos" }) Assert.IsTrue(HomeDesigner.Playable(mode), mode);
            foreach (string mode in new[] { "MovingOut", "MovingDay", "Escape", "Tutorial", "Workshop", "", null })
                Assert.IsFalse(HomeDesigner.Playable(mode), mode + " needs objective data a home save does not have");
        }

        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        public void ASavedHomeBecomesFurnitureInTheRoomsItWasPlacedIn(string map)
        {
            var designer = Designer();
            var house = Map(map);
            var layout = designer.CreateLayout(map, "Party Home");
            foreach (var room in house.Rooms.Take(3))
            {
                var batch = designer.AddWords(layout, "TABLE PLANT", room.Name);
                Assert.IsTrue(batch.Ok, string.Join("\n", batch.Errors));
                layout = batch.Layout;
            }
            layout.Props[0].Yaw = (layout.Props[0].Yaw + 180) % 360; layout.Props[1].Skin = "Arcade";
            Assert.IsTrue(designer.Validate(layout).Ok);
            Assert.IsTrue(designer.TryFurnish(layout, out var furniture, out string error), error);
            Assert.IsNull(error);
            Assert.AreEqual(layout.Props.Count, furniture.Count);
            for (int i = 0; i < furniture.Count; i++)
            {
                var prop = layout.Props[i]; var item = furniture[i];
                Assert.AreEqual(prop.Word, item.Word);
                Assert.AreEqual((float)prop.X, item.X); Assert.AreEqual((float)prop.Z, item.Z);
                Assert.AreEqual(prop.Yaw, item.Yaw); Assert.AreEqual(prop.Skin, item.Skin);
                Assert.AreEqual(house.RoomAt(item.X, item.Z), item.Room, "each piece stands in the room it was built in");
            }
        }

        [Test]
        public void AnInvalidHomeIsNeverFurnished()
        {
            var designer = Designer();
            var batch = designer.AddWords(designer.CreateLayout("pinwheel"), "TABLE", Map("pinwheel").Rooms[0].Name);
            batch.Layout.Props[0].X = double.NaN;
            Assert.IsFalse(designer.TryFurnish(batch.Layout, out var furniture, out string error));
            Assert.IsNull(furniture); Assert.IsNotEmpty(error);
            Assert.IsFalse(designer.TryFurnish(designer.CreateLayout("flat"), out furniture, out error), "homes are built only on supported maps");
            Assert.IsFalse(designer.TryFurnish(null, out furniture, out error));
        }

        [Test]
        public void AnEmptyHomeIsPlayableWithNoFurniture()
        {
            Assert.IsTrue(Designer().TryFurnish(Designer().CreateLayout("courtyard"), out var furniture, out _));
            Assert.IsEmpty(furniture);
        }
    }

    public class SpawnOrderTests
    {
        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
        public void TheFirstTwoSeatsAreTheFarthestWalkApartAndTheThirdBalancesThem(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            var walk = Walking(house);
            int seats = house.Spawns.Count;
            Assert.GreaterOrEqual(seats, 4);
            double farthest = 0;
            for (int a = 0; a < seats; a++)
                for (int b = a + 1; b < seats; b++) farthest = Math.Max(farthest, walk[a, b]);
            Assert.AreEqual(farthest, walk[0, 1], 1e-3, file + ": players 1 and 2 start as far apart as the house allows");
            double third = Math.Min(walk[2, 0], walk[2, 1]);
            for (int k = 3; k < seats; k++)
                Assert.GreaterOrEqual(third + 1e-3, Math.Min(walk[k, 0], walk[k, 1]), file + ": player 3 starts as far from both as any seat");
        }

        [Test]
        public void TheCityFlatNoLongerStartsTwoPlayersNextDoor()
        {
            var flat = HouseLayout.FromJson(TestData.Read("house_flat.json"), "house_flat.json");
            var a = flat.Spawns[0]; var b = flat.Spawns[1];
            Assert.Greater(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)), 15);
        }

        static double[,] Walking(HouseLayout house)
        {
            var points = new List<(double x, double y, double z, HashSet<string> rooms)>();
            foreach (var spawn in house.Spawns)
                points.Add((spawn.X, house.Room(spawn.Room).FloorY, spawn.Z, new HashSet<string> { spawn.Room }));
            foreach (var door in house.Doors)
                points.Add((door.X, house.Room(door.A).FloorY, door.Z, new HashSet<string> { door.A, door.B }));
            var flights = new List<(int foot, int top)>();
            foreach (var stairs in house.Stairs)
            {
                points.Add((stairs.FromX, house.Room(stairs.Lower).FloorY, stairs.FromZ, new HashSet<string> { stairs.Lower }));
                points.Add((stairs.ToX, house.Room(stairs.Upper).FloorY, stairs.ToZ, new HashSet<string> { stairs.Upper }));
                flights.Add((points.Count - 2, points.Count - 1));
            }
            int n = points.Count;
            var d = new double[n, n];
            double Gap(int i, int j)
            {
                double dx = points[i].x - points[j].x, dy = points[i].y - points[j].y, dz = points[i].z - points[j].z;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    d[i, j] = i == j ? 0 : points[i].rooms.Overlaps(points[j].rooms) ? Gap(i, j) : double.PositiveInfinity;
            foreach (var (foot, top) in flights) d[foot, top] = d[top, foot] = Gap(foot, top);
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (d[i, k] + d[k, j] < d[i, j]) d[i, j] = d[i, k] + d[k, j];
            return d;
        }
    }
}
