using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class HouseItemsRuntimeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Test backstop";
            wall.transform.position = new Vector3(0f, 1.5f, 14f);
            wall.transform.localScale = new Vector3(30f, 3f, 0.5f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static ItemCatalogue House()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsTrue(data && data.houseItems, "GameData has no items_house.json. Run Wreckabulary > Data > Set Up Game Data.");
            return ItemCatalogue.FromJson(data.houseItems.text, "items_house.json");
        }

        static IEnumerable<string> Ids(params HandlingFamily[] families) =>
            House().All.Where(i => families.Contains(i.Family)).Select(i => i.Id).ToArray();

        static IEnumerable<string> MeleeItems => Ids(HandlingFamily.MeleeSwing, HandlingFamily.MeleeThrust);
        static IEnumerable<string> ThrownItems => Ids(HandlingFamily.Thrown);
        static IEnumerable<string> ShieldItems => Ids(HandlingFamily.Shield);
        static readonly HandlingFamily[] DeployFamilies = { HandlingFamily.DeployCover, HandlingFamily.DeployPad, HandlingFamily.DeploySpeed, HandlingFamily.DeployZone };
        static IEnumerable<string> DeployItems => House().All.Where(i => DeployFamilies.Contains(i.Family) && !i.Consumable).Select(i => i.Id).ToArray();
        static IEnumerable<string> PatchItems => House().All.Where(i => DeployFamilies.Contains(i.Family) && i.Consumable).Select(i => i.Id).ToArray();
        static IEnumerable<string> SwiftItems => GameConfig.Current.Items.Enabled.Where(i => GameConfig.Current.Tuning?.Trait(i.Id, TraitKind.Swift) != null).Select(i => i.Id).ToArray();
        static IEnumerable<string> UseItems => Ids(HandlingFamily.Heal, HandlingFamily.Buff);
        static IEnumerable<string> RangedItems => Ids(HandlingFamily.Ranged);
        static IEnumerable<string> StashItems => Ids(HandlingFamily.Utility);
        static IEnumerable<string> PosedItems =>ItemHolds.All.Select(h => h.Id).ToArray();
        static IEnumerable<string> TurnedShieldItems => ShieldItems.Where(id => ItemHolds.For(id) != null).ToArray();

        static PlayerController Spawn(int seat, Vector3 at, out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, input);
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.LettersDroppedPerHit = 0;
            player.Health.UseRules(rules);
            player.Respawn(at);
            player.Inventory.Collects = false;
            return player;
        }

        static ItemDefinition Live(string id) => GameConfig.Current.Items.Get(id);

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static HeldWeapon Craft(PlayerController player, string id)
        {
            var item = Live(id);
            var authored = House().TryGet(id, out var house) ? house : item;
            player.Inventory.Set(id);
            Assert.IsTrue(player.Summoner.Summon(id), id + " is spelled from its letters");
            var held = player.Combat.Weapon;
            Assert.IsNotNull(held, id + " is in hand");
            Assert.AreEqual(id, held.word);
            Assert.AreSame(item, held.Definition, id + " uses the live catalogue entry");
            Assert.IsTrue(held.Definition.Enabled);
            Assert.AreEqual(authored.Family, held.Definition.Family, id + " plays as items_house.json says");
            Assert.AreEqual(authored.Hands, held.Definition.Hands, id);
            Assert.AreEqual(0, player.Inventory.Count, id + " spends exactly its letters");
            Assert.That(held.transform.localScale.x, Is.EqualTo(item.HeldScale).Within(0.001f), id + " held at its miniature scale");
            return held;
        }

        [Test]
        public void HouseItemsAreLiveInHouseModesButStayOutOfTheSharedCatalogue()
        {
            var shared = ItemCatalogue.FromJson(Resources.Load<GameData>(GameData.ResourcePath).items.text, "items.json");
            var house = House();
            var live = GameConfig.Current.Items;
            Assert.IsNotEmpty(house.All);
            foreach (var item in house.All)
            {
                Assert.IsFalse(shared.TryGet(item.Id, out var wordbound) && wordbound.Enabled, item.Id + " stays out of the Wordbound and online catalogue");
                Assert.IsTrue(live.TryGet(item.Id, out var runtime) && runtime.Enabled, item.Id + " is craftable in House modes");
                Assert.AreEqual(item.Family, runtime.Family, item.Id);
                Assert.IsNotNull(GameAssets.I.words.Find(item.Id), item.Id + " is on the word wheel");
            }
            Assert.AreEqual(shared.Enabled.Count() + house.All.Count, live.Enabled.Count());
        }

        [Test]
        public void EveryHouseItemHasAFamilyCase()
        {
            CollectionAssert.AreEquivalent(House().All.Select(i => i.Id),
                MeleeItems.Concat(ThrownItems).Concat(ShieldItems).Concat(DeployItems).Concat(PatchItems).Concat(UseItems).Concat(RangedItems).Concat(StashItems),
                "add a runtime case for any new handling family");
        }

        [UnityTest]
        public IEnumerator EveryHouseItemIsSpelledIntoTheHandInTheTutorial()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            yield return TestScenes.WaitUntil(() => p.CanAct && !p.IsDodging, 3f, "the tutorial roommate");
            Assert.AreEqual("Tutorial", Match.Mode);
            foreach (var item in House().All)
            {
                p.Combat.ResetForRound();
                Craft(p, item.Id);
                yield return null;
            }
            p.Combat.ResetForRound();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PosedItemsKeepTheirHoldTurnAndGripThroughACancelledUse([ValueSource(nameof(PosedItems))] string id)
        {
            var p = Spawn(0, Vector3.zero, out _);
            yield return Frames(3);
            var held = Craft(p, id);
            var item = held.Definition;
            var turn = HeldItemPose.Turn(item);
            yield return null;
            yield return null;
            Assert.Less(Quaternion.Angle(held.HoldRotation * turn, held.transform.localRotation), .5f, id + " rests in its hold turn");
            Assert.Less(Vector3.Distance(held.transform.TransformPoint(HeldItemPose.Grip(item)), p.handR.position), .002f, id + " grip sits in the hand");
            held.CancelUse();
            yield return null;
            Assert.Less(Quaternion.Angle(held.HoldRotation * turn, held.transform.localRotation), .5f, id + " returns to its hold turn after a cancelled use");
            Assert.Less(Vector3.Distance(held.transform.TransformPoint(HeldItemPose.Grip(item)), p.handR.position), .002f);
        }

        [UnityTest]
        public IEnumerator TurnedShieldsRaiseLikeTheUnturnedOnesAndSettleBackUpright([ValueSource(nameof(TurnedShieldItems))] string id)
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            var shield = Craft(p, id);
            var item = shield.Definition;
            input.Next.blockHeld = true;
            yield return TestScenes.WaitUntil(() => p.Combat.IsBlocking, item.Shield.RaiseSeconds + 1f, id + " raised");
            yield return null;
            yield return null;
            Assert.Less(Quaternion.Angle(Quaternion.Euler(90f, 0f, 0f), shield.transform.localRotation), .5f, id + " is raised exactly like MIRROR");
            Assert.Less(Vector3.Distance(shield.transform.TransformPoint(HeldItemPose.Grip(item)), p.handR.position), .002f, id + " grip stays in the hand");
            input.Next.blockHeld = false;
            yield return TestScenes.WaitUntil(() => !p.Combat.IsBlocking, 1f, id + " lowered");
            yield return null;
            yield return null;
            Assert.Less(Quaternion.Angle(shield.HoldRotation * HeldItemPose.Turn(item), shield.transform.localRotation), .5f, id + " settles back upright");
        }

        [UnityTest]
        public IEnumerator PlacedCabinetShowsItsDoorsToThePlacer()
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, "CABINET");
            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => DeployedGear.CountFor(p) == 1, Live("CABINET").Deploy.PlaceSeconds + 2f, "CABINET placed");
            Assert.Greater(Vector3.Dot(gear.transform.forward, Vector3.forward), .95f, "the cover root still faces the way it was placed");
            var source = ModelLibrary.Load().Find(gear.Definition.Model).transform;
            var model = HeldItemPose.Model(gear);
            Assert.IsNotNull(model);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 180f, 0f) * source.localRotation, model.localRotation), .5f, "the doors face back toward the placer");
            var box = gear.GetComponent<BoxCollider>();
            var bounds = ModelVisual.BoundsIn(gear.transform, model.gameObject);
            Assert.AreEqual(box.center.z, bounds.center.z, .005f, "the visible cabinet sits inside its cover footprint");
        }

        [UnityTest]
        public IEnumerator MeleeHitsTheFrontAndMissesBehind([ValueSource(nameof(MeleeItems))] string id)
        {
            var melee = Live(id).Melee;
            Assert.IsNotNull(melee, id);
            float gap = Mathf.Clamp(melee.Reach * 0.6f, 0.7f, 1.1f);
            var attacker = Spawn(0, Vector3.zero, out var input);
            var front = Spawn(1, Vector3.forward * gap, out _);
            var behind = Spawn(2, Vector3.back * gap, out _);
            yield return Frames(3);
            attacker.FaceTowards(Vector3.forward);
            attacker.Body.constraints = front.Body.constraints = behind.Body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            var weapon = Craft(attacker, id);
            float health = front.Health.Current;

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => weapon.SwingStartedAt >= 0f, 1f, id + " swing start");
            yield return TestScenes.WaitUntil(() => !weapon || weapon.SwingStartedAt < 0f, melee.Cycle + 1f, id + " swing end");
            Assert.AreEqual(health - melee.Damage, front.Health.Current, 0.01f, id + " lands its catalogue damage once");
            if (melee.ArcDegrees < 360f) Assert.AreEqual(health, behind.Health.Current, id + " misses behind");
        }

        [UnityTest]
        public IEnumerator ThrowsFlyForwardAndLand([ValueSource(nameof(ThrownItems))] string id)
        {
            var thrown = Live(id).Thrown;
            Assert.IsNotNull(thrown, id);
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, id);
            var body = gear.GetComponent<Rigidbody>();

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => !p.Combat.Weapon, 1f, id + " leaves the hand");
            Assert.IsTrue(gear, id + " is in the air");
            Assert.IsFalse(gear.IsHeld);
            Assert.Greater(body.linearVelocity.z, thrown.Speed * 0.5f, id + " flies forward at its catalogue speed");
            bool spent = !thrown.Recoverable || thrown.FuseSeconds > 0f;
            Assert.AreEqual(spent, gear.IsSpent, id + " is spent only when it can't be recovered");

            if (spent)
            {
                yield return TestScenes.WaitUntil(() => !gear, thrown.FuseSeconds + 4f, id + " ends on landing");
                Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " is spent, not refunded");
                yield break;
            }
            yield return TestScenes.WaitUntil(() => gear && !gear.GetComponent<ThrownGear>() && body.linearVelocity.sqrMagnitude < 0.05f, 8f, id + " lands");
            Assert.Greater(body.position.z, 1f, id + " landed ahead of the thrower");
            Assert.Less(body.worldCenterOfMass.y, Live(id).Size.Max() + 0.2f, id + " is resting on the ground");
            Assert.IsFalse(gear.IsSpent);
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " keeps its letters");
            Assert.IsTrue(p.Combat.TryEquip(gear), id + " can be picked up again");
            Assert.AreSame(gear, p.Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator RaisedShieldBlocksAFrontalHit([ValueSource(nameof(ShieldItems))] string id)
        {
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var shield = Craft(p, id);
            var stats = shield.Shield;
            Assert.IsNotNull(stats, id);
            Assert.IsFalse(p.Combat.IsBlocking, id + " is lowered until block is held");

            input.Next.blockHeld = true;
            yield return new WaitForSeconds(stats.RaiseSeconds + 0.15f);
            Assert.IsTrue(p.Combat.IsBlocking, id + " is raised");
            float health = p.Health.Current, durability = shield.DurabilityLeft, damage = Mathf.Min(6f, durability * 0.5f);
            Assert.IsFalse(p.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Melee, damage, 0f, hitStun: 0f)), id + " blocks a frontal hit");
            Assert.AreEqual(health - damage * (1f - stats.DamageReduction), p.Health.Current, 0.01f, id + " keeps its catalogue reduction");
            Assert.Less(shield.DurabilityLeft, durability, id + " wears from what it blocked");
            if (stats.FrontArcDegrees < 359f)
            {
                float before = p.Health.Current, taken = damage * (1f - (GameConfig.Current.Tuning?.Trait(id, TraitKind.Guard)?.Amount ?? 0f));
                Assert.IsTrue(p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, damage, 0f, hitStun: 0f)), id + " leaves the back open");
                Assert.AreEqual(before - taken, p.Health.Current, 0.01f, id + " guard softens the unblocked blow");
            }
            input.Next.blockHeld = false;
            yield return null;
            yield return null;
            Assert.IsFalse(p.Combat.IsBlocking, id + " lowers when block is let go");
        }

        [UnityTest]
        public IEnumerator DeployablesArePlacedInFrontAndWork([ValueSource(nameof(DeployItems))] string id)
        {
            var stats = Live(id).Deploy;
            Assert.IsNotNull(stats, id);
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, id);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => DeployedGear.CountFor(p) == 1, stats.PlaceSeconds + 2f, id + " placed by the attack button");
            yield return Frames(2);
            Assert.IsFalse(p.Combat.Weapon, id + " left the hand");
            Assert.IsTrue(gear && gear.GetComponent<DeployedGear>(), id + " stands in the room as placed gear");
            var body = gear.GetComponent<Rigidbody>();
            Assert.IsTrue(body.isKinematic, id + " stays where it was put");
            var offset = World.Flat(body.position - p.Body.position);
            Assert.Greater(Vector3.Dot(offset.normalized, Vector3.forward), 0.95f, id + " is placed in front");
            Assert.That(offset.magnitude, Is.InRange(0.8f, 2.5f), id + " is placed within reach");
            Assert.Greater(Vector3.Dot(gear.transform.forward, Vector3.forward), 0.95f, id + " faces the way it was placed");

            switch (stats.Effect)
            {
                case DeployEffect.Cover:
                    Assert.IsNull(gear.GetComponentInChildren<GearTrigger>(), id + " is plain cover");
                    Assert.IsFalse(gear.GetComponent<Collider>().isTrigger, id + " is solid");
                    break;
                case DeployEffect.JumpPad:
                {
                    var rider = Spawn(1, body.position + Vector3.up * (Live(id).Size[1] + 0.05f), out _);
                    yield return TestScenes.WaitUntil(() => rider.Body.linearVelocity.y > stats.Strength * 0.5f, 2f, id + " launches whoever steps on it");
                    break;
                }
                case DeployEffect.SpeedStrip:
                {
                    var runner = Spawn(1, body.position + Vector3.back * (stats.FootprintZ * 0.3f), out var run);
                    runner.FaceTowards(Vector3.forward);
                    run.Next.move = Vector2.up;
                    yield return TestScenes.WaitUntil(() => runner.BoostLeft > 0f, 2f, id + " speeds up whoever runs along it");
                    run.Next.move = Vector2.zero;
                    break;
                }
                case DeployEffect.WindField:
                case DeployEffect.SlowField:
                {
                    var field = gear.GetComponent<DeployedPowerField>();
                    Assert.IsNotNull(field, id + " runs its field");
                    Assert.AreEqual(stats.Effect, field.Effect);
                    float reach = Mathf.Clamp(stats.Radius * 0.5f, stats.FootprintZ * 0.5f + 0.6f, stats.Radius - 0.2f);
                    var start = body.position + Vector3.forward * reach;
                    var target = Spawn(1, start, out _);
                    Physics.SyncTransforms();
                    yield return new WaitForSeconds(stats.Effect == DeployEffect.WindField ? 0.55f : 0.15f);
                    if (stats.Effect == DeployEffect.SlowField) Assert.Greater(target.SlowLeft, 0f, id + " slows whoever is inside");
                    else Assert.Greater(target.transform.position.z, start.z + 0.1f, id + " pushes a player in front");
                    if (stats.Effect == DeployEffect.WindField && stats.ArcDegrees < 359f)
                        Assert.AreEqual(0f, p.transform.position.z, 0.08f, id + " leaves the area behind it calm");
                    break;
                }
                default:
                    Assert.Fail($"{id}: add a runtime check for {stats.Effect}");
                    break;
            }
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " keeps its letters while placed");
        }

        const float Wound = 40f, BowTargetGap = 4f;

        static string Sorted(IEnumerable<char> letters) => new string(letters.OrderBy(c => c).ToArray());

        [UnityTest]
        public IEnumerator UsedItemsTakeEffectAfterTheirChannelAndAreSpent([ValueSource(nameof(UseItems))] string id)
        {
            var use = Live(id).Use;
            Assert.IsNotNull(use, id);
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            Assert.IsTrue(p.Health.ApplyDamage(Hits.Of(null, Vector3.back, HitSource.Melee, Wound, 0f, hitStun: 0f)));
            float wounded = p.Health.Current;
            var gear = Craft(p, id);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => !p.Combat.Weapon, use.ChannelSeconds + 2f, id + " is used up");
            Assert.IsFalse(gear, id + " is gone once used");
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " is spent, not refunded");
            switch (use.Effect)
            {
                case UseEffect.Heal:
                    Assert.AreEqual(Mathf.Min(p.Health.Max, wounded + use.Amount), p.Health.Current, 0.01f, id + " restores its catalogue amount");
                    break;
                case UseEffect.Speed:
                    Assert.AreEqual(wounded, p.Health.Current, 0.01f, id + " boosts without healing");
                    Assert.That(p.BoostLeft, Is.InRange(use.Seconds - 1f, use.Seconds + 0.01f), id + " boosts for its catalogue time");
                    break;
                default:
                    Assert.Fail($"{id}: add a runtime check for {use.Effect}");
                    break;
            }
        }

        [UnityTest]
        public IEnumerator SpentPatchesSpreadTheirZoneInFront([ValueSource(nameof(PatchItems))] string id)
        {
            var stats = Live(id).Deploy;
            Assert.IsNotNull(stats, id);
            var p = Spawn(0, Vector3.zero, out var input);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, id);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => !p.Combat.Weapon && !p.Combat.IsDeploying, stats.PlaceSeconds + 2f, id + " laid by the attack button");
            yield return Frames(2);
            Assert.IsFalse(gear, id + " is used up as it is laid");
            var patch = Object.FindObjectsByType<SummonedThing>(FindObjectsSortMode.None).Single(s => s.Word == id);
            Assert.AreEqual(1, DeployedGear.CountFor(p), id + " counts as placed gear");
            Assert.Greater(Vector3.Dot(World.Flat(patch.transform.position - p.Body.position).normalized, Vector3.forward), 0.95f, id + " is laid in front");
            Assert.AreEqual(stats.Radius * 2f, patch.transform.localScale.x, 0.01f, id + " covers its catalogue radius");
            switch (stats.Effect)
            {
                case DeployEffect.SlipZone:
                {
                    var target = Spawn(1, patch.transform.position + Vector3.right * (stats.Radius * 0.5f), out _);
                    yield return TestScenes.WaitUntil(() => target.SlipperyLeft > 0f, 1f, id + " makes whoever stands in it slip");
                    break;
                }
                default:
                    Assert.Fail($"{id}: add a runtime check for {stats.Effect}");
                    break;
            }
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " is spent, not refunded");
        }

        [UnityTest]
        public IEnumerator SwiftItemsSpeedUpTheirHolderOnlyWhileHeld([ValueSource(nameof(SwiftItems))] string id)
        {
            var swift = GameConfig.Current.Tuning.Trait(id, TraitKind.Swift);
            var p = Spawn(0, Vector3.zero, out _);
            yield return Frames(3);
            Assert.AreEqual(1f, p.HeldSwift, 0.001f, "empty hands run at the normal speed");
            Craft(p, id);
            yield return null;
            Assert.AreEqual(swift.Amount, p.HeldSwift, 0.001f, id + " speeds up its holder");
            p.Combat.Drop();
            yield return null;
            Assert.AreEqual(1f, p.HeldSwift, 0.001f, id + " stops helping once dropped");
        }

        [UnityTest]
        public IEnumerator BowsDrawWhileAttackIsHeldAndHitHarderAtAFullDraw([ValueSource(nameof(RangedItems))] string id)
        {
            var ranged = Live(id).Ranged;
            Assert.IsNotNull(ranged, id);
            var p = Spawn(0, Vector3.zero, out var input);
            var target = Spawn(1, Vector3.forward * BowTargetGap, out _);
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            p.Body.constraints = target.Body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            var bow = Craft(p, id);
            float durability = bow.DurabilityLeft, health = target.Health.Current;

            input.Next.attack = true;
            input.Next.attackHeld = true;
            yield return TestScenes.WaitUntil(() => bow.IsDrawing, 1f, id + " starts drawing");
            Assert.Less(bow.DrawPower, 1f, id + " starts slack");
            yield return TestScenes.WaitUntil(() => bow.DrawPower >= 1f, ranged.DrawSeconds + 1f, id + " reaches a full draw");
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(bow.IsDrawing, id + " keeps drawing while attack is held");
            Assert.AreEqual(health, target.Health.Current, id + " holds its shot until attack is let go");

            input.Next.attackHeld = false;
            yield return TestScenes.WaitUntil(() => target.Health.Current < health, 2f, id + " full draw lands");
            Assert.AreEqual(health - Mathf.Round(ranged.FullDamage), target.Health.Current, 0.01f, id + " full draw deals its full damage");
            Assert.IsFalse(bow.IsDrawing, id + " is slack after the shot");
            Assert.AreSame(bow, p.Combat.Weapon, id + " stays in hand after firing");
            Assert.IsFalse(bow.IsSpent, id + " fires many shots");
            Assert.AreEqual(durability - 1f, bow.DurabilityLeft, 0.01f, id + " wears one point a shot");
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " keeps its letters");

            target.Body.constraints = RigidbodyConstraints.None;
            target.Body.position = target.transform.position = new Vector3(6f, 0f, 0f);
            var second = Spawn(2, Vector3.forward * BowTargetGap, out _);
            yield return Frames(3);
            second.Body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            float fresh = second.Health.Current;
            yield return new WaitForSeconds(ranged.Recovery + 0.2f);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => second.Health.Current < fresh, 2f, id + " quick shot lands");
            float quick = fresh - second.Health.Current;
            Assert.GreaterOrEqual(quick, Mathf.Round(ranged.Damage) - 0.01f, id + " a quick tap still deals the slack damage");
            Assert.Less(quick, Mathf.Round(ranged.FullDamage), id + " a quick tap hits softer than a full draw");
        }

        [UnityTest]
        public IEnumerator PlacedStashBanksAndReturnsLettersForItsTeamOnly([ValueSource(nameof(StashItems))] string id)
        {
            var stats = Live(id).Deploy;
            Assert.IsNotNull(stats, id);
            Assert.AreEqual(DeployEffect.Stash, stats.Effect, id);
            var p = Spawn(0, Vector3.zero, out var input);
            var mate = Spawn(1, new Vector3(2f, 0f, 0f), out _);
            var rival = Spawn(2, new Vector3(-2f, 0f, 0f), out _);
            p.Team = mate.Team = 0;
            rival.Team = 1;
            yield return Frames(3);
            p.FaceTowards(Vector3.forward);
            var gear = Craft(p, id);

            input.Next.attack = true;
            yield return TestScenes.WaitUntil(() => DeployedGear.CountFor(p) == 1, stats.PlaceSeconds + 2f, id + " placed by the attack button");
            yield return Frames(2);
            Assert.IsFalse(p.Combat.Weapon, id + " left the hand");
            var body = gear.GetComponent<Rigidbody>();
            Assert.IsTrue(body.isKinematic, id + " stays where it was put");
            Assert.Greater(Vector3.Dot(World.Flat(body.position - p.Body.position).normalized, Vector3.forward), 0.95f, id + " is placed in front");
            var stash = gear.GetComponent<LetterStash>();
            Assert.IsNotNull(stash, id + " opens as a letter stash");
            Assert.IsTrue(stash.IsEmpty);
            Assert.IsTrue(stash.CanOpen(p) && stash.CanOpen(mate), id + " opens for its team");
            Assert.IsFalse(stash.CanOpen(rival), id + " stays shut for the other team");

            p.Inventory.Set("WORDS");
            Assert.IsTrue(stash.WantsExchange(p));
            Assert.IsTrue(stash.TryExchange(p), id + " banks the bag");
            Assert.IsTrue(p.Inventory.IsEmpty, "every letter went in");
            Assert.AreEqual("DORSW", Sorted(stash.Letters));

            rival.Inventory.Set("");
            Assert.IsFalse(stash.TryExchange(rival), "a rival can't empty it");
            Assert.AreEqual(5, stash.Count);

            mate.Inventory.Set("");
            Assert.IsTrue(stash.TryExchange(mate), "a teammate takes the letters back out");
            Assert.AreEqual("DORSW", Sorted(mate.Inventory.Letters));
            Assert.IsTrue(stash.IsEmpty);

            Assert.IsTrue(stash.TryExchange(mate));
            Assert.AreEqual(5, stash.Count);
            Assert.AreEqual(0, TilePool.Instance.Active.Count, id + " keeps its letters while placed");
            int own = gear.IsSpent ? 0 : id.Length;
            gear.Break();
            yield return null;
            Assert.IsFalse(gear, id + " breaks");
            Assert.AreEqual(5 + own, TilePool.Instance.Active.Count, id + " spills what was banked when it breaks");
        }
    }
}
