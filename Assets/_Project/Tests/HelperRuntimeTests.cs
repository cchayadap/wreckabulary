using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public class HelperRuntimeTests
    {
        const string Chaser = "QACHASER", Biter = "QABITER", Guard = "QAGUARD";
        const int AllocationFrames = 300;
        const long AllocationSlackPerFrame = 64;
        readonly List<string> added = new();

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
            foreach (string id in added) GameConfig.Current.Helpers.Remove(id);
            added.Clear();
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static HelperSpec ChaserSpec(HelperPick pick = HelperPick.Aimed, float leash = 10f) => new HelperSpec
        {
            Id = Chaser, Body = HelperBody.Walks, Pick = pick, Bites = HelperBite.Enemies, Attack = HelperAttack.Swing,
            Damage = 8f, Range = 1.1f, Windup = .45f, Cooldown = .75f, Knockback = 3f,
            Speed = 3f, Sight = 8f, Leash = leash, Seconds = 10f, Strikes = 3, Health = 22f,
        };

        static HelperSpec BiterSpec() => new HelperSpec
        {
            Id = Biter, Body = HelperBody.Walks, Pick = HelperPick.Nearest, Bites = HelperBite.Anyone, Attack = HelperAttack.Swing,
            Damage = 10f, Range = 1f, Windup = .45f, Cooldown = .7f, Knockback = 4f,
            Speed = 3.4f, Sight = 8f, Leash = 10f, Seconds = 12f, Strikes = 5, Health = 30f,
        };

        static HelperSpec GuardSpec() => new HelperSpec
        {
            Id = Guard, Body = HelperBody.Stays, Pick = HelperPick.Nearest, Bites = HelperBite.Enemies, Attack = HelperAttack.Shot,
            Damage = 6f, Range = 3.5f, Windup = .5f, Cooldown = .9f, Knockback = 2f, ShotSpeed = 11f,
            Radius = 3.5f, Seconds = 18f, Strikes = 5, Health = 30f,
        };

        HelperSpec Register(HelperSpec spec)
        {
            GameConfig.Current.Helpers.Remove(spec.Id);
            GameConfig.Current.Helpers.Add(spec);
            added.Add(spec.Id);
            return spec;
        }

        static ItemDefinition TestItem(string id) => new ItemDefinition
        {
            Id = id, Category = ItemCategory.Combat, Tier = ItemTier.Expanded, Enabled = true, Consumable = true,
            Family = HandlingFamily.Helper, Hands = 1, Model = "Items/" + id, HeldScale = .8f,
            Grip = new[] { 0f, .1f, 0f }, Size = new[] { .32f, .42f, .28f },
        };

        static PlayerController Spawn(int seat, int team, Vector3 at)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, new ScriptedBinding());
            var rules = Match.Rules.Clone();
            rules.SpawnProtectionSeconds = 0f;
            rules.LettersDroppedPerHit = 0;
            player.Health.UseRules(rules);
            player.Respawn(at);
            player.Inventory.Collects = false;
            player.Team = team;
            return player;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        static HeldWeapon Hold(PlayerController player, string id)
        {
            var gear = CatalogGear.Create(TestItem(id));
            Assert.IsTrue(player.Combat.TryEquip(gear), id + " is in hand");
            return gear;
        }

        static HelperAgent Placed(GameObject root, HelperSpec spec, PlayerController maker, Vector3 at, int locked = -1)
        {
            root.transform.position = at;
            var body = root.GetComponent<Rigidbody>();
            body.position = at;
            return HelperAgent.Launch(root, TestItem(spec.Id), spec, maker, locked);
        }

        static GameObject Body(string id) => CatalogGear.Create(TestItem(id)).gameObject;

        [UnityTest]
        public IEnumerator AnAimedChaserReleasedAtADummyChasesAndHitsIt()
        {
            var spec = Register(ChaserSpec());
            var maker = Spawn(0, 0, Vector3.zero);
            var dummy = Spawn(1, 1, new Vector3(0f, 0f, 5f));
            yield return Frames(3);
            maker.FaceTowards(Vector3.forward);
            var gear = Hold(maker, Chaser);
            PlayerController struck = null;
            Action<HelperAgent, PlayerController> onStruck = (_, victim) => struck = victim;
            HelperAgent.Struck += onStruck;
            try
            {
                gear.Use(maker.Combat);
                yield return TestScenes.WaitUntil(() => HelperAgent.All.Count == 1, HelperAgent.PlaceSeconds + 2f, "the chaser hops out of the hand");
                yield return null;
                var agent = HelperAgent.All[0];
                Assert.IsNull(maker.Combat.Weapon, "the helper left the hand");
                Assert.IsFalse(agent.GetComponent<HeldWeapon>(), "an active helper is no longer held gear");
                Assert.AreEqual(dummy.Index, agent.LockedId, "the dummy straight ahead is the aimed lock");
                Assert.AreEqual(1, DeployedGear.CountFor(maker), "it counts against the placed limit");
                float start = dummy.Health.Current, from = Vector3.Distance(agent.transform.position, dummy.transform.position);
                yield return TestScenes.WaitUntil(() => dummy.Health.Current < start, 8f, "the chaser reaches and hits the dummy");
                Assert.AreSame(dummy, struck);
                Assert.Less(Vector3.Distance(agent.transform.position, dummy.transform.position), from, "it walked to its target");
                Assert.AreEqual(maker.Health.Max, maker.Health.Current, "its maker is never hit");
            }
            finally { HelperAgent.Struck -= onStruck; }
        }

        [UnityTest]
        public IEnumerator AFoesOnlyHelperFliesAFlagInItsMakersColourAndABiterFliesNone()
        {
            var chaser = Register(ChaserSpec(HelperPick.Nearest));
            var biter = Register(BiterSpec());
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            var friendly = Placed(Body(Chaser), chaser, maker, new Vector3(2f, 0f, 0f));
            var hostile = Placed(Body(Biter), biter, maker, new Vector3(-6f, 0f, 0f));
            yield return Frames(2);
            var flag = friendly.GetComponent<HelperMotion>().Flag;
            Assert.IsTrue(flag, "a foes-only helper flies a flag");
            var cloth = flag.Find(HelperMotion.PennantName).GetComponent<MeshRenderer>();
            Assert.AreSame(PlayerHud.Mark(maker.Color), cloth.sharedMaterial, "the flag is in its maker's colour");
            Assert.Greater(cloth.bounds.max.y, friendly.transform.position.y + HelperMotion.FlagPole * .5f, "the flag stands above the toy");
            Assert.IsFalse(hostile.GetComponent<HelperMotion>().Flag, "a helper that bites anyone flies no flag");
        }

        [UnityTest]
        public IEnumerator AnAnyoneHelperBitesItsMakerAndCreditsNobody()
        {
            var spec = Register(BiterSpec());
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            int attacker = int.MaxValue;
            maker.Health.Damaged += (_, hit, _) => attacker = hit.AttackerId;
            Placed(Body(Biter), spec, maker, new Vector3(0f, 0f, 2f));
            float start = maker.Health.Current;
            yield return TestScenes.WaitUntil(() => maker.Health.Current < start, 6f, "the hostile-to-all helper bites its maker");
            Assert.AreEqual(-1, attacker, "the bite comes from nobody, so it scores for nobody");
        }

        [UnityTest]
        public IEnumerator AnEnemiesHelperNeverHurtsATeammate()
        {
            var spec = Register(ChaserSpec(HelperPick.Nearest));
            var maker = Spawn(0, 0, Vector3.zero);
            var mate = Spawn(1, 0, new Vector3(0f, 0f, 2.6f));
            var foe = Spawn(2, 1, new Vector3(4f, 0f, 2f));
            yield return Frames(3);
            bool mateHit = false;
            mate.Health.Damaged += (_, _, _) => mateHit = true;
            float start = foe.Health.Current;
            Placed(Body(Chaser), spec, maker, new Vector3(0f, 0f, 1.8f));
            yield return TestScenes.WaitUntil(() => foe.Health.Current < start, 8f, "the helper skips the nearer teammate for the foe");
            yield return Frames(30);
            Assert.IsFalse(mateHit, "a teammate standing next to it is never bitten");
            Assert.AreEqual(maker.Health.Max, maker.Health.Current);
        }

        [UnityTest]
        public IEnumerator HelpersEndWhenTheRoundFreezesAndDropNoLetters()
        {
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            var ends = new List<HelperEnd>();
            Action<HelperAgent, HelperEnd> onEnd = (_, end) => ends.Add(end);
            HelperAgent.Ended += onEnd;
            try
            {
                var mover = Placed(Body(Chaser), Register(ChaserSpec(HelperPick.Nearest)), maker, new Vector3(2f, 0f, 2f));
                var guard = Placed(Body(Guard), Register(GuardSpec()), maker, new Vector3(-2f, 0f, 2f));
                Assert.IsFalse(mover.GetComponent<SummonedThing>().ReturnsLetters, "a helper is never recycled into letters");
                yield return Frames(5);
                Assert.AreEqual(2, HelperAgent.All.Count);
                World.FreezeTransient();
                yield return null;
                Assert.AreEqual(0, HelperAgent.All.Count);
                Assert.IsFalse(mover);
                Assert.IsFalse(guard);
                CollectionAssert.AreEqual(new[] { HelperEnd.RoundOver, HelperEnd.RoundOver }, ends);
            }
            finally { HelperAgent.Ended -= onEnd; }
        }

        [UnityTest]
        public IEnumerator AHelperEndsWhenItsMakerIsKnockedOut()
        {
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            var agent = Placed(Body(Guard), Register(GuardSpec()), maker, new Vector3(0f, 0f, 2f));
            yield return Frames(3);
            maker.Health.ApplyDamage(Hits.Hazard(Vector3.forward, maker.Health.Max * 2f));
            yield return TestScenes.WaitUntil(() => !agent, 2f, "the helper goes when its maker does");
        }

        [UnityTest]
        public IEnumerator PlayersCanSmashAHelperButNeverPickItUp()
        {
            var spec = Register(ChaserSpec(HelperPick.Nearest));
            var maker = Spawn(0, 0, Vector3.zero);
            var foe = Spawn(1, 1, new Vector3(0f, 0f, 1f));
            yield return Frames(3);
            foe.FaceTowards(Vector3.forward);
            var agent = Placed(Body(Chaser), spec, maker, new Vector3(0f, 0f, 1.9f));
            Assert.IsFalse(agent.GetComponent<Rigidbody>().isKinematic, "a light, loose body that would be grabbable without the rule");
            yield return Frames(3);
            Assert.AreNotEqual(agent.GetComponent<Rigidbody>(), foe.Combat.GrabTarget(), "an active helper is never a grab target");
            Assert.IsFalse(foe.Combat.TryGrab() && foe.Combat.Held == agent.GetComponent<Rigidbody>());
            Assert.IsTrue(agent.ApplyDamage(Hits.Of(foe, Vector3.forward, HitSource.Unarmed, spec.Health, 0f)));
            Assert.AreEqual(HelperEnd.Smashed, agent.EndReason);
            yield return TestScenes.WaitUntil(() => !agent, HelperAgent.EndSeconds + 1f, "a smashed helper shrinks away");
        }

        [UnityTest]
        public IEnumerator OneMovingHelperPerPlayerAndTheSecondStaysInHand()
        {
            Register(ChaserSpec(HelperPick.Nearest));
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            maker.FaceTowards(Vector3.forward);
            Hold(maker, Chaser);
            Assert.IsTrue(maker.Combat.ReleaseHelper());
            yield return TestScenes.WaitUntil(() => HelperAgent.All.Count == 1, HelperAgent.PlaceSeconds + 2f, "the first chaser");
            maker.FaceTowards(Vector3.right);
            var second = Hold(maker, Chaser);
            Assert.IsFalse(maker.Combat.ReleaseHelper(), "a second moving helper is refused");
            yield return Frames(10);
            Assert.AreSame(second, maker.Combat.Weapon, "the refused helper stays in the hand");
            Assert.AreEqual(1, HelperAgent.All.Count);
        }

        [UnityTest]
        public IEnumerator AGuardShootsAFoeInRangeWithAVisibleShot()
        {
            var spec = Register(GuardSpec());
            var maker = Spawn(0, 0, new Vector3(0f, 0f, -3f));
            var foe = Spawn(1, 1, new Vector3(0f, 0f, 3f));
            yield return Frames(3);
            float start = foe.Health.Current;
            Placed(Body(Guard), spec, maker, new Vector3(0f, 0f, 0.5f));
            yield return TestScenes.WaitUntil(() => Object.FindAnyObjectByType<HelperShot>() || foe.Health.Current < start, 4f, "the guard fires");
            yield return TestScenes.WaitUntil(() => foe.Health.Current < start, 3f, "the shot lands");
            Assert.AreEqual(start - spec.Damage, foe.Health.Current, .01f, "one shot, its damage, break power 0");
        }

        [UnityTest]
        public IEnumerator SixHelpersAllocateNothingPerFrame()
        {
            var maker = Spawn(0, 0, Vector3.zero);
            var foe = Spawn(1, 1, new Vector3(0f, 0f, 9.5f));
            yield return Frames(10);
            var baseline = new long[3];
            for (int run = 0; run < baseline.Length; run++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < AllocationFrames; i++) yield return null;
                baseline[run] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            var chaser = Register(ChaserSpec(HelperPick.Nearest, 3f));
            var guard = Register(GuardSpec());
            for (int i = 0; i < 3; i++)
            {
                Placed(Body(Chaser), chaser, maker, new Vector3(-3f + i * 3f, 0f, 1.5f));
                Placed(Body(Guard), guard, maker, new Vector3(-3f + i * 3f, 0f, -1.5f));
            }
            Assert.AreEqual(HelperCaps.MaxInPlay, HelperAgent.All.Count);
            for (int i = 0; i < 30; i++) yield return null;
            var loaded = new long[3];
            for (int run = 0; run < loaded.Length; run++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < AllocationFrames; i++) yield return null;
                loaded[run] = GC.GetAllocatedBytesForCurrentThread() - before;
            }
            Assert.AreEqual(HelperCaps.MaxInPlay, HelperAgent.All.Count, "all six still in play, none struck");
            Assert.AreEqual(foe.Health.Max, foe.Health.Current);
            long extra = Median(loaded) - Median(baseline);
            Debug.Log($"[Wreckabulary] helper allocation probe: baseline {Median(baseline)} B, six helpers {Median(loaded)} B over {AllocationFrames} frames");
            Assert.LessOrEqual(extra, AllocationSlackPerFrame * AllocationFrames, "six helpers add no per-frame garbage");
        }

        static long Median(long[] runs)
        {
            var sorted = (long[])runs.Clone();
            Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }

        [UnityTest]
        public IEnumerator ASeventhHelperRetiresTheOldest()
        {
            var maker = Spawn(0, 0, Vector3.zero);
            yield return Frames(3);
            var guard = Register(GuardSpec());
            var first = Placed(Body(Guard), guard, maker, new Vector3(-6f, 0f, 4f));
            yield return null;
            for (int i = 1; i < HelperCaps.MaxInPlay; i++)
            {
                Placed(Body(Guard), guard, maker, new Vector3(-6f + i * 2f, 0f, 4f));
                yield return null;
            }
            Assert.AreEqual(HelperCaps.MaxInPlay, HelperAgent.All.Count);
            Placed(Body(Guard), guard, maker, new Vector3(0f, 0f, 8f));
            Assert.AreEqual(HelperCaps.MaxInPlay, HelperAgent.All.Count, "never more than six in play");
            Assert.IsTrue(first.IsEnding, "the oldest one puffs away");
        }
    }
}
