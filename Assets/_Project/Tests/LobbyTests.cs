using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class LobbyTests
    {
        static readonly string[] Keys = new[] { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LastPlayChoice.StorageKey,
                RecentPlayers.StorageKey }
            .Concat(GraphicsOptions.Keys).ToArray();
        const float NeverStarts = 3600f;
        string[] saved;
        bool hadVolume, hadVsync, savedRemember;
        float savedVolume, savedDelay;
        int savedVsync, savedVsyncCount, savedTutorial;
        bool hadTutorial;
        string homes, savedHomes;
        Gamepad pad;
        Keyboard keys;
        InputSettings.EditorInputBehaviorInPlayMode? keysRoute;
        InputSettings.BackgroundBehavior keysFocus;

        [Test]
        public void RegisteredIconsCoverEveryPublicIdAndRejectUnknownNames()
        {
            var declared = typeof(LobbyIcons).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()).ToArray();
            CollectionAssert.AllItemsAreUnique(LobbyIcons.All);
            CollectionAssert.AreEquivalent(declared, LobbyIcons.All);
            foreach (string id in LobbyIcons.All)
            {
                var icon = LobbyIcons.Get(id);
                Assert.IsNotNull(icon, id);
                Assert.Greater(icon.rect.width, 0, id);
                Assert.Greater(icon.rect.height, 0, id);
                Assert.AreSame(icon, LobbyIcons.Get(id), "Repeated access reuses the icon: " + id);
            }
            Assert.Throws<System.ArgumentException>(() => LobbyIcons.Get("missing-editor-icon"));
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            hadVolume = PlayerPrefs.HasKey(LobbyMenu.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(LobbyMenu.VolumeKey, 1f);
            hadVsync = PlayerPrefs.HasKey(LobbyMenu.VsyncKey);
            savedVsync = PlayerPrefs.GetInt(LobbyMenu.VsyncKey, 0);
            savedVsyncCount = QualitySettings.vSyncCount;
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey);
            savedTutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            savedHomes = HomeStorage.DefaultFolder;
            homes = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wreckabulary-lobby-tests-" + System.Guid.NewGuid().ToString("N"));
            HomeStorage.DefaultFolder = homes;
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            GraphicsOptions.Load();
            savedDelay = LobbyMenu.StartDelay;
            LobbyMenu.StartDelay = .3f;
            savedRemember = LobbyMenu.RememberChoices;
            MatchTally.LastResult = null;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            keys = null;
            if (keysRoute.HasValue)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = keysRoute.Value;
                InputSystem.settings.backgroundBehavior = keysFocus;
            }
            keysRoute = null;
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (saved[i] != null) PlayerPrefs.SetString(Keys[i], saved[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
            }
            if (hadVolume) PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, savedVolume);
            else PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            if (hadVsync) PlayerPrefs.SetInt(LobbyMenu.VsyncKey, savedVsync);
            else PlayerPrefs.DeleteKey(LobbyMenu.VsyncKey);
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, savedTutorial);
            else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
            HomeStorage.DefaultFolder = savedHomes;
            if (System.IO.Directory.Exists(homes)) System.IO.Directory.Delete(homes, true);
            QualitySettings.vSyncCount = savedVsyncCount;
            GraphicsOptions.Load();
            AudioListener.volume = savedVolume;
            LobbyMenu.StartDelay = savedDelay;
            LobbyMenu.RememberChoices = savedRemember;
            MatchTally.LastResult = null;
        }

        static IEnumerator OpenLobby()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.IsNotNull(LobbyMenu.Instance, "the lobby opens on the hub");
            yield return TestScenes.WaitUntil(() => !GameHud.Active.StateController.IsTransitioning,
                1f, "lobby fade and menu input focus");
            Canvas.ForceUpdateCanvases();
        }

        static Button Find(string name)
        {
            var button = Showing(name) ?? Reach(name);
            Assert.IsNotNull(button, $"a '{name}' button is showing");
            return button;
        }

        static Button Showing(string name) => LobbyMenu.Instance.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);

        static bool IsNext(Button go) => go && go.interactable &&
            go.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.ToUpperInvariant().Contains(PlayPage.NextWord.ToUpperInvariant()));

        static bool PassNext(string name)
        {
            var go = Showing(name);
            if (!IsNext(go)) return false;
            go.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            return true;
        }

        static Button Reach(string name)
        {
            if (LobbyMenu.Instance.Current != LobbyMenu.Play) return null;
            string id = name.Substring(name.IndexOf(' ') + 1);
            bool tile = (name.StartsWith("Mode ") || name.StartsWith("Solo ")) && HouseModes.Families.Any(family => HouseModes.ModesIn(family).Contains(id));
            if (tile)
            {
                Showing(PlayPage.BackToModes)?.onClick.Invoke();
                if (!Showing(name)) Showing(PlayPage.CategoryCard + HouseModes.FamilyName(HouseModes.FamilyOf(id)))?.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                return Showing(name);
            }
            bool place = name.StartsWith("Map ") || name.StartsWith("Solo map ") || name.StartsWith(PlayPage.SavedHomeCard) || name == PlayPage.AnyHouse;
            return place && (PassNext("Solo GO") || PassNext("GO")) ? Showing(name) : null;
        }

        static void Click(string name)
        {
            if (name == "GO" || name == "Solo GO") PassNext(name);
            var button = Find(name);
            Assert.IsTrue(button.interactable, $"'{name}' can be pressed");
            button.onClick.Invoke();
        }

        IEnumerator PressGo()
        {
            var go = Find("GO");
            EventSystem.current.SetSelectedGameObject(go.gameObject);
            bool next = IsNext(go);
            yield return Press(GamepadButton.South);
            if (!next) yield break;
            EventSystem.current.SetSelectedGameObject(Find("GO").gameObject);
            yield return Press(GamepadButton.South);
        }

        static float ScreenX(Component c)
        {
            var rect = (RectTransform)c.transform;
            return rect.TransformPoint(rect.rect.center).x;
        }

        static Transform Backdrop(LobbyStage stage) =>
            stage.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Lobby backdrop"));

        static float Height(string name, System.Type layout)
        {
            Canvas.ForceUpdateCanvases();
            var rect = LobbyMenu.Instance.GetComponentsInChildren<RectTransform>().Single(r => r.name == name && r.GetComponent(layout));
            return rect.rect.height;
        }

        static bool Within(RectTransform inner, RectTransform outer)
        {
            var a = new Vector3[4]; var b = new Vector3[4];
            inner.GetWorldCorners(a); outer.GetWorldCorners(b);
            const float slack = .5f;
            return a[0].x >= b[0].x - slack && a[0].y >= b[0].y - slack && a[2].x <= b[2].x + slack && a[2].y <= b[2].y + slack;
        }

        static GameObject Selected => EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        static void AssertNoEllipsis(Transform root, string where)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated, $"\"{text.text}\" fits without an ellipsis in {where}");
            }
        }

        IEnumerator Press(GamepadButton button)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        IEnumerator Tap(Key key)
        {
            if (keys == null)
            {
                keysRoute = InputSystem.settings.editorInputBehaviorInPlayMode;
                keysFocus = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keys = InputSystem.AddDevice<Keyboard>();
            }
            keys.MakeCurrent();
            InputSystem.QueueStateEvent(keys, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
        }

        static void ClickOn(GameObject target)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            e.pointerPressRaycast = new RaycastResult { gameObject = target };
            ExecuteEvents.ExecuteHierarchy(target, e, ExecuteEvents.pointerClickHandler);
        }

        static void AssertFramed(LobbyStage stage, string why)
        {
            var cam = stage.Camera;
            var toYou = (stage.Spot + Vector3.up * .5f - cam.transform.position).normalized;
            Assert.Greater(Vector3.Dot(cam.transform.forward, toYou), .95f, why);
        }

        [UnityTest]
        public IEnumerator LobbyStandsYouInTheSelectedMap()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreEqual(Session.MapId, stage.Map, "the stage still records the selected map");
            Assert.IsNotNull(Backdrop(stage), "the moving-day room is built behind you");
            StringAssert.Contains(LobbyRoom.RoomName, Backdrop(stage).name, "one dedicated lobby room, whatever the map");
            Assert.AreSame(stage.Room, Backdrop(stage).GetComponentInChildren<LobbyRoom>());
            Assert.IsNotEmpty(stage.Room.Wreckable, "some furniture can wreck into its letters");
            Assert.IsNotNull(stage.Wreck, "the wreck loop runs in the room");
            Assert.IsNotNull(stage.Avatar, "your avatar stands in it");
            Assert.Greater(stage.Spot.magnitude, 100f, "built well away from the hub room");
            Assert.AreSame(Camera.main, stage.Camera);
            Assert.IsFalse(stage.Camera.orthographic);
            AssertFramed(stage, "the camera looks at you");
            Assert.IsFalse(Object.FindAnyObjectByType<PlayerJoinManager>().AllowJoining, "clicking the hub no longer joins players");
            Assert.IsEmpty(Object.FindAnyObjectByType<PlayerJoinManager>().Players, "bots never stand in the lobby");

            var sky = stage.GetComponentsInChildren<Canvas>().Single(c => c.name == "Lobby sky");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, sky.renderMode, "drawn in the world pass, behind the room");
            Assert.AreSame(stage.Camera, sky.worldCamera);
            Assert.IsTrue(sky.enabled);
            Assert.IsFalse(sky.GetComponent<GraphicRaycaster>(), "the sky never takes clicks");
            Assert.AreEqual(LobbyThemes.Current.Surface, stage.Camera.backgroundColor, "the lobby follows the equipped bright theme");
            var look = stage.GetComponentsInChildren<Volume>().Single(v => v.name == "Lobby look");
            Assert.Greater(look.priority, GraphicsOptions.Look.priority, "the lobby grade wins over the default look");
            Assert.IsTrue(look.sharedProfile.Has<Vignette>() && look.sharedProfile.Has<DepthOfField>());
            Assert.IsTrue(look.sharedProfile.TryGet<Vignette>(out var vignette));
            Assert.AreEqual(0f, vignette.intensity.value, "the new bright showroom does not darken the screen corners");
            Assert.AreEqual(3, stage.GetComponentsInChildren<Light>().Count(l => l.type == LightType.Spot && l.enabled), "warm key, fill and rim lights keep the whole avatar readable");
        }

        [UnityTest]
        public IEnumerator TopBarIsLaidOutLikeAPcShooter()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var bar = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Top bar");
            var names = bar.GetComponentsInChildren<Button>().Select(b => b.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Home", "Settings", "Quit", "Leaderboard", "Shop", "LOADOUT", "PLAY", "CAREER", "Server" }, names,
                "no sound or help buttons: those live in Settings, and the party moved to the social rail");

            float middle = ScreenX(menu.transform);
            float unit = Screen.width / 1920f * 10f;
            Assert.AreEqual(middle, ScreenX(Find("PLAY").transform.parent), unit, "the text navigation group is centered");
            Assert.Less(ScreenX(Find("LOADOUT")), ScreenX(Find("PLAY")));
            Assert.Greater(ScreenX(Find("CAREER")), ScreenX(Find("PLAY")));
            foreach (var left in new[] { "Home", "Settings", "Quit" })
                Assert.Less(ScreenX(Find(left)), ScreenX(Find("LOADOUT")), $"{left} is on the left");
            Assert.AreSame(Find("Home").transform.parent, Find("Quit").transform.parent, "home, settings and quit share a pill");
            Assert.AreSame(Find("Leaderboard").transform.parent, Find("CAREER").transform.parent, "the trophy sits beside Career");
            Assert.Greater(ScreenX(Find("Leaderboard")), ScreenX(Find("CAREER")));
            Assert.Greater(ScreenX(Find("Shop")), ScreenX(Find("Leaderboard")));
            Assert.IsFalse(Find("Party").transform.IsChildOf(bar), "the party lives in the rail, not the top bar");
            Assert.AreSame(Find("Server").transform.parent, Find("Shop").transform.parent, "the server pill sits by the coins, above the rail");
            Assert.Greater(ScreenX(Find("Server")), ScreenX(Find("Shop")), "at the right end, over the rail");
            Assert.Greater(ScreenX(Find("Party")), ScreenX(Find("CAREER")), "the rail runs down the right edge");
        }

        [UnityTest]
        public IEnumerator TheBarLeavesThemeArtworkClearAndItsButtonsReceiveClicks()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            foreach (var name in new[] { "Top scrim", "Right scrim", "Bottom scrim" })
                Assert.IsFalse(menu.GetComponentsInChildren<Image>(true).Any(i => i.name == name), $"{name} no longer covers the theme artwork");
            var home = (RectTransform)Find("Home").transform;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = home.TransformPoint(home.rect.center) }, hits);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(home), $"the click lands on Home, not on {hits[0].gameObject.name}");
            Click("PLAY");
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "the exposed artwork does not obstruct navigation");
        }

        [UnityTest]
        public IEnumerator TheRailPartyOpensThePartyPanelOnAnyPage()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var panel = menu.transform.Find(LobbyPartyLineup.PartyPanel);
            Assert.IsNotNull(panel);
            Assert.IsFalse(panel.gameObject.activeSelf, "the party stays out of the way until asked for");
            Assert.AreEqual("1/4", CountText(), "the rail counts the party");
            Click("Party");
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf, "the party count opens it");
            Assert.Greater(ScreenX(panel), Screen.width * .6f, "beside the rail, on the right");
            Assert.Less(ScreenX(panel), ScreenX(Find("Party")), "it slides out to the left of the rail");
            Click("Party");
            yield return null;
            Assert.IsFalse(panel.gameObject.activeSelf, "a second click closes it");

            Click("LOADOUT");
            yield return null;
            var corners = new Vector3[4];
            ((RectTransform)Find("RECIPES").transform).GetWorldCorners(corners);
            float tabsRight = corners[2].x;
            ((RectTransform)Find("Close page").transform).GetWorldCorners(corners);
            Assert.Less(tabsRight, corners[0].x, "the loadout's tabs end before the page's close button");
            Assert.Less(corners[2].x, ScreenX(Find("Party")), "the page ends before the rail");
            Click("Party");
            yield return null;
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "the party opens over any page");
            Assert.IsTrue(panel.gameObject.activeSelf);
            yield return Tap(Key.Escape);
            Assert.IsFalse(panel.gameObject.activeSelf, "Esc closes it first");
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "and leaves the page open");
            Assert.AreSame(Find("Party").gameObject, Selected, "with a keyboard or controller back on the party count");

            menu.Join(new ScriptedBinding());
            Assert.AreEqual("2/4", CountText());
            Click("Party");
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>().Concat(menu.Social.Rail.GetComponentsInChildren<TMPro.TMP_Text>()))
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated, $"\"{text.text}\" fits without an ellipsis");
            }
        }

        static string CountText() => LobbyMenu.Instance.Social.Rail.GetComponentsInChildren<TMPro.TMP_Text>()
            .Single(text => text.name == LobbySocialRail.CountName).text;

        [UnityTest]
        public IEnumerator TheHomeDockIsOneBigPlayButtonBottomRight()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Canvas.ForceUpdateCanvases();
            var dock = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Next match");
            var play = Find(LobbyHomePageView.PlayButton);
            Assert.IsTrue(play.transform.IsChildOf(dock), "PLAY is in the dock");
            CollectionAssert.AreEqual(new[] { LobbyHomePageView.PlayButton }, dock.GetComponentsInChildren<Button>().Select(button => button.name).ToArray(),
                "mode, house and online choices live in the Play section");
            Assert.AreEqual(LobbyHomePageView.PlayLabel, play.GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.IsFalse(dock.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text.Contains("LOCAL") || text.text.Contains("ONLINE")));
            Assert.Greater(ScreenX(dock), Screen.width * .6f, "bottom right, as on the web");
            var corners = new Vector3[4];
            dock.GetWorldCorners(corners);
            Assert.Less(corners[0].y, Screen.height * .1f, "down at the bottom");
            Assert.GreaterOrEqual(((RectTransform)play.transform).rect.height, LobbyHomePageView.PlayHeight - 1f, "a big PLAY");
            Assert.AreEqual(300f, dock.rect.width, 1f);
            Assert.AreSame(play.gameObject, Selected, "a controller starts on PLAY");
            AssertNoEllipsis(dock, "the Home dock");
            Assert.IsNull(menu.LastMatch, "nothing is remembered on a first visit");
            AssertHomeOffers(menu, null);
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "PLAY opens the Play section");
            Assert.IsFalse(menu.Starting, "PLAY starts nothing until a match has been chosen");
        }

        static float WorldTop(Component part)
        {
            var corners = new Vector3[4];
            ((RectTransform)part.transform).GetWorldCorners(corners);
            return corners[1].y;
        }

        static float WorldBottom(Component part)
        {
            var corners = new Vector3[4];
            ((RectTransform)part.transform).GetWorldCorners(corners);
            return corners[0].y;
        }

        static void AssertHomeOffers(LobbyMenu menu, string last)
        {
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Canvas.ForceUpdateCanvases();
            var dock = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Next match");
            string[] buttons = dock.GetComponentsInChildren<Button>().Select(button => button.name).ToArray();
            var lines = dock.GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.name == LobbyHomePageView.LastLine).ToArray();
            Assert.AreEqual(300f, dock.rect.width, 1f, "the dock keeps its width");
            if (last == null)
            {
                CollectionAssert.AreEqual(new[] { LobbyHomePageView.PlayButton }, buttons, "with nothing to start, Home shows only PLAY");
                Assert.IsEmpty(lines);
                return;
            }
            CollectionAssert.AreEqual(new[] { LobbyHomePageView.PlayButton, LobbyHomePageView.ChangeButton }, buttons, "PLAY, then Change");
            Assert.AreEqual(1, lines.Length, "one line names the match");
            Assert.AreEqual(last, lines[0].text);
            var play = Find(LobbyHomePageView.PlayButton); var change = Find(LobbyHomePageView.ChangeButton);
            Assert.AreEqual(LobbyHomePageView.ChangeLabel, change.GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.GreaterOrEqual(WorldBottom(lines[0]), WorldTop(play) - .5f, "the match is named above PLAY");
            Assert.LessOrEqual(WorldTop(change), WorldBottom(play) + .5f, "Change sits under PLAY");
            Assert.Less(((RectTransform)change.transform).rect.height, ((RectTransform)play.transform).rect.height * .5f, "Change is the small button");
            Assert.GreaterOrEqual(((RectTransform)play.transform).rect.height, LobbyHomePageView.PlayHeight - 1f, "PLAY stays big");
            AssertNoEllipsis(dock, "the Home dock");
        }

        [UnityTest]
        public IEnumerator HomePlayStartsTheLastLocalMatchAndChangeOpensThePlaySection()
        {
            LobbyMenu.StartDelay = NeverStarts;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            Click("Home");
            yield return null;
            Assert.IsNull(menu.LastMatch, "looking at the tabs is not choosing a match");
            AssertHomeOffers(menu, null);
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            Assert.IsFalse(menu.Starting);

            Click("Mode Duos");
            yield return null;
            Click("Home");
            yield return null;
            string line = LobbyMenu.ModeName("Duos") + MapCardText.Separator + PracticeMaps.Every;
            Assert.AreEqual(line, menu.LastMatch, "the chosen match is named in the footer's words");
            AssertHomeOffers(menu, line);
            Assert.AreSame(Find(LobbyHomePageView.PlayButton).gameObject, Selected, "a controller still starts on PLAY");
            Assert.AreSame(Find(LobbyHomePageView.ChangeButton), Find(LobbyHomePageView.PlayButton).FindSelectableOnDown(), "and Change is one step down");

            Click(LobbyHomePageView.ChangeButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "Change opens the Play section");
            Assert.IsFalse(menu.Starting, "and starts nothing");
            Assert.AreSame(Find("Mode Duos").gameObject, Selected);

            Click("Home");
            yield return null;
            Click(LobbyHomePageView.PlayButton);
            Assert.IsTrue(menu.Starting, "one press starts the remembered match");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Starting"), "with the same countdown as GO");
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "and the same CANCEL");
            StringAssert.Contains(menu.Describe(), menu.Messages[0]);
            Click("CANCEL");
            Assert.IsFalse(menu.Starting);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            AssertHomeOffers(menu, line);

            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            Click("Home");
            yield return null;
            AssertHomeOffers(menu, null);
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "after Create, PLAY opens the Play section");
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue);
            Assert.IsFalse(menu.Starting);

            Click("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Click("Home");
            yield return null;
            AssertHomeOffers(menu, null);
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.IsTrue(menu.Current == LobbyMenu.Play && menu.Page<PlayPage>().Online, "after Matchmaking, PLAY opens that tab");
            Assert.IsFalse(menu.Starting);

            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            Click("Home");
            yield return null;
            AssertHomeOffers(menu, menu.MatchTitle);
        }

        [UnityTest]
        public IEnumerator HomePlayPlaysTheRememberedMatchAndKeepsOfferingItAfterwards()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Mode Duos");
            yield return null;
            Click("Home");
            yield return null;
            Click(LobbyHomePageView.PlayButton);
            Assert.IsTrue(menu.Starting);
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.AreEqual("Duos", Match.Mode);
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && LobbyMenu.Instance.Current == LobbyMenu.Home, 2f, "the lobby to come back");
            Assert.AreEqual(LobbyMenu.ModeName("Duos") + MapCardText.Separator + PracticeMaps.Every, LobbyMenu.Instance.LastMatch,
                "the choice is still offered after the match");
            AssertHomeOffers(LobbyMenu.Instance, LobbyMenu.Instance.LastMatch);
            Assert.IsFalse(PlayerPrefs.HasKey(LastPlayChoice.StorageKey), "a test run never writes the saved choice");
        }

        [UnityTest]
        public IEnumerator HomePlayStartsTheRememberedCourseForOnePlayerOnly()
        {
            LobbyMenu.StartDelay = NeverStarts;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click(PlayPage.ModeButton("TimeRun"));
            yield return null;
            Click("Solo map " + WordboundSoloEntry.LostAddressMap);
            yield return null;
            Click("Home");
            yield return null;
            bool installed = Session.CanLoad(Session.WordboundScene);
            Assert.AreEqual(installed ? PlayPage.CourseTitle : null, menu.LastMatch, "a course is offered only when it can load");
            AssertHomeOffers(menu, menu.LastMatch);
            if (installed)
            {
                StringAssert.StartsWith(LobbyPlayModeLabels.Name("TimeRun") + MapCardText.Separator, menu.LastMatch);
                Click(LobbyHomePageView.PlayButton);
                Assert.IsTrue(menu.Starting, "the course starts with the same countdown");
                StringAssert.Contains(PlayPage.CourseTitle, menu.Messages[0]);
                Assert.AreSame(Find("CANCEL").gameObject, Selected);
                Click("CANCEL");
                Assert.IsFalse(menu.Starting);
                Assert.IsNull(WordboundSoloEntry.TakeRequest(), "a cancelled start never asks for the course");
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            }
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Assert.AreEqual(menu.MatchTitle, menu.LastMatch, "with a second seat PLAY offers the house mode, as the Play section does");
            AssertHomeOffers(menu, menu.MatchTitle);
            menu.Leave(second);
            yield return null;
            Assert.AreEqual(installed ? PlayPage.CourseTitle : null, menu.LastMatch, "and the course comes back when the seat leaves");
        }

        [UnityTest]
        public IEnumerator PlayKeepsTheOriginalHouseChoicesInAPlainScrollablePage()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            var page = menu.Page<PlayPage>().Root;
            CollectionAssert.AreEqual(HouseModes.Families.Select(family => PlayPage.CategoryCard + HouseModes.FamilyName(family)).ToArray(),
                page.GetComponentsInChildren<RectTransform>().Single(r => r.name == PlayPage.CategoryRow).GetComponentsInChildren<Button>().Select(b => b.name).ToArray(),
                "Classic, Co-op, Arcade and Training categories run left to right");
            foreach (var family in HouseModes.Families)
                foreach (string mode in HouseModes.ModesIn(family))
                    Assert.IsNotNull(Find(PlayPage.ModeButton(mode)), "every mode is a category and a tile away: " + mode);
            Click("Mode Duos");
            yield return null;
            Assert.IsTrue(PassNext("GO"), "a house mode picks its house on the next step");
            yield return null;
            Assert.AreEqual(GameConfig.Current.Houses.Count, page.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Map ")), "every house, without a Random card");
            Assert.IsFalse(page.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Rays"), "decorative rays are excluded from the redesigned choices");
            Assert.IsFalse(page.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Start" || r.name == "Fitted plan"), "map cards show pictures, not floor-plan skeletons");
            Assert.IsTrue(page.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Map ")).All(b => b.GetComponentsInChildren<RectTransform>().Count(r => r.name == "Map picture") == 1),
                "one picture per house");
            AssertNoEllipsis(page, "PLAY");
            Assert.AreEqual("Duos", LobbyMenu.ModeName("Duos"), "Use the same display name as the direct Play mode choice.");

            Click("Mode " + LobbyMenu.TutorialMode);
            yield return null;
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "the tutorial is the first Training choice");
            Assert.IsFalse(page.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Map ")), "the tutorial has its own room");
            Assert.IsFalse(IsNext(Find("GO")), "so Play starts it from the mode step");
            StringAssert.StartsWith(HouseModes.Name(LobbyMenu.TutorialMode) + " · ", menu.Describe());
            AssertNoEllipsis(page, "PLAY with the tutorial");
        }

        [UnityTest]
        public IEnumerator TheWorkshopOnlyOffersHousesItCanBuildIn()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.Choose(map: "flat");
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            Assert.IsTrue(HomeDesigner.Supports(menu.Map), "the workshop took you to a house it builds in");
            Assert.IsFalse(menu.Page<PlayPage>().Root.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Map ")), "Create shows its two tools, not houses");
            StringAssert.Contains(GameConfig.Current.HouseFor(menu.Map).Name, menu.Describe(), "and the footer says which");
            Click(PlayPage.CreateHouse);
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the workshop to open");
            yield return null;
            var workshop = CreativeWorkshop.Instance;
            var texts = workshop.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.IsTrue(texts.Any(text => text.text == CreativeWorkshop.HouseHeading), "the builder labels its house choice");
            var closed = GameConfig.Current.Houses.Where(house => !HomeDesigner.Supports(house.Key)).Select(house => house.Value.Name).ToArray();
            Assert.AreEqual(PlayPageText.BuilderNote(closed), texts.Single(text => text.name == CreativeWorkshop.HouseNoteName).text,
                "and names the houses it cannot use yet");
            workshop.Close();
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomePlayOpensThePlaySectionOnTheLastUsedTab()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            Assert.AreSame(Find("Mode " + menu.Mode).gameObject, Selected, "a keyboard or controller lands on the mode");
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            menu.Close();
            yield return null;
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue, "PLAY reopens the tab you used last");
            Assert.IsTrue(Find("Queue " + LobbyMenu.Workshop).GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled);
            Click("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            menu.Close();
            yield return null;
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Assert.IsTrue(menu.Page<PlayPage>().Online, "and Matchmaking too");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "opening a tab never leaves the lobby");
        }

        [UnityTest]
        public IEnumerator ShouldersStepThroughThePages()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            yield return Press(GamepadButton.RightShoulder);
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "RB steps right");
            yield return Press(GamepadButton.RightShoulder);
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            yield return Press(GamepadButton.LeftShoulder);
            yield return Press(GamepadButton.LeftShoulder);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "LB steps back");
            yield return Press(GamepadButton.LeftShoulder);
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "and round to the end");
        }

        [UnityTest]
        public IEnumerator NoticesShowTheNewestFewTopLeft()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            for (int i = 1; i <= 5; i++) menu.Post("Notice " + i);
            yield return null;
            var feed = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Messages");
            var cards = feed.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToArray();
            Assert.AreEqual(LobbyMenu.MessagesShown, cards.Length, "a few at once");
            StringAssert.Contains("Notice 5", cards[0].GetComponentInChildren<TMPro.TMP_Text>().text, "the newest on top");
            Assert.Less(ScreenX(feed), Screen.width * .35f, "top left, clear of the dock");
            Assert.AreEqual("Notice 5", menu.Messages[0], "the lobby still keeps what was said");
        }

        [UnityTest]
        public IEnumerator TabsAndIconsShowOnePageAtATime()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var host = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Pages" && r.parent.name == "Safe area");
            var feed = menu.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Messages");
            foreach (var (button, page) in new[]
            {
                ("LOADOUT", LobbyMenu.Loadout), ("PLAY", LobbyMenu.Play), ("CAREER", LobbyMenu.CareerPage),
                ("Shop", LobbyMenu.Shop), ("Leaderboard", LobbyMenu.Trophy), ("Settings", LobbyMenu.Settings), ("Home", LobbyMenu.Home),
            })
            {
                Click(button);
                yield return null;
                Assert.AreEqual(page, menu.Current, button);
                var showing = host.Cast<Transform>().Where(t => t.gameObject.activeSelf).Select(t => t.name).ToArray();
                CollectionAssert.AreEqual(new[] { page + " page" }, showing, $"{button} shows only its own page");
                Assert.AreEqual(page == LobbyMenu.Home, feed.gameObject.activeSelf, "lobby messages show beside Home only");
            }

            Click("LOADOUT");
            yield return new WaitForSeconds(1f);
            Assert.Less(menu.Stage.Camera.WorldToViewportPoint(menu.Stage.Spot).x, .42f, "you stand left of the loadout panel");
            Click("Home");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(.5f, menu.Stage.Camera.WorldToViewportPoint(menu.Stage.Spot).x, .03f, "back in the middle at home");
        }

        [UnityTest]
        public IEnumerator BarButtonsToggleTheirPage()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            foreach (var (button, page) in new[]
            {
                ("LOADOUT", LobbyMenu.Loadout), ("PLAY", LobbyMenu.Play), ("CAREER", LobbyMenu.CareerPage),
                ("Shop", LobbyMenu.Shop), ("Leaderboard", LobbyMenu.Trophy), ("Settings", LobbyMenu.Settings),
            })
            {
                Click(button);
                yield return null;
                Assert.AreEqual(page, menu.Current, $"{button} opens its page");
                Assert.IsTrue(Find(button).TryGetComponent(out LobbyTab tab) ? tab.On : true, $"{button} shows it is open");
                Click(button);
                yield return null;
                Assert.AreEqual(LobbyMenu.Home, menu.Current, $"{button} again closes it");
                Assert.AreSame(Find(button).gameObject, Selected, $"a keyboard or controller is back on {button}, not on GO");
            }
            Click("Home");
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "Home stays home");
        }

        [UnityTest]
        public IEnumerator TheModeGuideOpensFromTheGoalLineAndOwnsBackAndPaging()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, HouseModes.Dibs, "pinwheel");
            Click("PLAY");
            yield return null;
            var icon = menu.Page<PlayPage>().Root.GetComponentsInChildren<Button>().SingleOrDefault(b => b.name == PlayPage.GuideIcon);
            Assert.AreEqual(ModeGuides.Has(HouseModes.Dibs), icon != null, "the goal line offers the guide when one exists");
            if (!icon) yield break;
            try
            {
                EventSystem.current.SetSelectedGameObject(icon.gameObject);
                icon.onClick.Invoke();
                yield return null;
                Assert.IsTrue(ModeGuides.Open);
                Assert.AreEqual(HouseModes.Dibs, ModeGuides.Mode);
                yield return Press(GamepadButton.RightShoulder);
                Assert.AreEqual(LobbyMenu.Play, menu.Current, "paging belongs to the guide while it is open");
                yield return Press(GamepadButton.East);
                Assert.IsFalse(ModeGuides.Open, "B closes the guide");
                Assert.AreEqual(LobbyMenu.Play, menu.Current, "and leaves the Play page open");
                yield return Press(GamepadButton.East);
                Assert.AreEqual(LobbyMenu.Home, menu.Current, "with the guide closed, B closes the page again");
            }
            finally { ModeGuides.Close(); }
        }

        [UnityTest]
        public IEnumerator PagesCloseFromTheirCornerEscAndTheSpaceRoundThem()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            foreach (var (button, page) in new[] { ("CAREER", LobbyMenu.CareerPage), ("PLAY", LobbyMenu.Play), ("Settings", LobbyMenu.Settings) })
            {
                Click(button);
                yield return null;
                Click("Close page");
                yield return null;
                Assert.AreEqual(LobbyMenu.Home, menu.Current, $"the X closes {page}");
                Assert.AreSame(Find(button).gameObject, Selected);
            }

            Click("LOADOUT");
            yield return null;
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "Esc closes the page");
            Assert.AreSame(Find("LOADOUT").gameObject, Selected);

            Click("Settings");
            yield return null;
            var shade = menu.GetComponentsInChildren<LobbyShade>().Single(s => s.name == "Shade");
            var panel = menu.GetComponentsInChildren<RectTransform>().First(r => r.name == "Panel");
            ClickOn(panel.gameObject);
            yield return null;
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "a click on the panel stays");
            ClickOn(shade.gameObject);
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "a click beside it closes it");
            Assert.AreSame(Find("Settings").gameObject, Selected);
        }

        static Career PlayedCareer(params (string mode, int score)[] matches)
        {
            var career = new Career { Name = "Zed" };
            long at = 1759750000;
            foreach (var (mode, score) in matches)
            {
                career.Record(new MatchRecord { Mode = mode, Map = "pinwheel", Won = score >= 300, Score = score, Coins = score / 10, Xp = score / 2, EndedAt = at });
                at += 3600;
            }
            return career;
        }

        [UnityTest]
        public IEnumerator CareerShowsYourLevelAndMatchesAsCards()
        {
            PlayerPrefs.SetString(MatchTally.CareerKey, PlayedCareer(("Dibs", 320), ("Duos", 140)).Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("CAREER");
            yield return null;
            var page = menu.Page<CareerScreen>().Root;
            var line = page.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "XP line");
            line.ForceMeshUpdate();
            Assert.Greater(line.textInfo.lineInfo[0].visibleCharacterCount, 0, "the XP still to go shows");
            StringAssert.Contains("XP to level", line.text);
            Assert.AreEqual(2, page.GetComponentsInChildren<RectTransform>().Count(r => r.name == "Match"), "a card for each match");
            Assert.IsTrue(page.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text == "WON"), "with its result");
            AssertNoEllipsis(page, "CAREER");
        }

        [UnityTest]
        public IEnumerator AnEmptyCareerSendsYouToPractice()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("CAREER");
            yield return null;
            Assert.IsTrue(menu.Page<CareerScreen>().Root.GetComponentsInChildren<RectTransform>().Any(r => r.name == "No matches"));
            AssertNoEllipsis(menu.Page<CareerScreen>().Root, "an empty CAREER");
            Click("Play practice");
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
        }

        [UnityTest]
        public IEnumerator TheLeaderboardIsTheWebsSidePanelWithABoardPerMode()
        {
            PlayerPrefs.SetString(MatchTally.CareerKey, PlayedCareer(("Dibs", 900), ("Dibs", 120), ("Dibs", 300), ("Duos", 500)).Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.Choose(mode: "Dibs");
            Click("Leaderboard");
            yield return null;
            var page = menu.Page<TrophyPage>();
            Assert.AreEqual(LobbyStage.Focus.Left, page.Focus, "a side panel, as on the web");
            Assert.IsFalse(page.Root.GetComponentsInChildren<LobbyShade>().Any(), "with the map beside it, not a shade");
            string Score(int rank) => page.Root.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Rank " + rank)
                .GetComponentsInChildren<TMPro.TextMeshProUGUI>().Last().text;
            Assert.AreEqual("900", Score(1));
            Assert.AreEqual("300", Score(2), "best first");
            Assert.AreEqual("120", Score(3));
            Assert.IsTrue(page.Root.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Note"), "it says whose board this is");
            AssertNoEllipsis(page.Root, "the leaderboard");
            Click("Board Duos");
            yield return null;
            Assert.AreEqual("500", Score(1));
            Assert.IsFalse(page.Root.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Rank 2"));
            Assert.AreSame(Find("Board Duos").gameObject, Selected, "the controller stays on the chip");
        }

        [UnityTest]
        public IEnumerator SettingsHasTabsAndGraphicsThatReallyChange()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Settings");
            yield return null;
            var root = menu.Page<SettingsPage>().Root;
            foreach (string tab in new[] { "profile", "audio", "gameplay", "graphics", "controls", "help" })
            {
                Click("Settings tab " + tab);
                yield return null;
                Assert.IsFalse(root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("Mobile")), "no phone quality level: " + tab);
                AssertNoEllipsis(root, "Settings " + tab);
                if (tab == "controls")
                {
                    var rebind = root.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.name == "Rebind Smash, throw, place, block");
                    Assert.IsNotNull(rebind, "every key is a button you can rebind");
                    Assert.IsTrue(rebind.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == ControlHints.KeyOf(DesktopBinding.Shared.Attack)), "showing the live key");
                    rebind.onClick.Invoke();
                    yield return null;
                    Assert.IsTrue(KeyBindings.Listening, "waits for the new key");
                    Assert.IsNotNull(root.GetComponentInChildren<ControlsPanel>());
                    Assert.IsTrue(root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Press a key…"));
                    KeyBindings.Stop();
                    yield return null;
                    Assert.IsFalse(KeyBindings.Listening);
                    Assert.IsTrue(DesktopBinding.Shared.Map.enabled, "keys work again after");
                    Assert.IsNotNull(root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == "Sensitivity more"), "mouse sensitivity");
                    Assert.IsNotNull(root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == "Invert mouse Y"), "invert Y");
                    Assert.IsNotNull(root.GetComponentsInChildren<RectTransform>().FirstOrDefault(r => r.name == "Reset controls"));
                }
            }
            Assert.IsTrue(root.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Step 01"), "how to play in the web's three steps");

            Click("Settings tab graphics");
            yield return null;
            var shipped = GraphicsOptions.Shipped;
            Assert.AreEqual(GraphicsOptions.DefaultPreset, GraphicsOptions.Preset);
            Click("AA SMAA");
            yield return null;
            var cam = menu.Stage.Camera.GetUniversalAdditionalCameraData();
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, cam.antialiasing, "SMAA on the lobby camera");
            Assert.AreEqual(GraphicsOptions.Custom, GraphicsOptions.Preset, "High with SMAA is a mix of its own");
            Assert.AreEqual(1, ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).msaaSampleCount, "and no MSAA under it");
            Click("Render scale more");
            yield return null;
            var active = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Assert.AreNotSame(shipped, active, "a copy takes the player's numbers");
            Assert.AreEqual(1.25f, active.renderScale, 1e-4f);
            Assert.AreEqual(1f, shipped.renderScale, 1e-4f, "the project's asset is never edited");
            Assert.AreEqual(4, shipped.msaaSampleCount);
            Click("Preset High");
            yield return null;
            Assert.AreSame(shipped, GraphicsSettings.currentRenderPipeline, "High is the shipped picture, so its asset comes back");
            Assert.AreEqual(AntialiasingMode.None, cam.antialiasing);
            Click("Cap 60");
            yield return null;
            Assert.AreEqual(60, Application.targetFrameRate);
            Click("Reset graphics");
            yield return null;
            Assert.AreEqual(-1, Application.targetFrameRate);
            Assert.IsFalse(GraphicsOptions.Customised, "reset forgets the saved settings");
        }

        [UnityTest]
        public IEnumerator ReduceThemeMotionUpdatesTheStageAndSavedPreference()
        {
            bool had = PlayerPrefs.HasKey("wv.theme.effects");
            int saved = PlayerPrefs.GetInt("wv.theme.effects", 1);
            try
            {
                PlayerPrefs.DeleteKey("wv.theme.effects");
                yield return OpenLobby();
                var menu = LobbyMenu.Instance;
                Assert.IsTrue(menu.Stage.ThemeEffectsEnabled);
                Click("Settings");
                Click("Settings tab gameplay");
                Click("Reduce theme motion on");
                Assert.IsFalse(menu.Stage.ThemeEffectsEnabled);
                Assert.AreEqual(0, PlayerPrefs.GetInt("wv.theme.effects", 1));
                Click("Reduce theme motion off");
                Assert.IsTrue(menu.Stage.ThemeEffectsEnabled);
                Assert.AreEqual(1, PlayerPrefs.GetInt("wv.theme.effects"));
            }
            finally
            {
                if (had) PlayerPrefs.SetInt("wv.theme.effects", saved); else PlayerPrefs.DeleteKey("wv.theme.effects");
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator TheQuitBoxSaysWhatIsSavedAndClosesFromItsCorner()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Quit");
            yield return null;
            var dialog = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Quit dialog");
            Assert.IsTrue(dialog.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "Quit game?"));
            Assert.AreSame(Find("STAY").gameObject, Selected, "staying is the default");
            AssertNoEllipsis(dialog, "the quit box");
            Click("Close quit");
            yield return null;
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Quit dialog"), "the X means stay");
            Assert.AreSame(Find("Quit").gameObject, Selected);
        }

        [UnityTest]
        public IEnumerator EscInTheNameFieldKeepsSettingsOpen()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Settings");
            yield return null;
            var field = menu.GetComponentsInChildren<TMPro.TMP_InputField>().Single();
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.IsTrue(field.isFocused, "typing a name");
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "Esc leaves the field, not the page");
            field.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "out of the field, Esc closes the page");
        }

        [UnityTest]
        public IEnumerator AClickBesideTheQuitBoxMeansStay()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Quit");
            yield return null;
            var dialog = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Quit dialog");
            ClickOn(dialog.Find("Box").gameObject);
            yield return null;
            Assert.IsTrue(dialog, "a click on the box itself stays open");
            ClickOn(dialog.gameObject);
            yield return null;
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Quit dialog"), "a click beside it closes it");
            Assert.AreSame(Find("Quit").gameObject, Selected);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "and the game keeps running");
        }

        [UnityTest]
        public IEnumerator TheLobbyAndMatchesRenderSmoothedAndToneMapped()
        {
            yield return OpenLobby();
            var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Assert.AreEqual(4, asset.msaaSampleCount, "MSAA 4x on the PC pipeline");
            var cam = LobbyMenu.Instance.Stage.Camera;
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing, "the lobby camera post-processes");
            Assert.IsTrue(GraphicsOptions.Look, "the shared look volume exists");
            Assert.IsTrue(GraphicsOptions.Look.sharedProfile.TryGet(out Tonemapping tone) && tone.mode.value == TonemappingMode.ACES, "ACES, as on the web");
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.DibsScene);
            Assert.IsTrue(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing, "match cameras post-process too");

            var environment = Wreckabulary.Art.EnvironmentLighting.Active;
            Assert.IsNotNull(environment, "the match owns an editable environment profile");
            var settings = environment.Profile;
            var sun = environment.Sun;
            Assert.AreEqual(settings.SunIntensity, sun.intensity, 1e-3f);
            Assert.AreEqual(settings.SunColor, sun.color);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(settings.SunEuler), sun.transform.rotation), .01f);
            Assert.AreSame(sun, RenderSettings.sun, "the sky's visible sun follows the scene's real light");
            Assert.AreSame(settings.Skybox, RenderSettings.skybox);
            var fill = environment.Fill;
            Assert.AreEqual(LightShadows.None, fill.shadows);
            Assert.Greater(fill.color.b, fill.color.r, "a cool fill");
            Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
            Assert.Greater(RenderSettings.ambientSkyColor.r, RenderSettings.ambientGroundColor.r, "light from above");
            Assert.AreEqual(settings.AmbientGround, RenderSettings.ambientGroundColor, "the room's warm bounce is editable");
        }

        [UnityTest]
        public IEnumerator FixedRowsKeepTheirHeight()
        {
            yield return OpenLobby();
            Click("Party");
            yield return null;
            Assert.AreEqual(74f, Height("You", typeof(HorizontalLayoutGroup)), 1f, "your party card");
            Click("PLAY");
            yield return null;
            var modes = LobbyMenu.Instance.Page<PlayPage>().Root.GetComponentsInChildren<RectTransform>().Single(row => row.name == PlayPage.ModeTiles);
            var modeButtons = modes.GetComponentsInChildren<Button>();
            var open = HouseModes.FamilyOf(LobbyMenu.Instance.Mode);
            CollectionAssert.AreEqual(HouseModes.ModesIn(open).Select(PlayPage.ModeButton), modeButtons.Select(button => button.name),
                "The open category's modes are directly available.");
            var page = LobbyMenu.Instance.Page<PlayPage>().Root;
            var step = page.GetComponentsInChildren<RectTransform>().Single(row => row.name == PlayPage.ModeBoard);
            foreach (var button in modeButtons)
            {
                var tile = (RectTransform)button.transform;
                Assert.GreaterOrEqual(tile.rect.height, 44f, "Mode tiles retain accessible targets.");
                Assert.GreaterOrEqual(tile.rect.width, 44f, button.name);
                Assert.IsTrue(Within(tile, step), button.name + " stays inside the mode step");
            }
            Assert.IsFalse(modes.GetComponentsInChildren<RawImage>().Any(image => image.name == "Rays"), "No decorative stripes.");
            Assert.IsTrue(PassNext("GO"), "the houses are the next step");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var maps = LobbyMenu.Instance.Page<PlayPage>().Root.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses");
            Assert.GreaterOrEqual(maps.cellSize.y, LobbyPlayCardGrid.ShortestCard, "House cards keep their readable height on their own step.");
            Assert.GreaterOrEqual(maps.cellSize.x, LobbyPlayCardGrid.WidthFor(LobbyPlayCardGrid.ShortestCard) - 1f, "and a whole 16:9 picture");
            foreach (var card in maps.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")))
            {
                Assert.AreEqual(1, card.GetComponentsInChildren<RectTransform>().Count(r => r.name == "Map picture"), card.name + " shows one picture");
                foreach (var text in card.GetComponentsInChildren<TMPro.TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, card.name + ": " + text.text);
                }
            }
            Click("CAREER");
            yield return null;
            Assert.AreEqual(112f, Height("Level", typeof(HorizontalLayoutGroup)), 1f, "the level header");
            Assert.AreEqual(110f, Height("Stats", typeof(HorizontalLayoutGroup)), 1f, "the stat tiles");
        }

        [UnityTest]
        public IEnumerator DraggingTheOpenViewTurnsYou()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;

            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .55f) }, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual("Turn area", hits[0].gameObject.name);
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Turn hint"), "a first visit says how to turn");

            float before = stage.Yaw;
            var facing = stage.Avatar.rotation;
            ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current) { delta = new Vector2(-100f, 0f) }, ExecuteEvents.dragHandler);
            float turn = 100f * LobbyDrag.DegreesPerPixel * 1080f / Mathf.Max(1, Screen.height);
            Assert.AreEqual(Mathf.Repeat(before + turn, 360f), stage.Yaw, .01f);
            yield return new WaitForSeconds(.6f);
            Assert.AreEqual(turn, Quaternion.Angle(facing, stage.Avatar.rotation), 2f, "the avatar turns on the spot");
            Assert.AreEqual(menu.Stage.Spot, stage.Avatar.position, "without moving");
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Turn hint"), "and stops saying so once you have");
            Assert.IsTrue(PlayerPrefs.HasKey(LobbyMenu.TurnHintKey), "for good");
        }

        [UnityTest]
        public IEnumerator GoAsksForAWarmupOnlyWhenWarmupsAreOn()
        {
            bool saved = LobbyMenu.WarmupBeforeMatches;
            try
            {
                Assert.IsFalse(saved, "the editor starts matches without a warm-up until a test asks for one");
                LobbyMenu.WarmupBeforeMatches = true;
                yield return OpenLobby();
                Click("PLAY");
                yield return null;
                Click("Queue " + LobbyMenu.Practice);
                yield return null;
                Click("Mode Duos");
                yield return null;
                Click("GO");
                yield return TestScenes.WaitUntil(() => RoundManager.Instance && RoundManager.Instance.Phase == Phase.Warmup, 30f, "the warm-up after a lobby start");
                Assert.IsFalse(RoundManager.WarmupRequested, "the request is used once");
            }
            finally
            {
                LobbyMenu.WarmupBeforeMatches = saved;
            }
        }

        [UnityTest]
        public IEnumerator PlayPicksAModeAndMapThenGoStartsTheMatch()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            string other = GameConfig.Current.Houses.Keys.First(k => k != menu.Map);
            var oldBackdrop = Backdrop(menu.Stage);

            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            Click("Mode Duos");
            yield return null;
            Assert.AreEqual("Duos", menu.Mode);
            Assert.IsTrue(menu.Maps.All, "every house starts ticked, so each match picks one at random");
            Click("Map " + other);
            yield return null;
            CollectionAssert.AreEqual(new[] { other }, menu.Maps.Ticked, "from Any house, one tap plays that one house");
            Assert.AreEqual(other, menu.Map);
            Assert.AreEqual(other, Session.MapId);
            Assert.AreEqual(other, menu.Stage.Map, "the stage records the new map");
            Assert.AreSame(oldBackdrop, Backdrop(menu.Stage), "the lobby room stays the same; maps no longer rebuild it");
            StringAssert.Contains(GameConfig.Current.HouseFor(other).Name, menu.Describe(), "the selected match shows the current house");
            AssertFramed(menu.Stage, "you stand in the new map");

            Click("GO");
            Assert.IsTrue(menu.Starting, "GO starts a countdown");
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "back to the lobby while it starts");
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Starting"), "the countdown shows under the tabs");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.AreEqual("Duos", Match.Mode);
            Assert.AreEqual(other, Session.MapId);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount, "you came along");
            Assert.AreEqual(4, joins.Players.Count, "bots fill the practice seats");

            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual(LobbyMenu.Practice, LobbyMenu.Instance.Queue);
            Assert.AreEqual("Duos", LobbyMenu.Instance.Mode, "the lobby remembers the mode");
            Assert.AreEqual(other, LobbyMenu.Instance.Map);
            CollectionAssert.AreEqual(new[] { other }, LobbyMenu.Instance.Maps.Ticked, "and the ticked houses");
        }

        [UnityTest]
        public IEnumerator TheControllerThatPressesGoPlaysTheMatch()
        {
            yield return OpenLobby();
            Assert.AreSame(Find(LobbyHomePageView.PlayButton).gameObject, Selected, "a controller starts on PLAY");
            yield return Press(GamepadButton.South);
            Assert.AreEqual(LobbyMenu.Play, LobbyMenu.Instance.Current, "A on PLAY opens the Play section");
            Assert.IsFalse(LobbyMenu.Instance.Starting);
            Assert.AreSame(Find("Mode " + LobbyMenu.Instance.Mode).gameObject, Selected, "and lands on the chosen mode");
            yield return PressGo();
            Assert.IsTrue(LobbyMenu.Instance.Starting, "A presses Next, then GO");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            yield return null;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount);
            Assert.IsTrue(joins.Players.Any(p => p.Binding is GamepadBinding g && g.Pad == pad), "the pad that pressed GO plays");
            Assert.IsFalse(joins.Players.Any(p => p.Binding is DesktopBinding), "the keyboard and mouse don't take a seat as well");
        }

        [UnityTest]
        public IEnumerator ACouchPlayerJoinsThePartyAndComesAlong()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Party");
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "the party panel says how to join");
            yield return Press(GamepadButton.Start);
            Assert.AreEqual(2, menu.PartySize, "Start on a controller takes a seat");
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Seat 2"), "the party panel shows player 2");
            StringAssert.Contains("2 players + 2 bots", menu.Describe());
            yield return Press(GamepadButton.Start);
            Assert.AreEqual(2, menu.PartySize, "pressing Start again doesn't take a second seat");

            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Click("GO");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            yield return null;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(2, joins.HumanCount, "the couch player came along");
            Assert.IsTrue(joins.Players.Any(p => p.Binding is DesktopBinding), "you play on the keyboard and mouse");
            Assert.IsTrue(joins.Players.Any(p => p.Binding is GamepadBinding g && g.Pad == pad), "player 2 plays on their controller");
            Assert.AreEqual(4, joins.Players.Count, "bots fill the other seats");

            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual(2, LobbyMenu.Instance.PartySize, "the party is still together back home");
            yield return Press(GamepadButton.Select);
            Assert.AreEqual(1, LobbyMenu.Instance.PartySize, "Select leaves the party");
        }

        [UnityTest]
        public IEnumerator APartyHasFourSeatsAndAnyoneCanLeave()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Party");
            menu.Join(new KeyboardBinding(KeyboardBinding.Side.Right));
            menu.Join(new ScriptedBinding());
            menu.Join(new ScriptedBinding());
            menu.Join(new ScriptedBinding());
            Assert.AreEqual(LobbyMenu.PartyMax, menu.PartySize, "four seats at most");
            StringAssert.Contains("4 players", menu.Describe());
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "no join hint with every seat taken");
            menu.Choose(mode: "Duos");
            StringAssert.Contains("2 v 2", menu.Describe());

            Click("Leave 2");
            yield return null;
            Assert.AreEqual(3, menu.PartySize);
            Assert.IsFalse(menu.Party.Any(b => b is KeyboardBinding), "the keyboard's right half left");
            StringAssert.Contains("3 players and a bot", menu.Describe());
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "a free seat shows how to join again");
        }

        [UnityTest]
        public IEnumerator AControllerCanCancelAndStepBack()
        {
            LobbyMenu.StartDelay = 5f;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            yield return Press(GamepadButton.South);
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "A on PLAY opens the Play section");
            yield return PressGo();
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "GO hides, so the controller lands on CANCEL");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menu.Starting, "B cancels");
            Assert.AreSame(Find(LobbyHomePageView.PlayButton).gameObject, Selected, "and the controller is back on PLAY");

            Click("Settings");
            yield return null;
            Click("Quit");
            yield return null;
            Assert.AreSame(Find("STAY").gameObject, Selected);
            Assert.AreEqual(Navigation.Mode.Explicit, Find("STAY").navigation.mode, "a controller can't wander out of the dialog");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Quit dialog"), "B closes the dialog");
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "and only the dialog");
            Assert.AreSame(Find("Quit").gameObject, Selected, "focus goes back to the power button");
            yield return Press(GamepadButton.East);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "B again goes home");
        }

        [UnityTest]
        public IEnumerator RenamingChangesTheBadgeAndOldLooksAreSaved()
        {
            var outfit = GameConfig.Current.Wardrobe.Default.Clone();
            outfit.Colours["Top"] = "grape";
            PlayerPrefs.SetString(LobbyMenu.OutfitKey, outfit.Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Assert.AreEqual("grape", menu.Outfit.ColourOf("Top"));
            Assert.IsTrue(MatchTally.LoadCareer().Owns("colour", "grape"), "a colour worn before the shop is saved as yours");

            menu.Career.Name = "Zed";
            menu.SaveCareer();
            Click("Party");
            var badge = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Badge");
            Assert.AreEqual("Z", badge.GetComponentInChildren<TMPro.TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator CancelKeepsYouInTheLobby()
        {
            LobbyMenu.StartDelay = .5f;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click(LobbyHomePageView.PlayButton);
            yield return null;
            Click("GO");
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "a keyboard lands on CANCEL");
            Click("CANCEL");
            Assert.IsFalse(menu.Starting);
            Assert.AreSame(Find(LobbyHomePageView.PlayButton).gameObject, Selected, "and back on PLAY after");
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreSame(menu, LobbyMenu.Instance);
            Find(LobbyHomePageView.PlayButton);
        }

        [UnityTest]
        public IEnumerator MatchmakingSaysWhyItCannotStartYet()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            menu.Choose(LobbyMenu.Matchmaking, LobbyMenu.RoomMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.IsFalse(menu.Page<PlayPage>().Root.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Mode ")),
                "a stale queue never presents house cards as online modes");
            Assert.IsFalse(Find("GO").interactable, "House modes cannot launch an unsupported online session");
            menu.Go();
            Assert.IsFalse(menu.Starting);
            StringAssert.Contains("House modes are played on this screen", menu.Messages[0]);
            StringAssert.Contains("choose " + Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMPro.TMP_Text>().text, menu.Messages[0]);
            StringAssert.Contains("choose Online", menu.Messages[0]);
            Assert.IsFalse(menu.Messages[0].Contains("Online Play"), "the message names the control that exists");
            Click("Home");
            yield return null;
            Assert.IsTrue(Find(LobbyHomePageView.PlayButton).interactable, "Home PLAY only opens the Play section, so it never blocks");
            Assert.IsTrue(Find("PLAY").interactable, "you can still change the queue from the primary navigation");
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.IsTrue(Find("GO").interactable, "choosing a playable queue enables GO again");
        }

        [UnityTest]
        public IEnumerator LoadoutAndShopChangeWhatYouWear()
        {
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 500 }.Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return null;
            Click("Category Top");
            yield return null;
            Click("Colour mint");
            yield return null;
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"));
            Assert.AreEqual("mint", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).ColourOf("Top"), "saved");

            string savedLook = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            Click("Colour sky");
            yield return null;
            var loadout = menu.Page<LoadoutPage>();
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "a colour you don't own is tried on where you are");
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"), "trying on wears nothing");
            Assert.AreEqual(savedLook, PlayerPrefs.GetString(LobbyMenu.OutfitKey), "and saves nothing");
            Assert.AreEqual("sky", loadout.TryOn.ColourOf("Top"), "the avatar keeps it on");

            Click("Buy colour:sky");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins, "Sky costs 120");
            Assert.IsTrue(menu.Career.Owns("colour", "sky"));
            Assert.AreEqual(380, MatchTally.LoadCareer().Coins, "the purchase is saved");
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "buying keeps you on the page");
            Assert.IsNull(loadout.TryOn, "nothing is left to buy");
            Assert.AreEqual("sky", menu.Outfit.ColourOf("Top"), "what you tried on is worn once it is yours");
            Assert.AreEqual("sky", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).ColourOf("Top"), "and saved");

            menu.Open(LobbyMenu.Shop);
            yield return null;
            Click("Shop category gear");
            Click("Buy skin:Arcade");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins, "Arcade costs 400");
            Assert.IsFalse(menu.Career.Owns("skin", "Arcade"));
        }

        [UnityTest]
        public IEnumerator TheLockerKeepsItsCompleteScrollableChoicesAndCharacterPreview()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return new WaitForSeconds(1f);
            Canvas.ForceUpdateCanvases();
            var loadout = menu.Page<LoadoutPage>();
            var wardrobe = GameConfig.Current.Wardrobe;
            var panel = loadout.Root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Panel");
            Assert.AreEqual(LoadoutPage.Width, panel.rect.width, 1f, "the saved customization width");
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float panelLeft = corners[0].x;

            var rail = loadout.Root.GetComponentsInChildren<RectTransform>().Single(r => r.name == LoadoutPage.RailName);
            var categories = rail.GetComponentsInChildren<Button>();
            CollectionAssert.AreEqual(LoadoutPage.RailOrder.Select(slot => LoadoutPage.CategoryPrefix + slot).ToArray(),
                categories.Select(category => category.name).ToArray(), "ten categories: the head column, then the body column");
            Assert.AreEqual(LoadoutPage.RailRows * LoadoutPage.RailColumns, categories.Length);
            var spots = new Vector3[categories.Length];
            for (int i = 0; i < categories.Length; i++)
            {
                ((RectTransform)categories[i].transform).GetWorldCorners(corners);
                spots[i] = (corners[0] + corners[2]) * .5f;
                Assert.GreaterOrEqual(corners[0].x, -.5f, categories[i].name + " is on the screen");
                Assert.GreaterOrEqual(corners[0].y, -.5f, categories[i].name + " is on the screen");
                Assert.LessOrEqual(corners[2].y, Screen.height + .5f, categories[i].name + " is on the screen");
                Assert.LessOrEqual(corners[2].x, panelLeft + .5f, categories[i].name + " sits left of the panel");
                Assert.IsFalse(categories[i].GetComponentsInChildren<TMPro.TMP_Text>().Any(), categories[i].name + " is an icon, not a word");
                Assert.IsTrue(categories[i].GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("Icon ")), categories[i].name + " has its icon");
                if (i % LoadoutPage.RailRows == 0) continue;
                Assert.AreEqual(spots[i - 1].x, spots[i].x, .5f, categories[i].name + " continues its column");
                Assert.Less(spots[i].y, spots[i - 1].y, categories[i].name + " sits under the one before");
            }
            Assert.Less(spots[0].x, spots[LoadoutPage.RailRows].x, "two columns side by side");
            Assert.AreEqual(spots[0].y, spots[LoadoutPage.RailRows].y, .5f, "that start level");
            rail.GetWorldCorners(corners);
            var cam = menu.Stage.Camera;
            Assert.Less(cam.WorldToViewportPoint(menu.Stage.Spot).x * cam.pixelWidth, corners[0].x, "you stand in the room left of the rail");

            foreach (string slot in LoadoutPage.RailOrder)
            {
                Click(LoadoutPage.CategoryPrefix + slot);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(slot, loadout.Category);
                var list = loadout.Root.GetComponentsInChildren<ScrollRect>().Single();
                Assert.IsTrue(list.vertical && !list.horizontal, slot + " scrolls one way");
                Assert.IsNotNull(list.verticalScrollbar, slot + " shows its scroll position");
                var tiles = list.content.GetComponentsInChildren<Button>();
                CollectionAssert.AreEqual(Dressing.Choices(wardrobe, slot).Select(choice => LoadoutPage.TileName(slot, choice)).ToArray(),
                    tiles.Select(tile => tile.name).ToArray(), slot + " offers every choice as a tile");
                Assert.IsFalse(tiles.Any(tile => tile.name == "No Hair" || tile.name == "No Eyes"), "hair and eyes are always chosen");
                var places = new Vector3[tiles.Length];
                for (int i = 0; i < tiles.Length; i++)
                {
                    var rect = (RectTransform)tiles[i].transform;
                    Assert.AreEqual(rect.rect.width, rect.rect.height, .5f, tiles[i].name + " is square");
                    places[i] = rect.TransformPoint(rect.rect.center);
                    if (i % LoadoutPage.Columns != 0)
                    {
                        Assert.AreEqual(places[i - 1].y, places[i].y, .5f, tiles[i].name + " continues its row");
                        Assert.Greater(places[i].x, places[i - 1].x, tiles[i].name + " sits right of the one before");
                    }
                    else if (i > 0)
                    {
                        Assert.AreEqual(places[0].x, places[i].x, .5f, tiles[i].name + " starts a row of three");
                        Assert.Less(places[i].y, places[i - LoadoutPage.Columns].y, tiles[i].name + " starts the next row");
                    }
                    string piece = tiles[i].name == LoadoutPage.NoPrefix + slot ? null : tiles[i].name;
                    var art = piece == null ? null : Wreckabulary.UI.WardrobeArt.For(slot, piece);
                    var picture = tiles[i].GetComponentsInChildren<Image>().FirstOrDefault(image => image.name == LoadoutPage.PiecePicture);
                    var words = tiles[i].GetComponentsInChildren<TMPro.TMP_Text>().Where(text => text.transform.parent.name != LoadoutPage.PricePill).ToArray();
                    if (art)
                    {
                        Assert.IsNotNull(picture, tiles[i].name + " shows its picture");
                        Assert.AreSame(art, picture.sprite, tiles[i].name + " shows its own picture");
                        Assert.IsEmpty(words, tiles[i].name + " is a picture, not a word");
                    }
                    else if (piece != null)
                    {
                        Assert.IsNull(picture, tiles[i].name);
                        Assert.IsTrue(tiles[i].GetComponentsInChildren<RectTransform>().Any(r => r.name == LoadoutPage.PieceFallback), tiles[i].name + " falls back to an icon");
                        Assert.AreEqual(wardrobe.NameOf(piece), words.Single().text, tiles[i].name + " is named when it has no picture");
                    }
                    else Assert.IsEmpty(words, tiles[i].name + " is an icon, not the word None");
                }
            }
            Assert.IsFalse(loadout.Root.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Finish ")), "gear finishes live on the Equipment tab");
            Assert.IsFalse(loadout.Root.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Slot ")), "categories switch in place without modal slot pages");

            bool Marked(string tile, string mark) => Find(tile).GetComponentsInChildren<RectTransform>().Any(r => r.name == mark);
            foreach (var (slot, piece) in new[] { ("Hair", "HairSwept"), ("Eyes", "EyesSparkle") })
            {
                Click(LoadoutPage.CategoryPrefix + slot);
                yield return null;
                Assert.IsNull(menu.Outfit.PieceIn(slot), "the default outfit stores no " + slot);
                Assert.IsTrue(Marked(piece, LoadoutPage.CheckMark), piece + " is shown as chosen, not as None");
                Assert.AreSame(Find(piece).gameObject, loadout.First.gameObject, "and is where a controller starts");
                string before = PlayerPrefs.GetString(LobbyMenu.OutfitKey, "");
                Click(piece);
                yield return null;
                Assert.IsNull(menu.Outfit.PieceIn(slot), "choosing what you already wear changes nothing");
                Assert.AreEqual(before, PlayerPrefs.GetString(LobbyMenu.OutfitKey, ""), "and writes nothing");
            }
            foreach (string slot in new[] { "Bottoms", "Gloves", "Footwear" })
            {
                Click(LoadoutPage.CategoryPrefix + slot);
                yield return null;
                var list = loadout.Root.GetComponentsInChildren<ScrollRect>().Single();
                Assert.AreEqual(1, list.content.GetComponentsInChildren<Button>().Length, slot + " has one piece");
                Assert.AreEqual(wardrobe.Palettes[slot].Count, loadout.Root.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith(LoadoutPage.ColourPrefix)),
                    slot + " shows its colours under the tile");
            }
            AssertNoEllipsis(loadout.Root, "the wardrobe");

            Click("RECIPES");
            yield return null;
            Assert.IsTrue(loadout.ShowingRecipes);
            AssertNoEllipsis(loadout.Root, "the recipe book");
            Click("LOCKER");
            yield return null;
            Click("Done");
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "Done closes the wardrobe");
        }

        [UnityTest]
        public IEnumerator LockerLookAccessoriesHoodAndPartColours()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Face"), "no glasses to start");
            Click("Category Face");
            yield return null;
            Click("Glasses");
            yield return null;
            Assert.AreEqual("Glasses", menu.Outfit.PieceIn("Face"), "the round specs go on");
            Assert.AreEqual("Glasses", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).PieceIn("Face"), "saved");
            Assert.AreSame(Find("Glasses").gameObject, Selected, "a keyboard or controller stays on the choice");
            Click("HeartShades");
            yield return null;
            Assert.AreEqual("HeartShades", menu.Outfit.PieceIn("Face"), "one face piece at a time");
            Click("No Face");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Face"), "No Face takes them off");
            Click("Category Hair");
            yield return null;
            Click("HairBuns");
            yield return null;
            Assert.AreEqual("HairBuns", menu.Outfit.PieceIn("Hair"));
            Click("Category Eyes");
            yield return null;
            Click("EyesJelly");
            yield return null;
            Assert.AreEqual("EyesJelly", menu.Outfit.PieceIn("Eyes"));
            Assert.AreEqual("HairBuns", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).PieceIn("Hair"), "hair is saved with the look");

            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Click("Category Top");
            yield return null;
            Click("Crewneck");
            yield return null;
            Assert.AreEqual("Crewneck", menu.Outfit.PieceIn("Top"));
            Assert.IsNull(menu.Outfit.PieceIn("Headwear"), "the hood only fits the hoodie, so it comes off");
            Assert.IsTrue(menu.Messages.Contains("Hood taken off."), "and the page says so");
            Click("Category Headwear");
            yield return null;
            Click("Hood");
            yield return null;
            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Assert.AreEqual("Hoodie", menu.Outfit.PieceIn("Top"), "and the hood brings the hoodie back, as on the web");
            Assert.IsTrue(menu.Messages.Contains("Hoodie put on to go with the Hood."), "and the page says so");

            Click("Category Bottoms");
            yield return null;
            Click("Colour navy");
            yield return null;
            Assert.AreEqual("navy", menu.Outfit.ColourOf("Bottoms"));
            Assert.AreEqual("tomato", menu.Outfit.ColourOf("Top"), "only the part picked changes");

            Click("RECIPES");
            yield return null;
            string worn = menu.Outfit.Serialize(), savedLook = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            Click("Finish Arcade");
            yield return null;
            var loadout = menu.Page<LoadoutPage>();
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "a gear style you don't own is tried on where you are");
            Assert.AreEqual(worn, menu.Outfit.Serialize(), "trying on wears nothing");
            Assert.AreEqual(savedLook, PlayerPrefs.GetString(LobbyMenu.OutfitKey), "and saves nothing");
            Assert.IsTrue(loadout.ShowingRecipes, "still on the Equipment tab");
            Assert.IsTrue(menu.Stage.IsHoldingItem, "the avatar holds the gear in that style");
            Assert.IsTrue(Find("Buy skin:Arcade").gameObject.activeInHierarchy, "with what buys it on the same page");
            Assert.AreSame(Find("Finish Arcade").gameObject, Selected, "a keyboard or controller stays on the style");
            AssertNoEllipsis(loadout.Root, "the equipment tab");
        }

        [UnityTest]
        public IEnumerator TheShopOpensTheCategoryAndShowsTheOfferYouCameFor()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.OpenShop("colour:charcoal");
            yield return null;
            yield return null;
            var buy = Find("Buy colour:charcoal");
            Assert.AreEqual(ShopPage.Colours, menu.Page<ShopPage>().Category);
            Assert.AreSame(buy.gameObject, Selected, "a keyboard or controller lands on what buys it");
            var list = buy.GetComponentInParent<ScrollRect>();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(list.viewport, buy.transform);
            Assert.GreaterOrEqual(bounds.min.y, list.viewport.rect.yMin - .5f, "and shows it whole");
            Assert.LessOrEqual(bounds.max.y, list.viewport.rect.yMax + .5f);
        }

        [TestCase("APPLE", "Restore 30 HP")]
        [TestCase("WATER", "Restore 18 HP")]
        [TestCase("CAKE", "Restore 50 HP")]
        [TestCase("SODA", "speed")]
        [TestCase("SHIELD", "All-around")]
        [TestCase("FAN", "Forward gust")]
        [TestCase("CLOCK", "Slowing field")]
        [TestCase("PIE", "One throw")]
        public void RecipeDescriptionExplainsItsActualEffect(string id, string expected)
        {
            StringAssert.Contains(expected, LoadoutPage.Blurb(GameConfig.Current.Items.Get(id)));
        }

        [UnityTest]
        public IEnumerator RecipeDetailFollowsTheCardYouPick()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return null;
            Click("RECIPES");
            yield return null;
            var item = GameConfig.Current.Items.Enabled.Last();
            Click("Recipe " + item.Id);
            yield return null;
            Assert.AreEqual(item.Id, menu.Page<LoadoutPage>().Pinned);
            var detail = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Recipe detail");
            Assert.IsTrue(detail.GetComponentsInChildren<RawImage>().Any(r => r.name == "Art " + item.Id), "the strip shows the card you picked");
            Assert.AreEqual(LoadoutPage.Blurb(item), detail.GetComponentsInChildren<TMPro.TMP_Text>().Single(t => t.name == "Blurb").text);
            Assert.AreSame(Find("Recipe " + item.Id).gameObject, Selected, "a keyboard or controller stays on the card");
        }

        [UnityTest]
        public IEnumerator WorkshopOpensOverTheLobbyAndComesBack()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            var lobbyLook = menu.Stage.Camera.backgroundColor;
            var daylight = Wreckabulary.Art.EnvironmentLighting.Active;
            Assert.IsNotNull(daylight);
            float sunIntensity = daylight.Sun.intensity;
            Click(PlayPage.CreateHouse);
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the workshop to open");
            yield return null;
            var menuVisibility = menu.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, menuVisibility.alpha, "the lobby steps aside");
            Assert.IsFalse(menuVisibility.interactable || menuVisibility.blocksRaycasts,
                "the hidden lobby cannot intercept workshop controls");
            Assert.IsTrue(menu.gameObject.activeInHierarchy, "the lobby stays available for the return transition");
            Assert.IsFalse(Find(LobbyHomePageView.PlayButton).IsInteractable(), "the hidden lobby button cannot receive workshop input");
            Assert.AreSame(daylight, Wreckabulary.Art.EnvironmentLighting.Active, "workshop suspension preserves the scene's daylight controller");
            Assert.AreEqual(sunIntensity, daylight.Sun.intensity);
            Assert.LessOrEqual(daylight.SelectedLightCount, daylight.LightBudget);
            var cam = Camera.main;
            Assert.IsFalse(cam.clearFlags == CameraClearFlags.SolidColor && cam.backgroundColor == lobbyLook,
                "the workshop shows the hub's own sky, not the lobby's");
            var sky = menu.Stage.GetComponentsInChildren<Canvas>(true).Single(c => c.name == "Lobby sky");
            Assert.IsFalse(sky.enabled, "the lobby's sky steps aside too");
            Assert.IsFalse(menu.Stage.GetComponentsInChildren<Volume>(true).Single(v => v.name == "Lobby look").enabled, "and its grade");

            CreativeWorkshop.Instance.Close();
            yield return null;
            yield return null;
            Assert.AreEqual(1f, menuVisibility.alpha, "the lobby comes back");
            Assert.IsTrue(menuVisibility.interactable && menuVisibility.blocksRaycasts,
                "the restored lobby accepts pointer and controller input");
            Assert.IsTrue(Find(LobbyHomePageView.PlayButton).IsInteractable(), "the lobby button works again after the workshop closes");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            AssertFramed(menu.Stage, "the camera is yours again");
            Assert.AreEqual(lobbyLook, menu.Stage.Camera.backgroundColor, "with the lobby's sky");
            Assert.AreSame(daylight, Wreckabulary.Art.EnvironmentLighting.Active);
            Assert.AreEqual(sunIntensity, daylight.Sun.intensity);
            Assert.IsTrue(sky.enabled, "which comes back");
            Find("PLAY");
            Assert.AreSame(Find(LobbyHomePageView.PlayButton).gameObject, Selected, "PLAY is back, with a controller on it");
        }

        [UnityTest]
        public IEnumerator TheHomeBuilderPlaysItsHouseInDibsOrDuos()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            Click(PlayPage.CreateHouse);
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the workshop to open");
            yield return null;
            var workshop = CreativeWorkshop.Instance;
            string map = menu.Map;
            workshop.GetComponentsInChildren<TMPro.TMP_InputField>(true).Single(field => field.name == "SOFA TABLE PLANT").text = "TABLE PLANT";
            workshop.GetComponentsInChildren<Button>(true).Single(button => button.name == "Place words in room").onClick.Invoke();
            workshop.GetComponentsInChildren<Button>(true).Single(button => button.name == "Play").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsFalse(CreativeWorkshop.Instance, "the workshop closes");
            Assert.IsTrue(HomeStorage.ForGame().TryLoad(map, out var saved, out var error), error);
            CollectionAssert.AreEquivalent(new[] { "TABLE", "PLANT" }, saved.Props.Select(prop => prop.Word), "Play saves the house first");
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.IsTrue(HomeDesigner.Playable(menu.Mode), "a saved home starts in a mode that can play it");
            Assert.AreEqual(map, menu.SavedHome);
            Assert.AreSame(Find(PlayPage.SavedHomeCard + map).gameObject, Selected, "the saved home is ready to play");
            Click("Mode Duos");
            yield return null;
            Assert.AreEqual(map, menu.SavedHome, "and Duos plays it too");
            Click("Mode MovingOut");
            yield return null;
            Assert.IsNull(menu.SavedHome, "objective modes keep the original house");
            Assert.IsFalse(menu.Page<PlayPage>().Root.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(PlayPage.SavedHomeCard)));
        }
    }
}
