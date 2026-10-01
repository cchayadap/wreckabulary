using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>Reach queries must respect actual generated walls while keeping doorways usable.</summary>
    public class WallInteractionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            Match.ModeOverride = "Dibs";
            Session.SelectMap("pinwheel");
            var room = new GameObject("Wall test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerController Player(int id, Vector3 at)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(id, new ScriptedBinding());
            var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
            p.Health.UseRules(rules);
            p.Inventory.Collects = false;
            Place(p, at);
            return p;
        }

        static void Place(PlayerController p, Vector3 at)
        {
            p.Respawn(at);
            p.FaceTowards(Vector3.forward);
            p.Body.useGravity = false;
            p.Body.constraints = RigidbodyConstraints.FreezeAll;
        }

        static Smashable Prop(Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(World.Transient, false);
            go.transform.position = at;
            go.transform.localScale = Vector3.one * .6f;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 2f; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;
            var prop = go.AddComponent<Smashable>(); prop.Init("BOX", 100f);
            return prop;
        }

        [UnityTest]
        public IEnumerator MeleeCannotHitTheNextRoomThroughAWallButWorksThroughItsDoor()
        {
            var attacker = Player(0, new Vector3(2f, 0f, 3.5f));
            var victim = Player(1, new Vector3(2f, 0f, 4.5f));
            Physics.SyncTransforms();
            Assert.AreEqual(0, attacker.Combat.Strike(attacker.Health.Rules.Unarmed, null));
            Assert.AreEqual(100f, victim.Health.Current, "the z=4 wall blocks a target within melee reach");

            Place(attacker, new Vector3(0f, 0f, 3.5f));
            Place(victim, new Vector3(0f, 0f, 4.5f));
            Physics.SyncTransforms();
            Assert.AreEqual(1, attacker.Combat.Strike(attacker.Health.Rules.Unarmed, null));
            Assert.AreEqual(100f - attacker.Health.Rules.Unarmed.Damage, victim.Health.Current);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MeleeCannotBreakFurnitureThroughAWallButWorksThroughItsDoor()
        {
            var attacker = Player(0, new Vector3(2f, 0f, 3.5f));
            var prop = Prop(new Vector3(2f, .5f, 4.5f));
            Physics.SyncTransforms();
            attacker.Combat.Strike(attacker.Health.Rules.Unarmed, null);
            Assert.AreEqual(100f, prop.Health);

            Place(attacker, new Vector3(0f, 0f, 3.5f));
            prop.GetComponent<Rigidbody>().position = new Vector3(0f, .5f, 4.5f);
            Physics.SyncTransforms();
            attacker.Combat.Strike(attacker.Health.Rules.Unarmed, null);
            Assert.AreEqual(100f - attacker.Health.Rules.Unarmed.BreakPower * Smashable.HealthPerBreakPower, prop.Health);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GrabStartsAtThePlayersChestAndCannotReachAcrossAWall()
        {
            var player = Player(0, new Vector3(2f, 0f, 3.5f));
            var prop = Prop(new Vector3(2f, .5f, 4.5f));
            Physics.SyncTransforms();
            Assert.IsFalse(player.Combat.TryGrab(), "the forward grab sphere overlaps the far room, but the wall still blocks it");
            Assert.IsFalse(player.Combat.IsHolding);

            Place(player, new Vector3(0f, 0f, 3.5f));
            prop.GetComponent<Rigidbody>().position = new Vector3(0f, .5f, 4.5f);
            Physics.SyncTransforms();
            Assert.IsTrue(player.Combat.TryGrab());
            Assert.AreSame(prop.GetComponent<Rigidbody>(), player.Combat.Held);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TargetColliderOwnVisualAndLooseTilesDoNotBlockAnOpenDoorInteraction()
        {
            var attacker = Player(0, new Vector3(0f, 0f, 3.5f));
            var victim = Player(1, new Vector3(0f, 0f, 4.5f));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Held visual collider";
            visual.transform.SetParent(attacker.transform, false);
            visual.transform.localPosition = new Vector3(0f, .8f, .25f);
            visual.transform.localScale = Vector3.one * .2f;
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.position = new Vector3(0f, .8f, 4f);
            tile.transform.localScale = Vector3.one * .2f;
            tile.layer = World.TileLayer;
            tile.AddComponent<LetterTile>().Body.isKinematic = true;
            Physics.SyncTransforms();
            Assert.AreEqual(1, attacker.Combat.Strike(attacker.Health.Rules.Unarmed, null));
            Assert.IsTrue(attacker.Combat.TryGrab());
            Assert.AreSame(victim.Body, attacker.Combat.Held);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReviveNeedsAnOpenPathAndCancelsIfAWallSeparatesThePartners()
        {
            var reviver = Player(0, new Vector3(2f, 0f, 3.5f));
            var teammate = Player(1, new Vector3(2f, 0f, 4.5f));
            reviver.Team = teammate.Team = 0;
            var rules = Match.Rules.Clone(); rules.DownedEnabled = true; rules.SpawnProtectionSeconds = 0f;
            reviver.Health.UseRules(rules); teammate.Health.UseRules(rules);
            teammate.Health.ApplyDamage(HitInfo.Hazard(1000f));
            Physics.SyncTransforms();
            Assert.IsTrue(teammate.IsDowned);
            Assert.IsFalse(reviver.Combat.TryRevive(), "a close teammate on the far side of the wall is unreachable");

            Place(reviver, new Vector3(0f, 0f, 3.5f));
            Place(teammate, new Vector3(0f, 0f, 4.5f));
            Physics.SyncTransforms();
            Assert.IsTrue(reviver.Combat.TryRevive());
            ((ScriptedBinding)reviver.Binding).Next.grabHeld = true;
            yield return null;
            Assert.IsTrue(reviver.Combat.IsReviving);

            Place(reviver, new Vector3(2f, 0f, 3.5f));
            Place(teammate, new Vector3(2f, 0f, 4.5f));
            Physics.SyncTransforms();
            yield return null;
            Assert.IsFalse(reviver.Combat.IsReviving);
            Assert.AreEqual(0f, teammate.Health.ReviveProgress);
            Assert.IsTrue(teammate.IsDowned);
        }
    }
}
