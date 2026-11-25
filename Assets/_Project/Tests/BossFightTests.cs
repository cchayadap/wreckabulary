using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.BossFight;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BossFightTests
    {
        const float LoadSeconds = 10f;

        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DesktopBinding.Typing = false;
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(InputBinding binding)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, binding);
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        static void Arm(PlayerController player)
        {
            var catalogue = new BossNameCatalogue();
            player.Summoner.WordsOverride = catalogue.SpellWords();
            player.Summoner.SignalWords = catalogue.SignalWords();
            player.Summoner.ChecklistPlacementWords = System.Array.Empty<string>();
        }

        static IEnumerator LoadBossFight()
        {
            Match.ModeOverride = HouseModes.BossFight;
            Session.SelectMap(BossFightTuning.Map);
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.WaitUntil(() => BossFightDirector.Instance && BossFightDirector.Instance.Current == BossFightDirector.State.Preparing, LoadSeconds, "the Boss Fight director to start preparing");
        }

        [UnityTest]
        public IEnumerator BossWordListKeepsEveryNormalRecipe()
        {
            yield return null;
            var ids = new BossNameCatalogue().SpellWords().Select(w => w.word).ToArray();
            CollectionAssert.IsSubsetOf(GameAssets.I.words.Words.Select(w => w.word).ToArray(), ids);
            CollectionAssert.Contains(ids, "BAT");
            foreach (string word in BossFightTuning.Words) CollectionAssert.Contains(ids, word);
            Assert.IsFalse(GameAssets.I.words.Words.Any(w => BossFightTuning.Words.Contains(w.word)), "the shared word database is never changed");
        }

        [UnityTest]
        public IEnumerator BossOverrideStillSpellsWeaponsAndShieldWords()
        {
            var player = Spawn(new ScriptedBinding());
            yield return null;
            Arm(player);

            player.Inventory.Set("BAT");
            var weapon = player.Summoner.Recipe("BAT");
            Assert.IsNotNull(weapon, "BAT stays in the word wheel and the typed composer");
            Assert.IsFalse(player.Summoner.IsSignalWord("BAT"));
            Assert.IsTrue(player.Summoner.CanCraft(weapon));
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            Assert.AreEqual("BAT", player.Combat.Weapon.word);

            string fired = null;
            player.Summoner.Summoned += word => fired = word;
            player.Inventory.Set("CALC");
            var shield = player.Summoner.Recipe("CALC");
            Assert.IsNotNull(shield, "shield words are spellable");
            Assert.IsTrue(player.Summoner.IsSignalWord("calc"));
            Assert.IsTrue(player.Summoner.CanCraft(shield), "shield words have no item but pass the craft gate");
            Assert.IsTrue(player.Summoner.Summon("CALC"));
            Assert.AreEqual("CALC", fired, "a shield word fires the director event");
            Assert.AreEqual(0, player.Inventory.Count);
            Assert.AreEqual("BAT", player.Combat.Weapon.word, "a shield word never replaces the weapon");
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator FurnitureWordsEquipInBossFight()
        {
            var player = Spawn(new ScriptedBinding());
            yield return null;
            Arm(player);
            int placedBefore = Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None).Count(s => s.Word == "TABLE");
            player.Inventory.Set("TABLE");
            Assert.IsTrue(player.Summoner.Summon("TABLE"));
            yield return null;
            Assert.IsTrue(player.Combat.Weapon || player.Combat.StoredGear, "TABLE is held, not placed as Furnish the House furniture");
            Assert.AreEqual(placedBefore, Object.FindObjectsByType<Smashable>(FindObjectsSortMode.None).Count(s => s.Word == "TABLE"));
            Object.Destroy(player.gameObject);
        }

        [UnityTest]
        public IEnumerator TypedComposerAcceptsTheShieldWord()
        {
            var player = Spawn(DesktopBinding.Shared);
            var hud = new GameObject("Boss HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreSame(player, hud.LocalPlayer);
            Arm(player);
            player.Inventory.Set("CALC");
            player.Summoner.Open();
            yield return null;
            Assert.IsTrue(hud.ComposerOpen);
            hud.TypeWord("calc");
            yield return null;
            Assert.AreEqual("CALC is ready. Press Enter!", hud.ComposerStatus);
            hud.SubmitComposer();
            Assert.IsTrue(player.Summoner.IsCrafting, "Enter spells the shield word");
            Assert.AreEqual("CALC", player.Summoner.CraftWord);
            player.Summoner.CancelCraft();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossIsHittableAndTheShieldGatesDamage()
        {
            var tuning = new BossFightTuning();
            var go = new GameObject("Boss damage test");
            var boss = go.AddComponent<BossController>();
            boss.Configure(tuning, new BossNameCatalogue(), Vector3.zero);
            yield return null;

            Assert.IsNotNull(go.GetComponent<Rigidbody>(), "swings need a rigidbody to reach IDamageable");
            Assert.AreEqual(1, go.GetComponentsInChildren<Collider>().Length, "one known hit box, no stray primitive colliders");

            var hit = new HitInfo { Damage = 24f, Source = HitSource.Melee };
            Assert.IsFalse(boss.ApplyDamage(hit), "shielded hits are refused");
            Assert.AreEqual(tuning.BossMaxHealth, boss.Health);
            for (int i = 0; i < BossFightTuning.Words.Count; i++) Assert.IsTrue(boss.CrackLayer());
            Assert.IsTrue(boss.Vulnerable);
            Assert.IsFalse(boss.CrackLayer());
            Assert.IsTrue(boss.ApplyDamage(hit));
            Assert.AreEqual(tuning.BossMaxHealth - 24f * tuning.BossVulnerability, boss.Health, .01f);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator TelegraphsLandOutsideThePlayersFeet()
        {
            var tuning = new BossFightTuning();
            var go = new GameObject("AoE placement test");
            var aoe = go.AddComponent<TelegraphedAoE>();
            aoe.Configure(tuning);
            for (int i = 0; i < 20; i++)
            {
                aoe.CancelAll();
                aoe.Tick(true, 0, Vector3.zero, tuning.AoeCooldownFor(0) + .1f);
                var flat = new Vector2(aoe.LastSpawnCentre.x, aoe.LastSpawnCentre.z);
                Assert.GreaterOrEqual(flat.magnitude, tuning.AoeRadiusFor(0) + .5f, "standing still to spell is safe");
            }
            yield return null;
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator AoeBurstCanSpawnThreeCirclesAtOnce()
        {
            var tuning = new BossFightTuning { AoeBurstMin = 3, AoeBurstMax = 3, MaxAoe = 3 };
            var go = new GameObject("AoE burst test");
            var aoe = go.AddComponent<TelegraphedAoE>();
            aoe.Configure(tuning);
            aoe.Tick(true, 0, Vector3.zero, tuning.AoeCooldownFor(0) + .1f);
            yield return null;
            Assert.AreEqual(3, aoe.ActiveCircleCount);
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator MiniCalculatorTouchNeverChainStuns()
        {
            TilePool.Ensure();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0f, -.5f, 0f);
            floor.transform.localScale = new Vector3(20f, 1f, 20f);
            var player = Spawn(new ScriptedBinding());
            player.Inventory.Set("ABCD");
            float hp = player.Health.Current;
            yield return TestScenes.WaitUntil(() => !player.Health.IsInvulnerable, 5f, "spawn protection to end");

            var go = new GameObject("Mini calculator test");
            var mini = go.AddComponent<MiniCalculator>();
            mini.Configure(new BossFightTuning(), player.transform.position + new Vector3(.5f, 0f, 0f));
            yield return new WaitForSeconds(.7f);

            mini.Tick(player, .016f);
            Assert.IsTrue(player.IsStaggered, "the touch briefly staggers");
            Assert.AreEqual(hp, player.Health.Current, .001f, "a mini touch never removes health");
            Assert.AreEqual(3, player.Inventory.Count, "one letter is knocked loose");

            mini.Tick(player, .016f);
            yield return null;
            Assert.AreEqual(3, player.Inventory.Count, "a second touch inside the cooldown drains nothing");
            Object.Destroy(go);
            Object.Destroy(player.gameObject);
            Object.Destroy(floor);
        }

        [UnityTest]
        public IEnumerator MiniNeedsSeveralHitsAndDropsOneRandomLetter()
        {
            TilePool.Ensure();
            yield return null;
            var before = TilePool.Instance.Active.Where(t => t).ToList();
            var go = new GameObject("Mini smash test");
            var mini = go.AddComponent<MiniCalculator>();
            mini.Configure(new BossFightTuning(), new Vector3(0f, .4f, 0f));
            Assert.Greater(mini.MaxHealth, 1f);

            var hit = new HitInfo { Damage = 28f, Source = HitSource.Melee };
            Assert.IsTrue(mini.ApplyDamage(hit));
            Assert.IsFalse(mini.Broken, "one swing never breaks a mini");
            Assert.Less(mini.Health, mini.MaxHealth);
            yield return null;

            Assert.IsTrue(mini.ApplyDamage(hit), "the next swing finishes it");
            yield return null;
            var fresh = TilePool.Instance.Active.Where(t => t && !before.Contains(t)).ToList();
            Assert.AreEqual(1, fresh.Count, "a mini drops exactly one letter");
            char letter = fresh[0].Letter;
            Assert.IsTrue(letter >= 'A' && letter <= 'Z', "the dropped letter is one capital A to Z, got " + letter);
        }

        [UnityTest]
        public IEnumerator JumpSmashLeapsTheBossOntoTheTelegraph()
        {
            var tuning = new BossFightTuning();
            var bossGo = new GameObject("Leap boss");
            var boss = bossGo.AddComponent<BossController>();
            boss.Configure(tuning, new BossNameCatalogue(), Vector3.zero);
            var aoeGo = new GameObject("Leap marker");
            var aoe = aoeGo.AddComponent<TelegraphedAoE>();
            aoe.Configure(tuning, boss);
            Vector3 marker = Vector3.zero;
            aoe.SmashTelegraphed += at => marker = at;

            aoe.TickSmash(true, new Vector3(3f, 0f, 0f), tuning.SmashEverySeconds + .1f);
            yield return null;

            Assert.IsTrue(boss.IsLeaping, "the smash animates a real boss jump");
            Assert.IsTrue(boss.LeapsOverWalls, "an open floor gives an arc, not a pop");
            Assert.Less(Vector3.Distance(new Vector3(marker.x, 0f, marker.z), new Vector3(3f, 0f, 0f)), 1.3f);
            Assert.Less(Vector3.Distance(boss.LeapTarget, marker), .01f);
            Assert.Greater(aoe.SmashIn, 0f);
            aoe.CancelAll();
            Assert.IsFalse(boss.IsLeaping);
            Object.Destroy(bossGo);
            Object.Destroy(aoeGo);
        }

        [UnityTest]
        public IEnumerator TheLeapPopsInsteadOfArcingThroughAWall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(1.5f, 1.5f, 0f);
            wall.transform.localScale = new Vector3(.2f, 3f, 6f);
            var bossGo = new GameObject("Walled boss");
            var boss = bossGo.AddComponent<BossController>();
            boss.Configure(new BossFightTuning(), new BossNameCatalogue(), Vector3.zero);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            Assert.IsFalse(boss.PathClear(new Vector3(3f, 0f, 0f)));
            boss.LeapTo(new Vector3(3f, 0f, 0f), .5f);
            Assert.IsTrue(boss.IsLeaping);
            Assert.IsFalse(boss.LeapsOverWalls, "a wall in the way turns the leap into a vanish and drop");
            yield return TestScenes.WaitUntil(() => !boss.IsLeaping, 3f, "the boss to land");
            Assert.Less(Vector3.Distance(bossGo.transform.position, new Vector3(3f, 0f, 0f)), .01f);
            Object.Destroy(wall);
            Object.Destroy(bossGo);
        }

        [UnityTest]
        public IEnumerator BossFightAttachesItsDirectorFromTheRegistry()
        {
            Assert.IsTrue(HouseModeDirectors.Has(HouseModes.BossFight));
            yield return LoadBossFight();
            var director = BossFightDirector.Instance;
            var rounds = Object.FindAnyObjectByType<RoundManager>();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.IsFalse(rounds.enabled, "the director owns the run");
            Assert.AreEqual(1, joins.Players.Count, "no bots join a Boss Fight");
            Assert.AreEqual(0, joins.Players.Count(p => p.Binding is BotBinding));
            Assert.AreEqual(BossFightTuning.Map, Session.MapId);
            Assert.AreEqual(BossFightCaches.StartersFor(director.Tuning.StarterProps).Count, director.Props.Count(p => p && !p.IsBroken));
            Assert.IsFalse(director.Revealed);
            Assert.IsNull(director.WantedWord(joins.Players[0]));
            var player = joins.Players[0];
            Assert.IsTrue(player.Summoner.IsSignalWord("CALC"));
            Assert.Greater(Vector3.Distance(player.transform.position, director.Boss.Home), 2f, "the player never starts on the boss's spot");
        }

        [UnityTest]
        public IEnumerator TheClockCountsTheWholeRun()
        {
            yield return LoadBossFight();
            var director = BossFightDirector.Instance;
            yield return new WaitForSeconds(.5f);
            float before = director.TimeLeft;
            Assert.Less(before, Match.Rules.RoundTimeLimitSeconds);
            director.Advance(BossFightDirector.State.Appear);
            yield return null;
            Assert.LessOrEqual(director.TimeLeft, before, "a new phase never resets the clock");
            Assert.IsTrue(director.Revealed);
            director.Advance(BossFightDirector.State.DropAndPrevent);
            yield return null;
            Assert.LessOrEqual(director.TimeLeft, before);
            Assert.AreEqual("CALC", director.WantedWord(Object.FindAnyObjectByType<PlayerJoinManager>().Players[0]), "the wheel sorts the shield word first");
        }

        [UnityTest]
        public IEnumerator TheBossHealthBarStaysUpThroughTheHitPhaseUntilTheResultCard()
        {
            yield return LoadBossFight();
            var director = BossFightDirector.Instance;
            var hud = Object.FindAnyObjectByType<GameHud>();
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Players[0];
            director.Advance(BossFightDirector.State.DropAndPrevent);
            yield return TestScenes.WaitUntil(() => director.Panel && director.Panel.Shown, 5f, "the boss panel in the first shield phase");
            for (int layer = 0; layer < BossFightTuning.Words.Count; layer++)
            {
                yield return TestScenes.WaitUntil(() => director.Current == BossFightDirector.State.DropAndPrevent, 10f, "layer " + (layer + 1));
                player.Inventory.Set(director.ShieldWord);
                Assert.IsTrue(player.Summoner.Summon(director.ShieldWord));
            }
            Assert.AreEqual(BossFightDirector.State.Hit, director.Current);
            yield return null;
            yield return null;
            Assert.IsTrue(hud.BannerShown, "SHIELD BROKEN is on screen");
            Assert.IsTrue(director.Panel.Shown, "the boss panel stays up under the banner");
            Assert.IsTrue(director.Panel.HealthShown, "the health bar replaces the shield words");
            Assert.AreEqual(1f, director.Panel.HealthFill, .01f);

            float damage = director.Tuning.BossMaxHealth * .25f / director.Tuning.BossVulnerability;
            director.Boss.ApplyDamage(new HitInfo { Damage = damage, Source = HitSource.Melee });
            yield return null;
            yield return null;
            Assert.AreEqual(director.Boss.Health / director.Boss.MaxHealth, director.Panel.HealthFill, .01f, "the fill follows the boss's health");
            Assert.AreEqual(.75f, director.Panel.HealthFill, .02f);
            yield return TestScenes.WaitUntil(() => !hud.BannerShown, 6f, "the banner to fade");
            Assert.IsTrue(director.Panel.Shown && director.Panel.HealthShown, "still shown after the banner");

            director.Boss.ApplyDamage(new HitInfo { Damage = director.Tuning.BossMaxHealth, Source = HitSource.Melee });
            yield return TestScenes.WaitUntil(() => hud.ResultShown, 5f, "the result card");
            yield return null;
            Assert.IsFalse(director.Panel.Shown, "the panel hides only for the result card");
        }

        [UnityTest]
        public IEnumerator SpellingTheThreeWordsBreaksTheShieldAndTheResultCardShows()
        {
            yield return LoadBossFight();
            var director = BossFightDirector.Instance;
            var hud = Object.FindAnyObjectByType<GameHud>();
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Players[0];
            director.Advance(BossFightDirector.State.DropAndPrevent);
            for (int layer = 0; layer < BossFightTuning.Words.Count; layer++)
            {
                yield return TestScenes.WaitUntil(() => director.Current == BossFightDirector.State.DropAndPrevent, 10f, "layer " + (layer + 1));
                Assert.AreEqual(BossFightTuning.Words[layer], director.ShieldWord);
                player.Inventory.Set(director.ShieldWord);
                Assert.IsTrue(player.Summoner.Summon(director.ShieldWord));
                Assert.AreEqual(BossFightTuning.Words.Count - layer - 1, director.Boss.LayersRemaining);
            }
            Assert.AreEqual(BossFightDirector.State.Hit, director.Current);
            Assert.IsTrue(director.Boss.Vulnerable);
            director.Boss.ApplyDamage(new HitInfo { Damage = director.Tuning.BossMaxHealth, Source = HitSource.Melee });
            yield return TestScenes.WaitUntil(() => hud.ResultShown, 5f, "the result card");
            Assert.AreEqual(BossFightDirector.State.Result, director.Current);
            Assert.IsTrue(director.Won);
            Assert.AreEqual(1f, Time.timeScale, "slow motion never outlives the fight");
        }
    }
}
