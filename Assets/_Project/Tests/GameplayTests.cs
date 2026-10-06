using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class GameplayTests
    {
        GameObject ground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController SpawnPlayer(int index, Vector3 at, out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(index, input);
            p.Respawn(at);
            return p;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static HitInfo Punch(PlayerController attacker, Vector3 direction) =>
            Hits.Melee(attacker, direction, (attacker ? attacker.Health.Rules : Match.Rules).Unarmed, null);

        static HitInfo Lethal(PlayerController attacker = null) =>
            Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f);

        [UnityTest]
        public IEnumerator SmashedFurnitureBurstsIntoItsLetters()
        {
            var sofa = LetterBuilt.Spawn("SOFA", Vector3.one * 0.5f, 0, Color.red, null);
            sofa.gameObject.AddComponent<Rigidbody>();
            sofa.gameObject.AddComponent<Smashable>().Init("SOFA", 10f);
            yield return null;

            sofa.GetComponent<Smashable>().TakeHit(20f);
            yield return null;

            Assert.IsTrue(sofa == null, "sofa should be destroyed");
            var letters = new string(TilePool.Instance.Active.Select(t => t.Letter).OrderBy(c => c).ToArray());
            Assert.AreEqual("AFOS", letters);
        }

        [UnityTest]
        public IEnumerator BoxesLandingOnBoxesDontBreak()
        {
            var bottom = DeliverySpawner.CreateBox("SOFA", Vector3.zero);
            yield return new WaitForSeconds(2f); // past its spawn grace
            var top = DeliverySpawner.CreateBox("LAMP", Vector3.up * 4f);
            top.GetComponent<Rigidbody>().linearVelocity = Vector3.down * 10f;
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(bottom && top, "neither box broke");
        }

        [UnityTest]
        public IEnumerator ThrownThingsSmashWhatTheyHit()
        {
            var bottom = DeliverySpawner.CreateBox("SOFA", Vector3.zero);
            yield return new WaitForSeconds(2f);
            var thrown = DeliverySpawner.CreateBox("LAMP", Vector3.up * 3f);
            ThrowTracker.Attach(thrown.gameObject, null, 2f);
            thrown.GetComponent<Rigidbody>().linearVelocity = Vector3.down * 14f;
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(bottom && thrown, "the throw smashed something");
        }

        [UnityTest]
        public IEnumerator PlayerCollectsNearbyTiles()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            var tile = TilePool.Instance.Get('B');
            tile.Launch(new Vector3(0.7f, 0.2f, 0f), Vector3.zero);
            tile.transform.rotation = tile.Body.rotation = Quaternion.identity;
            tile.Body.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.5f);

            CollectionAssert.AreEqual(new[] { 'B' }, p.Inventory.Letters.ToArray());
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
        }

        [UnityTest]
        public IEnumerator DroppedTilesCantBeRegrabbedInstantly()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("AB");
            p.Inventory.DropRandom(2, p.transform.position, Vector3.forward);
            Assert.AreEqual(0, p.Inventory.Count);
            yield return Frames(10);
            Assert.AreEqual(0, p.Inventory.Count, "the dropper shouldn't instantly re-collect their own tiles");
        }

        [UnityTest]
        public IEnumerator SummoningSpendsLettersAndEquipsWeapon()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("BLADEX");

            Assert.IsTrue(p.Summoner.Summon("BLADE"));
            CollectionAssert.AreEqual(new[] { 'X' }, p.Inventory.Letters.ToArray());
            Assert.IsNotNull(p.Combat.Weapon);
            Assert.AreEqual("BLADE", p.Combat.Weapon.word);
            Assert.That(p.Combat.Weapon.transform.lossyScale.x, Is.EqualTo(p.Combat.Weapon.Definition.HeldScale).Within(0.15f), "held gear uses its catalogue miniature scale");
            Assert.IsFalse(p.Summoner.Summon("SWORD"), "can't summon without the letters");
        }

        [UnityTest]
        public IEnumerator WordWheelFromInputSummonsSelectedWord()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(2);
            p.Inventory.Set("BLADEW");

            input.Next.spellHeld = true;
            input.Next.spellDown = true;
            yield return null;
            yield return null;
            Assert.IsTrue(p.Summoner.IsSpelling);
            Assert.AreEqual("BLADE", p.Summoner.SelectedWord?.word);

            input.Next.spellHeld = false;
            input.Next.spellUp = true;
            yield return TestScenes.WaitUntil(() => !p.Summoner.IsCrafting && p.Combat.Weapon, 3f, "BLADE craft channel");

            Assert.IsFalse(p.Summoner.IsSpelling);
            CollectionAssert.AreEqual(new[] { 'W' }, p.Inventory.Letters.ToArray());
            Assert.AreEqual("BLADE", p.Combat.Weapon?.word);
        }

        [UnityTest]
        public IEnumerator HitsCostHealthNotLetters()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("ABCD");
            Assert.AreEqual(100f, p.Health.Current);

            Assert.IsTrue(p.Health.ApplyDamage(Punch(null, Vector3.forward)));
            Assert.AreEqual(92f, p.Health.Current, "a punch does the rules' unarmed damage");
            Assert.AreEqual(4, p.Inventory.Count, "letters are loot, not health");
            Assert.AreEqual(0, TilePool.Instance.Active.Count);

            Assert.IsTrue(p.Health.ApplyDamage(Punch(null, Vector3.forward)), "no invulnerability after a hit");
            Assert.AreEqual(84f, p.Health.Current);

            var rules = Match.Rules.Clone();
            rules.LettersDroppedPerHit = 2;
            p.Health.UseRules(rules);
            Assert.IsTrue(p.Health.ApplyDamage(Punch(null, Vector3.forward)));
            Assert.AreEqual(2, p.Inventory.Count);
            Assert.AreEqual(2, TilePool.Instance.Active.Count);
        }

        [UnityTest]
        public IEnumerator LethalHitWrecksAndSpillsEveryLetter()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("ABC");
            bool knockedOut = false, eliminated = false;
            p.Health.KnockedOut += _ => knockedOut = true;
            p.Health.Eliminated += _ => eliminated = true;

            Assert.IsTrue(p.Health.ApplyDamage(Lethal()));
            Assert.IsTrue(knockedOut && eliminated);
            Assert.IsTrue(p.IsEliminated);
            Assert.AreEqual(0f, p.Health.Current);
            Assert.AreEqual(0, p.Inventory.Count);
            Assert.AreEqual(3, TilePool.Instance.Active.Count);
            Assert.IsFalse(p.Health.ApplyDamage(Lethal()), "a wrecked player takes no more hits");
        }

        [UnityTest]
        public IEnumerator StaggerImmunityStopsStunLock()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            float lastStun = -1f;
            p.Health.Damaged += (_, _, r) => lastStun = r.HitStun;

            p.Health.ApplyDamage(Punch(null, Vector3.forward));
            Assert.Greater(lastStun, 0f);
            Assert.IsTrue(p.IsStaggered);

            p.Health.ApplyDamage(Punch(null, Vector3.forward));
            Assert.AreEqual(0f, lastStun, "a second hit inside the immunity window still hurts but doesn't stun");
            Assert.AreEqual(84f, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator ShieldBlocksHitsFromTheFront()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.FaceTowards(Vector3.forward);
            p.Health.FrontBlockUntil = Time.time + 5f;

            Assert.IsFalse(p.Health.ApplyDamage(Punch(null, Vector3.back)), "blocked");
            Assert.AreEqual(100f, p.Health.Current);

            Assert.IsTrue(p.Health.ApplyDamage(Punch(null, Vector3.forward)));
            Assert.AreEqual(92f, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator DownedTeammateCanBeRevived()
        {
            Match.ModeOverride = "Duos";
            var a = SpawnPlayer(0, Vector3.zero, out _);
            var b = SpawnPlayer(1, new Vector3(1f, 0f, 0f), out _);
            yield return Frames(2);
            a.Team = b.Team = 0;
            var rules = Match.Rules.Clone();
            Assert.IsTrue(rules.DownedEnabled, "Duos has downed players");
            Assert.IsFalse(rules.FriendlyFire, "Duos has no friendly fire");
            rules.ReviveSeconds = 0.2f;
            a.Health.UseRules(rules);
            b.Health.UseRules(rules);

            Assert.IsFalse(b.Health.ApplyDamage(Punch(a, Vector3.right)), "teammates can't hurt each other");
            Assert.AreEqual(100f, b.Health.Current);

            Assert.IsTrue(b.Health.ApplyDamage(Lethal()));
            Assert.IsTrue(b.IsDowned);
            Assert.IsFalse(b.IsEliminated);
            Assert.IsFalse(b.CanAct);

            Assert.IsTrue(b.Health.BeginRevive(a));
            Assert.IsFalse(b.Health.TryFinishRevive(a), "reviving takes a moment");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(b.Health.TryFinishRevive(a));
            Assert.IsTrue(b.Health.IsAlive);
            Assert.AreEqual(rules.ReviveHealth, b.Health.Current);
        }

        [UnityTest]
        public IEnumerator PunchFromInputHitsOpponentInFront()
        {
            var attacker = SpawnPlayer(0, Vector3.zero, out var input);
            var victim = SpawnPlayer(1, new Vector3(0f, 0f, 1.1f), out _);
            yield return Frames(3);
            attacker.FaceTowards(Vector3.forward);
            victim.Inventory.Set("ABC");

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => victim.Health.Current < 100f, 1f, "unarmed damage window");
            Assert.AreEqual(92f, victim.Health.Current);
            Assert.AreEqual(3, victim.Inventory.Count);
        }

        [UnityTest]
        public IEnumerator PunchMissesOpponentBehind()
        {
            var attacker = SpawnPlayer(0, Vector3.zero, out var input);
            var victim = SpawnPlayer(1, new Vector3(0f, 0f, -1.1f), out _);
            yield return Frames(3);
            attacker.FaceTowards(Vector3.forward);

            input.Next.attack = true;
            yield return new WaitForSeconds(attacker.Health.Rules.Unarmed.Cycle + 0.1f);

            Assert.AreEqual(100f, victim.Health.Current);
        }

        [UnityTest]
        public IEnumerator FoamBubbleSoaksDamage()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("FOAMS");
            Assert.IsTrue(p.Summoner.Summon("FOAM"));
            p.Combat.Attack();
            yield return new WaitForSeconds(GameConfig.Current.Items.Get("FOAM").Use.ChannelSeconds + 0.1f);
            Assert.AreEqual(35f, p.Health.Bubble);

            p.Health.ApplyDamage(Punch(null, Vector3.forward));
            Assert.AreEqual(100f, p.Health.Current, "the bubble soaked the punch");
            Assert.AreEqual(27f, p.Health.Bubble);

            p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 40f, 1f));
            Assert.AreEqual(87f, p.Health.Current, "what the bubble can't soak gets through");
            Assert.AreEqual(0f, p.Health.Bubble);
            Assert.AreEqual(1, p.Inventory.Count, "the S stays");
        }

        [UnityTest]
        public IEnumerator EveryListedWordCanBeSummoned()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            var foe = SpawnPlayer(1, new Vector3(3f, 0f, 0f), out _);
            var listener = new GameObject("Test listener", typeof(AudioListener));
            yield return Frames(2);

            foreach (var entry in GameAssets.I.words.Words.Where(w => w.word.Length <= p.Inventory.Capacity))
            {
                p.Health.ResetForRound();
                foe.Health.ResetForRound();
                foe.Inventory.Set("AAAAAA");
                p.Inventory.Set(entry.word);
                Assert.IsTrue(p.Summoner.Summon(entry), $"summon {entry.word}");
                yield return Frames(3);
                p.Combat.ResetForRound();
                SummonedThing.ClearAll();
            }
            LogAssert.NoUnexpectedReceived();
            Object.Destroy(listener);
        }

        [UnityTest]
        public IEnumerator RoundEndsWhenOneRoommateIsLeft()
        {
            yield return SceneManager.LoadSceneAsync("LivingRoom");
            yield return null;
            var rounds = RoundManager.Instance;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var a = joins.Join(new ScriptedBinding());
            var b = joins.Join(new ScriptedBinding());
            rounds.CountdownTime = 0.1f;

            rounds.StartMatch();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Phase.Playing, rounds.Phase);
            Assert.AreEqual(Match.Rules.StarterLetters.Length, a.Inventory.Count, "configured starter letters");

            yield return TestScenes.WaitUntil(() => !b.Health.IsInvulnerable, 3f, "spawn protection to wear off");
            Assert.AreEqual(Phase.Playing, rounds.Phase);
            foreach (var opponent in joins.Players.Where(p => p != a && p != b)) opponent.Health.Eliminate();
            b.Health.ApplyDamage(Lethal(a));
            yield return null;
            Assert.AreEqual(Phase.RoundOver, rounds.Phase);
            Assert.AreEqual(1, rounds.WinsOf(a));
            Assert.AreEqual(0, rounds.WinsOf(b));

            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.WaitUntil(() => hud.ResultShown, 3f, "the round's result card");
            Assert.AreEqual(1, rounds.Round, "no round starts behind the card");
            hud.UiCanvas.transform.Find("Safe HUD/Result/Result card/Next").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(2, rounds.Round);
            Assert.That(rounds.Phase, Is.EqualTo(Phase.Countdown).Or.EqualTo(Phase.Playing));
            Assert.IsFalse(b.IsKnockedOut, "knocked-out players get back up for the next round");
        }
    }
}
