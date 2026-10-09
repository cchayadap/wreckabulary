using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class PowerRecipeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            TilePool.Ensure();
        }

        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

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

        static HeldWeapon Equip(PlayerController player, string word)
        {
            player.Inventory.Set(word);
            Assert.IsTrue(player.Summoner.Summon(word), word);
            Assert.AreEqual(word, player.Combat.Weapon.word);
            return player.Combat.Weapon;
        }

        static HeldWeapon Deploy(PlayerController owner, string word, Vector3 at, float lifetime = -1f)
        {
            var item = GameConfig.Current.Items.Get(word);
            if (lifetime >= 0f)
            {
                var stats = item.Deploy;
                item = new ItemDefinition
                {
                    Id = item.Id, Model = item.Model, Size = item.Size, Grip = item.Grip, HeldScale = item.HeldScale,
                    Durability = item.Durability, Hands = item.Hands, Family = item.Family,
                    Deploy = new DeployStats { Effect = stats.Effect, Radius = stats.Radius, Strength = stats.Strength,
                        ArcDegrees = stats.ArcDegrees, FootprintX = stats.FootprintX, FootprintZ = stats.FootprintZ,
                        PlaceSeconds = stats.PlaceSeconds, LifetimeSeconds = lifetime }
                };
            }
            var gear = CatalogGear.Create(item);
            gear.transform.SetPositionAndRotation(at, Quaternion.identity);
            var body = gear.GetComponent<Rigidbody>();
            body.position = at;
            body.rotation = Quaternion.identity;
            body.isKinematic = true;
            DeployedGear.Attach(gear, owner);
            Physics.SyncTransforms();
            return gear;
        }

        static string Letters() => new(TilePool.Instance.Active.Select(tile => tile.Letter).OrderBy(c => c).ToArray());

        [UnityTest]
        public IEnumerator FoodAndWaterHealAfterUseWithoutExceedingMaxOrReturningLetters()
        {
            var player = Spawn(0, Vector3.zero);
            yield return null;
            foreach (string word in new[] { "APPLE", "WATER", "CAKE" })
            {
                player.Health.Heal(100f);
                float damage = word == "APPLE" ? 10f : 65f;
                player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, damage, 0f, hitStun: 0f));
                float expected = Mathf.Min(100f, player.Health.Current + GameConfig.Current.Items.Get(word).Use.Amount);
                var gear = Equip(player, word);
                player.Combat.Attack();
                Assert.IsTrue(gear.IsUsing);
                Assert.That(player.Health.Current, Is.LessThan(expected), "Healing waits for the channel.");
                yield return TestScenes.WaitUntil(() => !gear, 2f, word + " consumed");
                Assert.AreEqual(expected, player.Health.Current);
                Assert.IsFalse(player.Combat.Weapon);
                Assert.AreEqual(0, player.Inventory.Count);
                Assert.AreEqual("", Letters());
            }
        }

        [UnityTest]
        public IEnumerator InterruptedCakeStaysOwnedAndNeverHealsOrDuplicatesItsRecipe()
        {
            var player = Spawn(0, Vector3.zero);
            yield return null;
            player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 30f, 0f, hitStun: 0f));
            var cake = Equip(player, "CAKE");
            player.Combat.Attack();
            yield return new WaitForSeconds(.15f);
            player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 8f, 0f, hitStun: .2f));
            yield return new WaitForSeconds(1.2f);
            Assert.AreSame(cake, player.Combat.Weapon);
            Assert.IsFalse(cake.IsUsing);
            Assert.IsFalse(cake.IsSpent);
            Assert.AreEqual(62f, player.Health.Current);
            Assert.AreEqual("", Letters());
            cake.Break(player.Combat);
            cake.Break(player.Combat);
            Assert.AreEqual("ACEK", Letters(), "Only breaking the still-owned, unused item returns its recipe.");
        }

        [UnityTest]
        public IEnumerator SodaAndMatKeepSeparateExpiriesAndClockComposesWithBoth()
        {
            var player = Spawn(0, Vector3.zero);
            var input = (ScriptedBinding)player.Binding;
            input.Next.move = Vector2.right;
            yield return new WaitForSeconds(.5f);
            float ordinary = World.Flat(player.Body.linearVelocity).magnitude;
            Assert.Greater(ordinary, 5f);
            var soda = Equip(player, "SODA");
            player.Combat.Attack();
            yield return TestScenes.WaitUntil(() => !soda, 1f, "SODA channel");
            Assert.Greater(player.BoostLeft, 5.5f);
            player.Boost(GameConfig.Current.Items.Get("MAT").Deploy.Strength, 1.5f);
            player.MoveScale = .5f;
            player.Slow(.6f, .8f);
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(ordinary * 1.6f * .5f * .6f, World.Flat(player.Body.linearVelocity).magnitude, .25f);
            player.Boost(1.35f, 6f);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(ordinary * 1.6f * .5f * .6f, World.Flat(player.Body.linearVelocity).magnitude, .25f,
                "SODA must not replace the stronger MAT modifier.");
            float boost = player.BoostLeft, slow = player.SlowLeft;
            Time.timeScale = 0f;
            yield return null; yield return null; yield return null;
            Assert.AreEqual(boost, player.BoostLeft, .001f);
            Assert.AreEqual(slow, player.SlowLeft, .001f);
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1.3f);
            Assert.AreEqual(0f, player.SlowLeft);
            Assert.Greater(player.BoostLeft, 3f);
            Assert.AreEqual(ordinary * 1.35f * .5f, World.Flat(player.Body.linearVelocity).magnitude, .25f,
                "MAT expires after its own 1.5 seconds, leaving the remaining SODA boost.");
            player.Slow(.6f, 1f);
            player.Respawn(Vector3.zero);
            Assert.AreEqual(0f, player.SlowLeft);
            Assert.AreEqual(0f, player.BoostLeft);
            Assert.AreEqual(1f, player.MoveScale);
        }

        [UnityTest]
        public IEnumerator RaisedShieldBlocksAllDirectionsAndItsAuraEndsWithTheGuard()
        {
            var player = Spawn(0, Vector3.zero);
            yield return null;
            var shield = Equip(player, "SHIELD");
            var aura = shield.GetComponent<HeldShieldAura>();
            Assert.IsNotNull(aura);
            var input = (ScriptedBinding)player.Binding;
            input.Next.blockHeld = true;
            yield return new WaitForSeconds(.3f);
            Assert.IsTrue(player.Combat.IsBlocking);
            Assert.IsTrue(aura.Visible);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Assert.IsFalse(player.Health.ApplyDamage(Hits.Of(null, new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)),
                    HitSource.Melee, 5f, 0f, hitStun: 0f)), "direction " + i);
            }
            Assert.AreEqual(100f, player.Health.Current);
            Assert.AreEqual(50f, shield.DurabilityLeft);
            yield return null;
            Assert.Greater(aura.Aura.localScale.x, 1f, "A landed block flashes the segmented guard.");
            input.Next.blockHeld = false;
            yield return null; yield return null;
            Assert.IsFalse(aura.Visible);
            Assert.IsTrue(player.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 8f, 0f, hitStun: 0f)));
            Assert.AreEqual(92f, player.Health.Current);
            shield.Break(player.Combat); shield.Break(player.Combat);
            Assert.AreEqual("DEHILS", Letters());
            yield return null;
            Assert.IsNull(player.transform.Find("Raised shield aura"));
        }

        [UnityTest]
        public IEnumerator FanPushesForwardPlayersAndLooseLettersButNotThroughWallsOrFloors()
        {
            var owner = Spawn(0, new Vector3(0f, 0f, -1.4f));
            var front = Spawn(1, new Vector3(0f, 0f, 1.4f));
            var side = Spawn(2, new Vector3(2f, 0f, .5f));
            var above = Spawn(3, new Vector3(0f, 3f, 1.4f));
            above.Body.constraints |= RigidbodyConstraints.FreezePositionY;
            Deploy(owner, "FAN", Vector3.zero);
            var tile = TilePool.Instance.Get('A');
            tile.Launch(new Vector3(-.6f, .35f, 1.4f), Vector3.zero);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.55f);
            Assert.Greater(front.transform.position.z, 1.75f, "Wind must overcome normal player ground friction.");
            Assert.AreEqual(-1.4f, owner.transform.position.z, .08f, "The area behind the fan is safe.");
            Assert.AreEqual(.5f, side.transform.position.z, .08f, "Outside the cone.");
            Assert.AreEqual(1.4f, above.transform.position.z, .08f, "A different storey is outside the field.");
            Assert.Greater(tile.Body.position.z, 1.45f);
            Assert.AreEqual("A", Letters(), "Wind moves physical letters without taking or duplicating them.");
            front.Respawn(new Vector3(0f, 0f, 1.4f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, .8f);
            wall.transform.localScale = new Vector3(1.6f, 2f, .15f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(1.4f, front.transform.position.z, .08f, "A solid wall blocks wind.");
        }

        [UnityTest]
        public IEnumerator ClockIsRadialWallBoundedAndStopsSoonAfterPickup()
        {
            var owner = Spawn(0, Vector3.right * 1.1f);
            var blocked = Spawn(1, Vector3.forward * 2f);
            var upper = Spawn(2, new Vector3(0f, 3f, 1f));
            var back = Spawn(3, Vector3.left * 1.1f);
            foreach (var player in new[] { owner, blocked, upper, back }) player.Body.constraints = RigidbodyConstraints.FreezeAll;
            var clock = Deploy(owner, "CLOCK", Vector3.zero);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 1f);
            wall.transform.localScale = new Vector3(1.6f, 2f, .15f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.Greater(owner.SlowLeft, 0f);
            Assert.Greater(back.SlowLeft, 0f, "The clock acts in all directions.");
            Assert.AreEqual(0f, blocked.SlowLeft);
            Assert.AreEqual(0f, upper.SlowLeft);
            Assert.IsTrue(owner.Combat.TryEquip(clock));
            yield return new WaitForSeconds(.25f);
            Assert.IsNull(clock.GetComponent<DeployedPowerField>());
            Assert.AreEqual(0f, owner.SlowLeft);
            Assert.AreEqual(0f, back.SlowLeft);
            Assert.AreEqual(0, DeployedGear.CountFor(owner));
            Assert.AreEqual("", Letters());
        }

        [UnityTest]
        public IEnumerator FieldVisualsResumeAfterTemporaryDisableAndAreRemovedOnPickup()
        {
            var owner = Spawn(0, new Vector3(-4f, 0f, -4f));
            yield return null;
            foreach (string word in new[] { "FAN", "CLOCK" })
            {
                var gear = Deploy(owner, word, Vector3.zero);
                var field = gear.GetComponent<DeployedPowerField>();
                var visual = field.VisualRoot;
                Assert.IsTrue(visual.gameObject.activeInHierarchy);

                field.enabled = false;
                yield return null;
                Assert.AreSame(visual, field.VisualRoot);
                Assert.IsFalse(visual.gameObject.activeInHierarchy);
                field.enabled = true;
                yield return null;
                Assert.AreSame(visual, field.VisualRoot, "Restoring a temporarily disabled behaviour reuses its field visual.");
                Assert.IsTrue(visual.gameObject.activeInHierarchy);

                gear.gameObject.SetActive(false);
                yield return null;
                Assert.IsTrue(visual);
                Assert.IsFalse(visual.gameObject.activeInHierarchy);
                gear.gameObject.SetActive(true);
                yield return null;
                Assert.AreSame(visual, field.VisualRoot, "Returning from a hidden world must preserve its deployed presentation.");
                Assert.IsTrue(visual.gameObject.activeInHierarchy);
                Assert.AreEqual(1, gear.GetComponents<DeployedPowerField>().Length);
                Assert.AreEqual(1, DeployedGear.CountFor(owner));

                Assert.IsTrue(owner.Combat.TryEquip(gear));
                yield return TestScenes.WaitUntil(() => !field && !visual, .25f, word + " field pickup cleanup");
                Assert.IsFalse(field);
                Assert.IsFalse(visual, "Picking up the item removes the deployed visual permanently.");
                Assert.AreEqual(0, DeployedGear.CountFor(owner));
                Assert.AreEqual("", Letters());
                owner.Combat.ResetForRound();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ReusableFieldExpiryReturnsLettersOnceAndPickupCancelsTheTimer()
        {
            var owner = Spawn(0, new Vector3(-4f, 0f, -4f));
            yield return null;
            foreach (string word in new[] { "FAN", "CLOCK" })
            {
                var expires = Deploy(owner, word, Vector3.zero, .15f);
                yield return TestScenes.WaitUntil(() => !expires, .7f, word + " expiry");
                Assert.AreEqual(new string(word.OrderBy(c => c).ToArray()), Letters());
                Assert.AreEqual(0, DeployedGear.CountFor(owner));
                TilePool.Instance.ReleaseAll();
                var recovered = Deploy(owner, word, Vector3.zero, .15f);
                Assert.IsTrue(owner.Combat.TryEquip(recovered));
                yield return new WaitForSeconds(.3f);
                Assert.IsTrue(recovered, "A recovered reusable item must not expire in the player's hand.");
                Assert.IsNull(recovered.GetComponent<DeployedPowerField>());
                Assert.AreEqual("", Letters());
                owner.Combat.ResetForRound();
                yield return null;
                Assert.AreEqual("", Letters(), "Round reset is not a second recipe refund.");
            }
        }

        [UnityTest]
        public IEnumerator MatShowsMovingArrowsAndBoostsOnlyAlongTheirDirection()
        {
            var player = Spawn(0, Vector3.up * .08f);
            var mat = Deploy(player, "MAT", Vector3.zero);
            var arrow = mat.transform.Find("Ability marker/Direction chevron");
            Assert.IsNotNull(arrow);
            yield return null;
            var before = arrow.localPosition;
            var input = (ScriptedBinding)player.Binding;
            input.Next.move = Vector2.up;
            yield return new WaitForSeconds(.18f);
            Assert.Greater(player.BoostLeft, 1f);
            Assert.Greater(Vector3.Distance(before, arrow.localPosition), .01f);
            player.Respawn(Vector3.up * .08f);
            input.Next.move = Vector2.down;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(0f, player.BoostLeft, "Running against the arrows does not refresh the strip boost.");
        }

        [UnityTest]
        public IEnumerator PieIsSpentAtThrowDealsOneHitAndNeverReturnsRecipeLetters()
        {
            var owner = Spawn(0, Vector3.zero);
            var target = Spawn(1, Vector3.forward * 3f);
            target.Body.constraints = RigidbodyConstraints.FreezeAll;
            yield return null;
            owner.FaceTowards(Vector3.forward);
            var pie = Equip(owner, "PIE");
            owner.Combat.Throw();
            Assert.IsTrue(pie.IsSpent);
            Assert.IsFalse(owner.Combat.TryEquip(pie));
            var body = pie.GetComponent<Rigidbody>();
            body.position = pie.transform.position = new Vector3(0f, .8f, 1.2f);
            body.linearVelocity = Vector3.forward * 13f;
            Physics.SyncTransforms();
            yield return TestScenes.WaitUntil(() => !pie, 1f, "PIE impact");
            Assert.AreEqual(92f, target.Health.Current);
            yield return new WaitForSeconds(.15f);
            Assert.AreEqual(92f, target.Health.Current);
            Assert.AreEqual("", Letters());
            owner.FaceTowards(Vector3.back);
            var missed = Equip(owner, "PIE");
            owner.Combat.Throw();
            var missedBody = missed.GetComponent<Rigidbody>();
            missedBody.useGravity = false;
            missedBody.position = missed.transform.position = Vector3.up * 6f;
            missedBody.linearVelocity = Vector3.up * 13f;
            yield return TestScenes.WaitUntil(() => !missed, 2.5f, "missed PIE cleanup");
            Assert.AreEqual("", Letters());
        }
    }
}
