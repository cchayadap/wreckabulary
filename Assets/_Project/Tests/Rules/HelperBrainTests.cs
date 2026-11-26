using System.Collections.Generic;
using NUnit.Framework;
using static Wreckabulary.Rules.Tests.HelperTestSpecs;

namespace Wreckabulary.Rules.Tests
{
    public class HelperBrainTests
    {
        const float Dt = .25f;

        static HelperSense Near(float distance = .5f, bool sight = true, float fromHome = 0f, bool blocked = false) =>
            new HelperSense(distance, fromHome, sight, false, false, blocked);

        static List<HelperOrder> Run(HelperSpec spec, ref HelperState s, HelperSense sense, int target, float seconds, float dt = Dt)
        {
            var orders = new List<HelperOrder>();
            for (float t = 0f; t < seconds - 1e-4f; t += dt) orders.Add(HelperBrain.Step(ref s, spec, sense, target, dt));
            return orders;
        }

        [Test]
        public void NothingCanBeHitWhileArming()
        {
            foreach (var spec in BatchOne())
            {
                var s = HelperBrain.Start(spec, -1);
                var orders = Run(spec, ref s, Near(), 1, HelperBrain.ArmSeconds - .05f, .05f);
                CollectionAssert.DoesNotContain(orders, HelperOrder.Release, spec.Id + " arms before it can strike");
                CollectionAssert.DoesNotContain(orders, HelperOrder.FaceOnly, spec.Id);
                Assert.AreEqual(HelperPhase.Arming, s.Phase, spec.Id);
            }
        }

        [Test]
        public void EveryReleaseFollowsAFullWindUp()
        {
            foreach (var spec in BatchOne())
            {
                const float dt = .05f;
                var s = HelperBrain.Start(spec, -1);
                float windingSince = -1f, t = 0f;
                int releases = 0;
                while (s.Phase != HelperPhase.Done && t < 30f)
                {
                    var phaseBefore = s.Phase;
                    var order = HelperBrain.Step(ref s, spec, Near(spec.Range * .5f), 1, dt);
                    t += dt;
                    if (s.Phase == HelperPhase.WindUp && phaseBefore != HelperPhase.WindUp) windingSince = t;
                    if (order != HelperOrder.Release) continue;
                    releases++;
                    Assert.GreaterOrEqual(t - windingSince + 1e-3f, spec.Windup, spec.Id + " telegraphs every strike for its whole wind-up");
                    windingSince = -1f;
                }
                Assert.AreEqual(spec.Strikes, releases, spec.Id + " strikes exactly its strike count");
                Assert.AreEqual(HelperEnd.Strikes, s.Ended, spec.Id);
            }
        }

        [Test]
        public void StrikesAreSpacedByTheCooldown()
        {
            var robot = Robot();
            const float dt = .05f;
            var s = HelperBrain.Start(robot, -1);
            var times = new List<float>();
            for (float t = dt; s.Phase != HelperPhase.Done && t < 20f; t += dt)
                if (HelperBrain.Step(ref s, robot, Near(), 1, dt) == HelperOrder.Release) times.Add(t);
            Assert.AreEqual(robot.Strikes, times.Count);
            for (int i = 1; i < times.Count; i++)
                Assert.GreaterOrEqual(times[i] - times[i - 1] + 1e-3f, robot.Cooldown + robot.Windup, "cooldown then a fresh wind-up between strikes");
        }

        [Test]
        public void TheHelperVanishesOnTheFirstOfLifeStrikesHealthMakerOutAndRoundOver()
        {
            var rabbit = Rabbit();
            var s = HelperBrain.Start(rabbit, -1);
            var orders = Run(rabbit, ref s, Near(20f, false), -1, rabbit.Seconds + Dt);
            Assert.AreEqual(HelperOrder.Vanish, orders[orders.Count - 1]);
            Assert.AreEqual(HelperEnd.Life, s.Ended, "life ran out");
            Assert.AreEqual(rabbit.Seconds / Dt, orders.IndexOf(HelperOrder.Vanish), 1, "ends when its seconds run out");

            s = HelperBrain.Start(rabbit, -1);
            Assert.IsTrue(HelperBrain.Hurt(ref s, rabbit.Health));
            Assert.AreEqual(HelperOrder.Vanish, HelperBrain.Step(ref s, rabbit, Near(), 1, Dt));
            Assert.AreEqual(HelperEnd.Smashed, s.Ended);
            Assert.IsFalse(HelperBrain.Hurt(ref s, 5f), "a smashed helper takes no more hits");

            s = HelperBrain.Start(rabbit, -1);
            Assert.AreEqual(HelperOrder.Vanish, HelperBrain.Step(ref s, rabbit, new HelperSense(1f, 0f, true, true, false), 1, Dt));
            Assert.AreEqual(HelperEnd.MakerOut, s.Ended);

            s = HelperBrain.Start(rabbit, -1);
            Assert.AreEqual(HelperOrder.Vanish, HelperBrain.Step(ref s, rabbit, new HelperSense(1f, 0f, true, false, true), 1, Dt));
            Assert.AreEqual(HelperEnd.RoundOver, s.Ended);

            s = HelperBrain.Start(rabbit, -1);
            Assert.AreEqual(HelperOrder.Vanish, HelperBrain.Step(ref s, rabbit, new HelperSense(1f, 0f, true, false, false, lost: true), 1, Dt));
            Assert.AreEqual(HelperEnd.Lost, s.Ended, "off the map or fallen through the floor");
            Assert.AreEqual(HelperOrder.Vanish, HelperBrain.Step(ref s, rabbit, Near(), 1, Dt), "once done it stays done");
        }

        [Test]
        public void ABurstStrikesOnceAndEnds()
        {
            var cracker = Cracker();
            var s = HelperBrain.Start(cracker, -1);
            var orders = Run(cracker, ref s, Near(1f), 1, 3f);
            Assert.AreEqual(1, orders.FindAll(o => o == HelperOrder.Release).Count);
            Assert.AreEqual(HelperPhase.Done, s.Phase);
            Assert.AreEqual(HelperEnd.Strikes, s.Ended);
            Assert.AreEqual(HelperOrder.Vanish, orders[orders.Count - 1]);
        }

        [Test]
        public void MoversChaseAndReturnHomeAtTheLeash()
        {
            var robot = Robot();
            var s = HelperBrain.Start(robot, -1);
            Run(robot, ref s, Near(5f), 1, HelperBrain.ArmSeconds);
            Assert.AreEqual(HelperOrder.Approach, HelperBrain.Step(ref s, robot, Near(5f, fromHome: 2f), 1, Dt));
            Assert.AreEqual(HelperPhase.Chase, s.Phase);
            Assert.AreEqual(HelperOrder.FaceOnly, HelperBrain.Step(ref s, robot, Near(5f, fromHome: robot.Leash), 1, Dt), "never past its leash: it waits at the edge facing the target");
            Assert.AreEqual(HelperOrder.ReturnHome, HelperBrain.Step(ref s, robot, Near(5f, fromHome: 3f), -1, Dt), "no target: walks home");
            Assert.AreEqual(HelperOrder.Hold, HelperBrain.Step(ref s, robot, Near(5f, fromHome: .2f), -1, Dt), "home: waits");
        }

        [Test]
        public void StayersOnlyTurnToFaceATargetOutOfRange()
        {
            var blaster = Blaster();
            var s = HelperBrain.Start(blaster, -1);
            Run(blaster, ref s, Near(9f), 1, HelperBrain.ArmSeconds);
            Assert.AreEqual(HelperOrder.FaceOnly, HelperBrain.Step(ref s, blaster, Near(blaster.Range + 1f, fromHome: 0f), 1, Dt));
            Assert.AreEqual(HelperOrder.Hold, HelperBrain.Step(ref s, blaster, Near(blaster.Range + 1f, fromHome: 5f), -1, Dt), "a stayer never walks home");
        }

        [Test]
        public void LosingSightForTooLongDropsTheTargetAndTheLock()
        {
            var robot = Robot();
            var s = HelperBrain.Start(robot, 1);
            Run(robot, ref s, Near(5f), 1, HelperBrain.ArmSeconds);
            var orders = Run(robot, ref s, Near(5f, sight: false), 1, HelperBrain.LoseSightSeconds + .5f);
            Assert.Contains(HelperOrder.Hold, orders);
            Assert.AreEqual(-1, s.LockedId, "the aimed lock falls back to nearest");
            CollectionAssert.DoesNotContain(orders, HelperOrder.Release, "never strikes what it cannot see");
        }

        [Test]
        public void AStuckMoverSideStepsThenGoesHomeThenEnds()
        {
            var robot = Robot();
            var s = HelperBrain.Start(robot, -1);
            Run(robot, ref s, Near(5f), 1, HelperBrain.ArmSeconds);
            var orders = Run(robot, ref s, Near(5f, fromHome: 2f, blocked: true), 1, HelperBrain.StuckSeconds * 3f + Dt);
            int side = orders.IndexOf(HelperOrder.SideStep), home = orders.IndexOf(HelperOrder.ReturnHome), gone = orders.IndexOf(HelperOrder.Vanish);
            Assert.Greater(side, 0, "first stuck: one side-step");
            Assert.Greater(home, side, "second stuck: back home");
            Assert.Greater(gone, home, "third stuck: it ends");
            Assert.AreEqual(HelperEnd.Stuck, s.Ended);
        }

        [Test]
        public void ProgressResetsTheStuckTimer()
        {
            var robot = Robot();
            var s = HelperBrain.Start(robot, -1);
            Run(robot, ref s, Near(5f), 1, HelperBrain.ArmSeconds);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(HelperOrder.Approach, HelperBrain.Step(ref s, robot, Near(5f, fromHome: 2f, blocked: i % 2 == 0), 1, Dt));
            }
            Assert.AreEqual(0, s.StuckCount, "a mover that keeps moving is never stuck");
        }

        [Test]
        public void AHelperWithoutAnAttackNeverStrikes()
        {
            var decoy = new HelperSpec { Id = "CLONE", Body = HelperBody.Stays, Pick = HelperPick.Nearest, Attack = HelperAttack.None, Radius = 2f, Seconds = 10f, Health = 20f };
            var s = HelperBrain.Start(decoy, -1);
            var orders = Run(decoy, ref s, Near(.3f), 1, 5f);
            CollectionAssert.DoesNotContain(orders, HelperOrder.Release);
            CollectionAssert.DoesNotContain(orders, HelperOrder.FaceOnly);
        }

        [Test]
        public void TenSecondsOfAHelperRunInFortySteps()
        {
            var robot = Robot();
            var s = HelperBrain.Start(robot, -1);
            var orders = Run(robot, ref s, Near(20f, false, 0f), -1, robot.Seconds + .01f, robot.Seconds / 40f);
            Assert.AreEqual(41, orders.Count);
            Assert.AreEqual(39, orders.IndexOf(HelperOrder.Vanish), "the fortieth step ends it");
            Assert.AreEqual(HelperEnd.Life, s.Ended);
        }
    }
}
