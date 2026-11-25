using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class LobbyMapSelectionTests
    {
        string savedCareer;
        bool hadCareer;
        float savedVolume;
        int savedVsync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadCareer = PlayerPrefs.HasKey(MatchTally.CareerKey);
            savedCareer = PlayerPrefs.GetString(MatchTally.CareerKey);
            savedVolume = AudioListener.volume;
            savedVsync = QualitySettings.vSyncCount;
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.IsNotNull(LobbyMenu.Instance);
            yield return TestScenes.WaitUntil(() => !GameHud.Active.StateController.IsTransitioning,
                1f, "lobby fade and menu input focus");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (hadCareer) PlayerPrefs.SetString(MatchTally.CareerKey, savedCareer);
            else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            PlayerPrefs.Save();
            AudioListener.volume = savedVolume;
            QualitySettings.vSyncCount = savedVsync;
        }

        [UnityTest]
        public IEnumerator EscapeUsesTheTickedHousesLikeEveryOtherMode()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Escape");
            Assert.IsTrue(GameConfig.Current.Houses.ContainsKey(menu.Map), "the lobby always shows a real house");
            Assert.IsTrue(menu.Maps.All, "every house starts ticked");
            StringAssert.Contains(PracticeMaps.Every, menu.Describe());

            menu.Choose(mode: "Escape", map: "courtyard");
            Assert.AreEqual("courtyard", menu.Map, "Escape no longer overrides the chosen house");
            Assert.AreEqual("courtyard", Session.MapId);

            menu.Choose(mode: "Dibs");
            Assert.AreEqual("courtyard", menu.Map, "leaving Escape keeps the house");
            Assert.AreEqual("Dibs", menu.Mode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TicksStayAcrossHouseModesEscapeAndTheHomeBuilder()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs");
            menu.PlayOnly("courtyard");
            Assert.IsTrue(menu.ToggleMap("flat"));
            string[] ticked = menu.Maps.Ticked.ToArray();
            CollectionAssert.AreEquivalent(new[] { "courtyard", "flat" }, ticked);
            Assert.AreEqual(menu.Maps.Save(), Session.LobbyMaps, "the ticks are remembered for the session");
            foreach (string mode in LobbyMenu.Modes)
            {
                menu.Choose(mode: mode);
                CollectionAssert.AreEqual(ticked, menu.Maps.Ticked, mode);
                string fixedHouse = HouseModes.FixedHouse(mode);
                if (fixedHouse == null) StringAssert.Contains("Any of 2 houses", menu.Describe(), mode);
                else StringAssert.Contains(GameConfig.Current.HouseFor(fixedHouse).Name, menu.Describe(), mode + " names the one house it always plays in");
            }
            menu.Choose(LobbyMenu.Workshop);
            Assert.IsTrue(HomeDesigner.Supports(menu.Map), "The Home builder opens a real house.");
            menu.Choose(LobbyMenu.Practice, "Duos");
            CollectionAssert.AreEqual(ticked, menu.Maps.Ticked, "Leaving the Home builder keeps the ticks.");
            Assert.AreEqual("flat", menu.Map);
            Assert.IsTrue(menu.ToggleMap("flat"));
            Assert.AreEqual("courtyard", menu.Map, "unticking the shown house moves the lobby to a ticked one");
            Assert.IsFalse(menu.ToggleMap("courtyard"), "the last ticked house stays ticked");
            CollectionAssert.AreEqual(new[] { "courtyard" }, menu.Maps.Ticked);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TicksSurviveAMatchAndTheReturnToTheLobby()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "War");
            Assert.IsTrue(menu.ToggleMap("walkup"));
            string[] ticked = menu.Maps.Ticked.ToArray();
            string next = menu.NextMap();
            CollectionAssert.Contains(ticked, next);
            Session.SelectMap(next);
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.AreEqual(next, LobbyMenu.Instance.Map, "the lobby shows the house that was played");
            CollectionAssert.AreEqual(ticked, LobbyMenu.Instance.Maps.Ticked);
            Assert.AreEqual("War", LobbyMenu.Instance.Mode);
            StringAssert.Contains("Any of " + ticked.Length + " houses", LobbyMenu.Instance.Describe());
        }

        [UnityTest]
        public IEnumerator EveryHouseModePicksATickedRealHouseBeforeItLoads()
        {
            var houses = GameConfig.Current.Houses;
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs");
            foreach (string mode in LobbyMenu.Modes)
            {
                menu.Choose(mode: mode);
                var seen = Enumerable.Range(0, 40).Select(_ => menu.NextMap()).ToArray();
                Assert.IsTrue(seen.All(houses.ContainsKey) && seen.All(menu.Maps.IsTicked), mode + ": " + string.Join(", ", seen));
            }
            menu.PlayOnly("flat");
            Assert.AreEqual("flat", menu.NextMap(), "One ticked house always plays.");
            Assert.AreEqual("flat", Session.ResolveMap("Dibs", "flat"), "An explicit house is kept.");
            Assert.IsNull(Session.ResolveMap("Dibs", null), "No map keeps the current house.");
            Assert.IsNull(Session.ResolveMap(LobbyMenu.TutorialMode, null), "The Tutorial has its own room.");
            Assert.AreEqual(HouseModes.FixedHouse(HouseModes.BossFight), Session.ResolveMap(HouseModes.BossFight, "flat"), "Boss Fight always plays in its own house.");
            yield return null;
        }
    }
}
