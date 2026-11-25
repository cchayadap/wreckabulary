using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class RulesConfigTests
    {
        [Test]
        public void ShippedRulesAreValidForEveryMode()
        {
            var book = TestData.LoadRuleBook();
            Assert.IsEmpty(book.Validate());
            CollectionAssert.AreEquivalent(new[] { "Hub", "Tutorial", "Dibs", "Duos", "MovingOut", "MovingDay", "Escape", "BossFight" }, book.Modes);
        }

        // @trace NUM-002, NUM-003, NUM-004, NUM-005, MECH-002
        [Test]
        public void BossFightRulesDefineCalculatorAndExtremeValues()
        {
            var r = TestData.RulesFor("BossFight");
            Assert.AreEqual("CALCULATOR", r.BossName);
            CollectionAssert.AreEqual(new[] { "CALC", "ULA", "TOR" }, r.BossWords);
            Assert.AreEqual(3, r.BossWords.Length);
            Assert.AreEqual(240f, r.BossMaxHealth);
            Assert.AreEqual(1.5f, r.BossVulnerability);
            Assert.AreEqual(10, r.MaxLetters, "the bag is deliberately tight in Boss Fight");
            Assert.GreaterOrEqual(r.MaxLetters, r.BossWords.Max(w => w.Length));
            Assert.IsFalse(r.ClearOutEnabled, "clear-out would need an order in every house JSON");

            // full-minimum and full-maximum damage, with the 1 HP clamp
            Assert.AreEqual(15f, MathfMax(1f, 10f * r.BossVulnerability));
            Assert.AreEqual(54.6f, MathfMax(1f, 28f * r.BossVulnerability * 1.3f), 0.001f);

            // AoE table: coefficients rise across layers, and every radius stays below the dodge distance
            for (int layer = 0; layer < 4; layer++)
            {
                Assert.Greater(r.AoeRadiusFor(layer), 0f);
                Assert.Greater(r.AoeDamageFor(layer), 0f);
                Assert.Greater(r.AoeCooldownFor(layer), 0f);
                Assert.Less(r.AoeRadiusFor(layer), 4f, "a 4 m dodge must clear the circle");
            }
            Assert.Greater(r.AoeWarnSeconds, r.SlickDodgeWindow);
            Assert.AreEqual(1.6f, r.AoeWarnSeconds, "a longer telegraph keeps spelling readable");
            Assert.AreEqual(3, r.MaxAoe, "up to three circles at once");
            Assert.AreEqual(1, r.LettersLostOnHit, "a hit still matters but no longer wipes a word");

            // @trace AMD-006, AMD-007, AMD-008, AMD-010, AMD-012, AMD-013, AMD-014, AMD-015, AMD-016, AMD-017, AMD-018, AMD-019, AMD-022
            Assert.AreEqual(20f, r.PrepSeconds, "the prepare countdown is twenty seconds");
            Assert.AreEqual(5, r.StarterProps, "only five props exist at the start");
            Assert.AreEqual(30f, r.SupplyDropSeconds, "one supply drop every thirty seconds");
            Assert.AreEqual(3, r.SupplyPropsPerDrop, "each supply delivers a random handful of props");
            Assert.AreEqual(90f, r.SupplyPropLifetimeSeconds, "old supplies despawn before the house fills up");
            Assert.AreEqual(600f, r.RoundTimeLimitSeconds, "a long run limit keeps the fight unhurried");
            Assert.AreEqual(150f, r.StarThreeSeconds);
            Assert.AreEqual(300f, r.StarTwoSeconds, "three stars for a fast clear, one for a slow one");
            Assert.AreEqual(1, r.AoeBurstMin);
            Assert.AreEqual(3, r.AoeBurstMax);
            Assert.GreaterOrEqual(r.MaxAoe, r.AoeBurstMax);
            Assert.AreEqual(25f, r.MiniFirstSeconds, "first mini wave arrives later");
            Assert.AreEqual(30f, r.MiniIntervalSeconds, "mini waves return at a moderate cadence");
            Assert.AreEqual(2, r.MiniMaxAlive);
            Assert.Greater(r.MiniSpeed, 0f);
            Assert.Less(r.MiniStunSeconds, .5f, "a mini touch must not be a long stun");
            Assert.Greater(r.MiniHitCooldownSeconds, r.MiniStunSeconds * 2f,
                "cooldown must prevent chain stunning");
            Assert.AreEqual(6f, r.SmashEverySeconds, "post-break smashes keep the fight lively");
            Assert.Greater(r.SmashRadius, 0f);
            Assert.Greater(r.SmashDamage, 0f);
            Assert.AreEqual(42f, r.MiniHealth, "minis need several hits to break");
            Assert.AreEqual(.8f, r.MiniFreezeAfterHitSeconds, "a bonked mini pauses before chasing again");
            Assert.AreEqual(3f, r.EventWarnSeconds, "every timed event gets a 3-2-1 cue");
        }

        static float MathfMax(float a, float b) => a > b ? a : b;

        [Test]
        public void TheBriefsDefaults()
        {
            var r = TestData.RulesFor("Dibs");
            Assert.AreEqual(100f, r.MaxHealth);
            Assert.AreEqual(10, r.MaxLetters);
            Assert.AreEqual(2, r.MaxCarried);
            Assert.AreEqual(2, r.MaxDeployed);
            Assert.AreEqual(0, r.LettersDroppedPerHit, "D1: ordinary hits don't knock letters loose");
            Assert.IsTrue(r.ClearOutEnabled);
            Assert.AreEqual(3, r.RoundsToWin);
        }

        [Test]
        public void TeamModesHaveTeamsReviveAndNoFriendlyFire()
        {
            foreach (string mode in new[] { "Duos", "MovingOut" })
            {
                var r = TestData.RulesFor(mode);
                Assert.Greater(r.TeamSize, 1, mode);
                Assert.IsTrue(r.DownedEnabled, mode);
                Assert.IsFalse(r.FriendlyFire, mode);
            }
            Assert.AreEqual(2, TestData.RulesFor("Duos").TeamSize);
            Assert.IsFalse(TestData.RulesFor("Dibs").DownedEnabled);
        }

        [Test]
        public void ModesOnlyChangeWhatTheyList()
        {
            var book = TestData.LoadRuleBook();
            Assert.AreEqual(book.Defaults.CraftBaseSeconds, book.For("Duos").CraftBaseSeconds);
            Assert.AreEqual(book.Defaults.ClearOutFirstAt, book.For("Dibs").ClearOutFirstAt);
            Assert.AreNotEqual(book.Defaults.ClearOutFirstAt, book.For("MovingOut").ClearOutFirstAt);
            Assert.AreEqual(book.Defaults.ClearOutWarnSeconds, 10f);
            book.For("Duos").BleedOutSeconds[0] = 99f;
            Assert.AreEqual(20f, book.For("MovingOut").BleedOutSeconds[0], "modes don't share arrays");
        }

        [Test]
        public void CraftTimeGrowsWithTheWord()
        {
            var r = TestData.RulesFor("Dibs");
            var c = TestData.Catalogue();
            Assert.AreEqual(0.96f, r.CraftSeconds(c.Get("BAT")), 1e-5);
            Assert.AreEqual(1.2f, r.CraftSeconds(c.Get("TABLE")), 1e-5);
            Assert.AreEqual(20f, r.BleedOutFor(1));
            Assert.AreEqual(12f, r.BleedOutFor(2));
            Assert.AreEqual(6f, r.BleedOutFor(3));
            Assert.AreEqual(6f, r.BleedOutFor(9), "stays at the shortest");
        }

        [Test]
        public void TyposInRulesAreErrorsNotSilentDefaults()
        {
            var e = Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {\"maxHelth\": 90}, \"modes\": {}}", "rules.json"));
            Assert.IsTrue(e.Message.Contains("maxHelth") && e.Message.Contains("rules.json"), e.Message);
            Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {}, \"modes\": {\"X\": {\"clearOut\": {\"firstat\": 3}}}}"));
            Assert.Throws<FormatException>(() => RuleBook.FromJson("{\"defaults\": {\"maxLetters\": 17.5}, \"modes\": {}}"));
        }

        [Test]
        public void BadValuesAreReported()
        {
            var book = RuleBook.FromJson("{\"defaults\": {}, \"modes\": {\"Bad\": {\"hitStunMax\": 2, \"downed\": true, \"starterLetters\": \"abc\"}}}");
            var problems = book.Validate();
            Assert.IsTrue(problems.Any(p => p.StartsWith("Bad:") && p.Contains("stun")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("teams")), string.Join("\n", problems));
            Assert.IsTrue(problems.Any(p => p.Contains("A-Z")), string.Join("\n", problems));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => book.For("Nope"));
        }
    }
}
