using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPlayChoiceTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey };
        string[] saved;
        float volume;
        int vsync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "play choices ready");
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
            AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
        }

        static RectTransform Page => LobbyMenu.Instance.Page<PlayPage>().Root;
        static Button Find(string name) => Page.GetComponentsInChildren<Button>().Single(button => button.name == name);
        static string Detail => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail").text;
        static string[] Modes => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode "))
            .Select(button => button.name.Substring(5)).ToArray();

        static void Submit(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.IsInteractable(), name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        [UnityTest]
        public IEnumerator LocalAndCreateOfferActualModesWithConciseCards()
        {
            Assert.AreEqual("LOCAL", Find("Queue " + LobbyMenu.Practice).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("ONLINE", Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("CREATE", Find("Queue " + LobbyMenu.Workshop).GetComponentInChildren<TMP_Text>().text);
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            foreach (var button in Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode ")))
            {
                Assert.AreEqual(2, button.GetComponentsInChildren<TMP_Text>().Length, "title and category only: " + button.name);
                string item = LobbyKit.ModeArt(button.name.Substring(5));
                Assert.IsTrue(Wreckabulary.UI.ItemArt.TryGet(item, out var texture, out var uv));
                var art = button.GetComponentsInChildren<RawImage>().Single(image => image.name == "Art " + item);
                Assert.AreSame(texture, art.texture, "the existing item artwork is retained: " + button.name);
                Assert.AreEqual(uv, art.uvRect);
            }
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }, Modes);
            Assert.AreEqual(LobbyMenu.Workshop, LobbyMenu.Instance.Queue);
            Assert.AreEqual(LobbyMenu.TutorialMode, LobbyMenu.Instance.Mode);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            Assert.IsTrue(Page.GetComponentsInChildren<Transform>().Any(child => child.name == "Tutorial room"));
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            Assert.AreEqual("Dibs", LobbyMenu.Instance.Mode);
            Assert.AreSame(Find("Queue " + LobbyMenu.Practice).gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator SummaryUsesActualBotPolicyAndUpdatesWhenAPlayerJoins()
        {
            var menu = LobbyMenu.Instance;
            foreach (string mode in LobbyMenu.Modes)
            {
                Submit("Mode " + mode);
                yield return null;
                Assert.AreEqual(mode == "Dibs" || mode == "Duos", Detail.ToLowerInvariant().Contains("bot"), mode);
                Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_Text>().Count(text => text.name == "Mode detail"));
                Canvas.ForceUpdateCanvases();
                var detail = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail");
                detail.ForceMeshUpdate();
                Assert.IsFalse(detail.isTextOverflowing, "selected objective/participants fit: " + mode);
                Assert.IsTrue(Find("GO").interactable);
            }
            Submit("Mode Duos");
            yield return null;
            menu.Join(new ScriptedBinding());
            yield return null;
            StringAssert.Contains("2 players, each with a bot", Detail);
            Assert.AreSame(Find("Mode Duos").gameObject, EventSystem.current.currentSelectedGameObject,
                "party refresh preserves selected mode focus");
            Submit("Mode Escape");
            yield return null;
            StringAssert.Contains("2 players", Detail);
            Assert.IsFalse(Detail.ToLowerInvariant().Contains("bot"));
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual(1, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")));
        }

        [UnityTest]
        public IEnumerator OldTutorialSelectionAppearsInCreateWithoutChangingItsLaunch()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, LobbyMenu.TutorialMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }, Modes);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.IsTrue(Find("GO").interactable);
            Submit("Mode " + LobbyMenu.WorkshopMode);
            yield return null;
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue);
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual("Dibs", menu.Mode);
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
        }

        [UnityTest]
        public IEnumerator StaleMatchmakingCannotLaunchHouseModesOrOfferFakeOnlineCards()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Matchmaking, LobbyMenu.RoomMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.IsEmpty(Modes);
            Assert.IsFalse(Find("GO").interactable);
            menu.Go();
            Assert.IsFalse(menu.Starting);
            StringAssert.Contains("local", menu.Messages[0]);
            Assert.IsTrue(Find("Queue " + LobbyMenu.Matchmaking).interactable, "the working online front door remains available");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual("Dibs", menu.Mode);
            Assert.IsTrue(Find("GO").interactable);
        }

        [UnityTest]
        public IEnumerator OnlineSubmitLoadsWordboundWithoutReclassifyingTheLocalSelection()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return TestScenes.WaitForActive(Session.WordboundScene);
            Assert.AreEqual(Session.WordboundScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(LobbyMenu.Practice, Session.LobbyQueue);
            Assert.AreEqual("Duos", Session.LobbyMode);
            Assert.AreEqual("flat", Session.MapId);
            Assert.IsNull(Object.FindAnyObjectByType<RoundManager>(), "no legacy battle starts on the online route");
            Assert.IsEmpty(Object.FindObjectsByType<BotController>(FindObjectsSortMode.None));
        }
    }
}
