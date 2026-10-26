using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class LobbySettingsNavigationTests
    {
        static readonly string[] StringKeys = GraphicsOptions.Keys.Concat(new[]
        {
            MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, GameSoundPacks.PreferenceKey,
            KeyBindings.OverridesKey, KeyBindings.LegacyBackupKey
        }).ToArray();
        static readonly string[] IntKeys = { "wv.muted", LobbyMenu.VsyncKey, CombatFeedbackOptions.DamageKey,
            CombatFeedbackOptions.PointsKey, "wv.theme.effects", KeyBindings.InvertKey };
        static readonly string[] FloatKeys = { LobbyMenu.VolumeKey, KeyBindings.SensitivityKey };
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        string[] strings;
        int[] ints;
        float[] floats;
        bool[] hadStrings, hadInts, hadFloats;
        float volume, sensitivity;
        int vsync;
        bool invert;
        string bindings;
        SettingsPage page;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadStrings = StringKeys.Select(PlayerPrefs.HasKey).ToArray(); strings = StringKeys.Select(key => PlayerPrefs.GetString(key)).ToArray();
            hadInts = IntKeys.Select(PlayerPrefs.HasKey).ToArray(); ints = IntKeys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
            hadFloats = FloatKeys.Select(PlayerPrefs.HasKey).ToArray(); floats = FloatKeys.Select(key => PlayerPrefs.GetFloat(key)).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            bindings = DesktopBinding.Shared.Map.SaveBindingOverridesAsJson();
            sensitivity = KeyBindings.Sensitivity; invert = KeyBindings.InvertY;
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "settings ready");
            LobbyMenu.Instance.Open(LobbyMenu.Settings);
            page = LobbyMenu.Instance.Page<SettingsPage>();
            page.Root.anchorMin = page.Root.anchorMax = new Vector2(.5f, .5f);
            page.Root.sizeDelta = new Vector2(1600, 720);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (page != null)
            {
                typeof(SettingsPage).GetField("revertTo", Private).SetValue(page, null);
                typeof(SettingsPage).GetMethod("Stop", Private).Invoke(page, null);
            }
            KeyBindings.Stop();
            yield return TestScenes.Reset();
            DesktopBinding.Shared.Map.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(bindings)) DesktopBinding.Shared.Map.LoadBindingOverridesFromJson(bindings);
            KeyBindings.SetSensitivity(sensitivity); KeyBindings.SetInvertY(invert);
            for (int i = 0; i < StringKeys.Length; i++)
                if (hadStrings[i]) PlayerPrefs.SetString(StringKeys[i], strings[i]); else PlayerPrefs.DeleteKey(StringKeys[i]);
            for (int i = 0; i < IntKeys.Length; i++)
                if (hadInts[i]) PlayerPrefs.SetInt(IntKeys[i], ints[i]); else PlayerPrefs.DeleteKey(IntKeys[i]);
            for (int i = 0; i < FloatKeys.Length; i++)
                if (hadFloats[i]) PlayerPrefs.SetFloat(FloatKeys[i], floats[i]); else PlayerPrefs.DeleteKey(FloatKeys[i]);
            PlayerPrefs.Save(); GraphicsOptions.Load();
            AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
        }

        Button Button(string name) => page.Root.GetComponentsInChildren<Button>().Single(button => button.name == name);
        ScrollRect Scroll() => page.Root.GetComponentInChildren<ScrollRect>();
        void Tab(string id) { page.ShowTab(id); Canvas.ForceUpdateCanvases(); }
        static Bounds RectInSpace(RectTransform target, RectTransform space)
        {
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var bounds = new Bounds(space.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(space.InverseTransformPoint(corners[i]));
            return bounds;
        }

        [UnityTest]
        public IEnumerator GroupsKeepRealOptionsAliasesAndControlsInputLease()
        {
            var names = page.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Settings tab "))
                .Select(button => button.name).ToArray();
            CollectionAssert.AreEqual(new[] { "Settings tab profile", "Settings tab audio", "Settings tab gameplay", "Settings tab graphics",
                "Settings tab controls", "Settings tab help" }, names);
            Tab("general"); Assert.IsNotNull(Button("Open loadout"));
            Assert.IsFalse(page.Root.GetComponentsInChildren<Button>().Any(button => button.name == "Sound on"));
            Tab("audio"); Assert.IsNotNull(Button("Volume more")); Assert.IsNotNull(Button("Reset audio"));
            Tab("gameplay"); Assert.IsNotNull(Button("Damage numbers on")); Assert.IsNotNull(Button("Reduce theme motion off"));
            Tab("controls"); Assert.IsNotNull(Button("Sensitivity more")); Assert.IsTrue(KeyBindings.GameplayBlocked);
            Tab("how"); Assert.IsNotNull(Button("Got it")); Assert.IsNotNull(Scroll());
            yield return TestScenes.WaitUntil(() => !KeyBindings.GameplayBlocked, .5f, "controls close releases gameplay after the input guard");
            Assert.IsFalse(KeyBindings.GameplayBlocked);
            Assert.AreEqual("Settings tab help", EventSystem.current.currentSelectedGameObject.name);
            var rootBefore = Button("Got it");
            Assert.Throws<ArgumentException>(() => page.ShowTab("missing"));
            Assert.AreSame(rootBefore, Button("Got it"), "invalid tab does not destroy current content");
            Tab("profile"); Button("Open loadout").onClick.Invoke();
            Assert.AreEqual(LobbyMenu.Loadout, LobbyMenu.Instance.Current);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingDisplayLeavesContentAndControlsFooterAboveConfirmationOnEveryTab()
        {
            var display = (Screen.fullScreenMode, Screen.width, Screen.height);
            typeof(SettingsPage).GetField("revertTo", Private).SetValue(page, display);
            var timer = LobbyMenu.Instance.gameObject.AddComponent<LobbyCountdown>();
            float deadline = Time.unscaledTime + 600;
            timer.Ends = deadline;
            Action callback = () => Assert.Fail("This layout test must not alter the display.");
            timer.Done = callback;
            typeof(SettingsPage).GetField("timer", Private).SetValue(page, timer);
            foreach (string tab in new[] { "profile", "audio", "gameplay", "graphics", "controls", "help" })
            {
                Tab(tab);
                var content = page.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Tab content");
                var bar = page.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Keep display" && rect.GetComponent<HorizontalLayoutGroup>());
                var space = (RectTransform)content.parent;
                var contentBounds = RectInSpace(content, space);
                var barBounds = RectInSpace(bar, space);
                Assert.GreaterOrEqual(contentBounds.min.y, barBounds.max.y + 11f, tab + " content must leave the confirmation gutter");
                if (tab == "controls")
                {
                    var reset = (RectTransform)Button("Reset controls").transform;
                    var bounds = RectInSpace(reset, space);
                    Assert.GreaterOrEqual(bounds.min.y, barBounds.max.y + 11f, "controls reset remains clear of display confirmation");
                }
                Assert.AreEqual(deadline, timer.Ends); Assert.AreSame(callback, timer.Done);
            }
            Button("Keep display").onClick.Invoke();
            Assert.IsNull(typeof(SettingsPage).GetField("revertTo", Private).GetValue(page));
            Assert.IsFalse(page.Root.GetComponentsInChildren<Button>().Any(button => button.name == "Keep display"));
            Assert.AreEqual(display, (Screen.fullScreenMode, Screen.width, Screen.height), "layout tests never request a resolution change");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ScrolledGraphicsControlRetainsItsContextAndFocusAfterChangingValueAndTabs()
        {
            Tab("graphics");
            yield return null;
            Assert.IsNotNull(Scroll().verticalScrollbar);
            Assert.IsTrue(Scroll().verticalScrollbar.gameObject.activeInHierarchy);
            Scroll().verticalScrollbar.value = 0f;
            yield return null;
            Assert.LessOrEqual(Scroll().verticalNormalizedPosition, .001f, "the visible scrollbar reaches the bottom");
            var target = Button("V-sync off");
            LobbyScrollFollow.Show(Scroll(), (RectTransform)target.transform);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
            yield return null;
            float position = Scroll().verticalNormalizedPosition;
            Assert.Less(position, .99f, "fixture reaches a genuinely scrolled control");
            target.onClick.Invoke();
            Canvas.ForceUpdateCanvases(); yield return null;
            Assert.AreEqual(position, Scroll().verticalNormalizedPosition, .005f);
            Assert.AreSame(Button("V-sync off").gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(0, QualitySettings.vSyncCount); Assert.AreEqual(0, PlayerPrefs.GetInt(LobbyMenu.VsyncKey));
            Tab("audio"); Tab("graphics"); yield return null;
            Assert.AreEqual(position, Scroll().verticalNormalizedPosition, .005f, "returning to the tab retains its scroll");
            Assert.AreSame(Button("Settings tab graphics").gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator AudioResetChangesOnlyAudioAndKeepsItsFocus()
        {
            string career = PlayerPrefs.GetString(MatchTally.CareerKey), outfit = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            string keys = DesktopBinding.Shared.Map.SaveBindingOverridesAsJson();
            CombatFeedbackOptions.ShowDamage = false;
            int motion = PlayerPrefs.GetInt("wv.theme.effects", 1);
            var graphics = GraphicsOptions.Keys.Select(key => PlayerPrefs.GetString(key)).ToArray();
            var graphicsExist = GraphicsOptions.Keys.Select(PlayerPrefs.HasKey).ToArray();
            Tab("audio"); Button("Sound off").onClick.Invoke(); Button("Sound pack winter").onClick.Invoke();
            AudioListener.volume = .5f;
            Button("Volume less").onClick.Invoke();
            Assert.AreEqual(.4f, PlayerPrefs.GetFloat(LobbyMenu.VolumeKey), .001f);
            Assert.IsTrue(GameFeedback.Muted); Assert.AreEqual("winter", GameSoundPacks.Selected);
            Button("Reset audio").onClick.Invoke(); yield return null;
            Assert.IsFalse(GameFeedback.Muted); Assert.AreEqual("default", GameSoundPacks.Selected);
            Assert.AreEqual(1f, AudioListener.volume); Assert.IsFalse(PlayerPrefs.HasKey(LobbyMenu.VolumeKey));
            Assert.AreEqual("Reset audio", EventSystem.current.currentSelectedGameObject.name);
            Assert.IsFalse(CombatFeedbackOptions.ShowDamage); Assert.AreEqual(motion, PlayerPrefs.GetInt("wv.theme.effects", 1));
            Assert.AreEqual(career, PlayerPrefs.GetString(MatchTally.CareerKey)); Assert.AreEqual(outfit, PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual(keys, DesktopBinding.Shared.Map.SaveBindingOverridesAsJson());
            CollectionAssert.AreEqual(graphics, GraphicsOptions.Keys.Select(key => PlayerPrefs.GetString(key)).ToArray());
            CollectionAssert.AreEqual(graphicsExist, GraphicsOptions.Keys.Select(PlayerPrefs.HasKey).ToArray());
        }
    }
}
