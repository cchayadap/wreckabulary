using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ControlsTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController SpawnPlayer(int index, Vector3 at, out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(index, input);
            p.Respawn(at);
            return p;
        }

        static IEnumerator Updates(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static HitInfo Punch(Vector3 direction) => Hits.Melee(null, direction, Match.Rules.Unarmed, null);

        [UnityTest]
        public IEnumerator JumpRisesAboutTheRulesHeight()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(5);
            Assert.IsTrue(p.Grounded);
            int jumps = 0;
            p.Jumped += _ => jumps++;

            input.Next.jump = true;
            float top = 0f;
            for (float t = 0f; t < 1.2f; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                top = Mathf.Max(top, p.transform.position.y);
                if (Mathf.Abs(t - 0.3f) < 0.01f) input.Next.jump = true;
            }

            Assert.AreEqual(1, jumps);
            float height = p.Health.Rules.JumpHeight;
            Assert.That(top, Is.InRange(height * 0.8f, height * 1.3f), "jump height");
            Assert.IsTrue(p.Grounded, "landed again");
        }

        [UnityTest]
        public IEnumerator DownedPlayersCantJumpOrDodge()
        {
            Match.ModeOverride = "Duos";
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(5);
            var rules = Match.Rules.Clone();
            p.Health.UseRules(rules);
            p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 1000f, 0f));
            Assert.IsTrue(p.IsDowned);
            yield return Frames(5);

            input.Next.jump = true;
            input.Next.dodge = true;
            yield return Frames(10);
            Assert.Less(p.transform.position.y, 0.2f);
            Assert.IsFalse(p.IsDodging);
        }

        [UnityTest]
        public IEnumerator SpeedBuildsAndBleedsAtTheRulesRates()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            var rules = Match.Rules.Clone();
            rules.GroundAccel = 4f;
            rules.GroundFriction = 2f;
            p.Health.UseRules(rules);
            yield return Frames(5);

            input.Next.move = Vector2.up;
            yield return new WaitForSeconds(.5f);
            float built = p.Body.linearVelocity.z;
            Assert.AreEqual(2f, built, .4f, "groundAccel: 4 m/s² for half a second");

            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(built - 1f, p.Body.linearVelocity.z, .4f, "groundFriction: 2 m/s² once you let go");
        }

        [UnityTest]
        public IEnumerator DodgeDashesOutOfTroubleThenCoolsDown()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(5);
            p.FaceTowards(Vector3.forward);
            int dodges = 0;
            p.Dodged += _ => dodges++;

            input.Next.dodge = true;
            yield return Frames(2);
            Assert.IsTrue(p.IsDodging);
            Assert.IsFalse(p.Health.ApplyDamage(Punch(Vector3.forward)), "the start of a dodge can't be hit");
            Assert.AreEqual(100f, p.Health.Current);

            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(p.IsDodging);
            var rules = p.Health.Rules;
            Assert.That(p.transform.position.z, Is.InRange(rules.DodgeDistance * 0.75f, rules.DodgeDistance * 1.5f), "dash distance");
            Assert.Less(Mathf.Abs(p.transform.position.x), 0.2f);
            Assert.Less(World.Flat(p.Body.linearVelocity).magnitude, 1f, "comes out of the dash under control");

            input.Next.dodge = true;
            yield return Frames(3);
            Assert.IsFalse(p.IsDodging);
            Assert.AreEqual(1, dodges);
            Assert.IsTrue(p.Health.ApplyDamage(Punch(Vector3.forward)), "outside the dodge, hits land");
        }

        [UnityTest]
        public IEnumerator DodgeFollowsTheStick()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(5);
            p.FaceTowards(Vector3.forward);

            input.Next.move = Vector2.right;
            input.Next.dodge = true;
            yield return Frames(3);
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(p.transform.position.x, 3f, "dashed right, the way the stick points");
        }

        static void EquipPlate(PlayerController p)
        {
            p.Inventory.Set("PLATE");
            Assert.IsTrue(p.Summoner.Summon("PLATE"));
            Assert.AreEqual("PLATE", p.Combat.Weapon.word);
            Assert.IsNotNull(p.Combat.Weapon.Shield, "PLATE is a shield in items.json");
            p.FaceTowards(Vector3.forward);
        }

        [UnityTest]
        public IEnumerator RaisedPlateBlocksTheFrontAndWearsDown()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            var opponent = SpawnPlayer(1, new Vector3(0f, 0f, 1f), out _);
            yield return Frames(3);
            EquipPlate(p);
            yield return Frames(2);
            Assert.IsFalse(p.Combat.IsBlocking, "not until block is held");

            input.Next.blockHeld = true;
            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(p.Combat.IsBlocking);

            Assert.IsFalse(p.Health.ApplyDamage(Punch(Vector3.back)), "blocked");
            Assert.AreEqual(100f, p.Health.Current);
            var plate = p.Combat.Weapon;
            Assert.AreEqual(60f - 8f, plate.DurabilityLeft, "blocked damage wears the plate");

            input.Next.attack = true;
            yield return null;
            yield return null;
            Assert.AreEqual(100f, opponent.Health.Current);

            Assert.IsTrue(p.Health.ApplyDamage(Punch(Vector3.forward)));
            Assert.AreEqual(92f, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator HoldingLeftClickWithAPlateRaisesIt()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            var opponent = SpawnPlayer(1, new Vector3(0f, 0f, 1.1f), out _);
            yield return Frames(3);
            EquipPlate(p);
            yield return Frames(2);

            input.Next.attack = true;
            input.Next.attackHeld = true;
            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(p.Combat.IsBlocking, "left click is the plate's job: block");
            Assert.AreEqual(100f, opponent.Health.Current, "and it doesn't punch");

            input.Next.attackHeld = false;
            yield return Frames(2);
            Assert.IsFalse(p.Combat.IsBlocking, "letting go lowers it");
        }

        [UnityTest]
        public IEnumerator PlateInHandStillLetsYouPunch()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            var opponent = SpawnPlayer(1, new Vector3(0f, 0f, 1.1f), out _);
            yield return Frames(3);
            EquipPlate(p);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => opponent.Health.Current < 100f, 1f, "free hand punch damage window");
            Assert.AreEqual(92f, opponent.Health.Current, "the free hand punches");
            Assert.AreEqual("PLATE", p.Combat.Weapon.word, "and the plate stays in hand");
        }

        [UnityTest]
        public IEnumerator PlateWornThroughBreaksIntoLetters()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(3);
            EquipPlate(p);
            input.Next.blockHeld = true;
            yield return new WaitForSeconds(0.25f);
            Assert.IsTrue(p.Combat.IsBlocking);

            Assert.IsFalse(p.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Melee, 70f, 0f)), "blocked");
            Assert.AreEqual(100f, p.Health.Current);
            Assert.IsNull(p.Combat.Weapon, "the plate cracked apart");
            Assert.IsFalse(p.Combat.IsBlocking);
            Assert.AreEqual(5, TilePool.Instance.Active.Count, "P, L, A, T and E");

            Assert.IsTrue(p.Health.ApplyDamage(Punch(Vector3.back)), "no plate, no block");
        }

        [UnityTest]
        public IEnumerator BlockWithoutAPlateDoesNothing()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(5);
            p.FaceTowards(Vector3.forward);
            input.Next.blockHeld = true;
            yield return new WaitForSeconds(0.25f);

            Assert.IsFalse(p.Combat.IsBlocking);
            Assert.IsTrue(p.Health.ApplyDamage(Punch(Vector3.back)));
            Assert.AreEqual(92f, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator RaisedPlateSlowsWalking()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(3);
            EquipPlate(p);
            input.Next.move = Vector2.right;
            yield return new WaitForSeconds(0.5f);
            float free = World.Flat(p.Body.linearVelocity).magnitude;

            input.Next.blockHeld = true;
            yield return new WaitForSeconds(0.5f);
            float raised = World.Flat(p.Body.linearVelocity).magnitude;
            Assert.AreEqual(free * p.Combat.Weapon.Shield.MoveSpeedMultiplier, raised, 0.3f);
        }

        [UnityTest]
        public IEnumerator DropLetsGoOfTheHeldItem()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(3);
            EquipPlate(p);
            input.Next.drop = true;
            yield return null;
            yield return null;
            Assert.IsFalse(p.Combat.IsHolding);
        }

        [Test]
        public void DropNeedsAShortHold()
        {
            var hold = new HoldToFire();
            Assert.IsFalse(hold.Update(true, 0f));
            Assert.IsFalse(hold.Update(true, 0.1f), "a tap doesn't drop");
            Assert.IsFalse(hold.Update(false, 0.15f));
            Assert.IsFalse(hold.Update(true, 0.2f));
            Assert.IsFalse(hold.Update(true, 0.4f));
            Assert.IsTrue(hold.Update(true, 0.21f + InputBinding.DropHoldSeconds), "held long enough");
            Assert.IsFalse(hold.Update(true, 1f), "once per press");
        }

        static IEnumerator SpawnDownedPair(float reviveSeconds, System.Action<PlayerController, PlayerController, ScriptedBinding> ready)
        {
            Match.ModeOverride = "Duos";
            var a = SpawnPlayer(0, Vector3.zero, out var input);
            var b = SpawnPlayer(1, new Vector3(1f, 0f, 0f), out _);
            yield return Frames(3);
            a.Team = b.Team = 0;
            var rules = Match.Rules.Clone();
            rules.ReviveSeconds = reviveSeconds;
            a.Health.UseRules(rules);
            b.Health.UseRules(rules);
            b.Health.ApplyDamage(Hits.Of(null, Vector3.right, HitSource.Melee, 1000f, 0f));
            Assert.IsTrue(b.IsDowned);
            yield return Frames(3);
            ready(a, b, input);
        }

        [UnityTest]
        public IEnumerator HoldingGrabRevivesADownedTeammate()
        {
            PlayerController a = null, b = null;
            ScriptedBinding input = null;
            yield return SpawnDownedPair(0.5f, (x, y, i) => (a, b, input) = (x, y, i));
            Assert.AreSame(b, a.Combat.DownedTeammateNearby());

            input.Next.grab = true;
            input.Next.grabHeld = true;
            input.Next.move = Vector2.left;
            yield return null;
            Assert.IsTrue(a.Combat.IsReviving);
            var start = a.transform.position;

            yield return new WaitForSeconds(0.25f);
            Assert.That(b.Health.ReviveProgress, Is.InRange(0.2f, 0.9f));
            Assert.IsTrue(b.IsDowned);
            Assert.Less(World.Flat(a.transform.position - start).magnitude, 0.1f, "rooted while reviving");

            yield return new WaitForSeconds(0.45f);
            Assert.IsTrue(b.Health.IsAlive, "back up");
            Assert.AreEqual(b.Health.Rules.ReviveHealth, b.Health.Current);
            Assert.IsFalse(a.Combat.IsReviving);
            Assert.IsFalse(a.Combat.IsHolding, "reviving doesn't pick the teammate up");
        }

        [UnityTest]
        public IEnumerator LettingGoCancelsTheRevive()
        {
            PlayerController a = null, b = null;
            ScriptedBinding input = null;
            yield return SpawnDownedPair(0.4f, (x, y, i) => (a, b, input) = (x, y, i));

            input.Next.grab = true;
            input.Next.grabHeld = true;
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(a.Combat.IsReviving);

            input.Next.grabHeld = false;
            yield return null;
            yield return null;
            Assert.IsFalse(a.Combat.IsReviving);
            Assert.AreEqual(0f, b.Health.ReviveProgress);

            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(b.IsDowned, "still down: the revive has to be held to the end");
        }

        [UnityTest]
        public IEnumerator OpponentsCantRevive()
        {
            Match.ModeOverride = "Duos";
            var a = SpawnPlayer(0, Vector3.zero, out var input);
            var b = SpawnPlayer(1, new Vector3(1f, 0f, 0f), out _);
            yield return Frames(3);
            a.Team = 0;
            b.Team = 1;
            var rules = Match.Rules.Clone();
            a.Health.UseRules(rules);
            b.Health.UseRules(rules);
            b.Health.ApplyDamage(Hits.Of(null, Vector3.right, HitSource.Melee, 1000f, 0f));
            Assert.IsTrue(b.IsDowned);

            Assert.IsNull(a.Combat.DownedTeammateNearby());
            input.Next.grab = true;
            input.Next.grabHeld = true;
            yield return null;
            Assert.IsFalse(a.Combat.IsReviving);
        }

        [UnityTest]
        public IEnumerator MouseAndStickAimTurnThePlayer()
        {
            var cam = Camera.main;
            if (!cam)
            {
                cam = new GameObject("Test Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            var target = new RenderTexture(640, 360, 16);
            cam.targetTexture = target;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 10f, -7f), Quaternion.Euler(55f, 0f, 0f));

            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);

            input.Next.aimAtPointer = true;
            input.Next.pointer = cam.WorldToScreenPoint(new Vector3(3f, 0.8f, 0f));
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Angle(p.Facing, Vector3.right), 3f, "faces the pointer");

            input.Next.move = Vector2.left;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Angle(p.Facing, Vector3.right), 3f);

            input.Next.move = Vector2.zero;
            input.Next.look = Vector2.down;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(Vector3.Angle(p.Facing, Vector3.back), 3f, "faces the stick");

            cam.targetTexture = null;
            Object.Destroy(target);
        }

        static string Path(InputAction action, int binding = 0) => action.bindings[binding].path;

        [Test]
        public void RebindingSwapsClashesSavesAndResets()
        {
            var d = DesktopBinding.Shared;
            KeyBindings.ResetToDefaults();
            try
            {
                Assert.AreEqual("WASD", ControlHints.MoveKeys);
                KeyBindings.Assign(d.Jump, 0, "<Keyboard>/f");
                Assert.AreEqual("<Keyboard>/f", d.Jump.bindings[0].effectivePath);
                Assert.AreEqual("F", ControlHints.KeyOf(d.Jump), "prompts read the live key");
                KeyBindings.Assign(d.Spell, 0, "<Keyboard>/f");
                Assert.AreEqual("<Keyboard>/f", d.Spell.bindings[0].effectivePath);
                Assert.AreEqual("<Keyboard>/q", d.Jump.bindings[0].effectivePath, "a clash swaps the two keys");
                StringAssert.Contains("<Keyboard>/f", PlayerPrefs.GetString(KeyBindings.OverridesKey), "saved");

                d.Map.RemoveAllBindingOverrides();
                KeyBindings.Load(d.Map);
                Assert.AreEqual("<Keyboard>/f", d.Spell.bindings[0].effectivePath, "loaded back on the next start");

                int up = -1;
                for (int i = 0; i < d.Move.bindings.Count; i++) if (d.Move.bindings[i].name == "Up") up = i;
                KeyBindings.Assign(d.Move, up, "<Keyboard>/upArrow");
                StringAssert.Contains("/", ControlHints.MoveKeys);

                KeyBindings.SetSensitivity(2f);
                KeyBindings.SetInvertY(true);
                var look = KeyBindings.Look(new Vector2(10f, 10f));
                Assert.AreEqual(10f * ShoulderView.MouseSensitivity * 2f, look.x, 1e-5f);
                Assert.AreEqual(10f * ShoulderView.MouseSensitivity * 2f, look.y, 1e-5f, "inverted: mouse up looks down");
            }
            finally { KeyBindings.ResetToDefaults(); }
            Assert.AreEqual("<Keyboard>/space", d.Jump.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/q", d.Spell.bindings[0].effectivePath);
            Assert.AreEqual(1f, KeyBindings.Sensitivity);
            Assert.IsFalse(KeyBindings.InvertY);
            Assert.IsFalse(PlayerPrefs.HasKey(KeyBindings.OverridesKey));
        }

        [Test]
        public void DesktopKeysMatchTheBrief()
        {
            var d = DesktopBinding.Shared;
            Assert.AreEqual("keyboard-mouse", d.Id);
            Assert.AreEqual("<Mouse>/leftButton", Path(d.Attack));
            Assert.AreEqual("<Mouse>/rightButton", Path(d.Aim));
            Assert.AreEqual("<Keyboard>/space", Path(d.Jump));
            Assert.AreEqual("<Keyboard>/leftShift", Path(d.Dodge));
            Assert.AreEqual("<Keyboard>/e", Path(d.Interact));
            Assert.AreEqual("<Keyboard>/q", Path(d.Spell));
            Assert.AreEqual("<Keyboard>/r", Path(d.Drop));
            Assert.AreEqual("<Mouse>/position", Path(d.Point));
            Assert.AreEqual("<Keyboard>/w", Path(d.Move, 1));
            Assert.AreEqual("<Keyboard>/s", Path(d.Move, 2));
            Assert.AreEqual("<Keyboard>/a", Path(d.Move, 3));
            Assert.AreEqual("<Keyboard>/d", Path(d.Move, 4));
            Assert.IsTrue(d.Map.enabled);
            StringAssert.StartsWith("Press SPACE", ControlHints.Join("join"));
            foreach (var action in d.Map.actions)
                foreach (var binding in action.bindings)
                    Assert.IsFalse(binding.path is "<Keyboard>/f" or "<Keyboard>/g", action.name + " is bound to " + binding.path);
            Assert.AreEqual("Hold R, ' or Numpad6, or LB", ControlHints.Drop);
        }

        static HeldWeapon Craft(PlayerController p, string word)
        {
            p.Inventory.Set(word);
            Assert.IsTrue(p.Summoner.Summon(word), word + " is crafted into the hand");
            Assert.IsNotNull(p.Combat.Weapon, word + " is in hand");
            return p.Combat.Weapon;
        }

        [UnityTest]
        public IEnumerator AttackPlacesToolsTableIncluded()
        {
            var words = new[] { "TABLE", "BED" };
            for (int i = 0; i < words.Length; i++)
            {
                var p = SpawnPlayer(i, new Vector3(i * 8f, 0f, 0f), out var input);
                yield return Frames(3);
                p.FaceTowards(Vector3.forward);
                Craft(p, words[i]);
                input.Next.attack = true;
                yield return TestScenes.WaitUntil(() => DeployedGear.CountFor(p) == 1, 3f, words[i] + " placed by the attack button");
                Assert.IsFalse(p.Combat.Weapon, words[i] + " left the hand");
            }
        }

        [UnityTest]
        public IEnumerator InteractPicksUpAPropBesideGearAndAttackThrowsIt()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var bat = Craft(p, "BAT");
            var chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = "Test chair";
            chair.transform.position = new Vector3(0f, 0.25f, 0.9f);
            chair.transform.localScale = Vector3.one * 0.5f;
            var body = chair.AddComponent<Rigidbody>();
            body.mass = 2f;
            int thrown = 0;
            p.Combat.Thrown += _ => thrown++;
            yield return Frames(3);

            input.Next.grab = true;
            yield return Updates(3);
            Assert.AreEqual(0, thrown, "E never throws");
            Assert.AreEqual(body, p.Combat.Held, "the prop is picked up");
            Assert.AreEqual(bat, p.Combat.StoredGear, "the BAT moves to the other hand instead of being lost");

            input.Next.grab = true;
            yield return Updates(3);
            Assert.AreEqual(body, p.Combat.Held, "E again while carrying does nothing");

            input.Next.attack = true;
            yield return Updates(3);
            Assert.AreEqual(1, thrown, "the attack button throws what's carried");
            Assert.IsFalse(p.Combat.IsHolding);
            Assert.AreEqual(bat, p.Combat.StoredGear, "the BAT is still there for later");
        }
    }
}
