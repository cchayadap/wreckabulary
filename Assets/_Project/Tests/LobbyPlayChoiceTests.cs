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
        public IEnumerator CompactWordboundFamiliesRefitWithoutChangingChoicesOrFooter()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Submit("Family Wordbound");
            yield return null;
            Assert.IsFalse(Find("Solo GO").interactable);
            var focus = EventSystem.current.currentSelectedGameObject;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 920), new Vector2(900, 580), new Vector2(1868, 968) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero;
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertCompactFamily(size, "Solo modes", "Solo summary");
                Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "resizing must not recreate focus");
                Assert.IsFalse(Find("Solo GO").interactable, "the extra-seat guard survives refit");
            }
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            Submit("Family house");
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertFullHousePanel();
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            Submit("Create Wordbound");
            yield return null;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertCompactFamily(size, "Wordbound workshop", "Workshop summary");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
            }
            Submit("Create house");
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertFullHousePanel();
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }, Modes);
            menu.Leave(second);
        }

        static void AssertFullHousePanel()
        {
            var panel = Page.Find("Panel") as RectTransform;
            Assert.IsNull(panel.GetComponent<LobbyCompactPanel>());
            Assert.That(panel.rect.width, Is.EqualTo(Page.rect.width).Within(.1f));
            Assert.That(panel.rect.height, Is.EqualTo(Page.rect.height).Within(.1f));
        }

        static void AssertCompactFamily(Vector2 available, string rowName, string summaryName)
        {
            var panel = Page.Find("Panel") as RectTransform;
            Assert.IsNotNull(panel.GetComponent<LobbyCompactPanel>());
            Assert.That(panel.rect.width, Is.EqualTo(Mathf.Min(1100, available.x)).Within(.1f));
            Assert.That(panel.rect.height, Is.EqualTo(Mathf.Min(600, available.y)).Within(.1f));
            var panelBounds = InPage(panel);
            Assert.That(panelBounds.xMax, Is.EqualTo(Page.rect.xMax).Within(.1f));
            Assert.That(panelBounds.yMax, Is.EqualTo(Page.rect.yMax).Within(.1f));
            var rects = panel.GetComponentsInChildren<RectTransform>();
            var row = InPage(rects.Single(rect => rect.name == rowName));
            var summary = InPage(rects.Single(rect => rect.name == summaryName));
            Assert.LessOrEqual(summary.yMax, row.yMin - 1f, "the footer must not overlap cards");
            foreach (var button in panel.GetComponentsInChildren<Button>())
            {
                var bounds = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(bounds.xMin, panelBounds.xMin - .1f, button.name);
                Assert.LessOrEqual(bounds.xMax, panelBounds.xMax + .1f, button.name);
                Assert.GreaterOrEqual(bounds.yMin, panelBounds.yMin - .1f, button.name);
                Assert.LessOrEqual(bounds.yMax, panelBounds.yMax + .1f, button.name);
            }
            foreach (var text in panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        static Rect InPage(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = Page.InverseTransformPoint(corners[0]);
            var max = Page.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void LocalCreatorIntentIsOneShotExclusiveAndResetWithSession()
        {
            WordboundSoloEntry.Request("Freestyle");
            WordboundWorkshopEntry.Request();
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.IsTrue(WordboundWorkshopEntry.TakeRequest());
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            WordboundWorkshopEntry.MarkReturned();
            Assert.IsTrue(WordboundWorkshopEntry.TakeReturn());
            Assert.IsFalse(WordboundWorkshopEntry.TakeReturn());
            WordboundWorkshopEntry.Request();
            WordboundSoloEntry.Request("TimeRun");
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            Assert.AreEqual("TimeRun", WordboundSoloEntry.TakeRequest());
            WordboundWorkshopEntry.Request(); WordboundWorkshopEntry.MarkReturned();
            Session.Clear();
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            Assert.IsFalse(WordboundWorkshopEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator CreateFamilyKeepsHomeModeMapAndEveryLocalSeat()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            Assert.AreEqual("courtyard", menu.Map, "Start with a supported, non-default home-builder map.");
            var second = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(second);
            var bindings = Session.Bindings.ToArray();
            Submit("Create Wordbound"); yield return null;
            Assert.IsTrue(menu.Page<PlayPage>().WordboundMaps);
            Assert.IsEmpty(Modes);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            Assert.IsTrue(Find("Workshop OPEN").interactable);
            Assert.AreEqual("Local editor. One-player playtests.", Page.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Workshop detail").text);
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue); Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual(2, menu.PartySize);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            Submit("Create house"); yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }, Modes);
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode); Assert.AreEqual("courtyard", menu.Map);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            menu.Leave(second);
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
            Canvas.ForceUpdateCanvases();
            var random = Find("Map random");
            Assert.IsTrue(random.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Random house"));
            foreach (var text in random.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "random house card: " + text.text);
            }
            Assert.IsFalse(menu.Messages.Any(message => message == "Random house every run." || message.StartsWith("Map: ")),
                "map state is shown in the selected card and footer, without stale map notifications");
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

        [Test]
        public void SoloIntentIsExplicitOneShotAndRejectsMultiplayerModes()
        {
            try
            {
                foreach (string mode in new[] { "TeamRace", "FreeForAll", "Brawl", "Dibs", "", null })
                    Assert.Throws<System.ArgumentException>(() => WordboundSoloEntry.Request(mode));
                foreach (string mode in new[] { "TimeRun", "Freestyle" })
                {
                    WordboundSoloEntry.Request(mode);
                    Assert.AreEqual(mode, WordboundSoloEntry.TakeRequest());
                    Assert.IsNull(WordboundSoloEntry.TakeRequest());
                }
                WordboundSoloEntry.Request("TimeRun");
                WordboundSoloEntry.CancelRequest();
                Assert.IsNull(WordboundSoloEntry.TakeRequest());
                WordboundSoloEntry.MarkReturned();
                Assert.IsTrue(WordboundSoloEntry.TakeReturn());
                Assert.IsFalse(WordboundSoloEntry.TakeReturn());
                WordboundSoloEntry.Request("Freestyle");
                Session.Clear();
                Assert.IsNull(WordboundSoloEntry.TakeRequest());
                Assert.AreEqual("TimeRun", WordboundSoloEntry.SelectedMode);
            }
            finally { WordboundSoloEntry.Clear(); }
        }

        [UnityTest]
        public IEnumerator SoloFamilyPreservesHouseSelectionPartyAndOriginalArtwork()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            var second = new ScriptedBinding();
            menu.Join(second);
            var bindings = Session.Bindings.ToArray();
            yield return null;
            Submit("Family Wordbound");
            yield return null;
            Assert.IsEmpty(Modes);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            CollectionAssert.AreEqual(new[] { "Solo TimeRun", "Solo Freestyle" },
                Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Solo ") && button.name != "Solo GO")
                    .Select(button => button.name).ToArray());
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreSame(second, menu.Party.Single());
            CollectionAssert.AreEqual(bindings, Session.Bindings);
            Assert.IsFalse(Find("Solo GO").interactable);
            menu.OpenWordboundSolo("TimeRun");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Submit("Solo Freestyle");
            yield return null;
            Assert.AreEqual("Freestyle", WordboundSoloEntry.SelectedMode);
            foreach (var pair in new[] { ("Solo TimeRun", "CLOCK"), ("Solo Freestyle", "BOX") })
            {
                Assert.IsTrue(Wreckabulary.UI.ItemArt.TryGet(pair.Item2, out var texture, out var uv));
                var art = Find(pair.Item1).GetComponentsInChildren<RawImage>().Single(image => image.name == "Art " + pair.Item2);
                Assert.AreSame(texture, art.texture); Assert.AreEqual(uv, art.uvRect);
            }
            Submit("Family house");
            yield return null;
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            menu.Leave(second);
            Submit("Family Wordbound");
            yield return null;
            Assert.IsTrue(Find("Solo GO").interactable);
            Canvas.ForceUpdateCanvases();
            foreach (var text in Page.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        [UnityTest]
        public IEnumerator OnlineSubmitLoadsWordboundWithoutReclassifyingTheLocalSelection()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            WordboundSoloEntry.Request("Freestyle");
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return TestScenes.WaitForActive(Session.WordboundScene);
            Assert.AreEqual(Session.WordboundScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.AreEqual(LobbyMenu.Practice, Session.LobbyQueue);
            Assert.AreEqual("Duos", Session.LobbyMode);
            Assert.AreEqual("flat", Session.MapId);
            Assert.IsNull(Object.FindAnyObjectByType<RoundManager>(), "no legacy battle starts on the online route");
            Assert.IsEmpty(Object.FindObjectsByType<BotController>(FindObjectsSortMode.None));
        }
    }
}
