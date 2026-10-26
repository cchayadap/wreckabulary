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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Qa
{
    public sealed class LobbyExperienceQa : IQaScenario
    {
        public string Name => "lobby_experience";
        static readonly string[] HouseModes = { "Dibs", "Duos", "MovingOut", "MovingDay", "Escape" };
        static readonly string[] SettingsTabs = { "profile", "audio", "gameplay", "graphics", "controls", "help" };
        static readonly string[] NavNames = { "Home", "Settings", "Quit", "LOADOUT", "PLAY", "CAREER", "Leaderboard", "Shop", "Party" };
        LobbyMenu menu;
        Preference[] preferences;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register() => QaScenarios.Register("lobby_experience", () => new LobbyExperienceQa());

        public IEnumerable<QaStep> BuildSteps(QaContext context)
        {
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
                Require(Label(Find(Page(), "Queue " + LobbyMenu.Practice)) == "LOCAL" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Matchmaking)) == "ONLINE" &&
                    Label(Find(Page(), "Queue " + LobbyMenu.Workshop)) == "CREATE", "Play destination labels differ.");
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

        IEnumerator SoloFamilies(QaContext context)
        {
            Require(menu.PartySize == 1, "The bounded solo UI check starts with one local human.");
            string queue = menu.Queue, mode = menu.Mode, map = menu.Map, solo = WordboundSoloEntry.SelectedMode;
            var originalBindings = Session.Bindings.ToArray();
            var second = new ScriptedBinding();
            try
            {
                Submit(Find(Page(), "Family Wordbound"));
                yield return Settle();
                Require(!ModeCards().Any() && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")),
                    "Solo Wordbound must not display house or team mode cards.");
                foreach (string selected in new[] { "TimeRun", "Freestyle" })
                {
                    Submit(Find(Page(), "Solo " + selected));
                    yield return Settle();
                    Require(WordboundSoloEntry.SelectedMode == selected && Find(Page(), "Solo GO").interactable,
                        "The selected one-human Wordbound entry is unavailable.");
                    foreach (string name in new[] { "Family house", "Family Wordbound", "Solo TimeRun", "Solo Freestyle", "Solo GO" })
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
                Submit(Find(Page(), "Family house"));
                yield return Settle();
                Require(ModeCards().SequenceEqual(HouseModes) && menu.Queue == queue && menu.Mode == mode && menu.Map == map,
                    "Changing family must restore the exact house choice.");
                Require(menu.PartySize == 2 && ReferenceEquals(menu.Party.Single(), second) && Session.Bindings.SequenceEqual(originalBindings),
                    "Changing family must preserve local seats/devices.");
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
                if (menu.Page<PlayPage>().WordboundMaps) { Submit(Find(Page(), "Create house")); yield return Settle(); }
                Require(ModeCards().SequenceEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }), "Create has unexpected modes.");
                Require(menu.Mode == LobbyMenu.TutorialMode && !Page().GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")),
                    "Tutorial must keep its own room.");
                yield return Capture(context, "lobby_create_learn");
                Submit(Find(Page(), "Mode " + LobbyMenu.WorkshopMode));
                yield return Settle();
                Require(menu.Mode == LobbyMenu.WorkshopMode && HomeDesigner.Supports(menu.Map) && !menu.Starting,
                    "Creative workshop selection must keep a supported house without launching it.");
                yield return Capture(context, "lobby_create_build");
                string queue = menu.Queue, mode = menu.Mode, map = menu.Map;
                var party = menu.Party.ToArray(); var bindings = Session.Bindings.ToArray();
                Submit(Find(Page(), "Create Wordbound")); yield return Settle();
                Require(menu.Page<PlayPage>().WordboundMaps && !ModeCards().Any(), "Wordbound maps must be a separate creator destination.");
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
                Require(ModeCards().SequenceEqual(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }) &&
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
                    }
                }
                Submit(Find(Page(), "Close page"));
                yield return Settle();
                Require(menu.Current == LobbyMenu.Home && !KeyBindings.GameplayBlocked, "Closing Settings did not release input on Home.");
                Require(EventSystem.current.currentSelectedGameObject == Top("Settings").gameObject, "Back did not return focus to the Settings opener.");
                yield return Capture(context, "lobby_home_returned");
                context.Note("settings_scope", "Six tabs, Controls lease, close and opener focus; no binding, graphics, display, volume or profile value changed.");
            }
            finally { PreservePreferences(context); }
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
