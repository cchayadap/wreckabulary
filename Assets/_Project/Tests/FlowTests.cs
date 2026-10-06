using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>The house, the typewriter, carrying roommates between scenes, and the tutorial.</summary>
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
        public IEnumerator TypewriterIsUsedWithGrabAndStepsWithUpDown()
        {
            yield return TestScenes.Load(Session.HubScene);
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
            yield return TestScenes.Load(Session.TutorialScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            Assert.AreEqual(0, p.Inventory.Count, "the tutorial starts empty-handed");

            // 1. Walk around.
            input.Next.move = new Vector2(1f, 0f);
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 1, 3f, "walk step");
            input.Next.move = Vector2.zero;

            input.Next.jump = true;
            yield return new WaitForSeconds(0.8f);
            input.Next.dodge = true;
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 2, 2f, "jump and dodge step");

            yield return TestScenes.WaitUntil(() => Object.FindObjectsByType<Smashable>().Any(s => s.Word == "BAT"), 2f, "BAT box");
            Object.FindObjectsByType<Smashable>().First(s => s.Word == "BAT").Break();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 3, 1f, "smash step");

            p.Inventory.Set("BAT");
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 4, 1f, "collect step");
            yield return TestScenes.WaitUntil(() => !p.IsDodging, 2f, "dodge to finish before spelling BAT");
            Assert.IsTrue(p.Summoner.Summon("BAT"));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 5, 1f, "spell step");

            var dummy = director.Dummy;
            Assert.AreEqual("BAT", p.Combat.Weapon?.word);
            Assert.IsTrue(dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Combat.Weapon.Stats, "BAT")));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 6, 1f, "hit step");

            var chair = Object.FindObjectsByType<Smashable>().First(s => s.Word == "CHAIR");
            yield return new WaitForSeconds(0.3f);
            p.Combat.ResetForRound(); // put the BAT away so the hand is free
            p.Respawn(chair.transform.position + Vector3.back * 1.2f);
            p.FaceTowards(Vector3.forward);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(p.Combat.TryGrab(), "grab the chair");
            p.Combat.Throw();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 7, 1f, "throw step");

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(Match.Rules.MaxHealth, dummy.Health.Current);
            for (int i = 0; i < 20 && dummy.Health.IsAlive; i++)
                dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Health.Rules.Unarmed, null));
            Assert.IsTrue(dummy.IsEliminated);
            yield return TestScenes.WaitUntil(() => director.Finished, 1f, "knockout step");
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
