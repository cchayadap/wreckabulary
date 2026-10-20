using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public IEnumerator LeavingEscapeHonoursAnExplicitMapSelection()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(mode: "Escape");
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual("pinwheel", Session.MapId);

            menu.Choose(mode: "Dibs", map: "courtyard");

            Assert.AreEqual("courtyard", menu.Map);
            Assert.AreEqual("courtyard", Session.MapId);
            Assert.AreEqual("Dibs", menu.Mode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingEscapeWithoutAMapRestoresThePreviousHouse()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(map: "courtyard");
            menu.Choose(mode: "Escape");
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);

            menu.Choose(mode: "Dibs");

            Assert.AreEqual("courtyard", menu.Map);
            Assert.AreEqual("courtyard", Session.MapId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeStillSelectsARandomHouseEvenWithAnExplicitMap()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(mode: "Escape", map: "courtyard");

            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual("pinwheel", Session.MapId);
            Assert.AreEqual("Escape", menu.Mode);
            yield return null;
        }
    }
}
