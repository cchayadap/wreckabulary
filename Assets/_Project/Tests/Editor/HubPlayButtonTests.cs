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
        const string RestoreKey = "Wreckabulary.Tests.HubPlayButton.Restore";
        static bool hubLoadedEvent;

        [System.Serializable]
        sealed class SavedScene
        {
            public string Path;
            public bool Loaded;
            public bool Active;
        }

        [System.Serializable]
        sealed class RestoreState
        {
            public SavedScene[] Scenes;
            public bool HadCareer;
            public string Career;
        }

        static void NoteLoad(Scene scene, LoadSceneMode mode) => hubLoadedEvent |= scene.name == Session.HubScene;

        [SetUp]
        public void SetUp()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(RestoreKey, ""))) return;
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode, "Capture the original editor state before entering Play Mode.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scenes = new SavedScene[setup.Length];
            for (int i = 0; i < setup.Length; i++)
                scenes[i] = new SavedScene { Path = setup[i].path, Loaded = setup[i].isLoaded, Active = setup[i].isActive };
            SessionState.SetString(RestoreKey, JsonUtility.ToJson(new RestoreState
            {
                Scenes = scenes,
                HadCareer = PlayerPrefs.HasKey(MatchTally.CareerKey),
                Career = PlayerPrefs.GetString(MatchTally.CareerKey)
            }));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= NoteLoad;
            if (EditorApplication.isPlayingOrWillChangePlaymode) yield return new ExitPlayMode();
            SceneManager.sceneLoaded -= NoteLoad;
            string json = SessionState.GetString(RestoreKey, "");
            if (string.IsNullOrEmpty(json)) yield break;
            var saved = JsonUtility.FromJson<RestoreState>(json);
            try
            {
                if (saved.Scenes != null && saved.Scenes.Length > 0 &&
                    System.Array.TrueForAll(saved.Scenes, scene => !string.IsNullOrEmpty(scene.Path)))
                {
                    var setup = new SceneSetup[saved.Scenes.Length];
                    for (int i = 0; i < setup.Length; i++)
                        setup[i] = new SceneSetup
                        {
                            path = saved.Scenes[i].Path,
                            isLoaded = saved.Scenes[i].Loaded,
                            isActive = saved.Scenes[i].Active
                        };
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            finally
            {
                if (saved.HadCareer) PlayerPrefs.SetString(MatchTally.CareerKey, saved.Career);
                else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
                PlayerPrefs.Save();
            }
            SessionState.EraseString(RestoreKey);
        }

        [UnityTest]
        public IEnumerator PressingPlayWithTheHubOpenShowsTheLobby()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Hub.unity");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Hub must be the open scene before pressing Play.");
            hubLoadedEvent = false;
            SceneManager.sceneLoaded += NoteLoad;
            bool reloadDomain = !EditorSettings.enterPlayModeOptionsEnabled ||
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            yield return new EnterPlayMode(expectDomainReload: reloadDomain);
            yield return null;
            yield return null;
            TestContext.WriteLine($"Enter Play Mode Options: enabled={EditorSettings.enterPlayModeOptionsEnabled} " +
                $"options={EditorSettings.enterPlayModeOptions}; sceneLoaded raised for Hub: {hubLoadedEvent}");
            Assert.IsNotEmpty(SessionState.GetString(RestoreKey, ""), "The restoration checkpoint survives a Play Mode domain reload.");
            Assert.IsTrue(EditorApplication.isPlaying, "The lobby assertion must run during Play Mode.");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.IsNotNull(Object.FindAnyObjectByType<LobbyMenu>(), "the lobby opens when Play is pressed in the Hub");
        }
    }
}
