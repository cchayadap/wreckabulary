using System.Collections.Generic;
using NUnit.Framework;
using static Wreckabulary.Rules.Tests.HelperTestSpecs;

namespace Wreckabulary.Rules.Tests
{
    public class HelperTargetingTests
    {
        const int Maker = 0, MakerTeam = 0;

        static int Pick(HelperSpec spec, List<HelperCandidate> all, int locked = -1, int provoker = -1) =>
            HelperTargeting.Pick(spec, Maker, MakerTeam, 0f, 0f, 0f, locked, provoker, all);

        [Test]
        public void TheMakerAloneInRangeIsNeverPickedByAnEnemiesHelper()
        {
            foreach (var spec in new[] { Robot(), Rabbit(), Blaster(), Patrol(), Spikes(), Trap() })
            {
                var all = new List<HelperCandidate> { Player(Maker, MakerTeam, .3f, .3f) };
                Assert.AreEqual(-1, Pick(spec, all), spec.Id + " never picks its maker");
                Assert.IsFalse(HelperTargeting.Legal(spec, Maker, MakerTeam, all[0]), spec.Id);
            }
        }

        [Test]
        public void AnAnyoneHelperPicksItsMakerWhenTheMakerIsNearest()
        {
            var all = new List<HelperCandidate> { Player(Maker, MakerTeam, .5f, 0f), Player(1, 1, 3f, 0f) };
            Assert.AreEqual(Maker, Pick(Chomper(), all), "a hostile-to-all helper bites its maker too");
        }

        [Test]
        public void TeammatesAreNeverPickedByAnEnemiesHelper()
        {
            var all = new List<HelperCandidate> { Player(1, MakerTeam, .5f, 0f), Player(2, 1, 4f, 0f) };
            Assert.AreEqual(2, Pick(Rabbit(), all), "the nearer teammate is skipped for the farther foe");
            all.RemoveAt(1);
            Assert.AreEqual(-1, Pick(Rabbit(), all), "only a teammate in range: no target");
            Assert.AreEqual(1, Pick(Chomper(), all), "an Anyone helper bites teammates");
        }

        [Test]
        public void NoTeamMeansEveryoneElseIsAFoe()
        {
            var all = new List<HelperCandidate> { Player(1, Teams.NoTeam, 1f, 0f) };
            Assert.AreEqual(1, HelperTargeting.Pick(Rabbit(), Maker, Teams.NoTeam, 0f, 0f, 0f, -1, -1, all));
            Assert.AreEqual(-1, HelperTargeting.Pick(Rabbit(), 1, Teams.NoTeam, 1f, 0f, 0f, -1, -1, all), "but never the maker");
        }

        [Test]
        public void DownedEliminatedProtectedCarriedFrozenDodgingOrRevivingPlayersAreNotHittable()
        {
            Assert.IsTrue(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, false, false, false, 0)));
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Downed, false, false, false, false, false, 0)), "downed");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Eliminated, false, false, false, false, false, 0)), "eliminated");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, true, false, false, false, false, 0)), "spawn protected");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, true, false, false, false, 0)), "dodging");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, true, false, false, 0)), "carried");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, false, true, false, 0)), "frozen");
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, false, false, true, 0)), "reviving a partner");
            var all = new List<HelperCandidate> { Player(1, 1, 1f, 0f, hittable: false) };
            Assert.AreEqual(-1, Pick(Chomper(), all), "not even an Anyone helper picks an unhittable player");
        }

        [Test]
        public void ACrowdedTargetIsLeftForAThirdHelper()
        {
            Assert.IsTrue(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, false, false, false, HelperCaps.MaxPerTarget - 1)));
            Assert.IsFalse(HelperTargeting.Hittable(new HelperVictim(LifeState.Alive, false, false, false, false, false, HelperCaps.MaxPerTarget)),
                "two helpers already hold this target");
        }

        [Test]
        public void AimedHelpersStayOnTheirLockAndFallBackToNearestWhenTheLockIsLost()
        {
            var robot = Robot();
            var all = new List<HelperCandidate> { Player(1, 1, 1f, 0f), Player(2, 2, 5f, 0f) };
            Assert.AreEqual(2, Pick(robot, all, locked: 2), "the locked player is chased even when another foe is nearer");
            var state = HelperBrain.Start(robot, 2);
            Assert.AreEqual(2, HelperTargeting.Choose(robot, ref state, Maker, MakerTeam, 0f, 0f, 0f, all));
            all[1] = Player(2, 2, 5f, 0f, hittable: false);
            Assert.AreEqual(1, HelperTargeting.Choose(robot, ref state, Maker, MakerTeam, 0f, 0f, 0f, all), "knocked out lock: nearest from now on");
            Assert.AreEqual(-1, state.LockedId);
            all[1] = Player(2, 2, .5f, 0f);
            Assert.AreEqual(2, HelperTargeting.Choose(robot, ref state, Maker, MakerTeam, 0f, 0f, 0f, all), "the lock never comes back; nearest wins");
            Assert.AreEqual(-1, state.LockedId);
        }

        [Test]
        public void ALockedTargetOutOfSightIsStillChasedUntilTheBrainGivesUp()
        {
            var all = new List<HelperCandidate> { Player(1, 1, 3f, 0f, inSight: false), Player(2, 2, 1f, 0f, inSight: false) };
            Assert.AreEqual(1, Pick(Robot(), all, locked: 1));
            Assert.AreEqual(-1, Pick(Rabbit(), all), "a nearest chaser needs sight");
        }

        [Test]
        public void TheAimLockTakesThePointerPlayerOrTheBestAimInsideTheCone()
        {
            var robot = Robot();
            var all = new List<HelperCandidate> { Player(1, 1, 4f, 0f), Player(2, 2, 0f, 3f), Player(3, 3, -2f, 0f), Player(4, MakerTeam, 2f, .1f) };
            Assert.AreEqual(1, HelperTargeting.AimedLock(robot, Maker, MakerTeam, 0f, 0f, 0f, 1f, 0f, -1, all), "straight ahead");
            Assert.AreEqual(2, HelperTargeting.AimedLock(robot, Maker, MakerTeam, 0f, 0f, 0f, 0f, 1f, -1, all), "turned toward player 2");
            Assert.AreEqual(3, HelperTargeting.AimedLock(robot, Maker, MakerTeam, 0f, 0f, 0f, 1f, 0f, 3, all), "the player under the pointer wins");
            Assert.AreEqual(-1, HelperTargeting.AimedLock(robot, Maker, MakerTeam, 0f, 0f, 0f, 1f, -1f, -1, all), "nobody inside 35 degrees: no lock, nearest from the start");
            Assert.AreEqual(-1, HelperTargeting.AimedLock(robot, Maker, MakerTeam, 0f, 0f, 0f, 1f, 0f, 4, all.GetRange(3, 1)), "a teammate is never locked, even under the pointer");
            Assert.AreEqual(-1, HelperTargeting.AimedLock(Rabbit(), Maker, MakerTeam, 0f, 0f, 0f, 1f, 0f, -1, all), "only Aimed helpers lock");
        }

        [Test]
        public void AProvokedHelperOnlyTargetsWhoeverHurtItsMakerOrItWithinTheWindow()
        {
            var terrier = Terrier();
            var all = new List<HelperCandidate> { Player(1, 1, 1f, 0f), Player(2, 2, 3f, 0f) };
            var s = HelperBrain.Start(terrier, -1);
            Assert.AreEqual(-1, HelperTargeting.Choose(terrier, ref s, Maker, MakerTeam, 0f, 0f, 0f, all), "nobody provoked it yet");
            HelperBrain.Provoke(ref s, terrier, Maker, MakerTeam, 2, 2);
            Assert.AreEqual(2, HelperTargeting.Choose(terrier, ref s, Maker, MakerTeam, 0f, 0f, 0f, all), "goes for the attacker, not the nearest");
            HelperBrain.Provoke(ref s, terrier, Maker, MakerTeam, Maker, MakerTeam);
            Assert.AreEqual(2, s.Provoker, "the maker never provokes its own helper");
            HelperBrain.Provoke(ref s, terrier, Maker, MakerTeam, 5, MakerTeam);
            Assert.AreEqual(2, s.Provoker, "a teammate cannot provoke an Enemies helper");
            var sense = new HelperSense(9f, 0f, false, false, false);
            for (float t = 0f; t < HelperTargeting.ProvokeSeconds + .3f; t += .25f) HelperBrain.Step(ref s, terrier, sense, -1, .25f);
            Assert.AreEqual(-1, s.Provoker, "the grudge fades after five seconds");
        }

        [Test]
        public void AStepperOnlyReachesPlayersInsideItsRadius()
        {
            var spikes = Spikes();
            Assert.AreEqual(-1, Pick(spikes, new List<HelperCandidate> { Player(1, 1, 1.5f, 0f) }));
            Assert.AreEqual(1, Pick(spikes, new List<HelperCandidate> { Player(1, 1, 1f, .5f) }));
        }

        [Test]
        public void PlayersOnAnotherLevelAreIgnored()
        {
            var all = new List<HelperCandidate> { Player(1, 1, 1f, 0f, y: 3f) };
            Assert.AreEqual(-1, Pick(Rabbit(), all), "upstairs is out of reach");
            Assert.AreEqual(-1, Pick(Robot(), all, locked: 1), "even a locked target upstairs");
            all[0] = Player(1, 1, 1f, 0f, y: HelperTargeting.SameLevel - .1f);
            Assert.AreEqual(1, Pick(Rabbit(), all), "a step or a sofa is still the same level");
        }

        [Test]
        public void AStrikeOnlyLandsOnALegalTargetStillInReach()
        {
            var robot = Robot();
            Assert.IsTrue(HelperTargeting.StrikeLands(robot, robot.Range + HelperTargeting.StrikeSlack - .01f, true));
            Assert.IsFalse(HelperTargeting.StrikeLands(robot, robot.Range + HelperTargeting.StrikeSlack + .01f, true), "dodged out of reach during the wind-up");
            Assert.IsFalse(HelperTargeting.StrikeLands(robot, .2f, false), "became protected or knocked out during the wind-up");
        }

        [Test]
        public void HelpersNeverTargetHelpers()
        {
            Assert.IsFalse(HelperTargeting.Bites(Chomper(), Maker, MakerTeam, -1, Teams.NoTeam), "a non-player id is never bitten");
        }
    }
}
