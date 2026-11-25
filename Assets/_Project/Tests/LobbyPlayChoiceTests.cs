using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules.Wordbound;
using HouseModeFamily = Wreckabulary.Rules.HouseModeFamily;
using HouseModes = Wreckabulary.Rules.HouseModes;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPlayChoiceTests
    {
        const string LastKey = Wreckabulary.Rules.LastPlayChoice.StorageKey;
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, QuickPlayChoices.StorageKey, FriendRooms.StorageKey, LastKey };
        static readonly Vector2 Wide = new Vector2(1868, 968), Short = new Vector2(900, 580);
        const float SmallestLabel = 15f, SmallestTarget = 44f;
        const int HousesInARow = 5;
        string[] saved;
        float volume, delay;
        int vsync, tutorial;
        bool hadTutorial, remember;
        string homes, priorHomes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount; delay = LobbyMenu.StartDelay;
            remember = LobbyMenu.RememberChoices;
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey); tutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            priorHomes = HomeStorage.DefaultFolder;
            homes = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wreckabulary-lobby-homes-" + System.Guid.NewGuid().ToString("N"));
            HomeStorage.DefaultFolder = homes;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            yield return OpenLobby();
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static IEnumerator OpenLobby()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "play choices ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, tutorial); else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
            AudioListener.volume = volume; QualitySettings.vSyncCount = vsync; LobbyMenu.StartDelay = delay;
            LobbyMenu.RememberChoices = remember;
            HomeStorage.DefaultFolder = priorHomes;
            if (System.IO.Directory.Exists(homes)) System.IO.Directory.Delete(homes, true);
        }

        static Wreckabulary.Rules.HomeLayout SaveHome(string map, string name)
        {
            var designer = new Wreckabulary.Rules.HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items);
            var house = GameConfig.Current.HouseFor(map);
            var batch = designer.AddWords(designer.CreateLayout(map, name), "TABLE PLANT", house.Rooms[0].Name);
            Assert.IsTrue(batch.Ok, string.Join("\n", batch.Errors));
            Assert.IsTrue(new HomeStorage(designer).TrySave(batch.Layout, out var error), error);
            return batch.Layout;
        }

        static RectTransform Page => LobbyMenu.Instance.Page<PlayPage>().Root;
        static Button Find(string name) => Reach(name) ?? Page.GetComponentsInChildren<Button>().Single(button => button.name == name);

        static bool PressNext()
        {
            var next = Present("GO") ?? Present("Solo GO");
            if (!next || !next.IsInteractable() || !next.GetComponentsInChildren<TMP_Text>().Any(text => text.text.ToUpperInvariant().Contains(PlayPage.NextWord.ToUpperInvariant())))
                return false;
            next.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            return true;
        }

        static Button Present(string name) => Page.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == name);

        static Button Reach(string name)
        {
            var found = Present(name);
            if (found) return found;
            bool mode = name.StartsWith("Mode ") || name.StartsWith("Solo ") && !name.StartsWith("Solo map ") && name != "Solo GO";
            if (mode)
            {
                var back = Present(PlayPage.BackToModes);
                if (back) { back.onClick.Invoke(); Canvas.ForceUpdateCanvases(); found = Present(name); if (found) return found; }
                string id = name.Substring(5);
                if (HouseModes.ModesIn(HouseModes.FamilyOf(id)).Contains(id))
                {
                    var category = Present(PlayPage.CategoryCard + HouseModes.FamilyName(HouseModes.FamilyOf(id)));
                    if (category) { Browse(category); return Present(name); }
                }
                return null;
            }
            return PressNext() ? Present(name) : null;
        }
        static void Browse(Button category)
        {
            var menu = LobbyMenu.Instance; var page = menu.Page<PlayPage>();
            string mode = menu.Mode, course = WordboundSoloEntry.SelectedMode, courseMap = WordboundSoloEntry.SelectedMap;
            bool solo = page.WordboundSolo, had = PlayerPrefs.HasKey(LastKey);
            string remembered = PlayerPrefs.GetString(LastKey, "");
            category.onClick.Invoke();
            menu.Choose(mode: mode);
            WordboundSoloEntry.Select(course); WordboundSoloEntry.SelectMap(courseMap);
            page.WordboundSolo = solo;
            if (had) PlayerPrefs.SetString(LastKey, remembered); else PlayerPrefs.DeleteKey(LastKey);
            string name = category ? category.name : null;
            var family = HouseModes.Families.FirstOrDefault(item => PlayPage.CategoryCard + HouseModes.FamilyName(item) == name);
            if (name != null && !HouseModes.ModesIn(family).Any(item => Present(PlayPage.ModeButton(item)))) page.Refresh();
            Canvas.ForceUpdateCanvases();
        }

        static RectTransform CategoryRow => Page.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == PlayPage.CategoryRow);

        static Button[] CategoryCards => CategoryRow ? CategoryRow.Cast<Transform>().Select(child => child.GetComponent<Button>()).Where(button => button).ToArray() : new Button[0];

        static string Detail => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail").text;

        static string[] Modes
        {
            get
            {
                if (!CategoryRow) return Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode ")).Select(button => button.name.Substring(5)).ToArray();
                var page = LobbyMenu.Instance.Page<PlayPage>();
                string focus = EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : null;
                var found = new System.Collections.Generic.List<string>();
                foreach (string name in CategoryCards.Select(card => card.name).ToArray())
                {
                    var family = HouseModes.Families.Single(item => PlayPage.CategoryCard + HouseModes.FamilyName(item) == name);
                    var card = Present(name);
                    if (!HouseModes.ModesIn(family).Any(mode => Present(PlayPage.ModeButton(mode)))) Browse(card);
                    found.AddRange(ModeTargets.Where(button => button.name.StartsWith("Mode ")).Select(button => button.name.Substring(5))
                        .Where(mode => HouseModes.ModesIn(family).Contains(mode)));
                }
                page.BrowsedFamily = null;
                page.Refresh();
                Canvas.ForceUpdateCanvases();
                var back = focus == null ? null : Page.GetComponentsInChildren<Selectable>().FirstOrDefault(item => item.name == focus);
                if (back) EventSystem.current.SetSelectedGameObject(back.gameObject);
                return found.Distinct().ToArray();
            }
        }

        static Button[] HouseStep
        {
            get
            {
                PressNext();
                return Page.GetComponentsInChildren<Button>();
            }
        }

        static string Summary => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Matchmaking playlists").text;
        const string BrokenRoom = "wb-room:1234567890abcdef:NoSuchMode:Solo:asia:p1:0123456789abcdef:m1";
        static string FriendsRoom => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Friends room").text;
        static string FriendsDetail => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Friends detail").text;
        static TMP_InputField CodeField => Page.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == PlayPage.CodeField);
        static string GoalText => Strip.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine).text;
        static bool AnyHouseLit => Find(PlayPage.AnyHouse).GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("Icon ") && image.color == LobbyKit.Selection);
        static string Named(TMP_Text[] texts, string name) => texts.Single(text => text.name == name).text;
        static string[] Named(string prefix) => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(prefix))
            .Select(button => button.name.Substring(prefix.Length)).ToArray();
        static bool Ticked(string id) => Find(PlayPage.PlaylistCard + id).GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Selected");

        static void Submit(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.IsInteractable(), name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        static void Pick(string mode) => Submit(PlayPage.ModeButton(mode));

        static string[] AllHouseModes => HouseModes.Families.SelectMany(HouseModes.ModesIn).Where(HouseModes.Has).ToArray();

        static string[] AllModeButtons => HouseModes.Families.SelectMany(HouseModes.ModesIn).Select(PlayPage.ModeButton).ToArray();

        static string[] Sections => CategoryCards.Select(card => card.name.Substring(PlayPage.CategoryCard.Length)).ToArray();

        static Transform Section(HouseModeFamily family)
        {
            if (!HouseModes.ModesIn(family).Any(mode => Present(PlayPage.ModeButton(mode))))
                Browse(Present(PlayPage.CategoryCard + HouseModes.FamilyName(family)));
            return Columns;
        }

        static string[] Captions => CategoryRow.GetComponentsInChildren<TMP_Text>().Where(text => text.name == PlayPage.CategoryName)
            .Select(text => text.text).ToArray();

        static bool Underlined(string name) => Find(name).GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled;

        static string CardDetail(string id) => Find(PlayPage.PlaylistCard + id).GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").text;

        static bool Marked(string name) => Find(name).GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Selected");

        [UnityTest]
        public IEnumerator ModeCategoriesLeadTheChoicesAndEveryModeIsAtMostTwoPressesAway()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            foreach (var size in new[] { Wide, Short })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                Pick("TimeRun"); yield return null; yield return null;
                PageScroll.verticalNormalizedPosition = 1f;
                Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var row = CategoryRow;
                var queueBounds = InPage(frame.Queues); var rowBounds = InPage(row);
                var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
                var view = InPage(scroll.viewport);
                Assert.That(queueBounds.center.x, Is.EqualTo(InPage(frame.Panel).center.x).Within(.5f));
                Assert.AreSame(scroll.content, row.parent, "the categories are the first row of the page's own scroll");
                Assert.AreEqual(0, row.GetSiblingIndex());
                Assert.AreEqual(1, Strip.GetSiblingIndex(), "the open category's modes come next");
                Assert.LessOrEqual(rowBounds.yMax, queueBounds.yMin);
                Assert.That(rowBounds.yMax, Is.EqualTo(view.yMax).Within(.5f), "with the page at its top the categories start the choices");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Family ")));
                CollectionAssert.AreEqual(PlayPage.FamilyOrder.Select(family => PlayPage.CategoryCard + HouseModes.FamilyName(family)).ToArray(),
                    CategoryCards.Select(card => card.name).ToArray(), "one card per category");
                foreach (var family in PlayPage.FamilyOrder)
                    foreach (string id in HouseModes.ModesIn(family).Select(PlayPage.ModeButton))
                    {
                        var button = Find(id); var bounds = InPage((RectTransform)button.transform);
                        Assert.IsTrue(button.IsInteractable(), id); Assert.IsTrue(button.transform.IsChildOf(Columns), id + " is one of the open category's tiles");
                        var step = InPage(Strip);
                        Assert.GreaterOrEqual(bounds.yMin, step.yMin - .5f, id); Assert.LessOrEqual(bounds.yMax, step.yMax + .5f, id);
                    }
                Pick("TimeRun"); yield return null;
                if (size == Wide) AssertNoScrollingNeeded();
                Submit("Solo map ParcelService"); yield return null; yield return null;
                frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                AssertSameBounds(queueBounds, InPage(frame.Queues), "The category selector remains fixed.");
                Pick("Freestyle"); yield return null;
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap);
                Pick("Duos"); yield return null;
                Assert.AreEqual("flat", menu.Map); Assert.AreEqual("Duos", menu.Mode, "a category and a tile reach every mode");
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
                Reach("Map pinwheel"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var reference = InPage((RectTransform)Find("Map pinwheel").transform);
                float practiceHeader = InPage(Strip).height;
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var header = InPage(frame.Queues);
                var footer = InPage((RectTransform)Find("GO").transform);

                Pick("Escape"); yield return null;
                Reach("Map pinwheel"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var escape = Find("Map pinwheel");
                Assert.AreEqual(GameConfig.Current.Houses.Count, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")),
                    "Escape the House offers the same tickable houses as every other mode.");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"), "there is no separate Random card");
                AssertCardFootprint(reference, (RectTransform)escape.transform, Mathf.Abs(InPage(Strip).height - practiceHeader) < .5f);
                EventSystem.current.SetSelectedGameObject(escape.gameObject);
                yield return null; yield return null;
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Play stays fixed.");
                Assert.AreSame(escape.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.AreEqual("pinwheel", menu.Map);

                Submit("Queue " + LobbyMenu.Workshop); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                AssertToolCards(size.y >= 968);
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Creator header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Open uses the fixed action slot.");
                Assert.AreEqual(Wreckabulary.Rules.PlayPageText.OpenWord.ToUpperInvariant(), Find("GO").GetComponentInChildren<TMP_Text>().text.ToUpperInvariant());
                menu.Page<PlayPage>().WordboundMaps = true;
                menu.Page<PlayPage>().Refresh(); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                AssertToolCards(size.y >= 968);
                AssertSameBounds(footer, InPage((RectTransform)Find("Workshop OPEN").transform), "Open uses the fixed action slot.");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Showing the Create tools must not launch one.");
                menu.Page<PlayPage>().WordboundMaps = false;
            }
        }

        static Button[] ToolCards => Page.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.CreateRow)
            .Cast<Transform>().Select(child => child.GetComponent<Button>()).Where(button => button).ToArray();

        static void AssertToolCards(bool roomy)
        {
            var cards = ToolCards;
            CollectionAssert.AreEqual(new[] { PlayPage.CreateHouse, PlayPage.CreateEditor }, cards.Select(card => card.name).ToArray(), "two tools, the house first");
            var view = InPage(PageScroll.viewport);
            var first = InPage((RectTransform)cards[0].transform);
            var second = InPage((RectTransform)cards[1].transform);
            Assert.That(second.width, Is.EqualTo(first.width).Within(.5f), "the two tools share one size");
            Assert.That(second.height, Is.EqualTo(first.height).Within(.5f), "the two tools share one size");
            Assert.That(second.yMax, Is.EqualTo(first.yMax).Within(.5f), "side by side");
            Assert.Greater(second.xMin, first.xMax, "the map editor sits right of the house builder");
            Assert.That((first.xMin + second.xMax) * .5f, Is.EqualTo(InPage((RectTransform)cards[0].transform.parent).center.x).Within(1f), "the pair is centred");
            Assert.LessOrEqual(first.width, LobbyPlayToolRow.MaximumWidth + .5f, "a tool card stops at its widest");
            foreach (var card in cards)
            {
                var bounds = InPage((RectTransform)card.transform);
                Assert.GreaterOrEqual(bounds.height, SmallestTarget, card.name);
                Assert.GreaterOrEqual(bounds.xMin, view.xMin - .5f, card.name); Assert.LessOrEqual(bounds.xMax, view.xMax + .5f, card.name);
                if (roomy) { Assert.GreaterOrEqual(bounds.yMin, view.yMin - .5f, card.name); Assert.LessOrEqual(bounds.yMax, view.yMax + .5f, card.name); }
                var texts = card.GetComponentsInChildren<TMP_Text>();
                CollectionAssert.AreEquivalent(new[] { PlayPage.ToolTitle, PlayPage.ToolLine }, texts.Select(text => text.name).ToArray(), card.name + ": a name and one line");
                foreach (var text in texts)
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, card.name + ": " + text.text);
                    Assert.GreaterOrEqual(text.fontSize, SmallestLabel - .01f, card.name + ": " + text.text);
                }
                Assert.IsTrue(card.GetComponentsInChildren<RectTransform>().Any(part => part.name == "Map preview" || part.name.StartsWith("Art ")), card.name + " shows a picture");
            }
            Assert.AreEqual(LobbyMenu.ModeName(LobbyMenu.WorkshopMode), cards[0].GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.ToolTitle).text);
            Assert.AreEqual(PlayPage.MapEditorTitle, cards[1].GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.ToolTitle).text);
            Assert.IsTrue(cards[0].interactable);
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), cards[1].interactable);
        }

        static void AssertCardFootprint(Rect reference, RectTransform card, bool sameHeader)
        {
            var actual = InPage(card);
            Assert.That(actual.width, Is.EqualTo(reference.width).Within(.1f), "One card keeps the multi-map card width.");
            if (sameHeader) Assert.That(actual.height, Is.EqualTo(reference.height).Within(.1f), "One card keeps the multi-map card height.");
            else Assert.LessOrEqual(actual.height, LobbyPlayCardGrid.HeightFor(actual.width) + .1f, "A card never grows past its 16:9 picture and caption.");
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
            Reach("Map flat");
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
            Assert.GreaterOrEqual(((RectTransform)Find(PlayPage.BackToModes).transform).rect.height, 44,
                "The way back to the modes keeps an accessible target.");
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
            EventSystem.current.SetSelectedGameObject(Find(PlayPage.BackToModes).gameObject);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(scroll.content.rect.height > scroll.viewport.rect.height + .1f,
                scroll.verticalScrollbar.gameObject.activeSelf, "Only overflowing map galleries expose a scrollbar.");
            Assert.AreEqual(PlayPage.BackToModes, EventSystem.current.currentSelectedGameObject.name);
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
            float grid = Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses").cellSize.y;
            yield return null;
            yield return null;
            Assert.That(Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses").cellSize.y, Is.EqualTo(grid).Within(.01f),
                "An unchanged page keeps one card size from frame to frame.");
            if (scroll.content.rect.height > scroll.viewport.rect.height + .1f)
            {
                scroll.verticalNormalizedPosition = 1f;
                yield return null;
                yield return null;
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f),
                    "Manual scrolling must not jump back to an unchanged selection.");
            }
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
        }

        [UnityTest]
        public IEnumerator WordboundFamiliesFillThePageWithoutChangingChoicesOrFooter()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Solo TimeRun");
            yield return null;
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Solo GO").interactable);
            var focus = EventSystem.current.currentSelectedGameObject;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 920), new Vector2(900, 580), new Vector2(1868, 968) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero;
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, "Modes", "Solo summary");
                Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "resizing must not recreate focus");
            }
            Assert.AreEqual("ShoppingList", menu.Mode, "a course beside a house mode keeps the house choice"); Assert.AreEqual("flat", menu.Map);
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(menu.Page<PlayPage>().WordboundSolo, "a second seat leaves the solo-only courses");
            foreach (var size in new[] { Wide, Short })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullHousePanel();
                AssertStepFits();
            }
            CollectionAssert.AreEqual(AllHouseModes, Modes);
            AssertClosedCourses();
            Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, PlayPage.CreateRow, "Summary");
                Assert.IsTrue(Find("GO").interactable);
            }
            menu.Page<PlayPage>().WordboundMaps = true;
            menu.Page<PlayPage>().Refresh();
            yield return null;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, PlayPage.CreateRow, "Workshop summary");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
            }
            CollectionAssert.IsEmpty(Modes, "Create offers its two tools without play modes");
            Assert.IsFalse(menu.Starting, "showing the tools launches nothing");
            menu.Page<PlayPage>().WordboundMaps = false;
            menu.Leave(second);
        }

        static void AssertClosedCourses()
        {
            foreach (var (mode, family) in new[] { ("TimeRun", HouseModeFamily.Arcade), ("Freestyle", HouseModeFamily.Training) })
            {
                var row = Find(PlayPage.ModeButton(mode));
                Assert.IsTrue(row.transform.IsChildOf(Section(family)), mode + " keeps its category");
                Assert.AreEqual(HouseModes.ModesIn(family).ToList().IndexOf(mode), row.transform.GetSiblingIndex(), mode + " keeps its place");
                Assert.IsTrue(row.IsInteractable(), mode + " stays focusable so it can say why");
                Assert.That(row.GetComponent<CanvasGroup>().alpha, Is.EqualTo(.5f).Within(.01f), mode + " is dimmed");
                var tag = row.GetComponentsInChildren<TMP_Text>().Single(text => text.transform.parent.name == PlayPage.SeatTag);
                Assert.AreEqual(LobbyKit.Upper(Wreckabulary.Rules.ModeAccess.OnePlayer), tag.text, mode + " says it is for one player");
                Assert.GreaterOrEqual(tag.fontSize, SmallestLabel - .01f);
                Assert.AreEqual(PlayPage.SoloOnly(mode), row.GetComponent<LobbyHint>().Text);
                var swap = row.GetComponent<LobbyPlayGoalSwap>();
                Assert.IsNotNull(swap, mode + " explains itself on focus");
                Assert.AreEqual(PlayPage.SoloOnly(mode), swap.Reason);
            }
            LobbyMenu.Instance.Page<PlayPage>().BrowsedFamily = null;
            LobbyMenu.Instance.Page<PlayPage>().Refresh();
        }

        [UnityTest]
        public IEnumerator EveryPlayFamilyKeepsTheFullSurfaceAndSavedProfileAcrossResize()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            string savedProfile = JsonUtility.ToJson(profile);
            using var viewport = new StageViewport(menu);
            foreach (string family in new[] { "Solo TimeRun", "Mode Duos", PlayPage.CreateEditor, PlayPage.CreateHouse })
            {
                if (family == PlayPage.CreateEditor) { Submit("Queue " + LobbyMenu.Workshop); yield return null; }
                if (family.StartsWith("Create ")) menu.Page<PlayPage>().WordboundMaps = family == PlayPage.CreateEditor;
                if (family.StartsWith("Create ")) menu.Page<PlayPage>().Refresh(); else Pick(family.Substring(5));
                if (family.StartsWith("Create ")) EventSystem.current.SetSelectedGameObject(menu.Page<PlayPage>().First.gameObject);
                yield return null;
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
                if (family.StartsWith("Create "))
                {
                    Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
                    Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The home builder must normalize an unsupported house choice.");
                }
                else Assert.AreEqual("flat", menu.Map, "Practice choices preserve the original house selection.");
            }
            Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map));
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            Assert.AreEqual("flat", menu.Map, "Leaving Create gives the practice house back.");
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
            CollectionAssert.AreEqual(HouseModes.Families.Select(HouseModes.FamilyName), Sections);
            CollectionAssert.AreEqual(AllHouseModes, Modes);
            yield return null;
        }

        static RectTransform Strip
        {
            get
            {
                var rects = Page.GetComponentInChildren<LobbyPlayPageView>().Body.GetComponentsInChildren<RectTransform>();
                return rects.FirstOrDefault(rect => rect.name == PlayPage.ModeBoard) ?? rects.Single(rect => rect.name == PlayPage.HouseStepRow);
            }
        }

        static RectTransform Columns => (RectTransform)Strip.Find(PlayPage.ModeTiles);

        static Button[] ModeTargets => Columns.GetComponentsInChildren<Button>();

        static RectTransform ModePanel => (RectTransform)Strip.Find(PlayPage.ModeDetail);

        static ScrollRect PageScroll => Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();

        static void AssertStepFits()
        {
            var strip = Strip;
            var bounds = InPage(strip);
            var panel = InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Panel);
            Assert.GreaterOrEqual(bounds.xMin, panel.xMin - .5f); Assert.LessOrEqual(bounds.xMax, panel.xMax + .5f);
            Assert.IsEmpty(strip.GetComponentsInChildren<ScrollRect>(true), "the modes have no band of their own to scroll");
            Assert.IsEmpty(strip.GetComponentsInChildren<Scrollbar>(true), "and no sideways scrollbar");
            var scroll = strip.GetComponentInParent<ScrollRect>();
            Assert.AreSame(PageScroll, scroll, "the modes ride the page's own scroll");
            Assert.IsTrue(scroll.vertical, "which moves up and down"); Assert.IsFalse(scroll.horizontal, "and never sideways");
            var row = InPage(CategoryRow);
            Assert.GreaterOrEqual(row.yMin, bounds.yMax - .5f, "the categories sit above the mode step");
            foreach (var card in CategoryCards)
            {
                var at = InPage((RectTransform)card.transform);
                Assert.GreaterOrEqual(at.height, SmallestTarget - .01f, card.name);
                Assert.GreaterOrEqual(at.xMin, row.xMin - .5f, card.name); Assert.LessOrEqual(at.xMax, row.xMax + .5f, card.name);
            }
            var tiles = InPage(Columns); var detail = InPage(ModePanel);
            Assert.LessOrEqual(tiles.xMax, detail.xMin + .5f, "the detail panel sits right of the tiles");
            foreach (var area in new[] { tiles, detail })
            {
                Assert.GreaterOrEqual(area.yMin, bounds.yMin - .5f); Assert.LessOrEqual(area.yMax, bounds.yMax + .5f);
            }
            foreach (var button in ModeTargets)
            {
                var at = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(at.xMin, tiles.xMin - .5f, button.name); Assert.LessOrEqual(at.xMax, tiles.xMax + .5f, button.name);
                Assert.GreaterOrEqual(at.yMin, tiles.yMin - .5f, button.name); Assert.LessOrEqual(at.yMax, tiles.yMax + .5f, button.name);
                Assert.GreaterOrEqual(at.height, SmallestTarget - .01f, button.name); Assert.GreaterOrEqual(at.width, SmallestTarget - .01f, button.name);
                var label = button.GetComponentsInChildren<TMP_Text>().First();
                label.ForceMeshUpdate();
                Assert.GreaterOrEqual(label.fontSize, SmallestLabel - .01f, button.name + " keeps a readable label");
            }
            foreach (var text in strip.GetComponentsInChildren<TMP_Text>().Concat(CategoryRow.GetComponentsInChildren<TMP_Text>()))
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }



        static void AssertNoScrollingNeeded()
        {
            var scroll = PageScroll;
            var view = InPage(scroll.viewport);
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(.5f), "the page is at its top");
            foreach (var button in CategoryCards.Concat(ModeTargets))
            {
                var at = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(at.xMin, view.xMin - .5f, button.name); Assert.LessOrEqual(at.xMax, view.xMax + .5f, button.name);
                Assert.GreaterOrEqual(at.yMin, view.yMin - .5f, button.name + " shows without scrolling");
                Assert.LessOrEqual(at.yMax, view.yMax + .5f, button.name + " shows without scrolling");
            }
        }

        static IEnumerator RevealEveryMode()
        {
            foreach (var button in ModeTargets)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var view = InPage(PageScroll.viewport); var at = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(at.yMin, view.yMin - .5f, button.name + " is in view when focused");
                Assert.LessOrEqual(at.yMax, view.yMax + .5f, button.name + " is in view when focused");
                Assert.GreaterOrEqual(at.xMin, view.xMin - .5f, button.name); Assert.LessOrEqual(at.xMax, view.xMax + .5f, button.name);
            }
        }

        static void AssertTileNavigation()
        {
            var tiles = ModeTargets;
            if (tiles.Length == 0) return;
            float top = InPage((RectTransform)tiles[0].transform).yMax;
            int across = tiles.Count(tile => Mathf.Abs(InPage((RectTransform)tile.transform).yMax - top) < .5f);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (i % across != across - 1 && i + 1 < tiles.Length)
                {
                    Assert.AreSame(tiles[i + 1], tiles[i].FindSelectableOnRight(), "right from " + tiles[i].name);
                    Assert.AreSame(tiles[i], tiles[i + 1].FindSelectableOnLeft(), "left from " + tiles[i + 1].name);
                }
                if (i + across < tiles.Length)
                {
                    Assert.AreSame(tiles[i + across], tiles[i].FindSelectableOnDown(), "down from " + tiles[i].name);
                    Assert.AreSame(tiles[i], tiles[i + across].FindSelectableOnUp(), "up from " + tiles[i + across].name);
                }
            }
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
            LobbyMenu.StartDelay = 30f;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            Assert.AreEqual("courtyard", menu.Map, "Start with a supported, non-default home-builder map.");
            var second = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(second);
            var bindings = Session.Bindings.ToArray();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertToolCards(true);
            CollectionAssert.IsEmpty(Modes);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")), "the builder picks its house inside the tool");
            Assert.IsTrue(Find("GO").interactable);
            Assert.IsFalse(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.text.ToLowerInvariant().Contains("local")),
                "Create never calls the editor local");
            menu.Page<PlayPage>().WordboundMaps = true;
            menu.Page<PlayPage>().Refresh(); yield return null;
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
            Assert.AreEqual(PlayPage.MapEditorNote, Page.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Workshop detail").text);
            Assert.AreEqual(PlayPage.MapEditorTitle, Page.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Workshop title").text, "the footer names the tool used last");
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue); Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual(2, menu.PartySize);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            Submit(PlayPage.CreateHouse); yield return null;
            Assert.IsTrue(menu.Starting, "one click on the house card opens the Home builder");
            Assert.IsFalse(menu.Page<PlayPage>().WordboundMaps, "and the footer remembers the house tool");
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode); Assert.AreEqual("courtyard", menu.Map);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            menu.CancelStart();
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            menu.Leave(second);
        }

        [UnityTest]
        public IEnumerator CreateToolsKeepOneSelectedModeAfterEditorSwitches()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            var bindings = Session.Bindings.ToArray();
            foreach (bool editor in new[] { true, false, true, false })
            {
                menu.Page<PlayPage>().WordboundMaps = editor;
                menu.Page<PlayPage>().Refresh(); yield return null;
                Canvas.ForceUpdateCanvases();
                var cards = ToolCards;
                Assert.AreEqual(2, cards.Length);
                foreach (var button in cards)
                {
                    button.GetComponent<LobbyPress>().Hot?.Invoke(false);
                    Assert.IsFalse(button.GetComponentsInChildren<RectTransform>().Any(part => part.name == "Selected"), "a tool card is an action, never a ticked choice");
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();
                        Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "Create tool label fits immediately: " + text.text);
                    }
                }
                string focus = editor && Session.CanLoad(Session.WordboundScene) ? PlayPage.CreateEditor : PlayPage.CreateHouse;
                Assert.AreEqual(focus, menu.Page<PlayPage>().First.name, "focus starts on the tool used last");
                Assert.AreEqual(LobbyMenu.Workshop, menu.Queue);
                Assert.AreEqual("courtyard", menu.Map);
                Assert.IsTrue(Find(editor ? "Workshop OPEN" : "GO").interactable || !Session.CanLoad(Session.WordboundScene));
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Showing a Create tool cannot launch it.");
                CollectionAssert.AreEqual(bindings, Session.Bindings);
            }
            menu.Page<PlayPage>().WordboundMaps = false;
        }

        [UnityTest]
        public IEnumerator LocalAndCreateOfferActualModesWithConciseCards()
        {
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual("Same screen", Find("Queue " + LobbyMenu.Practice).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Online", Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Create", Find("Queue " + LobbyMenu.Workshop).GetComponentInChildren<TMP_Text>().text);
            foreach (var text in Page.GetComponentsInChildren<TMP_Text>(true))
                Assert.IsFalse(text.text.ToLowerInvariant().Contains("offline"), text.name + ": " + text.text);
            CollectionAssert.AreEqual(AllHouseModes, Modes);
            CollectionAssert.AreEqual(HouseModes.Families.Select(HouseModes.FamilyName), Captions);
            foreach (var family in HouseModes.Families)
            {
                foreach (string mode in HouseModes.ModesIn(family))
                {
                    var button = Find(PlayPage.ModeButton(mode));
                    Assert.AreEqual(1, button.GetComponentsInChildren<TMP_Text>().Length, "one concise mode label: " + button.name);
                    Assert.AreEqual(LobbyPlayModeLabels.Name(mode), button.GetComponentInChildren<TMP_Text>().text);
                    CollectionAssert.AreEqual(new[] { PlayPage.ModeEmblem }, button.GetComponentsInChildren<RawImage>().Select(image => image.name).ToArray(),
                        "each mode name carries one small emblem: " + button.name);
                    var emblem = InPage(button.GetComponentsInChildren<RawImage>().Single().rectTransform);
                    var tile = InPage((RectTransform)button.transform);
                    Assert.Greater(emblem.center.x, tile.center.x, button.name + ": the emblem sits in the tile's right half");
                    Assert.Greater(emblem.center.y, tile.center.y, button.name + ": above the name");
                    Assert.LessOrEqual(emblem.xMax, tile.xMax + .5f, button.name); Assert.LessOrEqual(emblem.yMax, tile.yMax + .5f, button.name);
                }
            }
            Pick("Dibs");
            yield return null;
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            CollectionAssert.IsEmpty(Modes);
            Assert.AreEqual(LobbyMenu.Workshop, LobbyMenu.Instance.Queue);
            Assert.AreEqual(LobbyMenu.WorkshopMode, LobbyMenu.Instance.Mode);
            CollectionAssert.AreEqual(new[] { "Create house", "Create Wordbound" }, Page.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Create ")).Select(button => button.name).ToArray());
            Assert.IsFalse(Page.GetComponentsInChildren<Transform>().Any(child => child.name == "Tutorial room"), "the tutorial lives in Practice");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            CollectionAssert.AreEqual(AllHouseModes, Modes);
            Assert.AreEqual("Dibs", LobbyMenu.Instance.Mode);
            Assert.AreSame(Find("Queue " + LobbyMenu.Practice).gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator SummaryUsesActualBotPolicyAndUpdatesWhenAPlayerJoins()
        {
            var menu = LobbyMenu.Instance;
            foreach (string mode in LobbyMenu.Modes)
            {
                Pick(mode);
                yield return null;
                Assert.AreEqual(Wreckabulary.Rules.HouseModes.Find(mode).BotSeats > 0, Detail.ToLowerInvariant().Contains("bot"), mode);
                Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_Text>().Count(text => text.name == "Mode detail"));
                Canvas.ForceUpdateCanvases();
                var detail = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail");
                detail.ForceMeshUpdate();
                Assert.IsFalse(detail.isTextOverflowing, "selected objective/participants fit: " + mode);
                Assert.IsTrue(Find("GO").interactable);
            }
            Pick("Duos");
            yield return null;
            menu.Join(new ScriptedBinding());
            yield return null;
            StringAssert.Contains("2 players, each with a bot", Detail);
            Assert.AreSame(Find("Mode Duos").gameObject, EventSystem.current.currentSelectedGameObject,
                "party refresh preserves selected mode focus");
            Pick("Escape");
            yield return null;
            StringAssert.Contains("2 players", Detail);
            Assert.IsFalse(Detail.ToLowerInvariant().Contains("bot"));
            Assert.AreEqual(GameConfig.Current.Houses.Count, HouseStep.Count(button => button.name.StartsWith("Map ")),
                "Escape the House picks from the ticked houses like every mode");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"));
            Canvas.ForceUpdateCanvases();
            string footer = LobbyMenu.ModeName("Escape") + Wreckabulary.Rules.MapCardText.Separator + Wreckabulary.Rules.PracticeMaps.Every;
            Assert.IsTrue(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.text == footer), "every house is ticked on a first visit: " + footer);
            Assert.IsFalse(menu.Messages.Any(message => message.StartsWith("Map: ")),
                "map state is shown in the ticked cards and footer, without stale map notifications");
        }

        [UnityTest]
        public IEnumerator TutorialLeadsTrainingAndPlaysInOneStep()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, LobbyMenu.TutorialMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.AreEqual(PlayPage.ModeButton(LobbyMenu.TutorialMode), Section(HouseModeFamily.Training).GetComponentsInChildren<Button>().First().name,
                "the tutorial leads the Training tiles");
            Assert.IsTrue(Underlined(PlayPage.ModeButton(LobbyMenu.TutorialMode)));
            CollectionAssert.AreEqual(new[] { "Solo Freestyle" }, ModeTargets.Where(b => b.name.StartsWith("Solo ")).Select(b => b.name).ToArray(),
                "Free Roam shares the Training tiles");
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.AreSame(Find("Mode " + LobbyMenu.TutorialMode).gameObject, EventSystem.current.currentSelectedGameObject);
            var go = Find("GO");
            Assert.IsTrue(go.interactable);
            StringAssert.DoesNotContain(PlayPage.NextWord.ToUpperInvariant(), go.GetComponentInChildren<TMP_Text>().text.ToUpperInvariant(),
                "the tutorial has its own room, so Play starts it with no house step");
            Assert.IsFalse(HouseStep.Any(button => button.name.StartsWith("Map ")), "there are no houses to pick");
            Assert.IsFalse(Page.GetComponentsInChildren<Transform>().Any(child => child.name == PlayPage.TutorialRoom), "and no stand-in room card");
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            CollectionAssert.IsEmpty(Modes);
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual("Dibs", menu.Mode, "a finished tutorial hands Practice to Dibs");
        }

        [UnityTest]
        public IEnumerator TutorialIsSelectedUntilItIsFinished()
        {
            yield return TestScenes.Reset();
            PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode, "a first visit starts on the tutorial");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.AreSame(Find("Mode " + LobbyMenu.TutorialMode).gameObject, EventSystem.current.currentSelectedGameObject);
            CollectionAssert.AreEqual(Wreckabulary.Rules.PlayPageText.Families(false).Select(HouseModes.FamilyName).ToArray(), Sections,
                "the category with the tutorial comes first until it is done");
            Assert.AreEqual(HouseModes.FamilyName(HouseModeFamily.Training), Sections[0]);
            var start = Find("Mode " + LobbyMenu.TutorialMode).GetComponentsInChildren<TMP_Text>().Single(text => text.transform.parent.name == PlayPage.StartTag);
            Assert.AreEqual(LobbyKit.Upper(Wreckabulary.Rules.PlayPageText.StartHere), start.text);
            Assert.GreaterOrEqual(start.fontSize, SmallestLabel - .01f);
            Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_Text>().Count(text => text.transform.parent.name == PlayPage.StartTag), "one start tag");
            Assert.IsFalse(HouseStep.Any(button => button.name.StartsWith("Map ")), "the tutorial plays in its own room");
            Submit("Queue " + LobbyMenu.Workshop); yield return null;
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode, "Practice keeps offering the tutorial first");
            Assert.AreEqual(LobbyMenu.TutorialMode, Session.LobbyMode);
            TutorialDirector.MarkDone();
            yield return OpenLobby();
            Assert.AreEqual("Dibs", LobbyMenu.Instance.Mode, "finishing the tutorial moves the default to Dibs");
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            CollectionAssert.AreEqual(HouseModes.Families.Select(HouseModes.FamilyName).ToArray(), Sections, "then the categories return to their usual order");
            Assert.IsFalse(Page.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == PlayPage.StartTag), "and the start tag is gone");
        }

        [UnityTest]
        public IEnumerator PracticeModesSitInFourCategoriesAndSoloCoursesDimWhileExtraSeatsAreTaken()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel"); menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = Wide;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            CollectionAssert.AreEqual(HouseModes.Families.Select(HouseModes.FamilyName).ToArray(), Sections, "Classic, Co-op, Arcade, then Training");
            CollectionAssert.AreEqual(new[] { "Classic", "Co-op", "Arcade", "Training" }, Captions);
            var counts = new System.Collections.Generic.List<int>();
            foreach (var family in HouseModes.Families)
            {
                var tiles = Section(family).GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
                CollectionAssert.AreEqual(HouseModes.ModesIn(family).Select(PlayPage.ModeButton).ToArray(), tiles, family + " in order");
                counts.Add(tiles.Length);
            }
            CollectionAssert.AreEqual(new[] { 4, 3, 4, 2 }, counts, "four, three, four and two modes");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Group ")), "no group tabs");
            Pick("Dibs"); yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertStepFits();
            AssertNoScrollingNeeded();
            AssertTileNavigation();
            Page.sizeDelta = Short;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertStepFits();
            yield return RevealEveryMode();
            Page.sizeDelta = Wide;
            yield return null; yield return null;
            Pick("MovingOut"); yield return null;
            Assert.AreEqual("MovingOut", menu.Mode, "every mode is a category and a tile away");
            Assert.IsTrue(Underlined("Mode MovingOut"));
            Assert.IsFalse(ModeTargets.Where(button => button.name != "Mode MovingOut").Any(button => Underlined(button.name)), "only the chosen tile is underlined");
            Canvas.ForceUpdateCanvases();
            var alone = ModeTargets.ToDictionary(button => button.name, button => InPage((RectTransform)button.transform));
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertClosedCourses();
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var button in ModeTargets)
            {
                var now = InPage((RectTransform)button.transform);
                Assert.That(now.xMin, Is.EqualTo(alone[button.name].xMin).Within(.5f), button.name + " stays where it was when a seat joins");
                Assert.That(now.yMax, Is.EqualTo(alone[button.name].yMax).Within(.5f), button.name + " stays where it was when a seat joins");
                Assert.That(now.width, Is.EqualTo(alone[button.name].width).Within(.5f), button.name + " keeps its width when a seat joins");
            }
            CollectionAssert.AreEqual(AllHouseModes, Modes, "every house party mode stays");
            Assert.IsTrue(HouseModes.Has(menu.Mode) && !menu.Page<PlayPage>().WordboundSolo);
            Canvas.ForceUpdateCanvases();
            AssertStepFits();
            AssertNoScrollingNeeded();
            AssertTileNavigation();
            string goal = GoalText;
            var closed = Find("Solo TimeRun");
            EventSystem.current.SetSelectedGameObject(closed.gameObject);
            yield return null;
            Assert.AreEqual(PlayPage.SoloOnly("TimeRun"), GoalText, "focus on a closed course says why");
            Assert.IsTrue(closed.GetComponent<LobbyPlayGoalSwap>().Showing);
            EventSystem.current.SetSelectedGameObject(Find("Mode MovingOut").gameObject);
            yield return null;
            Assert.AreEqual(goal, GoalText, "and the goal line comes back when focus moves on");
            Submit("Solo Freestyle");
            yield return null;
            CollectionAssert.Contains(menu.Messages, PlayPage.SoloOnly("Freestyle"));
            Assert.AreEqual("MovingOut", menu.Mode, "a closed course never changes the choice");
            Assert.IsFalse(menu.Page<PlayPage>().WordboundSolo);
            menu.Leave(second);
            yield return null;
            Assert.IsTrue(Find("Solo Freestyle").IsInteractable());
            Assert.IsFalse(Find("Solo Freestyle").TryGetComponent(out CanvasGroup _), "with one seat the course is lit again");
            Assert.IsFalse(Page.GetComponentsInChildren<LobbyPlayGoalSwap>().Any());
            Assert.IsFalse(Strip.GetComponentsInChildren<TMP_Text>().Any(text => text.transform.parent.name == PlayPage.SeatTag && text.gameObject.activeInHierarchy));
        }

        [UnityTest]
        public IEnumerator ASavedHomeIsOfferedOnlyForLastTeamStanding()
        {
            var saved = SaveHome("courtyard", "Test Den");
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "flat"); menu.Open(LobbyMenu.Play);
            yield return null;
            string card = PlayPage.SavedHomeCard + "courtyard";
            Assert.IsTrue(Find(card).GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Test Den"));
            Assert.AreEqual(GameConfig.Current.Houses.Count, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")));
            Submit(card); yield return null;
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual("courtyard", menu.SavedHome);
            Assert.AreEqual("Test Den", menu.SavedHomeName);
            StringAssert.Contains("Test Den", menu.Describe());
            Assert.AreSame(Find(card).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(GameConfig.Current.Houses.Keys.Any(id => Marked("Map " + id)), "a chosen saved home replaces the house ticks on screen");
            Assert.IsFalse(AnyHouseLit, "and Any house is not lit while a saved home is chosen");
            Assert.AreEqual("courtyard", menu.NextMap(), "a saved home always plays its own house");
            Pick("Duos"); yield return null;
            Assert.AreEqual("courtyard", menu.SavedHome, "Duos plays the saved home too");
            Assert.IsTrue(Find(card).GetComponentsInChildren<Transform>().Any(child => child.name == "Selected"));
            foreach (string mode in new[] { "MovingOut", "MovingDay", "Escape" })
            {
                Pick(mode); yield return null;
                Assert.IsNull(menu.SavedHome, mode + " needs objectives that a home save does not have");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(PlayPage.SavedHomeCard)), mode);
            }
            Pick("Dibs"); yield return null;
            Submit(card); yield return null;
            Submit("Map courtyard"); yield return null;
            Assert.IsNull(menu.SavedHome, "the original house card plays the original furniture");
            Assert.AreEqual(saved.Map, menu.Map);
            Assert.IsTrue(menu.Maps.All, "leaving a saved home brings the earlier ticks back");
            Assert.IsFalse(GameConfig.Current.Houses.Keys.Any(id => Marked("Map " + id)), "every house is one Any house chip, not five ticks");
            Assert.IsTrue(AnyHouseLit);
        }

        [UnityTest]
        public IEnumerator GoPlaysTheSavedHomeInsteadOfTheOriginalFurniture()
        {
            var saved = SaveHome("pinwheel", "Party Place");
            LobbyMenu.StartDelay = .2f;
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos");
            Assert.IsTrue(menu.ChooseHome("pinwheel"));
            menu.Go();
            Assert.IsTrue(menu.Starting);
            yield return TestScenes.WaitForActive(Session.DibsScene, 8f);
            Assert.AreEqual("Duos", Match.Mode);
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.AreEqual("Party Place", room.HomeName);
            CollectionAssert.AreEquivalent(saved.Props.Select(prop => prop.Word), room.Originals.Select(prop => prop.Word));
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual("pinwheel", LobbyMenu.Instance.SavedHome, "the lobby remembers the saved home");
            Assert.AreEqual("Duos", LobbyMenu.Instance.Mode);
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
            StringAssert.Contains("on this screen", menu.Messages[0]);
            StringAssert.Contains("choose " + Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMP_Text>().text, menu.Messages[0],
                "the message names the tab as it is written");
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
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
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
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
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
                menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat"); menu.Open(LobbyMenu.Play);
                var bindings = Session.Bindings.ToArray();
                Submit("Solo TimeRun"); yield return null;
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(900, 580);
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var footer = InPage((RectTransform)Find("Solo GO").transform);
                var card = Find("Solo map ParcelService");
                Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(), "A missing map preview must not pretend an item is the map.");
                AssertSoloFallback("ParcelService");
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
                Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
                CollectionAssert.AreEqual(bindings, Session.Bindings);
                WordboundSoloEntry.Clear(); Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
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
                menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat"); menu.Open(LobbyMenu.Play);
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
                AssertSoloFallback("LostAddress");
                AssertSoloPreview("ParcelHall", parcel);
                profile.ParcelHallPreview = null;
                Assert.DoesNotThrow(profile.Validate);
                menu.Open(LobbyMenu.Play); yield return null;
                AssertSoloFallback("LostAddress");
                AssertSoloFallback("ParcelHall");
                Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
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
                    menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
                    menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
                    Present(PlayPage.BackToModes)?.onClick.Invoke();
                    Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    Assert.IsNull(Present("Map flat"), "each size starts on the mode step, so both visits reach the houses with Next");
                    Reach("Map flat"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
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
                    Submit("Mode ShoppingList"); yield return null;
                    Reach("Map flat"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    AssertSameBounds(house, InPage((RectTransform)Find("Map flat").transform), "House card geometry stays unchanged.");
                }
            }
            finally
            {
                profile.ParcelServicePreview = oldPreview; profile.MapMinimumHeight = oldHeight;
                Object.Destroy(preview); Object.Destroy(texture);
            }
        }

        static void AssertSoloPreview(string id, Sprite authored)
        {
            var card = Find("Solo map " + id);
            var image = card.GetComponentsInChildren<Image>().Single(item => item.name == "Map preview");
            Assert.AreSame(LobbyMapSnapshots.For(id) ?? authored, image.sprite, "A captured snapshot comes first, then the play layout preview.");
            Assert.IsTrue(image.preserveAspect); Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(Color.white, image.color);
            Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(item => item.name.StartsWith("Art ")));
            AssertNameOnPicture(card, InPage((RectTransform)image.transform.parent), id);
            var selected = card.GetComponentsInChildren<Transform>().SingleOrDefault(item => item.name == "Selected");
            if (selected) Assert.Greater(selected.GetSiblingIndex(), image.transform.GetSiblingIndex(),
                "The image cannot cover the selected mark.");
        }

        static void AssertSoloFallback(string id)
        {
            var card = Find("Solo map " + id);
            var snapshot = LobbyMapSnapshots.For(id);
            if (snapshot) { AssertSoloPreview(id, snapshot); return; }
            Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(item => item.name == "Map preview"));
            Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(), "The fallback is a tinted card with a symbol, not item art.");
            var fallback = card.GetComponentsInChildren<Image>().Single(item => item.name == "Map fallback");
            Assert.IsTrue(fallback.GetComponentsInChildren<Image>().Any(item => item.name.StartsWith("Icon ")));
        }

        [UnityTest]
        public IEnumerator SoloFamilyPreservesHouseSelectionPartyAndOriginalArtwork()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
            menu.Open(LobbyMenu.Play);
            var bindings = Session.Bindings.ToArray();
            yield return null;
            Submit("Solo TimeRun");
            yield return null;
            CollectionAssert.AreEqual(AllHouseModes, Modes, "House choices beside the course stay directly reachable.");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            Assert.IsTrue(Find("Solo TimeRun").transform.IsChildOf(Section(HouseModeFamily.Arcade)), "Time Trial sits in Arcade");
            CollectionAssert.AreEqual(new[] { "Solo map ParcelService", "Solo map LostAddress", "Solo map ParcelHall" },
                HouseStep.Where(button => button.name.StartsWith("Solo map "))
                    .Select(button => button.name).ToArray());
            Assert.AreEqual(LobbyPlayModeLabels.Name("TimeRun"), Find("Solo TimeRun").GetComponentInChildren<TMP_Text>().text);
            CollectionAssert.AreEqual(new[] { PlayPage.ModeEmblem }, Find("Solo TimeRun").GetComponentsInChildren<RawImage>().Select(image => image.name).ToArray());
            Submit("Solo map ParcelHall");
            yield return null;
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual("Solo map ParcelHall", EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            CollectionAssert.AreEqual(bindings, Session.Bindings);
            Pick("Freestyle");
            yield return null;
            Assert.AreEqual("Freestyle", WordboundSoloEntry.SelectedMode);
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            Assert.IsTrue(Find("Solo Freestyle").transform.IsChildOf(Section(HouseModeFamily.Training)), "Free Roam sits in Training");
            Assert.AreEqual(LobbyPlayModeLabels.Name("Freestyle"), Find("Solo Freestyle").GetComponentInChildren<TMP_Text>().text);
            CollectionAssert.AreEqual(new[] { PlayPage.ModeEmblem }, Find("Solo Freestyle").GetComponentsInChildren<RawImage>().Select(image => image.name).ToArray());
            Assert.AreEqual("ShoppingList", menu.Mode, "picking a course beside the house modes keeps the house choice");
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Assert.IsFalse(menu.Page<PlayPage>().WordboundSolo, "a second seat returns to the house party");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Solo map ") || button.name == "Solo GO"));
            AssertClosedCourses();
            menu.OpenWordboundSolo("TimeRun");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            CollectionAssert.AreEqual(AllHouseModes, Modes);
            Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            menu.Leave(second);
            yield return null;
            Pick("TimeRun");
            yield return null;
            Assert.AreEqual("TimeRun", WordboundSoloEntry.SelectedMode, "Time Trial comes back after the extra seat leaves");
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap, "the course choice survives the extra seat");
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Solo GO").interactable);
            Canvas.ForceUpdateCanvases();
            foreach (var text in Page.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        [UnityTest]
        public IEnumerator HouseCardsNameOnePictureAtASteadyLeftAlignedWidth()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null; Reach("Map pinwheel"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var grid = Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses");
            var row = InPage((RectTransform)grid.transform);
            var cards = grid.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).ToArray();
            Assert.AreEqual(GameConfig.Current.Houses.Count, cards.Length);
            Assert.AreEqual(HousesInARow, Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath).MapColumns,
                "Resources/UI/LobbyPlayLayout.asset keeps MapColumns at five");
            Assert.IsTrue(grid.GetComponent<LobbyPlayCardGrid>().FillPage, "the house step sizes its cards to the page");
            int across = grid.constraintCount;
            float filled = Mathf.Min(Wreckabulary.Rules.PlaceGrid.CappedWidth(row.width, grid.spacing.x), (row.width - (across - 1) * grid.spacing.x) / across);
            Assert.That(grid.cellSize.x, Is.EqualTo(filled).Within(.5f), "the houses fill the row, or stop at the three-column width");
            var view = InPage(PageScroll.viewport);
            foreach (var card in cards)
            {
                var at = InPage((RectTransform)card.transform);
                Assert.GreaterOrEqual(at.yMin, view.yMin - .5f, card.name + " shows without scrolling");
                Assert.LessOrEqual(at.yMax, view.yMax + .5f, card.name + " shows without scrolling");
            }
            var first = InPage((RectTransform)cards[0].transform);
            Assert.That(first.xMin, Is.EqualTo(row.xMin).Within(.5f), "Cards start at the left edge.");
            Assert.LessOrEqual(cards.Max(card => InPage((RectTransform)card.transform).xMax), row.xMax + .5f, "The row of houses fits the page.");
            foreach (var card in cards.Take(across))
                Assert.That(InPage((RectTransform)card.transform).yMax, Is.EqualTo(first.yMax).Within(.5f), card.name + " shares the first row");
            foreach (var card in cards)
            {
                string id = card.name.Substring(4);
                var layout = GameConfig.Current.Houses[id];
                var bounds = InPage((RectTransform)card.transform);
                Assert.That(bounds.width, Is.EqualTo(first.width).Within(.1f)); Assert.That(bounds.height, Is.EqualTo(first.height).Within(.1f));
                Assert.IsFalse(card.GetComponentsInChildren<RectTransform>().Any(part => part.name == "Fitted plan" || part.name == "Storey" || part.name == "Start"),
                    "The floor-plan skeleton is gone: " + id);
                var picture = card.GetComponentsInChildren<RectTransform>().Single(part => part.name == "Map preview" || part.name == "Map fallback");
                var shape = InPage(picture.name == "Map preview" ? (RectTransform)picture.parent : picture);
                Assert.That(shape.width / shape.height, Is.EqualTo(16f / 9f).Within(.02f), id + " shows one 16:9 picture");
                var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
                var detail = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail");
                Assert.AreEqual(layout.Name, title.text);
                Assert.AreEqual(LobbyMapSnapshots.HouseCaption(id, layout), detail.text);
                Assert.IsNotEmpty(LobbyMapSnapshots.Trait(id), id + " has a short trait line");
                StringAssert.StartsWith(LobbyMapSnapshots.Trait(id), detail.text, id);
                StringAssert.Contains(Wreckabulary.Rules.MapCardText.HouseLine(layout), card.GetComponent<LobbyHint>().Text, id + ": the full facts stay in the hint");
                AssertNameOnPicture(card, shape, id);
                Assert.AreEqual(LobbyMapSnapshots.For(id) != null, picture.name == "Map preview", id);
                foreach (var text in new[] { title, detail }) { text.ForceMeshUpdate(); Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.text); }
            }
        }

        [UnityTest]
        public IEnumerator EveryHouseModeOffersTickedHousesAndEachMatchPicksOneOfThem()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Page<PlayPage>().WordboundMaps = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null;
            var houses = GameConfig.Current.Houses.Keys.ToArray();
            var cards = houses.Select(id => "Map " + id).ToArray();
            Assert.IsTrue(menu.Maps.All, "a first visit ticks every house");
            foreach (string mode in LobbyMenu.Modes)
            {
                Pick(mode); yield return null; yield return null;
                if (mode == LobbyMenu.TutorialMode || mode == HouseModes.BossFight)
                {
                    Assert.IsFalse(HouseStep.Any(button => button.name.StartsWith("Map ")), mode + " plays in its own room");
                    continue;
                }
                Reach(cards[0]); yield return null;
                Canvas.ForceUpdateCanvases();
                var grid = Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses");
                CollectionAssert.AreEqual(cards, grid.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).Select(button => button.name).ToArray(), mode);
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"), mode + ": no separate Random card");
                Assert.IsFalse(cards.Any(Marked), mode + ": every house is one Any house chip, not a row of ticks");
                Assert.IsTrue(AnyHouseLit, mode);
                AssertAnyHouseChip(mode);
            }
            Pick("Dibs"); yield return null;
            Submit(cards[0]); yield return null;
            CollectionAssert.AreEqual(new[] { houses[0] }, menu.Maps.Ticked, "from Any house, a tap plays only that house");
            Assert.IsTrue(Marked(cards[0])); Assert.IsFalse(cards.Skip(1).Any(Marked)); Assert.IsFalse(AnyHouseLit);
            Assert.AreSame(Find(cards[0]).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(menu.Maps.Save(), Session.LobbyMaps, "ticks persist like the map choice");
            Assert.AreEqual(houses[0], menu.Map, "one ticked house is the house you stand in");
            StringAssert.Contains(GameConfig.Current.HouseFor(houses[0]).Name, menu.Describe());
            Submit(cards[1]); yield return null;
            CollectionAssert.AreEqual(new[] { houses[0], houses[1] }, menu.Maps.Ticked, "a further tap adds a house");
            StringAssert.Contains("Any of 2 houses", menu.Describe());
            menu.Roll = count => 0; Assert.AreEqual(houses[0], menu.NextMap());
            menu.Roll = count => count - 1; Assert.AreEqual(houses[1], menu.NextMap());
            Submit(cards[0]); yield return null;
            Assert.IsFalse(menu.Maps.IsTicked(houses[0]), "a second tap unticks a house");
            Assert.IsFalse(Marked(cards[0])); Assert.AreSame(Find(cards[0]).gameObject, EventSystem.current.currentSelectedGameObject);
            CollectionAssert.AreEqual(new[] { houses[1] }, menu.Maps.Ticked);
            menu.Roll = _ => throw new System.InvalidOperationException("one house never rolls");
            Assert.AreEqual(houses[1], menu.NextMap());
            Submit(cards[1]); yield return null;
            Assert.IsTrue(menu.Maps.All, "unticking the last house goes back to Any house");
            Assert.IsTrue(AnyHouseLit); Assert.IsFalse(cards.Any(Marked));
            Assert.IsFalse(menu.Messages.Contains(PlayPage.KeepOneMap), "nothing to warn about");
            Assert.AreEqual(menu.Maps.Save(), Session.LobbyMaps);
            Submit(cards.Last()); yield return null;
            Submit(cards[1]); yield return null;
            CollectionAssert.AreEqual(new[] { houses[1], houses.Last() }, menu.Maps.Ticked);
            Pick("Duos"); yield return null;
            Assert.IsTrue(Marked(cards[1]) && Marked(cards.Last()) && !Marked(cards[0]), "ticks stay the same in the next mode");
            Assert.IsFalse(AnyHouseLit);
            Submit("Queue " + LobbyMenu.Workshop); yield return null;
            Assert.IsTrue(GameConfig.Current.Houses.ContainsKey(menu.Map) && Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The Home builder edits one real house.");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")), "Create shows its two tools, not houses");
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            CollectionAssert.AreEqual(new[] { houses[1], houses.Last() }, menu.Maps.Ticked, "leaving the Home builder keeps the ticks");
            Assert.IsTrue(Marked(cards[1]) && Marked(cards.Last()));
            Submit(PlayPage.AnyHouse); yield return null;
            Assert.IsTrue(menu.Maps.All, "the chip ticks every house at once");
            Assert.IsTrue(AnyHouseLit); Assert.IsFalse(cards.Any(Marked));
            Assert.AreSame(Find(PlayPage.AnyHouse).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(menu.Maps.Save(), Session.LobbyMaps);
        }

        static void AssertAnyHouseChip(string where)
        {
            var chip = Find(PlayPage.AnyHouse);
            Assert.AreEqual(PlayPage.MapsRow, chip.transform.parent.name, where + ": the chip sits beside the Maps label");
            var bounds = InPage((RectTransform)chip.transform);
            Assert.GreaterOrEqual(bounds.height, SmallestTarget - .01f, where);
            Assert.GreaterOrEqual(bounds.yMin, InPage(Page.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.Houses)).yMax - .5f, where + ": above the houses");
            var label = chip.GetComponentsInChildren<TMP_Text>().Single();
            Assert.AreEqual(Wreckabulary.Rules.PracticeMaps.Every, label.text);
            label.ForceMeshUpdate();
            Assert.IsFalse(label.isTextTruncated || label.isTextOverflowing, where + ": " + label.text);
            Assert.GreaterOrEqual(label.fontSize, SmallestLabel - .01f, where);
            Assert.AreEqual(PlayPage.AnyHouseHint, chip.GetComponent<LobbyHint>().Text);
        }

        [UnityTest]
        public IEnumerator GoPlaysTheRolledHouseAndTheTicksSurviveTheMatch()
        {
            LobbyMenu.StartDelay = .2f;
            var menu = LobbyMenu.Instance;
            var houses = GameConfig.Current.Houses.Keys.ToArray();
            menu.Choose(LobbyMenu.Practice, "Dibs", houses[0]); menu.Open(LobbyMenu.Play);
            yield return null;
            foreach (string id in houses.Take(3)) { Submit("Map " + id); yield return null; }
            CollectionAssert.AreEqual(houses.Take(3), menu.Maps.Ticked);
            menu.Roll = count => { Assert.AreEqual(3, count); return 2; };
            menu.Go();
            Assert.IsTrue(menu.Starting);
            yield return TestScenes.WaitForActive(Session.DibsScene, 8f);
            Assert.AreEqual(houses[2], Session.MapId, "the injected roll picked the third ticked house");
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            CollectionAssert.AreEqual(houses.Take(3), LobbyMenu.Instance.Maps.Ticked, "the ticks come back after the match");
            Assert.AreEqual(houses[2], LobbyMenu.Instance.Map, "the lobby stands in the house just played");
        }

        [UnityTest]
        public IEnumerator TheHowToPlayButtonShowsOnlyWhenAGuideExists()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null;
            foreach (string mode in LobbyMenu.PracticeModes.Concat(new[] { "TimeRun", "Freestyle" }))
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var icons = Page.GetComponentsInChildren<Button>().Where(item => item.name == PlayPage.GuideIcon).ToArray();
                Assert.AreEqual(ModeGuides.Has(mode) ? 1 : 0, icons.Length, mode);
                if (icons.Length == 0) continue;
                var goal = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
                goal.ForceMeshUpdate();
                Assert.IsFalse(goal.isTextTruncated || goal.isTextOverflowing, goal.text);
                var icon = InPage((RectTransform)icons[0].transform); var line = InPage(goal.rectTransform); var panel = InPage(ModePanel);
                Assert.IsTrue(icons[0].transform.IsChildOf(ModePanel), mode + ": How to play belongs to the mode panel");
                Assert.LessOrEqual(icon.yMax, line.yMin + .5f, mode + ": under the goal");
                Assert.That(icon.xMin, Is.EqualTo(line.xMin).Within(.5f), mode + ": starting where the goal starts");
                Assert.GreaterOrEqual(icon.yMin, panel.yMin - .5f, mode + ": inside the panel"); Assert.LessOrEqual(icon.xMax, panel.xMax + .5f, mode);
                Assert.IsTrue(icons[0].IsInteractable());
                Assert.GreaterOrEqual(icon.height, SmallestTarget - .01f, mode + ": How to play keeps a full target");
                Assert.GreaterOrEqual(icon.width, SmallestTarget - .01f, mode + ": How to play keeps a full target");
                var label = icons[0].GetComponentInChildren<TMP_Text>();
                label.ForceMeshUpdate();
                StringAssert.Contains(PlayPage.GuideHint, label.text);
                Assert.IsFalse(label.isTextTruncated || label.isTextOverflowing, label.text);
                Assert.GreaterOrEqual(label.fontSize, SmallestLabel - .01f);
                Assert.AreEqual(PlayPage.GuideHint, icons[0].GetComponent<LobbyHint>().Text);
            }
        }

        static void AssertNameOnPicture(Selectable card, Rect shape, string id)
        {
            var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
            var name = InPage(title.rectTransform);
            var detail = InPage(card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").rectTransform);
            Assert.GreaterOrEqual(name.xMin, shape.xMin, id + ": the name starts inside the picture");
            Assert.GreaterOrEqual(name.yMin, shape.yMin - .5f, id + ": the name sits on the picture");
            Assert.LessOrEqual(name.yMax, shape.center.y + .5f, id + ": the name keeps to the lower half");
            Assert.LessOrEqual(detail.yMax, shape.yMin + .5f, id + ": the detail line sits under the picture");
            Assert.AreEqual(Color.white, title.color, id);
            var scrim = card.GetComponentsInChildren<Image>().Single(image => image.name == PlayPage.NameScrim);
            Assert.AreSame(scrim.transform.parent, title.transform.parent, id);
            Assert.Less(scrim.transform.GetSiblingIndex(), title.transform.GetSiblingIndex(), id + ": the gradient sits under the name");
            var gradient = scrim.GetComponent<LobbyGradient>();
            Assert.IsNotNull(gradient, id + ": a soft gradient keeps the name legible over any picture");
            Assert.Less(gradient.To.grayscale, .25f, id + ": the gradient darkens under the white name");
            Assert.Greater(gradient.To.a, .5f, id);
            Assert.IsFalse(scrim.raycastTarget);
        }

        [UnityTest]
        public IEnumerator EverySelectedModeShowsItsGoalInThePanelBesideTheTiles()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel"); menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = Wide;
            yield return null;
            foreach (string mode in LobbyMenu.PracticeModes)
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var bounds = AssertGoal(LobbyPlayModeLabels.Goal(mode));
                StringAssert.Contains(Wreckabulary.Rules.HouseModes.Goal(mode), LobbyPlayModeLabels.Line(mode));
                Assert.GreaterOrEqual(bounds.xMin, InPage(Columns).xMax, mode + ": the goal sits right of the tiles");
                Assert.IsTrue(Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine).transform.IsChildOf(ModePanel), mode + ": in the mode panel");
                var title = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.ModeTitle);
                Assert.AreEqual(LobbyPlayModeLabels.Name(mode), title.text, mode + ": the panel names the mode");
                Assert.GreaterOrEqual(InPage(title.rectTransform).yMin, bounds.yMax - .5f, mode + ": above its goal");
                var access = AssertAccess(mode, bounds);
                Assert.GreaterOrEqual(access.xMin, InPage(Columns).xMax, mode + ": who can play sits right of the tiles too");
                Assert.AreEqual(HouseModes.BotSeats(mode) > 0 && HouseModes.Has(mode), AccessText.ToLowerInvariant().Contains("bot"), mode + ": bots are claimed only where bots play");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == PlayPage.PlayOnline), mode + " plays on this screen only");
                AssertNoScrollingNeeded();
                Assert.AreSame(Find("Mode " + mode).gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.IsTrue(Underlined("Mode " + mode), mode + " is marked as the chosen tile");
            }
            Pick("TimeRun"); yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            AssertAccess("TimeRun", AssertGoal(LobbyPlayModeLabels.Goal("TimeRun")));
            AssertPlayOnlineTarget();
            Page.sizeDelta = Short;
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var narrow = AssertGoal(LobbyPlayModeLabels.Goal("TimeRun"));
            Assert.GreaterOrEqual(narrow.xMin, InPage(Columns).xMax, "a narrow page keeps the goal beside the tiles");
            AssertAccess("TimeRun", narrow);
            AssertPlayOnlineTarget();
        }

        static string AccessText => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.AccessLine).text;

        static Rect AssertAccess(string mode, Rect goal)
        {
            var line = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.AccessLine);
            Assert.AreEqual(Wreckabulary.Rules.ModeAccess.For(mode).Line, line.text, mode);
            Assert.IsNotEmpty(line.text, mode + " says who can play");
            Assert.IsTrue(line.transform.IsChildOf(Strip), "who can play belongs to the mode block");
            var bounds = InPage(line.rectTransform); var block = InPage(Strip);
            Assert.GreaterOrEqual(bounds.xMin, block.xMin - .5f, mode); Assert.LessOrEqual(bounds.xMax, block.xMax + .5f, mode);
            Assert.GreaterOrEqual(bounds.yMin, block.yMin - .5f, mode + ": inside the mode block"); Assert.LessOrEqual(bounds.yMax, block.yMax + .5f, mode);
            Assert.LessOrEqual(bounds.yMax, goal.yMin + .5f, mode + ": under the goal line");
            Assert.That(bounds.xMin, Is.EqualTo(goal.xMin).Within(.5f), mode + ": starting where the goal starts");
            Assert.IsFalse(bounds.Overlaps(InPage(Columns)), "who can play never covers the tiles");
            line.ForceMeshUpdate();
            Assert.IsFalse(line.isTextTruncated || line.isTextOverflowing, line.text);
            Assert.GreaterOrEqual(line.fontSize, SmallestLabel, mode + ": a readable line");
            return bounds;
        }

        static void AssertPlayOnlineTarget()
        {
            var button = Find(PlayPage.PlayOnline);
            var at = InPage((RectTransform)button.transform); var panel = InPage(ModePanel);
            var line = InPage(Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.AccessLine).rectTransform);
            Assert.IsTrue(button.IsInteractable());
            Assert.IsTrue(button.transform.IsChildOf(ModePanel), "Play online belongs to the mode panel");
            Assert.GreaterOrEqual(at.height, SmallestTarget - .01f, "Play online keeps a full target");
            Assert.LessOrEqual(at.yMax, line.yMin + .5f, "under who can play");
            Assert.GreaterOrEqual(at.yMin, panel.yMin - .5f); Assert.LessOrEqual(at.xMax, panel.xMax + .5f, "and inside the panel");
            var guide = Page.GetComponentsInChildren<Button>().FirstOrDefault(item => item.name == PlayPage.GuideIcon);
            if (guide)
            {
                var beside = InPage((RectTransform)guide.transform);
                Assert.That(at.center.y, Is.EqualTo(beside.center.y).Within(.5f), "level with How to play");
                Assert.Greater(at.xMin, beside.xMax, "right of How to play");
            }
            var label = button.GetComponentInChildren<TMP_Text>();
            label.ForceMeshUpdate();
            Assert.AreEqual(PlayPage.PlayOnline, label.text);
            Assert.IsFalse(label.isTextTruncated || label.isTextOverflowing, label.text);
            Assert.GreaterOrEqual(label.fontSize, SmallestLabel);
        }

        [UnityTest]
        public IEnumerator SameScreenShowsModesAndHousesWithoutScrollingAtBothWindowSizes()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            yield return null;
            using var viewport = new StageViewport(menu);
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1248, 576) })
            {
                viewport.Set(size.x, size.y);
                Pick("Dibs");
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
                Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .5f, size + ": the categories and the mode step fit without scrolling");
                AssertStepFits();
                AssertNoScrollingNeeded();
                AssertAccess("Dibs", AssertGoal(LobbyPlayModeLabels.Goal("Dibs")));
                foreach (var text in frame.Panel.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, size + " " + text.name + ": " + text.text);
                }

                Reach("Map pinwheel");
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
                Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .5f, size + ": the house step fits without scrolling");
                var view = InPage(scroll.viewport);
                var cards = Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).ToArray();
                Assert.AreEqual(GameConfig.Current.Houses.Count, cards.Length);
                foreach (var card in cards.Concat(new[] { Find(PlayPage.BackToModes) }))
                {
                    var at = InPage((RectTransform)card.transform);
                    Assert.GreaterOrEqual(at.xMin, view.xMin - .5f, size + ": " + card.name); Assert.LessOrEqual(at.xMax, view.xMax + .5f, size + ": " + card.name);
                    Assert.GreaterOrEqual(at.yMin, view.yMin - .5f, size + ": " + card.name); Assert.LessOrEqual(at.yMax, view.yMax + .5f, size + ": " + card.name);
                }
                foreach (var text in frame.Panel.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, size + " " + text.name + ": " + text.text);
                }
            }
        }

        [UnityTest]
        public IEnumerator SameScreenLeavesHowToPlayToThePreviewAndTheGuide()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            yield return null;
            using var viewport = new StageViewport(menu);
            viewport.Set(1280, 720);
            foreach (string mode in new[] { "Dibs", "Escape", "TimeRun", HouseModes.Tutorial })
            {
                Pick(mode);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.IsNull(HowToPlayStrip, mode + ": no step strip under the maps");
                var preview = Page.GetComponentInChildren<LobbyPlayModePreview>();
                Assert.IsTrue(preview, mode + ": the preview sits beside the modes");
                Assert.AreEqual(mode, preview.Shown, mode + ": and shows the selected mode");
                Assert.AreEqual(1, Page.GetComponentsInChildren<Button>().Count(button => button.name == PlayPage.GuideIcon), mode + ": the i still opens the full guide");
                AssertNoScrollingNeeded();
            }
        }

        static LobbyPlaySpare HowToPlayStrip => Page.GetComponentsInChildren<LobbyPlaySpare>().SingleOrDefault();

        [UnityTest]
        public IEnumerator ACourseOffersPlayOnlineAndItOpensTheOnlineTabOnThatCard()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel"); menu.Open(LobbyMenu.Play);
            yield return null;
            foreach (var (mode, card) in new[] { ("TimeRun", "coop"), ("Freestyle", "freeplay") })
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(Wreckabulary.Rules.ModeAccess.For(mode).Line, AccessText);
                Submit(PlayPage.PlayOnline); yield return null; yield return null;
                Assert.IsTrue(menu.Page<PlayPage>().Online, mode + " opens the Online tab");
                Assert.IsFalse(menu.Page<PlayPage>().Friends);
                Assert.IsTrue(Underlined("Queue " + LobbyMenu.Matchmaking));
                Assert.AreEqual(PlayPage.PlaylistCard + card, EventSystem.current.currentSelectedGameObject.name, mode + " lands on its own card");
                Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "the same-screen choice is not reclassified");
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Play online only changes the tab");
                Assert.IsFalse(menu.Starting);
                Assert.IsEmpty(Modes, "no house modes on the Online tab");
                Submit("Queue " + LobbyMenu.Practice); yield return null;
                Assert.IsTrue(Underlined(PlayPage.ModeButton(mode)), "coming back shows the same course");
            }
        }

        static Rect AssertGoal(string line)
        {
            var goal = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
            Assert.AreEqual(line, goal.text);
            Assert.IsTrue(goal.transform.IsChildOf(Strip), "the goal belongs to the mode block");
            var bounds = InPage(goal.rectTransform); var block = InPage(Strip);
            Assert.GreaterOrEqual(bounds.xMin, block.xMin - .5f); Assert.LessOrEqual(bounds.xMax, block.xMax + .5f);
            Assert.GreaterOrEqual(bounds.yMin, block.yMin - .5f); Assert.LessOrEqual(bounds.yMax, block.yMax + .5f);
            Assert.IsFalse(bounds.Overlaps(InPage(Columns)), "the goal never covers the tiles");
            goal.ForceMeshUpdate();
            Assert.IsFalse(goal.isTextTruncated || goal.isTextOverflowing, goal.text);
            return bounds;
        }

        [Test]
        public void CardGridKeepsItsWidthShrinksOnlyToFitAndKeepsThePictureAt16By9()
        {
            var wide = LobbyPlayCardGrid.Layout(1820, 12, 264, 3, 640);
            Assert.AreEqual(3, wide.columns);
            Assert.AreEqual(LobbyPlayCardGrid.PreferredWidth, wide.cell.x);
            var few = LobbyPlayCardGrid.Layout(1820, 12, 264, 6, 640, 3, HousesInARow);
            Assert.AreEqual(3, few.columns, "three courses sit on the first three house columns");
            Assert.AreEqual(LobbyPlayCardGrid.Layout(1820, 12, 264, 6, 640, HousesInARow, HousesInARow).cell, few.cell, "with the house card footprint");
            Assert.That((wide.cell.x - LobbyPlayCardGrid.Inset * 2) / (wide.cell.y - LobbyPlayCardGrid.CaptionHeight - LobbyPlayCardGrid.Inset), Is.EqualTo(16f / 9f).Within(.001f));
            var narrow = LobbyPlayCardGrid.Layout(840, 12, 264, 3, 0);
            Assert.AreEqual(3, narrow.columns);
            Assert.That(narrow.cell.x, Is.EqualTo((840 - 24) / 3f).Within(.01f));
            var low = LobbyPlayCardGrid.Layout(1820, 12, 264, 3, 200);
            Assert.That(low.cell.y, Is.EqualTo(200 - LobbyPlayCardGrid.ViewportMargin).Within(.01f), "A short viewport still shows a whole card.");
            Assert.That(low.cell.x, Is.EqualTo(LobbyPlayCardGrid.WidthFor(low.cell.y)).Within(.01f), "and narrows the card so the picture stays a full 16:9.");
            Assert.AreEqual(1, LobbyPlayCardGrid.Layout(200, 12, 264, 3, 0).columns);
            var five = LobbyPlayCardGrid.Layout(1838, 12, 264, HousesInARow, 782);
            Assert.AreEqual(HousesInARow, five.columns);
            Assert.That(five.cell.x, Is.EqualTo((1838 - 48) / 5f).Within(.01f), "five houses share the width of a 1280x720 page");
            Assert.That(five.cell.y, Is.EqualTo(LobbyPlayCardGrid.HeightFor(five.cell.x)).Within(.01f));
            Assert.AreEqual(LobbyPlayCardGrid.PreferredWidth, LobbyPlayCardGrid.Layout(2258, 12, 264, HousesInARow, 782).cell.x, "a wider window stops at the steady width");
        }

        [Test]
        public void ColumnFlowFillsOneColumnBeforeTheNextAndWrapsWholeSectionsWhenNarrow()
        {
            foreach (float width in new[] { 1838f, 2258f })
            {
                var page = LobbyPlayColumnFlow.Layout(new[] { 3, 2 }, width, 662);
                Assert.AreEqual(4, page.RowsFit, width.ToString());
                Assert.IsFalse(page.Wrapped, width.ToString());
                CollectionAssert.AreEqual(new[] { 1, 1 }, page.Columns, "Classic and Arcade take one column each");
                CollectionAssert.AreEqual(new[] { 2 }, page.Lines);
                Assert.That(page.Width, Is.EqualTo(LobbyPlayColumnFlow.MaximumWidth).Within(.01f), "columns stop at the widest card");
                Assert.That(page.Height, Is.EqualTo(201f).Within(.01f));
                Assert.That(page.BandHeight, Is.EqualTo(661f).Within(.01f), "34 + 3 x 201 + 24");
                Assert.LessOrEqual(page.BandHeight, 662f, "so the page does not scroll");
                Assert.AreEqual((0, 2), page.Cell(2));
            }
            var small = LobbyPlayColumnFlow.Layout(new[] { 3, 2 }, 842, 256);
            Assert.AreEqual(1, small.RowsFit);
            Assert.IsTrue(small.Wrapped, "five columns of one card cannot fit, so each section keeps one column");
            CollectionAssert.AreEqual(new[] { 1, 1 }, small.Columns);
            CollectionAssert.AreEqual(new[] { 2 }, small.Lines, "both sections still fit side by side");
            Assert.That(small.Width, Is.EqualTo(407f).Within(.01f));
            Assert.That(small.Height, Is.EqualTo(LobbyPlayColumnFlow.MinimumHeight).Within(.01f));
            Assert.That(small.BandHeight, Is.EqualTo(418f).Within(.01f), "taller than the room, so the page scrolls");
            Assert.AreEqual((0, 2), small.Cell(2));
            var grown = LobbyPlayColumnFlow.Layout(new[] { 7, 2 }, 1838, 662);
            Assert.AreEqual(4, grown.RowsFit);
            Assert.IsFalse(grown.Wrapped);
            CollectionAssert.AreEqual(new[] { 2, 1 }, grown.Columns, "a full column continues in a second one beside it");
            Assert.That(grown.Width, Is.EqualTo(599f).Within(.01f));
            Assert.That(grown.Height, Is.EqualTo(148f).Within(.01f));
            Assert.That(grown.BandHeight, Is.EqualTo(662f).Within(.01f));
            Assert.AreEqual((0, 0), grown.Cell(0)); Assert.AreEqual((0, 3), grown.Cell(3));
            Assert.AreEqual((1, 0), grown.Cell(4)); Assert.AreEqual((1, 2), grown.Cell(6));
            Assert.That(grown.SectionWidth(0), Is.EqualTo(599f * 2 + LobbyPlayColumnFlow.Spacing).Within(.01f));
            var slim = LobbyPlayColumnFlow.Layout(new[] { 3, 2 }, 500, 256);
            Assert.IsTrue(slim.Wrapped);
            CollectionAssert.AreEqual(new[] { 1, 1 }, slim.Lines, "sections wrap onto further lines");
            Assert.That(slim.Width, Is.EqualTo(500f).Within(.01f));
            Assert.That(slim.BandHeight, Is.EqualTo(418f + LobbyPlayColumnFlow.SectionSpacing + 286f).Within(.01f));
            var open = LobbyPlayColumnFlow.Layout(new[] { 3, 2 }, 1838, 0);
            CollectionAssert.AreEqual(new[] { 1, 1 }, open.Columns, "with no viewport every section is one column");
            Assert.That(open.Height, Is.EqualTo(LobbyPlayColumnFlow.MaximumHeight).Within(.01f));
            Assert.That(LobbyPlayColumnFlow.Layout(new int[0], 1838, 662).BandHeight, Is.EqualTo(0f));
            foreach (var plan in new[] { small, grown, slim, open })
                Assert.GreaterOrEqual(plan.Height, SmallestTarget, "a card is always a full target");
        }

        [Test]
        public void TheDetailPanelTakesTheSpareWidthOnlyWhenBothColumnsKeepAFullCard()
        {
            float side = LobbyPlayColumnFlow.SideMinimumWidth + LobbyPlayColumnFlow.SectionSpacing;
            float least = side + 2 * LobbyPlayColumnFlow.MinimumWidth + LobbyPlayColumnFlow.SectionSpacing;
            Assert.IsTrue(LobbyPlayColumnFlow.SideFits(2, least));
            Assert.IsFalse(LobbyPlayColumnFlow.SideFits(2, least - 1f), "never squeezes a column under its narrowest card");
            Assert.IsFalse(LobbyPlayColumnFlow.SideFits(0, 4000f), "nothing to describe, no panel");
            foreach (float width in new[] { 1838f, 2258f })
            {
                Assert.IsTrue(LobbyPlayColumnFlow.SideFits(2, width), width + ": both window sizes have room");
                var beside = LobbyPlayColumnFlow.Layout(new[] { 3, 2 }, width - side, 662);
                Assert.IsFalse(beside.Wrapped, width.ToString());
                CollectionAssert.AreEqual(new[] { 1, 1 }, beside.Columns, width.ToString());
                Assert.That(beside.Width, Is.InRange(LobbyPlayColumnFlow.MinimumWidth, LobbyPlayColumnFlow.MaximumWidth), width.ToString());
                float spare = width - beside.SectionWidth(0) - beside.SectionWidth(1) - 2 * LobbyPlayColumnFlow.SectionSpacing;
                Assert.GreaterOrEqual(spare, LobbyPlayColumnFlow.SideMinimumWidth - .01f, width + ": the panel keeps its narrowest width");
            }
        }

        [Test]
        public void ModeTilesShareOneSizeAndFillAsFewRowsAsFit()
        {
            var today = new[] { 4, 3, 3, 2 };
            var wide = LobbyPlayModeFamilies.Layout(today, 1838);
            Assert.AreEqual(1, wide.Rows, "every mode on one row of tiles on a 1280x720 page");
            Assert.GreaterOrEqual(wide.Width, LobbyPlayModeFamilies.MinimumTile);
            var more = LobbyPlayModeFamilies.Layout(new[] { 5, 3, 3, 2 }, 1838);
            var fewer = LobbyPlayModeFamilies.Layout(new[] { 3, 3, 3, 2 }, 1838);
            foreach (var (plan, counts) in new[] { (wide, today), (more, new[] { 5, 3, 3, 2 }), (fewer, new[] { 3, 3, 3, 2 }),
                (LobbyPlayModeFamilies.Layout(today, 1382), today), (LobbyPlayModeFamilies.Layout(today, 860), today), (LobbyPlayModeFamilies.Layout(today, 400), today) })
            {
                float right = 0f;
                for (int group = 0; group < counts.Length; group++)
                {
                    Assert.GreaterOrEqual(plan.Columns[group], 1);
                    Assert.LessOrEqual(plan.Columns[group], counts[group], "never an empty column in a group");
                    int rows = plan.RowsOf(group, counts[group]);
                    int last = counts[group] - (rows - 1) * plan.Columns[group];
                    Assert.Greater(last, 0);
                    Assert.LessOrEqual(rows, plan.Rows, "a group never has more rows than the band");
                    for (int entry = 0; entry < counts[group]; entry++)
                    {
                        var at = plan.Position(group, entry);
                        Assert.GreaterOrEqual(at.x, plan.Left[group] - .01f);
                        right = Mathf.Max(right, at.x + plan.Width);
                    }
                    if (group > 0 && plan.Line[group] == plan.Line[group - 1])
                        Assert.That(plan.Left[group], Is.EqualTo(plan.Left[group - 1] + plan.GroupWidth(group - 1) + LobbyPlayModeFamilies.GroupGap).Within(.01f), "groups sit one group gap apart");
                }
                Assert.LessOrEqual(plan.Width, LobbyPlayModeFamilies.MaximumTile + .01f);
                Assert.AreEqual(LobbyPlayModeFamilies.TileHeight, plan.Height, "one tile height everywhere");
            }
            Assert.LessOrEqual(Right(wide, today), 1838 + .5f);
            Assert.AreEqual(2, LobbyPlayModeFamilies.Layout(today, 1382).Rows, "a 4:3 page takes two rows of tiles");
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 1 }, LobbyPlayModeFamilies.Layout(today, 1382).Columns);
            Assert.IsEmpty(LobbyPlayModeFamilies.Layout(new int[0], 1838).Columns);
        }

        static float Right(LobbyPlayTilePlan plan, int[] counts) =>
            Enumerable.Range(0, counts.Length).Max(group => plan.Left[group] + plan.GroupWidth(group));

        [UnityTest]
        public IEnumerator MatchmakingTabStaysInTheLobbyWithPlaylistCardsAndFindMatch()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "the local selection is not reclassified");
            CollectionAssert.AreEqual(QuickPlayCards.Ids, Named(PlayPage.PlaylistCard), "one card per mode");
            CollectionAssert.AreEqual(new[] { " Duo", " Squad" }, Named(PlayPage.TeamSizeSwitch), "2v2 and 4v4, and no 8v8");
            CollectionAssert.AreEqual(new[] { "2v2", "4v4" }, QuickPlayCards.TeamSizes.Select(size => Find(PlayPage.TeamSizeSwitch + " " + size).GetComponentInChildren<TMP_Text>().text));
            Assert.AreEqual(PlayPage.TeamSizeWord, Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.TeamSizeTitle));
            Assert.IsTrue(Underlined(PlayPage.TeamSizeSwitch + " Duo")); Assert.IsFalse(Underlined(PlayPage.TeamSizeSwitch + " Squad"));
            Assert.IsTrue(Find(PlayPage.TeamSizeSwitch + " Squad").interactable, "one player picks either size");
            Assert.IsFalse(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.TeamSizeNote));
            Assert.AreEqual("Teams of 2 race to the delivery", CardDetail("relay"));
            Assert.AreEqual("Teams of 2 race, and fighting is on", CardDetail("brawl"));
            Assert.IsEmpty(Modes, "no house modes on the Matchmaking tab");
            Assert.IsTrue(Find(PlayPage.FindMatch).interactable); Assert.IsTrue(Find(PlayPage.CustomMatch).interactable);
            Assert.AreEqual("Custom match".ToUpperInvariant(), Find(PlayPage.CustomMatch).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Party · You", Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.PartyLabel), "the party strip is always there");
            Assert.IsTrue(Find(PlayPage.InviteFriendsButton).interactable); Assert.IsTrue(Find(PlayPage.JoinCode).interactable);
            Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_InputField>().Length, "one code box takes party and room codes");
            Assert.AreEqual("", CodeField.text);
            Assert.AreEqual(Wreckabulary.Rules.PlayPageText.CodeHint, ((TMP_Text)CodeField.placeholder).text);
            Assert.AreEqual(PlayPage.TeamSizeRow, CodeField.transform.parent.parent.name, "the code box shares the team size row");
            Assert.AreEqual("4 modes · 2v2", Summary);
            foreach (var card in QuickPlayCards.All)
                Assert.AreEqual(card.PlaylistFor(QuickPlayCards.Duo).Players.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Find(PlayPage.PlaylistCard + card.Id).GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.PlayersCount).text,
                    card.Id + " shows how many people play");
            CollectionAssert.AreEqual(QuickPlaylists.Groups, Page.GetComponentsInChildren<TMP_Text>()
                .Where(text => QuickPlaylists.Groups.Contains(text.text)).Select(text => text.text), "Classic, then Arcade");
            yield return MatchmakingFitsWithoutScrolling(menu, 1280, 720);
            yield return MatchmakingFitsWithoutScrolling(menu, 1248, 576);
            Submit(PlayPage.TeamSizeSwitch + " Squad");
            yield return null;
            Assert.IsTrue(Underlined(PlayPage.TeamSizeSwitch + " Squad")); Assert.IsFalse(Underlined(PlayPage.TeamSizeSwitch + " Duo"));
            Assert.AreEqual(PlayPage.TeamSizeSwitch + " Squad", EventSystem.current.currentSelectedGameObject.name, "focus stays on the switch");
            Assert.AreEqual("4 modes · 4v4", Summary, "the footer reads the switch");
            Assert.AreEqual("Teams of 4 race to the delivery", CardDetail("relay"));
            Assert.AreEqual("Teams of 4 race, and fighting is on", CardDetail("brawl"));
            Assert.AreEqual("Up to 8 fight, most knockouts wins", CardDetail("ffa"));
            string saved = PlayerPrefs.GetString(QuickPlayChoices.StorageKey);
            StringAssert.StartsWith("v2;lists=relay-4v4+brawl-4v4+ffa+coop;", saved); StringAssert.EndsWith(";size=Squad", saved);
            Submit(PlayPage.TeamSizeSwitch + " Duo");
            yield return null;
            Assert.IsFalse(Ticked("freeplay")); Assert.IsTrue(Ticked("ffa"));
            Submit(PlayPage.PlaylistCard + "freeplay");
            yield return null;
            Assert.IsTrue(Ticked("freeplay")); Assert.AreEqual("All modes · 2v2", Summary);
            StringAssert.Contains("freeplay", PlayerPrefs.GetString(QuickPlayChoices.StorageKey));
            foreach (string id in QuickPlayCards.Ids.Skip(1))
            {
                Submit(PlayPage.PlaylistCard + id);
                yield return null;
            }
            Assert.AreEqual("Team Race 2v2", Summary);
            Submit(PlayPage.PlaylistCard + "relay");
            yield return null;
            Assert.IsTrue(Ticked("relay"), "the last card stays ticked");
            CollectionAssert.Contains(menu.Messages, PlayPage.KeepOnePlaylist);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "ticking never leaves the lobby");
            CodeField.text = "not a code";
            Submit(PlayPage.JoinCode);
            yield return null;
            CollectionAssert.Contains(menu.Messages, Wreckabulary.Rules.PlayPageText.NotACode);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "a wrong party code never leaves the lobby");
            Assert.IsNull(WordboundRoomEntry.TakeRequest(), "a wrong party code asks for nothing");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.IsNotEmpty(Modes); Assert.AreEqual("Duos", menu.Mode);
        }

        IEnumerator MatchmakingFitsWithoutScrolling(LobbyMenu menu, float width, float height)
        {
            using var viewport = new StageViewport(menu);
            viewport.Set(width, height);
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            var column = scroll.content;
            Assert.AreEqual(PlayPage.TeamSizeRow, column.GetChild(0).name, "the switch row comes first");
            Assert.AreEqual(PlayPage.PartyStrip, column.GetChild(1).name, "then the party strip");
            Assert.AreEqual(PlayPage.PlaylistBand, column.GetChild(2).name, "then the cards");
            var tabs = InPage(frame.Queues); var row = InPage((RectTransform)column.GetChild(0));
            Assert.LessOrEqual(row.yMax, tabs.yMin + .5f, "the switch sits just under the tabs");
            Assert.LessOrEqual(InPage((RectTransform)column.GetChild(1)).yMax, row.yMin + .5f);
            Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .5f, "Online never scrolls up and down at " + width + "x" + height);
            var view = InPage(scroll.viewport);
            var band = column.GetChild(2);
            var sections = QuickPlaylists.Groups.Select(group => (RectTransform)band.Find(PlayPage.PlaylistSection + group)).ToArray();
            CollectionAssert.AreEqual(new[] { 3, 2 }, sections.Select(section => section.GetComponentsInChildren<Button>().Length).ToArray(),
                "Classic holds three cards and Arcade two");
            var size = InPage((RectTransform)Find(PlayPage.PlaylistCard + QuickPlayCards.Ids[0]).transform);
            for (int at = 0; at < sections.Length; at++)
            {
                string group = QuickPlaylists.Groups[at];
                var buttons = sections[at].GetComponentsInChildren<Button>();
                CollectionAssert.AreEqual(QuickPlayCards.All.Where(card => card.Group == group).Select(card => PlayPage.PlaylistCard + card.Id).ToArray(),
                    buttons.Select(button => button.name).ToArray(), group + " top to bottom");
                var caption = InPage((RectTransform)sections[at].Find(LobbyPlayColumnFlow.Caption));
                Rect above = caption;
                foreach (var button in buttons)
                {
                    var card = InPage((RectTransform)button.transform);
                    Assert.GreaterOrEqual(card.xMin, view.xMin - .5f, button.name); Assert.LessOrEqual(card.xMax, view.xMax + .5f, button.name);
                    Assert.GreaterOrEqual(card.yMin, view.yMin - .5f, button.name); Assert.LessOrEqual(card.yMax, view.yMax + .5f, button.name);
                    Assert.That(card.xMin, Is.EqualTo(caption.xMin).Within(.5f), button.name + " starts at its column's left edge");
                    Assert.That(card.width, Is.EqualTo(size.width).Within(.5f), "the cards share one width");
                    Assert.That(card.height, Is.EqualTo(size.height).Within(.5f), "and one height");
                    Assert.LessOrEqual(card.yMax, above.yMin + .5f, button.name + " sits under the one before it");
                    Assert.GreaterOrEqual(card.height, SmallestTarget, button.name);
                    AssertRowCard(button, card);
                    above = card;
                }
            }
            Assert.That(size.width, Is.InRange(LobbyPlayColumnFlow.MinimumWidth, LobbyPlayColumnFlow.MaximumWidth));
            Assert.That(size.height, Is.InRange(LobbyPlayColumnFlow.MinimumHeight, LobbyPlayColumnFlow.MaximumHeight));
            var classic = InPage(sections[0]); var arcade = InPage(sections[1]);
            Assert.Greater(arcade.xMin, classic.xMax, "Arcade starts right of Classic");
            Assert.That(arcade.yMax, Is.EqualTo(classic.yMax).Within(.5f), "at the same top");
            var plan = band.GetComponent<LobbyPlayColumnFlow>().Plan;
            Assert.IsFalse(plan.Wrapped, "both window sizes have room for both columns");
            CollectionAssert.AreEqual(new[] { 1, 1 }, plan.Columns);
            foreach (var text in frame.Panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        static void AssertRowCard(Button card, Rect at)
        {
            var picture = card.GetComponentsInChildren<RectTransform>().Single(part => part.name == "Map preview" || part.name == "Map fallback");
            var shape = InPage(picture.name == "Map preview" ? (RectTransform)picture.parent : picture);
            Assert.That(shape.width / shape.height, Is.EqualTo(16f / 9f).Within(.02f), card.name + " shows one 16:9 picture");
            Assert.That(shape.xMin - at.xMin, Is.EqualTo(LobbyPlayRowCard.Inset).Within(.5f), card.name + ": the picture sits on the left");
            Assert.GreaterOrEqual(shape.yMin, at.yMin - .5f, card.name); Assert.LessOrEqual(shape.yMax, at.yMax + .5f, card.name);
            var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
            var detail = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail");
            foreach (var text in new[] { title, detail })
            {
                var words = InPage(text.rectTransform);
                Assert.GreaterOrEqual(words.xMin, shape.xMax, card.name + ": " + text.name + " is right of the picture");
                Assert.LessOrEqual(words.xMax, at.xMax + .5f, card.name + ": " + text.name);
                Assert.GreaterOrEqual(words.yMin, at.yMin - .5f, card.name + ": " + text.name); Assert.LessOrEqual(words.yMax, at.yMax + .5f, card.name + ": " + text.name);
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, card.name + ": " + text.text);
                Assert.GreaterOrEqual(text.fontSize, SmallestLabel, card.name + ": " + text.name);
            }
            Assert.LessOrEqual(InPage(detail.rectTransform).yMax, InPage(title.rectTransform).yMin + .5f, card.name + ": the name sits over the detail");
            Assert.AreEqual(LobbyKit.Navy, title.color, card.name);
            Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(image => image.name == PlayPage.NameScrim), card.name + ": the name is not on the picture");
        }

        [UnityTest]
        public IEnumerator OnlineCardsShowTheirOwnPictureAndThePanelDescribesTheChosenCard()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            var emblems = new System.Collections.Generic.List<string>();
            foreach (var card in QuickPlayCards.All)
            {
                var art = Find(PlayPage.PlaylistCard + card.Id).GetComponentsInChildren<RawImage>().Select(image => image.name).ToArray();
                CollectionAssert.AreEqual(new[] { "Art " + LobbyPlayModeLabels.Emblem(card.Mode) }, art, card.Id + " shows its own mode picture");
                emblems.Add(art[0]);
            }
            CollectionAssert.AllItemsAreUnique(emblems, "no two cards share a picture");
            using var viewport = new StageViewport(menu);
            viewport.Set(1280, 720);
            yield return null;
            yield return TestScenes.WaitUntil(() => Band.GetComponent<LobbyPlayColumnFlow>().SideShown, 3f, "a 1280x720 window has room for the detail panel beside the rail");
            Assert.AreEqual(QuickPlayCards.StartTicked[0], Page.GetComponentInChildren<LobbyPlayDetail>().Shown, "the panel opens on the first ticked card");
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1248, 576) })
            {
                viewport.Set(size.x, size.y);
                yield return null;
                yield return TestScenes.WaitUntil(() => Band.GetComponent<LobbyPlayColumnFlow>().SideShown, 3f, size + ": room for the detail panel");
                foreach (var card in QuickPlayCards.All)
                {
                    EventSystem.current.SetSelectedGameObject(Find(PlayPage.PlaylistCard + card.Id).gameObject);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertDetail(card, size + " " + card.Id);
                }
                var scroll = PageScroll;
                Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + .5f, size + ": the panel never makes Online scroll");
            }
            EventSystem.current.SetSelectedGameObject(Find(PlayPage.PlaylistCard + "brawl").gameObject);
            yield return null;
            Submit(LobbyPlayDetail.GuideName);
            yield return null;
            try
            {
                Assert.IsTrue(ModeGuides.Open, "the panel opens the guide");
                Assert.AreEqual("BrawlRace", ModeGuides.Mode, "for the card it describes");
            }
            finally { ModeGuides.Close(); }
            yield return null;
            Assert.IsFalse(Ticked("freeplay"));
            Submit(PlayPage.PlaylistCard + "freeplay");
            yield return null;
            Assert.IsTrue(Ticked("freeplay"));
            yield return TestScenes.WaitUntil(() => Band.GetComponent<LobbyPlayColumnFlow>().SideShown, 3f, "the rebuilt page keeps the detail panel");
            Assert.AreEqual("freeplay", Page.GetComponentInChildren<LobbyPlayDetail>().Shown, "ticking a card keeps the panel on it after the page rebuilds");
        }

        static Transform Band => PageScroll.content.Find(PlayPage.PlaylistBand);

        static void AssertDetail(QuickPlayCard card, string where)
        {
            var detail = Page.GetComponentInChildren<LobbyPlayDetail>();
            Assert.AreEqual(PlayPage.PlaylistDetail, detail.name, where);
            Assert.AreEqual(card.Id, detail.Shown, where + ": the panel follows the selected card");
            var view = InPage(PageScroll.viewport); var panel = InPage((RectTransform)detail.transform);
            var arcade = InPage((RectTransform)Band.Find(PlayPage.PlaylistSection + QuickPlaylists.Arcade));
            Assert.GreaterOrEqual(panel.xMin, arcade.xMax + LobbyPlayColumnFlow.SectionSpacing - .5f, where + ": right of the columns");
            Assert.LessOrEqual(panel.xMax, view.xMax + .5f, where); Assert.GreaterOrEqual(panel.yMin, view.yMin - .5f, where);
            Assert.LessOrEqual(panel.yMax, view.yMax + .5f, where);
            Assert.That(panel.width, Is.InRange(LobbyPlayColumnFlow.SideMinimumWidth - .5f, LobbyPlayColumnFlow.SideMaximumWidth + .5f), where);
            var texts = detail.GetComponentsInChildren<TMP_Text>();
            Assert.AreEqual(LobbyPlayModeLabels.Name(card.Mode), Named(texts, LobbyPlayDetail.TitleName), where);
            Assert.AreEqual(OnlineMaps.Name(OnlineMaps.CourseFor(card.Mode)), Named(texts, LobbyPlayDetail.PlaceName), where + ": names the course it plays");
            Assert.AreEqual(Wreckabulary.Rules.ModeAccess.For(card.Mode).Line, Named(texts, LobbyPlayDetail.WhoName), where);
            var steps = Wreckabulary.Rules.ModeAccess.Steps(card.Mode);
            Assert.That(steps.Count, Is.InRange(1, LobbyPlayDetail.MostSteps), where);
            CollectionAssert.AreEqual(steps.Select((step, at) => (at + 1) + LobbyPlayDetail.StepSeparator + step).ToArray(),
                texts.Where(text => text.name.StartsWith(LobbyPlayDetail.StepName)).Select(text => text.text).ToArray(), where);
            foreach (var text in texts)
            {
                var words = InPage(text.rectTransform);
                Assert.GreaterOrEqual(words.xMin, panel.xMin - .5f, where + " " + text.name); Assert.LessOrEqual(words.xMax, panel.xMax + .5f, where + " " + text.name);
                Assert.GreaterOrEqual(words.yMin, panel.yMin - .5f, where + " " + text.name); Assert.LessOrEqual(words.yMax, panel.yMax + .5f, where + " " + text.name);
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, where + " " + text.name + ": " + text.text);
                Assert.GreaterOrEqual(text.fontSize, SmallestLabel, where + " " + text.name);
            }
            var guide = Find(LobbyPlayDetail.GuideName);
            Assert.AreEqual(ModeGuides.Has(card.Mode), guide.IsInteractable(), where);
            var target = InPage((RectTransform)guide.transform);
            Assert.GreaterOrEqual(target.height, SmallestTarget - .01f, where + ": the guide button keeps a full target");
            Assert.GreaterOrEqual(target.yMin, panel.yMin - .5f, where); Assert.LessOrEqual(target.xMax, panel.xMax + .5f, where);
            Assert.IsNull(guide.transform.parent.GetComponentInParent<Button>(), where + ": the guide button is not inside a card");
            var frame = InPage((RectTransform)detail.transform.Find(LobbyPlayDetail.FrameName));
            Assert.That(frame.width / frame.height, Is.EqualTo(16f / 9f).Within(.02f), where + ": one 16:9 picture");
            Assert.LessOrEqual(target.yMax, frame.yMin + .5f, where + ": the words sit under the picture");
            var course = LobbyMapSnapshots.ForMode(card.Mode);
            var picture = detail.GetComponentsInChildren<Image>(true).Single(image => image.name == LobbyPlayDetail.PictureName);
            if (course) Assert.AreSame(course, picture.sprite, where + ": the picture is the course this mode plays");
        }

        [UnityTest]
        public IEnumerator CustomMatchOffersTheGuideOfTheChosenRoomMode()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Submit(PlayPage.CustomMatch);
            yield return null;
            var emblems = new System.Collections.Generic.List<string>();
            foreach (var room in FriendRooms.All)
            {
                Submit(PlayPage.RoomModeCard + room.Id);
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var art = Find(PlayPage.RoomModeCard + room.Id).GetComponentsInChildren<RawImage>().Select(image => image.name).ToArray();
                CollectionAssert.AreEqual(new[] { "Art " + LobbyPlayModeLabels.Emblem(room.Mode) }, art, room.Id + " shows its own mode picture");
                emblems.Add(art[0]);
                Assert.AreEqual(ModeGuides.Has(room.Mode), HowToPlayStrip != null, room.Id + ": the room mode's steps sit under the choices");
                Assert.IsFalse(Page.GetComponentsInChildren<TMP_Text>(true).Any(text => text.name == PlayPage.LastMatchLine),
                    room.Id + ": an online room never quotes a same-screen result");
                if (HowToPlayStrip && HowToPlayStrip.Shown)
                    Assert.LessOrEqual(PageScroll.content.rect.height, PageScroll.viewport.rect.height + .5f, room.Id + ": the steps never make Custom match scroll");
                var icons = Page.GetComponentsInChildren<Button>().Where(button => button.name == PlayPage.GuideIcon).ToArray();
                Assert.AreEqual(ModeGuides.Has(room.Mode) ? 1 : 0, icons.Length, room.Id + ": one guide button when a guide exists");
                if (icons.Length == 0) continue;
                var title = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Friends room");
                Assert.IsTrue(icons[0].transform.IsChildOf(title.transform), room.Id + ": beside the title that names the room");
                Assert.IsNull(icons[0].transform.parent.GetComponentInParent<Button>(), room.Id + ": not inside a card");
                var target = InPage((RectTransform)icons[0].transform);
                Assert.GreaterOrEqual(target.height, SmallestTarget - .01f, room.Id); Assert.GreaterOrEqual(target.width, SmallestTarget - .01f, room.Id);
                title.ForceMeshUpdate();
                Assert.IsFalse(title.isTextTruncated || title.isTextOverflowing, title.text);
                var ink = title.textBounds;
                float end = Page.InverseTransformPoint(title.rectTransform.TransformPoint(new Vector3(ink.max.x, ink.center.y))).x;
                Assert.GreaterOrEqual(target.xMin, end - .5f, room.Id + ": after the title");
                Assert.LessOrEqual(target.xMax, InPage((RectTransform)Find(PlayPage.BackToPlaylists).transform).xMin + .5f, room.Id + ": clear of the footer buttons");
                Assert.AreEqual(PlayPage.GuideHint, icons[0].GetComponent<LobbyHint>().Text);
                icons[0].onClick.Invoke();
                yield return null;
                try
                {
                    Assert.IsTrue(ModeGuides.Open, room.Id);
                    Assert.AreEqual(room.Mode, ModeGuides.Mode, room.Id);
                }
                finally { ModeGuides.Close(); }
                yield return null;
            }
            CollectionAssert.AllItemsAreUnique(emblems, "no two room cards share a picture");
        }

        [UnityTest]
        public IEnumerator CustomMatchHostsOrJoinsFromTheLobby()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Submit(PlayPage.CustomMatch);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Custom match stays in the lobby");
            Assert.AreEqual(PlayPage.AmongYourselves, Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.CustomMatchNote));
            CollectionAssert.AreEqual(FriendRooms.All.Select(room => room.Id), Named(PlayPage.RoomModeCard));
            Assert.IsEmpty(Named(PlayPage.PlaylistCard), "the playlists step aside");
            foreach (string name in new[] { PlayPage.CreateRoom, PlayPage.JoinCode, PlayPage.CustomMap, PlayPage.BackToPlaylists })
                Assert.IsTrue(Find(name).interactable, name);
            Assert.AreEqual("Team Race 2v2 · The Lost Address", FriendsRoom);
            StringAssert.StartsWith("Needs 4 players", FriendsDetail);
            CollectionAssert.AreEqual(new[] { " Solo", " Duo", " Squad" }, Named(PlayPage.TeamSizeChoice));
            var column = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>().content;
            Assert.AreEqual(PlayPage.TeamSizeChoice + " row", column.GetChild(0).name, "Custom match uses the same switch row at the top");
            Assert.AreEqual(PlayPage.TeamSizeWord, column.GetChild(0).GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.TeamSizeTitle).text);
            Assert.IsTrue(Named(PlayPage.TeamSizeChoice).All(size => Find(PlayPage.TeamSizeChoice + size).interactable));
            Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_InputField>().Length, "one code box");
            Assert.AreEqual(PlayPage.TeamSizeChoice + " row", CodeField.transform.parent.parent.name, "the code box shares the team size row");
            Assert.AreEqual(FriendRooms.Layouts, Named(PlayPage.LayoutChoice).Length);
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" },
                Enumerable.Range(1, FriendRooms.Layouts).Select(slot => Find(PlayPage.LayoutChoice + " " + slot).GetComponentInChildren<TMP_Text>().text),
                "the map chips are short version numbers");
            AssertLayoutRow("The Lost Address");
            Submit(PlayPage.TeamSizeChoice + " Solo");
            yield return null;
            Assert.AreEqual("Team Race 1v1 · The Lost Address", FriendsRoom);
            Submit(PlayPage.LayoutChoice + " 3");
            yield return null;
            Assert.AreEqual("Team Race 1v1 · The Lost Address 3", FriendsRoom);
            Submit(PlayPage.RoomModeCard + "ffa");
            yield return null;
            Assert.AreEqual("Free-for-All · Toybox Arena 3", FriendsRoom);
            AssertLayoutRow("Toybox Arena");
            CollectionAssert.AreEqual(new[] { " Solo", " Duo", " Squad" }, Named(PlayPage.TeamSizeChoice), "the row stays put");
            Assert.IsFalse(Named(PlayPage.TeamSizeChoice).Any(size => Find(PlayPage.TeamSizeChoice + size).interactable), "team size is for team modes");
            Assert.AreEqual(PlayPage.NoTeams(FriendRooms.Find("ffa")), Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.TeamSizeNote));
            StringAssert.StartsWith("2 to 8 players", FriendsDetail);
            Assert.AreEqual("v1;mode=ffa;team=Solo;layout=2", PlayerPrefs.GetString(FriendRooms.StorageKey));
            CodeField.text = "not a code";
            Submit(PlayPage.JoinCode);
            yield return null;
            CollectionAssert.Contains(menu.Messages, Wreckabulary.Rules.PlayPageText.NotACode);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "a wrong code never leaves the lobby");
            Assert.AreEqual("not a code", CodeField.text);
            Submit(PlayPage.BackToPlaylists);
            yield return null;
            CollectionAssert.AreEqual(QuickPlayCards.Ids, Named(PlayPage.PlaylistCard));
            Assert.IsTrue(Find(PlayPage.FindMatch).interactable);
        }

        static void AssertLayoutRow(string course)
        {
            Canvas.ForceUpdateCanvases();
            var row = Page.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.LayoutRow);
            var bounds = InPage(row);
            Assert.LessOrEqual(bounds.height, 60f, "the map choice is one compact row");
            var picture = InPage(row.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.LayoutPicture));
            Assert.That(picture.width / picture.height, Is.EqualTo(16f / 9f).Within(.02f), "a small course picture");
            var name = row.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.LayoutCourse);
            Assert.AreEqual(course, name.text);
            Assert.GreaterOrEqual(InPage(name.rectTransform).xMin, picture.xMax - .5f, "the name follows the picture");
            foreach (var text in row.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
                Assert.GreaterOrEqual(text.fontSize, SmallestLabel - .01f, text.name);
            }
            for (int slot = 1; slot <= FriendRooms.Layouts; slot++)
            {
                var chip = InPage((RectTransform)Find(PlayPage.LayoutChoice + " " + slot).transform);
                Assert.GreaterOrEqual(chip.height, SmallestTarget - .01f, "version " + slot);
                Assert.GreaterOrEqual(chip.width, SmallestTarget - .01f, "version " + slot);
                Assert.GreaterOrEqual(chip.xMin, InPage(name.rectTransform).xMax - .5f, "the versions follow the name");
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
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "the Matchmaking tab stays in the lobby");
            Submit(PlayPage.CustomMatch);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Custom match stays in the lobby");
            CodeField.text = BrokenRoom;
            Submit(PlayPage.JoinCode);
            yield return TestScenes.WaitForActive(Session.WordboundScene);
            yield return null; yield return null;
            var party = Object.FindAnyObjectByType<Wreckabulary.Online.WordboundPartyMenu>();
            Assert.IsTrue(party && party.InRoom, "the room view opens in the online scene");
            Assert.IsFalse(party.Coordinator.Authenticated, "a code that cannot work never reaches the service");
            Assert.IsNull(WordboundRoomEntry.TakeRequest());
            Assert.AreEqual(Session.WordboundScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.AreEqual(LobbyMenu.Practice, Session.LobbyQueue);
            Assert.AreEqual("Duos", Session.LobbyMode);
            Assert.AreEqual("flat", Session.MapId);
            Assert.IsNull(Object.FindAnyObjectByType<RoundManager>(), "no legacy battle starts on the online route");
            Assert.IsEmpty(Object.FindObjectsByType<BotController>(FindObjectsSortMode.None));
        }

        const string StoodIn = "pinwheel";
        const string RememberedWorkshop = "v1;tab=workshop;mode=Duos;maps=;course=TimeRun;courseMap=ParcelService;shown=house;chosen=1";
        static readonly string[] BadMemories =
        {
            "v0;tab=practice;mode=Duos;maps=;course=TimeRun;courseMap=ParcelService;shown=house;chosen=1",
            "v1;tab=practice;mode=NoSuchMode;maps=;course=TimeRun;courseMap=ParcelService;shown=house;chosen=1",
        };

        static IEnumerator NextLaunch()
        {
            yield return TestScenes.Reset();
            yield return OpenLobby();
        }

        static void AssertNothingRemembered(string why)
        {
            var menu = LobbyMenu.Instance;
            var play = menu.Page<PlayPage>();
            Assert.AreEqual(LobbyMenu.Home, menu.Current, why);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, why);
            Assert.AreEqual("Dibs", menu.Mode, why);
            Assert.IsTrue(menu.Maps.All, why);
            Assert.IsFalse(play.Online || play.WordboundSolo, why);
            Assert.AreEqual("TimeRun", WordboundSoloEntry.SelectedMode, why);
            Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, why);
            Assert.IsNull(menu.LastMatch, why);
        }

        [UnityTest]
        public IEnumerator ARealChoiceIsSavedAndComesBackOnTheNextLaunch()
        {
            var menu = LobbyMenu.Instance;
            string other = GameConfig.Current.Houses.Keys.First(id => id != StoodIn);
            string[] ticked = GameConfig.Current.Houses.Keys.Where(id => id == StoodIn || id == other).ToArray();
            Assert.IsFalse(LobbyMenu.RememberChoices, "the editor leaves the saved choice alone until a test asks for it");
            Pick("Duos"); yield return null;
            Assert.IsFalse(PlayerPrefs.HasKey(LastKey), "so an ordinary test run writes nothing");
            LobbyMenu.RememberChoices = true;
            menu.Choose(LobbyMenu.Practice, "MovingOut");
            Assert.IsFalse(PlayerPrefs.HasKey(LastKey), "Choose alone is setup, not a player's choice");
            menu.Open(LobbyMenu.Play); yield return null;
            Pick("ShoppingList"); yield return null;
            Assert.AreEqual("v1;tab=practice;mode=ShoppingList;maps=;course=TimeRun;courseMap=ParcelService;shown=house;chosen=1",
                PlayerPrefs.GetString(LastKey), "every house ticked is saved as no list");
            Submit("Map " + other); yield return null;
            Assert.AreEqual("v1;tab=practice;mode=ShoppingList;maps=" + other + ";course=TimeRun;courseMap=ParcelService;shown=house;chosen=1",
                PlayerPrefs.GetString(LastKey), "from Any house a tap keeps only that house");
            Submit("Map " + StoodIn); yield return null;
            string saved = PlayerPrefs.GetString(LastKey);
            Assert.AreEqual("v1;tab=practice;mode=ShoppingList;maps=" + string.Join("+", ticked) + ";course=TimeRun;courseMap=ParcelService;shown=house;chosen=1", saved);

            yield return NextLaunch();
            menu = LobbyMenu.Instance;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "a new launch still opens on Home");
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue); Assert.AreEqual("ShoppingList", menu.Mode);
            CollectionAssert.AreEqual(ticked, menu.Maps.Ticked);
            Assert.IsTrue(menu.Maps.IsTicked(menu.Map), "the lobby stands in a ticked house");
            Assert.AreEqual(menu.MatchTitle, menu.LastMatch, "Home can start it again");
            StringAssert.StartsWith(LobbyMenu.ModeName("ShoppingList") + Wreckabulary.Rules.MapCardText.Separator, menu.LastMatch);
            Assert.AreEqual(saved, PlayerPrefs.GetString(LastKey), "restoring writes nothing");

            menu.Open(LobbyMenu.Play); yield return null;
            Pick("Duos"); yield return null;
            StringAssert.Contains(";mode=Duos;", PlayerPrefs.GetString(LastKey));
            PlayerPrefs.SetString(LastKey, saved);
            yield return OpenLobby();
            Assert.AreEqual("Duos", LobbyMenu.Instance.Mode, "a choice made in this run is never replaced by the saved one");
            StringAssert.StartsWith(LobbyMenu.ModeName("Duos") + Wreckabulary.Rules.MapCardText.Separator, LobbyMenu.Instance.LastMatch);
        }

        [UnityTest]
        public IEnumerator TheLastTabAndCourseComeBackAndABadValueRemembersNothing()
        {
            LobbyMenu.RememberChoices = true;
            Pick("Freestyle"); yield return null;
            Submit("Solo map " + WordboundSoloEntry.LostAddressMap); yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking); yield return null;
            string saved = PlayerPrefs.GetString(LastKey);
            Assert.AreEqual("v1;tab=matchmaking;mode=Dibs;maps=;course=Freestyle;courseMap=LostAddress;shown=course;chosen=1", saved);

            yield return NextLaunch();
            var menu = LobbyMenu.Instance;
            var play = menu.Page<PlayPage>();
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.IsTrue(play.Online, "Play opens on Matchmaking again");
            Assert.IsTrue(play.WordboundSolo, "and Practice still shows the course");
            Assert.AreEqual("Freestyle", WordboundSoloEntry.SelectedMode);
            Assert.AreEqual(WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "the Matchmaking tab never reclassifies the local choice");
            Assert.IsNull(menu.LastMatch, "Home does not start a local match while Matchmaking was used last");

            PlayerPrefs.SetString(LastKey, RememberedWorkshop);
            yield return NextLaunch();
            menu = LobbyMenu.Instance;
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue, "Create comes back as the last tab");
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Assert.IsFalse(menu.Page<PlayPage>().Online);
            Assert.IsNull(menu.LastMatch, "and Home opens the Play section instead of starting");

            foreach (string bad in BadMemories)
            {
                PlayerPrefs.SetString(LastKey, bad);
                yield return NextLaunch();
                AssertNothingRemembered(bad);
            }

            PlayerPrefs.SetString(LastKey, saved);
            LobbyMenu.RememberChoices = false;
            yield return NextLaunch();
            AssertNothingRemembered("remembering is off");
            Assert.AreEqual(saved, PlayerPrefs.GetString(LastKey), "and the saved value is left alone");
        }
    }
}
