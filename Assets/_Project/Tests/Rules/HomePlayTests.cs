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

    public class HomeMigrationTests
    {
        static HouseLayout Map(string id) => HouseLayout.FromJson(TestData.Read("house_" + id + ".json"), "house_" + id + ".json");

        static HomeDesigner Designer() => new HomeDesigner(Map, TestData.Catalogue());

        static HomeLayout OldPinwheelHome()
        {
            var home = new HomeLayout { Map = "pinwheel", Name = "Old Home" };
            home.Props.Add(new HomeProp { Id = "sofa", Word = "SOFA", X = -9, Z = -9 });
            home.Props.Add(new HomeProp { Id = "table", Word = "TABLE", X = -6, Z = 7, Yaw = 90 });
            home.Props.Add(new HomeProp { Id = "plant", Word = "PLANT", X = 0, Z = 0, Skin = "Candy" });
            home.Props.Add(new HomeProp { Id = "lamp", Word = "LAMP", X = -3, Z = 9.5 });
            return home;
        }

        [Test]
        public void AHomeFromTheSmallPinwheelScalesIntoTheSameRoomsOfTheBigOne()
        {
            var house = Map("pinwheel");
            var migrated = Designer().Migrate(OldPinwheelHome());
            Assert.IsTrue(migrated.Ok, string.Join("\n", migrated.Errors));
            Assert.AreEqual(0, migrated.Dropped, "no piece is lost");
            CollectionAssert.AreEqual(new[] { "sofa", "table", "plant", "lamp" }, migrated.Layout.Props.Select(p => p.Id));
            var props = migrated.Layout.Props;
            Assert.AreEqual(-12.5, props[0].X); Assert.AreEqual(-12.5, props[0].Z);
            Assert.AreEqual(-8.5, props[1].X); Assert.AreEqual(10, props[1].Z);
            Assert.AreEqual(90, props[1].Yaw);
            Assert.AreEqual("Candy", props[2].Skin);
            CollectionAssert.AreEqual(new[] { "LivingRoom", "Bedroom", "Playroom", "Bedroom" }, props.Select(p => house.RoomAt((float)p.X, (float)p.Z)));
            Assert.AreEqual(1, migrated.Moved, "the lamp would block the new garden door, so it steps aside");
            Assert.AreNotEqual((-4.0, 13.5), (props[3].X, props[3].Z));
            Assert.IsTrue(Designer().Validate(migrated.Layout).Ok);
        }

        [Test]
        public void AHomeFromTheSmallCourtyardScalesByHalfAgain()
        {
            var old = new HomeLayout { Map = "courtyard", Name = "Old Courtyard" };
            old.Props.Add(new HomeProp { Id = "p1", Word = "SOFA", X = -11, Z = -8, Yaw = 270, Skin = "Arcade" });
            var migrated = Designer().ImportLegacy(old.ToJson());
            Assert.IsTrue(migrated.Ok, string.Join("\n", migrated.Errors));
            Assert.AreEqual(-16.5, migrated.Layout.Props[0].X);
            Assert.AreEqual(-12, migrated.Layout.Props[0].Z);
            Assert.AreEqual(0, migrated.Moved);
            Assert.AreEqual(1.5, HomeDesigner.LegacyScale("courtyard"));
            Assert.AreEqual(1.4, HomeDesigner.LegacyScale("pinwheel"), "the small pinwheel maps onto the big house, not its new garden");
        }

        [Test]
        public void ACurrentHomeIsNeverScaledAndABrokenLegacyFileIsRefused()
        {
            var designer = Designer();
            var current = designer.AddWords(designer.CreateLayout("pinwheel", "New Home"), "TABLE", "Playroom").Layout;
            var imported = designer.Import(current.ToJson());
            Assert.IsTrue(imported.Ok);
            Assert.AreEqual(current.ToJson(), imported.Layout.ToJson());
            Assert.IsFalse(designer.ImportLegacy("{\"schema\":1,").Ok);
            Assert.IsFalse(designer.ImportLegacy(designer.CreateLayout("flat").ToJson()).Ok, "homes exist only on the maps that support them");
        }

        [Test]
        public void TheBiggerHousesHoldTwiceAsManyPieces()
        {
            var designer = Designer();
            var full = designer.AddWords(designer.CreateLayout("courtyard", "Full"), string.Join(" ", Enumerable.Repeat("APPLE", 64)), "FountainSquare").Layout;
            full = designer.AddWords(full, string.Join(" ", Enumerable.Repeat("APPLE", 64)), "Orchard").Layout;
            Assert.AreEqual(HomeDesigner.MaxProps, full.Props.Count);
            Assert.AreEqual(128, HomeDesigner.MaxProps);
            var over = designer.AddWords(full, "APPLE", "Bedroom");
            Assert.AreEqual(1, over.Rejected.Count);
            StringAssert.Contains("128", over.Rejected[0].Reason);
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
            Assert.Greater(Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)), 30, "across the block, not next door");
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
