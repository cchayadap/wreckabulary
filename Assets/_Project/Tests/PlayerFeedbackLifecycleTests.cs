using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class PlayerFeedbackLifecycleTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerController Spawn()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        static IEnumerator PrepareReservation(PlayerController player)
        {
            player.Inventory.Set("BAT");
            World.ClearTransient();
            yield return null;
            Assert.IsNull(GameObject.Find("Transient"));
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BAT")));
            Assert.AreEqual(3, player.Inventory.ReservedCount);
        }

        [UnityTest]
        public IEnumerator DisablingAnActorRefundsLettersWithoutCreatingEffects() => VerifyDisabledCraft(true);

        [UnityTest]
        public IEnumerator DisablingOnlyTheSummonerRefundsLettersWithoutCreatingEffects() => VerifyDisabledCraft(false);

        static IEnumerator VerifyDisabledCraft(bool wholeActor)
        {
            var player = Spawn();
            yield return PrepareReservation(player);
            int changes = 0;
            player.Inventory.Changed += () => changes++;

            if (wholeActor) player.gameObject.SetActive(false);
            else player.Summoner.enabled = false;

            Assert.IsFalse(player.Summoner.IsCrafting);
            Assert.AreEqual(0, player.Inventory.ReservedCount);
            CollectionAssert.AreEquivalent("BAT".ToCharArray(), player.Inventory.Letters);
            Assert.AreEqual(3, changes, "Gameplay observers still receive each refunded letter.");
            Assert.AreEqual(1f, player.MoveScale);
            Assert.IsNull(GameObject.Find("Transient"), "Teardown refunds must not recreate cosmetic scene objects.");

            if (wholeActor) player.gameObject.SetActive(true);
            else player.Summoner.enabled = true;

            Assert.IsTrue(player.Inventory.TryAdd('Z'));
            Assert.IsNotNull(GameObject.Find("Transient"), "Normal pickup feedback resumes after re-enabling.");
            Assert.IsTrue(Object.FindObjectsByType<FeedbackBurst>().Any(cue => cue.name == "Pickup_Ring cue"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LoadingAnotherSceneWithAnActiveReservationLeavesNoLateEffects()
        {
            var player = Spawn();
            yield return PrepareReservation(player);
            yield return TestScenes.Reset();
            Assert.IsFalse(player);
            Assert.IsNull(GameObject.Find("Transient"), "Unloading an active craft must not create effects in the new scene.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
