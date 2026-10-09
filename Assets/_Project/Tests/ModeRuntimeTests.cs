using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ModeRuntimeTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        IEnumerator LoadBattle(string mode = "Dibs", string map = "pinwheel")
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
        }

        static void StopBots(PlayerJoinManager joins)
        {
            foreach (var bot in joins.GetComponentsInChildren<BotController>()) bot.enabled = false;
            foreach (var p in joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        IEnumerator BeginBattle()
        {
            RoundManager.Instance.CountdownTime = .05f;
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.Playing, 2f, "battle start");
            StopBots(Object.FindAnyObjectByType<PlayerJoinManager>());
        }

        [UnityTest]
        public IEnumerator SoloDibsGetsAnOpponentWithoutPersistingAnAiRoommate()
        {
            yield return LoadBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(4, joins.Players.Count);
            Assert.AreEqual(1, joins.HumanCount);
            Assert.AreEqual(1, Session.Bindings.Count);
            Assert.AreEqual(3, joins.Players.Count(p => p.Binding is BotBinding));
            Assert.AreEqual(Phase.Countdown, RoundManager.Instance.Phase);
            Assert.IsTrue(joins.Players.All(p => p.Health.Max == 100f && p.Inventory.Capacity == 10 && p.Inventory.Count == 0));
            Assert.AreNotEqual(joins.Players[0].Team, World.NearestOpponent(joins.Players[0], joins.Players[0].transform.position).Team);
        }

        [UnityTest]
        public IEnumerator DuosKeepsADownedTeammateInPlayWhileTheirPartnerIsStanding()
        {
            yield return LoadBattle("Duos");
            yield return BeginBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(new[] { 0, 1, 0, 1 }, joins.Players.Select(p => p.Team).ToArray());
            var p = joins.Players[0];
            p.Health.ApplyDamage(HitInfo.Hazard(1000f));
            yield return null;
            Assert.IsTrue(p.IsDowned);
            Assert.AreEqual(Phase.Playing, RoundManager.Instance.Phase);
            Assert.AreNotEqual(p.Team, World.NearestOpponent(p, p.transform.position).Team);
        }

        [UnityTest]
        public IEnumerator DuosTeamWipeAwardsBothWinningTeammatesTheSameScore()
        {
            yield return LoadBattle("Duos");
            yield return BeginBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            foreach (var p in joins.Players.Where(p => p.Team == 1)) p.Health.ApplyDamage(HitInfo.Hazard(1000f));
            yield return null;
            Assert.AreEqual(Phase.RoundOver, RoundManager.Instance.Phase);
            Assert.IsTrue(joins.Players.Where(p => p.Team == 1).All(p => p.IsEliminated));
            Assert.IsTrue(joins.Players.Where(p => p.Team == 0).All(p => RoundManager.Instance.WinsOf(p) == 1));
        }

        [UnityTest]
        public IEnumerator AiPartnerStartsAndFinishesAHeldRevive()
        {
            yield return LoadBattle("Duos");
            yield return BeginBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var human = joins.Players[0];
            var partner = joins.Players[2];
            human.Respawn(new Vector3(0f, 0f, 0f));
            partner.Respawn(new Vector3(1.2f, 0f, 0f));
            human.Health.ApplyDamage(HitInfo.Hazard(1000f));
            partner.GetComponent<BotController>().enabled = true;
            yield return TestScenes.WaitUntil(() => partner.Combat.IsReviving, 2f, "AI begins revive with an interact press");
            yield return TestScenes.WaitUntil(() => human.Health.IsAlive, Match.Rules.ReviveSeconds + 2f, "AI holds revive until complete");
            Assert.AreEqual(Match.Rules.ReviveHealth, human.Health.Current);
            Assert.AreEqual(Phase.Playing, RoundManager.Instance.Phase);
        }

        [UnityTest]
        public IEnumerator SimultaneousDibsWipeIsADraw()
        {
            yield return LoadBattle();
            yield return BeginBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            foreach (var p in joins.Players) p.Health.Eliminate();
            yield return null;
            Assert.AreEqual(Phase.RoundOver, RoundManager.Instance.Phase);
            Assert.IsTrue(joins.Players.All(p => RoundManager.Instance.WinsOf(p) == 0));
        }

        [UnityTest]
        public IEnumerator AFinalizedRoundStopsABombFuseFromWreckingItsWinner()
        {
            yield return LoadBattle();
            yield return BeginBattle();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var winner = joins.Players[0];
            winner.Health.ApplyDamage(HitInfo.Hazard(90f));
            var definition = GameConfig.Current.Items.Get("BOMB");
            var bomb = CatalogGear.Create(definition);
            bomb.transform.position = winner.transform.position;
            bomb.GetComponent<Rigidbody>().isKinematic = true;
            ThrownGear.Attach(bomb, joins.Players[1]);
            foreach (var opponent in joins.Players.Where(p => p != winner)) opponent.Health.Eliminate();
            yield return null;
            Assert.AreEqual(Phase.RoundOver, RoundManager.Instance.Phase);
            yield return new WaitForSeconds(definition.Thrown.FuseSeconds + .5f);
            Assert.AreEqual(10f, winner.Health.Current);
            Assert.AreEqual(1, RoundManager.Instance.WinsOf(winner));
            Assert.AreEqual(0, Object.FindObjectsByType<ThrownGear>().Length);
        }

        [UnityTest]
        public IEnumerator CourtyardGeometryLeavesConfiguredDoorwaysOpen()
        {
            yield return LoadBattle(map: "courtyard");
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.AreEqual("Garden Courtyard", room.Layout.Name);
            Assert.AreEqual(32f, room.Layout.Rooms.Max(r => r.MaxX) - room.Layout.Rooms.Min(r => r.MinX));
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.Raycast(new Vector3(5.2f, .5f, -2f), Vector3.right, 1.6f, World.GroundMask), "Garden–Kitchen doorway");
            Assert.IsTrue(Physics.Raycast(new Vector3(5.2f, .5f, 0f), Vector3.right, 1.6f, World.GroundMask), "adjacent wall segment");
            Assert.IsTrue(room.Originals.Count > 0);
        }

        [UnityTest]
        public IEnumerator TheFlatPlaysWithItsOwnRoomsAndOpenDoorways()
        {
            yield return LoadBattle(map: "flat");
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.AreEqual("City Flat", room.Layout.Name);
            Assert.AreEqual(24f, room.Layout.Rooms.Max(r => r.MaxX) - room.Layout.Rooms.Min(r => r.MinX));
            Assert.IsNull(GameObject.Find("Courtyard path east-west"), "the courtyard's paths stay in the courtyard");
            Assert.IsNull(GameObject.Find("Sign"), "no wall sign cut short by the flat's walls");
            Assert.AreEqual(room.Layout.Furniture.Count, room.Originals.Count);
            Assert.AreEqual(new[] { "H", "Ba", "Be", "S", "K", "L" }, room.Layout.Rooms.Select(r => GameHud.Initial(room.Layout, r.Name)).ToArray(),
                "the small map tells the bedroom from the bathroom");
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.Raycast(new Vector3(2.2f, .5f, 2.5f), Vector3.right, 1.6f, World.GroundMask), "Hall–Kitchen doorway");
            Assert.IsTrue(Physics.Raycast(new Vector3(2.2f, .5f, 0f), Vector3.right, 1.6f, World.GroundMask), "the wall beside it");
            yield return new WaitForSeconds(.5f);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(4, joins.Players.Count);
            var seats = room.Layout.Spawns.Select(s => s.Room).ToList();
            foreach (var p in joins.Players)
            {
                var at = p.transform.position;
                Assert.AreEqual(0f, at.y, .3f, p.name + " stands on the floor");
                CollectionAssert.Contains(seats, room.Layout.RoomAt(at.x, at.z), p.name + " starts in a seat room");
            }
        }

        [UnityTest]
        public IEnumerator MovingOutInTheFlatHidesThreeKeepsakes()
        {
            yield return LoadBattle("MovingOut", "flat");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 5f, "evacuation start");
            Assert.AreEqual(3, director.KeepsakeCount);
            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            Assert.AreEqual("LivingRoom", layout.RoomAt(director.ExtractionPoint.x, director.ExtractionPoint.z), "the van waits by the living room");
        }

        [UnityTest]
        public IEnumerator MovingDayInTheFlatFurnishesTheFlat()
        {
            Session.SelectMap("flat");
            yield return TestScenes.Load(Session.MovingDayScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var director = Object.FindAnyObjectByType<MovingDayDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Playing, 5f, "level start");
            Assert.AreEqual("City Flat", director.CurrentLevel.name);
            Assert.AreEqual(6, director.Rooms.Count);
            CollectionAssert.AreEquivalent(new[] { "BED", "SOFA", "TABLE", "LAMP" }, director.CurrentLevel.items.Select(i => i.word).ToArray());
            Assert.AreEqual("Bedroom", director.CurrentLevel.items.First(i => i.word == "BED").room);
        }

        [UnityTest]
        public IEnumerator ClearOutWarnsBeforeItHurtsAndUsesTheSelectedRoom()
        {
            yield return LoadBattle();
            yield return BeginBattle();
            var clear = Object.FindAnyObjectByType<ClearOutController>();
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players[0];
            p.Respawn(new Vector3(7f, 0f, 3f));
            var began = typeof(ClearOutController).GetField("began", BindingFlags.Instance | BindingFlags.NonPublic);
            began.SetValue(clear, Time.time - (Match.Rules.ClearOutFirstAt - Match.Rules.ClearOutWarnSeconds + 1f));
            yield return null;
            StringAssert.Contains("Kitchen", clear.Message);
            Assert.AreEqual(p.Health.Max, p.Health.Current, "warnings cause no damage");
            began.SetValue(clear, Time.time - (Match.Rules.ClearOutFirstAt + 1f));
            yield return new WaitForSeconds(.6f);
            Assert.Less(p.Health.Current, p.Health.Max, "filling rooms apply shared hazard damage");
        }

        [UnityTest]
        public IEnumerator MovingOutRequiresPackedKeepsakesAndSurvivorsAtTheVanThenRestarts()
        {
            yield return LoadBattle("MovingOut");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 5f, "evacuation start");
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players.Single();
            Assert.AreEqual(3, director.KeepsakeCount);
            foreach (var keepsake in Object.FindObjectsByType<Keepsake>())
                keepsake.GetComponent<Rigidbody>().position = director.ExtractionPoint + Vector3.up * .3f;
            yield return new WaitForSeconds(.4f);
            Assert.AreEqual(3, director.PackedCount);
            Assert.AreEqual(MovingOutDirector.State.Playing, director.Current, "the roommate still needs to get out");
            p.Respawn(director.ExtractionPoint);
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Complete, 2f, "survivor reaches van");
            director.Restart();
            yield return null;
            Assert.AreEqual(MovingOutDirector.State.Countdown, director.Current);
            Assert.AreEqual(0, director.PackedCount);
            Assert.AreEqual(100f, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator MovingOutFailsOnWholeTeamWipeAndCanRetry()
        {
            yield return LoadBattle("MovingOut");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 5f, "evacuation start");
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players.Single();
            p.Health.Eliminate();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Failed, 2f, "team wipe");
            director.Restart();
            Assert.IsTrue(p.Health.IsAlive);
            Assert.AreEqual(MovingOutDirector.State.Countdown, director.Current);
        }
    }
}
