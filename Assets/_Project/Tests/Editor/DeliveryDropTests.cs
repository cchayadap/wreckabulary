using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wreckabulary.Tests
{
    public class DeliveryDropTests
    {
        [SetUp] public void SetUp() => GameConfig.Use(null);
        [TearDown] public void TearDown() => GameConfig.Use(null);

        [TestCase("courtyard")]
        [TestCase("pinwheel")]
        [TestCase("flat")]
        public void DropsReachEveryRoomOfTheHouse(string map)
        {
            var house = GameConfig.Current.HouseFor(map);
            var go = new GameObject("Deliveries");
            try
            {
                var spawner = go.AddComponent<DeliverySpawner>();
                spawner.Layout = house;
                UnityEngine.Random.InitState(7);
                var reached = new HashSet<string>();
                bool outside = false;
                for (int i = 0; i < 800; i++)
                {
                    var at = spawner.DropPoint();
                    string room = house.RoomAt(at.x, at.z);
                    Assert.IsNotNull(room, map + ": a drop at " + at + " lands inside the house");
                    reached.Add(room);
                    outside |= at.x < -6.5f || at.x > 6.5f || at.z < -4.2f || at.z > 3.2f;
                }
                CollectionAssert.AreEquivalent(house.Rooms.Select(room => room.Name), reached, map + ": every room restocks");
                Assert.IsTrue(outside, map + ": drops leave the old fixed centre area");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ClosedRoomsStopReceivingDropsWhileAnyRoomIsOpen()
        {
            var house = GameConfig.Current.HouseFor("courtyard");
            CollectionAssert.AreEqual(new[] { "Garden" }, DeliverySpawner.DropRooms(house, room => room == "Garden").Select(room => room.Name));
            CollectionAssert.AreEquivalent(house.Rooms.Select(room => room.Name), DeliverySpawner.DropRooms(house, room => false).Select(room => room.Name),
                "with every room closed the whole house stays in play");
            Assert.IsEmpty(DeliverySpawner.DropRooms(null, null));
        }
    }
}
