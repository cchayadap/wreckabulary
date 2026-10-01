using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class PlayerHealthLifecycleTests
    {
        PlayerHealth health;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            health = new GameObject("Health lifecycle test player").AddComponent<PlayerHealth>();
            health.UseRules(new GameRules());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
        }

        [Test]
        public void OlderBubbleCleanupCannotClearItsReplacement()
        {
            int old = health.GiveOwnedBubble(35f, 10f);
            int current = health.GiveOwnedBubble(20f, 10f);

            health.ClearOwnedBubble(old);

            Assert.IsFalse(health.OwnsBubble(old));
            Assert.IsTrue(health.OwnsBubble(current));
            Assert.AreEqual(20f, health.Bubble);
            health.ClearOwnedBubble(current);
            Assert.AreEqual(0f, health.Bubble);
            Assert.AreEqual(0, health.Model.BubbleUntil, 1e-9);
        }

        [Test]
        public void LegacyBubbleGrantReplacesOwnership()
        {
            int old = health.GiveOwnedBubble(35f, 10f);
            health.GiveBubble(20f, 10f);

            health.ClearOwnedBubble(old);

            Assert.IsFalse(health.OwnsBubble(old));
            Assert.AreEqual(20f, health.Bubble);
            health.ClearBubble();
            Assert.AreEqual(0f, health.Bubble);
        }

        [Test]
        public void ExpiredAndDepletedBubblesLoseOwnership()
        {
            int expired = health.GiveOwnedBubble(35f, 0f);
            Assert.IsFalse(health.OwnsBubble(expired));

            int depleted = health.GiveOwnedBubble(35f, 10f);
            health.Model.ApplyHit(HitInfo.Hazard(35f), Time.timeAsDouble);
            Assert.IsFalse(health.OwnsBubble(depleted));
        }

        [Test]
        public void OlderShieldCleanupCannotClearItsReplacement()
        {
            int old = health.GiveTimedShield(10f);
            int current = health.GiveTimedShield(20f);
            float deadline = health.FrontBlockUntil;

            health.ClearTimedShield(old);

            Assert.IsFalse(health.OwnsTimedShield(old));
            Assert.IsTrue(health.OwnsTimedShield(current));
            Assert.AreEqual(deadline, health.FrontBlockUntil);
            health.ClearTimedShield(current);
            Assert.IsFalse(health.OwnsTimedShield(current));
            Assert.AreEqual(0f, health.FrontBlockUntil);
        }

        [Test]
        public void LegacyShieldDeadlineReplacesOwnership()
        {
            int old = health.GiveTimedShield(10f);
            float deadline = Time.time + 20f;
            health.FrontBlockUntil = deadline;

            health.ClearTimedShield(old);

            Assert.IsFalse(health.OwnsTimedShield(old));
            Assert.AreEqual(deadline, health.FrontBlockUntil);
        }

        [Test]
        public void ShieldTokenStillBlocksFrontHits()
        {
            health.GiveTimedShield(10f);
            var hit = HitInfo.From(1, 1, new MeleeStats { Damage = 8f }, null, 0f, -1f);

            Assert.IsFalse(health.ApplyDamage(hit));
            Assert.AreEqual(100f, health.Current);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacingTheHealthModelInvalidatesOldTokens(bool resetRound)
        {
            int oldBubble = health.GiveOwnedBubble(35f, 10f);
            int oldShield = health.GiveTimedShield(10f);
            if (resetRound) health.ResetForRound();
            else health.Init();
            Assert.IsFalse(health.OwnsBubble(oldBubble));
            Assert.IsFalse(health.OwnsTimedShield(oldShield));

            int currentBubble = health.GiveOwnedBubble(20f, 10f);
            int currentShield = health.GiveTimedShield(20f);
            health.ClearOwnedBubble(oldBubble);
            health.ClearTimedShield(oldShield);

            Assert.IsTrue(health.OwnsBubble(currentBubble));
            Assert.IsTrue(health.OwnsTimedShield(currentShield));
            Assert.AreEqual(20f, health.Bubble);
        }
    }
}
