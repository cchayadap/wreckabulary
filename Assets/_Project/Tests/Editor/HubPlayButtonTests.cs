using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class HubPlayButtonTests
    {
        static bool hubLoadedEvent;
        SceneSetup[] setup;
        string savedCareer;

        static void NoteLoad(Scene scene, LoadSceneMode mode) => hubLoadedEvent |= scene.name == Session.HubScene;

        [SetUp]
        public void SetUp()
        {
            setup = EditorSceneManager.GetSceneManagerSetup();
            savedCareer = PlayerPrefs.HasKey(MatchTally.CareerKey) ? PlayerPrefs.GetString(MatchTally.CareerKey) : null;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= NoteLoad;
            if (savedCareer != null) PlayerPrefs.SetString(MatchTally.CareerKey, savedCareer);
            else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            PlayerPrefs.Save();
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }

        [UnityTest]
        public IEnumerator PressingPlayWithTheHubOpenShowsTheLobby()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Hub.unity");
            hubLoadedEvent = false;
            SceneManager.sceneLoaded += NoteLoad;
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            TestContext.WriteLine($"Enter Play Mode Options: enabled={EditorSettings.enterPlayModeOptionsEnabled} " +
                $"options={EditorSettings.enterPlayModeOptions}; sceneLoaded raised for Hub: {hubLoadedEvent}");
            Assert.IsNotNull(Object.FindAnyObjectByType<LobbyMenu>(), "the lobby opens when Play is pressed in the Hub");
            yield return new ExitPlayMode();
        }
    }
}
