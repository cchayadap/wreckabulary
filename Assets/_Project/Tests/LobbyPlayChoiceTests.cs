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
        public IEnumerator CenteredDirectModesStayVisibleWhenTheSharedMapGalleryScrolls()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                Submit("Solo TimeRun"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var row = frame.Body.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Modes");
                var queueBounds = InPage(frame.Queues); var modeBounds = InPage(row);
                var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
                Assert.That(queueBounds.center.x, Is.EqualTo(InPage(frame.Panel).center.x).Within(.5f));
                Assert.That(modeBounds.center.x, Is.EqualTo(InPage(frame.Panel).center.x).Within(.5f));
                Assert.LessOrEqual(modeBounds.yMax, queueBounds.yMin);
                Assert.GreaterOrEqual(modeBounds.yMin, InPage(scroll.viewport).yMax);
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Family ")));
                foreach (string id in new[] { "Mode Dibs", "Mode Duos", "Mode MovingOut", "Mode MovingDay", "Mode Escape", "Solo TimeRun", "Solo Freestyle" })
                {
                    var button = Find(id); var bounds = InPage((RectTransform)button.transform);
                    Assert.IsTrue(button.IsInteractable()); Assert.IsTrue(button.transform.IsChildOf(row));
                    Assert.GreaterOrEqual(bounds.yMin, modeBounds.yMin - .5f); Assert.LessOrEqual(bounds.yMax, modeBounds.yMax + .5f);
                }
                Submit("Solo map ParcelService"); yield return null; yield return null;
                frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                row = frame.Body.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Modes");
                AssertSameBounds(modeBounds, InPage(row), "Selecting/revealing a map cannot scroll the mode selector.");
                AssertSameBounds(queueBounds, InPage(frame.Queues), "The category selector remains fixed.");
                Submit("Solo Freestyle"); yield return null;
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap);
                Submit("Mode Duos"); yield return null;
                Assert.AreEqual("flat", menu.Map); Assert.AreEqual("Duos", menu.Mode);
            }
        }

        [UnityTest]
        public IEnumerator SingleChoiceCardsKeepTheHouseCardFootprintAndFixedActions()
        {
            var menu = LobbyMenu.Instance;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 580) })
            {
                menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
                menu.Page<PlayPage>().WordboundSolo = false;
                menu.Open(LobbyMenu.Play);
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var reference = InPage((RectTransform)Find("Map pinwheel").transform);
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var header = InPage(frame.Queues);
                var footer = InPage((RectTransform)Find("GO").transform);

                Submit("Mode Escape"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var random = Find("Map random");
                Assert.AreEqual(1, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")),
                    "Random generation remains the only map choice for Escape.");
                AssertCardFootprint(reference, (RectTransform)random.transform);
                EventSystem.current.SetSelectedGameObject(random.gameObject);
                yield return null; yield return null;
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Play stays fixed.");
                Assert.AreSame(random.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.AreEqual(Session.LobbyRandomMap, menu.Map);

                Submit("Queue " + LobbyMenu.Workshop); yield return null;
                Submit("Create Wordbound"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var editor = Find("Wordbound maps");
                AssertCardFootprint(reference, (RectTransform)editor.transform);
                Assert.AreEqual(1, editor.transform.parent.GetComponentsInChildren<Button>().Length,
                    "The editor card does not add fake choices to fill the row.");
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Creator header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("Workshop OPEN").transform), "Open uses the fixed action slot.");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
                foreach (var text in editor.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.text);
                }
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Choosing a creator family must not launch it.");
            }
        }

        static void AssertCardFootprint(Rect reference, RectTransform card)
        {
            var actual = InPage(card);
            Assert.That(actual.width, Is.EqualTo(reference.width).Within(.1f), "One card keeps the multi-map card width.");
            Assert.That(actual.height, Is.EqualTo(reference.height).Within(.1f), "One card keeps the multi-map card height.");
            Assert.That(actual.xMin, Is.EqualTo(reference.xMin).Within(.1f), "One card aligns with the first map column.");
        }

        [UnityTest]
        public IEnumerator ShortHouseChooserScrollsFocusedChoicesWithoutMovingHeaderOrPlay()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero;
            Page.sizeDelta = new Vector2(900, 580);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll, "Short house choices need a reachable scroll region.");
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);
            var header = InPage(frame.Queues);
            var footer = InPage((RectTransform)Find("GO").transform);
            var viewport = InPage(scroll.viewport);
            Assert.LessOrEqual(viewport.yMax, header.yMin);
            Assert.GreaterOrEqual(viewport.yMin, footer.yMax);
            Assert.GreaterOrEqual(((RectTransform)Find("Mode Dibs").transform).rect.height, 44,
                "The compact fixed mode strip retains accessible targets.");
            var mapNames = Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map "))
                .Select(button => button.name).ToArray();
            Assert.AreEqual(GameConfig.Current.Houses.Count, mapNames.Length, "Every installed house remains focusable.");
            foreach (string name in mapNames)
            {
                var target = Find(name);
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertSameBounds(viewport, InPage(scroll.viewport), "Revealing a card cannot change the fixed map viewport.");
                var visible = InPage((RectTransform)target.transform);
                Assert.GreaterOrEqual(visible.yMin, viewport.yMin - .1f, name + " enters the viewport");
                Assert.LessOrEqual(visible.yMax, viewport.yMax + .1f, name + " enters the viewport");
                AssertSameBounds(header, InPage(frame.Queues), "Scrolling must not move the queue strip.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Play stays fixed.");
                Assert.AreSame(target.gameObject, EventSystem.current.currentSelectedGameObject);
            }
            Page.sizeDelta = new Vector2(1868, 968);
            EventSystem.current.SetSelectedGameObject(Find("Mode Dibs").gameObject);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(scroll.content.rect.height > scroll.viewport.rect.height + .1f,
                scroll.verticalScrollbar.gameObject.activeSelf, "Only overflowing map galleries expose a scrollbar.");
            Assert.AreEqual("Mode Dibs", EventSystem.current.currentSelectedGameObject.name);
            var map = Find("Map flat");
            EventSystem.current.SetSelectedGameObject(map.gameObject);
            yield return null;
            yield return null;
            Page.sizeDelta = new Vector2(900, 580);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            viewport = InPage(scroll.viewport);
            var focused = InPage((RectTransform)map.transform);
            Assert.AreSame(map.gameObject, EventSystem.current.currentSelectedGameObject, "Resize keeps the selected map.");
            Assert.GreaterOrEqual(focused.yMin, viewport.yMin - .1f, "Resize reveals the same selected map.");
            Assert.LessOrEqual(focused.yMax, viewport.yMax + .1f, "Resize reveals the same selected map.");
            scroll.verticalNormalizedPosition = 1f;
            yield return null;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f),
                "Manual scrolling must not jump back to an unchanged selection.");
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
        }

        [UnityTest]
        public IEnumerator WordboundFamiliesFillThePageWithoutChangingChoicesOrFooter()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Submit("Solo TimeRun");
            yield return null;
            Assert.IsFalse(Find("Solo GO").interactable);
            var focus = EventSystem.current.currentSelectedGameObject;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 920), new Vector2(900, 580), new Vector2(1868, 968) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero;
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, "Modes", "Solo summary");
                Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "resizing must not recreate focus");
                Assert.IsFalse(Find("Solo GO").interactable, "the extra-seat guard survives refit");
            }
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            Submit("Mode Duos");
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
                AssertFullFamily(size, "Wordbound workshop", "Workshop summary");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
            }
            Submit("Create house");
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertFullHousePanel();
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes);
            menu.Leave(second);
        }

        [UnityTest]
        public IEnumerator EveryPlayFamilyKeepsTheFullSurfaceAndSavedProfileAcrossResize()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            string savedProfile = JsonUtility.ToJson(profile);
            using var viewport = new StageViewport(menu);
            foreach (string family in new[] { "Solo TimeRun", "Mode Duos", "Create Wordbound", "Create house" })
            {
                if (family == "Create Wordbound") { Submit("Queue " + LobbyMenu.Workshop); yield return null; }
                Submit(family); yield return null;
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var focus = EventSystem.current.currentSelectedGameObject;
                foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(854, 480), new Vector2(2560, 1080), new Vector2(1920, 1080) })
                {
                    viewport.Set(size.x, size.y); yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.AreSame(frame, Page.GetComponentInChildren<LobbyPlayPageView>(), "Resize must not recreate the frame.");
                    Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "Resize retains keyboard/controller focus.");
                    AssertFullHousePanel();
                    Assert.AreEqual(LobbyStage.Focus.Centre, menu.Page<PlayPage>().Focus, "Play does not reserve space for an avatar.");
                }
                if (family == "Create house")
                {
                    Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
                    Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The home builder must normalize an unsupported house choice.");
                }
                else Assert.AreEqual("flat", menu.Map, "Presentation and map-editor choices preserve the original house selection.");
            }
            Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map));
            Assert.AreEqual(savedProfile, JsonUtility.ToJson(profile), "Presentation must not mutate the saved profile.");
        }

        sealed class StageViewport : System.IDisposable
        {
            readonly Canvas canvas;
            readonly CanvasScaler scaler;
            readonly Camera camera;
            readonly Vector2 size;
            readonly float scale, aspect;
            readonly bool scaled;
            public readonly RectTransform Safe;

            public StageViewport(LobbyMenu menu)
            {
                canvas = menu.GetComponent<Canvas>(); scaler = menu.GetComponent<CanvasScaler>(); camera = menu.Stage.Camera;
                Safe = (RectTransform)menu.transform.Find("Safe area");
                size = Safe.sizeDelta; scale = canvas.scaleFactor; scaled = scaler.enabled; aspect = camera.aspect;
                scaler.enabled = false; canvas.scaleFactor = Screen.height / 1080f;
                Canvas.ForceUpdateCanvases();
            }

            public void Set(float width, float height)
            {
                var requested = new Vector2(width / height * 1080f, 1080f);
                Safe.sizeDelta += requested - Safe.rect.size;
                camera.aspect = width / height;
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (Safe) Safe.sizeDelta = size;
                if (camera) camera.aspect = aspect;
                if (canvas) canvas.scaleFactor = scale;
                if (scaler) scaler.enabled = scaled;
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        public IEnumerator SavedPlayFrameSuppliesRuntimeRegionsWithoutReplacingActions()
        {
            var saved = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            Assert.IsNotNull(saved); saved.Validate();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            Assert.IsNotNull(frame); Assert.AreNotSame(saved.Page, frame);
            Assert.AreEqual(saved.Page.ContentInset, frame.ContentInset);
            Assert.AreEqual(saved.Page.HeaderHeight, frame.HeaderHeight);
            Assert.AreEqual(saved.Page.SummaryHeight, frame.SummaryHeight);
            AssertFullHousePanel();
            Assert.IsTrue(Find("Queue " + LobbyMenu.Practice).transform.IsChildOf(frame.Queues));
            Assert.IsTrue(Find("GO").transform.IsChildOf(frame.Summary));
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            yield return null;
        }

        static void AssertFullHousePanel()
        {
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            Assert.IsNotNull(frame, "The real Play page must instantiate the saved frame.");
            var panel = frame.Panel;
            Assert.IsNull(panel.GetComponent<LobbyCompactPanel>());
            Assert.That(panel.rect.width, Is.EqualTo(Page.rect.width).Within(.1f));
            Assert.That(panel.rect.height, Is.EqualTo(Page.rect.height).Within(.1f));
        }

        static void AssertFullFamily(Vector2 available, string rowName, string summaryName)
        {
            AssertFullHousePanel();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            var panelBounds = InPage(frame.Panel);
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll, "Every family keeps a reachable choices viewport.");
            var viewport = InPage(scroll.viewport);
            var summary = InPage(frame.Summary);
            Assert.LessOrEqual(summary.yMax, viewport.yMin - 1f, "The footer stays below choices.");
            Assert.LessOrEqual(viewport.yMax, InPage(frame.Queues).yMin, "Queue choices stay outside the scroll region.");
            Assert.IsTrue(frame.Body.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == rowName));
            Assert.IsTrue(frame.Summary.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == summaryName));
            foreach (var button in frame.Queues.GetComponentsInChildren<Button>().Concat(frame.Summary.GetComponentsInChildren<Button>()))
            {
                var bounds = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(bounds.xMin, panelBounds.xMin - .1f, button.name);
                Assert.LessOrEqual(bounds.xMax, panelBounds.xMax + .1f, button.name);
                Assert.GreaterOrEqual(bounds.yMin, panelBounds.yMin - .1f, button.name);
                Assert.LessOrEqual(bounds.yMax, panelBounds.yMax + .1f, button.name);
            }
            foreach (var text in frame.Panel.GetComponentsInChildren<TMP_Text>())
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

        static void AssertSameBounds(Rect expected, Rect actual, string message)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(.1f), message);
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(.1f), message);
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(.1f), message);
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(.1f), message);
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
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            Assert.IsTrue(Find("Workshop OPEN").interactable);
            Assert.AreEqual("Local editor. One-player playtests.", Page.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Workshop detail").text);
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue); Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual(2, menu.PartySize);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            Submit("Create house"); yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes);
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode); Assert.AreEqual("courtyard", menu.Map);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            menu.Leave(second);
        }

        [UnityTest]
        public IEnumerator CreateToolsKeepOneSelectedModeAfterTutorialAndEditorSwitches()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            var bindings = Session.Bindings.ToArray();
            foreach (string id in new[] { "Mode " + LobbyMenu.TutorialMode, "Create Wordbound", "Create house" })
            {
                Submit(id); yield return null;
                Canvas.ForceUpdateCanvases();
                var row = Page.GetComponentInChildren<LobbyPlayPageView>().Body.GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Create modes");
                foreach (var button in row.GetComponentsInChildren<Button>())
                {
                    button.GetComponent<LobbyPress>().Hot(false);
                    var underline = button.GetComponentsInChildren<Image>().Single(image => image.name == "Underline");
                    Assert.AreEqual(button.name == id, underline.enabled, "Only the active Create tool retains its selection marker.");
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();
                        Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "Create mode label fits immediately: " + text.text);
                    }
                }
                Assert.AreEqual(LobbyMenu.Workshop, menu.Queue);
                Assert.AreEqual("courtyard", menu.Map);
                Assert.IsTrue(Find(id == "Create Wordbound" ? "Workshop OPEN" : "GO").interactable);
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Changing a Create tool cannot launch it.");
                CollectionAssert.AreEqual(bindings, Session.Bindings);
            }
        }

        [UnityTest]
        public IEnumerator LocalAndCreateOfferActualModesWithConciseCards()
        {
            Assert.AreEqual("Practice (Offline)", Find("Queue " + LobbyMenu.Practice).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Matchmaking", Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Create", Find("Queue " + LobbyMenu.Workshop).GetComponentInChildren<TMP_Text>().text);
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            foreach (var button in Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode ")))
            {
                Assert.AreEqual(1, button.GetComponentsInChildren<TMP_Text>().Length, "one concise mode label: " + button.name);
                Assert.AreEqual(LobbyPlayModeLabels.Name(button.name.Substring(5)), button.GetComponentInChildren<TMP_Text>().text);
                Assert.IsFalse(button.GetComponentsInChildren<RawImage>().Any(), "Mode choices leave imagery to the map gallery.");
            }
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes);
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
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.IsTrue(Find("GO").interactable);
            Submit("Create house");
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
                WordboundSoloEntry.Clear();
                Assert.AreEqual(WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.SelectedMap);
                foreach (string map in new[] { "flat", "Parcelhall", "", null })
                    Assert.Throws<System.ArgumentException>(() => WordboundSoloEntry.SelectMap(map));
                WordboundSoloEntry.SelectMap(WordboundSoloEntry.ParcelHallMap);
                foreach (string mode in new[] { "TeamRace", "FreeForAll", "Brawl", "Dibs", "", null })
                    Assert.Throws<System.ArgumentException>(() => WordboundSoloEntry.Request(mode));
                foreach (string mode in new[] { "TimeRun", "Freestyle" })
                {
                    WordboundSoloEntry.Request(mode);
                    Assert.AreEqual(mode, WordboundSoloEntry.TakeRequest());
                    Assert.IsNull(WordboundSoloEntry.TakeRequest());
                    Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
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
                Assert.AreEqual(WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.SelectedMap);
            }
            finally { WordboundSoloEntry.Clear(); }
        }

        [UnityTest]
        public IEnumerator ParcelServiceChoiceUsesAnOptionalRealPreviewAndStaysReachableOnShortFrames()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            var old = profile.ParcelServicePreview;
            var texture = new Texture2D(128, 64);
            var preview = Sprite.Create(texture, new Rect(0, 0, 128, 64), new Vector2(.5f, .5f));
            try
            {
                profile.ParcelServicePreview = null;
                var menu = LobbyMenu.Instance;
                menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
                var bindings = Session.Bindings.ToArray();
                Submit("Solo TimeRun"); yield return null;
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(900, 580);
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var footer = InPage((RectTransform)Find("Solo GO").transform);
                var card = Find("Solo map ParcelService");
                Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(), "A missing map preview must not pretend an item is the map.");
                Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(image => image.name == "Map preview"));
                Submit("Solo map ParcelService"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                card = Find("Solo map ParcelService");
                var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
                var shown = InPage((RectTransform)card.transform); var viewport = InPage(scroll.viewport);
                Assert.GreaterOrEqual(shown.yMin, viewport.yMin - .5f);
                Assert.LessOrEqual(shown.yMax, viewport.yMax + .5f);
                AssertSameBounds(footer, InPage((RectTransform)Find("Solo GO").transform), "Third map focus cannot move Play.");
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("Solo map ParcelService", EventSystem.current.currentSelectedGameObject.name);
                profile.ParcelServicePreview = preview; menu.Open(LobbyMenu.Play); yield return null;
                Canvas.ForceUpdateCanvases(); AssertSoloPreview("ParcelService", preview);
                Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
                CollectionAssert.AreEqual(bindings, Session.Bindings);
                WordboundSoloEntry.Clear(); Assert.AreEqual(WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.SelectedMap);
            }
            finally
            {
                profile.ParcelServicePreview = old;
                UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(texture);
                WordboundSoloEntry.Clear();
            }
        }

        [UnityTest]
        public IEnumerator SoloMapPreviewsKeepSelectionAndFallBackIndependently()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            Assert.IsNotNull(profile);
            var savedLost = profile.LostAddressPreview;
            var savedParcel = profile.ParcelHallPreview;
            var texture = new Texture2D(128, 64);
            var lost = Sprite.Create(texture, new Rect(0, 0, 128, 64), new Vector2(.5f, .5f));
            var parcel = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            try
            {
                profile.LostAddressPreview = lost; profile.ParcelHallPreview = parcel;
                Assert.DoesNotThrow(profile.Validate);
                WordboundSoloEntry.SelectMap(WordboundSoloEntry.LostAddressMap);
                var menu = LobbyMenu.Instance;
                menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
                var bindings = Session.Bindings.ToArray();
                Submit("Solo TimeRun"); yield return null;
                foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
                {
                    Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertSoloPreview("LostAddress", lost);
                    AssertSoloPreview("ParcelHall", parcel);
                }
                Submit("Solo map ParcelHall"); yield return null;
                Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("Solo map ParcelHall", EventSystem.current.currentSelectedGameObject.name);
                AssertSoloPreview("ParcelHall", parcel);

                profile.LostAddressPreview = null;
                menu.Open(LobbyMenu.Play); yield return null;
                AssertSoloFallback("LostAddress", "BOX");
                AssertSoloPreview("ParcelHall", parcel);
                profile.ParcelHallPreview = null;
                Assert.DoesNotThrow(profile.Validate);
                menu.Open(LobbyMenu.Play); yield return null;
                AssertSoloFallback("LostAddress", "BOX");
                AssertSoloFallback("ParcelHall", "TABLE");
                Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
                CollectionAssert.AreEqual(bindings, Session.Bindings);
            }
            finally
            {
                profile.LostAddressPreview = savedLost; profile.ParcelHallPreview = savedParcel;
                Object.Destroy(lost); Object.Destroy(parcel); Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator SoloPreviewUsesCardWidthWithReadableLabelsAndFixedFooterOnShortFrames()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            var oldPreview = profile.ParcelServicePreview;
            float oldHeight = profile.MapMinimumHeight;
            var texture = new Texture2D(160, 90);
            var preview = Sprite.Create(texture, new Rect(0, 0, 160, 90), new Vector2(.5f, .5f));
            try
            {
                profile.ParcelServicePreview = preview; profile.MapMinimumHeight = 480;
                var menu = LobbyMenu.Instance;
                foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
                {
                    menu.Choose(LobbyMenu.Practice, "Duos", "flat");
                    menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
                    Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    var house = InPage((RectTransform)Find("Map flat").transform);
                    var footer = InPage((RectTransform)Find("GO").transform);
                    Submit("Solo TimeRun"); yield return null; yield return null;
                    Submit("Solo map ParcelService"); yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertSoloPreview("ParcelService", preview);
                    var card = Find("Solo map ParcelService");
                    var art = card.GetComponentsInChildren<Image>().Single(image => image.name == "Map preview");
                    var imageRect = art.rectTransform.rect;
                    float displayedWidth = Mathf.Min(imageRect.width, imageRect.height * 16f / 9f);
                    Assert.That(displayedWidth, Is.EqualTo(imageRect.width).Within(.5f),
                        "A 16:9 map should fill the padded card width without cropping or distortion.");
                    var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
                    var shown = InPage((RectTransform)card.transform); var viewport = InPage(scroll.viewport);
                    Assert.GreaterOrEqual(shown.yMin, viewport.yMin - .5f);
                    Assert.LessOrEqual(shown.yMax, viewport.yMax + .5f);
                    AssertSameBounds(footer, InPage((RectTransform)Find("Solo GO").transform), "Larger map previews cannot move Play.");
                    Assert.AreSame(card.gameObject, EventSystem.current.currentSelectedGameObject);
                    foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                    { text.ForceMeshUpdate(); Assert.IsFalse(text.isTextOverflowing, text.text); }
                    Submit("Mode Duos"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    AssertSameBounds(house, InPage((RectTransform)Find("Map flat").transform), "House card geometry stays unchanged.");
                }
            }
            finally
            {
                profile.ParcelServicePreview = oldPreview; profile.MapMinimumHeight = oldHeight;
                Object.Destroy(preview); Object.Destroy(texture);
            }
        }

        static void AssertSoloPreview(string id, Sprite expected)
        {
            var card = Find("Solo map " + id);
            var image = card.GetComponentsInChildren<Image>().Single(item => item.name == "Map preview");
            Assert.AreSame(expected, image.sprite);
            Assert.IsTrue(image.preserveAspect); Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(Color.white, image.color);
            Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(item => item.name.StartsWith("Art ")));
            var title = card.GetComponentsInChildren<TMP_Text>().Single(item => item.name == "Map title");
            Assert.GreaterOrEqual(InPage(image.rectTransform).yMin, InPage(title.rectTransform).yMax,
                "Course image stays above the readable title.");
            var selected = card.GetComponentsInChildren<Transform>().SingleOrDefault(item => item.name == "Selected");
            if (selected) Assert.Greater(selected.GetSiblingIndex(), image.transform.GetSiblingIndex(),
                "The image cannot cover the selected mark.");
        }

        static void AssertSoloFallback(string id, string artId)
        {
            var card = Find("Solo map " + id);
            Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(item => item.name == "Map preview"));
            Assert.IsTrue(Wreckabulary.UI.ItemArt.TryGet(artId, out var texture, out var uv));
            var art = card.GetComponentsInChildren<RawImage>().Single(item => item.name == "Art " + artId);
            Assert.AreSame(texture, art.texture); Assert.AreEqual(uv, art.uvRect);
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
            Submit("Solo TimeRun");
            yield return null;
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes, "House choices remain directly reachable from the fixed mode strip.");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            CollectionAssert.AreEqual(new[] { "Solo TimeRun", "Solo Freestyle" },
                Page.GetComponentsInChildren<Button>().Where(button => button.name == "Solo TimeRun" || button.name == "Solo Freestyle")
                    .Select(button => button.name).ToArray());
            CollectionAssert.AreEqual(new[] { "Solo map LostAddress", "Solo map ParcelHall", "Solo map ParcelService" },
                Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Solo map "))
                    .Select(button => button.name).ToArray());
            Submit("Solo map ParcelHall");
            yield return null;
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual("Solo map ParcelHall", EventSystem.current.currentSelectedGameObject.name);
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
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            foreach (var pair in new[] { ("Solo TimeRun", "Time Trial"), ("Solo Freestyle", "Freeplay") })
            {
                Assert.AreEqual(pair.Item2, Find(pair.Item1).GetComponentInChildren<TMP_Text>().text);
                Assert.IsFalse(Find(pair.Item1).GetComponentsInChildren<RawImage>().Any());
            }
            Submit("Mode Duos");
            yield return null;
            CollectionAssert.AreEqual(LobbyMenu.Modes, Modes);
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            menu.Leave(second);
            Submit("Solo TimeRun");
            yield return null;
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
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
