using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ThirdPersonCameraTests
    {
        Camera lens;
        CameraRig rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            lens = new GameObject("Main Camera").AddComponent<Camera>();
            lens.tag = "MainCamera";
            lens.transform.SetPositionAndRotation(new Vector3(0f, 12f, -9f), Quaternion.Euler(50f, 0f, 0f));
            rig = lens.gameObject.AddComponent<CameraRig>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameHud.PauseOnFocusLossOverride = null;
            Time.timeScale = 1f;
            CursorPolicy.Apply(false);
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        sealed class LookBinding : InputBinding
        {
            public PlayerCommands Next;
            public bool Look = true;
            public override string Id => "test-look";
            public override bool CanLook => Look;
            public override void Read(ref PlayerCommands c)
            {
                c = Next;
                Next.lookDelta = Vector2.zero;
                Next.jump = false;
            }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static PlayerController Spawn(InputBinding binding, Vector3 at, int index = 0)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(index, binding);
            p.Inventory.Collects = false;
            p.Respawn(at);
            return p;
        }

        static IEnumerator Settle() { yield return new WaitForSeconds(.4f); }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        static void SetPaused(GameHud hud, bool on) =>
            typeof(GameHud).GetMethod("SetPaused", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hud, new object[] { on });

        [UnityTest]
        public IEnumerator SoloCameraSitsCentredBehindThePlayer()
        {
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();

            Assert.AreSame(p, rig.Target);
            Assert.IsTrue(p.ShooterView);
            Assert.IsFalse(lens.orthographic);
            Assert.AreEqual(ShoulderView.FieldOfView, lens.fieldOfView, .01f);
            Assert.AreEqual(ShoulderView.NearClip, lens.nearClipPlane, .001f);
            var feet = p.transform.position;
            var at = lens.transform.position;
            Debug.Log($"THIRD_PERSON feet {feet} camera {at}");
            Assert.AreEqual(feet.x, at.x, .05f, "centred behind, not over a shoulder");
            Assert.AreEqual(-3.258f, at.z - feet.z, .1f, "behind the player");
            Assert.AreEqual(1.776f, at.y - feet.y, .1f, "just above the player's head");
            Assert.AreEqual(0f, Yaw(lens.transform.forward), .5f, "looking the way the player faces");
        }

        [UnityTest]
        public IEnumerator TheWholeBodyStandsCentredInView()
        {
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            var (feet, head) = BodyInView(p, "FRAMING");
            Assert.AreEqual(.5f, head.x, .02f, "centred left to right");
            Assert.Greater(feet.y, .2f, "the feet stand clear of the letter tray");
            Assert.Less(head.y, .62f, "the head stays below the crosshair line");
            Assert.Greater(head.y - feet.y, .2f, "the body is big enough to read");
        }

        (Vector3 feet, Vector3 head) BodyInView(PlayerController p, string tag)
        {
            Bounds? body = null;
            foreach (var r in p.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is not (SkinnedMeshRenderer or MeshRenderer) || r.GetComponentInParent<TMP_Text>()) continue;
                if (body is { } b) { b.Encapsulate(r.bounds); body = b; }
                else body = r.bounds;
            }
            Assert.IsTrue(body.HasValue, "the avatar has visible meshes");
            var box = body.Value;
            var feet = lens.WorldToViewportPoint(new Vector3(box.center.x, box.min.y, box.center.z));
            var head = lens.WorldToViewportPoint(new Vector3(box.center.x, box.max.y, box.center.z));
            Debug.Log($"{tag} height {box.size.y:F2} feet {feet.y:F3} head {head.y:F3} x {head.x:F3} distance {rig.ViewDistance:F2}");
            return (feet, head);
        }

        [UnityTest]
        public IEnumerator AWallBehindLiftsTheCameraOverTheHead()
        {
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test wall";
            wall.transform.position = new Vector3(0f, 1.65f, -1.2f);
            wall.transform.localScale = new Vector3(6f, 3.3f, .2f);
            yield return new WaitForSeconds(1f);
            Debug.Log($"CLIMB camera {lens.transform.position} distance {rig.ViewDistance:F2}");
            Assert.Greater(lens.transform.position.y, p.transform.position.y + ShoulderView.PivotHeight + ShoulderView.Lift + .8f, "rises over the head");
            Assert.Greater(lens.transform.position.z, -1.1f, "still in front of the wall");
            Object.Destroy(wall);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(ShoulderView.Distance, rig.ViewDistance, .05f, "settles back down behind");
        }

        [UnityTest]
        public IEnumerator ACornerSpawnStepsOutForTheCamera()
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.transform.position = new Vector3(0f, 1.65f, -1.2f);
            back.transform.localScale = new Vector3(8f, 3.3f, .2f);
            var side = GameObject.CreatePrimitive(PrimitiveType.Cube);
            side.transform.position = new Vector3(-1.2f, 1.65f, 0f);
            side.transform.localScale = new Vector3(.2f, 3.3f, 8f);
            Physics.SyncTransforms();
            yield return null;
            var spot = ShoulderView.RoomySpot(Vector3.zero, null);
            Debug.Log($"ROOMY spot {spot} openness {ShoulderView.Openness(Vector3.zero):F2} -> {ShoulderView.Openness(spot):F2}");
            Assert.Greater(spot.x, .4f, "away from the side wall");
            Assert.Greater(spot.z, .4f, "away from the back wall");
            Assert.LessOrEqual(new Vector2(spot.x, spot.z).magnitude, 1.51f, "but only a short step");
            Assert.AreEqual(0f, spot.y, 1e-3f);
            var blocked = ShoulderView.RoomySpot(Vector3.zero, at => at.x < .1f);
            Assert.Less(blocked.x, .1f, "only where the map allows");
            Object.Destroy(back);
            Object.Destroy(side);
        }

        [UnityTest]
        public IEnumerator SpawningAgainstAWallFacesTheOpenRoom()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test wall";
            wall.transform.position = new Vector3(0f, 1.65f, -1.2f);
            wall.transform.localScale = new Vector3(6f, 3.3f, .2f);
            Physics.SyncTransforms();
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            var (feet, head) = BodyInView(p, "OPEN");
            Assert.AreEqual(ShoulderView.Distance, rig.ViewDistance, .05f, "the camera has its full distance");
            Assert.AreEqual(.5f, head.x, .02f, "centred left to right");
            Assert.Greater(feet.y, .2f, "the whole body is in view");
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator LookTurnsTheBodyAndTheCameraTogether()
        {
            var input = new LookBinding();
            var p = Spawn(input, Vector3.zero);
            yield return Settle();

            input.Next.lookDelta = new Vector2(.24f, 0f);
            yield return Frames(2);
            Assert.AreEqual(.24f, p.LookYaw, .001f, "one look step, applied once");
            Assert.AreEqual(13.75f, Yaw(p.Facing), .1f, "the body turns with the view");
            Assert.AreEqual(13.75f, Yaw(lens.transform.forward), .5f, "and the camera with it");
        }

        [UnityTest]
        public IEnumerator PitchClampsAndARespawnLevelsIt()
        {
            var input = new LookBinding();
            var p = Spawn(input, Vector3.zero);
            yield return Settle();

            input.Next.lookDelta = new Vector2(0f, 5f);
            yield return Frames(2);
            Assert.AreEqual(ShoulderView.MaxPitch, p.LookPitch, 1e-4f);
            Assert.AreEqual(0f, p.Facing.y, 1e-4f, "looking down doesn't tip the body");

            input.Next.lookDelta = new Vector2(0f, -10f);
            yield return Frames(2);
            Assert.AreEqual(ShoulderView.MinPitch, p.LookPitch, 1e-4f);
            Assert.AreEqual(0f, Yaw(p.Facing), .1f, "looking up still aims straight ahead");
            Assert.Greater(lens.transform.position.y, p.transform.position.y + ShoulderView.MinDistance - .01f, "never under the floor");

            p.Respawn(new Vector3(2f, 0f, 2f));
            Assert.AreEqual(ShoulderView.DefaultPitch, p.LookPitch, 1e-4f);
            Assert.AreEqual(Mathf.Atan2(p.Facing.x, p.Facing.z), p.LookYaw, 1e-4f, "the view turns to the way you face");
        }

        [UnityTest]
        public IEnumerator MovingFollowsTheCamera()
        {
            var input = new LookBinding();
            var p = Spawn(input, Vector3.zero);
            yield return Settle();

            p.LookYaw = Mathf.PI * .5f;
            input.Next.move = Vector2.up;
            yield return new WaitForSeconds(.5f);
            var at = p.transform.position;
            Assert.Greater(at.x, .8f, "W walks where the camera looks (+x)");
            Assert.AreEqual(0f, at.z, .15f);
            Assert.AreEqual(90f, Yaw(p.Facing), .5f);

            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            var from = p.transform.position;
            input.Next.move = Vector2.right;
            yield return new WaitForSeconds(.5f);
            var step = p.transform.position - from;
            Assert.Less(step.z, -.8f, "D strafes to the camera's right (-z)");
            Assert.AreEqual(0f, step.x, .2f);
            Assert.AreEqual(90f, Yaw(p.Facing), .5f, "strafing keeps facing the crosshair");
        }

        [UnityTest]
        public IEnumerator AKnockedOutPlayerKeepsTheView()
        {
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 1000f, 0f));
            yield return Settle();
            Assert.AreSame(p, rig.Target);
            Assert.IsFalse(lens.orthographic);
        }

        [UnityTest]
        public IEnumerator OnlyOnePersonWhoCanLookGetsTheView()
        {
            var human = Spawn(new LookBinding(), Vector3.zero);
            Spawn(null, new Vector3(3f, 0f, 0f), 1);
            Spawn(new ScriptedBinding(), new Vector3(-3f, 0f, 0f), 2);
            yield return Settle();
            Assert.AreSame(human, rig.Target, "a tutorial dummy and scripted players aren't people at the keyboard");

            var second = Spawn(new LookBinding(), new Vector3(0f, 0f, 3f), 3);
            yield return Frames(2);
            Assert.IsNull(rig.Target, "couch play shares one view");
            Assert.IsFalse(human.ShooterView);

            Object.Destroy(second.gameObject);
            yield return Frames(3);
            Assert.AreSame(human, rig.Target);
            Assert.IsTrue(human.ShooterView);
        }

        [UnityTest]
        public IEnumerator PlayersWhoCantLookKeepTheOldView()
        {
            var touchLike = Spawn(new LookBinding { Look = false }, Vector3.zero);
            var scripted = Spawn(new ScriptedBinding(), new Vector3(3f, 0f, 0f), 1);
            yield return Settle();
            Assert.IsNull(rig.Target);
            Assert.IsFalse(touchLike.ShooterView);
            Assert.IsFalse(scripted.ShooterView);
            Assert.AreEqual(12f, lens.transform.position.y, .5f, "the scene's own camera pose");
        }

        [UnityTest]
        public IEnumerator AWallPullsTheCameraInThenItEasesBackOut()
        {
            Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            Assert.AreEqual(ShoulderView.Distance, rig.ViewDistance, .02f);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test wall";
            wall.transform.position = new Vector3(0f, 2f, -1.5f);
            wall.transform.localScale = new Vector3(6f, 4f, .2f);
            yield return new WaitForSeconds(.1f);
            Debug.Log($"PULL_IN distance {rig.ViewDistance:F3} camera {lens.transform.position}");
            Assert.Less(rig.ViewDistance, 1.3f, "pulled in at once");
            Assert.GreaterOrEqual(rig.ViewDistance, ShoulderView.MinDistance);
            Assert.Greater(lens.transform.position.z, -1.4f, "in front of the wall, not behind it");

            Object.Destroy(wall);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.Less(rig.ViewDistance, 2.45f, "eases out rather than jumping back");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(ShoulderView.Distance, rig.ViewDistance, .05f);
        }

        [UnityTest]
        public IEnumerator FurnitureFadesInsteadOfPullingTheCameraIn()
        {
            Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            var chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = "Test chair";
            chair.transform.position = new Vector3(0f, 1f, -1.5f);
            chair.transform.localScale = new Vector3(2f, 2f, .4f);
            chair.AddComponent<Rigidbody>().isKinematic = true;
            var look = chair.GetComponent<Renderer>();
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(ShoulderView.Distance, rig.ViewDistance, .05f, "furniture doesn't pull the camera in");
            Assert.AreEqual(ShadowCastingMode.ShadowsOnly, look.shadowCastingMode, "it fades to its shadow");
            CollectionAssert.Contains(rig.Faded.ToList(), look);

            chair.transform.position = new Vector3(10f, 1f, 0f);
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(ShadowCastingMode.On, look.shadowCastingMode, "back once it's out of the way");
            Assert.IsEmpty(rig.Faded);
        }

        [UnityTest]
        public IEnumerator WallsStandTallAroundTheThirdPersonView()
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap("pinwheel");
            var room = new GameObject("Test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            var spawn = room.Layout.Spawns[0];
            var p = Spawn(new LookBinding(), new Vector3(spawn.X, room.Layout.Room(spawn.Room).FloorY + .08f, spawn.Z));
            yield return Settle();

            var walls = room.GetComponentsInChildren<TallWall>();
            Assert.IsNotEmpty(walls);
            Physics.SyncTransforms();
            void Check(bool tall)
            {
                foreach (var w in walls)
                {
                    Assert.AreEqual(w.VisualHeight(tall), w.transform.localScale.y, 1e-3f, w.name);
                    Assert.AreEqual(w.FloorY + w.VisualHeight(tall) * .5f, w.transform.position.y, 1e-3f, "stands on its floor");
                    var b = w.GetComponent<BoxCollider>().bounds;
                    Assert.AreEqual(w.FloorY, b.min.y, .01f, "the collider still starts at the floor");
                    Assert.AreEqual(w.FloorY + w.Height, b.max.y, .01f, "and still blocks the full storey");
                }
            }
            Check(true);
            Assert.IsTrue(walls.Any(w => w.Outside && Mathf.Approximately(w.transform.localScale.y, TallWall.Exterior)), "2.7 m outside");
            Assert.IsTrue(walls.Any(w => !w.Outside && Mathf.Approximately(w.transform.localScale.y, TallWall.Interior)), "2.4 m inside");

            Object.Destroy(p.gameObject);
            yield return Frames(3);
            Physics.SyncTransforms();
            Assert.IsNull(rig.Target);
            Assert.IsTrue(lens.orthographic, "back to the overhead view of the house");
            Check(false);
        }

        [UnityTest]
        public IEnumerator TheCrosshairSitsInTheMiddleAndHidesWithThePause()
        {
            var hud = new GameObject("Test HUD", typeof(Canvas)).AddComponent<GameHud>();
            Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            var cross = (RectTransform)hud.UiCanvas.transform.Find("Crosshair");
            Assert.IsNotNull(cross);
            Assert.IsTrue(cross.gameObject.activeSelf);
            var centre = (Vector2)cross.TransformPoint(cross.rect.center);
            Assert.AreEqual(Screen.width * .5f, centre.x, 2f);
            Assert.AreEqual(Screen.height * .5f, centre.y, 2f);

            SetPaused(hud, true);
            yield return Frames(2);
            Assert.IsFalse(cross.gameObject.activeSelf, "the pause card needs the pointer");
            SetPaused(hud, false);
            yield return Frames(2);
            Assert.IsTrue(cross.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator LosingTheWindowPausesTheMatch()
        {
            GameHud.PauseOnFocusLossOverride = true;
            var hud = new GameObject("Test HUD", typeof(Canvas)).AddComponent<GameHud>();
            var p = Spawn(new LookBinding(), Vector3.zero);
            yield return Settle();
            Assert.AreSame(p, hud.LocalPlayer);
            hud.SendMessage("OnApplicationFocus", false);
            Assert.IsTrue(hud.Paused);
            Assert.IsFalse(CursorPolicy.Locked);
            SetPaused(hud, false);
        }

        [UnityTest]
        public IEnumerator YourOwnTagStepsAsideForTheCamera()
        {
            var me = Spawn(new LookBinding(), Vector3.zero);
            var other = Spawn(new ScriptedBinding(), new Vector3(3f, 0f, 0f), 1);
            yield return Settle();
            var field = typeof(PlayerHud).GetField("lettersText", BindingFlags.NonPublic | BindingFlags.Instance);
            string Tag(PlayerController p) => ((TextMeshPro)field.GetValue(p.GetComponentInChildren<PlayerHud>(true))).text;
            Assert.AreEqual("", Tag(me), "your own name and letters would sit in front of the camera");
            Assert.IsNotEmpty(Tag(other), "everyone else keeps theirs");
        }

        [UnityTest]
        public IEnumerator TheHomeTourFollowsTheRoommateFromBehind()
        {
            rig.enabled = false;
            var house = GameConfig.Current.HouseFor("pinwheel");
            var input = new LookBinding();
            var tour = new GameObject("Test tour").AddComponent<HomeTourDirector>();
            tour.Begin(house, input, lens);
            yield return Settle();
            Assert.IsTrue(tour.ThirdPerson);
            Assert.IsTrue(tour.Player.ShooterView);
            Assert.IsFalse(lens.orthographic);
            Assert.AreEqual(ShoulderView.FieldOfView, lens.fieldOfView, .01f);
            float behind = Vector3.Distance(Vector3.Scale(lens.transform.position, new Vector3(1f, 0f, 1f)),
                Vector3.Scale(tour.Player.transform.position, new Vector3(1f, 0f, 1f)));
            Assert.That(behind, Is.InRange(.3f, ShoulderView.Distance + .1f), "close behind the roommate");

            float yaw = tour.Player.LookYaw;
            input.Next.lookDelta = new Vector2(.24f, 0f);
            yield return Frames(2);
            Assert.AreEqual(yaw + .24f, tour.Player.LookYaw, .001f, "the tour passes look through");
            Object.Destroy(tour.gameObject);
            yield return null;

            var still = new GameObject("Test tour, no look").AddComponent<HomeTourDirector>();
            still.Begin(house, new ScriptedBinding(), lens);
            yield return Settle();
            Assert.IsFalse(still.ThirdPerson);
            Assert.IsTrue(lens.orthographic, "a player who can't turn the view keeps the overhead tour");
        }
    }
}
