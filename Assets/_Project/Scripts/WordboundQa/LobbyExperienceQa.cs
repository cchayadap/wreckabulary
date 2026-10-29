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
        static readonly string[] HouseModes = { "Dibs", "Duos", "MovingOut", "MovingDay", "Escape" };
        static readonly string[] SettingsTabs = { "profile", "audio", "gameplay", "graphics", "controls", "help" };
        static readonly string[] NavNames = { "Home", "Settings", "Quit", "LOADOUT", "PLAY", "CAREER", "Leaderboard", "Shop", "Party" };
        LobbyMenu menu;
        Preference[] preferences;

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
                yield return QaStep.Named("centered_play_flow").Timeout(45)
                    .When(_ => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning)
                    .Do(value => GuardChoices(value, Hierarchy(value), Time.realtimeSinceStartupAsDouble + 40, 40, 45, "hierarchy_owned_stack_cleanup"));
                yield break;
            }
            double localDeadline = 0;
            yield return QaStep.Named("local_choices").Timeout(30)
                .When(_ => {
                    if (localDeadline == 0) localDeadline = Time.realtimeSinceStartupAsDouble + 25;
                    return LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning;
                })
                .Do(value => GuardChoices(value, Local(value), localDeadline, 25, 30, "local_owned_stack_cleanup"));
            yield return QaStep.Named("create_choices").Timeout(20).Do(value =>
                GuardChoices(value, Create(value), Time.realtimeSinceStartupAsDouble + 15, 15, 20, "create_owned_stack_cleanup"));
            yield return QaStep.Named("customize_and_shop").Timeout(20).Do(CustomizeAndShop);
            yield return QaStep.Named("settings_navigation").Timeout(30).Do(Settings);
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
            preferences = Preference.Capture();
            context.Note("scope", "Real Windows UI Submit callbacks and screenshots; no physical-input, matchmaking or full-match acceptance claim.");
            try
            {
                Submit(Top("PLAY"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Play, "Top PLAY did not open the real chooser.");
                Submit(Find(Page(), "Queue " + LobbyMenu.Practice));
                yield return Settle();
                Require(ModeCards().SequenceEqual(HouseModes), "Local must show exactly five original house modes.");
                Require(Label(Find(Page(), "Queue " + LobbyMenu.Practice)) == "Practice (Offline)" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)) == "Matchmaking" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) == "Create", "Play destination labels differ.");
                foreach (string mode in HouseModes)
                {
                    Submit(Find(Page(), "Mode " + mode));
                    yield return Settle();
                    Require(menu.Mode == mode && !menu.Starting && menu.Blocked == null, "Mode selection changed its launch contract: " + mode);
                    var detail = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail");
                    Require(detail.text.ToLowerInvariant().Contains("bot") == (mode == "Dibs" || mode == "Duos"), "Incorrect bot claim: " + mode);
                    TextFits(detail);
                    foreach (string id in HouseModes) ControlFits(Find(Page(), "Mode " + id));
                    if (mode == "Escape") ControlFits(Find(Page(), "Map random"));
                    Require(Find(Page(), "GO").interactable, "The selected local mode cannot start.");
                    context.Note("local_" + mode, new Dictionary<string, object> { ["summary"] = detail.text, ["map"] = menu.Map,
                        ["humans"] = menu.PartySize, ["selected"] = EventSystem.current.currentSelectedGameObject.name });
                    if (mode == "Dibs" || mode == "Duos" || mode == "Escape") yield return Capture(context, "lobby_local_" + mode.ToLowerInvariant());
                }
                yield return SoloFamilies(context);
            }
            finally { PreservePreferences(context); }
        }

        IEnumerator Hierarchy(QaContext context)
        {
            Require(Application.platform == RuntimePlatform.WindowsPlayer && !Application.isBatchMode,
                "Play hierarchy needs a normal presented Windows player.");
            menu = LobbyMenu.Instance; preferences = Preference.Capture();
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
                Submit(Find(Page(), "Mode Dibs")); yield return Settle();
                ObserveHierarchy(context, "practice", "Modes", new[] { "Mode Dibs", "Mode Duos", "Mode MovingOut", "Mode MovingDay", "Mode Escape", "Solo TimeRun", "Solo Freestyle" });
                var houseMaps = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Map ")).ToArray();
                Require(houseMaps.Length > 0, "The selected house mode needs actual maps.");
                var footprint = ScreenRect((RectTransform)houseMaps[0].transform);
                yield return UniformMaps(context, "house", houseMaps, footprint);
                context.Note("house_maps", houseMaps.Select(b => b.name).ToArray());
                yield return Capture(context, "play_practice_house");
                string[] timeMaps = null;
                foreach (string selected in new[] { "TimeRun", "Freestyle" })
                {
                    Submit(Find(Page(), "Solo " + selected)); yield return Settle();
                    ObserveHierarchy(context, selected, "Modes", new[] { "Mode Dibs", "Mode Duos", "Mode MovingOut", "Mode MovingDay", "Mode Escape", "Solo TimeRun", "Solo Freestyle" });
                    Require(Label(Find(Page(), "Solo " + selected)) == (selected == "TimeRun" ? "Time Trial" : "Freeplay"), "Solo display name differs from the chosen direct mode.");
                    var maps = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Solo map ")).ToArray();
                    yield return UniformMaps(context, selected, maps, footprint);
                    var ids = maps.Select(b => b.name).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                    Require(ids.Length == 3, "The installed solo modes must show exactly three compatible installed maps.");
                    if (timeMaps == null) timeMaps = ids; else Require(timeMaps.SequenceEqual(ids), "Time Trial and Freeplay must use the same installed map gallery.");
                    context.Note("solo_maps_" + selected, ids);
                    Require(Find(Page(), "Solo GO").interactable && WordboundSoloEntry.SelectedMode == selected, "The selected compatible solo mode is not launchable.");
                    ControlFits(Find(Page(), "Solo GO"));
                    yield return Capture(context, "play_" + selected.ToLowerInvariant());
                }
                Submit(Find(Page(), "Mode Escape")); yield return Settle();
                var random = Page().GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Map ")).ToArray();
                Require(random.Length == 1 && random[0].name == "Map random" && menu.Map == Session.LobbyRandomMap,
                    "Escape must retain the real random-house contract.");
                yield return UniformMaps(context, "Escape", random, footprint);
                yield return Capture(context, "play_escape_random");
                Submit(Find(Page(), "Queue " + LobbyMenu.Workshop)); yield return Settle();
                Submit(Find(Page(), "Create Wordbound")); yield return Settle();
                ObserveHierarchy(context, "create", "Create modes", new[] { "Create house", "Create Wordbound", "Mode " + LobbyMenu.TutorialMode });
                yield return UniformMaps(context, "Create", new[] { Find(Page(), "Wordbound maps") }, footprint);
                Require(Find(Page(), "Workshop OPEN").interactable, "The installed map editor must remain available.");
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
            Require(Label(Find(Page(), "Queue " + LobbyMenu.Practice)) == "Practice (Offline)" &&
                Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)) == "Matchmaking" &&
                Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) == "Create", "Queue captions differ.");
            Require(!Page().GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Family ")), "An obsolete family selector is still visible.");
            var frame = Page().GetComponentInChildren<LobbyPlayPageView>(); Require(frame, "The saved Play frame is missing.");
            var rows = Page().GetComponentsInChildren<RectTransform>().Where(r => r.name == rowName).ToArray();
            Require(rows.Length == 1, "The direct mode row is missing or duplicated.");
            var row = ScreenRect(rows[0]); var queues = ScreenRect(frame.Queues); var panel = ScreenRect(frame.Panel);
            Require(row.yMax <= queues.yMin + 2, "Modes must sit below the category row.");
            Require(Mathf.Abs(row.center.x - panel.center.x) <= 12, "The direct mode row is not centered in Play.");
            foreach (string target in targets) ControlFits(Find(Page(), target));
            context.Note("hierarchy_" + key, new Dictionary<string, object> {
                ["targets"] = targets, ["captions"] = targets.Select(id => Label(Find(Page(), id))).ToArray(),
                ["queue_captions"] = new[] { Label(Find(Page(), "Queue " + LobbyMenu.Practice)), Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)), Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) },
                ["selected"] = EventSystem.current.currentSelectedGameObject.name,
                ["mode_row"] = new[] { row.x, row.y, row.width, row.height },
                ["queue_row"] = new[] { queues.x, queues.y, queues.width, queues.height },
                ["panel"] = new[] { panel.x, panel.y, panel.width, panel.height }, ["obsolete_family_controls"] = 0 });
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
                Submit(Find(Page(), "Solo TimeRun"));
                yield return Settle();
                Require(ModeCards().SequenceEqual(HouseModes) && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")),
                    "Practice must retain its direct modes and show only maps compatible with the selected solo mode.");
                foreach (string selected in new[] { "TimeRun", "Freestyle" })
                {
                    Submit(Find(Page(), "Solo " + selected));
                    yield return Settle();
                    Require(WordboundSoloEntry.SelectedMode == selected && Find(Page(), "Solo GO").interactable,
                        "The selected one-human Wordbound entry is unavailable.");
                    foreach (string name in new[] { "Mode Dibs", "Mode Duos", "Mode MovingOut", "Mode MovingDay", "Mode Escape", "Solo TimeRun", "Solo Freestyle", "Solo GO" })
                        ControlFits(Find(Page(), name));
                    TextFits(Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Solo detail"));
                    yield return Capture(context, "lobby_solo_" + selected.ToLowerInvariant());
                }
                menu.Join(second);
                yield return Settle();
                Require(menu.PartySize == 2 && ReferenceEquals(menu.Party.Single(), second), "Only the fixture-owned extra seat may be present.");
                Require(!Find(Page(), "Solo GO").interactable, "Solo GO must be disabled with two local humans.");
                var note = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Solo detail");
                Require(note.text == "Solo only. Leave extra local seats to start.", "The one-human restriction must be visible.");
                TextFits(note);
                menu.OpenWordboundSolo("TimeRun");
                Require(SceneManager.GetActiveScene().name == Session.HubScene && menu.PartySize == 2 && WordboundSoloEntry.TakeRequest() == null,
                    "The secondary solo launch guard lost a seat or entered a scene.");
                yield return Capture(context, "lobby_solo_two_seats");
                Submit(Find(Page(), "Mode " + mode));
                yield return Settle();
                Require(ModeCards().SequenceEqual(HouseModes) && menu.Queue == queue && menu.Mode == mode && menu.Map == map,
                    "Selecting the prior direct house mode must restore its exact choice.");
                Require(menu.PartySize == 2 && ReferenceEquals(menu.Party.Single(), second) && Session.Bindings.SequenceEqual(originalBindings),
                    "Selecting another direct mode must preserve local seats/devices.");
                context.Note("local_solo_families", "Two real solo cards; one-human GO; owned second seat blocks launch; exact original house and bindings preserved.");
            }
            finally
            {
                if (menu && menu.Party.Any(binding => ReferenceEquals(binding, second))) menu.Leave(second);
                WordboundSoloEntry.Select(solo);
                if (menu) { menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play); }
            }
        }

        IEnumerator Create(QaContext context)
        {
            bool priorFamily = menu.Page<PlayPage>().WordboundMaps;
            try
            {
                Submit(Find(Page(), "Queue " + LobbyMenu.Workshop));
                yield return Settle();
                Submit(Find(Page(), "Mode " + LobbyMenu.TutorialMode)); yield return Settle();
                Require(ModeCards().SequenceEqual(new[] { LobbyMenu.TutorialMode }), "Create must expose its three direct tools without duplicate mode controls.");
                Require(menu.Mode == LobbyMenu.TutorialMode && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")),
                    "Tutorial must keep its own room.");
                yield return Capture(context, "lobby_create_learn");
                Submit(Find(Page(), "Create house"));
                yield return Settle();
                Require(menu.Mode == LobbyMenu.WorkshopMode && HomeDesigner.Supports(menu.Map) && !menu.Starting,
                    "Creative workshop selection must keep a supported house without launching it.");
                yield return Capture(context, "lobby_create_build");
                string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
                var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
                Submit(Find(Page(), "Create Wordbound")); yield return Settle();
                Require(menu.Page<PlayPage>().WordboundMaps && ModeCards().SequenceEqual(new[] { LobbyMenu.TutorialMode }), "Map editor must remain a directly selectable creator tool.");
                Require(menu.Queue == queue && menu.Mode == mode && menu.Map == map &&
                    menu.Party.SequenceEqual(party) && Session.Bindings.SequenceEqual(bindings), "Creator family changed house selection or local seats.");
                foreach (string name in new[] { "Create house", "Create Wordbound", "Wordbound maps", "Workshop OPEN" }) ControlFits(Find(Page(), name));
                var detail = Page().GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Workshop detail");
                Require(detail.text == "Local editor. One-player playtests." && Find(Page(), "Workshop OPEN").interactable,
                    "The installed local editor entry must be concise and available.");
                TextFits(detail);
                Require(SceneManager.GetActiveScene().name == Session.HubScene && !menu.Starting,
                    "Creator selection must not open a scene or touch workshop autosaves.");
                yield return Capture(context, "lobby_create_wordbound");
                Submit(Find(Page(), "Create house")); yield return Settle();
                Require(ModeCards().SequenceEqual(new[] { LobbyMenu.TutorialMode }) &&
                    menu.Queue == queue && menu.Mode == mode && menu.Map == map && menu.Party.SequenceEqual(party) &&
                    Session.Bindings.SequenceEqual(bindings), "Returning to home tools lost the original selection or seats.");
                context.Note("create_routes", "Tutorial, original home workshop and distinct local Wordbound maps entry; selection-only, no creator launch or autosave access.");
            }
            finally
            {
                try {
                    if (menu) { menu.Page<PlayPage>().WordboundMaps = priorFamily; if (menu.Current == LobbyMenu.Play) menu.Open(LobbyMenu.Play); }
                }
                finally { PreservePreferences(context); }
            }
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
                yield return Capture(context, "lobby_customize");
                var wardrobeScroll = Page().GetComponentsInChildren<ScrollRect>().Single();
                float originalScroll = wardrobeScroll.verticalNormalizedPosition;
                try
                {
                    wardrobeScroll.verticalNormalizedPosition = 0;
                    yield return Settle();
                    yield return Capture(context, "lobby_customize_finishes");
                }
                finally { if (wardrobeScroll) wardrobeScroll.verticalNormalizedPosition = originalScroll; }
                Submit(Find(Page(), "RECIPES"));
                yield return Settle();
                yield return Capture(context, "lobby_customize_equipment");
                Submit(Find(Page(), "LOCKER"));
                yield return Settle();
                Submit(Top("Shop"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Shop, "Shop navigation did not open Shop.");
                yield return Capture(context, "lobby_shop");
                context.Note("customization_scope", "Navigation only; no purchase, outfit change or unlock mutation.");
            }
            finally { PreservePreferences(context); }
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
                panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Rebind ")) == 14,
                "Wordbound must retain both numbered equipment bindings and hide only Dodge.");
            var craft = BindingButton(panel, "Spell");
            EventSystem.current.SetSelectedGameObject(craft.gameObject);
            LobbyScrollFollow.Show(list, (RectTransform)craft.transform.parent.parent);
            yield return Settle();
            context.Note("controls_legend_Wordbound_mid", LegendView(panel, list, "lobby_settings_wordbound_controls_mid",
                new[] { "Aim", "Jump", "Interact", "Spell" }, new[] { "Aim (hold)", "Jump", "Pick up / use", "Open crafting" },
                new[] { "RB / R1", "A / Cross", "RT / R2", "Y / Triangle" }));
            yield return Capture(context, "lobby_settings_wordbound_controls_mid");
            EventSystem.current.SetSelectedGameObject(BindingButton(panel, "Pause").gameObject);
            list.verticalScrollbar.value = 0f;
            yield return Settle();
            Require(list.verticalNormalizedPosition <= .001f, "Wordbound controls did not reach the final bindings.");
            context.Note("controls_legend_Wordbound_bottom", LegendView(panel, list, "lobby_settings_wordbound_controls_bottom",
                new[] { "Hand 1", "Hand 2", "Bag", "Pause" }, new[] { "Equipment 1", "Equipment 2", "Change equipment", "Menu" },
                new[] { "—", "—", "R3 cycles gear", "Start / Options" }));
            yield return Capture(context, "lobby_settings_wordbound_controls_bottom");
            list.verticalScrollbar.value = 1f;
            yield return Settle();
            var house = Find(panel.transform, "House controls");
            ControlFits(house);
            Submit(house);
            yield return Settle();
            Require(panel.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Rebind ")) == 15 &&
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
            Require(header.text == "WORDBOUND CONTROLS", "The scrolled capture needs its visible Wordbound header.");
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

        IEnumerator Online(QaContext context)
        {
            WordboundPartyFrameObservation observation = null;
            try
            {
                Submit(Top("PLAY"));
                yield return Settle();
                string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
                Submit(Find(Page(), "Queue " + LobbyMenu.Matchmaking));
                WordboundLauncher launcher = null;
                yield return context.WaitUntil(() => {
                    launcher = Object.FindFirstObjectByType<WordboundLauncher>();
                    return launcher && launcher.GetComponent<WordboundPartyMenu>()?.Coordinator != null;
                }, 45, "the real Wordbound party launcher");
                Require(SceneManager.GetActiveScene().name == Session.WordboundScene && Session.LobbyQueue == queue && Session.LobbyMode == mode && Session.MapId == map,
                    "Online route failed or reclassified the selected local mode.");
                var party = launcher.GetComponent<WordboundPartyMenu>();
                Require(!party.Coordinator.Authenticated && party.Coordinator.Party == null && !party.Coordinator.MatchActive && party.BeginMatchRequested != null,
                    "Online lobby must be connected to its launcher without connecting a guest or starting matchmaking.");
                observation = launcher.gameObject.AddComponent<WordboundPartyFrameObservation>();
                observation.Begin(context, party, launcher.LobbyCamera);
                yield return context.WaitUntil(() => observation && observation.Ready, 10, "actual party repaints and assigned camera frames");
                observation.Record();
                yield return Capture(context, "lobby_online_ready", false);
                observation.Record();
                Require(observation.Ready, "Online screen stopped rendering during capture.");
                context.Note("online_scope", "Real Wordbound scene, initialized coordinator and rendered guest menu; no Connect, queue, room or match request.");
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
        static Button Find(Transform root, string name) => root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        string[] ModeCards() => Page().GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode ")).Select(button => button.name.Substring(5)).ToArray();
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
            public static Preference[] Capture() => GraphicsOptions.Keys.Concat(new[] { MatchTally.CareerKey, LobbyMenu.OutfitKey,
                LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey, GameSoundPacks.PreferenceKey, KeyBindings.OverridesKey, KeyBindings.LegacyBackupKey }).Select(key => Read(key, 0))
                .Concat(new[] { "wv.muted", LobbyMenu.VsyncKey, CombatFeedbackOptions.DamageKey, CombatFeedbackOptions.PointsKey,
                    "wv.theme.effects", KeyBindings.InvertKey }.Select(key => Read(key, 1)))
                .Concat(new[] { LobbyMenu.VolumeKey, KeyBindings.SensitivityKey }.Select(key => Read(key, 2))).ToArray();
        }
    }
}
#endif
