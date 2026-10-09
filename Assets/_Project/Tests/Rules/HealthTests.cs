using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class HealthTests
    {
        GameRules dibs, duos;
        ItemCatalogue catalogue;

        [SetUp]
        public void Load()
        {
            dibs = TestData.RulesFor("Dibs");
            duos = TestData.RulesFor("Duos");
            catalogue = TestData.Catalogue();
        }

        HitInfo Hit(string item, float fromX, float fromZ, int attacker = 1, int team = 1)
        {
            var stats = item == null ? dibs.Unarmed : catalogue.Get(item).Melee;
            return HitInfo.From(attacker, team, stats, item, -fromX, -fromZ);
        }

        [Test]
        public void PlayersHave100HealthAndTakeDamage()
        {
            var h = new HealthModel(dibs, 0, 0);
            Assert.AreEqual(100f, h.Current);
            var r = h.ApplyHit(Hit("BAT", 0, 1), 10);
            Assert.IsTrue(r.Landed);
            Assert.AreEqual(14f, r.Damage);
            Assert.AreEqual(86f, h.Current);
        }

        [Test]
        public void ThePlateBlocksOnlyFromTheFront()
        {
            var plate = catalogue.Get("PLATE").Shield;
            var h = new HealthModel(dibs, 0, 0);
            h.SetFacing(0, 1);
            h.SetBlocking(plate);

            var front = h.ApplyHit(Hit("BLADE", 0, 2), 10);
            Assert.IsTrue(front.Blocked);
            Assert.AreEqual(0f, front.Damage);
            Assert.AreEqual(24f, front.BlockedDamage, "the PLATE wears by what it stopped");
            Assert.AreEqual(0f, front.HitStun, "a blocked hit doesn't stagger");

            var edge = h.ApplyHit(Hit("BLADE", 2, 1.9f), 12);
            Assert.IsTrue(edge.Blocked, "about 46 degrees off centre is inside the 120 degree arc");

            var side = h.ApplyHit(Hit("BLADE", 2, 0), 14);
            Assert.IsFalse(side.Blocked, "90 degrees off centre is outside the arc");
            var behind = h.ApplyHit(Hit("BLADE", 0, -2), 16);
            Assert.IsFalse(behind.Blocked);
            Assert.AreEqual(100f - 48f, h.Current);
        }

        [Test]
        public void HazardsCantBeBlocked()
        {
            var h = new HealthModel(dibs, 0, 0);
            h.SetFacing(0, 1);
            h.SetBlocking(catalogue.Get("PLATE").Shield);
            var r = h.ApplyHit(HitInfo.Hazard(8), 10);
            Assert.IsFalse(r.Blocked);
            Assert.AreEqual(8f, r.Damage);
        }

        [Test]
        public void StunCantChain()
        {
            var h = new HealthModel(dibs, 0, 0);
            var first = h.ApplyHit(Hit("TABLE", 0, 1), 10);
            Assert.AreEqual(dibs.HitStunMax, first.HitStun, 1e-6, "the TABLE's 0.35 s stun is at the cap");
            var second = h.ApplyHit(Hit("BAT", 0, 1), 10.5);
            Assert.AreEqual(0f, second.HitStun, "stagger-immune for 1 s");
            Assert.AreEqual(14f, second.Damage, "immunity stops the stun, not the damage");
            var third = h.ApplyHit(Hit("BAT", 0, 1), 11.0);
            Assert.Greater(third.HitStun, 0f, "immunity has worn off");
        }

        [Test]
        public void FoamRefreshesInsteadOfStacking()
        {
            var foam = catalogue.Get("FOAM").Use;
            var h = new HealthModel(dibs, 0, 0);
            h.GiveBubble(foam.Amount, 10 + foam.Seconds);
            h.GiveBubble(foam.Amount, 12 + foam.Seconds);
            Assert.AreEqual(35f, h.Bubble, "two FOAMs are not 70");

            var r = h.ApplyHit(Hit("BLADE", 0, 1), 13);
            Assert.AreEqual(24f, r.Absorbed);
            Assert.AreEqual(0f, r.Damage);
            var r2 = h.ApplyHit(Hit("BLADE", 0, 1), 15);
            Assert.AreEqual(11f, r2.Absorbed);
            Assert.AreEqual(13f, r2.Damage);
            h.GiveBubble(foam.Amount, 20);
            h.Tick(21);
            Assert.AreEqual(0f, h.Bubble, "the bubble pops when its time is up");
        }

        [Test]
        public void DodgeGivesBriefInvulnerabilityWithACooldown()
        {
            var h = new HealthModel(dibs, 0, 0);
            Assert.IsTrue(h.Dodge(10));
            Assert.AreEqual(HitIgnored.Invulnerable, h.ApplyHit(Hit("BAT", 0, 1), 10.1).Ignored);
            Assert.IsTrue(h.ApplyHit(Hit("BAT", 0, 1), 10.2).Landed, "only the first 0.15 s");
            Assert.IsFalse(h.Dodge(11));
            Assert.IsTrue(h.Dodge(11.5));
        }

        [Test]
        public void SpawnProtectionThenFullHealth()
        {
            var h = new HealthModel(dibs, 0, 0);
            h.ApplyHit(Hit("BLADE", 0, 1), 1);
            h.Respawn(50);
            Assert.AreEqual(100f, h.Current);
            Assert.AreEqual(HitIgnored.Invulnerable, h.ApplyHit(Hit("BAT", 0, 1), 51.9).Ignored);
            Assert.IsTrue(h.ApplyHit(Hit("BAT", 0, 1), 52).Landed);
        }

        [Test]
        public void InFreeForAllZeroHealthEliminates()
        {
            var h = new HealthModel(dibs, 0, 0);
            HitResult last = default;
            for (int i = 0; i < 5; i++) last = h.ApplyHit(Hit("BLADE", 0, 1), 10 + i * 2);
            Assert.IsTrue(last.BecameEliminated);
            Assert.AreEqual(LifeState.Eliminated, h.State);
            Assert.AreEqual(0f, h.Current);
            Assert.AreEqual(4f, last.Damage, "damage never goes below zero health");
            Assert.AreEqual(HitIgnored.Eliminated, h.ApplyHit(Hit("BAT", 0, 1), 30).Ignored);
        }

        [Test]
        public void InDuosATeammateCanReviveTheDowned()
        {
            var h = new HealthModel(duos, 0, 0);
            var ko = h.ApplyHit(new HitInfo { Damage = 150, AttackerId = 1, AttackerTeam = 1, Blockable = true }, 10);
            Assert.IsTrue(ko.BecameDowned);
            Assert.AreEqual(LifeState.Downed, h.State);
            Assert.AreEqual(30, h.BleedOutAt, 1e-9, "20 s the first time");
            Assert.AreEqual(HitIgnored.Downed, h.ApplyHit(Hit("BAT", 0, 1), 11).Ignored);

            Assert.IsFalse(h.BeginRevive(1, 1, 12), "an enemy can't revive you");
            Assert.IsTrue(h.BeginRevive(2, 0, 12));
            Assert.IsFalse(h.BeginRevive(3, 0, 12), "one reviver at a time");
            Assert.IsFalse(h.TryFinishRevive(2, 14.9));
            Assert.IsTrue(h.TryFinishRevive(2, 15));
            Assert.AreEqual(LifeState.Alive, h.State);
            Assert.AreEqual(30f, h.Current);
            Assert.AreEqual(HitIgnored.Invulnerable, h.ApplyHit(Hit("BAT", 0, 1), 15.5).Ignored, "a moment to get up");
        }

        [Test]
        public void EachDowningBleedsOutFaster()
        {
            var h = new HealthModel(duos, 0, 0);
            var kill = new HitInfo { Damage = 500, AttackerId = 1, AttackerTeam = 1 };
            h.ApplyHit(kill, 0);
            Assert.AreEqual(20, h.BleedOutAt, 1e-9);
            h.BeginRevive(2, 0, 1);
            h.TryFinishRevive(2, 4);
            h.ApplyHit(kill, 10);
            Assert.AreEqual(22, h.BleedOutAt, 1e-9, "12 s the second time");
            h.BeginRevive(2, 0, 11);
            h.CancelRevive(2);
            Assert.IsFalse(h.TryFinishRevive(2, 20), "letting go cancels the revive");
            Assert.IsFalse(h.Tick(21.9));
            Assert.IsTrue(h.Tick(22));
            Assert.AreEqual(LifeState.Eliminated, h.State);
            h.ResetForRound(100);
            Assert.AreEqual(0, h.TimesDowned);
            Assert.AreEqual(100f, h.Current);
        }

        [Test]
        public void FriendlyFireFollowsTheMode()
        {
            var teammateHit = Hit("BAT", 0, 1, attacker: 2, team: 0);
            Assert.AreEqual(HitIgnored.FriendlyFire, new HealthModel(duos, 0, 0).ApplyHit(teammateHit, 10).Ignored);
            Assert.IsTrue(new HealthModel(duos, 0, 0).ApplyHit(Hit("BAT", 0, 1, attacker: 1, team: 1), 10).Landed);
            Assert.IsTrue(new HealthModel(duos, 0, 0).ApplyHit(HitInfo.Hazard(5), 10).Landed, "the Movers hurt everyone");
            var selfBomb = new HitInfo { Damage = 20, AttackerId = 0, AttackerTeam = 0, Source = HitSource.Explosion };
            Assert.IsTrue(new HealthModel(duos, 0, 0).ApplyHit(selfBomb, 10).Landed, "your own BOMB hurts you");
        }

        [Test]
        public void HealingNeverExceedsMax()
        {
            var h = new HealthModel(dibs, 0, 0);
            h.ApplyHit(Hit("BAT", 0, 1), 10);
            Assert.AreEqual(14f, h.Heal(50));
            Assert.AreEqual(100f, h.Current);
        }

        [TestCase(0f, 1f, 0f, 1f, 120f, true)]
        [TestCase(0f, 1f, 1f, 1f, 120f, true)]
        [TestCase(0f, 1f, 1f, 0f, 120f, false)]
        [TestCase(0f, 1f, 0f, -1f, 120f, false)]
        [TestCase(0f, 1f, 0f, 0f, 120f, false)]
        [TestCase(0f, 1f, 1f, 0f, 180f, true)]
        public void FrontArc(float fx, float fz, float tx, float tz, float arc, bool expected)
        {
            Assert.AreEqual(expected, Geometry.InFrontArc(fx, fz, tx, tz, arc));
        }
    }
}
