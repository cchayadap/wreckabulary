using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class StaminaTests
    {
        [Test]
        public void DodgingConsumesStaminaAndRecoveryHonoursDelayAndCooldown()
        {
            var rules = new GameRules();
            var health = new HealthModel(rules, 0, 0);
            Assert.AreEqual(100, health.Stamina);
            Assert.IsTrue(health.Dodge(10));
            Assert.AreEqual(70, health.Stamina);
            Assert.IsFalse(health.Dodge(10.5));
            health.Tick(11);
            Assert.AreEqual(70, health.Stamina);
            health.Tick(11.5);
            Assert.AreEqual(80, health.Stamina);
            Assert.IsTrue(health.Dodge(11.5));
            Assert.AreEqual(50, health.Stamina);
            health.Tick(11.5);
            Assert.AreEqual(50, health.Stamina, "Repeated ticks during pause cannot regenerate stamina.");
            health.Tick(20);
            Assert.AreEqual(100, health.Stamina);
        }

        [Test]
        public void ExhaustionBlocksDodgingAndDeathDoesNotBankRecovery()
        {
            var rules = new GameRules { DodgeCooldown = 0, StaminaRegenDelay = 0 };
            var health = new HealthModel(rules, 0, 0);
            Assert.IsTrue(health.Dodge(0)); Assert.IsTrue(health.Dodge(0)); Assert.IsTrue(health.Dodge(0));
            Assert.IsFalse(health.Dodge(0)); Assert.AreEqual(10, health.Stamina);
            health.ApplyHit(HitInfo.Hazard(1000), 1);
            health.Tick(50); Assert.AreEqual(10, health.Stamina);
            Assert.IsFalse(health.CanDodge(60));
            health.Respawn(60); Assert.AreEqual(100, health.Stamina);
            Assert.IsTrue(health.Dodge(60));
            health.Tick(60.5); Assert.AreEqual(80, health.Stamina);
        }

        [Test]
        public void InvalidStaminaBalanceIsRejected()
        {
            var rules = new GameRules { MaxStamina = 20, DodgeStamina = 30, StaminaRegen = -1 };
            Assert.IsTrue(rules.Validate().Any(error => error.Contains("stamina")));
        }
    }
}
