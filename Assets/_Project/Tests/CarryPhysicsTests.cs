using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class CarryPhysicsTests
    {
        const float Overlap = .03f;

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        const int WatchedFrames = 120;

        static PlayerController Player(int id, Vector3 at)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(id, new ScriptedBinding());
            p.Inventory.Collects = false;
            p.Respawn(at);
            p.FaceTowards(Vector3.forward);
            p.Body.useGravity = false;
            p.Body.constraints = RigidbodyConstraints.FreezeAll;
            return p;
        }

        static Rigidbody Box(Vector3 at, float size = .4f, float mass = 1f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Carry test box";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * size;
            var body = go.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = mass;
            body.constraints = RigidbodyConstraints.FreezeAll;
            return body;
        }

        static Collider Wall(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Carry test wall";
            go.transform.position = centre;
            go.transform.localScale = size;
            Physics.SyncTransforms();
            return go.GetComponent<Collider>();
        }

        static void Move(Rigidbody body, Vector3 at)
        {
            body.transform.position = at;
            body.position = at;
            Physics.SyncTransforms();
        }

        static bool Penetrates(Collider a, Collider b, out float depth)
        {
            depth = 0f;
            return Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out depth)
                && depth > Overlap;
        }

        static void CapsuleEnds(PlayerController p, out Vector3 a, out Vector3 b)
        {
            var capsule = p.GetComponent<CapsuleCollider>();
            var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 2 ? Vector3.forward : Vector3.up;
            var centre = p.transform.TransformPoint(capsule.center);
            var along = p.transform.TransformDirection(axis) * (capsule.height * .5f * Mathf.Abs(p.transform.lossyScale.y));
            a = centre + along;
            b = centre - along;
        }

        [UnityTest]
        public IEnumerator GrabPrefersTheThingStraightAheadAndMarksItBeforeThePress()
        {
            var player = Player(0, Vector3.zero);
            var ahead = Box(new Vector3(0f, .8f, 1.2f));
            var beside = Box(new Vector3(.6f, .8f, .6f));
            Physics.SyncTransforms();
            Assert.AreSame(ahead, player.Combat.GrabTarget(), "the box in front wins over a nearer one off to the side");
            var marker = player.GetComponent<CarryTargetMarker>();
            Assert.IsNotNull(marker);
            yield return TestScenes.WaitUntil(() => marker.Target == ahead && marker.Showing, 2f, "grab marker on the box in front");
            Assert.IsTrue(player.Combat.TryGrab());
            Assert.AreSame(ahead, player.Combat.Held);
            Assert.AreNotSame(beside, player.Combat.Held);
            yield return TestScenes.WaitUntil(() => !marker.Showing, 2f, "marker hides while hands are full");
        }

        [UnityTest]
        public IEnumerator TheGrabTargetStaysPutUntilARivalIsClearlyBetter()
        {
            var player = Player(0, Vector3.zero);
            var left = Box(new Vector3(-.35f, .8f, 1f));
            var right = Box(new Vector3(.35f, .8f, 1f));
            Physics.SyncTransforms();
            var first = player.Combat.GrabTarget();
            Assert.IsNotNull(first);
            var other = first == left ? right : left;
            Move(other, other.position + Vector3.back * .05f);
            Assert.AreSame(first, player.Combat.GrabTarget(), "a slightly nearer rival does not steal the target");
            Move(other, new Vector3(-Mathf.Sign(first.position.x) * .1f, .8f, .5f));
            Assert.AreSame(other, player.Combat.GrabTarget(), "a clearly nearer thing in front takes over");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ADroppedBoxNeverStartsInsideAnotherPlayer()
        {
            var carrier = Player(0, Vector3.zero);
            var box = Box(new Vector3(0f, .8f, .9f));
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            var at = box.transform.position;
            var bystander = Player(1, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
            carrier.Combat.Drop();
            Physics.SyncTransforms();
            var shape = box.GetComponent<Collider>();
            Assert.IsFalse(Penetrates(shape, bystander.GetComponent<Collider>(), out float depth), "box overlaps the other player by " + depth);
        }

        [UnityTest]
        public IEnumerator HeavyFurnitureIsSetDownInFrontInsteadOfDroppedOnTheCarrier()
        {
            var carrier = Player(0, Vector3.zero);
            var chair = Box(new Vector3(0f, .6f, 1f), .6f, 4f);
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            Assert.Greater(chair.GetComponent<Renderer>().bounds.min.y, 1f, "heavy things ride overhead");
            carrier.Combat.Drop();
            Physics.SyncTransforms();
            var bounds = chair.GetComponent<Collider>().bounds;
            Assert.Less(bounds.min.y, .2f, "set down near the floor");
            Assert.Greater(bounds.min.z, .3f, "set down in front of the carrier");
            Assert.IsFalse(Penetrates(chair.GetComponent<Collider>(), carrier.GetComponent<Collider>(), out float depth), "chair overlaps the carrier by " + depth);
            Assert.Less(chair.angularVelocity.magnitude, CarryRules.DropSpin + .01f, "a drop barely spins");
        }

        [UnityTest]
        public IEnumerator ACarriedPlayerStaysOutOfASideWall()
        {
            var carrier = Player(0, Vector3.zero);
            var victim = Player(1, new Vector3(0f, 0f, .9f));
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            Assert.AreSame(victim.Body, carrier.Combat.Held);
            yield return null;
            CapsuleEnds(victim, out var a, out var b);
            float side = Mathf.Abs(a.x) > Mathf.Abs(b.x) ? Mathf.Sign(a.x) : Mathf.Sign(b.x);
            const float face = .8f;
            Wall(new Vector3(side * (face + .1f), 2f, 0f), new Vector3(.2f, 4f, 6f));
            yield return null;
            yield return null;
            CapsuleEnds(victim, out a, out b);
            float reach = Mathf.Max(a.x * side, b.x * side);
            Assert.Less(reach, face + .05f, "the carried player's far end stays on the carrier side of the wall");
            Assert.IsTrue(victim.IsHeld);
        }

        [UnityTest]
        public IEnumerator ACarriedPlayerStaysOutOfASideWallOnEveryFrame()
        {
            var carrier = Player(0, Vector3.zero);
            var victim = Player(1, new Vector3(0f, 0f, .9f));
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            CapsuleEnds(victim, out var a, out var b);
            float side = Mathf.Abs(a.x) > Mathf.Abs(b.x) ? Mathf.Sign(a.x) : Mathf.Sign(b.x);
            const float face = .8f;
            Wall(new Vector3(side * (face + .1f), 2f, 0f), new Vector3(.2f, 4f, 6f));
            yield return null;
            yield return null;
            for (int frame = 0; frame < WatchedFrames; frame++)
            {
                CapsuleEnds(victim, out a, out b);
                Assert.Less(Mathf.Max(a.x * side, b.x * side), face + .05f, "frame " + frame + ": the carried player's far end is through the wall");
                yield return null;
            }
            Assert.IsTrue(victim.IsHeld);
        }

        [UnityTest]
        public IEnumerator ACarriedPlayerIsNotInterpolatedAndGetsItBackWhenPutDown()
        {
            var carrier = Player(0, Vector3.zero);
            var victim = Player(1, new Vector3(0f, 0f, .9f));
            Physics.SyncTransforms();
            var free = victim.Body.interpolation;
            Assert.AreNotEqual(RigidbodyInterpolation.None, free, "a free player is interpolated");
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            Assert.AreEqual(RigidbodyInterpolation.None, victim.Body.interpolation, "a carried player follows the hands without a physics step of lag");
            carrier.Combat.Drop();
            yield return null;
            Assert.IsFalse(victim.IsHeld);
            Assert.AreEqual(free, victim.Body.interpolation, "a player put down is interpolated again");
        }

        [UnityTest]
        public IEnumerator AReleasedPlayerLandsClearOfWallsCeilingsAndTheCarrier()
        {
            var carrier = Player(0, Vector3.zero);
            var victim = Player(1, new Vector3(0f, 0f, .9f));
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            var wall = Wall(new Vector3(0f, 1.5f, 1.1f), new Vector3(8f, 3f, .2f));
            var ceiling = Wall(new Vector3(0f, 2.6f, 0f), new Vector3(6f, .2f, 6f));
            yield return null;
            carrier.Combat.Drop();
            Physics.SyncTransforms();
            Assert.IsFalse(victim.IsHeld);
            Assert.IsFalse(carrier.Combat.IsHolding);
            var shape = victim.GetComponent<Collider>();
            Assert.IsFalse(Penetrates(shape, wall, out float wallDepth), "inside the wall by " + wallDepth);
            Assert.IsFalse(Penetrates(shape, ceiling, out float ceilingDepth), "inside the ceiling by " + ceilingDepth);
            Assert.Less(victim.transform.position.y, .2f, "a dropped player is put down on the floor");
            Assert.Less(victim.transform.position.z, 1f, "never through the wall into the next room");
            Assert.IsFalse(Penetrates(shape, carrier.GetComponent<Collider>(), out float carrierDepth), "inside the carrier by " + carrierDepth);
        }

        [UnityTest]
        public IEnumerator RespawningTheCarriedPlayerFreesTheCarriersHands()
        {
            var carrier = Player(0, Vector3.zero);
            var victim = Player(1, new Vector3(0f, 0f, .9f));
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            victim.Respawn(new Vector3(5f, 0f, 5f));
            yield return TestScenes.WaitUntil(() => !carrier.Combat.IsHolding, 1f, "carrier lets go of a respawned player");
            Assert.That(Vector3.Distance(victim.transform.position, new Vector3(5f, 0f, 5f)), Is.LessThan(.5f));
        }

        [UnityTest]
        public IEnumerator AThrownBoxFliesWithContinuousCollisionAndThenRestoresItsMode()
        {
            var carrier = Player(0, Vector3.zero);
            var box = Box(new Vector3(0f, .8f, .9f), .2f);
            box.constraints = RigidbodyConstraints.None;
            box.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Physics.SyncTransforms();
            Assert.IsTrue(carrier.Combat.TryGrab());
            yield return null;
            carrier.Combat.Throw();
            Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, box.collisionDetectionMode);
            Assert.LessOrEqual(box.linearVelocity.magnitude, CarryRules.MaxThrowSpeed + .01f);
            yield return TestScenes.WaitUntil(() => box.collisionDetectionMode == CollisionDetectionMode.Discrete, CarryRules.FlightSeconds + 2f, "flight mode restored");
        }

        [UnityTest]
        public IEnumerator PlacedGearIsNeverPutThroughAWall()
        {
            var player = Player(0, Vector3.zero);
            var stool = CatalogGear.Create(GameConfig.Current.Items.Get("STOOL"));
            Assert.IsTrue(player.Combat.TryEquip(stool));
            yield return null;
            var wall = Wall(new Vector3(0f, 1.5f, 1f), new Vector3(8f, 3f, .2f));
            Assert.IsTrue(player.Combat.DeployHeld());
            yield return TestScenes.WaitUntil(() => !player.Combat.IsDeploying, 3f, "STOOL placement channel");
            Assert.IsFalse(player.Combat.Weapon, "the STOOL left the hand");
            Physics.SyncTransforms();
            var shape = stool.GetComponent<Collider>();
            Assert.Less(shape.bounds.max.z, .9f + Overlap, "placed on the player's side of the wall");
            Assert.IsFalse(Penetrates(shape, wall, out float depth), "inside the wall by " + depth);
        }
    }
}
