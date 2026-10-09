using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class HudMapTests
    {
        sealed class MapInput : InputBinding
        {
            public override string Id => "map-test-local";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        bool? previousFocusOverride;
        GameHud hud;
        RoomBuilder builder;
        PlayerController player;
        const string MapPath = "Safe HUD/Side column/Minimap/Floor/House plan";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousFocusOverride = GameHud.PauseOnFocusLossOverride;
            GameHud.PauseOnFocusLossOverride = false;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameHud.PauseOnFocusLossOverride = previousFocusOverride;
            yield return TestScenes.Reset();
        }

        IEnumerator Load(string map = "pinwheel", string mode = "Dibs")
        {
            Session.SelectMap(map); Match.ModeOverride = mode;
            yield return TestScenes.Load(mode == "MovingDay" ? Session.MovingDayScene : Session.DibsScene);
            builder = Object.FindAnyObjectByType<RoomBuilder>();
            player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new MapInput());
            hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.ExploreHouse();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreSame(player, hud.LocalPlayer);
        }

        Transform Plan => hud.transform.Find(MapPath);

        Vector2 At(Vector3 point)
        {
            var box = builder.Layout.Rooms.First(r => Plan.Find("Rooms/" + r.Name));
            var room = (RectTransform)Plan.Find("Rooms/" + box.Name);
            return new Vector2(Mathf.LerpUnclamped(room.anchorMin.x, room.anchorMax.x, (point.x - box.MinX) / (box.MaxX - box.MinX)),
                Mathf.LerpUnclamped(room.anchorMin.y, room.anchorMax.y, (point.z - box.MinZ) / (box.MaxZ - box.MinZ)));
        }

        [UnityTest]
        public IEnumerator RealDoorwaysRemainOpenAndFacingTracksTheLocalPlayerWithoutRebuilding()
        {
            yield return Load();
            var originalPlan = Plan;
            var walls = Plan.Find("Walls and passages").GetComponentsInChildren<Image>();
            Assert.Greater(walls.Length, builder.Layout.Rooms.Count * 2);
            foreach (var door in builder.Layout.Doors)
            {
                Vector2 at = At(new Vector3(door.X, 0f, door.Z));
                foreach (var wall in walls)
                {
                    var rt = wall.rectTransform;
                    bool vertical = Mathf.Abs(rt.anchorMin.x - rt.anchorMax.x) < .00001f;
                    bool onWall = vertical ? Mathf.Abs(at.x - rt.anchorMin.x) < .00001f : Mathf.Abs(at.y - rt.anchorMin.y) < .00001f;
                    bool betweenEnds = vertical ? at.y > rt.anchorMin.y + .00001f && at.y < rt.anchorMax.y - .00001f
                        : at.x > rt.anchorMin.x + .00001f && at.x < rt.anchorMax.x - .00001f;
                    Assert.IsFalse(onWall && betweenEnds, door.A + "–" + door.B + " must be an open passage on the map.");
                }
            }
            var labels = Plan.Find("Room names").GetComponentsInChildren<TMP_Text>();
            Assert.IsTrue(labels.All(label => label.text.Length >= 3), "Rooms use readable names, not ambiguous initials.");
            player.LookYaw = 1.1f; player.Frozen = false;
            yield return new WaitForSecondsRealtime(.25f);
            var marker = (RectTransform)Plan.Find("Markers/Player " + (player.Index + 1));
            Assert.NotNull(marker.Find("Facing"));
            float expected = -Mathf.Atan2(player.Facing.x, player.Facing.z) * Mathf.Rad2Deg;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(expected, marker.localEulerAngles.z)), 1f);
            Assert.AreSame(originalPlan, Plan, "Turning the player updates markers without rebuilding map geometry.");
        }

        [UnityTest]
        public IEnumerator FurnitureFootprintsFollowAuthoredMovementBreaksAndRoundReset()
        {
            yield return Load();
            var prop = builder.Originals.First(p => p && p.Word == "TABLE");
            string markerName = "Furniture/Obstacle " + prop.Word + " " + prop.GetHashCode();
            yield return TestScenes.WaitUntil(() => Plan.Find(markerName), 1f, "table footprint");
            var marker = (RectTransform)Plan.Find(markerName);
            Assert.IsTrue(marker.gameObject.activeSelf);
            var before = (marker.anchorMin + marker.anchorMax) * .5f;
            prop.GetComponent<Rigidbody>().isKinematic = true;
            prop.transform.position += new Vector3(.8f, 0f, .6f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.4f);
            var after = (marker.anchorMin + marker.anchorMax) * .5f;
            Assert.Greater(after.x, before.x); Assert.Greater(after.y, before.y);
            var bounds = prop.GetComponentsInChildren<Collider>().First(c => c.enabled && !c.isTrigger).bounds;
            foreach (var collider in prop.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
            Assert.Less(Vector2.Distance(after, At(bounds.center)), .0001f, "The footprint uses live authored collider bounds, not JSON placement.");
            prop.Break();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsFalse(Plan.Find(markerName), "A broken obstacle must leave the map.");
            builder.ResetRoom();
            yield return new WaitForSecondsRealtime(.4f);
            var replacement = builder.Originals.First(p => p && p.Word == "TABLE");
            var restored = Plan.Find("Furniture/Obstacle " + replacement.Word + " " + replacement.GetHashCode());
            Assert.IsNotNull(restored, "Reset furniture receives a new live map reference.");
            Assert.IsTrue(restored.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator MovingDayCreatedFurnitureAppearsMovesAndBreaksWithoutRebuildingTheMap()
        {
            yield return Load(mode: "MovingDay");
            Object.FindAnyObjectByType<MovingDayDirector>().enabled = false;
            Assert.IsEmpty(builder.Originals, "Moving Day begins with no original furniture.");
            var originalPlan = Plan;
            var room = builder.Layout.Rooms[0];
            var point = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);
            var prop = FurnitureCatalog.Spawn("TABLE", point, 0f, World.Transient);
            prop.GetComponent<Rigidbody>().isKinematic = true;
            Physics.SyncTransforms();
            string name = "Furniture/Obstacle TABLE " + prop.GetHashCode();
            yield return TestScenes.WaitUntil(() => Plan.Find(name) && Plan.Find(name).gameObject.activeSelf, 2f, "newly created furniture map footprint");
            var marker = (RectTransform)Plan.Find(name);
            Vector2 before = (marker.anchorMin + marker.anchorMax) * .5f;
            prop.transform.position += new Vector3(.7f, 0f, .4f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.4f);
            Vector2 after = (marker.anchorMin + marker.anchorMax) * .5f;
            Assert.Greater(after.x, before.x); Assert.Greater(after.y, before.y);
            Assert.AreSame(originalPlan, Plan, "Placement updates reuse the cached floor plan.");
            prop.Break();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsFalse(Plan.Find(name), "Broken spawned furniture disappears before the next discovery scan.");
        }

        [UnityTest]
        public IEnumerator UpperFloorShowsItsRoomsAndNavigableStairsWithTheSameProjection()
        {
            yield return Load("terrace");
            var floors = builder.Layout.StoreyFloors();
            Assert.Greater(floors.Count, 1);
            var stairs = Plan.Find("Stairs");
            Assert.Greater(stairs.childCount, 0);
            Assert.IsTrue(stairs.GetComponentsInChildren<Transform>().Any(t => t.name == "Stair direction"));
            var groundPlan = Plan;
            var upper = builder.Layout.Rooms.First(r => r.FloorY == floors[1]);
            player.Respawn(new Vector3((upper.MinX + upper.MaxX) * .5f, upper.FloorY + .08f, (upper.MinZ + upper.MaxZ) * .5f));
            player.Frozen = true;
            builder.GetComponent<StoreyCutaway>().Refresh();
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreNotSame(groundPlan, Plan);
            Assert.AreEqual(builder.Layout.StoreyLabel(1), Plan.Find("Storey").GetComponent<TMP_Text>().text);
            foreach (var box in builder.Layout.Rooms)
                Assert.AreEqual(builder.Layout.StoreyOf(box) == 1, Plan.Find("Rooms/" + box.Name) != null, box.Name);
            Assert.Greater(Plan.Find("Stairs").childCount, 0);
            var marker = (RectTransform)Plan.Find("Markers/Player " + (player.Index + 1));
            Assert.Less(Vector2.Distance(marker.anchorMin, At(player.transform.position)), .0001f);
        }

        [UnityTest]
        public IEnumerator MovingDayMarksActualDeliveryPointAndOnlyPendingRoomObjectives()
        {
            yield return Load(mode: "MovingDay");
            var mode = Object.FindAnyObjectByType<MovingDayDirector>();
            var extraction = (RectTransform)Plan.Find("Markers/Delivery point");
            Assert.IsNotNull(extraction);
            var layout = builder.Layout;
            Assert.Less(Vector2.Distance(extraction.anchorMin, At(new Vector3(layout.ExtractionX, 0f, layout.ExtractionZ))), .0001f);
            foreach (var box in layout.Rooms)
            {
                var label = Plan.Find("Room names/" + box.Name).GetComponent<TMP_Text>();
                Assert.AreEqual(mode.Remaining.Any(item => item.room == box.Name), label.text.StartsWith("+ "), box.Name);
            }
        }
    }
}
