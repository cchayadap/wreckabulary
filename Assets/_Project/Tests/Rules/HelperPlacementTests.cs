using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HelperPlacementTests
    {
        static readonly HelperGuard[] NoGuards = new HelperGuard[0];
        static readonly HelperFootprint[] NoBlockers = new HelperFootprint[0];

        internal static HouseLayout Layout()
        {
            var h = new HouseLayout { Name = "test" };
            h.Rooms.Add(new RoomBox { Name = "Hall", MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 6f, FloorY = 0f });
            h.Rooms.Add(new RoomBox { Name = "Den", MinX = 10f, MinZ = 0f, MaxX = 16f, MaxZ = 6f, FloorY = 0f });
            h.Rooms.Add(new RoomBox { Name = "Corridor", MinX = 16f, MinZ = 0f, MaxX = 18f, MaxZ = 6f, FloorY = 0f });
            h.Rooms.Add(new RoomBox { Name = "Loft", MinX = 0f, MinZ = 0f, MaxX = 10f, MaxZ = 6f, FloorY = 3f });
            h.Doors.Add(new Doorway { A = "Hall", B = "Den", X = 10f, Z = 3f, Width = 1f });
            h.Doors.Add(new Doorway { A = "Den", B = "Corridor", X = 16f, Z = 1f, Width = 1f });
            h.Stairs.Add(new Stairway { Lower = "Hall", Upper = "Loft", FromX = 9f, FromZ = 1f, ToX = 9f, ToZ = 5f, Width = 1f });
            h.Spawns.Add(new SpawnPoint { Room = "Hall", X = 1f, Z = 1f });
            return h;
        }

        static HelperRefusal Place(HelperPlaceKind kind, float x, float z, float sizeX = .5f, float sizeZ = .5f, float y = 0f,
            IReadOnlyList<HelperGuard> guards = null, IReadOnlyList<HelperFootprint> blockers = null, float yaw = 0f) =>
            HelperPlacement.Check(Layout(), kind, x, y, z, HelperFootprint.Around(x, z, sizeX, sizeZ, yaw), guards ?? NoGuards, blockers ?? NoBlockers);

        [Test]
        public void AnOpenFloorSpotIsFineForEveryKind()
        {
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Mover, 5f, 3f));
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Stayer, 5f, 3f));
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 5f, 3f, 1.4f, .3f));
        }

        [Test]
        public void NothingGoesOnASpawnPoint()
        {
            Assert.AreEqual(HelperRefusal.Spawn, Place(HelperPlaceKind.Stayer, 1.8f, 1.8f), "spikes under a respawning player");
            Assert.AreEqual(HelperRefusal.Spawn, Place(HelperPlaceKind.Mover, 2f, 1.5f));
            Assert.AreEqual(HelperRefusal.Spawn, Place(HelperPlaceKind.Wall, 2.6f, 1f, 1.6f, .3f), "a long wall reaching over the spawn");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Stayer, 2f, 2f, y: 3f), "the loft above the spawn is another floor");
        }

        [Test]
        public void NothingGoesOnADownedPlayer()
        {
            var downed = new[] { new HelperGuard(5f, 3f, HelperPlacement.DownedClear, HelperRefusal.Downed) };
            Assert.AreEqual(HelperRefusal.Downed, Place(HelperPlaceKind.Stayer, 5.8f, 3.5f, guards: downed), "a trap on the revive spot");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Stayer, 7.5f, 3f, guards: downed));
        }

        [Test]
        public void WallsAndTrapsStayOutOfDoorwaysButMoversMayStartThere()
        {
            Assert.AreEqual(HelperRefusal.Doorway, Place(HelperPlaceKind.Wall, 10.9f, 3f, 1.4f, .3f));
            Assert.AreEqual(HelperRefusal.Doorway, Place(HelperPlaceKind.Stayer, 10.6f, 3.4f));
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Mover, 10.6f, 3.4f), "a chaser may set off from a doorway");
            Assert.AreEqual(HelperRefusal.Doorway, Place(HelperPlaceKind.Wall, 11.8f, 3f, 1.6f, .3f), "the centre is 1.8 m away but the wall's end reaches the door");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 13f, 3f, 1.6f, .3f));
        }

        [Test]
        public void EscapeExitsKeysAndTheVanStayClear()
        {
            var guards = new[]
            {
                new HelperGuard(14f, 5f, HelperPlacement.ObjectiveClear, HelperRefusal.Exit),
                new HelperGuard(3f, 5f, HelperPlacement.ObjectiveClear, HelperRefusal.Key),
                new HelperGuard(7f, 1f, HelperPlacement.ObjectiveClear, HelperRefusal.Van),
            };
            Assert.AreEqual(HelperRefusal.Exit, Place(HelperPlaceKind.Wall, 13f, 3.5f, 1.4f, .3f, guards: guards));
            Assert.AreEqual(HelperRefusal.Key, Place(HelperPlaceKind.Stayer, 4f, 4f, guards: guards));
            Assert.AreEqual(HelperRefusal.Van, Place(HelperPlaceKind.Mover, 6f, 2f, guards: guards));
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 6.5f, 4f, 1.4f, .3f, guards: guards));
        }

        [Test]
        public void NothingGoesOutsideTheHouseOrOnStairs()
        {
            Assert.AreEqual(HelperRefusal.OutsideRooms, Place(HelperPlaceKind.Mover, 25f, 3f));
            Assert.AreEqual(HelperRefusal.OutsideRooms, Place(HelperPlaceKind.Wall, 5f, -3f, 1.4f, .3f));
            Assert.AreEqual(HelperRefusal.Stairs, Place(HelperPlaceKind.Mover, 9f, 3f));
            Assert.AreEqual(HelperRefusal.Stairs, Place(HelperPlaceKind.Wall, 7.9f, 3f, 1.6f, .3f), "a wall overhanging the stairs");
        }

        [Test]
        public void TrapsAndWallsNeedTheFloor()
        {
            Assert.AreEqual(HelperRefusal.OffTheFloor, Place(HelperPlaceKind.Stayer, 13f, 3f, y: .6f), "on top of a bed");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Stayer, 13f, 3f, y: HelperPlacement.FloorTolerance * .5f));
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Mover, 13f, 3f, y: .6f), "a mover hops down");
        }

        [Test]
        public void AWallNeverSealsARoom()
        {
            Assert.AreEqual(HelperRefusal.Seals, Place(HelperPlaceKind.Wall, 17f, 4f, 1.6f, .3f), "a 2 m corridor keeps less than 0.9 m");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 17.45f, 4f, 1f, .3f), "0.9 m left to squeeze past");
            var cover = new[] { new HelperFootprint(16f, 3.8f, 16.9f, 4.2f) };
            Assert.AreEqual(HelperRefusal.Seals, Place(HelperPlaceKind.Wall, 17.45f, 4f, 1f, .3f, blockers: cover), "two walls together seal it too");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 17f, 5.85f, 1.6f, .3f), "flush against the end wall seals nothing");
            Assert.AreEqual(HelperRefusal.None, Place(HelperPlaceKind.Wall, 17f, 3f, 1.6f, .16f, yaw: 90f), "turned along the corridor it leaves over 0.9 m on both sides");
            Assert.AreEqual(HelperRefusal.Seals, Place(HelperPlaceKind.Wall, 17f, 3f, 1.6f, .4f, yaw: 90f), "a thicker one along the corridor does not");
        }

        [Test]
        public void TodaysCoversAndTimedWallsBothFollowThePlacementRules()
        {
            var fence = new ItemDefinition { Id = "FENCE", Family = HandlingFamily.DeployCover, Deploy = new DeployStats { Effect = DeployEffect.Cover } };
            var cone = new ItemDefinition { Id = "CONE", Family = HandlingFamily.DeployCover, Deploy = new DeployStats { Effect = DeployEffect.Cover, LifetimeSeconds = 8f } };
            var pad = new ItemDefinition { Id = "BED", Family = HandlingFamily.DeployPad, Deploy = new DeployStats { Effect = DeployEffect.JumpPad } };
            Assert.IsTrue(HelperPlacement.Guards(cone));
            Assert.AreEqual(HelperPlacement.EveryCover, HelperPlacement.Guards(fence), "one rule for every placed cover unless the owner turns it off");
            Assert.IsFalse(HelperPlacement.Guards(pad), "pads and strips keep today's rules");
            Assert.IsFalse(HelperPlacement.Guards(null));
        }

        [Test]
        public void EveryRealSpawnAndDoorwayIsKeptClear()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(TestData.Root, "Assets/_Project/Data/Config"), "house_*.json"))
            {
                var layout = HouseLayout.FromJson(File.ReadAllText(file), Path.GetFileName(file));
                foreach (var spawn in layout.Spawns)
                {
                    var room = layout.Room(spawn.Room);
                    Assert.AreNotEqual(HelperRefusal.None, HelperPlacement.Check(layout, HelperPlaceKind.Stayer, spawn.X, room.FloorY, spawn.Z,
                        HelperFootprint.Around(spawn.X, spawn.Z, .5f, .5f, 0f), NoGuards, NoBlockers), Path.GetFileName(file) + " spawn in " + spawn.Room);
                }
                foreach (var door in layout.Doors)
                {
                    var room = layout.Room(door.A);
                    var refusal = HelperPlacement.Check(layout, HelperPlaceKind.Wall, door.X, room.FloorY, door.Z,
                        HelperFootprint.Around(door.X, door.Z, 1.4f, .3f, 0f), NoGuards, NoBlockers);
                    Assert.AreNotEqual(HelperRefusal.None, refusal, Path.GetFileName(file) + " door " + door.A + "-" + door.B);
                }
            }
        }
    }

    public class HelperRouteTests
    {
        [Test]
        public void TheSameRoomGoesStraightToTheGoal()
        {
            var route = new HelperRoute(HelperPlacementTests.Layout());
            Assert.IsTrue(route.Next(2f, 0f, 3f, 6f, 0f, 4f, out float x, out float z));
            Assert.AreEqual(6f, x, 1e-4f);
            Assert.AreEqual(4f, z, 1e-4f);
        }

        [Test]
        public void AnotherRoomOnTheSameFloorGoesThroughTheDoor()
        {
            var route = new HelperRoute(HelperPlacementTests.Layout());
            Assert.IsTrue(route.Next(5f, 0f, 3f, 14f, 0f, 3f, out float x, out float z));
            Assert.AreEqual(10f, x, 1e-4f, "heads for the doorway first");
            Assert.AreEqual(3f, z, 1e-4f);
            Assert.IsTrue(route.Next(9.8f, 0f, 3f, 14f, 0f, 3f, out x, out z));
            Assert.Greater(x, 10.5f, "at the doorway it steps through into the next room");
        }

        [Test]
        public void ChasersNeverUseStairsOrChaseToAnotherFloor()
        {
            var route = new HelperRoute(HelperPlacementTests.Layout());
            Assert.IsFalse(route.Next(5f, 0f, 3f, 5f, 3f, 3f, out _, out _), "the loft is another level");
            Assert.IsFalse(route.Next(2f, 3f, 3f, 14f, 0f, 3f, out _, out _), "no route down the stairs");
        }

        [Test]
        public void WithoutALayoutItGoesStraight()
        {
            var route = new HelperRoute(null);
            Assert.IsTrue(route.Next(0f, 0f, 0f, 3f, 0f, 4f, out float x, out float z));
            Assert.AreEqual(3f, x);
            Assert.AreEqual(4f, z);
        }
    }
}
