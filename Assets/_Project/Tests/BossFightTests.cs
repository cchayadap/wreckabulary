using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.BossFight;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>Regression coverage for the Boss Fight recipe list and typed spell path.</summary>
    public class BossFightTests
    {
        static GameRules BossRules() => new GameRules
        {
            BossName = "CALCULATOR",
            BossWords = new[] { "CALC", "ULA", "TOR" },
        };

        [Test]
        public void BossWordListKeepsEveryNormalRecipe()
        {
            var ids = new BossNameCatalogue(BossRules()).SpellWords().Select(w => w.word).ToArray();
            CollectionAssert.IsSubsetOf(GameAssets.I.words.Words.Select(w => w.word).ToArray(), ids);
            CollectionAssert.Contains(ids, "BAT");
            CollectionAssert.Contains(ids, "WALL");
            CollectionAssert.Contains(ids, "CALC");
            CollectionAssert.Contains(ids, "ULA");
            CollectionAssert.Contains(ids, "TOR");
        }

        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator BossOverrideStillSpellsWeaponsAndShieldWords()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            yield return null;

            var catalogue = new BossNameCatalogue(BossRules());
            player.Summoner.WordsOverride = catalogue.SpellWords();
            player.Summoner.SignalWords = catalogue.SignalWords();

            player.Inventory.Set("BAT");
            var weapon = player.Summoner.Recipe("BAT");
            Assert.IsNotNull(weapon, "BAT must stay in the word wheel / typed composer");
            Assert.IsFalse(player.Summoner.IsSignalWord("BAT"));
            Assert.IsTrue(player.Summoner.CanCraft(weapon), "the typed composer gate must accept normal recipes");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            Assert.AreEqual("BAT", player.Combat.Weapon.word);

            string fired = null;
            player.Summoner.Summoned += word => fired = word;
            player.Inventory.Set("CALC");
            var shield = player.Summoner.Recipe("CALC");
            Assert.IsNotNull(shield, "shield words must be spellable");
            Assert.IsTrue(player.Summoner.IsSignalWord("CALC"));
            Assert.IsTrue(player.Summoner.CanCraft(shield), "shield words have no items.json recipe but must pass the typed gate");
            Assert.IsTrue(player.Summoner.Summon("CALC"));
            Assert.AreEqual("CALC", fired, "a shield word fires the director event without spawning gear");
            Assert.AreEqual(0, player.Inventory.Count);
            Assert.AreEqual("BAT", player.Combat.Weapon.word, "spelling a shield word must not replace the weapon");
        }

        [UnityTest]
        public IEnumerator BossIsHittableAndTheShieldGatesDamage()
        {
            var rules = BossRules();
            var go = new GameObject("Boss damage test");
            var boss = go.AddComponent<BossController>();
            boss.Configure(rules, new BossNameCatalogue(rules), Vector3.zero);
            yield return null;

            // PlayerCombat.Strike skips colliders without a Rigidbody - this was why hits did nothing.
            Assert.IsNotNull(go.GetComponent<Rigidbody>(), "swings need a rigidbody to reach IDamageable");
            Assert.IsNotNull(go.GetComponent<Collider>(), "the boss needs a hittable box");

            var hit = new HitInfo { Damage = 24f, Source = HitSource.Melee };
            Assert.IsFalse(boss.ApplyDamage(hit), "shielded hits must be refused");
            Assert.AreEqual(240f, boss.Health);

            for (int i = 0; i < 3; i++) Assert.IsTrue(boss.CrackLayer());
            Assert.IsTrue(boss.Vulnerable);
            Assert.IsTrue(boss.ApplyDamage(hit));
            Assert.AreEqual(240f - 24f * rules.BossVulnerability, boss.Health, .01f);
        }

        [UnityTest]
        public IEnumerator TelegraphsLandOutsideThePlayersFeet()
        {
            var rules = BossRules();
            var go = new GameObject("AoE placement test");
            var aoe = go.AddComponent<TelegraphedAoE>();
            aoe.Configure(rules);
            aoe.Tick(true, 0, Vector3.zero, rules.AoeCooldownFor(0) + .1f);
            yield return null;

            var flat = new Vector2(aoe.LastSpawnCentre.x, aoe.LastSpawnCentre.z);
            Assert.GreaterOrEqual(flat.magnitude, rules.AoeRadiusFor(0) + .5f,
                "standing still and spelling must be safe; only stepping into a circle should hurt");
        }
    }
}
