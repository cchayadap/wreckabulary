using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class CameraRigTests
    {
        Camera camera;
        CameraRig rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            camera = new GameObject("Third person test camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            rig = camera.gameObject.AddComponent<CameraRig>();
            rig.FrameLayout(GameConfig.Current.HouseFor("pinwheel"));
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        sealed class CameraInputBinding : InputBinding
        {
            public PlayerCommands Next;
            public override string Id => "camera-test-human";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands)
            {
                commands = Next;
                Next.lookDelta = Vector2.zero;
            }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static void Seat(PlayerController player, Vector3 at)
        {
            player.Respawn(at);
            player.Body.useGravity = false;
            player.Body.constraints = RigidbodyConstraints.FreezeAll;
        }

        static PlayerController Spawn(int index, InputBinding binding, Vector3 facing)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(index, binding);
            Seat(player, Vector3.zero);
            player.FaceTowards(facing);
            return player;
        }

        [UnityTest]
        public IEnumerator OneLocalPlayerIsCenteredBehindInPerspectiveEvenWithBots()
        {
            var player = Spawn(0, new CameraInputBinding(), Vector3.right);
            Spawn(1, new BotBinding(), Vector3.forward);
            yield return null;
            yield return null;

            Assert.IsFalse(camera.orthographic, "The single-player view must not regress to the overhead map.");
            Assert.AreSame(player, rig.FollowedPlayer);
            var behind = World.Flat(camera.transform.position - player.transform.position).normalized;
            Assert.That(Vector3.Dot(behind, player.Facing), Is.LessThan(-0.99f));
            Assert.That(camera.transform.eulerAngles.x, Is.InRange(6f, 25f));
            var onScreen = camera.WorldToViewportPoint(player.transform.position + Vector3.up * 0.9f);
            Assert.That(onScreen.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(onScreen.y, Is.InRange(0.25f, 0.65f));
            var forward = rig.ScreenDirectionToWorld(Vector2.up);
            Assert.That(Vector2.Distance(forward, Vector2.right), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator SecondCouchSeatSharesTheHouseAndLeavingRestoresFollow()
        {
            Spawn(0, new CameraInputBinding(), Vector3.forward);
            yield return null;
            yield return null;
            Assert.IsFalse(camera.orthographic);
            var second = Spawn(1, new CameraInputBinding(), Vector3.forward);
            yield return null;
            yield return null;
            Assert.IsTrue(camera.orthographic, "Both couch players need the shared house view.");
            Assert.IsFalse(rig.IsThirdPerson);
            Object.Destroy(second.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(camera.orthographic);
        }

        void InView(PlayerController player)
        {
            var at = camera.WorldToViewportPoint(player.transform.position + Vector3.up * .9f);
            Assert.That(at.x, Is.InRange(0f, 1f), player.name + " across");
            Assert.That(at.y, Is.InRange(0f, 1f), player.name + " along");
        }

        [UnityTest]
        public IEnumerator CouchViewFramesTheSeatsAndAtMostTheWholeHouse()
        {
            var house = GameConfig.Current.HouseFor("pinwheel");
            var area = new PartyArea(house.Rooms.Min(r => r.MinX), house.Rooms.Max(r => r.MaxX),
                house.Rooms.Min(r => r.MinZ), house.Rooms.Max(r => r.MaxZ));
            float widest = PartyCamera.HouseSize(area.Width, area.Depth, camera.aspect);
            float closest = Mathf.Min(PartyCamera.MinSize, widest);
            var overhead = Quaternion.LookRotation(new Vector3(0f, -PartyCamera.Rise, PartyCamera.Back));
            var a = Spawn(0, new ScriptedBinding(), Vector3.forward);
            var b = Spawn(1, new ScriptedBinding(), Vector3.forward);
            Seat(a, new Vector3(area.CentreX - 1f, 0f, area.CentreZ));
            Seat(b, new Vector3(area.CentreX + 1f, 0f, area.CentreZ));
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(camera.orthographic, "Two couch seats share the overhead view.");
            Assert.IsFalse(rig.IsThirdPerson);
            Assert.AreEqual(closest, camera.orthographicSize, .2f, "Seats side by side get the closest framing, not the whole house.");
            Assert.That(Quaternion.Angle(overhead, camera.transform.rotation), Is.LessThan(.5f), "The overhead angle the storey cutaway relies on never changes.");
            InView(a);
            InView(b);

            Seat(a, new Vector3(area.MinX + 1.5f, 0f, area.MinZ + 1.5f));
            Seat(b, new Vector3(area.MaxX - 1.5f, 0f, area.MaxZ - 1.5f));
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(widest, camera.orthographicSize, .2f, "Seats in opposite corners see the whole house and no more.");
            var focus = camera.transform.position - new Vector3(0f, PartyCamera.Rise, -PartyCamera.Back);
            Assert.AreEqual(area.CentreX, focus.x, .3f, "The whole house is centred as before.");
            Assert.AreEqual(area.CentreZ, focus.z, .3f);
            InView(a);
            InView(b);

            Object.Destroy(b.gameObject);
            Seat(a, new Vector3(area.MaxX - 1f, 0f, area.MaxZ - 1f));
            yield return new WaitForSeconds(2.5f);
            Assert.IsFalse(camera.orthographic, "A lone seat that can't turn the view is followed up close.");
            Assert.That(Quaternion.Angle(overhead, camera.transform.rotation), Is.LessThan(.5f));
            InView(a);
        }

        [UnityTest]
        public IEnumerator VisibleBlockerPullsCameraInButCutawayColliderDoesNot()
        {
            Spawn(0, new CameraInputBinding(), Vector3.forward);
            yield return null;
            yield return null;
            float clearDistance = Vector3.Distance(camera.transform.position, Vector3.up);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.8f, -2f);
            wall.transform.localScale = new Vector3(6f, 4f, 0.3f);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            float blockedDistance = Vector3.Distance(camera.transform.position, Vector3.up);
            Assert.That(blockedDistance, Is.LessThan(clearDistance - 1f));
            Assert.That(camera.transform.position.z, Is.GreaterThan(-1.85f), "The camera stays in front of the wall.");

            wall.transform.position = new Vector3(0f, 0.55f, -2f);
            wall.transform.localScale = new Vector3(6f, 1.1f, 0.3f);
            var collider = wall.GetComponent<BoxCollider>();
            collider.size = new Vector3(1f, 3f, 1f);
            collider.center = new Vector3(0f, 1f, 0f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.8f);
            Assert.That(Vector3.Distance(camera.transform.position, Vector3.up),
                Is.EqualTo(clearDistance).Within(0.05f), "Invisible gameplay collider height must not collapse the camera boom.");
            Assert.IsTrue(collider.enabled, "Temporarily hiding a near wall must preserve its gameplay collision.");
            var wallRenderer = wall.GetComponent<Renderer>();
            Assert.IsTrue(camera.GetComponent<CameraCutaway>().WouldHide(wallRenderer), "A cutaway in front of the avatar must not hide the player's body.");
            Assert.AreEqual(ShadowCastingMode.On, wallRenderer.shadowCastingMode, "The authored renderer is unchanged outside this camera's render.");
            using (CameraCutaway.BeginCameraVisibility(camera))
                Assert.AreEqual(ShadowCastingMode.ShadowsOnly, wallRenderer.shadowCastingMode);
            Spawn(1, new CameraInputBinding(), Vector3.forward);
            yield return null;
            yield return null;
            Assert.AreEqual(ShadowCastingMode.On, wall.GetComponent<Renderer>().shadowCastingMode, "Shared couch view restores cutaway walls.");
        }

        [UnityTest]
        public IEnumerator PausedCameraIgnoresOrbitUntilResume()
        {
            var input = new CameraInputBinding();
            Spawn(0, input, Vector3.forward);
            yield return null;
            yield return null;
            var before = camera.transform.rotation;
            Time.timeScale = 0f;
            input.Next.lookDelta = new Vector2(.5f, 0f);
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(before, camera.transform.rotation), Is.LessThan(0.01f));
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Assert.That(Quaternion.Angle(before, camera.transform.rotation), Is.GreaterThan(10f));
        }

        [UnityTest]
        public IEnumerator BillboardTracksAssignedCameraAndStructuralOffsetWithoutMainTag()
        {
            camera.tag = "Untagged";
            var anchor = new GameObject("Billboard anchor").transform;
            anchor.position = new Vector3(2f, 3f, 4f);
            var label = new GameObject("World-space TMP-compatible label").AddComponent<WorldSpaceBillboard>();
            label.SetCamera(camera);
            label.FollowTarget = anchor;
            label.StructuralOffset = new Vector3(0.2f, 1.8f, -0.1f);
            camera.transform.rotation = Quaternion.Euler(12f, 76f, 0f);
            rig.enabled = false;
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(label.transform.position, anchor.position + label.StructuralOffset), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(label.transform.rotation, camera.transform.rotation), Is.LessThan(0.001f));
        }
    }
}
