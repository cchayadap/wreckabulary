using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class FlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        static int ModeIndex(Typewriter t, string scene) =>
            t.Modes.Select((m, i) => (m, i)).First(x => x.m.scene == scene).i;

        [UnityTest]
        public IEnumerator RoommatesWalkInThroughTheFrontDoor()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var p = joins.Join(new ScriptedBinding());
            float startX = p.transform.position.x;
            Assert.Less(startX, -8.7f, "arrives outside, on the porch");

            yield return new WaitForSeconds(1f);
            Assert.Greater(p.transform.position.x, -8.4f, "walked in through the doorway");
            Assert.AreEqual(1, Session.Bindings.Count, "remembered for the next scene");
        }

        [UnityTest]
        public IEnumerator FourRoommatesArrivingTogetherAllGetThroughTheDoor()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            for (int i = 0; i < 4; i++) joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(2f);
            foreach (var p in joins.Players)
                Assert.Greater(p.transform.position.x, -8.4f, $"{p.Name} made it inside");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsDibsWithTheHouseRoommates()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new ScriptedBinding());
            joins.Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();

            Assert.IsTrue(typewriter.Choose(ModeIndex(typewriter, Session.DibsScene)));
            yield return TestScenes.WaitForActive(Session.DibsScene);

            var dibsJoins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(2, dibsJoins.HumanCount, "both roommates came along");
            Assert.AreEqual(4, dibsJoins.Players.Count, "remaining seats have AI opponents");
            Assert.AreEqual(new[] { "P1", "P2" }, dibsJoins.Players.Where(p => p.Binding is not BotBinding).Select(p => p.Name).ToArray());
            Assert.AreEqual(Phase.Countdown, RoundManager.Instance.Phase, "coming from the house skips the lobby");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsSoloDibsWithAnAiOpponent()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            Assert.IsTrue(typewriter.Choose(ModeIndex(typewriter, Session.DibsScene)));
            yield return TestScenes.WaitForActive(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount);
            Assert.AreEqual(4, joins.Players.Count);
            Assert.AreEqual(1, Session.Bindings.Count, "AI is not carried home");
        }

        [UnityTest]
        public IEnumerator TheTypewriterListsEveryModeByItsPlayerNameAndGoal()
        {
            var typewriter = new GameObject("Named typewriter").AddComponent<Typewriter>();
            yield return null;
            Assert.Greater(typewriter.Modes.Count, 1);
            foreach (var mode in typewriter.Modes)
            {
                Assert.IsTrue(HouseModes.Has(mode.id), mode.label);
                Assert.AreEqual(HouseModes.Name(mode.id).ToUpperInvariant(), mode.label, mode.id);
                Assert.AreEqual(HouseModes.Goal(mode.id), mode.blurb, mode.id);
                Assert.AreEqual(Session.SceneForMode(mode.id), mode.scene, mode.id);
            }
            var labels = typewriter.Modes.Select(m => m.label).ToArray();
            CollectionAssert.AllItemsAreUnique(labels);
            foreach (string stale in new[] { "DIBS!", "MOVING DAY", "MOVING OUT", "DEATHMATCH", "WAR", "SHOPPING LIST", "RIDDLE RUSH" })
                CollectionAssert.DoesNotContain(labels, stale);
            Assert.AreEqual(HouseModes.Tutorial, typewriter.Modes[0].id);
            Assert.AreEqual(HouseModes.Dibs, typewriter.Modes[typewriter.Selected].id, "the menu opens on Last One Standing");
            Assert.AreEqual(HouseModes.Dibs, typewriter.Modes.First(m => m.scene == Session.DibsScene).id);
            Object.Destroy(typewriter.gameObject);
        }

        [UnityTest]
        public IEnumerator TypewriterIsUsedWithGrabAndStepsWithUpDown()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            yield return new WaitForSeconds(0.8f);

            p.Respawn(typewriter.transform.position + Vector3.back * 1.2f + Vector3.down * 0.82f);
            yield return new WaitForFixedUpdate();
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.AreEqual(p, typewriter.User);
            Assert.IsTrue(p.Frozen, "sitting at the typewriter");

            int before = typewriter.Selected;
            input.Next.down = true;
            yield return null;
            yield return null;
            Assert.AreEqual((before + 1) % typewriter.Modes.Count, typewriter.Selected);

            input.Next.spellDown = true;
            yield return null;
            yield return null;
            Assert.IsNull(typewriter.User);
            Assert.IsFalse(p.Frozen);
        }

        [UnityTest]
        public IEnumerator KnockedOutRoommatesGetBackUpInTheHouse()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var p = joins.Join(new ScriptedBinding());
            yield return null;
            Assert.IsTrue(p.Health.IsInvulnerable, "spawn protection");
            yield return TestScenes.WaitUntil(() => !p.Health.IsInvulnerable, 3f, "spawn protection to wear off");
            p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 1000f, 1f));
            Assert.IsTrue(p.IsEliminated);
            yield return new WaitForSeconds(2.3f);
            Assert.IsFalse(p.IsKnockedOut);
            Assert.AreEqual(p.Health.Max, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator TutorialCanBeCompleted()
        {
            bool hadDone = PlayerPrefs.HasKey(TutorialDirector.DoneKey);
            int wasDone = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            try
            {
                yield return CompleteTutorial();
                Assert.IsTrue(TutorialDirector.Done, "finishing the last step marks the tutorial done for the lobby");
                yield return EndCardStartsDibs();
            }
            finally
            {
                if (hadDone) PlayerPrefs.SetInt(TutorialDirector.DoneKey, wasDone);
                else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
                PlayerPrefs.Save();
            }
        }

        static Smashable Standing(string word) => Object.FindObjectsByType<Smashable>().FirstOrDefault(s => s.Word == word && !s.IsBroken);

        static IEnumerator ReachTheTable(TutorialDirector director, ScriptedBinding input)
        {
            input.Next.move = new Vector2(1f, 0f);
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 1, 3f, "walk step");
            input.Next.move = Vector2.zero;

            input.Next.jump = true;
            yield return new WaitForSeconds(0.8f);
            input.Next.dodge = true;
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 2, 2f, "jump and dodge step");
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.FirstFurniture), 2f, "a TABLE to smash");
        }

        static int Index(TutorialStep step) => TutorialSteps.Order.ToList().IndexOf(step);

        static IEnumerator Ready(PlayerController p, string what) =>
            TestScenes.WaitUntil(() => p.CanAct && !p.IsDodging && !p.Combat.IsChanneling && !p.Summoner.IsCrafting, 3f, what);

        static IEnumerator Vulnerable(PlayerController p) =>
            TestScenes.WaitUntil(() => !p.Health.IsInvulnerable, 5f, "spawn protection to end");

        static bool HudSays(GameHud hud, string words) =>
            hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text != null && t.text.Contains(words));

        static void StandInFrontOf(PlayerController p, PlayerController dummy)
        {
            p.Respawn(dummy.transform.position + Vector3.back * 1.2f);
            p.FaceTowards(Vector3.forward);
        }

        static IEnumerator PlayToTheThrow(TutorialDirector director, GameHud hud, ScriptedBinding input, PlayerController p)
        {
            yield return ReachTheTable(director, input);
            Assert.IsNull(Standing("BAT"), "no special BAT box: real furniture drops the letters of its name");
            Standing(TutorialDirector.FirstFurniture).Break();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 3, 1f, "smash step");

            p.Inventory.Set("TABLE");
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 4, 1f, "collect step");
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 5, 1f, "recipe book step");
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, 1f, "book closed again");

            yield return TestScenes.WaitUntil(() => !p.IsDodging && p.CanAct, 2f, "dodge recovery before spelling BAT");
            Assert.IsTrue(p.Summoner.Summon("BAT"));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 6, 1f, "spell step");
            CollectionAssert.AreEquivalent("LE".ToCharArray(), p.Inventory.Letters, "spare letters stay in the bag");

            var dummy = director.Dummy;
            Assert.AreEqual("BAT", p.Combat.Weapon?.word);
            Assert.IsTrue(dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Combat.Weapon.Stats, "BAT")));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 7, 1f, "hit step");

            var chair = Standing("CHAIR");
            yield return new WaitForSeconds(0.3f);
            p.Respawn(chair.transform.position + Vector3.back * 1.2f);
            p.FaceTowards(Vector3.forward);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(p.Combat.TryGrab(), "grab the chair while the BAT waits in the other hand");
            p.Combat.Throw();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 8, 1f, "throw step");
            Assert.AreEqual("BAT", p.Combat.StoredGear?.word);
            Assert.AreEqual(TutorialStep.Place, director.Current, "the new steps start once one hand is free");
        }

        static IEnumerator PlaceATable(TutorialDirector director, PlayerController p)
        {
            Assert.AreEqual(TutorialStep.Place, director.Current);
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.FirstFurniture), 3f, "a TABLE to spell and place");
            Standing(TutorialDirector.FirstFurniture).Break();
            p.Inventory.Set(TutorialDirector.PlaceWord);
            yield return Ready(p, "ready to spell TABLE");
            Assert.IsTrue(p.Summoner.Summon(TutorialDirector.PlaceWord), "TABLE fits the free hand");
            Assert.AreEqual(TutorialDirector.PlaceWord, p.Combat.Weapon?.word);
            yield return null;
            Assert.AreEqual(Index(TutorialStep.Place), director.StepIndex, "holding the TABLE is not placing it");
            yield return Ready(p, "ready to place the TABLE");
            Assert.IsTrue(p.Combat.DeployHeld());
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.Place), 5f, "place step");
            Assert.IsNull(p.Combat.Weapon, "the placed TABLE left the hand");
            Assert.AreEqual(1, DeployedGear.CountFor(p));
        }

        static IEnumerator EatAnApple(TutorialDirector director, PlayerController p)
        {
            Assert.AreEqual(TutorialStep.Heal, director.Current);
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.HealWord), 3f, "an APPLE to smash");
            yield return Vulnerable(p);
            Assert.AreEqual(p.Health.Max, p.Health.Current, "unhurt so far");
            Assert.IsTrue(p.Health.ApplyDamage(Hits.Of(director.Dummy, Vector3.forward, HitSource.Melee, TutorialDirector.DummyJab, 0f, hitStun: 0f)));
            Assert.AreEqual(p.Health.Max - TutorialDirector.DummyJab, p.Health.Current, 0.01f);
            Standing(TutorialDirector.HealWord).Break();
            p.Inventory.Set(TutorialDirector.HealWord);
            yield return Ready(p, "ready to spell APPLE");
            Assert.IsTrue(p.Summoner.Summon(TutorialDirector.HealWord), "APPLE fits the free hand");
            yield return null;
            Assert.AreEqual(Index(TutorialStep.Heal), director.StepIndex, "holding the APPLE is not eating it");
            yield return Ready(p, "ready to eat the APPLE");
            p.Combat.Attack();
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.Heal), 5f, "heal step");
            Assert.AreEqual(p.Health.Max, p.Health.Current, 0.01f, "the APPLE healed the punch");
            Assert.IsNull(p.Combat.Weapon, "the eaten APPLE left the hand");
        }

        static IEnumerator SpellTheLampAndSwitchHands(TutorialDirector director, PlayerController p)
        {
            Assert.AreEqual(TutorialStep.Effect, director.Current);
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.EffectWord), 2f, "a LAMP to smash");
            Standing(TutorialDirector.EffectWord).Break();
            p.Inventory.Set(TutorialDirector.EffectWord);
            yield return Ready(p, "ready to spell LAMP");
            Assert.IsTrue(p.Summoner.Summon(TutorialDirector.EffectWord), "LAMP fits the free hand");
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.Effect), 1f, "effect word step");
            Assert.AreEqual(TutorialDirector.EffectWord, p.Combat.Weapon?.word);

            Assert.IsTrue(p.Combat.SelectSlot(1 - p.Combat.ActiveSlot));
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.Hands), 1f, "switch hands step");
            Assert.AreEqual("BAT", p.Combat.Weapon?.word, "switching hands brings the BAT back");
            Assert.AreEqual(TutorialStep.KnockedOut, director.Current);
        }

        static void AssertEverythingWasDropped(PlayerController p)
        {
            Assert.IsTrue(p.IsEliminated);
            Assert.AreEqual(0, p.Inventory.Count, "a knockout drops every letter");
            Assert.IsNull(p.Combat.Weapon, "a knockout drops the item in hand");
            Assert.IsNull(p.Combat.StoredGear, "and the item in the other hand");
            var onTheFloor = Object.FindObjectsByType<HeldWeapon>().Where(g => !g.IsHeld).Select(g => g.word).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "BAT", TutorialDirector.EffectWord }, onTheFloor, "both items stay where the player fell");
        }

        static IEnumerator ComeBack(TutorialDirector director, PlayerController p)
        {
            Assert.AreEqual(Index(TutorialStep.KnockedOut), director.StepIndex, "the step waits for the return");
            yield return TestScenes.WaitUntil(() => !p.IsKnockedOut, 6f, "the tutorial brings a knockout back");
            Assert.AreEqual(p.Health.Max, p.Health.Current, "back at full health");
            Assert.IsNull(p.Combat.Weapon, "back with empty hands");
            Assert.AreEqual(Index(TutorialStep.KnockedOut), director.StepIndex, "a moment to read how you came back");
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.KnockedOut), TutorialDirector.ReturnLinger + 4f, "knocked out step");
            Assert.AreEqual(TutorialStep.Block, director.Current);
        }

        static IEnumerator SpellAPlate(TutorialDirector director, PlayerController p)
        {
            Assert.AreEqual(TutorialStep.Block, director.Current);
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.ShieldWord), 3f, "a PLATE to smash");
            Standing(TutorialDirector.ShieldWord).Break();
            p.Inventory.Set(TutorialDirector.ShieldWord);
            yield return Ready(p, "ready to spell PLATE");
            Assert.IsTrue(p.Summoner.Summon(TutorialDirector.ShieldWord), "the knockout emptied both hands, so PLATE fits");
            Assert.IsNotNull(p.Combat.Weapon?.Shield, "PLATE is a shield");
            yield return null;
            Assert.AreEqual(Index(TutorialStep.Block), director.StepIndex, "holding the PLATE is not blocking");
        }

        IEnumerator CompleteTutorial()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            hud.Follow(p);
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer == p, 1f, "the HUD follows the tutorial seat");
            Assert.AreEqual(0, p.Inventory.Count, "the tutorial starts empty-handed");
            Assert.AreEqual(15, director.StepCount);
            Assert.AreEqual(TutorialSteps.Order.Count, director.StepCount);

            yield return PlayToTheThrow(director, hud, input, p);
            var dummy = director.Dummy;

            yield return TestScenes.WaitUntil(() => HudSays(hud, TutorialSteps.Text(TutorialStep.Place)), 2f, "the place step on the HUD");
            Assert.IsTrue(HudSays(hud, $"{Index(TutorialStep.Place) + 1}/{director.StepCount}"), "the step counter counts the new steps");
            yield return PlaceATable(director, p);
            yield return EatAnApple(director, p);
            Assert.AreEqual("BAT", p.Combat.StoredGear?.word, "placing and eating leave the BAT in the other hand");

            yield return SpellTheLampAndSwitchHands(director, p);

            p.Inventory.Set("LE");
            yield return Vulnerable(p);
            Assert.IsTrue(p.Health.ApplyDamage(Hits.Of(dummy, Vector3.forward, HitSource.Melee, 1000f, 1f)));
            AssertEverythingWasDropped(p);
            yield return ComeBack(director, p);

            yield return SpellAPlate(director, p);
            yield return Vulnerable(p);
            StandInFrontOf(p, dummy);
            input.Next.blockHeld = true;
            yield return TestScenes.WaitUntil(() => p.Combat.IsBlocking, 3f, "the PLATE to come up");
            float before = p.Health.Current;
            p.Health.ApplyDamage(Hits.Melee(dummy, Vector3.back, dummy.Health.Rules.Unarmed, null));
            yield return TestScenes.WaitUntil(() => director.StepIndex > Index(TutorialStep.Block), 3f, "block step");
            input.Next.blockHeld = false;
            Assert.AreEqual(before, p.Health.Current, 0.01f, "a blocked punch does no damage");
            Assert.AreEqual(TutorialStep.Finish, director.Current);

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(TutorialDirector.DummyHealth, dummy.Health.Max, "a short fight: the dummy has less health than a player");
            Assert.AreEqual(dummy.Health.Max, dummy.Health.Current);
            int punches = Mathf.CeilToInt(dummy.Health.Max / p.Health.Rules.Unarmed.Damage) + 1;
            for (int i = 0; i < punches && dummy.Health.IsAlive; i++)
                dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Health.Rules.Unarmed, null));
            Assert.IsTrue(dummy.IsEliminated);
            Assert.IsFalse(TutorialDirector.Done, "only the last step marks the tutorial done");
            yield return TestScenes.WaitUntil(() => director.Finished, 1f, "knockout step");
        }

        static IEnumerator EndCardStartsDibs()
        {
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.WaitUntil(() => director.EndCardShown && hud.ResultShown, 3f, "the end card");
            Assert.AreEqual(Session.TutorialScene, SceneManager.GetActiveScene().name, "the tutorial waits for the player instead of leaving");
            var labels = hud.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToArray();
            CollectionAssert.Contains(labels, TutorialDirector.EndCard().NextLabel);
            CollectionAssert.Contains(labels, "Back to the lobby");
            hud.ConfirmResult();
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.AreEqual(TutorialDirector.PlayNextMode, Match.Mode);
            var dibs = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, dibs.HumanCount, "the tutorial seat came along");
            yield return TestScenes.WaitUntil(() => dibs.Players.Count > dibs.HumanCount, 3f, "bots to play against");
        }

        [UnityTest]
        public IEnumerator TutorialBringsBackTheTableWhenItsLettersRunOut()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var input = new ScriptedBinding();
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Join(input);
            yield return ReachTheTable(director, input);
            Standing(TutorialDirector.FirstFurniture).Break();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 3, 1f, "smash step");
            yield return null;
            TilePool.Instance.ReleaseAll();
            p.Inventory.Set("");
            Assert.IsNull(Standing(TutorialDirector.FirstFurniture));
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.FirstFurniture), 2.5f, "a new TABLE once B, A and T are gone");
            Assert.AreEqual(3, director.StepIndex, "the step waits for the new letters");
        }

        static IEnumerator StartTutorialAtTheThrow(System.Action<PlayerJoinManager, TutorialDirector, ScriptedBinding, PlayerController> started)
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            hud.Follow(p);
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer == p, 1f, "the HUD follows the tutorial seat");
            started(joins, director, input, p);
            yield return PlayToTheThrow(director, hud, input, p);
        }

        [UnityTest]
        public IEnumerator TheNewTutorialStepsGiveUpAndTheOldOnesStillWait()
        {
            TutorialDirector director = null;
            yield return StartTutorialAtTheThrow((joins, d, input, p) => director = d);
            Assert.AreEqual(TutorialSteps.GiveUpSeconds, director.GiveUpSeconds);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(TutorialStep.Place, director.Current, "a new step waits a good while before it gives up");

            director.GiveUpSeconds = 0.5f;
            yield return TestScenes.WaitUntil(() => director.Current == TutorialStep.Effect, 10f, "the place and heal steps to give up");
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(TutorialStep.Effect, director.Current, "the old steps still wait for the player");
            Assert.IsFalse(director.GiveUp(), "only the new steps can be given up");
            Assert.IsFalse(director.Finished);
        }

        [UnityTest]
        public IEnumerator TheTutorialDummyJabsOnlyWhenAStepAsksAndOnlyUntilSomeoneIsHurt()
        {
            TutorialDirector director = null;
            PlayerController p = null;
            yield return StartTutorialAtTheThrow((joins, d, input, player) => { director = d; p = player; });
            var dummy = director.Dummy;
            yield return Vulnerable(p);

            StandInFrontOf(p, dummy);
            yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(p.Health.Max, p.Health.Current, "the dummy leaves players alone until a step asks for punches");

            Assert.IsTrue(director.GiveUp());
            Assert.AreEqual(TutorialStep.Heal, director.Current);
            StandInFrontOf(p, dummy);
            yield return TestScenes.WaitUntil(() => p.Health.Current < p.Health.Max, 10f, "the dummy's jab");
            Assert.AreEqual(p.Health.Max - TutorialDirector.DummyJab, p.Health.Current, 0.01f, "a little damage");
            Assert.IsTrue(p.Health.IsAlive);

            float hurt = p.Health.Current;
            yield return TestScenes.WaitUntil(() => p.CanAct, 3f, "the jab's stagger to end");
            StandInFrontOf(p, dummy);
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(hurt, p.Health.Current, 0.01f, "one jab is enough: the dummy waits while someone is hurt");
            Assert.AreEqual(TutorialStep.Heal, director.Current, "being hurt is only half of the heal step");
        }

        [UnityTest]
        public IEnumerator TheTutorialDummyKnocksAPlayerOutThenItsPunchIsBlocked()
        {
            TutorialDirector director = null;
            PlayerController p = null;
            ScriptedBinding input = null;
            yield return StartTutorialAtTheThrow((joins, d, binding, player) => { director = d; input = binding; p = player; });
            var dummy = director.Dummy;
            Assert.IsTrue(director.GiveUp());
            Assert.IsTrue(director.GiveUp());
            yield return SpellTheLampAndSwitchHands(director, p);

            p.Inventory.Set("LE");
            yield return Vulnerable(p);
            StandInFrontOf(p, dummy);
            yield return TestScenes.WaitUntil(() => p.IsEliminated, 10f, "the dummy's knockout punch");
            AssertEverythingWasDropped(p);
            yield return ComeBack(director, p);

            yield return SpellAPlate(director, p);
            yield return Vulnerable(p);
            StandInFrontOf(p, dummy);
            yield return TestScenes.WaitUntil(() => p.Health.Current < p.Health.Max, 10f, "a jab on the lowered PLATE");
            Assert.AreEqual(TutorialStep.Block, director.Current, "a lowered PLATE stops nothing and finishes nothing");

            yield return TestScenes.WaitUntil(() => p.CanAct, 3f, "the jab's stagger to end");
            StandInFrontOf(p, dummy);
            input.Next.blockHeld = true;
            yield return TestScenes.WaitUntil(() => director.Current == TutorialStep.Finish, 10f, "the dummy's punch on the raised PLATE");
            input.Next.blockHeld = false;
            Assert.IsTrue(p.Health.IsAlive);
        }

        [UnityTest]
        public IEnumerator AnySeatCanFinishANewTutorialStep()
        {
            TutorialDirector director = null;
            PlayerJoinManager joins = null;
            PlayerController p = null;
            yield return StartTutorialAtTheThrow((j, d, input, player) => { joins = j; director = d; p = player; });
            var second = joins.Join(new ScriptedBinding());
            Assert.IsNotNull(second, "a second seat joins the tutorial");
            yield return Ready(second, "the second seat");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(TutorialStep.Place, director.Current, "joining finishes nothing");

            second.Inventory.Set(TutorialDirector.PlaceWord);
            yield return Ready(second, "the second seat ready to spell TABLE");
            Assert.IsTrue(second.Summoner.Summon(TutorialDirector.PlaceWord));
            yield return Ready(second, "the second seat ready to place");
            Assert.IsTrue(second.Combat.DeployHeld());
            yield return TestScenes.WaitUntil(() => director.Current == TutorialStep.Heal, 5f, "the second seat's TABLE to finish the place step");
            Assert.AreEqual(0, DeployedGear.CountFor(p), "the first seat placed nothing");

            yield return Vulnerable(second);
            Assert.IsTrue(second.Health.ApplyDamage(Hits.Of(director.Dummy, Vector3.forward, HitSource.Melee, TutorialDirector.DummyJab, 0f, hitStun: 0f)));
            second.Inventory.Set(TutorialDirector.HealWord);
            yield return Ready(second, "the second seat ready to spell APPLE");
            Assert.IsTrue(second.Summoner.Summon(TutorialDirector.HealWord));
            yield return Ready(second, "the second seat ready to eat");
            second.Combat.Attack();
            yield return TestScenes.WaitUntil(() => director.Current == TutorialStep.Effect, 5f, "the second seat's APPLE to finish the heal step");
            Assert.AreEqual(p.Health.Max, p.Health.Current, "the first seat was never hurt");
            Assert.AreEqual(second.Health.Max, second.Health.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator BatSummonWaitsForDodgeCompletionWithoutSpendingLetters()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var input = new ScriptedBinding();
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Join(input);
            yield return null;
            p.Inventory.Set("BAT");
            var recipe = GameAssets.I.words.Find("BAT");
            Assert.IsNotNull(recipe);
            Assert.IsTrue(SummonEffects.CanApply(p, recipe), "BAT is admitted before the dodge");

            bool observed = false, dodging = false, canAct = false, canApply = true;
            bool summonedDuringDodge = true, holdingAfterAttempt = true;
            char[] lettersAfterAttempt = null;
            int reservedAfterAttempt = -1, summons = 0;
            void OnSummoned(string word) { if (word == "BAT") summons++; }
            void OnDodged(PlayerController player)
            {
                observed = true;
                dodging = player.IsDodging;
                canAct = player.CanAct;
                canApply = SummonEffects.CanApply(player, recipe);
                summonedDuringDodge = player.Summoner.Summon("BAT");
                lettersAfterAttempt = player.Inventory.Letters.ToArray();
                reservedAfterAttempt = player.Inventory.ReservedCount;
                holdingAfterAttempt = player.Combat.IsHolding;
            }

            p.Dodged += OnDodged;
            p.Summoner.Summoned += OnSummoned;
            try
            {
                input.Next.dodge = true;
                yield return TestScenes.WaitUntil(() => observed, 2f, "dodge start");
                Assert.IsTrue(dodging);
                Assert.IsTrue(canAct, "the dodge guard is the admission blocker");
                Assert.IsFalse(canApply);
                Assert.IsFalse(summonedDuringDodge);
                CollectionAssert.AreEqual("BAT".ToCharArray(), lettersAfterAttempt);
                Assert.AreEqual(0, reservedAfterAttempt);
                Assert.IsFalse(holdingAfterAttempt);
                Assert.AreEqual(0, summons);

                yield return TestScenes.WaitUntil(() => !p.IsDodging, 2f, "natural dodge completion");
                CollectionAssert.AreEqual("BAT".ToCharArray(), p.Inventory.Letters);
                Assert.IsTrue(SummonEffects.CanApply(p, recipe));
                Assert.IsTrue(p.Summoner.Summon("BAT"));
                Assert.AreEqual("BAT", p.Combat.Weapon?.word);
                Assert.AreEqual(0, p.Inventory.Count);
                Assert.AreEqual(1, summons);
            }
            finally
            {
                p.Dodged -= OnDodged;
                p.Summoner.Summoned -= OnSummoned;
            }
        }
    }
}
