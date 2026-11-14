using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class AvatarVictoryExpressionTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static AvatarExpression State(PlayerController player) =>
            ((AvatarExpressionDriver)typeof(PlayerAppearance).GetField("expressions", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(player.GetComponent<PlayerAppearance>())).State;

        [UnityTest]
        public IEnumerator ConfirmedDuosVictoryCelebratesTheAliveWinnerButPreservesDownedTeammateAndLosers()
        {
            Session.SelectMap("pinwheel"); Match.ModeOverride = "Duos";
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            var round = RoundManager.Instance;
            round.CountdownTime = .05f;
            yield return TestScenes.WaitUntil(() => round.Phase == Phase.Playing, 2f, "Duos begins");
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(new[] { 0, 1, 0, 1 }, joins.Players.Select(p => p.Team).ToArray());
            foreach (var player in joins.Players)
            {
                if (player.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (player.Binding is BotBinding input) input.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0;
                player.Health.UseRules(rules);
            }
            var downedWinner = joins.Players[0];
            downedWinner.Health.ApplyDamage(HitInfo.Hazard(1000));
            Assert.IsTrue(downedWinner.IsDowned);
            foreach (var player in joins.Players.Where(p => p.Team == 1))
                player.Health.ApplyDamage(HitInfo.Hazard(1000));
            yield return TestScenes.WaitUntil(() => round.Phase == Phase.RoundOver, 2f, "confirmed team result");
            yield return null;

            var winner = joins.Players.Single(p => p.Team == 0 && p.Health.IsAlive);
            var appearance = winner.GetComponent<PlayerAppearance>();
            Assert.AreEqual(1, round.WinsOf(winner)); Assert.AreEqual(1, round.WinsOf(downedWinner));
            Assert.AreEqual("Celebrate", appearance.LocomotionClip);
            Assert.AreEqual(AvatarExpression.Celebrate, State(winner));
            Assert.AreEqual(AvatarExpression.Downed, State(downedWinner));
            Assert.AreNotEqual("Celebrate", downedWinner.GetComponent<PlayerAppearance>().LocomotionClip);
            foreach (var loser in joins.Players.Where(p => p.Team == 1))
            {
                Assert.AreEqual(0, round.WinsOf(loser));
                Assert.AreEqual(AvatarExpression.Downed, State(loser));
                Assert.AreNotEqual("Celebrate", loser.GetComponent<PlayerAppearance>().LocomotionClip);
            }
            yield return new WaitForSeconds(.1f);
            Assert.AreEqual(AvatarExpression.Celebrate, State(winner));
        }
    }
}
