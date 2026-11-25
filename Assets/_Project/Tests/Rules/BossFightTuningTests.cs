using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class BossFightTuningTests
    {
        static GameRules Rules()
        {
            var book = TestData.LoadRuleBook();
            book.Layer(TestData.Read("modes_house.json"));
            return book.For(HouseModes.BossFight);
        }

        [Test]
        public void ModesHouseCarriesTheBossNumbers()
        {
            var t = BossFightTuning.From(Rules());
            Assert.AreEqual(240f, t.BossMaxHealth);
            Assert.AreEqual(1.5f, t.BossVulnerability);
            Assert.AreEqual(20f, t.PrepSeconds);
            Assert.AreEqual(12f, t.AppearSeconds);
            Assert.AreEqual(4f, t.StaggerSeconds);
            Assert.AreEqual(5, t.StarterProps);
            Assert.AreEqual(30f, t.SupplyDropSeconds);
            Assert.AreEqual(1.6f, t.AoeWarnSeconds);
            Assert.AreEqual(1, t.AoeBurstMin);
            Assert.AreEqual(3, t.AoeBurstMax);
            Assert.LessOrEqual(t.AoeBurstMax, t.MaxAoe);
            Assert.AreEqual(25f, t.MiniFirstSeconds);
            Assert.AreEqual(30f, t.MiniIntervalSeconds);
            Assert.AreEqual(2, t.MiniMaxAlive);
            Assert.AreEqual(42f, t.MiniHealth);
            Assert.Less(t.MiniStunSeconds, .5f);
            Assert.Greater(t.MiniHitCooldownSeconds, t.MiniStunSeconds * 2f);
            Assert.AreEqual(6f, t.SmashEverySeconds);
            Assert.AreEqual(2.4f, t.SmashRadius);
            Assert.AreEqual(18f, t.SmashDamage);
            CollectionAssert.IsEmpty(t.Validate());
        }

        [Test]
        public void EachPhaseReadsItsOwnCircleAndClampsOutsideTheRange()
        {
            var t = BossFightTuning.From(Rules());
            CollectionAssert.AreEqual(new[] { 1.6f, 1.8f, 2f, 2.2f }, Enumerable.Range(0, BossFightTuning.Phases).Select(t.AoeRadiusFor).ToArray());
            CollectionAssert.AreEqual(new[] { 10f, 12f, 14f, 14f }, Enumerable.Range(0, BossFightTuning.Phases).Select(t.AoeDamageFor).ToArray());
            CollectionAssert.AreEqual(new[] { 5.5f, 5f, 4.4f, 4f }, Enumerable.Range(0, BossFightTuning.Phases).Select(t.AoeCooldownFor).ToArray());
            Assert.AreEqual(t.AoeRadiusFor(0), t.AoeRadiusFor(-1));
            Assert.AreEqual(t.AoeRadiusFor(3), t.AoeRadiusFor(9));
            Assert.Greater(BossFightTuning.FeetGap, 0f, "circles land beside the feet, so standing still to spell is safe");
        }

        [Test]
        public void TheBossIsTheCalculatorWithThreeShieldWordsThatFitTheBag()
        {
            var rules = Rules();
            Assert.AreEqual("CALCULATOR", BossFightTuning.Name);
            Assert.AreEqual("pinwheel", BossFightTuning.Map);
            CollectionAssert.AreEqual(new[] { "CALC", "ULA", "TOR" }, BossFightTuning.Words.ToArray());
            Assert.AreEqual(BossFightTuning.Name, string.Concat(BossFightTuning.Words));
            Assert.LessOrEqual(BossFightTuning.Words.Max(w => w.Length), rules.MaxLetters);
            Assert.AreEqual("CALC", BossFightTuning.WordFor(-1));
            Assert.AreEqual("TOR", BossFightTuning.WordFor(7));
        }

        [Test]
        public void MissingSettingsFallBackToTheShippedNumbers()
        {
            var t = BossFightTuning.From(new GameRules());
            Assert.AreEqual(240f, t.BossMaxHealth);
            Assert.AreEqual(2.2f, t.AoeRadiusFor(3));
            CollectionAssert.IsEmpty(t.Validate());
            Assert.AreEqual(240f, BossFightTuning.From(null).BossMaxHealth);
        }

        [Test]
        public void BadNumbersAreReported()
        {
            var t = new BossFightTuning { MaxAoe = 2, MiniStunSeconds = .8f, BossMaxHealth = 0f };
            t.AoeRadius[2] = 0f;
            var problems = t.Validate();
            Assert.IsTrue(problems.Any(p => p.Contains("maxAoe")), string.Join("; ", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("miniStunSeconds")));
            Assert.IsTrue(problems.Any(p => p.Contains("bossMaxHealth")));
            Assert.IsTrue(problems.Any(p => p.Contains("aoeRadius3")));
        }
    }
}
