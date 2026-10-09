using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class HealthLifecycleTests
    {
        [Test]
        public void RespawnResetsDodgeCooldownAndRetainsSpawnProtection()
        {
            var rules = new GameRules();
            var health = new HealthModel(rules, 0, 0);
            Assert.IsTrue(health.Dodge(10));
            Assert.IsFalse(health.CanDodge(10.1));

            health.Respawn(10.1);

            Assert.IsTrue(health.CanDodge(10.1), "a previous life cannot consume the new life's dodge");
            Assert.IsTrue(health.Dodge(10.1));
            Assert.AreEqual(10.1 + rules.SpawnProtectionSeconds, health.InvulnerableUntil, 1e-9);
            Assert.AreEqual(100f, health.Current);
        }

        [Test]
        public void RepeatedBeginReviveFromTheSameTeammatePreservesProgress()
        {
            var rules = new GameRules { DownedEnabled = true, TeamSize = 2 };
            var health = new HealthModel(rules, 0, 0);
            health.ApplyHit(HitInfo.Hazard(100f), 10);

            Assert.IsTrue(health.BeginRevive(1, 0, 11));
            Assert.IsTrue(health.BeginRevive(1, 0, 12));
            Assert.IsTrue(health.BeginRevive(1, 0, 13.9));
            Assert.AreEqual(11, health.ReviveStartedAt, 1e-9);
            Assert.IsFalse(health.BeginRevive(2, 0, 13.9), "another reviver cannot take over an active revive");
            Assert.IsTrue(health.TryFinishRevive(1, 14));
            Assert.AreEqual(rules.ReviveHealth, health.Current);
        }

        [Test]
        public void CancelledReviveRequiresTheFullHoldAgain()
        {
            var rules = new GameRules { DownedEnabled = true, TeamSize = 2 };
            var health = new HealthModel(rules, 0, 0);
            health.ApplyHit(HitInfo.Hazard(100f), 10);
            health.BeginRevive(1, 0, 11);
            health.CancelRevive(1);

            Assert.IsTrue(health.BeginRevive(1, 0, 13));
            Assert.IsFalse(health.TryFinishRevive(1, 14));
            Assert.IsTrue(health.TryFinishRevive(1, 16));
        }

        [Test]
        public void BubbleExpiresAtItsDeadlineAndClearsBothFields()
        {
            var health = new HealthModel(new GameRules(), 0, 0);
            health.GiveBubble(35f, 10);
            health.Tick(9.99);
            Assert.AreEqual(35f, health.Bubble);

            health.Tick(10);

            Assert.AreEqual(0f, health.Bubble);
            Assert.AreEqual(0, health.BubbleUntil, 1e-9);
        }

        [Test]
        public void HitAtBubbleDeadlineDoesNotAbsorbOrLeaveStaleProtection()
        {
            var health = new HealthModel(new GameRules(), 0, 0);
            health.GiveBubble(35f, 10);

            var hit = health.ApplyHit(HitInfo.Hazard(8f), 10);

            Assert.AreEqual(0f, hit.Absorbed);
            Assert.AreEqual(8f, hit.Damage);
            Assert.AreEqual(0f, health.Bubble);
            Assert.AreEqual(0, health.BubbleUntil, 1e-9);
        }

        [Test]
        public void ClearBubbleMakesTheNextHitDamageHealth()
        {
            var health = new HealthModel(new GameRules(), 0, 0);
            health.GiveBubble(35f, 20);
            health.ClearBubble();

            var hit = health.ApplyHit(HitInfo.Hazard(8f), 10);

            Assert.AreEqual(8f, hit.Damage);
            Assert.AreEqual(0f, hit.Absorbed);
            Assert.AreEqual(0, health.BubbleUntil, 1e-9);
        }

        [Test]
        public void DepletedBubbleClearsItsDeadline()
        {
            var health = new HealthModel(new GameRules(), 0, 0);
            health.GiveBubble(35f, 20);

            var hit = health.ApplyHit(HitInfo.Hazard(40f), 10);

            Assert.AreEqual(35f, hit.Absorbed);
            Assert.AreEqual(5f, hit.Damage);
            Assert.AreEqual(0f, health.Bubble);
            Assert.AreEqual(0, health.BubbleUntil, 1e-9);
        }

        [Test]
        public void RespawnClearsPriorLifeProtectionAndReviveDeadlines()
        {
            var rules = new GameRules { DownedEnabled = true, TeamSize = 2 };
            var health = new HealthModel(rules, 0, 0);
            health.ApplyHit(HitInfo.Hazard(100f), 10);
            health.BeginRevive(1, 0, 11);
            health.GiveBubble(35f, 100);

            health.Respawn(12);

            Assert.AreEqual(LifeState.Alive, health.State);
            Assert.AreEqual(-1, health.ReviverId);
            Assert.AreEqual(0, health.ReviveStartedAt, 1e-9);
            Assert.AreEqual(0, health.BleedOutAt, 1e-9);
            Assert.AreEqual(0f, health.Bubble);
            Assert.AreEqual(0, health.BubbleUntil, 1e-9);
            Assert.IsFalse(health.TryFinishRevive(1, 20));
            Assert.IsFalse(health.Tick(100), "a previous life's bleed-out cannot eliminate the new life");
        }
    }
}
