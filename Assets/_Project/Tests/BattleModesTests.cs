using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class BattleModesTests
    {
        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud Hud => Object.FindAnyObjectByType<GameHud>();

        static IEnumerator Load(string mode, string map = "pinwheel")
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.ExploreHouse();
            Hud.Follow(Joins.Players[0]);
        }

        static readonly string[] Houses = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };

        static IEnumerator Play(HouseRoundDirector director, bool stopBots = true)
        {
            director.CountdownSeconds = .05f;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, 3f, "the match to start");
            foreach (var p in Joins.Players)
            {
                if (stopBots && p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (stopBots && p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        static void Knock(PlayerController victim, PlayerController attacker) =>
            victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f));

        static string Card(string path) => Hud.UiCanvas.transform.Find("Safe HUD/Result/Result card/" + path).GetComponent<TMP_Text>().text;

        static string Seat(int seat) => Hud.UiCanvas.transform.Find($"Safe HUD/Player bar/Card {seat}/Name").GetComponent<TMP_Text>().text;

        static string Score(int seat) => Hud.UiCanvas.transform.Find($"Safe HUD/Player bar/Card {seat}/Score").GetComponent<TMP_Text>().text;

        static string Objective() => Hud.UiCanvas.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Objective text").text;

        [UnityTest]
        public IEnumerator FreeForAllFillsTheHouseWithBotsAndKeepsTheMoversAway()
        {
            yield return Load(HouseModes.FreeForAll);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            Assert.IsNotNull(director, "Free-for-All attaches its own director.");
            Assert.IsTrue(RoundManager.Instance.Directed);
            Assert.IsNull(Object.FindAnyObjectByType<ClearOutController>(), "The Movers are off.");
            Assert.AreEqual(4, Joins.Players.Count);
            Assert.AreEqual(1, Joins.HumanCount);
            Assert.AreEqual(4, Joins.Players.Select(p => p.Team).Distinct().Count(), "Everyone against everyone.");
            Assert.AreEqual(10, director.Target);
            Assert.AreEqual(3f, director.RespawnSeconds);
            Assert.AreEqual(240f, director.TimeLimit);
            Assert.AreEqual(("HOUSE PARTY", "Free-for-All"), GameHud.MatchHeading(HouseModes.FreeForAll));
            Assert.AreEqual(Phase.Countdown, RoundManager.Instance.Phase);
        }

        [UnityTest]
        public IEnumerator FreeForAllCountsKnockoutsOnTheCardsAndCrownsTheFirstToTheTarget()
        {
            yield return Load(HouseModes.FreeForAll);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            director.Target = 2;
            director.RespawnSeconds = .2f;
            yield return Play(director);
            var players = Joins.Players;
            PlayerController crowned = null;
            RoundManager.Instance.MatchWon += p => crowned = p;
            Knock(players[2], players[1]);
            yield return null;
            Assert.AreEqual(1, director.Race.Of(players[1].Index));
            Assert.AreEqual(Phase.Playing, RoundManager.Instance.Phase);
            yield return TestScenes.WaitUntil(() => Score(2) == "1/2", 1f, "player cards to show knockouts against the target");
            StringAssert.StartsWith("AI 2", Seat(2));
            yield return TestScenes.WaitUntil(() => !players[2].IsKnockedOut, 2f, "the knockout to come back");
            Knock(players[3], players[1]);
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.MatchOver, 1f, "the race to end");
            Assert.AreEqual(players[1], RoundManager.Instance.LastWinner);
            Assert.AreEqual(players[1], crowned);
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the result card");
            Assert.AreEqual("AI 2 wins with 2 knockouts!", Card("Heading"));
            Assert.AreEqual("MATCH OVER · AI 2 WINS", Card("Eyebrow"));
            StringAssert.StartsWith("<b>AI 2 · 2 knockouts</b>", Card("Players"));
            StringAssert.Contains("You · 0 knockouts", Card("Players"));
        }

        [UnityTest]
        public IEnumerator AKnockoutComesBackAtTheSpawnFarthestFromEnemies()
        {
            yield return Load(HouseModes.FreeForAll);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            director.RespawnSeconds = .3f;
            yield return Play(director);
            var victim = Joins.Players[0];
            var enemies = Joins.Players.Skip(1).ToArray();
            for (int i = 0; i < enemies.Length; i++) enemies[i].Respawn(Spot(Joins.SpawnSpot(i)) + Vector3.right * .4f);
            int expected = director.FarthestSpawn(victim);
            Assert.AreEqual(3, expected, "The only spawn without an enemy on it.");
            Knock(victim, enemies[0]);
            yield return new WaitForSeconds(.15f);
            Assert.IsTrue(victim.IsKnockedOut, "Knockouts wait before they return.");
            yield return TestScenes.WaitUntil(() => !victim.IsKnockedOut, 2f, "the respawn");
            Assert.Less(World.Flat(victim.transform.position - Spot(Joins.SpawnSpot(expected))).magnitude, 3f);
        }

        static Vector3 Spot(Spot s) => new(s.X, s.Y, s.Z);

        [UnityTest]
        public IEnumerator ATieWhenTimeRunsOutGoesToSuddenDeath()
        {
            yield return Load(HouseModes.FreeForAll);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            director.TimeLimit = 1f;
            director.RespawnSeconds = .2f;
            yield return Play(director);
            var players = Joins.Players;
            Knock(players[2], players[1]);
            Knock(players[0], players[3]);
            yield return TestScenes.WaitUntil(() => director.Race.SuddenDeath, 3f, "sudden death");
            Assert.AreEqual(Phase.Playing, RoundManager.Instance.Phase);
            yield return TestScenes.WaitUntil(() => !players[2].IsKnockedOut, 2f, "the knockout to come back");
            Knock(players[2], players[3]);
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.MatchOver, 1f, "sudden death to end");
            Assert.AreEqual(players[3], RoundManager.Instance.LastWinner);
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the result card");
            Assert.AreEqual("AI 4 wins in sudden death!", Card("Heading"));
        }

        [UnityTest]
        public IEnumerator BotsPlayFreeForAllToAWinner()
        {
            yield return Load(HouseModes.FreeForAll);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            director.Target = 1;
            yield return Play(director, stopBots: false);
            var human = Joins.Players[0];
            human.Health.ApplyDamage(HitInfo.Hazard(human.Health.Current - 1f));
            Time.timeScale = 2f;
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.MatchOver, 90f, "a bot to land the first knockout");
            Time.timeScale = 1f;
            Assert.IsTrue(RoundManager.Instance.LastWinner && RoundManager.Instance.LastWinner.Binding is BotBinding);
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the result card");
            StringAssert.Contains(RoundManager.Instance.LastWinner.Name, Card("Heading"));
        }

        [UnityTest]
        public IEnumerator WarSplitsEightSeatsIntoTwoTeamsInOppositeEnds([ValueSource(nameof(Houses))] string map)
        {
            yield return Load(HouseModes.War, map);
            var director = Object.FindAnyObjectByType<WarDirector>();
            Assert.IsNotNull(director);
            Assert.AreEqual(8, Joins.Players.Count, "Bots fill four a side.");
            Assert.AreEqual(new[] { 0, 1, 0, 1, 0, 1, 0, 1 }, Joins.Players.Select(p => p.Team).ToArray());
            Assert.AreEqual(12, director.StartingLives);
            Assert.AreEqual(12, director.Score.Lives(0));
            Assert.AreEqual(12, director.Score.Lives(1));
            foreach (var p in Joins.Players)
            {
                int nearest = Enumerable.Range(0, Joins.SpawnCount).OrderBy(i => (Spot(Joins.SpawnSpot(i)) - p.transform.position).sqrMagnitude).First();
                Assert.AreEqual(p.Team, director.SideOfSpawn(nearest), $"{p.Name} starts in its own end");
            }
            Assert.IsNotNull(director.HoldRoom);
            var safe = Hud.UiCanvas.transform.Find("Safe HUD");
            yield return TestScenes.WaitUntil(() => safe.Find("Player bar/Card 8").gameObject.activeSelf, 1f, "a card for every seat");
            yield return new WaitForSecondsRealtime(.1f);
            var bar = (RectTransform)safe.Find("Player bar");
            var plate = (RectTransform)bar.Find("Backing");
            var corners = new Vector3[4];
            plate.GetWorldCorners(corners);
            var area = new Vector3[4];
            ((RectTransform)safe).GetWorldCorners(area);
            Assert.GreaterOrEqual(corners[0].x, area[0].x, "Eight cards and their plate fit inside the safe area.");
            Assert.LessOrEqual(corners[2].x, area[2].x);
            Assert.LessOrEqual(bar.rect.width, 900f, "Eight snug cards stay within about 900 px.");
            int humans = Joins.HumanCount;
            var teamOf = Joins.Players.ToDictionary(p => SeatTags.Name(p.Name, p == Hud.LocalPlayer, humans), p => p.Team);
            var cards = Enumerable.Range(1, 8).Select(i => (RectTransform)bar.Find($"Card {i}")).ToArray();
            var teams = cards.Select(c => teamOf[c.Find("Name").GetComponent<TMP_Text>().text]).ToArray();
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, teams, "Cards sort by team: one side left, the other right.");
            for (int i = 1; i < 8; i++) Assert.Greater(cards[i].anchoredPosition.x, cards[i - 1].anchoredPosition.x, "left to right");
            float split = cards[4].anchoredPosition.x - cards[3].anchoredPosition.x;
            Assert.AreEqual(PlayerBar.SnugCard + ScoreLine.CentreWidth + 2f * PlayerBar.Outline, split, .5f, "The lives and the clock sit between the teams.");
            Assert.AreEqual(PlayerBar.SnugCard, cards[0].rect.width, .01f);
            Assert.AreEqual(0, plate.GetSiblingIndex(), "The plate sits behind the cards.");
            var span = new Vector3[4];
            foreach (var card in cards)
            {
                card.GetWorldCorners(span);
                Assert.LessOrEqual(corners[0].x, span[0].x, card.name + " sits on the plate");
                Assert.GreaterOrEqual(corners[2].x, span[2].x, card.name);
                Assert.LessOrEqual(corners[0].y, span[0].y, card.name);
                Assert.GreaterOrEqual(corners[2].y, span[2].y, card.name);
            }
            Assert.AreEqual(PlayerBar.PlateWidth(bar.rect.width), plate.rect.width, .01f, "One plate spans both teams and the gap.");
        }

        [Test]
        public void EveryHouseHeadingAndSceneReadsTheRegistry()
        {
            foreach (var mode in HouseModes.All)
            {
                Assert.AreEqual((mode.Tag, mode.Name), GameHud.MatchHeading(mode.Id), mode.Id);
                Assert.IsNotEmpty(Session.SceneForMode(mode.Id), mode.Id);
            }
            Assert.AreEqual(Session.DibsScene, Session.SceneForMode(HouseModes.FreeForAll));
            Assert.AreEqual(Session.DibsScene, Session.SceneForMode(HouseModes.War));
            Assert.AreEqual(Session.MovingDayScene, Session.SceneForMode(HouseModes.MovingDay));
            Assert.AreEqual(("WELCOME HOME", "The house"), GameHud.MatchHeading("Hub"));
            Assert.Throws<System.ArgumentException>(() => Session.SceneForMode("Nope"));
            Assert.IsTrue(HouseModeDirectors.Has(HouseModes.FreeForAll) && HouseModeDirectors.Has(HouseModes.War));
            Assert.IsTrue(HouseModeDirectors.Has(HouseModes.Escape) && HouseModeDirectors.Has(HouseModes.MovingOut));
            Assert.IsTrue(HouseModeDirectors.Has(HouseModes.BossFight));
            Assert.IsFalse(HouseModeDirectors.Has(HouseModes.Dibs), "Dibs! and Duos keep the round manager's own rounds.");
        }

        [Test]
        public void SeatScoresReadAgainstTheirTarget()
        {
            Assert.AreEqual("3/10 KO", GameHud.SeatScore(3, 10, FreeForAllDirector.Unit));
            Assert.AreEqual("2 KO", GameHud.SeatScore(2, 0, FreeForAllDirector.Unit));
            Assert.AreEqual("1/3", GameHud.SeatScore(1, 3, null));
        }

        [UnityTest]
        public IEnumerator TheShippedDataCarriesTheHouseModes()
        {
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsTrue(data.houseModes, "GameData has no modes_house.json. Run Wreckabulary > Data > Set Up Game Data.");
            Assert.IsTrue(data.houseRiddles, "GameData has no riddles_house.json. Run Wreckabulary > Data > Set Up Game Data.");
            Assert.AreEqual(10, GameConfig.Current.RulesFor(HouseModes.FreeForAll).KnockoutsToWin);
            Assert.AreEqual(12, GameConfig.Current.RulesFor(HouseModes.War).TeamLives);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarKnockoutsSpendSharedLivesAndAnEmptyTeamLoses()
        {
            yield return Load(HouseModes.War);
            var director = Object.FindAnyObjectByType<WarDirector>();
            director.StartingLives = 1;
            director.RespawnSeconds = .2f;
            director.HoldSeconds = 0f;
            yield return Play(director);
            var you = Joins.Players[0];
            var them = Joins.Players.Where(p => p.Team == 1).ToArray();
            yield return TestScenes.WaitUntil(() => Score(1).EndsWith(" KO"), 1f, "knockout counts on the cards");
            StringAssert.DoesNotContain("life", Objective(), "the bar already shows the lives, so the side block does not repeat them");
            Knock(them[0], you);
            Assert.AreEqual(0, director.Score.Lives(1));
            yield return TestScenes.WaitUntil(() => Objective().Contains("Team 2 · no lives"), 1f, "a side with no lives left on screen");
            yield return TestScenes.WaitUntil(() => !them[0].IsKnockedOut, 2f, "the spent life to bring them back");
            Assert.AreEqual(1, director.SideOfSpawn(director.HomeSpawn(them[0])), "Knockouts return in their own end.");
            foreach (var p in them) Knock(p, you);
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.MatchOver, 1f, "the empty team to lose");
            Assert.AreEqual(0, RoundManager.Instance.LastWinner.Team);
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, 4f, "the result card");
            Assert.AreEqual("Your team wins!", Card("Heading"));
            Assert.AreEqual("Team 2 ran out of lives.", Card("Flavour"));
            StringAssert.Contains("You 5 KO", Card("Players"));
        }

        [UnityTest]
        public IEnumerator HoldingTheMiddleRoomAloneEarnsALife()
        {
            yield return Load(HouseModes.War);
            var director = Object.FindAnyObjectByType<WarDirector>();
            director.HoldSeconds = .5f;
            yield return Play(director);
            var room = Object.FindAnyObjectByType<RoomBuilder>().Layout.Room(director.HoldRoom);
            var holder = Joins.Players[0];
            holder.Respawn(new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY + .05f, (room.MinZ + room.MaxZ) * .5f));
            yield return TestScenes.WaitUntil(() => director.Score.Lives(0) > director.StartingLives, 3f, "a bonus life");
            Assert.AreEqual(director.StartingLives, director.Score.Lives(1));
            yield return TestScenes.WaitUntil(() => Objective().Contains(GameHud.Spaced(director.HoldRoom)), 1f, "the hold room on screen");
        }
    }
}
