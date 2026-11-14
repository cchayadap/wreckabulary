using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Audio;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BeltRangeTests
    {
        readonly List<ImpactPlayback> played = new();
        bool muted;
        float volume;

        void Record(ImpactPlayback p) => played.Add(p);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            muted = GameFeedback.Muted;
            volume = AudioListener.volume;
            GameFeedback.Muted = false;
            AudioListener.volume = 1f;
            yield return TestScenes.Reset();
            played.Clear();
            ImpactAudioPlayer.Played += Record;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ImpactAudioPlayer.Played -= Record;
            GameFeedback.Muted = muted;
            AudioListener.volume = volume;
            yield return TestScenes.Reset();
        }

        IEnumerator Range(System.Action<BeltRangeDirector, PlayerController, ScriptedBinding> ready)
        {
            yield return TestScenes.Load(BeltRangeDirector.SceneName);
            var director = Object.FindAnyObjectByType<BeltRangeDirector>();
            Assert.IsNotNull(director, "BELT range director");
            var input = new ScriptedBinding();
            var player = director.Joins.Join(input);
            yield return new WaitForSeconds(0.7f);
            yield return TestScenes.WaitUntil(() => player.CanAct && player.Combat.Weapon, 3f, "player holding BELT");
            ready(director, player, input);
        }

        static Smashable Target(BeltRangeDirector director, string word) =>
            director.LiveTarget(director.Targets.Single(t => t.word == word));

        static void StandBefore(PlayerController player, Smashable target, float distance)
        {
            var bounds = target.GetComponentsInChildren<Collider>().Select(c => c.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var at = new Vector3(bounds.center.x, 0f, bounds.min.z - distance);
            player.Respawn(at);
            player.Body.linearVelocity = Vector3.zero;
            player.FaceTowards(Vector3.forward);
            player.ResetLook();
        }

        IEnumerator Swing(PlayerController player, ScriptedBinding input)
        {
            var weapon = player.Combat.Weapon;
            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => weapon.SwingStartedAt >= 0f, 1f, "swing start");
            yield return TestScenes.WaitUntil(() => weapon.SwingStartedAt < 0f, 2f, "swing end");
            yield return new WaitForSeconds(0.1f);
        }

        [UnityTest]
        public IEnumerator RangeHandsOutAnIsolatedBelt()
        {
            BeltRangeDirector director = null; PlayerController player = null;
            yield return Range((d, p, _) => { director = d; player = p; });
            var weapon = player.Combat.Weapon;
            Assert.AreEqual("BELT", weapon.word);
            Assert.IsFalse(weapon.Definition.Enabled, "BELT is a disabled prototype definition");
            Assert.IsFalse(GameConfig.Current.Items.TryGet("BELT", out _), "BELT is not in the shared catalogue");
            var flex = weapon.GetComponent<BeltFlexDriver>();
            Assert.IsTrue(flex.Ready, "strap rig bones found");
            yield return new WaitForSeconds(0.2f);
            var visual = flex.Root.parent;
            Assert.Greater(flex.Root.position.y - flex.Tip.position.y, 0.15f,
                $"held strap hangs from the hand; held={weapon.IsHeld} root={flex.Root.name}{flex.Root.position:F3} tip={flex.Tip.position:F3} " +
                $"visual={visual.name} up={visual.up:F2} fwd={visual.forward:F2} right={visual.right:F2} angles=[{string.Join(",", flex.Angles.Select(a => a.ToString("F1")))}] " +
                $"chain=[{string.Join(" ", flex.Root.GetComponentsInChildren<Transform>().Select(b => b.name + b.position.ToString("F2")))}]");
            Assert.IsNotNull(weapon.GetComponent<BeltWeapon>());
            Assert.AreEqual(6, director.Targets.Count(t => director.LiveTarget(t)));
            foreach (var t in director.Targets)
                Assert.AreEqual(t.material, SurfaceAudioTag.Resolve(director.LiveTarget(t)), t.word);
            Assert.IsNotNull(director.Dummy, "practice dummy");
            Assert.IsTrue(ImpactAudioBridge.Active, "range turns material sounds on");
            Assert.IsTrue(ImpactAudioBridge.VoicesSwing(weapon));
        }

        [UnityTest]
        public IEnumerator StrapHitOnGlassPlaysStrapContactAndCrackWithoutBreaking()
        {
            BeltRangeDirector director = null; PlayerController player = null; ScriptedBinding input = null;
            yield return Range((d, p, i) => { director = d; player = p; input = i; });
            var glass = Target(director, "GLASS");
            StandBefore(player, glass, 0.6f);
            yield return null;
            played.Clear();
            yield return Swing(player, input);
            Assert.IsFalse(glass.IsBroken);
            Assert.Less(glass.Remaining, 1f, "the strap hit landed");
            CollectionAssert.AreEqual(new[] { ImpactLayer.Swing, ImpactLayer.Contact, ImpactLayer.Damage }, played.Select(p => p.Layer).ToArray(),
                string.Join(", ", played.Select(p => p.Key)));
            Assert.AreEqual("swing/BELT", played[0].Key);
            Assert.AreEqual("contact/BELT/Strap/Glass", played[1].Key);
            Assert.AreEqual("damage/Glass", played[2].Key);
        }

        [UnityTest]
        public IEnumerator BuckleHitsHarderThanTheStrap()
        {
            BeltRangeDirector director = null; PlayerController player = null; ScriptedBinding input = null;
            yield return Range((d, p, i) => { director = d; player = p; input = i; });
            var crate = Target(director, "CRATE");
            float reach = player.Combat.Weapon.GetComponent<BeltWeapon>().Definition.Melee.Reach;
            StandBefore(player, crate, reach - 0.06f);
            yield return null;
            float before = crate.Health;
            played.Clear();
            yield return Swing(player, input);
            Assert.AreEqual(StrikePart.Buckle, player.Combat.Weapon.GetComponent<BeltWeapon>().LastPart);
            Assert.AreEqual(1.5f * Smashable.HealthPerBreakPower, before - crate.Health, 0.01f, "buckle break power");
            Assert.IsTrue(played.Any(p => p.Key == "contact/BELT/Buckle/Wood"), string.Join(", ", played.Select(p => p.Key)));
        }

        [UnityTest]
        public IEnumerator BatBreakPlaysBreakOnceAndRespawns()
        {
            BeltRangeDirector director = null; PlayerController player = null; ScriptedBinding input = null;
            yield return Range((d, p, i) => { director = d; player = p; input = i; });
            Assert.IsNotNull(director.EquipCatalogue(player, "BAT"));
            var vase = Target(director, "VASE");
            StandBefore(player, vase, 0.6f);
            yield return null;
            played.Clear();
            yield return Swing(player, input);
            Assert.IsTrue(vase == null || vase.IsBroken, "BAT breaks the vase in one hit");
            Assert.AreEqual(1, played.Count(p => p.Layer == ImpactLayer.Break), string.Join(", ", played.Select(p => p.Key)));
            Assert.AreEqual("break/Ceramic", played.Single(p => p.Layer == ImpactLayer.Break).Key);
            Assert.AreEqual("contact/BAT/Any/Ceramic", played.Single(p => p.Layer == ImpactLayer.Contact).Key);
            Assert.IsFalse(played.Any(p => p.Layer == ImpactLayer.Damage), "a break is not also a crack");
            yield return TestScenes.WaitUntil(() => Target(director, "VASE") && !Target(director, "VASE").IsBroken, 4f, "vase respawn");
        }

        [UnityTest]
        public IEnumerator MissPlaysOnlyTheSwing()
        {
            PlayerController player = null; ScriptedBinding input = null;
            yield return Range((_, p, i) => { player = p; input = i; });
            player.Respawn(new Vector3(0f, 0f, -4.5f));
            player.FaceTowards(Vector3.back);
            player.ResetLook();
            yield return null;
            played.Clear();
            yield return Swing(player, input);
            CollectionAssert.AreEqual(new[] { "swing/BELT" }, played.Select(p => p.Key).ToArray());
        }

        [UnityTest]
        public IEnumerator SameStrapPoseForEveryViewer()
        {
            PlayerController player = null;
            yield return Range((_, p, _) => player = p);
            var flex = player.Combat.Weapon.GetComponent<BeltFlexDriver>();
            var copy = new GameObject("Remote BELT").AddComponent<BeltFlexDriver>();
            var profile = Object.FindAnyObjectByType<BeltRangeDirector>().Belt;
            var visual = Object.Instantiate(profile.visual, copy.transform);
            copy.Configure(profile, profile.Definition(), visual.transform);
            var a = new float[BeltFlexDriver.Segments];
            var b = new float[BeltFlexDriver.Segments];
            var melee = profile.Definition().Melee;
            foreach (double since in new[] { melee.Windup * 0.75, melee.Windup + melee.Active * 0.5, melee.Cycle - 0.05 })
            {
                CollectionAssert.AreEqual(flex.Evaluate(since, true, true, 1.0, 0f, a), copy.Evaluate(since, true, true, 1.0, 0f, b));
                Assert.AreEqual(FlexibleSwing.PhaseAt(melee, since), copy.Phase);
            }
            Object.Destroy(copy.gameObject);
        }

        [UnityTest]
        public IEnumerator SkinsSwapLeatherAndThrowReleasesTheBelt()
        {
            BeltRangeDirector director = null; PlayerController player = null;
            yield return Range((d, p, _) => { director = d; player = p; });
            var weapon = player.Combat.Weapon;
            var skin = weapon.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.AreEqual(director.Belt.leather[0], skin.sharedMaterials[0]);
            Assert.AreEqual("Candy", director.CycleSkin(player));
            Assert.AreEqual(director.Belt.leather[1], skin.sharedMaterials[0]);
            Assert.AreEqual("Arcade", director.CycleSkin(player));
            Assert.AreEqual(director.Belt.leather[2], skin.sharedMaterials[0]);
            Assert.IsTrue(skin.sharedMaterials[1].name.StartsWith("gold"), "buckle keeps its gold");
            director.Throw(player);
            yield return null;
            Assert.IsNull(player.Combat.Weapon, "thrown BELT leaves the hand");
            Assert.IsFalse(weapon.IsHeld);
        }

        [UnityTest]
        public IEnumerator HudShowsYouNotTheDummyWithARoomMap()
        {
            BeltRangeDirector director = null; PlayerController player = null;
            yield return Range((d, p, _) => { director = d; player = p; });
            var hud = GameHud.Active;
            Assert.IsNotNull(director.Dummy);
            Assert.AreNotSame(director.Dummy, hud.LocalPlayer, "The practice dummy has no controls; it is never you.");
            hud.Follow(player);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreSame(player, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");
            Assert.IsTrue(safe.Find("Side column/Minimap").gameObject.activeInHierarchy, "The range has a map, top right.");
            Assert.IsNotNull(safe.Find("Side column/Minimap/Floor/House plan/Rooms/BELT range"), "The map draws the range as one room.");
            Assert.IsFalse(safe.Find("Side column/Room").gameObject.activeSelf, "One room: the map's own label is enough.");
            Assert.IsNotNull(safe.Find($"Side column/Minimap/Floor/House plan/Markers/Player {player.Index + 1}"), "You are on the map.");
            Assert.IsFalse(safe.Find("Side column/Alive").gameObject.activeSelf, "No head count with one player.");
            Assert.IsFalse(safe.Find("Roommate 1").gameObject.activeSelf, "Your own stats stay bottom left, not on a card.");
            Assert.IsNull(safe.Find("Match label").GetComponent<Image>(), "The mode title sits on the game, with no card.");
        }

        [UnityTest]
        public IEnumerator OutsideTheRangeOriginalCuesStay()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            yield return null;
            Assert.IsFalse(ImpactAudioBridge.Active, "no scope, House modes off");
            player.Inventory.Set("BAT");
            yield return TestScenes.WaitUntil(() => player.CanAct, 2f, "player ready");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            Assert.IsFalse(ImpactAudioBridge.VoicesSwing(player.Combat.Weapon), "Tutorial keeps the generic swing cue");
        }
    }
}
