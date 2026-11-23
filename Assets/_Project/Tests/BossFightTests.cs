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

        [TestCase("CALC")]
        [TestCase("ULA")]
        [TestCase("TOR")]
        public void CachePropsNaturallyContainTheWholeShieldWord(string shield)
        {
            var props = BossFightDirector.CachePropsFor(shield);
            Assert.IsNotEmpty(props, shield + " needs natural prop carriers");
            string letters = string.Concat(props);
            foreach (var group in shield.GroupBy(c => c))
                Assert.GreaterOrEqual(letters.Count(c => c == group.Key), group.Count(),
                    "props must naturally contain every " + group.Key + " needed by " + shield);
            Assert.LessOrEqual(letters.Length, 12, "carriers should fit the bag without excessive junk");
        }

        [UnityTest]
        public IEnumerator PropsReleaseTheirWholeWordNotOneFakeLetter()
        {
            TilePool.Ensure();
            var prop = FurnitureCatalog.Spawn("BED", new Vector3(0f, .35f, 0f), 0f, World.Transient);
            Assert.IsNotNull(prop);
            Assert.AreEqual("BED", prop.Drops, "a prop must default to dropping its own letters");

            prop.Break();
            yield return null;

            var released = Object.FindObjectsByType<LetterTile>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.Letter).ToArray();
            foreach (char c in "BED")
                Assert.Contains(c, released, "smashing a BED must release its own " + c + " tile");
        }

        // @trace AMD-007
        [UnityTest]
        public IEnumerator AoeBurstCanSpawnThreeCirclesAtOnce()
        {
            var rules = BossRules();
            rules.AoeBurstMin = 3;
            rules.AoeBurstMax = 3;
            rules.MaxAoe = 3;
            var go = new GameObject("AoE burst test");
            var aoe = go.AddComponent<TelegraphedAoE>();
            aoe.Configure(rules);
            aoe.Tick(true, 0, Vector3.zero, rules.AoeCooldownFor(0) + .1f);
            yield return null;

            Assert.AreEqual(3, aoe.ActiveCircleCount, "the burst must respect a 1-3 random range");
        }

        // @trace AMD-008
        [UnityTest]
        public IEnumerator MiniCalculatorTouchNeverChainStuns()
        {
            TilePool.Ensure();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            player.Inventory.Set("ABCD");
            float hp = player.Health.Current;
            yield return null;

            var go = new GameObject("Mini calculator test");
            var mini = go.AddComponent<MiniCalculator>();
            mini.Configure(BossRules(), player.transform.position + new Vector3(.5f, 0f, 0f));
            yield return new WaitForSeconds(.7f); // wait out the spawn grace

            mini.Tick(player, .016f);
            yield return null;
            Assert.AreEqual(hp, player.Health.Current, .001f, "a mini touch must never remove HP");
            Assert.IsTrue(player.IsStaggered, "the touch should briefly stagger");
            Assert.AreEqual(3, player.Inventory.Count, "one letter should be knocked loose");

            mini.Tick(player, .016f);
            yield return null;
            Assert.AreEqual(3, player.Inventory.Count,
                "a second touch inside the cooldown cannot keep stunning or draining letters");
        }

        // @trace AMD-014
        [UnityTest]
        public IEnumerator MiniNeedsSeveralHitsAndDropsOneRandomLetter()
        {
            TilePool.Ensure();
            int tilesBefore = Object.FindObjectsByType<LetterTile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var go = new GameObject("Mini smash test");
            var mini = go.AddComponent<MiniCalculator>();
            mini.Configure(BossRules(), new Vector3(0f, .4f, 0f));
            Assert.Greater(mini.MaxHealth, 1f, "a mini must have real health");

            var hit = new HitInfo { Damage = 28f, Source = HitSource.Melee }; // strongest shipped weapon
            Assert.IsTrue(mini.ApplyDamage(hit));
            Assert.IsFalse(mini.Broken, "one swing must never one-shot a mini");
            Assert.Less(mini.Health, mini.MaxHealth);
            yield return null;

            Assert.IsTrue(mini.ApplyDamage(hit), "the next swing should finish it");
            yield return null;

            var released = Object.FindObjectsByType<LetterTile>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(t => t.Letter).ToArray();
            Assert.AreEqual(tilesBefore + 1, released.Length,
                "a mini drops exactly one letter, never the word CALC");
            Assert.GreaterOrEqual(released[0], 'A');
            Assert.LessOrEqual(released[0], 'Z');
        }

        // @trace AMD-012, AMD-013
        [UnityTest]
        public IEnumerator JumpSmashLeapsTheBossOntoTheTelegraph()
        {
            var rules = BossRules();
            var bossGo = new GameObject("Leap boss");
            var boss = bossGo.AddComponent<BossController>();
            boss.Configure(rules, new BossNameCatalogue(rules), Vector3.zero);
            var aoeGo = new GameObject("Leap marker");
            var aoe = aoeGo.AddComponent<TelegraphedAoE>();
            aoe.Configure(rules, boss);
            Vector3 marker = Vector3.zero;
            aoe.SmashTelegraphed += at => marker = at;

            aoe.TickSmash(true, new Vector3(3f, 0f, 0f), rules.SmashEverySeconds + .1f);
            yield return null;

            Assert.IsTrue(boss.IsLeaping, "the smash must animate a real boss jump");
            Assert.Less(Vector3.Distance(new Vector3(marker.x, 0f, marker.z), new Vector3(3f, 0f, 0f)), 1.3f,
                "the marker lands next to the player");
            Assert.Less(Vector3.Distance(boss.LeapTarget, marker), .01f,
                "the boss's landing spot is the marked circle");
            Assert.Greater(aoe.SmashIn, 0f, "the next smash timer is exposed for the 3-2-1 cue");
            aoe.CancelAll();
            Assert.IsFalse(boss.IsLeaping, "cancelling telegraphs also cancels the leap");
        }
    }
}
