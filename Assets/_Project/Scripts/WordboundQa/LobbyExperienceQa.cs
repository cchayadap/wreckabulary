#if UNITY_EDITOR || GSK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameStudioKit.Qa;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class LobbyExperienceQa : IQaScenario
    {
        readonly bool hierarchyOnly;
        public LobbyExperienceQa(bool captureHierarchyOnly = false) { hierarchyOnly = captureHierarchyOnly; }
        public string Name => hierarchyOnly ? "play_hierarchy" : "lobby_experience";
        static readonly string[] PartyModes = LobbyMenu.Modes;
        static readonly string[] CreateTools = { "Create house", "Create Wordbound" };
        const string QaHomeMap = "pinwheel", QaHomeName = "QA Home";
        static readonly string[] SettingsTabs = { "profile", "audio", "gameplay", "graphics", "controls", "help" };
        static readonly string[] NavNames = { "Home", "Settings", "Quit", "LOADOUT", "PLAY", "CAREER", "Leaderboard", "Shop", "Party" };
        const float SmallestModeLabel = 15f;
        LobbyMenu menu;
        Preference[] preferences;
        Preference lastChoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            QaScenarios.Register("lobby_experience", () => new LobbyExperienceQa());
            QaScenarios.Register("play_hierarchy", () => new LobbyExperienceQa(true));
        }

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
            if (hierarchyOnly)
            {
                yield return QaStep.Named("centered_play_flow").Timeout(75)
                    .When(_ => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning)
                    .Do(value => GuardChoices(value, Hierarchy(value), Time.realtimeSinceStartupAsDouble + 70, 70, 75, "hierarchy_owned_stack_cleanup"));
                yield break;
            }
            double localDeadline = 0;
            yield return QaStep.Named("local_choices").Timeout(60)
                .When(_ => {
                    if (localDeadline == 0) localDeadline = Time.realtimeSinceStartupAsDouble + 55;
                    return LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning;
                })
                .Do(value => GuardChoices(value, Local(value), localDeadline, 55, 60, "local_owned_stack_cleanup"));
            yield return QaStep.Named("saved_home_choices").Timeout(35).Do(value =>
                GuardChoices(value, SavedHomes(value), Time.realtimeSinceStartupAsDouble + 30, 30, 35, "saved_home_owned_stack_cleanup"));
            yield return QaStep.Named("create_choices").Timeout(20).Do(value =>
                GuardChoices(value, Create(value), Time.realtimeSinceStartupAsDouble + 15, 15, 20, "create_owned_stack_cleanup"));
            yield return QaStep.Named("home_play").Timeout(20).Do(value =>
                GuardChoices(value, HomePlay(value), Time.realtimeSinceStartupAsDouble + 15, 15, 20, "home_play_owned_stack_cleanup"));
            yield return QaStep.Named("customize_and_shop").Timeout(20).Do(CustomizeAndShop);
            yield return QaStep.Named("shop_tabs").Timeout(30).Do(ShopTabs);
            yield return QaStep.Named("lobby_themes").Timeout(30).Do(Themes);
            yield return QaStep.Named("settings_navigation").Timeout(30).Do(Settings);
            yield return QaStep.Named("phone_width").Timeout(30).Do(PhoneWidth);
            yield return QaStep.Named("open_wordbound").Timeout(60).Do(Online);
        }

        IEnumerator GuardChoices(QaContext context, IEnumerator body, double deadline, int workBudget, int outerBudget, string note)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            Exception failure = null;
            var cleanupErrors = new List<string>();
            try
            {
                while (stack.Count != 0 && failure == null)
                {
                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                    { failure = new QaFailException("Choice work exceeded its owned " + workBudget + "-second budget."); break; }
                    object current = null; bool next = false;
                    try
                    {
                        next = stack.Peek().MoveNext();
                        if (next) current = stack.Peek().Current;
                        else (stack.Pop() as IDisposable)?.Dispose();
                    }
                    catch (Exception error) { failure = error; }
                    if (failure != null || !next) continue;
                    if (current is IEnumerator nested) stack.Push(nested); else yield return current;
                }
            }
            finally
            {
                while (stack.Count != 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception error) { cleanupErrors.Add(error.GetType().Name); }
                context.Note(note, new Dictionary<string, object> {
                    ["errors"] = cleanupErrors.ToArray(), ["failure"] = failure?.GetType().Name ?? "none",
                    ["work_budget_seconds"] = workBudget, ["outer_budget_seconds"] = outerBudget });
            }
            if (failure != null) throw failure;
            Require(cleanupErrors.Count == 0, "Choice cleanup failed: " + string.Join(", ", cleanupErrors));
        }

        IEnumerator Local(QaContext context)
        {
            Require(Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode,
                "This scenario needs a normal Windows player window.");
            menu = LobbyMenu.Instance;
            preferences = Preference.Capture(); lastChoice = Preference.Text(LastPlayChoice.StorageKey);
            context.Note("scope", "Real Windows UI Submit callbacks and screenshots; no physical-input, matchmaking or full-match acceptance claim.");
            try
            {
                Submit(Top("PLAY"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Play, "Top PLAY did not open the real chooser.");
                Submit(Find(Page(), "Queue " + LobbyMenu.Practice));
                yield return Settle();
                Require(CategoryCards().SequenceEqual(PlayPage.FamilyOrder.Select(Category)), "Practice must show every mode category, in order.");
                Require(Label(Find(Page(), "Queue " + LobbyMenu.Practice)) == "Same screen" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)) == "Online" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) == "Create", "Play destination labels differ.");
                yield return ObserveCategories(context, "practice_families_one_seat");
                yield return RevealModes(context, "one_seat");
                yield return Learn(context);
                foreach (string mode in PartyModes)
                {
                    yield return Pick(mode);
                    Require(menu.Mode == mode && !menu.Starting && menu.Blocked == null, "Mode selection changed its launch contract: " + mode);
                    var detail = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail");
                    Require(detail.text.ToLowerInvariant().Contains("bot") == HouseModes.Find(mode).BotSeats > 0, "Incorrect bot claim: " + mode);
                    TextFits(detail);
                    var goal = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
                    Require(goal.text == LobbyPlayModeLabels.Goal(mode) && goal.transform.IsChildOf(Strip()),
                        "The selected mode must state its goal in the panel beside the tiles: " + mode);
                    TextFits(goal);
                    var access = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.AccessLine);
                    Require(access.text == ModeAccess.For(mode).Line && access.transform.IsChildOf(Strip()) &&
                        ScreenRect(access.rectTransform).yMax <= ScreenRect(goal.rectTransform).yMin + 2,
                        "The selected mode must say who can play under its goal line: " + mode);
                    Require(access.text.ToLowerInvariant().Contains("bot") == HouseModes.Find(mode).BotSeats > 0, "Incorrect bot claim in who can play: " + mode);
                    TextFits(access);
                    foreach (string id in ShownTargets()) ColumnFits(Find(Page(), id));
                    var guide = Page().GetComponentsInChildren<Button>().Where(button => button.name == PlayPage.GuideIcon).ToArray();
                    Require(guide.Length == (ModeGuides.Has(mode) ? 1 : 0), "The How to play button must show exactly when a guide exists: " + mode);
                    if (guide.Length == 1) ControlFits(guide[0]);
                    Require(!Page().GetComponentsInChildren<LobbyPlaySpare>().Any(), "Same screen leaves how to play to the preview and the guide, not a strip: " + mode);
                    var preview = Page().GetComponentInChildren<LobbyPlayModePreview>();
                    Require(preview && preview.Shown == mode, "The preview beside the modes must show the selected mode: " + mode);
                    string goalText = goal.text, accessText = access.text;
                    bool pictureShown = preview.PictureShown, guided = guide.Length == 1;
                    bool fixedPlace = HouseModes.FixedHouse(mode) != null;
                    Require(IsNext("GO") != fixedPlace, fixedPlace ? "A mode with its own house must start from the mode step: " + mode
                        : "A house mode must lead to its houses with Next: " + mode);
                    string[] maps = new string[0];
                    if (!fixedPlace)
                    {
                        yield return PlaceStep("GO", "houses of " + mode);
                        var chosen = Page().GetComponentsInChildren<RectTransform>().SingleOrDefault(rect => rect.name == PlayPage.ChosenMode);
                        Require(chosen && chosen.GetComponentsInChildren<TMP_Text>().Any(text => text.text == LobbyPlayModeLabels.Name(mode)),
                            "The house step must keep the chosen mode in view: " + mode);
                        maps = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).Select(button => button.name).ToArray();
                        Require(maps.SequenceEqual(GameConfig.Current.Houses.Keys.Select(id => "Map " + id)), "Every House mode must offer every installed house: " + mode);
                        Require(menu.Maps.Count > 0 && GameConfig.Current.Houses.Keys.All(id => Ticked(Find(Page(), "Map " + id)) == (menu.SavedHome == null && !menu.Maps.All && menu.Maps.IsTicked(id))),
                            "Each house card must show whether it is ticked, and Any house stands for every house: " + mode);
                        var anyHouse = Find(Page(), PlayPage.AnyHouse);
                        Require(anyHouse.transform.parent.name == PlayPage.MapsRow && anyHouse.GetComponentsInChildren<TMP_Text>().Single().text == PracticeMaps.Every,
                            "The Houses label must carry the Any house chip: " + mode);
                        ControlFits(anyHouse);
                        TextFits(anyHouse.GetComponentsInChildren<TMP_Text>().Single());
                        ControlFits(Find(Page(), PlayPage.BackToModes));
                        Require(!IsNext("GO"), "The house step must end in Play: " + mode);
                    }
                    else Require(!Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ") || button.name.StartsWith(PlayPage.SavedHomeCard)),
                        "A mode with its own house must not offer houses: " + mode);
                    string refusal = PlayPage.Refusal(menu);
                    Require(Find(Page(), "GO").interactable == (refusal == null) && (refusal == null || Summary() == refusal),
                        "The selected local mode cannot start, or hides why: " + mode);
                    context.Note("local_" + mode, new Dictionary<string, object> { ["summary"] = Summary(), ["goal"] = goalText, ["access"] = accessText, ["map"] = menu.Map,
                        ["map_label"] = menu.MapLabel, ["humans"] = menu.PartySize, ["selected"] = Focused(), ["maps"] = maps,
                        ["ticked"] = menu.Maps.Ticked.ToArray(), ["guide"] = guided, ["preview"] = pictureShown, ["two_steps"] = !fixedPlace });
                    if (mode is HouseModes.Dibs or HouseModes.Duos or HouseModes.Escape or HouseModes.War or HouseModes.BossFight)
                        yield return Capture(context, "lobby_local_" + mode.ToLowerInvariant());
                }
                yield return SoloFamilies(context);
            }
            finally { PreservePreferences(context); }
        }

        static string Focused() => EventSystem.current && EventSystem.current.currentSelectedGameObject ? EventSystem.current.currentSelectedGameObject.name : "";

        string Summary() => Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail").text;

        IEnumerator Learn(QaContext context)
        {
            yield return Pick(LobbyMenu.TutorialMode);
            Require(menu.Mode == LobbyMenu.TutorialMode && !menu.Starting && menu.Blocked == null, "Tutorial selection changed its launch contract.");
            Require(!Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ") || button.name.StartsWith(PlayPage.SavedHomeCard)) &&
                !Page().GetComponentsInChildren<RectTransform>().Any(rect => rect.name == PlayPage.TutorialRoom), "The tutorial has its own room, so it offers no houses.");
            Require(Find(Page(), "GO").interactable && !IsNext("GO"), "The tutorial must start from the mode step.");
            foreach (string id in ShownTargets()) ColumnFits(Find(Page(), id));
            foreach (var detail in Page().GetComponentsInChildren<TMP_Text>().Where(text => text.name == "Mode detail")) TextFits(detail);
            var start = Page().GetComponentsInChildren<TMP_Text>().Where(text => text.transform.parent.name == PlayPage.StartTag).ToArray();
            Require(start.Length == (TutorialDirector.Done ? 0 : 1), "The tutorial must carry a Start here tag until it is done.");
            foreach (var tag in start) TextFits(tag);
            context.Note("practice_learn", new Dictionary<string, object> { ["mode"] = menu.Mode, ["tutorial_done"] = TutorialDirector.Done,
                ["selected"] = EventSystem.current.currentSelectedGameObject.name, ["start_here"] = start.Length == 1,
                ["first_section"] = PlayPage.FamilyOrder.Select(HouseModes.FamilyName).First() });
            yield return Capture(context, "lobby_practice_learn");
        }

        static string Category(HouseModeFamily family) => PlayPage.CategoryCard + HouseModes.FamilyName(family);

        string[] CategoryCards() => Page().GetComponentsInChildren<RectTransform>().Where(rect => rect.name == PlayPage.CategoryRow)
            .SelectMany(row => row.GetComponentsInChildren<Button>()).Select(button => button.name).ToArray();

        IEnumerator ObserveCategories(QaContext context, string key)
        {
            bool solo = menu.Page<PlayPage>().WordboundSolo;
            string original = solo ? WordboundSoloEntry.SelectedMode : menu.Mode;
            var row = Page().GetComponentsInChildren<RectTransform>().SingleOrDefault(rect => rect.name == PlayPage.CategoryRow);
            Require(row, "Practice must lead with its row of mode categories.");
            Require(CategoryCards().SequenceEqual(PlayPage.FamilyOrder.Select(Category)),
                "Practice must show the " + string.Join(", ", PlayPage.FamilyOrder.Select(HouseModes.FamilyName)) + " categories left to right.");
            Require(!Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Group ")), "Practice still shows group tabs.");
            var scroll = row.GetComponentInParent<ScrollRect>();
            Require(scroll && scroll.vertical && !scroll.horizontal && row.parent == scroll.content && row.GetSiblingIndex() == 0 && Strip().GetSiblingIndex() == 1,
                "The categories, then the open category's modes, must lead the page's own up-and-down scroll.");
            var names = row.GetComponentsInChildren<TMP_Text>().Where(text => text.name == PlayPage.CategoryName).ToArray();
            Require(names.Select(text => text.text).SequenceEqual(PlayPage.FamilyOrder.Select(HouseModes.FamilyName)), "Each category card must carry its name.");
            foreach (var text in row.GetComponentsInChildren<TMP_Text>()) TextFits(text);
            string[] nameTexts = names.Select(text => text.text).ToArray();
            float right = float.NegativeInfinity;
            foreach (string card in CategoryCards())
            {
                var area = ScreenRect((RectTransform)Find(Page(), card).transform);
                Require(area.xMin > right, card + " must sit right of the category before it.");
                right = area.xMax;
                ControlFits(Find(Page(), card));
            }
            var categories = new List<object>();
            var closedCourses = new List<string>();
            var courses = new List<string>();
            foreach (var family in PlayPage.FamilyOrder)
            {
                Submit(Find(Page(), Category(family)));
                yield return Settle();
                var strip = Strip();
                Require(strip.GetComponentsInChildren<ScrollRect>(true).Length == 0 && strip.GetComponentsInChildren<Scrollbar>(true).Length == 0,
                    "The Practice modes must not scroll sideways or carry a scrollbar of their own.");
                var tiles = ModeTargets();
                Require(tiles.Select(button => button.name).SequenceEqual(HouseModes.ModesIn(family).Select(PlayPage.ModeButton)),
                    "The " + HouseModes.FamilyName(family) + " category must show its own modes, in order.");
                var view = ScreenRect(strip.GetComponentInParent<ScrollRect>().viewport);
                foreach (var tile in tiles)
                {
                    ColumnFits(tile);
                    Require(Inside(ScreenRect((RectTransform)tile.transform), view), "Every mode of an open category must show without scrolling: " + tile.name);
                    foreach (var text in tile.GetComponentsInChildren<TMP_Text>())
                    {
                        TextFits(text);
                        Require(text.fontSize >= SmallestModeLabel, "The mode label is too small to read: " + tile.name);
                    }
                }
                foreach (var course in tiles.Where(IsCourse))
                {
                    courses.Add(course.name);
                    var tag = course.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.transform.parent.name == PlayPage.SeatTag);
                    bool closed = course.TryGetComponent(out CanvasGroup group) && group.alpha < 1f;
                    if (closed) closedCourses.Add(course.name);
                    if (menu.PartySize == 1)
                        Require(!tag && !closed && !course.GetComponent<LobbyPlayGoalSwap>(), "Each solo course must look open while one local seat is taken: " + course.name);
                    else
                    {
                        Require(!menu.Page<PlayPage>().WordboundSolo && closed && course.IsInteractable(),
                            "Solo courses must stay in place, dimmed and focusable, while extra local seats are taken: " + course.name);
                        Require(tag && tag.text == LobbyKit.Upper(ModeAccess.OnePlayer), "A dimmed solo course must carry its 1 player tag: " + course.name);
                        Require(course.TryGetComponent(out LobbyPlayGoalSwap swap) && swap.Reason == PlayPage.SoloOnly(course.name.Substring(5)),
                            "A dimmed solo course must say why when focused: " + course.name);
                        TextFits(tag);
                    }
                }
                var bounds = ScreenRect(strip); var safe = Screen.safeArea;
                Require(bounds.xMin >= safe.xMin - 2 && bounds.xMax <= safe.xMax + 2, "The Practice modes overflow the window.");
                categories.Add(new Dictionary<string, object> { ["category"] = HouseModes.FamilyName(family), ["modes"] = tiles.Select(button => button.name).ToArray(),
                    ["step"] = new[] { bounds.x, bounds.y, bounds.width, bounds.height }, ["chosen"] = menu.Mode });
            }
            if (solo && menu.PartySize == 1) yield return Pick(original);
            else if (HouseModes.Has(original) && PlayPage.SeatOpen(original, menu.PartySize)) yield return Pick(original);
            scroll = Strip().GetComponentInParent<ScrollRect>();
            context.Note(key, new Dictionary<string, object> { ["categories"] = CategoryCards(), ["names"] = nameTexts,
                ["courses"] = courses.ToArray(), ["closed_courses"] = closedCourses.ToArray(), ["humans"] = menu.PartySize, ["steps"] = categories.ToArray(),
                ["content_height"] = scroll.content.rect.height, ["view_height"] = scroll.viewport.rect.height, ["scrolls_sideways"] = false });
        }

        static bool Inside(Rect rect, Rect view) =>
            rect.xMin >= view.xMin - 2 && rect.xMax <= view.xMax + 2 && rect.yMin >= view.yMin - 2 && rect.yMax <= view.yMax + 2;

        RectTransform Strip() => Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.ModeBoard);

        Button[] ModeTargets() => Strip().Find(PlayPage.ModeTiles).GetComponentsInChildren<Button>();

        void ColumnFits(Button button)
        {
            var rect = ScreenRect((RectTransform)button.transform);
            Require(rect.width > 1 && rect.height > 1 && Inside(rect, ScreenRect(Strip())), "A mode leaves the Practice tiles: " + button.name);
            ControlFits(button);
        }

        IEnumerator RevealModes(QaContext context, string key)
        {
            var prior = EventSystem.current.currentSelectedGameObject;
            var shown = new List<object>();
            foreach (var button in ModeTargets())
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return Settle();
                var rect = ScreenRect((RectTransform)button.transform);
                Require(Inside(rect, ScreenRect(button.GetComponentInParent<ScrollRect>().viewport)), "Focus must keep " + button.name + " fully in view.");
                ControlFits(button);
                shown.Add(new Dictionary<string, object> { ["id"] = button.name, ["rect"] = new[] { rect.x, rect.y, rect.width, rect.height } });
            }
            if (prior && prior.activeInHierarchy) EventSystem.current.SetSelectedGameObject(prior);
            yield return Settle();
            context.Note("practice_columns_focus_" + key, shown.ToArray());
        }

        static bool IsCourse(Button button) => button.name.StartsWith("Solo ") && !button.name.StartsWith("Solo map ") && button.name != "Solo GO";

        static bool Ticked(Button card) => card.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Selected");

        string[] ShownTargets() => Page().GetComponentsInChildren<Button>()
            .Where(button => button.name.StartsWith("Mode ") || IsCourse(button)).Select(button => button.name).ToArray();

        bool IsNext(string go) => Page().GetComponentsInChildren<Button>().Any(button => button.name == go &&
            button.GetComponentsInChildren<TMP_Text>().Any(text => text.text.ToUpperInvariant().Contains(PlayPage.NextWord.ToUpperInvariant())));

        IEnumerator PlaceStep(string go, string what)
        {
            yield return Shown(go, "Next to the " + what);
            Require(IsNext(go), "The mode step must lead to the " + what + " with Next.");
            Submit(Find(Page(), go));
            yield return Settle();
            yield return Shown(PlayPage.BackToModes, "the way back from the " + what);
        }

        IEnumerator ModeStep()
        {
            if (!Present(PlayPage.BackToModes)) yield break;
            Submit(Find(Page(), PlayPage.BackToModes));
            yield return Settle();
        }

        IEnumerator ShowCategory(string mode)
        {
            yield return ModeStep();
            if (Present(PlayPage.ModeButton(mode))) yield break;
            string card = Category(HouseModes.FamilyOf(mode));
            yield return Shown(card, "the " + HouseModes.FamilyName(HouseModes.FamilyOf(mode)) + " category");
            Submit(Find(Page(), card));
            yield return Settle();
        }

        IEnumerator Pick(string mode)
        {
            string id = PlayPage.ModeButton(mode);
            yield return ShowCategory(mode);
            yield return Shown(id, "the " + mode + " mode");
            Submit(Find(Page(), id));
            yield return Settle();
        }

        IEnumerator SavedHomes(QaContext context)
        {
            string queue = menu.Queue, mode = menu.Mode, map = menu.Map, home = menu.SavedHome, folder = HomeStorage.DefaultFolder;
            var play = menu.Page<PlayPage>(); bool solo = play.WordboundSolo;
            var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
            string homes = Path.Combine(Path.GetTempPath(), "wreckabulary-qa-homes-" + Guid.NewGuid().ToString("N"));
            context.Note("saved_home_scope", "A temporary home folder replaces the player's saved homes for this step; selection only, no match launch.");
            try
            {
                string cardName = PlayPage.SavedHomeCard + QaHomeMap;
                if (menu.Current != LobbyMenu.Play) { Submit(Top("PLAY")); yield return Settle(); }
                yield return Shown("Queue " + LobbyMenu.Practice, "the Practice tab");
                Submit(Find(Page(), "Queue " + LobbyMenu.Practice)); yield return Settle();
                HomeStorage.DefaultFolder = homes;
                var config = GameConfig.Current;
                Require(config != null && config.Houses != null && config.Items != null && config.HouseFor(QaHomeMap) != null && config.HouseFor(QaHomeMap).Rooms.Count > 0,
                    "The installed game data has no " + QaHomeMap + " house to build a QA home in.");
                var designer = new HomeDesigner(config.Houses, config.Items);
                var batch = designer.AddWords(designer.CreateLayout(QaHomeMap, QaHomeName), "TABLE PLANT", config.HouseFor(QaHomeMap).Rooms[0].Name);
                Require(batch.Ok, "The QA home could not be furnished: " + string.Join("; ", batch.Errors));
                Require(HomeStorage.ForGame().TrySave(batch.Layout, out string saveError), "The QA home could not be saved in its temporary folder: " + saveError);
                yield return Pick("Dibs");
                yield return PlaceStep("GO", "houses of Dibs");
                yield return Shown(cardName, "the saved-home card in the Houses grid");
                var card = Find(Page(), cardName);
                var houses = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).ToArray();
                Require(houses.Length > 0 && card.transform.parent == houses[0].transform.parent, "Saved homes must share the Houses grid.");
                var words = card.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                Require(words.Contains(QaHomeName) && words.Any(text => text.StartsWith("Your home · ")), "The saved-home card must name the home and its house.");
                yield return UniformMaps(context, "saved_home", new[] { card }, ScreenRect((RectTransform)houses[0].transform));
                Submit(Find(Page(), cardName)); yield return Settle();
                yield return Shown(cardName, "the chosen saved-home card after the page refresh");
                Require(menu.SavedHome == QaHomeMap && menu.SavedHomeName == QaHomeName && menu.Map == QaHomeMap && menu.Mode == "Dibs" &&
                    menu.Queue == LobbyMenu.Practice && Session.LobbyHome == QaHomeMap && !menu.Starting && SceneManager.GetActiveScene().name == Session.HubScene,
                    "Choosing a saved home must select it without starting a match.");
                Require(Find(Page(), "GO").interactable && Page().GetComponentsInChildren<TMP_Text>().Any(text => text.text.EndsWith(" · " + QaHomeName)),
                    "The summary must name the chosen saved home.");
                ControlFits(Find(Page(), cardName));
                yield return Capture(context, "lobby_local_saved_home");
                foreach (string playable in HomeDesigner.PlayableModes.Where(mode => mode != "Dibs"))
                {
                    yield return Pick(playable);
                    yield return PlaceStep("GO", "houses of " + playable);
                    yield return Shown(cardName, "the saved-home card in " + playable);
                    Require(menu.Mode == playable && menu.SavedHome == QaHomeMap, playable + " must keep the chosen saved home.");
                }
                string[] withheld = LobbyMenu.PracticeModes.Where(mode => !HomeDesigner.Playable(mode)).ToArray();
                foreach (string other in withheld)
                {
                    yield return Pick(other);
                    Require(menu.Mode == other, "Selecting " + other + " did not change the mode.");
                    if (IsNext("GO")) yield return PlaceStep("GO", "houses of " + other);
                    Require(menu.SavedHome == null && Session.LobbyHome == null &&
                        !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(PlayPage.SavedHomeCard)),
                        "Saved homes must not be offered for " + other + ".");
                }
                context.Note("saved_home_choices", new Dictionary<string, object> { ["offered"] = HomeDesigner.PlayableModes.ToArray(),
                    ["withheld"] = withheld, ["card"] = cardName, ["words"] = words });
            }
            finally
            {
                try
                {
                    HomeStorage.DefaultFolder = folder;
                    try { if (Directory.Exists(homes)) Directory.Delete(homes, true); }
                    catch (Exception error) { context.Note("saved_home_folder_cleanup", error.GetType().Name); }
                    if (menu)
                    {
                        menu.Choose(queue: queue, mode: mode, map: map);
                        if (home != null) menu.ChooseHome(home);
                        play.WordboundSolo = solo; menu.Open(LobbyMenu.Play);
                        bool preserved = menu.Queue == queue && menu.Mode == mode && menu.Map == map && menu.SavedHome == home &&
                            menu.Party.SequenceEqual(party) && Session.Bindings.SequenceEqual(bindings);
                        context.Note("saved_home_selection_preserved", preserved);
                        Require(preserved, "Saved-home choices changed the original selection or local devices.");
                    }
                }
                finally { PreservePreferences(context); }
            }
        }

        IEnumerator Hierarchy(QaContext context)
        {
            Require(Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode,
                "Play hierarchy needs a normal presented Windows player.");
            menu = LobbyMenu.Instance; preferences = Preference.Capture(); lastChoice = Preference.Text(LastPlayChoice.StorageKey);
            string current = menu.Current, queue = menu.Queue, mode = menu.Mode, map = menu.Map;
            string solo = WordboundSoloEntry.SelectedMode;
            var play = menu.Page<PlayPage>(); bool priorSolo = play.WordboundSolo, priorMaps = play.WordboundMaps;
            var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
            context.Note("scope", "Actual Windows UI Submit callbacks, centered hierarchy/map bounds and unchanged captures. Logical selection only; no physical keyboard/controller, online session or full-match acceptance.");
            try
            {
                Require(menu.PartySize == 1, "This bounded Play hierarchy starts with one local human.");
                Submit(Top("PLAY")); yield return Settle();
                Submit(Find(Page(), "Queue " + LobbyMenu.Practice)); yield return Settle();
                yield return Pick("Dibs");
                ObserveHierarchy(context, "practice", PlayPage.ModeBoard, ShownTargets());
                yield return ObserveCategories(context, "hierarchy_families");
                yield return PlaceStep("GO", "houses of Dibs");
                var houseMaps = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Map ")).ToArray();
                Require(houseMaps.Length > 0, "The selected house mode needs actual maps.");
                var footprint = ScreenRect((RectTransform)houseMaps[0].transform);
                yield return UniformMaps(context, "house", houseMaps, footprint);
                context.Note("house_maps", houseMaps.Select(b => b.name).ToArray());
                MapPictures(context, "house", houseMaps);
                yield return Capture(context, "play_practice_house");
                string[] timeMaps = null;
                foreach (string selected in new[] { "TimeRun", "Freestyle" })
                {
                    yield return Pick(selected);
                    ObserveHierarchy(context, selected, PlayPage.ModeBoard, ShownTargets());
                    Require(Label(Find(Page(), "Solo " + selected)) == (selected == "TimeRun" ? "Time Trial" : "Free Roam"), "Solo display name differs from the chosen direct mode.");
                    yield return PlaceStep("Solo GO", "maps of " + selected);
                    var maps = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Solo map ")).ToArray();
                    Require(maps.Length > 0, "The map step of " + selected + " shows no maps.");
                    yield return UniformMaps(context, selected, maps, ScreenRect((RectTransform)maps[0].transform));
                    MapPictures(context, selected, maps);
                    var ids = maps.Select(b => b.name).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                    Require(ids.Length == 3, "The installed solo modes must show exactly three compatible installed maps.");
                    if (timeMaps == null) timeMaps = ids; else Require(timeMaps.SequenceEqual(ids), "Time Trial and Free Roam must use the same installed map gallery.");
                    context.Note("solo_maps_" + selected, ids);
                    Require(Find(Page(), "Solo GO").interactable && WordboundSoloEntry.SelectedMode == selected, "The selected compatible solo mode is not launchable.");
                    ControlFits(Find(Page(), "Solo GO"));
                    yield return Capture(context, "play_" + selected.ToLowerInvariant());
                }
                yield return Pick(HouseModes.Escape);
                yield return PlaceStep("GO", "houses of Escape");
                var escapeMaps = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Map ")).ToArray();
                Require(escapeMaps.Select(b => b.name).SequenceEqual(GameConfig.Current.Houses.Keys.Select(id => "Map " + id)) &&
                    GameConfig.Current.Houses.ContainsKey(menu.Map), "Escape must offer the installed houses to tick like every other House mode.");
                yield return UniformMaps(context, "Escape", escapeMaps, footprint);
                context.Note("escape_ticked", menu.Maps.Ticked.ToArray());
                yield return Capture(context, "play_escape_houses");
                Submit(Find(Page(), "Queue " + LobbyMenu.Workshop)); yield return Settle();
                ObserveHierarchy(context, "create", PlayPage.CreateRow, CreateTools);
                Require(ModeCards().Length == 0 && CreateCards().SequenceEqual(CreateTools), "Create must hold only its two tools.");
                ToolCardsFit(context, "hierarchy");
                Require(Find(Page(), PlayPage.CreateEditor).interactable, "The installed map editor must remain available.");
                yield return Capture(context, "play_create_tools");
            }
            finally
            {
                try
                {
                    WordboundSoloEntry.Select(solo);
                    if (menu)
                    {
                        menu.Choose(queue: queue, mode: mode, map: map);
                        play.WordboundSolo = priorSolo; play.WordboundMaps = priorMaps; menu.Open(current);
                        bool preserved = menu.Queue == queue && menu.Mode == mode && menu.Map == map && menu.Party.SequenceEqual(party) && Session.Bindings.SequenceEqual(bindings);
                        context.Note("play_selection_preserved", preserved);
                        Require(preserved, "Play hierarchy changed the original selection or local devices.");
                    }
                }
                finally { PreservePreferences(context); }
            }
        }

        void ObserveHierarchy(QaContext context, string key, string rowName, string[] targets)
        {
            Require(Label(Find(Page(), "Queue " + LobbyMenu.Practice)) == "Same screen" &&
                Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)) == "Online" &&
                Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) == "Create", "Queue captions differ.");
            Require(!Page().GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Family ")), "An obsolete family selector is still visible.");
            var frame = Page().GetComponentInChildren<LobbyPlayPageView>(); Require(frame, "The saved Play frame is missing.");
            var rows = Page().GetComponentsInChildren<RectTransform>().Where(r => r.name == rowName).ToArray();
            Require(rows.Length == 1, "The direct mode row is missing or duplicated.");
            var row = ScreenRect(rows[0]); var queues = ScreenRect(frame.Queues); var panel = ScreenRect(frame.Panel);
            Require(row.yMax <= queues.yMin + 2, "Modes must sit below the category row.");
            if (rowName == PlayPage.ModeBoard)
                Require(Mathf.Abs(row.xMin - ScreenRect(rows[0].GetComponentInParent<ScrollRect>().viewport).xMin) <= 2,
                    "The mode columns must line up with the left edge of the map cards.");
            else Require(Mathf.Abs(row.center.x - panel.center.x) <= 12, "The Create tools are not centered in Play.");
            foreach (string target in targets)
            {
                var button = Find(Page(), target);
                if (rowName == PlayPage.ModeBoard) ColumnFits(button); else ControlFits(button);
            }
            context.Note("hierarchy_" + key, new Dictionary<string, object> {
                ["targets"] = targets, ["captions"] = targets.Select(id => Label(Find(Page(), id))).ToArray(),
                ["queue_captions"] = new[] { Label(Find(Page(), "Queue " + LobbyMenu.Practice)), Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)), Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) },
                ["selected"] = EventSystem.current.currentSelectedGameObject.name,
                ["mode_row"] = new[] { row.x, row.y, row.width, row.height },
                ["queue_row"] = new[] { queues.x, queues.y, queues.width, queues.height },
                ["panel"] = new[] { panel.x, panel.y, panel.width, panel.height }, ["obsolete_family_controls"] = 0 });
        }

        static void MapPictures(QaContext context, string key, Button[] cards)
        {
            var shown = new Dictionary<string, object>();
            foreach (var card in cards)
            {
                var parts = card.GetComponentsInChildren<RectTransform>();
                Require(!parts.Any(part => part.name == "Fitted plan" || part.name == "Storey"), "A map card still draws the floor-plan skeleton: " + card.name);
                var picture = parts.Where(part => part.name == "Map preview" || part.name == "Map fallback").ToArray();
                Require(picture.Length == 1, "Each map card shows exactly one picture or fallback: " + card.name);
                var frame = picture[0].name == "Map preview" ? (RectTransform)picture[0].parent : picture[0];
                var shape = ScreenRect(frame);
                Require(Mathf.Abs(shape.width / Mathf.Max(1f, shape.height) - 16f / 9f) <= .03f, "Map pictures keep 16:9: " + card.name);
                var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
                var name = ScreenRect(title.rectTransform);
                Require(title.transform.parent == frame && frame.Cast<Transform>().Any(part => part.name == PlayPage.NameScrim) &&
                    name.xMin >= shape.xMin - 1f && name.yMin >= shape.yMin - 1f && name.yMax <= shape.center.y + 1f,
                    "The map name must sit on the lower left of its picture over a soft gradient: " + card.name);
                foreach (var text in card.GetComponentsInChildren<TMP_Text>()) TextFits(text);
                shown[card.name] = picture[0].name == "Map preview" ? "snapshot" : "fallback";
            }
            context.Note("map_pictures_" + key, shown);
        }

        static IEnumerator UniformMaps(QaContext context, string key, Button[] buttons, Rect reference)
        {
            Require(buttons.Length > 0, "The compatible map gallery is empty.");
            var prior = EventSystem.current.currentSelectedGameObject;
            var observations = new List<object>();
            foreach (var button in buttons)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return Settle();
                ControlFits(button); var rect = ScreenRect((RectTransform)button.transform);
                Require(Mathf.Abs(rect.width - reference.width) <= 2 && Mathf.Abs(rect.height - reference.height) <= 2,
                    "Map card footprint differs across direct modes: " + button.name);
                var scroll = button.GetComponentInParent<ScrollRect>();
                var viewport = scroll && button.transform.IsChildOf(scroll.content) ? ScreenRect(scroll.viewport) : Screen.safeArea;
                Require(rect.xMin >= viewport.xMin - 2 && rect.xMax <= viewport.xMax + 2 &&
                    rect.yMin >= viewport.yMin - 2 && rect.yMax <= viewport.yMax + 2,
                    "Focused map was not revealed completely by its real gallery: " + button.name);
                Require(EventSystem.current.currentSelectedGameObject == button.gameObject, "Map focus was not retained.");
                observations.Add(new Dictionary<string, object> { ["id"] = button.name,
                    ["rect"] = new[] { rect.x, rect.y, rect.width, rect.height },
                    ["viewport"] = new[] { viewport.x, viewport.y, viewport.width, viewport.height }, ["visible_at_focus"] = true });
            }
            context.Note("map_footprints_" + key, observations.ToArray());
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
            yield return Settle();
            if (prior && prior.activeInHierarchy) EventSystem.current.SetSelectedGameObject(prior);
            yield return Settle();
        }

        IEnumerator SoloFamilies(QaContext context)
        {
            Require(menu.PartySize == 1, "The bounded solo UI check starts with one local human.");
            string queue = menu.Queue, mode = menu.Mode, map = menu.Map, solo = WordboundSoloEntry.SelectedMode;
            var originalBindings = Session.Bindings.ToArray();
            var second = new ScriptedBinding();
            try
            {
                yield return Pick("TimeRun");
                Require(CategoryCards().SequenceEqual(PlayPage.FamilyOrder.Select(Category)) && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")),
                    "Practice must keep its categories, and a course picks its map on the next step, not among the modes.");
                foreach (string selected in new[] { "TimeRun", "Freestyle" })
                {
                    yield return Pick(selected);
                    Require(WordboundSoloEntry.SelectedMode == selected && Find(Page(), "Solo GO").interactable && IsNext("Solo GO"),
                        "The selected one-human course must lead to its maps with Next.");
                    foreach (string name in ShownTargets()) ColumnFits(Find(Page(), name));
                    ControlFits(Find(Page(), "Solo GO"));
                    TextFits(Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Solo detail"));
                    var access = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.AccessLine);
                    Require(access.text == ModeAccess.For(selected).Line && access.transform.IsChildOf(Strip()),
                        "A course must say it plays alone here and with others online: " + selected);
                    TextFits(access);
                    ControlFits(Find(Page(), PlayPage.PlayOnline));
                    context.Note("solo_access_" + selected, access.text);
                    yield return Capture(context, "lobby_solo_" + selected.ToLowerInvariant());
                    yield return PlaceStep("Solo GO", "maps of " + selected);
                    var maps = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Solo map ")).Select(button => button.name).ToArray();
                    Require(maps.SequenceEqual(PlayPage.SoloMaps.Select(id => "Solo map " + id)), "A course must offer its three maps on the map step: " + selected);
                    Require(Find(Page(), "Solo GO").interactable && !IsNext("Solo GO"), "The map step must end in Play: " + selected);
                    foreach (string name in maps) ControlFits(Find(Page(), name));
                    context.Note("solo_maps_step_" + selected, maps);
                }
                yield return Pick(HouseModes.BossFight);
                Require(menu.Mode == HouseModes.BossFight && PlayPage.Refusal(menu) == null && Find(Page(), "GO").interactable && !IsNext("GO"),
                    "Boss Fight must be ready to play from the mode step with one local human.");
                Require(menu.MapLabel == GameConfig.Current.HouseFor(HouseModes.FixedHouse(HouseModes.BossFight)).Name,
                    "Boss Fight must name its own house in the summary.");
                menu.Join(second);
                yield return Settle();
                Require(menu.PartySize == 2 && ReferenceEquals(menu.Party.Single(), second), "Only the fixture-owned extra seat may be present.");
                yield return BossFightTwoSeats(context);
                Require(!Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Solo map ") || button.name == "Solo GO"),
                    "Course maps and GO must be hidden with two local humans.");
                yield return ObserveCategories(context, "practice_families_two_seats");
                menu.OpenWordboundSolo("TimeRun");
                Require(SceneManager.GetActiveScene().name == Session.HubScene && menu.PartySize == 2 && WordboundSoloEntry.TakeRequest() == null,
                    "The secondary solo launch guard lost a seat or entered a scene.");
                yield return ShowCategory("TimeRun");
                var closedCourse = Find(Page(), PlayPage.ModeButton("TimeRun"));
                EventSystem.current.SetSelectedGameObject(closedCourse.gameObject);
                yield return Settle();
                var reason = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
                Require(reason.text == PlayPage.SoloOnly("TimeRun"), "Focus on a dimmed course must say why it is closed.");
                TextFits(reason);
                context.Note("solo_two_seats_reason", reason.text);
                yield return Capture(context, "lobby_solo_two_seats");
                menu.Leave(second);
                yield return Settle();
                Require(menu.PartySize == 1 && !menu.Party.Any(binding => ReferenceEquals(binding, second)) && Session.Bindings.SequenceEqual(originalBindings),
                    "Leaving the extra seat must restore the local seats and devices.");
                yield return Pick(mode);
                Require(CategoryCards().SequenceEqual(PlayPage.FamilyOrder.Select(Category)) && menu.Queue == queue && menu.Mode == mode && menu.Map == map,
                    "Selecting the prior direct house mode must restore its exact choice.");
                context.Note("local_solo_families", "Each solo course sits in its category and picks its map on the next step; one-human GO; an owned second seat moves Boss Fight to a mode that fits, dims the courses in place with a 1 player tag and the reason on focus, and blocks launch; exact original house and bindings preserved.");
            }
            finally
            {
                if (menu && menu.Party.Any(binding => ReferenceEquals(binding, second))) menu.Leave(second);
                WordboundSoloEntry.Select(solo);
                if (menu) { menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play); }
            }
        }

        IEnumerator BossFightTwoSeats(QaContext context)
        {
            string id = PlayPage.ModeButton(HouseModes.BossFight);
            string moved = menu.Mode;
            Require(moved != HouseModes.BossFight && HouseModes.FamilyOf(moved) == HouseModes.FamilyOf(HouseModes.BossFight) && PlayPage.SeatOpen(moved, menu.PartySize),
                "A second local seat must move Boss Fight to an Arcade mode that fits: " + moved);
            yield return ShowCategory(HouseModes.BossFight);
            var tile = Find(Page(), id);
            var go = Find(Page(), "GO");
            Require(go.interactable && PlayPage.Refusal(menu) == null, "The mode picked for two local humans must be ready: " + moved);
            Require(tile.TryGetComponent(out CanvasGroup group) && group.alpha < 1f && tile.IsInteractable(),
                "With two local humans the Boss Fight tile must stay in place, dimmed and focusable.");
            var tag = tile.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.transform.parent.name == PlayPage.SeatTag);
            Require(tag && tag.text == LobbyKit.Upper(ModeAccess.OnePlayer), "The dimmed Boss Fight tile must carry its 1 player tag.");
            TextFits(tag);
            EventSystem.current.SetSelectedGameObject(tile.gameObject);
            yield return Settle();
            string tooMany = PlayPage.TooMany(HouseModes.BossFight);
            var reason = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
            Require(reason.text == tooMany, "Focus on the dimmed Boss Fight tile must say why it is closed.");
            TextFits(reason);
            context.Note("bossfight_two_seats", new Dictionary<string, object> { ["moved_to"] = moved, ["humans"] = menu.PartySize,
                ["play_enabled"] = go.interactable, ["focused_reason"] = reason.text, ["tile_alpha"] = group.alpha });
            yield return Capture(context, "lobby_bossfight_two_seats");
        }

        IEnumerator Create(QaContext context)
        {
            bool priorFamily = menu.Page<PlayPage>().WordboundMaps;
            try
            {
                Submit(Find(Page(), "Queue " + LobbyMenu.Workshop));
                yield return Settle();
                Require(ModeCards().Length == 0 && CreateCards().SequenceEqual(CreateTools),
                    "Create must expose only its two tools without duplicate mode controls.");
                Require(menu.Mode == LobbyMenu.WorkshopMode && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(PlayPage.SavedHomeCard) || button.name.StartsWith("Map ")),
                    "Create must not offer saved homes, houses or the tutorial.");
                Require(HomeDesigner.Supports(menu.Map) && !menu.Starting, "Create must stand in a house the builder supports without launching it.");
                string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
                var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
                menu.Page<PlayPage>().WordboundMaps = false; menu.Page<PlayPage>().Refresh();
                yield return Settle();
                ToolCardsFit(context, "build");
                Require(Find(Page(), "GO").interactable, "The Home builder must be ready to open.");
                ControlFits(Find(Page(), "GO"));
                yield return Capture(context, "lobby_create_build");
                menu.Page<PlayPage>().WordboundMaps = true; menu.Page<PlayPage>().Refresh();
                yield return Settle();
                Require(ModeCards().Length == 0 && CreateCards().SequenceEqual(CreateTools), "The footer change must keep both tool cards.");
                Require(menu.Queue == queue && menu.Mode == mode && menu.Map == map &&
                    menu.Party.SequenceEqual(party) && Session.Bindings.SequenceEqual(bindings), "Creator footer changed house selection or local seats.");
                foreach (string name in new[] { PlayPage.CreateHouse, PlayPage.CreateEditor, "Workshop OPEN" }) ControlFits(Find(Page(), name));
                var detail = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Workshop detail");
                Require(detail.text == PlayPage.MapEditorNote && Find(Page(), "Workshop OPEN").interactable,
                    "The installed map editor entry must be concise and available.");
                TextFits(detail);
                Require(SceneManager.GetActiveScene().name == Session.HubScene && !menu.Starting,
                    "Creator selection must not open a scene or touch workshop autosaves.");
                yield return Capture(context, "lobby_create_wordbound");
                context.Note("create_routes", "Two large tool cards, Home builder and Map editor; a click on either opens its tool (not pressed here); the footer names the tool used last; the tutorial lives under Practice > Training; no creator launch or autosave access.");
            }
            finally
            {
                try {
                    if (menu) { menu.Page<PlayPage>().WordboundMaps = priorFamily; if (menu.Current == LobbyMenu.Play) menu.Open(LobbyMenu.Play); }
                }
                finally { PreservePreferences(context); }
            }
        }

        IEnumerator HomePlay(QaContext context)
        {
            string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
            var play = menu.Page<PlayPage>();
            bool online = play.Online, solo = play.WordboundSolo, editor = play.WordboundMaps;
            var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
            context.Note("home_play_scope", "Home PLAY through real Submit callbacks; the countdown is started and cancelled in the same frame, so no match is launched.");
            try
            {
                if (menu.Current != LobbyMenu.Play) { Submit(Top("PLAY")); yield return Settle(); }
                Submit(Find(Page(), "Queue " + LobbyMenu.Workshop)); yield return Settle();
                Submit(Top("Home")); yield return Settle();
                Require(menu.LastMatch == null, "With Create used last, Home must not offer a local match.");
                HomeOffers(context, "home_play_after_create");
                Submit(Find(Page(), LobbyHomePageView.PlayButton)); yield return Settle();
                Require(menu.Current == LobbyMenu.Play && menu.Queue == LobbyMenu.Workshop && !menu.Starting,
                    "With Create used last, Home PLAY must open the Play section on that tab without starting.");
                Submit(Find(Page(), "Queue " + LobbyMenu.Practice)); yield return Settle();
                yield return Pick(HouseModes.Dibs);
                Submit(Top("Home")); yield return Settle();
                string match = LobbyMenu.ModeName(HouseModes.Dibs) + MapCardText.Separator + menu.MapLabel;
                Require(menu.LastMatch == match, "After a mode is picked, Home must offer that match: " + match);
                HomeOffers(context, "home_play_last_match");
                yield return Capture(context, "lobby_home_last_match");
                Submit(Find(Page(), LobbyHomePageView.ChangeButton)); yield return Settle();
                Require(menu.Current == LobbyMenu.Play && !menu.Starting && menu.Mode == HouseModes.Dibs, "Change must open the Play section without starting.");
                Submit(Top("Home")); yield return Settle();
                Submit(Find(Page(), LobbyHomePageView.PlayButton));
                bool started = menu.Starting;
                var focus = EventSystem.current.currentSelectedGameObject;
                bool onCancel = focus && focus.name == "CANCEL";
                if (onCancel) Submit(focus.GetComponent<Button>()); else menu.CancelStart();
                Require(started && onCancel && !menu.Starting && SceneManager.GetActiveScene().name == Session.HubScene,
                    "Home PLAY must start the named match with the same cancellable countdown as GO.");
                yield return Settle();
                Require(menu.Current == LobbyMenu.Home && menu.LastMatch == match, "Cancelling must return to Home with the match still offered.");
                context.Note("home_play_one_click", new Dictionary<string, object> { ["match"] = match, ["countdown_started"] = started,
                    ["cancel_focused"] = onCancel, ["launched"] = false });
            }
            finally
            {
                try
                {
                    if (menu)
                    {
                        if (menu.Starting) menu.CancelStart();
                        menu.Choose(queue: queue, mode: mode, map: map);
                        play.Online = online; play.WordboundSolo = solo; play.WordboundMaps = editor;
                        menu.Open(LobbyMenu.Play);
                        bool preserved = menu.Queue == queue && menu.Mode == mode && menu.Map == map &&
                            menu.Party.SequenceEqual(party) && Session.Bindings.SequenceEqual(bindings);
                        context.Note("home_play_selection_preserved", preserved);
                        Require(preserved, "The Home PLAY check changed the original selection or local devices.");
                    }
                }
                finally { PreservePreferences(context); }
            }
        }

        void HomeOffers(QaContext context, string key)
        {
            var homePlay = Find(Page(), LobbyHomePageView.PlayButton);
            ControlFits(homePlay);
            string last = menu.LastMatch;
            string[] buttons = Page().GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
            var lines = Page().GetComponentsInChildren<TMP_Text>().Where(text => text.name == LobbyHomePageView.LastLine).ToArray();
            Require(Label(homePlay) == LobbyHomePageView.PlayLabel && !buttons.Any(name => name is "GO" or "Choose mode" or "Change house" or "Wordbound"),
                "Home must start from its one PLAY button.");
            if (last == null)
                Require(buttons.SequenceEqual(new[] { LobbyHomePageView.PlayButton }) && lines.Length == 0,
                    "With no local match to start, Home must offer PLAY alone.");
            else
            {
                Require(buttons.SequenceEqual(new[] { LobbyHomePageView.PlayButton, LobbyHomePageView.ChangeButton }) && lines.Length == 1 && lines[0].text == last,
                    "Home must name the match PLAY starts and offer Change: " + last);
                var change = Find(Page(), LobbyHomePageView.ChangeButton);
                Require(Label(change) == LobbyHomePageView.ChangeLabel, "The Change button is mislabelled.");
                TextFits(lines[0]); ControlFits(change);
                var line = ScreenRect(lines[0].rectTransform); var big = ScreenRect((RectTransform)homePlay.transform); var small = ScreenRect((RectTransform)change.transform);
                Require(line.yMin >= big.yMax - 2 && small.yMax <= big.yMin + 2 && small.height < big.height,
                    "The match name must sit above PLAY and the smaller Change button under it.");
            }
            context.Note(key, new Dictionary<string, object> { ["last_match"] = last ?? "", ["buttons"] = buttons, ["queue"] = menu.Queue });
        }

        IEnumerator CustomizeAndShop(QaContext context)
        {
            try
            {
                Submit(Top("Settings"));
                yield return Settle();
                Submit(Find(Page(), "Settings tab profile"));
                yield return Settle();
                var customize = Find(Page(), "Open loadout");
                Require(Label(customize).Equals("Customize", StringComparison.OrdinalIgnoreCase), "Profile lacks the visible Customize action.");
                Submit(customize);
                yield return Settle();
                Require(menu.Current == LobbyMenu.Loadout, "Customize did not open the saved loadout.");
                var wardrobe = menu.Page<LoadoutPage>();
                string worn = menu.Outfit.Serialize();
                yield return Capture(context, "lobby_customize");
                CustomizeTitle(context);
                CustomizeRail(context, "customize_rail");
                yield return CustomizeCategory(context, "Headwear", "lobby_customize_hats");
                yield return CustomizeCategory(context, "Top", null);
                yield return CustomizeCategory(context, "Badge", null);
                Submit(Find(Page(), LoadoutPage.LooksView));
                yield return Settle();
                Require(wardrobe.ShowingLooks, "The Looks switch did not show the looks.");
                var looks = Page().GetComponentsInChildren<ScrollRect>().Single().content.GetComponentsInChildren<Button>();
                string[] shopLooks = CosmeticBundles.All.Select(look => LoadoutPage.LookPrefix + look.Id).ToArray();
                Require(shopLooks.All(wanted => looks.Any(offered => offered.name == wanted)), "Customize must offer every shop look as a tile.");
                Require(looks.Length > 0 && (looks[0].name == LoadoutPage.AddLook || looks[0].name.StartsWith(LoadoutPage.SavedPrefix)),
                    "Customize must list the player's own looks before the shop looks.");
                Require(Page().GetComponentsInChildren<TMP_Text>().Any(text => text.name == LoadoutPage.ShopLooksTitle), "The shop looks need their own label.");
                foreach (var tile in looks.Where(offered => offered.name.StartsWith(LoadoutPage.LookPrefix)))
                    Require(tile.GetComponentsInChildren<RawImage>().Any(image => image.name == LoadoutPage.LookPicture && image.texture) ||
                        tile.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == LoadoutPage.LookFallback), "The look tile has no picture: " + tile.name);
                context.Note("customize_looks", new Dictionary<string, object> { ["tiles"] = looks.Select(offered => offered.name).ToArray(), ["saved"] = wardrobe.Looks.Count });
                yield return Capture(context, "lobby_customize_looks");
                Submit(Find(Page(), LoadoutPage.PiecesView));
                yield return Settle();
                Submit(Find(Page(), "RECIPES"));
                yield return Settle();
                yield return Capture(context, "lobby_customize_equipment");
                CustomizeEquipment(context);
                var wardrobeScroll = Page().GetComponentsInChildren<ScrollRect>().Single();
                float originalScroll = wardrobeScroll.verticalNormalizedPosition;
                var priorFocus = EventSystem.current.currentSelectedGameObject;
                LobbyPress pointed = null;
                try
                {
                    var finishes = CustomizeFinishes();
                    EventSystem.current.SetSelectedGameObject(finishes[0].gameObject);
                    LobbyScrollFollow.Show(wardrobeScroll, (RectTransform)finishes[0].transform.parent);
                    yield return Settle();
                    var viewport = ScreenRect(wardrobeScroll.viewport);
                    var shown = new List<object>();
                    foreach (var finish in finishes)
                    {
                        var rect = ScreenRect((RectTransform)finish.transform);
                        Require(rect.yMin >= viewport.yMin - 2 && rect.yMax <= viewport.yMax + 2 && rect.xMin >= viewport.xMin - 2 && rect.xMax <= viewport.xMax + 2,
                            "The Customize finish is not scrolled fully into view: " + finish.name);
                        string name = Finishes.Find(finish.name.Substring("Finish ".Length))?.Name;
                        var title = finish.GetComponentsInChildren<TMP_Text>().Where(text => text.text == name).ToArray();
                        Require(name != null && title.Length == 1, "The Customize finish card must show its finish name: " + finish.name);
                        TextFits(title[0]);
                        shown.Add(new Dictionary<string, object> { ["id"] = finish.name, ["name"] = name, ["rect"] = new[] { rect.x, rect.y, rect.width, rect.height } });
                    }
                    context.Note("customize_finishes_in_view", new Dictionary<string, object> { ["finishes"] = shown.ToArray(),
                        ["viewport"] = new[] { viewport.x, viewport.y, viewport.width, viewport.height }, ["scroll"] = wardrobeScroll.verticalNormalizedPosition });
                    var other = finishes.FirstOrDefault(finish => !finish.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == LoadoutPage.CheckMark));
                    pointed = other ? other.GetComponent<LobbyPress>() : null;
                    if (pointed) pointed.OnPointerEnter(new PointerEventData(EventSystem.current));
                    yield return Settle();
                    context.Note("customize_finish_held", new Dictionary<string, object> { ["finish"] = other ? other.name : "", ["holding"] = menu.Stage.IsHoldingItem });
                    yield return Capture(context, "lobby_customize_finishes");
                }
                finally
                {
                    if (pointed) pointed.OnPointerExit(new PointerEventData(EventSystem.current));
                    if (wardrobeScroll) wardrobeScroll.verticalNormalizedPosition = originalScroll;
                    if (priorFocus && priorFocus.activeInHierarchy) EventSystem.current.SetSelectedGameObject(priorFocus);
                }
                Submit(Find(Page(), "LOCKER"));
                yield return Settle();
                Require(menu.Outfit.Serialize() == worn && wardrobe.TryOn == null, "Browsing Customize changed or tried on an outfit.");
                Submit(Top("Shop"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Shop, "Shop navigation did not open Shop.");
                LooseLetters(context);
                yield return Capture(context, "lobby_shop");
                context.Note("customization_scope", "Navigation only; no purchase, outfit change or unlock mutation.");
            }
            finally { PreservePreferences(context); }
        }

        void CustomizeTitle(QaContext context)
        {
            var word = Page().GetComponentsInChildren<LobbyTileWord>().FirstOrDefault(tiles => tiles.Within);
            Require(word, "The Customize title must be letter tiles kept to their own area.");
            var panel = (RectTransform)word.Within.parent;
            var tabs = ScreenRect((RectTransform)panel.Cast<Transform>().Single(child => child.name == "Tabs"));
            var area = ScreenRect(word.Within);
            var spots = new List<object>();
            foreach (Transform child in word.transform)
            {
                var rect = ScreenRect((RectTransform)child);
                Require(rect.xMax <= tabs.xMin + 2, "A Customize title tile covers the tabs: " + child.name);
                Require(rect.yMin >= area.yMin - 2 && rect.yMax <= area.yMax + 2, "A Customize title tile left its title area: " + child.name);
                spots.Add(new[] { rect.x, rect.y, rect.width, rect.height });
            }
            context.Note("customize_title", new Dictionary<string, object> { ["word"] = word.Word, ["settled"] = word.Settled,
                ["tabs"] = new[] { tabs.x, tabs.y, tabs.width, tabs.height }, ["tiles"] = spots.ToArray() });
        }

        void CustomizeEquipment(QaContext context)
        {
            var scroll = Page().GetComponentsInChildren<ScrollRect>().Single();
            var view = ScreenRect(scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform);
            var recipes = Page().GetComponentsInChildren<LobbyRecipeTileView>().Select(tile => ScreenRect((RectTransform)tile.transform)).ToArray();
            int whole = recipes.Count(rect => rect.yMin >= view.yMin - 2 && rect.yMax <= view.yMax + 2);
            var detail = Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Recipe detail");
            Require(detail.rect.height <= LoadoutPage.DetailCard + 2, "The Customize recipe card must be one line.");
            TextFits(detail.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Blurb"));
            context.Note("customize_equipment", new Dictionary<string, object> { ["recipes_in_view"] = whole, ["recipes"] = recipes.Length,
                ["detail_height"] = detail.rect.height });
        }

        void LooseLetters(QaContext context)
        {
            var pages = menu.Page<ShopPage>().Root.parent;
            var layer = pages.parent.Find(LobbyLooseTiles.Name);
            Require(!layer || layer.GetSiblingIndex() < pages.GetSiblingIndex(), "Loose title letters must fall behind the open page.");
            context.Note("loose_letters", new Dictionary<string, object> { ["layer"] = layer ? layer.GetSiblingIndex() : -1, ["pages"] = pages.GetSiblingIndex(),
                ["falling"] = layer ? layer.GetComponent<LobbyLooseTiles>().Count : 0 });
        }

        void CustomizeRail(QaContext context, string key)
        {
            var rail = Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == LoadoutPage.RailName);
            var categories = rail.GetComponentsInChildren<Button>();
            string[] expected = LoadoutPage.RailOrder.Select(slot => LoadoutPage.CategoryPrefix + slot).ToArray();
            Require(categories.Select(button => button.name).SequenceEqual(expected), "Customize must list its ten categories in order: " + string.Join(", ", expected));
            var panel = ScreenRect(Page().GetComponentsInChildren<RectTransform>().First(rect => rect.name == "Panel"));
            var area = ScreenRect(rail);
            foreach (var category in categories)
            {
                ControlFits(category);
                var rect = ScreenRect((RectTransform)category.transform);
                Require(rect.xMax <= panel.xMin + 2, "The Customize category must sit left of the panel: " + category.name);
                Require(!category.GetComponentsInChildren<TMP_Text>().Any(), "The Customize category must be an icon, not a word: " + category.name);
                Require(category.GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("Icon ") && image.sprite), "The Customize category has no icon: " + category.name);
            }
            context.Note(key, new Dictionary<string, object> { ["categories"] = expected, ["rect"] = new[] { area.x, area.y, area.width, area.height },
                ["panel_left"] = panel.xMin, ["selected"] = menu.Page<LoadoutPage>().Category });
        }

        IEnumerator CustomizeCategory(QaContext context, string slot, string capture)
        {
            Submit(Find(Page(), LoadoutPage.CategoryPrefix + slot));
            yield return Settle();
            Require(menu.Page<LoadoutPage>().Category == slot, "The Customize category did not open: " + slot);
            var scroll = Page().GetComponentsInChildren<ScrollRect>().Single();
            var viewport = ScreenRect(scroll.viewport);
            var tiles = scroll.content.GetComponentsInChildren<Button>();
            string[] expected = Dressing.Choices(GameConfig.Current.Wardrobe, slot).Select(choice => LoadoutPage.TileName(slot, choice)).ToArray();
            Require(tiles.Select(offered => offered.name).SequenceEqual(expected), "Customize must offer every " + slot + " choice as a tile: " + string.Join(", ", expected));
            int pictures = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                var rect = ScreenRect((RectTransform)tile.transform);
                Require(rect.xMin >= viewport.xMin - 2 && rect.xMax <= viewport.xMax + 2, "The Customize tile is wider than its grid: " + tile.name);
                Require(Mathf.Abs(rect.width - rect.height) <= 2, "The Customize tile must be square: " + tile.name);
                Require(i >= LoadoutPage.Columns || rect.yMin >= viewport.yMin - 2 && rect.yMax <= viewport.yMax + 2, "The first Customize row is cut off: " + tile.name);
                bool picture = tile.GetComponentsInChildren<Image>().Any(image => image.name == LoadoutPage.PiecePicture && image.sprite);
                Require(picture || tile.name == LoadoutPage.NoPrefix + slot, "The Customize tile shows no picture: " + tile.name);
                Require(!picture || !tile.GetComponentsInChildren<TMP_Text>().Any(text => text.transform.parent.name != LoadoutPage.PricePill),
                    "The Customize tile must be a picture, not a word: " + tile.name);
                if (picture) pictures++;
            }
            context.Note("customize_" + slot.ToLowerInvariant(), new Dictionary<string, object> { ["tiles"] = expected, ["pictures"] = pictures,
                ["viewport"] = new[] { viewport.x, viewport.y, viewport.width, viewport.height },
                ["scrolls"] = scroll.content.rect.height > scroll.viewport.rect.height + 1f });
            if (capture != null) yield return Capture(context, capture);
        }

        Button[] CustomizeFinishes()
        {
            var finishes = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Finish ")).ToArray();
            string[] expected = Finishes.All.Where(finish => finish.InCustomize).Select(finish => "Finish " + finish.Id).ToArray();
            Require(finishes.Select(button => button.name).SequenceEqual(expected), "Customize must list the registry finishes in order: " + string.Join(", ", expected));
            return finishes;
        }

        IEnumerator ShopTabs(QaContext context)
        {
            var shop = menu.Page<ShopPage>();
            string category = shop.Category;
            string outfit = menu.Outfit.Serialize();
            int coins = menu.Career.Coins;
            try
            {
                if (menu.Current != LobbyMenu.Shop) { Submit(Top("Shop")); yield return Settle(); }
                Require(menu.Current == LobbyMenu.Shop, "Shop navigation did not open Shop.");
                foreach (string tab in new[] { ShopPage.Looks, ShopPage.Gear, ShopPage.Colours, ShopPage.Rooms, ShopPage.Extras })
                {
                    Submit(Find(Page(), "Shop category " + tab));
                    yield return Settle();
                    Require(shop.Category == tab, "The Shop tab did not open: " + tab);
                    context.Note("shop_" + tab + "_cards", ShopCards(tab));
                    yield return Capture(context, "lobby_shop_" + tab);
                }
                Require(menu.Outfit.Serialize() == outfit && menu.Career.Coins == coins, "Browsing Shop tabs changed the outfit or coins.");
                context.Note("shop_tabs_scope", "Every Shop tab opened through its real tab button; preview only, no purchase or equip.");
            }
            finally
            {
                try { if (menu && shop.Category != category) shop.ShowCategory(category); }
                finally { PreservePreferences(context); }
            }
        }

        Dictionary<string, object> ShopCards(string tab)
        {
            var names = new List<string>(); var tags = new List<string>();
            if (tab == ShopPage.Extras)
            {
                var cards = Page().GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Accessory ") && !rect.GetComponent<Button>()).ToArray();
                Require(cards.Length > 0, "Extras must show accessory cards.");
                foreach (var card in cards)
                {
                    var title = card.GetComponentsInChildren<TMP_Text>().Where(text => text.name == LobbyPage.CardName).ToArray();
                    Require(title.Length == 1 && title[0].text.Length > 0, "Each Extras card shows its name: " + card.name);
                    TextFits(title[0]); names.Add(title[0].text);
                }
                return new Dictionary<string, object> { ["names"] = names.ToArray() };
            }
            var offers = Page().GetComponentsInChildren<LobbyShopOfferView>();
            Require(offers.Length > 0, "The Shop tab shows no cards: " + tab);
            foreach (var offer in offers)
            {
                Require(!string.IsNullOrWhiteSpace(offer.Title.text) && offer.Title.gameObject.activeInHierarchy && offer.Title.enabled,
                    "Each Shop card shows its name on the card: " + offer.name);
                TextFits(offer.Title); names.Add(offer.Title.text);
                if (tab != ShopPage.Looks && tab != ShopPage.Gear) continue;
                var line = offer.GetComponentsInChildren<TMP_Text>().Where(text => text.name == LobbyPage.TagLine).ToArray();
                Require(line.Length == 1 && line[0].text.Length > 0, "Looks and Gear cards carry a name tag line: " + offer.name);
                TextFits(line[0]); tags.Add(line[0].text);
            }
            return new Dictionary<string, object> { ["names"] = names.ToArray(), ["tags"] = tags.ToArray() };
        }

        IEnumerator Themes(QaContext context)
        {
            string saved = LobbyThemes.Current.Id;
            var previewed = new List<string>();
            try
            {
                if (menu.Current != LobbyMenu.Home) { Submit(Top("Home")); yield return Settle(); }
                Require(menu.Current == LobbyMenu.Home, "Home navigation did not open Home.");
                HomeOffers(context, "home_play_themes");
                foreach (var theme in LobbyThemes.All)
                {
                    Require(LobbyThemes.Preview(theme.Id) && LobbyThemes.Current == theme, "The lobby theme could not be previewed: " + theme.Id);
                    yield return Settle();
                    yield return Capture(context, "lobby_home_theme_" + theme.Id);
                    previewed.Add(theme.Id);
                }
            }
            finally
            {
                LobbyThemes.Restore();
                bool restored = LobbyThemes.Current.Id == saved;
                context.Note("lobby_themes_previewed", new Dictionary<string, object> { ["themes"] = previewed.ToArray(), ["saved"] = saved, ["restored"] = restored });
                try { Require(restored, "The saved lobby theme did not come back after the previews."); }
                finally { PreservePreferences(context); }
            }
        }

        IEnumerator PhoneWidth(QaContext context)
        {
            const int Width = 1248, Height = 576;
            int width = Screen.width, height = Screen.height;
            var mode = Screen.fullScreenMode;
            try
            {
                Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
                double until = Time.realtimeSinceStartupAsDouble + 4;
                while ((Screen.width != Width || Screen.height != Height) && Time.realtimeSinceStartupAsDouble < until) yield return null;
                if (Screen.width != Width || Screen.height != Height)
                    context.Note("phone_width", new Dictionary<string, object> { ["skipped"] = "The player window did not resize.", ["size"] = new[] { Screen.width, Screen.height } });
                else
                {
                    yield return Settle();
                    Submit(Top("Shop")); yield return Settle(); yield return Settle();
                    Require(menu.Current == LobbyMenu.Shop, "Shop navigation did not open Shop at phone width.");
                    context.Note("phone_shop_cards", ShopCards(menu.Page<ShopPage>().Category));
                    yield return Capture(context, "lobby_shop_phone");
                    Submit(Top("LOADOUT")); yield return Settle(); yield return Settle();
                    Require(menu.Current == LobbyMenu.Loadout, "Customize did not open at phone width.");
                    CustomizeRail(context, "customize_rail_phone");
                    yield return CustomizeCategory(context, "Headwear", "lobby_customize_phone");
                    Submit(Top("Home")); yield return Settle();
                    context.Note("phone_width", new Dictionary<string, object> { ["size"] = new[] { Width, Height }, ["aspect"] = "19.5:9 landscape window",
                        ["scope"] = "Windowed size only; no phone DPI, touch input or safe-area emulation." });
                }
            }
            finally
            {
                if (Screen.width != width || Screen.height != height) Screen.SetResolution(width, height, mode);
                PreservePreferences(context);
            }
            double back = Time.realtimeSinceStartupAsDouble + 4;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartupAsDouble < back) yield return null;
            yield return Settle();
            Require(Screen.width == width && Screen.height == height, "The player window did not return to its size after the phone captures.");
        }

        void GameplayRows(QaContext context)
        {
            var list = Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Tab content").GetComponentInChildren<ScrollRect>().content;
            string[] rows = list.Cast<Transform>().Select(row => row.name).ToArray();
            Require(rows.SequenceEqual(SettingsPage.GameplayRows), "Gameplay settings differ: " + string.Join(", ", rows));
            var controls = SettingsPage.ShakeChoices.Select(choice => "Screen shake " + choice.id)
                .Concat(new[] { "Damage numbers", "Effect words", "Furniture names", "Control tips" }.SelectMany(name => new[] { name + " on", name + " off" })).ToArray();
            foreach (string name in controls) Require(Find(Page(), name).IsInteractable(), "Gameplay control is unavailable: " + name);
            context.Note("settings_gameplay", new Dictionary<string, object> { ["rows"] = rows, ["shake"] = ScreenShake.Level.ToString(),
                ["damage_numbers"] = GameplayPrefs.DamageNumbers, ["effect_words"] = GameplayPrefs.EffectWords,
                ["furniture_names"] = GameplayPrefs.FurnitureNames, ["control_tips"] = GameplayPrefs.ControlTips });
        }

        IEnumerator Settings(QaContext context)
        {
            var keys = DesktopBinding.Shared;
            var actionMap = keys.Map;
            string overrides = actionMap.SaveBindingOverridesAsJson();
            int bindingVersion = KeyBindings.Version;
            float sensitivity = KeyBindings.Sensitivity;
            bool invert = KeyBindings.InvertY, mapEnabled = actionMap.enabled;
            bool restoredHouseLegend = false;
            try
            {
                Submit(Top("Settings"));
                yield return Settle();
                var names = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Settings tab "))
                    .Select(button => button.name.Substring("Settings tab ".Length)).ToArray();
                Require(names.SequenceEqual(SettingsTabs), "Settings must expose the six actual categories.");
                foreach (string tab in SettingsTabs)
                {
                    Submit(Find(Page(), "Settings tab " + tab));
                    yield return Settle();
                    Require(KeyBindings.GameplayBlocked == (tab == "controls"), "Controls input lease differs after switching to " + tab);
                    Require(EventSystem.current.currentSelectedGameObject == Find(Page(), "Settings tab " + tab).gameObject,
                        "Tab selection lost keyboard/controller focus: " + tab);
                    foreach (string id in SettingsTabs) ControlFits(Find(Page(), "Settings tab " + id));
                    if (tab == "gameplay") GameplayRows(context);
                    yield return Capture(context, "lobby_settings_" + tab);
                    if (tab == "controls")
                    {
                        var list = Page().GetComponentInChildren<ScrollRect>();
                        Require(list && list.verticalScrollbar && list.verticalScrollbar.gameObject.activeInHierarchy,
                            "Long controls must expose a visible scrollbar.");
                        list.verticalScrollbar.value = 0f;
                        yield return Settle();
                        Require(list.verticalNormalizedPosition <= .001f, "Controls scrollbar did not reach the final bindings.");
                        yield return Capture(context, "lobby_settings_controls_bottom");
                        yield return WordboundControls(context, list);
                        restoredHouseLegend = true;
                    }
                }
                Submit(Find(Page(), "Close page"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Home && !KeyBindings.GameplayBlocked, "Closing Settings did not release input on Home.");
                Require(EventSystem.current.currentSelectedGameObject == Top("Settings").gameObject, "Back did not return focus to the Settings opener.");
                yield return Capture(context, "lobby_home_returned");
                context.Note("settings_scope", "Six tabs, actual House/Wordbound legend Submit, Controls lease, close and opener focus; no binding, graphics, display, volume or profile value changed.");
            }
            finally
            {
                bool sourceSame = ReferenceEquals(keys, DesktopBinding.Shared), mapSame = ReferenceEquals(actionMap, DesktopBinding.Shared.Map);
                bool overridesSame = overrides == actionMap.SaveBindingOverridesAsJson(), versionSame = bindingVersion == KeyBindings.Version;
                bool mouseSame = sensitivity == KeyBindings.Sensitivity && invert == KeyBindings.InvertY;
                bool enabledSame = mapEnabled == actionMap.enabled;
                context.Note("controls_legend_bindings_preserved", new Dictionary<string, object> {
                    ["shared_source_same"] = sourceSame, ["shared_map_same"] = mapSame, ["overrides_unchanged"] = overridesSame,
                    ["version_unchanged"] = versionSame, ["before_version"] = bindingVersion, ["after_version"] = KeyBindings.Version,
                    ["mouse_settings_unchanged"] = mouseSame, ["map_enabled_unchanged"] = enabledSame,
                    ["listening"] = KeyBindings.Listening, ["restored_house_legend"] = restoredHouseLegend });
                try { Require(sourceSame && mapSame && overridesSame && versionSame && mouseSame && enabledSame && !KeyBindings.Listening,
                    "Control legend navigation changed shared input state."); }
                finally { PreservePreferences(context); }
            }
        }

        IEnumerator WordboundControls(QaContext context, ScrollRect list)
        {
            var panel = Page().GetComponentInChildren<ControlsPanel>();
            Require(panel && !panel.IsListening, "The real controls panel is unavailable or rebinding.");
            list.verticalScrollbar.value = 1f;
            yield return Settle();
            var wordbound = Find(panel.transform, "Wordbound controls");
            ControlFits(wordbound);
            Submit(wordbound);
            yield return Settle();
            Require(EventSystem.current.currentSelectedGameObject == Find(panel.transform, "Wordbound controls").gameObject,
                "Wordbound legend Submit did not retain selector focus.");
            Require(!panel.GetComponentsInChildren<Button>(true).Single(button => button.name == "Rebind Dodge").gameObject.activeInHierarchy &&
                panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Rebind ")) == 16,
                "Wordbound must retain both numbered equipment bindings and hide only Dodge.");
            var craft = BindingButton(panel, "Spell");
            EventSystem.current.SetSelectedGameObject(craft.gameObject);
            LobbyScrollFollow.Show(list, (RectTransform)craft.transform.parent.parent);
            yield return Settle();
            context.Note("controls_legend_Wordbound_mid", LegendView(panel, list, "lobby_settings_wordbound_controls_mid",
                new[] { "Aim", "Jump", "Interact", "Spell" }, new[] { "Aim (hold)", "Jump", "Pick up, use, hold to revive", "Spell a word" },
                new[] { "RB / R1", "A / Cross", "RT / R2", "Y / Triangle" }));
            yield return Capture(context, "lobby_settings_wordbound_controls_mid");
            EventSystem.current.SetSelectedGameObject(BindingButton(panel, "Pause").gameObject);
            list.verticalScrollbar.value = 0f;
            yield return Settle();
            Require(list.verticalNormalizedPosition <= .001f, "Wordbound controls did not reach the final bindings.");
            context.Note("controls_legend_Wordbound_bottom", LegendView(panel, list, "lobby_settings_wordbound_controls_bottom",
                new[] { "Hand 1", "Hand 2", "Bag", "Pause" }, new[] { "Hand 1", "Hand 2", "Bag", "Menu" },
                new[] { "D-pad left", "D-pad right", "R3 cycles gear", "Start / Options" }));
            yield return Capture(context, "lobby_settings_wordbound_controls_bottom");
            list.verticalScrollbar.value = 1f;
            yield return Settle();
            var house = Find(panel.transform, "House controls");
            ControlFits(house);
            Submit(house);
            yield return Settle();
            Require(panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Rebind ")) == 17 &&
                panel.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Controller Pause").text == "View / Share" &&
                EventSystem.current.currentSelectedGameObject == house.gameObject && !panel.IsListening,
                "House legend and selector focus were not restored.");
        }

        static Button BindingButton(ControlsPanel panel, string action)
        {
            var row = KeyBindings.Rows(DesktopBinding.Shared).Single(value => value.action.name == action);
            return Find(panel.transform, "Rebind " + row.label);
        }

        static Dictionary<string, object> LegendView(ControlsPanel panel, ScrollRect list, string capture,
            string[] actions, string[] labels, string[] hints)
        {
            Require(!panel.IsListening && KeyBindings.GameplayBlocked && DesktopBinding.Shared.Map.enabled,
                "Legend capture must preserve the controls lease without starting a rebind.");
            var viewport = ScreenRect(list.viewport);
            var header = panel.GetComponentsInChildren<TMP_Text>().Single(value => value.name == "Control legend title");
            Require(header.text == ControlsPanel.CourseTitle, "The scrolled capture needs its visible Courses header.");
            TextFits(header);
            var headerRect = ScreenRect(header.rectTransform); var safe = Screen.safeArea;
            Require(headerRect.width > 1 && headerRect.height > 1 && headerRect.xMin >= safe.xMin - 2 &&
                headerRect.xMax <= safe.xMax + 2 && headerRect.yMin >= viewport.yMax - 2 && headerRect.yMax <= safe.yMax + 2,
                "The fixed Wordbound header is clipped or overlaps the scrolling viewport.");
            var rows = new List<Dictionary<string, object>>();
            for (int index = 0; index < actions.Length; index++)
            {
                var button = BindingButton(panel, actions[index]);
                var row = (RectTransform)button.transform.parent.parent;
                var text = row.GetComponentInChildren<TMP_Text>();
                var legend = row.GetComponentsInChildren<TMP_Text>().Single(value => value.name == "Controller " + actions[index]);
                Require(text.text == labels[index] && legend.text == hints[index], "Incorrect Wordbound control label: " + actions[index]);
                var bounds = ScreenRect(row);
                Require(bounds.xMin >= viewport.xMin - 2 && bounds.xMax <= viewport.xMax + 2 &&
                    bounds.yMin >= viewport.yMin - 2 && bounds.yMax <= viewport.yMax + 2, "Required legend row is clipped: " + actions[index]);
                ControlFits(button);
                foreach (var value in row.GetComponentsInChildren<TMP_Text>()) TextFits(value);
                rows.Add(new Dictionary<string, object> { ["action"] = actions[index], ["label"] = text.text,
                    ["controller"] = legend.text, ["fully_visible"] = true });
            }
            return new Dictionary<string, object> { ["family"] = "Wordbound", ["capture"] = capture, ["header_text"] = header.text,
                ["selected_control"] = EventSystem.current.currentSelectedGameObject.name,
                ["active_binding_count"] = panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Rebind ")),
                ["rows"] = rows.ToArray(), ["listening"] = panel.IsListening, ["input_lease_active"] = KeyBindings.GameplayBlocked };
        }

        IEnumerator LobbyPartyStates(QaContext context)
        {
            var party = new QaLobbyParty();
            var previous = WordboundLobbyParty.Register(() => party);
            try
            {
                var choices = new Wreckabulary.Rules.Wordbound.QuickPlayChoices(PlayerPrefs.GetString(Wreckabulary.Rules.Wordbound.QuickPlayChoices.StorageKey, ""));
                party.Status = choices.PartyLine(4);
                Require(ReferenceEquals(WordboundLobbyParty.Open(), party), "The lobby party strip could not be shown.");
                yield return Settle();
                var heading = Page().GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == PlayPage.PartyHeading);
                var plates = Page().GetComponentsInChildren<LobbyPartyChips>().SingleOrDefault(row => row.name == PlayPage.PartyPlatesRow);
                var copy = Find(Page(), PlayPage.CopyPartyCode);
                var leave = Find(Page(), PlayPage.LeaveParty);
                var go = Find(Page(), PlayPage.FindMatch);
                var title = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Matchmaking playlists");
                string[] names = plates ? plates.Plates.Select(plate => plate.Name).ToArray() : new string[0];
                Require(heading && heading.text == party.Heading && names.SequenceEqual(new[] { "You", "Player 2", "Player 3", "Player 4" }) &&
                    copy.IsInteractable() && leave.IsInteractable() && go.IsInteractable(),
                    "The party strip must show the party as four plates, Copy code and Leave, with Find match ready.");
                Require(!Page().GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.PartyMembers), "The party must not also be a grey sentence of names.");
                foreach (Transform plate in plates.transform)
                {
                    float height = ((RectTransform)plate).rect.height;
                    Require(height >= LobbyPartyChips.Height - 2, "A party plate is too short: " + plate.name + " " + height.ToString("F1"));
                    foreach (var text in plate.GetComponentsInChildren<TMP_Text>()) { TextFits(text); Require(text.fontSize >= SmallestModeLabel, "A party plate word is too small: " + text.text); }
                }
                Require(!Page().GetComponentsInChildren<TMP_InputField>().Any(), "The code box must step aside while in a party.");
                Require(CardDetail("ffa") == PlayPageText.SkipLine(PlayPageText.SoloOnly) &&
                    Find(Page(), PlayPage.PlaylistCard + "ffa").TryGetComponent(out CanvasGroup skipped) && skipped.alpha < 1f,
                    "A card the party cannot play must be dimmed and say why.");
                var duo = Find(Page(), PlayPage.TeamSizeSwitch + " " + Wreckabulary.Rules.Wordbound.QuickPlayCards.Duo);
                var squad = Find(Page(), PlayPage.TeamSizeSwitch + " " + Wreckabulary.Rules.Wordbound.QuickPlayCards.Squad);
                var why = Page().GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == PlayPage.TeamSizeNote);
                Require(!duo.interactable && squad.interactable && Underlined(squad) && why && why.text == PlayPage.PartyPlaysSquad(4),
                    "A party of four must see 4v4 picked, 2v2 disabled and a short reason.");
                TextFits(why);
                Require(CardDetail("relay") == "Teams of 4 race to the delivery" && !title.text.Contains("2v2"),
                    "A party of four must read the 4v4 captions and summary.");
                string region = Wreckabulary.Rules.Wordbound.QuickPlayRegions.Name(Wreckabulary.Rules.Wordbound.QuickPlayRegions.Nearest(Wreckabulary.Rules.Wordbound.QuickPlayRegions.LocalOffsetMinutes()));
                string partyLine = PlayPageText.Playable(choices, 4).Count > 0 ? PlayPageText.PartyLine(4, region) : party.Status;
                Require(Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Matchmaking detail").text == partyLine &&
                    title.text == PlayPageText.SearchLine(choices, 4),
                    "The footer must show the short party line and count only what the party can play.");
                foreach (var text in new[] { copy, leave, go }.SelectMany(button => button.GetComponentsInChildren<TMP_Text>())) TextFits(text);
                var scroll = copy.GetComponentInParent<ScrollRect>();
                var view = scroll ? ScreenRect(scroll.viewport) : Screen.safeArea;
                foreach (var shown in new[] { heading.rectTransform, (RectTransform)plates.transform, (RectTransform)copy.transform, (RectTransform)leave.transform })
                {
                    var area = ScreenRect(shown);
                    Require(area.yMin >= view.yMin - 2 && area.yMax <= view.yMax + 2, "The party strip hides " + shown.name + " below the fold.");
                }
                yield return Capture(context, "lobby_party");
                party.Searching = true; party.CanCancel = true; party.SearchTitle = "Searching 0:42";
                yield return Settle();
                Require(title.text == party.SearchTitle && Label(go) == LobbyKit.Upper(LobbyPartyStrip.CancelWord) && go.IsInteractable(),
                    "The search bar must show the timer and Cancel.");
                TextFits(title);
                var goArea = ScreenRect((RectTransform)go.transform);
                var titleArea = ScreenRect(title.rectTransform);
                Require(goArea.yMin >= Screen.safeArea.yMin - 2 && goArea.yMax <= Screen.safeArea.yMax + 2 &&
                    titleArea.yMin >= Screen.safeArea.yMin - 2 && titleArea.yMax <= Screen.safeArea.yMax + 2, "The search bar is off screen.");
                var band = Page().GetComponentsInChildren<LobbyMatchBand>(true).Single();
                Require(band.gameObject.activeInHierarchy && !band.Found && band.Plates.Plates.Count == 4 &&
                    band.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LobbyMatchBand.TitleName).text == LobbyKit.Upper(party.SearchTitle),
                    "The search must cover the cards with the timer and the party plates.");
                Require(!Find(Page(), PlayPage.PlaylistCard + "relay").IsInteractable(), "The covered cards must not take input mid-search.");
                foreach (var text in band.GetComponentsInChildren<TMP_Text>()) TextFits(text);
                yield return Capture(context, "lobby_party_search");
                int cues = LobbyMatchMoment.Cues;
                party.Found = true; party.SearchTitle = "Match found · starting in 4";
                yield return Settle();
                double landBy = Time.realtimeSinceStartupAsDouble + 2;
                while (!band.Landed && Time.realtimeSinceStartupAsDouble < landBy) yield return null;
                yield return Settle();
                Require(title.text == party.SearchTitle, "The search bar must show the match-found countdown.");
                TextFits(title);
                var stamp = band.transform.Find(LobbyMatchBand.StampName);
                var count = band.transform.Find(LobbyMatchBand.CountName).GetComponentInChildren<TMP_Text>();
                Require(band.Found && band.Landed && stamp.gameObject.activeSelf && count.text == "4", "Match found must land a stamp over the cards with a big count.");
                Require(LobbyMatchMoment.Cues == cues + 1, "The stamp must land with one thump.");
                foreach (var text in band.GetComponentsInChildren<TMP_Text>()) TextFits(text);
                yield return Capture(context, "lobby_party_found");
                context.Note("lobby_party", new Dictionary<string, object> {
                    ["heading"] = heading.text, ["plates"] = names, ["copy"] = Label(copy), ["leave"] = Label(leave),
                    ["summary"] = PlayPageText.SearchLine(choices, 4), ["skipped"] = CardDetail("ffa"), ["stamp_cues"] = LobbyMatchMoment.Cues - cues,
                    ["sound"] = LobbyMatchMoment.Sound.ToString(),
                    ["team_size"] = "4v4 picked, 2v2 disabled", ["team_size_note"] = why.text, ["party_line"] = party.Status,
                    ["search_bar"] = new[] { "Searching 0:42", party.SearchTitle }, ["cancel"] = Label(go),
                    ["source"] = "QA stub party, no service calls" });
            }
            finally
            {
                WordboundLobbyParty.Closed(party);
                WordboundLobbyParty.Register(previous);
            }
            yield return Settle();
            Require(Page().GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.PartyLabel), "The strip must return to Party · You.");
        }

        static bool Underlined(Button button) => button.GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled;

        string CardDetail(string id) => Find(Page(), PlayPage.PlaylistCard + id).GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").text;

        IEnumerator MatchmakingLayout(QaContext context, string size)
        {
            var frame = Page().GetComponentInChildren<LobbyPlayPageView>();
            Require(frame, "The saved Play frame is missing.");
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            var column = scroll.content;
            var row = column.childCount > 2 ? (RectTransform)column.GetChild(0) : null;
            Require(row && row.name == PlayPage.TeamSizeRow && column.GetChild(1).name == PlayPage.PartyStrip && column.GetChild(2).name == PlayPage.PlaylistBand,
                "Matchmaking must show the team-size switch, then the party strip, then the cards.");
            var tabs = ScreenRect(frame.Queues); var rowArea = ScreenRect(row); var view = ScreenRect(scroll.viewport); var safe = Screen.safeArea;
            Require(rowArea.yMax <= tabs.yMin + 2 && rowArea.yMax >= view.yMax - 24 && rowArea.yMin >= view.yMin - 2,
                "The team-size switch must sit just under the Same screen, Online and Create tabs.");
            var sizes = row.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(PlayPage.TeamSizeSwitch + " ")).ToArray();
            Require(sizes.Select(Label).SequenceEqual(new[] { "2v2", "4v4" }), "The switch must offer 2v2 and 4v4, and no 8v8.");
            Require(sizes.Count(Underlined) == 1, "The switch must pick one size at a time.");
            var title = row.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.TeamSizeTitle);
            Require(title.text == PlayPage.TeamSizeWord, "The switch must be labelled Team size.");
            TextFits(title);
            foreach (var choice in sizes) ControlFits(choice);
            Require(scroll.content.rect.height <= scroll.viewport.rect.height + 1, "The Online tab must not scroll up and down.");
            var about = Page().GetComponentInChildren<LobbyPlayDetail>();
            Require(about && about.Shown != null && column.GetChild(2).GetComponent<LobbyPlayColumnFlow>().SideShown,
                "The Online tab must describe the chosen card beside the columns.");
            ControlFits(about.Guide);
            foreach (var text in about.GetComponentsInChildren<TMP_Text>()) TextFits(text);
            var ids = Wreckabulary.Rules.Wordbound.QuickPlayCards.Ids;
            var cards = ids.Select(id => Find(Page(), PlayPage.PlaylistCard + id)).ToArray();
            var areas = cards.Select(card => ScreenRect((RectTransform)card.transform)).ToArray();
            var band = column.GetChild(2);
            var plan = band.GetComponent<LobbyPlayColumnFlow>() ? band.GetComponent<LobbyPlayColumnFlow>().Plan : null;
            Require(plan != null && !plan.Wrapped, "The playlist cards must sit in columns, one section beside the next.");
            var places = new Dictionary<string, int[]>();
            var groups = Wreckabulary.Rules.Wordbound.QuickPlaylists.Groups;
            int firstColumn = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                string group = groups[g];
                var listed = ((RectTransform)band.Find(PlayPage.PlaylistSection + group)).GetComponentsInChildren<Button>();
                Require(listed.Select(card => card.name).SequenceEqual(Wreckabulary.Rules.Wordbound.QuickPlayCards.All
                    .Where(card => card.Group == group).Select(card => PlayPage.PlaylistCard + card.Id)), group + " must list its cards top to bottom.");
                for (int entry = 0; entry < listed.Length; entry++)
                {
                    var (columnAt, rowAt) = plan.Cell(entry);
                    places[listed[entry].name.Substring(PlayPage.PlaylistCard.Length)] = new[] { firstColumn + columnAt, rowAt };
                    if (entry == 0) continue;
                    var area = ScreenRect((RectTransform)listed[entry].transform); var before = ScreenRect((RectTransform)listed[entry - 1].transform);
                    Require(columnAt == plan.Cell(entry - 1).column
                        ? Mathf.Abs(area.xMin - before.xMin) <= 2 && area.yMax <= before.yMin + 2
                        : area.xMin > before.xMax - 1,
                        "Each card must sit under the one before it, or start the next column: " + listed[entry].name);
                }
                firstColumn += plan.Columns[g];
            }
            for (int i = 0; i < cards.Length; i++)
            {
                var area = areas[i];
                Require(area.xMin >= view.xMin - 2 && area.xMax <= view.xMax + 2 && area.yMin >= view.yMin - 2 && area.yMax <= view.yMax + 2 &&
                    area.xMin >= safe.xMin - 2 && area.xMax <= safe.xMax + 2 && area.yMin >= safe.yMin - 2 && area.yMax <= safe.yMax + 2,
                    "The " + ids[i] + " card is not fully visible without scrolling.");
                Require(Mathf.Abs(area.width - areas[0].width) <= 2 && Mathf.Abs(area.height - areas[0].height) <= 2, "All five cards must share one size.");
                Require(places.ContainsKey(ids[i]), "The " + ids[i] + " card is in no section.");
                RowCardFits(cards[i], area);
                foreach (var text in cards[i].GetComponentsInChildren<TMP_Text>()) TextFits(text);
            }
            var classic = ScreenRect((RectTransform)band.Find(PlayPage.PlaylistSection + Wreckabulary.Rules.Wordbound.QuickPlaylists.Classic));
            var arcade = ScreenRect((RectTransform)band.Find(PlayPage.PlaylistSection + Wreckabulary.Rules.Wordbound.QuickPlaylists.Arcade));
            Require(arcade.xMin > classic.xMax && Mathf.Abs(arcade.yMax - classic.yMax) <= 2, "Arcade must start right of Classic at the same top.");
            context.Note("matchmaking_layout_" + size, new Dictionary<string, object> {
                ["screen"] = new[] { Screen.width, Screen.height }, ["switch"] = sizes.Select(Label).ToArray(),
                ["picked"] = sizes.Single(Underlined).name, ["switch_row"] = new[] { rowArea.x, rowArea.y, rowArea.width, rowArea.height },
                ["tabs"] = new[] { tabs.x, tabs.y, tabs.width, tabs.height }, ["viewport"] = new[] { view.x, view.y, view.width, view.height },
                ["content_height"] = scroll.content.rect.height, ["viewport_height"] = scroll.viewport.rect.height,
                ["cards"] = ids.Select((id, i) => (object)new Dictionary<string, object> { ["id"] = id, ["detail"] = CardDetail(id),
                    ["column"] = places[id][0], ["row"] = places[id][1],
                    ["rect"] = new[] { areas[i].x, areas[i].y, areas[i].width, areas[i].height } }).ToArray(),
                ["section_columns"] = plan.Columns.ToArray(), ["rows_that_fit"] = plan.RowsFit,
                ["fully_visible"] = true });
            yield break;
        }

        static void RowCardFits(Button card, Rect area)
        {
            var picture = card.GetComponentsInChildren<RectTransform>().Where(part => part.name == "Map preview" || part.name == "Map fallback").ToArray();
            Require(picture.Length == 1, "Each playlist card shows exactly one picture or fallback: " + card.name);
            var shape = ScreenRect(picture[0].name == "Map preview" ? (RectTransform)picture[0].parent : picture[0]);
            Require(Mathf.Abs(shape.width / Mathf.Max(1f, shape.height) - 16f / 9f) <= .03f && shape.xMin >= area.xMin - 1 && shape.center.x < area.center.x,
                "A playlist card keeps its 16:9 picture on the left: " + card.name);
            foreach (string label in new[] { "Map title", "Map detail" })
            {
                var words = ScreenRect(card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == label).rectTransform);
                Require(words.xMin >= shape.xMax - 1 && words.xMax <= area.xMax + 1 && words.yMin >= area.yMin - 1 && words.yMax <= area.yMax + 1,
                    "A playlist card writes its name and detail right of the picture: " + card.name);
            }
        }

        IEnumerator MatchmakingSwitch(QaContext context)
        {
            string key = Wreckabulary.Rules.Wordbound.QuickPlayChoices.StorageKey;
            bool existed = PlayerPrefs.HasKey(key); string saved = PlayerPrefs.GetString(key, "");
            try
            {
                string squad = PlayPage.TeamSizeSwitch + " " + Wreckabulary.Rules.Wordbound.QuickPlayCards.Squad;
                string duo = PlayPage.TeamSizeSwitch + " " + Wreckabulary.Rules.Wordbound.QuickPlayCards.Duo;
                Submit(Find(Page(), squad));
                yield return Settle();
                string summary = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Matchmaking playlists").text;
                Require(Underlined(Find(Page(), squad)) && CardDetail("relay") == "Teams of 4 race to the delivery" &&
                    CardDetail("brawl") == "Teams of 4 race, and fighting is on" && !summary.Contains("2v2"),
                    "4v4 must switch the team cards and the summary.");
                yield return MatchmakingLayout(context, "4v4");
                yield return Capture(context, "lobby_matchmaking_4v4");
                Submit(Find(Page(), duo));
                yield return Settle();
                Require(Underlined(Find(Page(), duo)) && CardDetail("relay") == "Teams of 2 race to the delivery", "2v2 must switch back.");
                context.Note("matchmaking_switch", new Dictionary<string, object> { ["squad_summary"] = summary,
                    ["squad_saved"] = "size=Squad", ["back_to"] = "2v2" });
            }
            finally
            {
                if (existed) PlayerPrefs.SetString(key, saved); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            if (menu) { menu.Page<PlayPage>().Refresh(); yield return Settle(); }
        }

        sealed class QaLobbyParty : IWordboundLobbyParty
        {
            public bool Active { get; set; } = true;
            public bool Leader { get; set; } = true;
            public bool Searching { get; set; }
            public bool Found { get; set; }
            public string Heading { get; set; } = "Party · 4 of 4";
            public string Status { get; set; } = new Wreckabulary.Rules.Wordbound.QuickPlayChoices().PartyLine(4);
            public string Problem { get; set; }
            public string SearchTitle { get; set; } = "";
            public IReadOnlyList<string> Members { get; set; } = new[] { "You · leader · ready", "Player 2 · ready", "Player 3 · ready", "Player 4 · not ready" };
            public bool Copied { get; set; }
            public bool CanCopy { get; set; } = true;
            public bool CanLeave { get; set; } = true;
            public bool CanFind { get; set; } = true;
            public bool CanCancel { get; set; }
            public bool CanRetry { get; set; }
            public void Invite() { }
            public void Join(string code) { }
            public void Copy() { }
            public void Leave() { }
            public void FindMatch() { }
            public void Cancel() { }
            public void Retry() { }
        }

        IEnumerator Online(QaContext context)
        {
            WordboundPartyFrameObservation observation = null;
            try
            {
                Submit(Top("PLAY"));
                yield return Settle();
                string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
                Submit(Find(Page(), "Queue " + LobbyMenu.Matchmaking));
                yield return Settle();
                var cards = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(PlayPage.PlaylistCard)).ToArray();
                var findMatch = Find(Page(), PlayPage.FindMatch);
                var friends = Find(Page(), PlayPage.CustomMatch);
                var invite = Find(Page(), PlayPage.InviteFriendsButton);
                var joinParty = Find(Page(), PlayPage.JoinCode);
                var partyField = Page().GetComponentsInChildren<TMP_InputField>().SingleOrDefault(field => field.name == PlayPage.CodeField);
                Require(SceneManager.GetActiveScene().name == Session.HubScene && menu.Current == LobbyMenu.Play && menu.Queue == queue &&
                    cards.Select(card => card.name.Substring(PlayPage.PlaylistCard.Length)).SequenceEqual(Wreckabulary.Rules.Wordbound.QuickPlayCards.Ids) &&
                    findMatch.IsInteractable() && friends.IsInteractable(),
                    "The Matchmaking tab must stay in the lobby with one card per mode, Find match and Custom match.");
                Require(invite.IsInteractable() && joinParty.IsInteractable() && partyField && partyField.IsInteractable(),
                    "The Matchmaking tab must show Invite friends and the one code box with Join.");
                Require(Page().GetComponentsInChildren<TMP_InputField>().Length == 1 && partyField.transform.parent.parent.name == PlayPage.TeamSizeRow,
                    "The one code box must share the team size row.");
                foreach (var text in cards.Concat(new[] { findMatch, friends, invite, joinParty }).SelectMany(button => button.GetComponentsInChildren<TMP_Text>()))
                    TextFits(text);
                var matchScroll = partyField.GetComponentInParent<ScrollRect>();
                var matchView = matchScroll ? ScreenRect(matchScroll.viewport) : Screen.safeArea;
                foreach (var shown in new[] { (RectTransform)invite.transform, (RectTransform)partyField.transform, (RectTransform)joinParty.transform })
                {
                    var area = ScreenRect(shown);
                    Require(area.yMin >= matchView.yMin - 2 && area.yMax <= matchView.yMax + 2, "The Matchmaking tab hides " + shown.name + " below the fold.");
                }
                var findArea = ScreenRect((RectTransform)findMatch.transform);
                Require(findArea.yMin >= Screen.safeArea.yMin - 2 && findArea.yMax <= Screen.safeArea.yMax + 2, "Find match is off screen.");
                yield return MatchmakingLayout(context, "2v2");
                yield return Capture(context, "lobby_matchmaking");
                context.Note("matchmaking_tab", new Dictionary<string, object> {
                    ["cards"] = cards.Select(card => card.name.Substring(PlayPage.PlaylistCard.Length)).ToArray(),
                    ["find_match"] = Label(findMatch), ["custom_match"] = Label(friends), ["invite_friends"] = Label(invite),
                    ["join_party"] = Label(joinParty), ["scene"] = SceneManager.GetActiveScene().name });
                yield return MatchmakingSwitch(context);
                yield return LobbyPartyStates(context);
                friends = Find(Page(), PlayPage.CustomMatch);
                Submit(friends);
                yield return Settle();
                var rooms = Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(PlayPage.RoomModeCard)).ToArray();
                var create = Find(Page(), PlayPage.CreateRoom);
                var join = Find(Page(), PlayPage.JoinCode);
                var custom = Find(Page(), PlayPage.CustomMap);
                var playlists = Find(Page(), PlayPage.BackToPlaylists);
                var codeField = Page().GetComponentsInChildren<TMP_InputField>().SingleOrDefault(field => field.name == PlayPage.CodeField);
                var layoutRow = Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.LayoutRow);
                Require(layoutRow.rect.height <= 60 && layoutRow.GetComponentsInChildren<Button>().Length == Wreckabulary.Rules.Wordbound.FriendRooms.Layouts,
                    "The custom match map must be one compact row with its four versions.");
                foreach (var version in layoutRow.GetComponentsInChildren<Button>()) ControlFits(version);
                foreach (var text in layoutRow.GetComponentsInChildren<TMP_Text>()) TextFits(text);
                Require(SceneManager.GetActiveScene().name == Session.HubScene && menu.Current == LobbyMenu.Play &&
                    rooms.Length == Wreckabulary.Rules.Wordbound.FriendRooms.All.Count && codeField && codeField.IsInteractable() &&
                    new[] { create, join, custom, playlists }.All(button => button.IsInteractable()),
                    "Custom match must stay in the lobby with the room mode cards, Create room, the code field, Join, the custom map link and Playlists.");
                foreach (var text in rooms.Concat(new[] { create, join, custom, playlists }).SelectMany(button => button.GetComponentsInChildren<TMP_Text>()))
                    TextFits(text);
                var friendsScroll = codeField.GetComponentInParent<ScrollRect>();
                var friendsView = friendsScroll ? ScreenRect(friendsScroll.viewport) : Screen.safeArea;
                foreach (var shown in new[] { (RectTransform)codeField.transform, (RectTransform)join.transform, (RectTransform)custom.transform })
                {
                    var area = ScreenRect(shown);
                    Require(area.yMin >= friendsView.yMin - 2 && area.yMax <= friendsView.yMax + 2, "Custom match hides " + shown.name + " below the fold.");
                }
                var roomGuide = Page().GetComponentsInChildren<Button>().Where(button => button.name == PlayPage.GuideIcon).ToArray();
                Require(roomGuide.Length == 1 && roomGuide[0].IsInteractable() && roomGuide[0].GetComponentInParent<TMP_Text>()?.name == "Friends room",
                    "Custom match must offer the guide of the chosen room mode beside its footer title.");
                ControlFits(roomGuide[0]);
                yield return Capture(context, "lobby_friends_host");
                context.Note("friends_view", new Dictionary<string, object> {
                    ["modes"] = rooms.Select(card => card.name.Substring(PlayPage.RoomModeCard.Length)).ToArray(),
                    ["create"] = Label(create), ["join"] = Label(join), ["custom_map"] = Label(custom), ["guide"] = roomGuide.Length == 1,
                    ["scene"] = SceneManager.GetActiveScene().name });
                codeField.text = "wb-room:1234567890abcdef:NoSuchMode:Solo:asia:p1:0123456789abcdef:m1";
                Submit(join);
                WordboundLauncher launcher = null;
                yield return context.WaitUntil(() => {
                    launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                    return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
                }, 45, "the real Wordbound party launcher");
                Require(SceneManager.GetActiveScene().name == Session.WordboundScene && Session.LobbyQueue == queue && Session.LobbyMode == mode && Session.MapId == map,
                    "Online route failed or reclassified the selected local mode.");
                var party = launcher.GetComponent<WordboundPartyMenu>();
                yield return context.WaitUntil(() => party.InRoom, 10, "the room view");
                Require(!party.Coordinator.Authenticated && party.Coordinator.Party == null && !party.Coordinator.MatchActive && party.BeginMatchRequested != null,
                    "The room view must be connected to its launcher without connecting a guest or starting matchmaking.");
                observation = launcher.gameObject.AddComponent<WordboundPartyFrameObservation>();
                observation.Begin(context, party, launcher.LobbyCamera);
                yield return context.WaitUntil(() => observation && observation.Ready, 10, "actual party repaints and assigned camera frames");
                observation.Record();
                yield return Capture(context, "lobby_online_ready", false);
                observation.Record();
                Require(observation.Ready, "Online screen stopped rendering during capture.");
                context.Note("online_scope", "Lobby Matchmaking tab with its party strip, then the Custom match host view, then Join with a code the online scene rejects before any request, so the real room view shows its code error with an initialized coordinator; Find match, Invite friends and Create room are not pressed, so no Connect, party, queue, room or match request.");
                context.Note("preserved_local_selection", new[] { queue, mode, map });
            }
            finally
            {
                if (observation) observation.Dispose();
                PreservePreferences(context);
            }
        }

        RectTransform Page() => menu.Current switch {
            LobbyMenu.Play => menu.Page<PlayPage>().Root, LobbyMenu.Loadout => menu.Page<LoadoutPage>().Root,
            LobbyMenu.Shop => menu.Page<ShopPage>().Root, LobbyMenu.Settings => menu.Page<SettingsPage>().Root,
            _ => menu.Page<HomePage>().Root };
        Button Top(string name) => Find(menu.transform.Find("Safe area/Top bar"), name);
        static Button Find(Transform root, string name)
        {
            Require(root, "The page holding " + name + " is not built.");
            var found = root.GetComponentsInChildren<Button>().Where(button => button.name == name).ToArray();
            Require(found.Length == 1, (found.Length == 0 ? "Missing control: " : "Duplicated control: ") + name);
            return found[0];
        }
        bool Present(string name) => menu && Page() && Page().GetComponentsInChildren<Button>().Any(button => button.name == name && button.IsInteractable());
        IEnumerator Shown(string name, string what, float seconds = 3f)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (!Present(name) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Require(Present(name), "Timed out waiting for " + what + " (" + name + ").");
        }
        string[] ModeCards() => Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode ")).Select(button => button.name.Substring(5)).ToArray();
        string[] CreateCards() => Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.CreateRow)
            .GetComponentsInChildren<Button>().Select(button => button.name).ToArray();

        void ToolCardsFit(QaContext context, string key)
        {
            var row = Page().GetComponentsInChildren<RectTransform>().Single(rect => rect.name == PlayPage.CreateRow);
            var cards = CreateTools.Select(name => Find(Page(), name)).ToArray();
            var areas = cards.Select(card => ScreenRect((RectTransform)card.transform)).ToArray();
            var view = ScreenRect(row.GetComponentInParent<ScrollRect>().viewport);
            Require(Mathf.Abs(areas[0].width - areas[1].width) <= 2 && Mathf.Abs(areas[0].height - areas[1].height) <= 2 &&
                Mathf.Abs(areas[0].yMax - areas[1].yMax) <= 2 && areas[1].xMin > areas[0].xMax, "The two Create tools must be the same size, side by side.");
            for (int i = 0; i < cards.Length; i++)
            {
                Require(Inside(areas[i], view), "A Create tool is not fully visible without scrolling: " + cards[i].name);
                ControlFits(cards[i]);
                foreach (var text in cards[i].GetComponentsInChildren<TMP_Text>())
                {
                    TextFits(text);
                    Require(text.fontSize >= SmallestModeLabel, "A Create tool word is too small: " + text.text);
                }
            }
            context.Note("create_tools_" + key, new Dictionary<string, object> { ["tools"] = CreateTools,
                ["rects"] = areas.Select(area => (object)new[] { area.x, area.y, area.width, area.height }).ToArray(),
                ["editor_available"] = cards[1].interactable, ["footer"] = menu.Page<PlayPage>().WordboundMaps ? "Map editor" : "Home builder" });
        }
        static string Label(Button button) => button.GetComponentInChildren<TMP_Text>().text;
        static void Submit(Button button)
        {
            Require(button && button.IsInteractable() && EventSystem.current, "Navigation target is unavailable.");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }
        static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(LobbyPop.Seconds + .03f);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
        }
        static void Require(bool condition, string error) { if (!condition) throw new QaFailException(error); }
        static void TextFits(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Require(!text.isTextTruncated && !text.isTextOverflowing && text.textInfo.characterCount > 0, "Text does not fit: " + text.text);
        }
        static Rect ScreenRect(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
        static void ControlFits(Button button)
        {
            var rect = ScreenRect((RectTransform)button.transform); var safe = Screen.safeArea;
            Require(rect.width > 1 && rect.height > 1 && rect.xMin >= safe.xMin - 2 && rect.yMin >= safe.yMin - 2 &&
                rect.xMax <= safe.xMax + 2 && rect.yMax <= safe.yMax + 2, "Control lies outside the visible safe area: " + button.name);
            foreach (var text in button.GetComponentsInChildren<TMP_Text>()) TextFits(text);
        }
        void NavigationFits()
        {
            var buttons = NavNames.Select(Top).ToArray();
            foreach (var button in buttons) ControlFits(button);
            for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++)
            {
                var a = ScreenRect((RectTransform)buttons[i].transform); var b = ScreenRect((RectTransform)buttons[j].transform);
                Require(Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) <= 2 ||
                    Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) <= 2, "Top navigation overlaps: " + buttons[i].name + " / " + buttons[j].name);
            }
        }
        IEnumerator Capture(QaContext context, string name, bool lobby = true)
        {
            if (lobby) NavigationFits();
            yield return context.CaptureScreen(name);
            string path = Path.Combine(context.OutDir, name + ".png");
            Require(File.Exists(path), "Screenshot was not written: " + name);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Require(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false) && texture.width == Screen.width && texture.height == Screen.height,
                    "Screenshot dimensions differ from the actual player window: " + name);
                var pixels = texture.GetPixels32(); var first = pixels[0];
                Require(pixels.Any(pixel => pixel.r != first.r || pixel.g != first.g || pixel.b != first.b), "Screenshot is a uniform frame: " + name);
                context.Note(name + "_capture", new Dictionary<string, object> { ["width"] = texture.width, ["height"] = texture.height,
                    ["focused"] = Application.isFocused, ["rendered_frame"] = Time.renderedFrameCount, ["varied_pixels"] = true });
            }
            finally { Object.Destroy(texture); }
        }
        void PreservePreferences(QaContext context)
        {
            if (lastChoice != null && !lastChoice.Matches())
            {
                lastChoice.Restore(); PlayerPrefs.Save();
                context.Note("last_play_choice_restored", lastChoice.Key);
            }
            if (preferences == null) return;
            var changed = preferences.Where(value => !value.Matches()).ToArray();
            context.Note("navigation_preferences_unchanged", changed.Length == 0);
            foreach (var value in changed) value.Restore();
            if (changed.Length != 0) PlayerPrefs.Save();
            Require(changed.Length == 0, "Navigation changed preferences; originals restored: " + string.Join(", ", changed.Select(value => value.Key)));
        }
        internal sealed class Preference
        {
            public string Key; int kind; bool existed; string text; int integer; float number;
            static Preference Read(string key, int type) => new Preference { Key = key, kind = type, existed = PlayerPrefs.HasKey(key),
                text = type == 0 ? PlayerPrefs.GetString(key) : null, integer = type == 1 ? PlayerPrefs.GetInt(key) : 0,
                number = type == 2 ? PlayerPrefs.GetFloat(key) : 0 };
            public bool Matches() => existed == PlayerPrefs.HasKey(Key) && (!existed || (kind == 0 ? text == PlayerPrefs.GetString(Key) :
                kind == 1 ? integer == PlayerPrefs.GetInt(Key) : number == PlayerPrefs.GetFloat(Key)));
            public void Restore()
            {
                if (!existed) PlayerPrefs.DeleteKey(Key);
                else if (kind == 0) PlayerPrefs.SetString(Key, text);
                else if (kind == 1) PlayerPrefs.SetInt(Key, integer);
                else PlayerPrefs.SetFloat(Key, number);
            }
            public static Preference Text(string key) => Read(key, 0);
            public static Preference[] Capture() => GraphicsOptions.Keys.Concat(new[] { MatchTally.CareerKey, LobbyMenu.OutfitKey,
                LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey, GameSoundPacks.PreferenceKey, KeyBindings.OverridesKey, KeyBindings.LegacyBackupKey,
                LobbyLooksStore.LooksKey, LobbyLooksStore.SeenKey }).Select(key => Read(key, 0))
                .Concat(new[] { "wv.muted", LobbyMenu.VsyncKey, CombatFeedbackOptions.DamageKey, CombatFeedbackOptions.PointsKey,
                    "wv.theme.effects", KeyBindings.InvertKey, TutorialDirector.DoneKey, ScreenShake.Key }.Concat(GameplayPrefs.Keys).Distinct().Select(key => Read(key, 1)))
                .Concat(new[] { LobbyMenu.VolumeKey, KeyBindings.SensitivityKey }.Select(key => Read(key, 2))).ToArray();
        }
    }
}
#endif
