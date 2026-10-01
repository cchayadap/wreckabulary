using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>Integration invariants between catalogue data, crafting, physical gear and loot.</summary>
    public class CatalogueRuntimeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(int id = 0, Vector3 position = default)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, position, Quaternion.identity);
            player.Setup(id, new ScriptedBinding());
            player.Respawn(position);
            player.Inventory.Collects = false;
            return player;
        }

        static string Sorted(System.Collections.Generic.IEnumerable<char> letters) =>
            new string(letters.OrderBy(c => c).ToArray());

        [Test]
        public void LiveRecipesExactlyMatchTheEnabledCatalogue()
        {
            CollectionAssert.AreEquivalent(GameConfig.Current.Items.Enabled.Select(i => i.Id).ToArray(),
                GameAssets.I.words.Words.Select(w => w.word).ToArray());
            Assert.AreEqual(12, GameAssets.I.words.Words.Count);
            Assert.IsNull(GameAssets.I.words.Find("ARMOR"));
            Assert.IsNull(GameAssets.I.words.Find("AXE"));
        }

        [UnityTest]
        public IEnumerator DisabledRecipesAndFurnitureOverridesCannotSpendLetters()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("AXECHAIR");
            string before = Sorted(player.Inventory.Letters);
            Assert.IsFalse(player.Summoner.Summon("AXE"));
            player.Summoner.WordsOverride = new[] { new WordEntry { word = "CHAIR", category = WordCategory.Furniture } };
            Assert.IsFalse(player.Summoner.Summon("CHAIR"));
            Assert.AreEqual(before, Sorted(player.Inventory.Letters));
            Assert.IsFalse(player.Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator MovingDayPlacesEnabledUtilityItemsFromItsTrustedChecklist()
        {
            var player = Spawn();
            yield return null;
            Assert.AreEqual(ItemCategory.Utilities, GameConfig.Current.Items.Get("LAMP").Category);
            player.Summoner.WordsOverride = new[] { new WordEntry { word = "LAMP", category = WordCategory.Furniture } };
            player.Inventory.Set("LAMP");
            Assert.IsTrue(player.Summoner.Summon("LAMP"), "Moving Day's checked map can place LAMP as furniture");
            Assert.AreEqual(0, player.Inventory.Count);
            Assert.IsFalse(player.Combat.Weapon, "checklist furniture is placed, not equipped");
            Assert.AreEqual(1, World.Transient.GetComponentsInChildren<Smashable>().Count(s => s.Word == "LAMP"));
            player.Inventory.Set("BALL");
            Assert.IsFalse(player.Summoner.Summon("BALL"), "an enabled item outside the checklist is still refused");
            Assert.IsFalse(SummonEffects.Apply(player, new WordEntry { word = "BALL", category = WordCategory.Furniture }),
                "calling the effect directly cannot bypass the active recipe list");
            Assert.AreEqual("ABLL", Sorted(player.Inventory.Letters));
        }

        [UnityTest]
        public IEnumerator ExplicitChecklistOnlyPlacesItsObjectivesAndLeavesOtherRecipesAsGear()
        {
            var player = Spawn();
            yield return null;
            player.Summoner.WordsOverride = new[]
            {
                new WordEntry { word = "LAMP", category = WordCategory.Furniture },
                new WordEntry { word = "MAT", category = WordCategory.Furniture },
                new WordEntry { word = "AXE", category = WordCategory.Weapon },
            };
            player.Summoner.ChecklistPlacementWords = new[] { "LAMP" };
            player.Inventory.Set("MAT");
            Assert.IsTrue(player.Summoner.Summon("MAT"));
            Assert.AreEqual("MAT", player.Combat.Weapon.word);
            Assert.IsNotNull(player.Combat.Weapon.Definition.Deploy, "non-objective MAT remains a usable speed strip");
            Assert.IsFalse(World.Transient.GetComponentsInChildren<Smashable>().Any(s => s.Word == "MAT"));
            player.Inventory.Set("AXE");
            Assert.IsFalse(player.Summoner.Summon("AXE"), "overrides never enable disabled catalogue IDs");
            Assert.AreEqual("AEX", Sorted(player.Inventory.Letters));
        }

        [UnityTest]
        public IEnumerator CraftReservationCountsTowardsTheEighteenLetterLimitAndCancelsExactly()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BLADEXXXXXXXXXXXXX");
            Assert.AreEqual(18, player.Inventory.Count);
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BLADE")));
            Assert.AreEqual(13, player.Inventory.Count);
            Assert.AreEqual(5, player.Inventory.ReservedCount);
            Assert.AreEqual(18, player.Inventory.TotalCount);
            Assert.IsFalse(player.Inventory.TryAdd('Q'), "reserved letters occupy bag space");
            player.Summoner.CancelCraft();
            player.Summoner.CancelCraft();
            Assert.AreEqual(0, player.Inventory.ReservedCount);
            Assert.AreEqual(Sorted("BLADEXXXXXXXXXXXXX"), Sorted(player.Inventory.Letters));
        }

        [UnityTest]
        public IEnumerator CraftCompletesOnlyAfterTheAuthoredChannel()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BLADET");
            int completed = 0;
            player.Summoner.Summoned += _ => completed++;
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BLADE")));
            Assert.IsFalse(player.Combat.Weapon);
            Assert.AreEqual(player.Health.Rules.CraftMoveSpeed, player.MoveScale);
            yield return TestScenes.WaitUntil(() => !player.Summoner.IsCrafting, 3f, "catalogue craft completion");
            Assert.AreEqual("BLADE", player.Combat.Weapon.word);
            Assert.AreEqual("T", new string(player.Inventory.Letters.ToArray()), "the surplus letter remains in the bag");
            Assert.AreEqual(0, player.Inventory.ReservedCount);
            Assert.AreEqual(1, completed);
            Assert.AreEqual(1f, player.MoveScale);
        }

        [UnityTest]
        public IEnumerator AStaggeringHitRefundsTheReservation()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BLADET");
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BLADE")));
            player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 8f, 0f, hitStun: 0.15f));
            Assert.IsFalse(player.Summoner.IsCrafting);
            Assert.AreEqual(Sorted("BLADET"), Sorted(player.Inventory.Letters));
            Assert.AreEqual(0, player.Inventory.ReservedCount);
            Assert.IsFalse(player.Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator EliminationSpillsReservedAndAvailableLettersOnce()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BLADEAB");
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BLADE")));
            player.Health.Eliminate();
            player.Health.Eliminate();
            Assert.IsFalse(player.Summoner.IsCrafting);
            Assert.AreEqual(0, player.Inventory.TotalCount);
            Assert.AreEqual("AABBDEL", Sorted(TilePool.Instance.Active.Select(t => t.Letter)));
        }

        [UnityTest]
        public IEnumerator SlotsKeepItemIdentityAndRefuseAThirdCraft()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BATBLADEPLATE");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            Assert.AreSame(bat, player.Combat.StoredGear);
            Assert.IsFalse(player.Summoner.Summon("PLATE"));
            Assert.AreEqual(Sorted("PLATE"), Sorted(player.Inventory.Letters));
            Assert.IsTrue(player.Combat.SwitchGear());
            Assert.AreSame(bat, player.Combat.Weapon);
            Assert.AreSame(blade, player.Combat.StoredGear);
            player.Combat.Drop();
            Assert.AreSame(World.Transient, bat.transform.parent, "swapped gear is released into the room");
            Assert.AreEqual(Vector3.one, bat.transform.localScale);
            Assert.IsFalse(bat.GetComponent<Rigidbody>().isKinematic);
            Assert.AreSame(blade, player.Combat.StoredGear);
        }

        [UnityTest]
        public IEnumerator ReusableGearReturnsItsRecipeOnlyOnce()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BAT");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            bat.Break(player.Combat);
            bat.Break(player.Combat);
            Assert.AreEqual("ABT", Sorted(TilePool.Instance.Active.Select(t => t.Letter)));
            Assert.IsFalse(player.Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator BallThrowsUseTheCatalogueSpeedAndRemainRecoverable()
        {
            var player = Spawn();
            yield return null;
            player.FaceTowards(Vector3.forward);
            player.Inventory.Set("BALL");
            Assert.IsTrue(player.Summoner.Summon("BALL"));
            var ball = player.Combat.Weapon;
            player.Combat.Throw();
            Assert.IsFalse(ball.IsSpent);
            Assert.That(ball.GetComponent<Rigidbody>().linearVelocity.z, Is.EqualTo(ball.Definition.Thrown.Speed).Within(0.1f));
            Assert.That(ball.transform.localScale.x, Is.EqualTo(ball.Definition.HeldScale).Within(0.001f));
            Assert.AreSame(World.Transient, ball.transform.parent);
            ball.Break();
            Assert.AreEqual("ABLL", Sorted(TilePool.Instance.Active.Select(t => t.Letter)));
        }

        [UnityTest]
        public IEnumerator AnArmedBombWaitsForItsFuseAndNeverRefundsLetters()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("BOMB");
            Assert.IsTrue(player.Summoner.Summon("BOMB"));
            var bomb = player.Combat.Weapon;
            float fuse = bomb.Definition.Thrown.FuseSeconds;
            player.Combat.Throw();
            Assert.IsTrue(bomb.IsSpent);
            yield return new WaitForSeconds(fuse - 0.4f);
            Assert.IsTrue(bomb, "collision does not prematurely end the fuse");
            yield return TestScenes.WaitUntil(() => !bomb, 1f, "BOMB fuse explosion");
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
            Assert.AreEqual(0, player.Inventory.Count);
        }

        [UnityTest]
        public IEnumerator FoamConsumptionAndEffectCleanupNeverRefundLetters()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("FOAM");
            Assert.IsTrue(player.Summoner.Summon("FOAM"));
            player.Combat.Attack();
            yield return TestScenes.WaitUntil(() => player.Health.Bubble > 0f, 1f, "FOAM use channel");
            Assert.AreEqual(35f, player.Health.Bubble);
            Assert.IsFalse(player.Combat.Weapon);
            SummonedThing.ClearAll();
            Assert.AreEqual(0f, player.Health.Bubble);
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
        }

        [UnityTest]
        public IEnumerator DeployLimitAndConsumableExpiryPreserveLetterAccounting()
        {
            var player = Spawn();
            yield return null;
            var table = CatalogGear.Create(GameConfig.Current.Items.Get("TABLE"));
            table.transform.position = Vector3.right * 12f;
            table.GetComponent<Rigidbody>().isKinematic = true;
            DeployedGear.Attach(table, player);
            var sofa = CatalogGear.Create(GameConfig.Current.Items.Get("SOFA"));
            sofa.transform.position = Vector3.left * 12f;
            sofa.GetComponent<Rigidbody>().isKinematic = true;
            DeployedGear.Attach(sofa, player);
            player.Inventory.Set("SOAP");
            Assert.IsTrue(player.Summoner.Summon("SOAP"));
            var soap = player.Combat.Weapon;
            Assert.AreEqual(2, DeployedGear.CountFor(player));
            Assert.IsFalse(player.Combat.DeployHeld());
            Assert.AreSame(soap, player.Combat.Weapon);
            Assert.IsFalse(soap.IsSpent, "refusing placement never consumes gear");
            Object.Destroy(sofa.gameObject);
            yield return null;
            Assert.IsTrue(player.Combat.DeployHeld());
            yield return TestScenes.WaitUntil(() => !player.Combat.IsDeploying, 1f, "SOAP placement channel");
            Assert.IsFalse(player.Combat.Weapon);
            Assert.AreEqual(2, DeployedGear.CountFor(player));
            var patch = Object.FindObjectsByType<SummonedThing>().Single(s => s.Word == "SOAP");
            patch.Finish(SummonEndReason.Expired);
            patch.Finish(SummonEndReason.Expired);
            yield return null;
            Assert.AreEqual(1, DeployedGear.CountFor(player));
            Assert.AreEqual(0, TilePool.Instance.Active.Count, "SOAP is spent, not refunded when its zone expires");
        }

        [UnityTest]
        public IEnumerator StaggerCancelsPlacementWithoutConsumingTheItem()
        {
            var player = Spawn();
            yield return null;
            player.Inventory.Set("SOAP");
            Assert.IsTrue(player.Summoner.Summon("SOAP"));
            var soap = player.Combat.Weapon;
            Assert.IsTrue(player.Combat.DeployHeld());
            player.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Melee, 8f, 0f, hitStun: 0.15f));
            Assert.IsFalse(player.Combat.IsDeploying);
            Assert.AreSame(soap, player.Combat.Weapon);
            Assert.IsFalse(soap.IsSpent);
            Assert.AreEqual(0, DeployedGear.CountFor(player));
            Assert.AreEqual(1f, player.MoveScale);
        }

        [UnityTest]
        public IEnumerator UnarmedActiveWindowHitsEachBodyOnceAfterWindup()
        {
            var player = Spawn();
            var target = Spawn(1, Vector3.forward * 1.1f);
            yield return null;
            player.FaceTowards(Vector3.forward);
            var rules = player.Health.Rules.Clone();
            rules.Unarmed = new MeleeStats { Damage = 8f, Reach = 1.4f, ArcDegrees = 90f,
                Windup = 0.5f, Active = 0.25f, Recovery = 0.1f, Knockback = 0f, HitStun = 0f };
            player.Health.UseRules(rules);
            player.Body.constraints = target.Body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            player.Combat.Attack();
            Assert.AreEqual(100f, target.Health.Current, "windup does not deal damage");
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(100f, target.Health.Current);
            yield return TestScenes.WaitUntil(() => !player.Combat.IsChanneling, 2f, "unarmed active and recovery windows");
            Assert.AreEqual(92f, target.Health.Current, "remaining in the active arc never multiplies damage");
        }

        [UnityTest]
        public IEnumerator StaggerPermanentlyCancelsAMeleeWindup()
        {
            var player = Spawn();
            var target = Spawn(1, Vector3.forward * 1.1f);
            yield return null;
            player.FaceTowards(Vector3.forward);
            player.Inventory.Set("BLADE");
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            player.Combat.Attack();
            player.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Melee, 8f, 0f, hitStun: 0.05f));
            yield return new WaitForSeconds(blade.Stats.Cycle + 0.1f);
            Assert.AreEqual(100f, target.Health.Current, "an interrupted windup cannot resume after the short stagger ends");
            Assert.AreEqual(blade.Definition.Durability, blade.DurabilityLeft);
        }

        [TestCase(0f, 45f)]
        [TestCase(1.5f, 30f)]
        [TestCase(3f, 15f)]
        public void BombDamageUsesAuthoredLinearFalloff(float distance, float expected)
        {
            var bomb = GameConfig.Current.Items.Get("BOMB").Thrown;
            Assert.AreEqual(expected, Projectile.BlastDamage(bomb.Damage, bomb.EdgeDamage, distance, bomb.Radius), 0.001f);
        }

        [UnityTest]
        public IEnumerator ExplosionDamagesAMultiColliderPlayerOnce()
        {
            var player = Spawn();
            yield return null;
            var extra = new GameObject("Extra collider");
            extra.transform.SetParent(player.transform, false);
            extra.transform.localPosition = Vector3.up * 0.8f;
            extra.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Projectile.ExplodeAt(player.transform.position, null, "BOMB", 10f, 10f, 3f, 0f, 0f, true);
            Assert.AreEqual(90f, player.Health.Current);
        }

        [UnityTest]
        public IEnumerator ResetRunsCleanupOnceAndDoesNotCreateLoot()
        {
            var effect = new GameObject("Lifecycle test effect");
            var life = SummonedThing.Attach(effect, "BAT", null, 10f);
            int cleanup = 0;
            SummonEndReason? reason = null;
            life.Ended += () => cleanup++;
            life.EndedWithReason += r => reason = r;
            SummonedThing.ClearAll();
            SummonedThing.ClearAll();
            Assert.AreEqual(SummonEndReason.Reset, reason);
            Assert.AreEqual(1, cleanup);
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
            yield return null;
            Assert.AreEqual(1, cleanup, "OnDestroy cannot repeat cleanup");
        }
    }
}
