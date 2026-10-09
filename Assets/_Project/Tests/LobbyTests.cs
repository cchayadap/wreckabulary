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
        static readonly string[] Keys = new[] { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey }
            .Concat(GraphicsOptions.Keys).ToArray();
        string[] saved;
        bool hadVolume, hadVsync;
        float savedVolume, savedDelay;
        int savedVsync, savedVsyncCount;
        Gamepad pad;
        Keyboard keys;
        InputSettings.EditorInputBehaviorInPlayMode? keysRoute;
        InputSettings.BackgroundBehavior keysFocus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            hadVolume = PlayerPrefs.HasKey(LobbyMenu.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(LobbyMenu.VolumeKey, 1f);
            hadVsync = PlayerPrefs.HasKey(LobbyMenu.VsyncKey);
            savedVsync = PlayerPrefs.GetInt(LobbyMenu.VsyncKey, 0);
            savedVsyncCount = QualitySettings.vSyncCount;
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            GraphicsOptions.Load();
            savedDelay = LobbyMenu.StartDelay;
            LobbyMenu.StartDelay = .3f;
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
            PlayerPrefs.Save();
            QualitySettings.vSyncCount = savedVsyncCount;
            GraphicsOptions.Load();
            AudioListener.volume = savedVolume;
            LobbyMenu.StartDelay = savedDelay;
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
            var button = LobbyMenu.Instance.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"a '{name}' button is showing");
            return button;
        }

        static void Click(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.interactable, $"'{name}' can be pressed");
            button.onClick.Invoke();
        }

        static float ScreenX(Component c)
        {
            var rect = (RectTransform)c.transform;
            return rect.TransformPoint(rect.rect.center).x;
        }

        static Transform Backdrop(LobbyStage stage) =>
            stage.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Lobby backdrop"));

        static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector2 p = new Vector2(point.x, point.z), a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            return (p - (a + (b - a) * t)).magnitude;
        }

        static float Height(string name, System.Type layout)
        {
            Canvas.ForceUpdateCanvases();
            var rect = LobbyMenu.Instance.GetComponentsInChildren<RectTransform>().Single(r => r.name == name && r.GetComponent(layout));
            return rect.rect.height;
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
            Assert.AreEqual(Session.MapId, stage.Map, "the backdrop is the selected map");
            Assert.IsNotNull(Backdrop(stage), "the map is built behind you");
            Assert.IsNotNull(stage.Avatar, "your avatar stands in it");
            Assert.Greater(stage.Spot.magnitude, 100f, "built well away from the hub room");
            Assert.AreSame(Camera.main, stage.Camera);
            Assert.IsFalse(stage.Camera.orthographic);
            AssertFramed(stage, "the camera looks at you");
            var eye = stage.Camera.transform.position;
            foreach (var rug in Backdrop(stage).GetComponentsInChildren<Transform>().Where(t => t.name is "RUG" or "Rug" || t.name.EndsWith("_Rug")))
                Assert.Greater(DistanceToSegment(rug.position, eye, stage.Spot), 2f, $"{rug.name} at {rug.position} is clear of the view");
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
            CollectionAssert.AreEquivalent(new[] { "Home", "Settings", "Quit", "Leaderboard", "Shop", "LOADOUT", "PLAY", "CAREER", "Party" }, names,
                "no sound or help buttons: those live in Settings");

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
            Assert.AreSame(Find("Party").transform.parent, Find("Shop").transform.parent, "the coin balance is the shop entry beside the party");
            Assert.Greater(ScreenX(Find("Shop")), ScreenX(Find("Leaderboard")));
            Assert.Greater(ScreenX(Find("Party")), ScreenX(Find("CAREER")), "the party chip is on the right, by the coins");
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
        public IEnumerator ThePartyChipOpensThePartyUnderIt()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var panel = menu.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Party and friends");
            Assert.IsFalse(panel.gameObject.activeSelf, "the party stays out of the way until asked for");
            StringAssert.Contains("1/4", Find("Party").GetComponentInChildren<TMPro.TMP_Text>().text, "the chip counts the party");
            Click("Party");
            yield return null;
            Assert.IsTrue(panel.gameObject.activeSelf, "the chip opens it");
            Assert.IsTrue(Find("Party").GetComponent<LobbyTab>().On, "and shows it is open");
            Assert.Greater(ScreenX(panel), Screen.width * .7f, "under the chip, on the right");
            Click("Party");
            yield return null;
            Assert.IsFalse(panel.gameObject.activeSelf, "a second click closes it");

            Click("Party");
            yield return null;
            Click("LOADOUT");
            yield return null;
            Assert.IsFalse(panel.gameObject.activeSelf, "a page opens over where it was");
            var corners = new Vector3[4];
            ((RectTransform)Find("RECIPES").transform).GetWorldCorners(corners);
            float tabsRight = corners[2].x;
            ((RectTransform)Find("Close page").transform).GetWorldCorners(corners);
            Assert.Less(tabsRight, corners[0].x, "the loadout's tabs end before the page's close button");
            Click("Party");
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "the party belongs to the lobby screen");
            Assert.IsTrue(panel.gameObject.activeSelf);
            yield return Tap(Key.Escape);
            Assert.IsFalse(panel.gameObject.activeSelf, "Esc closes it first");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreSame(Find("Party").gameObject, Selected, "with a keyboard or controller back on the chip");

            menu.Join(new ScriptedBinding());
            StringAssert.Contains("2/4", Find("Party").GetComponentInChildren<TMPro.TMP_Text>().text);
            Click("Party");
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>().Concat(Find("Party").GetComponentsInChildren<TMPro.TMP_Text>()))
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated, $"\"{text.text}\" fits without an ellipsis");
            }
        }

        [UnityTest]
        public IEnumerator TheMatchDockSitsBottomRightWithABigGo()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Canvas.ForceUpdateCanvases();
            var dock = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Next match");
            var go = Find("GO");
            Assert.IsTrue(go.transform.IsChildOf(dock), "GO is in the dock");
            Assert.IsFalse(dock.GetComponentsInChildren<Button>().Any(button => button.name == "CHANGE"), "mode selection now lives in PLAY");
            Assert.IsTrue(Find("Change house").transform.IsChildOf(dock), "and the house");
            Assert.Greater(ScreenX(dock), Screen.width * .6f, "bottom right, as on the web");
            var corners = new Vector3[4];
            dock.GetWorldCorners(corners);
            Assert.Less(corners[0].y, Screen.height * .1f, "down at the bottom");
            Assert.AreEqual(64f, ((RectTransform)go.transform).rect.height, 1f, "a compact START");
            Assert.AreEqual("START", go.GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.Less(Find("Choose mode").transform.position.y, go.transform.position.y);
            Assert.Less(Find("Change house").transform.position.y, Find("Choose mode").transform.position.y);
            Assert.AreEqual(300f, dock.rect.width, 1f);
            Click("Change house");
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "a pick opens PLAY to change it");
        }

        [UnityTest]
        public IEnumerator PlayIsTheWebsTwoPickersOnOnePage()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            var page = menu.Page<PlayPage>().Root;
            var posters = page.GetComponentsInChildren<Button>().Where(b => b.name.StartsWith("Mode ")).Select(b => b.name.Substring(5)).ToArray();
            CollectionAssert.AreEqual(new[] { "Dibs", "Duos", "MovingOut", "MovingDay", LobbyMenu.TutorialMode }, posters,
                "the web's posters in its order, with Play & learn in practice too");
            Assert.AreEqual(GameConfig.Current.Houses.Count, page.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Map ")), "every house");
            Assert.IsTrue(page.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Rays"), "posters wear the web's rays");
            Assert.IsTrue(page.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Start"), "plans mark where players start");
            AssertNoEllipsis(page, "PLAY");
            Assert.AreEqual("Double trouble", LobbyMenu.ModeName("Duos"), "the web's names");

            Click("Mode " + LobbyMenu.TutorialMode);
            yield return null;
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "practice keeps its queue");
            Assert.IsFalse(page.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Map ")), "the tutorial has its own room");
            Assert.IsTrue(page.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Tutorial room"), "and says so");
            StringAssert.Contains("Play & learn", menu.Describe());
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
            Click("Mode " + LobbyMenu.WorkshopMode);
            yield return null;
            Assert.IsTrue(HomeDesigner.Supports(menu.Map), "the workshop took you to a house it builds in");
            Assert.IsFalse(Find("Map flat").interactable, "the others are off");
            Assert.IsTrue(Find("Map courtyard").interactable);
            StringAssert.Contains(GameConfig.Current.HouseFor(menu.Map).Name, menu.Describe(), "and GO says which");
        }

        [UnityTest]
        public IEnumerator TheDockOpensPlayOnWhatItChanges()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            Assert.AreSame(Find("Mode " + menu.Mode).gameObject, Selected, "a keyboard or controller lands on the mode");
            menu.Close();
            yield return null;
            Click("Change house");
            yield return null;
            Assert.AreSame(Find("Map " + menu.Map).gameObject, Selected, "and on the house");
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
            foreach (string tab in new[] { "general", "graphics", "controls", "how" })
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
                    Assert.IsTrue(root.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "PRESS A KEY…"));
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
            Assert.IsTrue(dialog.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "LEAVE THE HOUSE PARTY?"));
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
            Assert.AreEqual(330f, Height("Modes", typeof(LobbyFit)), 1f, "the mode posters");
            Assert.AreEqual(280f, Height("Houses", typeof(LobbyFit)), 1f, "the house cards");
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
            Click("Map " + other);
            yield return null;
            Assert.AreEqual(other, menu.Map);
            Assert.AreEqual(other, Session.MapId);
            Assert.AreEqual(other, menu.Stage.Map, "the backdrop follows the map");
            Assert.IsTrue(!oldBackdrop, "the old map is taken down");
            StringAssert.Contains(GameConfig.Current.HouseFor(other).Name, Backdrop(menu.Stage).name);
            StringAssert.Contains(GameConfig.Current.HouseFor(other).Name, menu.Messages[0]);
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
        }

        [UnityTest]
        public IEnumerator TheControllerThatPressesGoPlaysTheMatch()
        {
            yield return OpenLobby();
            Assert.AreSame(Find("GO").gameObject, Selected, "a controller starts on GO");
            yield return Press(GamepadButton.South);
            Assert.IsTrue(LobbyMenu.Instance.Starting, "A presses GO");
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
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "GO hides, so the controller lands on CANCEL");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menu.Starting, "B cancels");
            Assert.AreSame(Find("GO").gameObject, Selected, "and the controller is back on GO");

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
            Click("GO");
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "a keyboard lands on CANCEL");
            Click("CANCEL");
            Assert.IsFalse(menu.Starting);
            Assert.AreSame(Find("GO").gameObject, Selected, "and back on GO after");
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreSame(menu, LobbyMenu.Instance);
            Find("GO");
        }

        [UnityTest]
        public IEnumerator MatchmakingSaysWhyItCannotStartYet()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Find("Mode " + LobbyMenu.RoomMode);
            Assert.IsFalse(Find("GO").interactable, "GO is off until online play exists");
            menu.Go();
            Assert.IsFalse(menu.Starting);
            StringAssert.Contains("online or LAN", menu.Messages[0]);
            Click("Home");
            yield return null;
            Assert.IsFalse(Find("GO").interactable);
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
            Click("Part Top");
            yield return null;
            Click("Colour mint");
            yield return null;
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"));
            Assert.AreEqual("mint", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).ColourOf("Top"), "saved");

            Click("Colour sky");
            yield return null;
            Assert.AreEqual(LobbyMenu.Shop, menu.Current, "a colour you don't own opens the shop");
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"));

            Click("Buy colour:sky");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins);
            Assert.IsTrue(menu.Career.Owns("colour", "sky"));
            Assert.AreEqual(380, MatchTally.LoadCareer().Coins, "the purchase is saved");
            Click("Wear colour:sky");
            yield return null;
            Assert.AreEqual("sky", menu.Outfit.ColourOf("Top"));

            Click("Buy skin:Arcade");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins, "Arcade costs 400");
            Assert.IsFalse(menu.Career.Owns("skin", "Arcade"));
        }

        [UnityTest]
        public IEnumerator TheLockerIsTheWebsOnePagePanel()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return new WaitForSeconds(1f);
            Canvas.ForceUpdateCanvases();
            var loadout = menu.Page<LoadoutPage>();
            var panel = loadout.Root.GetComponentsInChildren<RectTransform>().First(r => r.name == "Panel");
            Assert.AreEqual(LoadoutPage.Width, panel.rect.width, 1f, "the web's narrow locker, not the career page's width");
            foreach (string name in new[] { "Hoodie", "Crewneck", "Cap", "Hood", "No Headwear", "Part Top", "Part Bottoms", "Colour tomato",
                "Extra Face", "Extra Back", "Extra Badge", "Finish Classic", "Finish Candy", "Finish Arcade", "Done" })
                Find(name);
            Assert.IsFalse(loadout.Root.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Slot ")),
                "no list of slots to open first (user, 6 Oct 2026: too messy and content heavy)");
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            var cam = menu.Stage.Camera;
            Assert.Less(cam.WorldToViewportPoint(menu.Stage.Spot).x * cam.pixelWidth, corners[0].x, "you stand in the room left of the panel");
            AssertNoEllipsis(loadout.Root, "the locker");

            Click("RECIPES");
            yield return null;
            Assert.IsTrue(loadout.ShowingRecipes);
            AssertNoEllipsis(loadout.Root, "the recipe book");
            Click("LOCKER");
            yield return null;
            Click("Done");
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "THAT'S MY LOOK closes the locker");
        }

        [UnityTest]
        public IEnumerator LockerExtrasHoodAndPartColours()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Face"), "no glasses to start");
            Click("Extra Face");
            yield return null;
            Assert.AreEqual("Glasses", menu.Outfit.PieceIn("Face"), "the card puts the glasses on");
            Assert.AreEqual("Glasses", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).PieceIn("Face"), "saved");
            Assert.AreSame(Find("Extra Face").gameObject, Selected, "a keyboard or controller stays on the card");
            Click("Extra Face");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Face"), "a second click takes them off");

            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Click("Crewneck");
            yield return null;
            Assert.AreEqual("Crewneck", menu.Outfit.PieceIn("Top"));
            Assert.IsNull(menu.Outfit.PieceIn("Headwear"), "the hood only fits the hoodie, so it comes off");
            Click("Hood");
            yield return null;
            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Assert.AreEqual("Hoodie", menu.Outfit.PieceIn("Top"), "and the hood brings the hoodie back, as on the web");

            Click("Part Bottoms");
            yield return null;
            Click("Colour navy");
            yield return null;
            Assert.AreEqual("navy", menu.Outfit.ColourOf("Bottoms"));
            Assert.AreEqual("tomato", menu.Outfit.ColourOf("Top"), "only the part picked changes");

            Click("Finish Arcade");
            yield return null;
            Assert.AreEqual(LobbyMenu.Shop, menu.Current, "a gear style you don't own opens the shop");
            var shop = menu.Page<ShopPage>();
            Assert.AreEqual("skin:Arcade", shop.Spotlit, "on that style");
            Assert.AreSame(Find("Buy skin:Arcade").gameObject, Selected, "with a keyboard or controller on what buys it");
            AssertNoEllipsis(shop.Root, "the shop");
        }

        [UnityTest]
        public IEnumerator TheShopScrollsToTheOfferYouCameFor()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.OpenShop("colour:charcoal");
            yield return null;
            yield return null;
            var buy = Find("Buy colour:charcoal");
            Assert.AreSame(buy.gameObject, Selected, "a keyboard or controller lands on what buys it");
            var list = buy.GetComponentInParent<ScrollRect>();
            Assert.Greater(list.content.anchoredPosition.y, 0f, "the last shelf is below the fold, so the list scrolls down to it");
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
            Click("Mode " + LobbyMenu.WorkshopMode);
            yield return null;
            var lobbyLook = menu.Stage.Camera.backgroundColor;
            var daylight = Wreckabulary.Art.EnvironmentLighting.Active;
            Assert.IsNotNull(daylight);
            float sunIntensity = daylight.Sun.intensity;
            Click("GO");
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the workshop to open");
            yield return null;
            var menuVisibility = menu.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, menuVisibility.alpha, "the lobby steps aside");
            Assert.IsFalse(menuVisibility.interactable || menuVisibility.blocksRaycasts,
                "the hidden lobby cannot intercept workshop controls");
            Assert.IsTrue(menu.gameObject.activeInHierarchy, "the lobby stays available for the return transition");
            Assert.IsFalse(Find("GO").IsInteractable(), "the hidden lobby button cannot receive workshop input");
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
            Assert.IsTrue(Find("GO").IsInteractable(), "the lobby button works again after the workshop closes");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            AssertFramed(menu.Stage, "the camera is yours again");
            Assert.AreEqual(lobbyLook, menu.Stage.Camera.backgroundColor, "with the lobby's sky");
            Assert.AreSame(daylight, Wreckabulary.Art.EnvironmentLighting.Active);
            Assert.AreEqual(sunIntensity, daylight.Sun.intensity);
            Assert.IsTrue(sky.enabled, "which comes back");
            Find("PLAY");
            Assert.AreSame(Find("GO").gameObject, Selected, "GO is back, with a controller on it");
        }
    }
}
