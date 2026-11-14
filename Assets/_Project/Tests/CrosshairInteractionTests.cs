using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class CrosshairInteractionTests
    {
        PlayerController player;
        Camera camera;
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Body.useGravity = false;
            player.Body.constraints = RigidbodyConstraints.FreezeAll;
            player.Inventory.Collects = false;
            player.ShooterView = true;
            camera = new GameObject("Interaction camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
        }
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        Rigidbody Prop(Vector3 at, float size = .4f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = at; go.transform.localScale = Vector3.one * size;
            var body = go.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = 1;
            return body;
        }
        void Aim(Vector3 at)
        {
            camera.transform.position = new Vector3(0, 1.1f, -3);
            camera.transform.LookAt(at);
            Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator EUsesCrosshairInsteadOfNearestForwardBodyAndRejectsFarOrOccludedTargets()
        {
            var selected = Prop(new Vector3(.65f, 1, 1));
            Prop(new Vector3(0, .6f, .6f), .25f);
            Aim(selected.position);
            Assert.AreSame(selected, player.Combat.GrabTarget());
            ((ScriptedBinding)player.Binding).Next.grab = true;
            yield return null;
            Assert.AreSame(selected, player.Combat.Held, "The E input consumes the same target shown by the reticle.");
            player.Combat.Drop(); selected.position = selected.transform.position = new Vector3(.65f, 1, 8);
            Aim(selected.position);
            Assert.IsNull(player.Combat.GrabTarget(), "Camera sight is not physical reach.");
            selected.position = selected.transform.position = new Vector3(.65f, 1, 1);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(.4f, 1, .55f); wall.transform.localScale = new Vector3(2, 3, .1f);
            Aim(selected.position);
            Assert.IsNull(player.Combat.GrabTarget(), "The first solid hit blocks E.");
        }

        [UnityTest] public IEnumerator PointerAndCrosshairUseTheSameTargetRay()
        {
            var selected = Prop(new Vector3(.65f, 1, 1)); Aim(selected.position);
            Assert.AreSame(selected, player.Combat.GrabTarget());
            player.ShooterView = false;
            player.Commands.aimAtPointer = true;
            player.Commands.pointer = camera.WorldToScreenPoint(selected.position);
            Assert.AreSame(selected, player.Combat.GrabTarget());
            yield return null;
        }

        [UnityTest] public IEnumerator ThrowLeavesAnimatedHandAndArcsTowardTheAimedHeight()
        {
            var ball = CatalogGear.Create(GameConfig.Current.Items.Get("BALL"));
            var body = ball.GetComponent<Rigidbody>(); body.interpolation = RigidbodyInterpolation.Interpolate;
            Assert.IsTrue(player.Combat.TryEquip(ball));
            yield return null;
            player.handR.position += new Vector3(.15f, .1f, .1f);
            var target = Prop(new Vector3(1, 2.3f, 6)); target.isKinematic = true;
            Aim(target.position);
            Assert.IsTrue(InteractionAim.TryRay(player, out var ray));
            Assert.IsTrue(InteractionAim.Trace(player, ray, out var hit));
            var origin = body.transform.position;
            var centre = body.transform.TransformPoint(body.centerOfMass);
            player.Combat.Throw();
            Assert.That(Vector3.Distance(origin, body.position), Is.LessThan(.01f), "Release never teleports to the player's feet/facing offset.");
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
            var velocity = body.linearVelocity;
            Assert.Greater(velocity.y, 0);
            float flight = World.Flat(hit.point - centre).magnitude / World.Flat(velocity).magnitude;
            var arrival = centre + velocity * flight + (body.useGravity ? Physics.gravity * (.5f * flight * flight) : Vector3.zero);
            Assert.That(Vector3.Distance(arrival, hit.point), Is.LessThan(.01f), "Hand trajectory converges on the reticle hit, including gravity.");
        }

        [UnityTest] public IEnumerator CarryStopsBeforeWallInsteadOfFollowingHandThroughIt()
        {
            var body = Prop(new Vector3(.65f, 1, 1));
            Aim(body.position); Assert.IsTrue(player.Combat.TryGrab());
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1, .8f); wall.transform.localScale = new Vector3(4, 3, .1f);
            player.holdPoint.position = new Vector3(0, 1, 1.3f);
            Physics.SyncTransforms();
            yield return null;
            Assert.Less(body.transform.position.z + .2f, .75f, "A carried box stays on the player side of the wall.");
            player.Combat.Throw();
            Assert.Less(body.position.z + .2f, .75f, "Releasing cannot teleport the object through the obstruction.");
        }

        [UnityTest] public IEnumerator LargeCarryStillStopsWhenSweepOriginIsAlreadyNearWall()
        {
            var body = Prop(new Vector3(.65f, 1, 1), 1.2f);
            Aim(body.position); Assert.IsTrue(player.Combat.TryGrab());
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1, .5f); wall.transform.localScale = new Vector3(4, 3, .1f);
            player.holdPoint.position = new Vector3(0, 1, 1.3f);
            Physics.SyncTransforms(); yield return null;
            Assert.Less(body.transform.position.z + .6f, .45f, "A wide prop remains on the carrier side even when the initial sweep sphere overlaps the wall.");
        }

        [UnityTest] public IEnumerator DroppedRecipeExpandsWithoutEmbeddingInTheFloorOrWall()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.1f, 0); floor.transform.localScale = new Vector3(8, .2f, 8);
            var sofa = CatalogGear.Create(GameConfig.Current.Items.Get("SOFA"));
            Assert.IsTrue(player.Combat.TryEquip(sofa));
            yield return null;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 1.5f, .9f); wall.transform.localScale = new Vector3(8, 3, .1f);
            Physics.SyncTransforms();
            player.Combat.Drop();
            Physics.SyncTransforms();
            var shape = sofa.GetComponent<BoxCollider>();
            foreach (var obstacle in new[] { wall.GetComponent<Collider>(), floor.GetComponent<Collider>() })
                Assert.IsFalse(Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out _), "Full-size SOFA must start clear of " + obstacle.name);
            Assert.AreEqual(Vector3.one, sofa.transform.localScale);
        }

        [UnityTest] public IEnumerator FullHandsDoNotAdvertiseUnusablePickup()
        {
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("BAT"))));
            Assert.IsTrue(player.Combat.TryEquip(CatalogGear.Create(GameConfig.Current.Items.Get("BALL"))));
            var body = Prop(new Vector3(.65f, 1, 1)); Aim(body.position);
            Assert.IsNull(player.Combat.GrabTarget());
            Assert.IsFalse(player.Combat.TryGrab());
            yield return null;
        }

        [UnityTest] public IEnumerator DropRestoresOriginalColliderStatesAndCollisionPairsAfterSeparation()
        {
            var body = Prop(new Vector3(.65f, 1, 1));
            var disabled = body.gameObject.AddComponent<SphereCollider>(); disabled.enabled = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Aim(body.position); Assert.IsTrue(player.Combat.TryGrab());
            player.Combat.Drop();
            Assert.IsFalse(disabled.enabled);
            Assert.IsTrue(body.GetComponent<BoxCollider>().enabled);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
            var own = player.GetComponent<Collider>(); var item = body.GetComponent<BoxCollider>();
            Assert.IsTrue(Physics.GetIgnoreCollision(own, item));
            body.position = body.transform.position = new Vector3(10, 2, 10);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.3f);
            Assert.IsFalse(Physics.GetIgnoreCollision(own, item));
            Assert.IsNull(body.GetComponent<ReleasedBodyCollision>());
        }
    }
}
