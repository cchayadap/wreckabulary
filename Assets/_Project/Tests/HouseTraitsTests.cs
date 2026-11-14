using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class HouseTraitsTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerHealth.TraitRoll = null;
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static HouseTuning Tuning => GameConfig.Current.Tuning;

        static PlayerController Spawn(int seat, Vector3 at)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, new ScriptedBinding());
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.LettersDroppedPerHit = 0;
            player.Health.UseRules(rules);
            player.Respawn(at);
            player.Inventory.Collects = false;
            return player;
        }

        static HitInfo Blow(PlayerController attacker, string item, float damage, float knockback = 0f) =>
            Hits.Of(attacker, Vector3.forward, HitSource.Melee, damage, knockback, 0f, 1f, item);

        [UnityTest]
        public IEnumerator ACutBleedsForItsTimeAndCreditsTheCutter()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var bleed = Tuning.Trait("BLADE", TraitKind.Bleed);
            Assert.IsNotNull(bleed);
            PlayerController credited = null;
            int ticks = 0;
            victim.Health.Damaged += (_, hit, _) => { if (hit.Source == HitSource.Bleed) { ticks++; credited = Hits.Attacker(hit); } };
            Assert.IsTrue(victim.Health.ApplyDamage(Blow(attacker, "BLADE", 10f)));
            Assert.AreEqual(90f, victim.Health.Current, 0.01f);
            Assert.Greater(victim.Health.BleedingLeft, 0f, "the cut keeps bleeding");
            Assert.IsNull(credited, "no tick yet");
            yield return new WaitForSeconds(bleed.Seconds + 0.6f);
            Assert.AreEqual(90f - bleed.Amount * bleed.Seconds, victim.Health.Current, 0.01f, "bleeds exactly its amount, then stops");
            Assert.Greater(ticks, 0);
            Assert.AreSame(attacker, credited, "each bleed tick credits the cutter");
            Assert.AreEqual(0f, victim.Health.BleedingLeft);
            Assert.IsFalse(victim.IsStaggered, "bleeding does not stagger");
        }

        [UnityTest]
        public IEnumerator HeavyItemsPushFurtherThanPlainOnes()
        {
            var attacker = Spawn(0, Vector3.zero);
            var plain = Spawn(1, new Vector3(4f, 0f, 0f));
            var crushed = Spawn(2, new Vector3(-4f, 0f, 0f));
            yield return null;
            var heavy = Tuning.Trait("HAMMER", TraitKind.Heavy);
            Assert.IsNotNull(heavy);
            CollectionAssert.IsEmpty(Tuning.TraitsOf("BAT"));
            plain.Body.linearVelocity = crushed.Body.linearVelocity = Vector3.zero;
            plain.Health.ApplyDamage(Blow(attacker, "BAT", 5f, 4f));
            crushed.Health.ApplyDamage(Blow(attacker, "HAMMER", 5f, 4f));
            Assert.AreEqual(heavy.Amount, crushed.Body.linearVelocity.magnitude / plain.Body.linearVelocity.magnitude, 0.02f);
        }

        [UnityTest]
        public IEnumerator DazingBlowsSlowTheTarget()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var daze = Tuning.Trait("SPEAR", TraitKind.Daze);
            Assert.IsNotNull(daze);
            Assert.AreEqual(TraitDelivery.Chance, daze.Delivery);
            PlayerHealth.TraitRoll = () => 0f;
            victim.Health.ApplyDamage(Blow(attacker, "BAT", 5f));
            Assert.AreEqual(0f, victim.SlowLeft, "a plain blow does not daze");
            victim.Health.ApplyDamage(Blow(attacker, "SPEAR", 5f));
            Assert.AreEqual(daze.Seconds, victim.SlowLeft, 0.05f);
        }

        [UnityTest]
        public IEnumerator AChanceTraitLandsOnlyWhenTheRollIsUnderItsChance()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var daze = Tuning.Trait("SPEAR", TraitKind.Daze);
            PlayerHealth.TraitRoll = () => daze.Chance + .01f;
            victim.Health.ApplyDamage(Blow(attacker, "SPEAR", 5f));
            Assert.AreEqual(0f, victim.SlowLeft, "the roll missed");
            PlayerHealth.TraitRoll = () => daze.Chance - .01f;
            victim.Health.ApplyDamage(Blow(attacker, "SPEAR", 5f));
            Assert.AreEqual(daze.Seconds, victim.SlowLeft, 0.05f, "the roll landed");
        }

        [UnityTest]
        public IEnumerator ABuildUpLandsOnEveryNthBlowPerAttackerAndResetsWhenTheTargetFalls()
        {
            var attacker = Spawn(0, Vector3.zero);
            var other = Spawn(2, new Vector3(-4f, 0f, 0f));
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var daze = Tuning.Trait("RACK", TraitKind.Daze);
            Assert.AreEqual(TraitDelivery.BuildUp, daze.Delivery);
            for (int i = 1; i < daze.Every; i++)
            {
                victim.Health.ApplyDamage(Blow(attacker, "RACK", 2f));
                victim.Health.ApplyDamage(Blow(other, "RACK", 2f));
                Assert.AreEqual(0f, victim.SlowLeft, "blow " + i + " only builds up");
            }
            victim.Health.ApplyDamage(Blow(attacker, "RACK", 2f));
            Assert.AreEqual(daze.Seconds, victim.SlowLeft, 0.05f, "the attacker's own count reached it");
            victim.Respawn(new Vector3(4f, 0f, 0f));
            victim.Health.ResetForRound();
            yield return null;
            victim.Health.ApplyDamage(Blow(other, "RACK", 2f));
            Assert.AreEqual(0f, victim.SlowLeft, "counts start again after the target resets");
        }

        [UnityTest]
        public IEnumerator ACrushLandsHarderOnItsCount()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var crush = Tuning.Trait("HAMMER", TraitKind.Crush);
            Assert.IsNotNull(crush);
            float health = victim.Health.Current;
            for (int i = 1; i <= crush.Every; i++)
            {
                victim.Health.ApplyDamage(Blow(attacker, "HAMMER", 4f));
                float taken = health - victim.Health.Current;
                health = victim.Health.Current;
                Assert.AreEqual(i == crush.Every ? 4f * crush.Amount : 4f, taken, 0.01f, "blow " + i);
            }
        }

        [UnityTest]
        public IEnumerator ABubbleStillCountsTheBuildUp()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var crush = Tuning.Trait("HAMMER", TraitKind.Crush);
            victim.Health.GiveBubble(500f, 30f);
            int crushes = 0, blows = 0;
            victim.Health.Damaged += (_, _, r) => { blows++; if (r.Absorbed > 4.01f) crushes++; };
            for (int i = 0; i < crush.Every + 1; i++) victim.Health.ApplyDamage(Blow(attacker, "HAMMER", 4f));
            Assert.AreEqual(crush.Every + 1, blows);
            Assert.AreEqual(1, crushes, "one crush per count even while a bubble soaks the blows");
            Assert.AreEqual(100f, victim.Health.Current, 0.01f);
        }

        [UnityTest]
        public IEnumerator OnlyRealBlowsBuildUpAndGuardIgnoresHazards()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var crush = Tuning.Trait("HAMMER", TraitKind.Crush);
            float health = victim.Health.Current;
            for (int i = 0; i < crush.Every * 2; i++)
                victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Explosion, 2f, 0f, 0f, 1f, "HAMMER"));
            Assert.AreEqual(health - 2f * crush.Every * 2, victim.Health.Current, 0.01f, "explosions never crush");
            victim.Inventory.Set("MIRROR");
            Assert.IsTrue(victim.Summoner.Summon("MIRROR"));
            float before = victim.Health.Current;
            victim.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Hazard, 10f, 0f, hitStun: 0f));
            Assert.AreEqual(before - 10f, victim.Health.Current, 0.01f, "guard softens blows, not hazards");
        }

        [UnityTest]
        public IEnumerator CutsDeepenUpToTheirStacks()
        {
            var attacker = Spawn(0, Vector3.zero);
            var victim = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            var bleed = Tuning.Trait("BLADE", TraitKind.Bleed);
            Assert.Greater(bleed.Stacks, 1);
            for (int i = 1; i <= bleed.Stacks + 1; i++)
            {
                victim.Health.ApplyDamage(Blow(attacker, "BLADE", 1f));
                Assert.AreEqual(Mathf.Min(i, bleed.Stacks), victim.Health.BleedStacks, "cut " + i);
            }
            float tick = 0f;
            victim.Health.Damaged += (_, hit, result) => { if (hit.Source == HitSource.Bleed && tick == 0f) tick = result.Damage; };
            yield return TestScenes.WaitUntil(() => tick > 0f, 2f, "a bleed tick");
            Assert.AreEqual(bleed.Amount * bleed.Stacks * .5f, tick, 0.001f, "a deep cut bleeds its stacked amount each half second");
        }

        [UnityTest]
        public IEnumerator GuardAndSwiftLastWhileTheItemIsHeld()
        {
            var p = Spawn(0, Vector3.zero);
            var q = Spawn(1, new Vector3(4f, 0f, 0f));
            yield return null;
            Assert.AreEqual(1f, p.HeldSwift);
            var swift = Tuning.Trait("PLATE", TraitKind.Swift);
            var guard = Tuning.Trait("MIRROR", TraitKind.Guard);
            Assert.IsNotNull(swift); Assert.IsNotNull(guard);
            p.Inventory.Set("PLATE");
            Assert.IsTrue(p.Summoner.Summon("PLATE"));
            Assert.AreEqual(swift.Amount, p.HeldSwift, 1e-4f, "quicker while holding it");
            q.Inventory.Set("MIRROR");
            Assert.IsTrue(q.Summoner.Summon("MIRROR"));
            Assert.IsTrue(q.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 10f, 0f, hitStun: 0f)));
            Assert.AreEqual(100f - 10f * (1f - guard.Amount), q.Health.Current, 0.01f, "guarded even when not blocking");
        }

        [Test]
        public void FistsBarelyHurtInHouseModes()
        {
            var shared = RuleBook.FromJson(Resources.Load<GameData>(GameData.ResourcePath).rules.text);
            Assert.AreEqual(Tuning.UnarmedDamage.Value, Match.Rules.Unarmed.Damage, "House fists come from items_house.json tuning");
            Assert.Less(Match.Rules.Unarmed.Damage, shared.Defaults.Unarmed.Damage, "spell a weapon to really fight");
            Assert.AreEqual(shared.Defaults.Unarmed.BreakPower, Match.Rules.Unarmed.BreakPower, "punching furniture for letters is unchanged");
        }
    }
}
