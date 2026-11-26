using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ReversalRuntimeTests
    {
        const float Walk = 0.6f, Moved = 0.5f, Still = 0.35f;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerHealth.TraitRoll = null;
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(int seat, Vector3 at, InputBinding input)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, input);
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.LettersDroppedPerHit = 0;
            player.Health.UseRules(rules);
            player.Respawn(at);
            player.Inventory.Collects = false;
            return player;
        }

        static PlayerController Spawn(int seat, Vector3 at, out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            return Spawn(seat, at, input);
        }

        static HeldWeapon Craft(PlayerController player, string id)
        {
            player.Inventory.Set(id);
            Assert.IsTrue(player.Summoner.Summon(id), id + " is spelled from its letters");
            Assert.AreEqual(id, player.Combat.Weapon.word);
            return player.Combat.Weapon;
        }

        static void Pin(params PlayerController[] players)
        {
            foreach (var p in players) p.Body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator AMirroredPlayerWalksRightOnLeftAndNormallyAfterwards()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            Assert.IsTrue(p.Reverse(ReverseMode.Mirror, 1.5f, "SPINNER"));
            var start = p.transform.position;
            input.Next.move = Vector2.left;
            yield return new WaitForSeconds(Walk);
            Assert.Greater(p.transform.position.x, start.x + Moved, "left walks right while mirrored");
            Assert.AreEqual(start.z, p.transform.position.z, Still, "forward and back are untouched by a mirror");
            Assert.AreEqual(ReverseMode.Mirror, p.Reversed);
            Assert.Greater(p.ReversedLeft, 0f);
            Assert.AreEqual("SPINNER", p.ReversedBy, "the HUD chip shows the item that did it");
            yield return TestScenes.WaitUntil(() => p.Reversed == ReverseMode.None, 2f, "the reversal ends on time");
            yield return new WaitForSeconds(.2f);
            var after = p.transform.position;
            yield return new WaitForSeconds(Walk);
            Assert.Less(p.transform.position.x, after.x - Moved, "left walks left again once it ends");
            input.Next.move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator FlipSwapsForwardAndBackAndTurnaboutSwapsBoth()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.Reverse(ReverseMode.Flip, 3f, "FLIPPER");
            var start = p.transform.position;
            input.Next.move = new Vector2(.7071f, .7071f);
            yield return new WaitForSeconds(Walk);
            Assert.Greater(p.transform.position.x, start.x + Moved * .5f, "a flip keeps right as right");
            Assert.Less(p.transform.position.z, start.z - Moved * .5f, "but forward walks back");
            p.Reverse(ReverseMode.Mirror, 3f, "SPINNER");
            Assert.AreEqual(ReverseMode.Turnabout, p.Reversed, "a mirror on top of a flip is a full turnabout, never normal again");
            start = p.transform.position;
            yield return new WaitForSeconds(Walk);
            Assert.Less(p.transform.position.x, start.x - Moved * .5f);
            Assert.Less(p.transform.position.z, start.z - Moved * .5f);
            input.Next.move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator ABotStumblesThenWaitsThenWalksWhereItMeans()
        {
            var binding = new BotBinding();
            var bot = Spawn(0, Vector3.zero, binding);
            yield return Frames(3);
            Assert.IsTrue(bot.Reverse(ReverseMode.Turnabout, 4f, "HOOP"));
            var start = bot.transform.position;
            float began = Time.time, settle = Reversal.BotStumbleSeconds + .45f, ready = Reversal.BotStumbleSeconds + Reversal.BotWaitSeconds - .05f;
            Vector3 stumbled = start, settled = start, waited = start;
            while (Time.time - began < Reversal.BotStumbleSeconds + Reversal.BotWaitSeconds + 1f)
            {
                binding.Commands.move = bot.SteerReversed(Vector2.up);
                float t = Time.time - began;
                if (t < Reversal.BotStumbleSeconds - .05f) stumbled = bot.transform.position;
                if (t < settle) settled = bot.transform.position;
                if (t < ready) waited = bot.transform.position;
                yield return null;
            }
            Assert.Less(stumbled.z, start.z - .2f, "a reversed bot first stumbles the wrong way, like a person");
            Assert.AreEqual(settled.z, waited.z, .15f, "then it stops and waits instead of pushing into a wall");
            Assert.Greater(bot.transform.position.z, waited.z + Moved, "then it walks where it means to while still reversed");
            Assert.AreEqual(ReverseMode.Turnabout, bot.Reversed, "it recovered before the effect ended");
            binding.Commands.move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator KnockoutRoundEndRespawnAndFreezeEndIt()
        {
            var p = Spawn(0, Vector3.zero, out _);
            var q = Spawn(1, Vector3.right * 3f, out _);
            yield return Frames(3);
            p.Reverse(ReverseMode.Turnabout, 4f, "HOOP");
            p.Health.Eliminate();
            Assert.AreEqual(ReverseMode.None, p.Reversed, "a knockout ends it");
            Assert.IsFalse(p.Reverse(ReverseMode.Turnabout, 4f, "HOOP"), "nobody reverses a knocked-out player");
            q.Reverse(ReverseMode.Mirror, 4f, "SPINNER");
            World.FreezeTransient();
            Assert.AreEqual(ReverseMode.None, q.Reversed, "the round-end freeze ends it");
            q.Reverse(ReverseMode.Mirror, 4f, "SPINNER");
            q.Respawn(Vector3.right * 3f);
            Assert.AreEqual(ReverseMode.None, q.Reversed, "a new round starts with normal controls");
            q.Frozen = true;
            Assert.IsFalse(q.Reverse(ReverseMode.Mirror, 4f, "SPINNER"), "a frozen player (countdown, round over) is not reversed");
        }

        [UnityTest]
        public IEnumerator TheMakerIsNeverReversedByItsOwnBlowAndABlockStopsIt()
        {
            var p = Spawn(0, Vector3.zero, out _);
            var q = Spawn(1, Vector3.forward * 1.5f, out _);
            yield return Frames(3);
            Pin(p, q);
            p.Health.ApplyDamage(Hits.Of(p, Vector3.forward, HitSource.Thrown, 2f, 0f, 0f, 0f, "SPINNER"));
            Assert.AreEqual(ReverseMode.None, p.Reversed, "its own spinner never reverses the thrower");
            q.Health.ApplyDamage(Hits.Of(p, Vector3.forward, HitSource.Thrown, 2f, 0f, 0f, 0f, "SPINNER"));
            Assert.AreEqual(ReverseMode.Mirror, q.Reversed, "the player it hits is mirrored");
            q.ClearReversal();
            q.Health.GiveBubble(50f, 5f);
            q.Health.ApplyDamage(Hits.Of(p, Vector3.forward, HitSource.Thrown, 2f, 0f, 0f, 0f, "SPINNER"));
            Assert.AreEqual(ReverseMode.None, q.Reversed, "a bubble that soaks the whole hit also stops the reversal");
        }

        [UnityTest]
        public IEnumerator TheThrownSpinnerMirrorsWhoItHits()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            var q = Spawn(1, Vector3.forward * 3f, out _);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            q.Body.constraints = RigidbodyConstraints.FreezeAll;
            Craft(p, "SPINNER");
            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => q.Reversed != ReverseMode.None, 3f, "the spinner reverses the player it hits");
            Assert.AreEqual(ReverseMode.Mirror, q.Reversed);
            Assert.AreEqual(ReverseMode.None, p.Reversed, "and never its thrower");
            Assert.LessOrEqual(q.ReversedLeft, GameConfig.Current.Tuning.Trait("SPINNER", TraitKind.Reverse).Seconds + .01f);
        }

        [UnityTest]
        public IEnumerator TheFlipperFlipsOnEveryThirdSlap()
        {
            var p = Spawn(0, Vector3.zero, out _);
            var q = Spawn(1, Vector3.forward * 1.2f, out _);
            yield return Frames(3);
            Pin(p, q);
            PlayerHealth.TraitRoll = () => 0f;
            for (int blow = 1; blow <= 3; blow++)
            {
                q.Health.ApplyDamage(Hits.Of(p, Vector3.forward, HitSource.Melee, 1f, 0f, 0f, 0f, "FLIPPER"));
                Assert.AreEqual(blow == 3 ? ReverseMode.Flip : ReverseMode.None, q.Reversed, "slap " + blow);
            }
        }

        [UnityTest]
        public IEnumerator TheHoopReversesWhoeverIsInsideAndLetsGoSoonAfterTheyLeave()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, "HOOP");
            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => DeployedGear.CountFor(p) == 1, 3f, "HOOP placed");
            var centre = gear.transform.position;
            var q = Spawn(1, centre + Vector3.right * 1f, out _);
            yield return TestScenes.WaitUntil(() => q.Reversed == ReverseMode.Turnabout, 2f, "standing in the hoop turns everything about");
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(ReverseMode.Turnabout, q.Reversed, "it lasts while they stay inside");
            Assert.AreEqual(ReverseMode.None, p.Reversed, "the maker is never caught by their own hoop");
            q.Body.position = q.transform.position = centre + Vector3.right * 6f;
            Physics.SyncTransforms();
            float tail = GameConfig.Current.Tuning.Trait("HOOP", TraitKind.Reverse).Seconds;
            yield return TestScenes.WaitUntil(() => q.Reversed == ReverseMode.None, tail + 1f, "it ends a moment after stepping out");
        }

        [UnityTest]
        public IEnumerator ARaisedCompassTurnsAboutEveryoneNearIncludingTheHolder()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            var near = Spawn(1, Vector3.right * 2f, out _);
            var far = Spawn(2, Vector3.right * 6f, out _);
            yield return Frames(3);
            Craft(p, "COMPASS");
            Assert.AreEqual(ReverseMode.None, p.Reversed, "carrying it lowered does nothing");
            input.Next.blockHeld = true;
            yield return TestScenes.WaitUntil(() => near.Reversed == ReverseMode.Turnabout, 2f, "raising it reverses a rival in reach");
            Assert.AreEqual(ReverseMode.Turnabout, p.Reversed, "the holder is reversed too, as the card says");
            Assert.AreEqual(ReverseMode.None, far.Reversed, "a rival out of reach is not");
            input.Next.blockHeld = false;
            yield return TestScenes.WaitUntil(() => near.Reversed == ReverseMode.None && p.Reversed == ReverseMode.None, 2f, "lowering it lets everyone go");
        }

        [UnityTest]
        public IEnumerator TeammatesAreSparedWhenFriendlyFireIsOff()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            var mate = Spawn(1, Vector3.right * 2f, out _);
            yield return Frames(3);
            mate.Team = p.Team;
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.FriendlyFire = false;
            p.Health.UseRules(rules);
            mate.Health.UseRules(rules);
            Craft(p, "COMPASS");
            input.Next.blockHeld = true;
            yield return TestScenes.WaitUntil(() => p.Reversed == ReverseMode.Turnabout, 2f, "the holder still reverses themselves");
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(ReverseMode.None, mate.Reversed, "a teammate is spared when friendly fire is off");
            mate.Health.ApplyDamage(Hits.Of(p, Vector3.right, HitSource.Thrown, 2f, 0f, 0f, 0f, "SPINNER"));
            Assert.AreEqual(ReverseMode.None, mate.Reversed, "and a teammate's spinner does not land either");
            input.Next.blockHeld = false;
        }
    }
}
