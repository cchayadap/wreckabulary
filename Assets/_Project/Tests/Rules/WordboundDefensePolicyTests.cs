using System;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundDefensePolicyTests
    {
        static ShieldStats Stats(string recipe) => TestData.Catalogue().Get(recipe).Shield;
        static WorldBodyState Player() => new WorldBodyState("player:0", WorldBodyKind.Player, default, sourceId: "a");

        [TestCase("PLATE", 8, .7f)]
        [TestCase("SHIELD", 14, .55f)]
        public void ExistingCatalogueRaiseAndMovementAreAppliedOnlyInsideTheRaisedLease(string recipe, int raise, float move)
        {
            var stats = Stats(recipe);
            var defense = new MatchDefenseState("item:1", 10, 40);
            Assert.AreEqual(raise, WordboundDefensePolicy.RaiseTicks(stats));
            Assert.IsFalse(WordboundDefensePolicy.Raised(defense, stats, 10 + raise - 1));
            Assert.AreEqual(1, WordboundDefensePolicy.MoveScale(defense, stats, 10 + raise - 1));
            Assert.IsTrue(WordboundDefensePolicy.Raised(defense, stats, 10 + raise));
            Assert.AreEqual(move, WordboundDefensePolicy.MoveScale(defense, stats, 10 + raise));
            Assert.IsFalse(WordboundDefensePolicy.Raised(defense, stats, 40));
            Assert.AreEqual(1, WordboundDefensePolicy.MoveScale(defense, stats, 40));
        }

        [TestCase("PLATE", 0, 1, true)]
        [TestCase("PLATE", 0, -1, false)]
        [TestCase("PLATE", 1, 0, false)]
        [TestCase("SHIELD", 0, 1, true)]
        [TestCase("SHIELD", 0, -1, true)]
        [TestCase("SHIELD", 1, 0, true)]
        public void CatalogueArcControlsBlockDamageWearAndImpulse(string recipe, float x, float z, bool blocks)
        {
            var defense = new MatchDefenseState("item:1", 1, 31);
            var result = WordboundDefensePolicy.Resolve(defense, Stats(recipe), Player(), new WorldVector(x, 0, z), 20, 20);
            Assert.AreEqual(blocks, result.Blocked);
            Assert.AreEqual(blocks ? 0 : 20, result.Damage);
            Assert.AreEqual(blocks ? 20 : 0, result.BlockedDamage);
            Assert.AreEqual(blocks ? .3f : 1, result.KnockbackScale);
            Assert.AreEqual(blocks ? 0 : .35f, result.VerticalKnockbackScale);
        }

        [Test]
        public void BlockUsesCanonicalFacingAndIncomingSideRatherThanRemoteAttackerIdentity()
        {
            var rotated = new WorldBodyState("player:0", WorldBodyKind.Player, default,
                rotationXyz: new WorldVector(0, .70710677f, 0), rotationW: .70710677f, sourceId: "a");
            var defense = new MatchDefenseState("item:1", 1, 31);
            Assert.IsTrue(WordboundDefensePolicy.Resolve(defense, Stats("PLATE"), rotated, new WorldVector(1, 0, 0), 20, 20).Blocked);
            Assert.IsFalse(WordboundDefensePolicy.Resolve(defense, Stats("PLATE"), rotated, new WorldVector(-1, 0, 0), 20, 20).Blocked);
        }

        [Test]
        public void HazardsAndUnknownIncomingDirectionDoNotGainAFreeBlock()
        {
            var defense = new MatchDefenseState("item:1", 1, 31);
            var stats = Stats("SHIELD");
            Assert.IsFalse(WordboundDefensePolicy.Resolve(defense, stats, Player(), null, 20, 20).Blocked);
            Assert.IsFalse(WordboundDefensePolicy.Resolve(defense, stats, Player(), default(WorldVector), 20, 20).Blocked);
            Assert.IsFalse(WordboundDefensePolicy.Resolve(null, null, Player(), new WorldVector(0, 0, 1), 20, 20).Blocked);
        }

        [Test]
        public void RefreshPreservesRaiseProgressButExpiredOrDifferentEquipmentStartsAgain()
        {
            var previous = new MatchDefenseState("item:1", 10, 40);
            var refreshed = WordboundDefensePolicy.Renew(previous, "item:1", 20);
            Assert.AreEqual(10, refreshed.StartedTick);
            Assert.AreEqual(50, refreshed.UntilTick);
            Assert.AreEqual(40, WordboundDefensePolicy.Renew(previous, "item:1", 40).StartedTick);
            Assert.AreEqual(20, WordboundDefensePolicy.Renew(previous, "item:2", 20).StartedTick);
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.Renew(previous, "item:1", 9));
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.Renew(null, "item:1", 0));
        }

        [Test]
        public void UnreadyAndExpiredDefenseKeepDamageAndDoNotWearTheShield()
        {
            var defense = new MatchDefenseState("item:1", 10, 40);
            foreach (long tick in new long[] { 9, 10, 17, 40, 41 })
            {
                var hit = WordboundDefensePolicy.Resolve(defense, Stats("PLATE"), Player(), new WorldVector(0, 0, 1), 20, tick);
                Assert.AreEqual(20, hit.Damage);
                Assert.AreEqual(0, hit.BlockedDamage);
            }
        }

        [Test]
        public void InvalidShieldStatisticsAndDamageFailClosed()
        {
            var stats = Stats("PLATE");
            stats.FrontArcDegrees = 361;
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.RaiseTicks(stats));
            stats = Stats("PLATE"); stats.RaiseSeconds = float.NaN;
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.RaiseTicks(stats));
            stats = Stats("PLATE"); stats.MoveSpeedMultiplier = 0;
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.RaiseTicks(stats));
            Assert.Throws<ArgumentException>(() => WordboundDefensePolicy.Resolve(null, null, Player(), null, float.NaN, 1));
        }
    }
}
