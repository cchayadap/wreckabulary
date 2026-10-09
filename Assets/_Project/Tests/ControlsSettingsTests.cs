using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class ControlsSettingsTests
    {
        readonly string[] preferenceKeys = { KeyBindings.OverridesKey, KeyBindings.LegacyBackupKey, KeyBindings.SensitivityKey, KeyBindings.InvertKey };
        bool[] existed;
        string overrides, backup, mapOverrides;
        float preferenceSensitivity, liveSensitivity;
        int preferenceInvert;
        bool liveInvert, touchEnabled;
        Keyboard keyboard;
        Mouse mouse;
        Gamepad pad;
        InputSettings.EditorInputBehaviorInPlayMode route;
        InputSettings.BackgroundBehavior focus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            existed = preferenceKeys.Select(PlayerPrefs.HasKey).ToArray();
            overrides = PlayerPrefs.GetString(KeyBindings.OverridesKey, "");
            backup = PlayerPrefs.GetString(KeyBindings.LegacyBackupKey, "");
            preferenceSensitivity = PlayerPrefs.GetFloat(KeyBindings.SensitivityKey, 1f);
            preferenceInvert = PlayerPrefs.GetInt(KeyBindings.InvertKey, 0);
            mapOverrides = DesktopBinding.Shared.Map.SaveBindingOverridesAsJson();
            liveSensitivity = KeyBindings.Sensitivity; liveInvert = KeyBindings.InvertY;
            touchEnabled = TouchBinding.Shared.Enabled;
            KeyBindings.ResetToDefaults();
            DesktopBinding.Typing = false;
            route = InputSystem.settings.editorInputBehaviorInPlayMode;
            focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            pad = InputSystem.AddDevice<Gamepad>();
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            KeyBindings.Stop();
            yield return TestScenes.Reset();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = touchEnabled;
            DesktopBinding.Typing = false;
            DesktopBinding.Shared.Map.RemoveAllBindingOverrides();
            if (!string.IsNullOrEmpty(mapOverrides)) DesktopBinding.Shared.Map.LoadBindingOverridesFromJson(mapOverrides);
            KeyBindings.SetSensitivity(liveSensitivity); KeyBindings.SetInvertY(liveInvert);
            PlayerPrefs.SetString(KeyBindings.OverridesKey, overrides);
            PlayerPrefs.SetString(KeyBindings.LegacyBackupKey, backup);
            PlayerPrefs.SetFloat(KeyBindings.SensitivityKey, preferenceSensitivity);
            PlayerPrefs.SetInt(KeyBindings.InvertKey, preferenceInvert);
            for (int i = 0; i < preferenceKeys.Length; i++) if (!existed[i]) PlayerPrefs.DeleteKey(preferenceKeys[i]);
            PlayerPrefs.Save();
        }

        static Button ButtonNamed(ControlsPanel panel, string name) => panel.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

        static ControlsPanel Mount()
        {
            if (!EventSystem.current)
            {
                var events = new GameObject("Controls events", typeof(EventSystem)).AddComponent<InputSystemUIInputModule>();
                events.AssignDefaultActions();
            }
            var canvas = new GameObject("Controls canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var host = LobbyKit.Rect(canvas.transform, "Controls host");
            host.anchorMin = host.anchorMax = host.pivot = new Vector2(.5f, .5f);
            host.sizeDelta = new Vector2(800f, 680f);
            return ControlsPanel.Mount(host);
        }

        [Test]
        public void FreshMapsReloadAllOverridesAndSwapOnlyTheirOwnBindings()
        {
            using var first = new DesktopBinding();
            KeyBindings.Assign(first.Jump, 0, "<Keyboard>/f");
            KeyBindings.Assign(first.Spell, 0, "<Keyboard>/f");
            KeyBindings.Assign(first.Move, 1, "<Keyboard>/upArrow");
            KeyBindings.SetSensitivity(1.75f); KeyBindings.SetInvertY(true);
            using var fresh = new DesktopBinding();
            Assert.AreNotSame(first.Map, fresh.Map);
            Assert.AreEqual(first.Jump.bindings[0].id, fresh.Jump.bindings[0].id);
            Assert.AreEqual("<Keyboard>/f", fresh.Spell.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/q", fresh.Jump.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/upArrow", fresh.Move.bindings[1].effectivePath);
            Assert.AreEqual(1.75f, KeyBindings.Sensitivity); Assert.IsTrue(KeyBindings.InvertY);
            KeyBindings.Assign(fresh.Dodge, 0, "<Keyboard>/q");
            Assert.AreEqual("<Keyboard>/leftShift", fresh.Jump.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/q", first.Jump.bindings[0].effectivePath, "A different map is not part of the conflict swap.");
            Assert.AreEqual("<Keyboard>/space", DesktopBinding.Shared.Jump.bindings[0].effectivePath);
            KeyBindings.Load(DesktopBinding.Shared.Map);
            Assert.AreEqual("F", ControlHints.KeyOf(DesktopBinding.Shared.Spell));
        }

        [Test]
        public void OlderRandomIdSavesRecoverNamedActionsAndPreserveAmbiguousMovement()
        {
            string old = "{\"bindings\":[{\"action\":\"Desktop/Jump\",\"id\":\"" + Guid.NewGuid() +
                "\",\"path\":\"<Keyboard>/f\"},{\"action\":\"Desktop/Move\",\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"<Keyboard>/upArrow\"}]}";
            PlayerPrefs.SetString(KeyBindings.OverridesKey, old);
            using var fresh = new DesktopBinding();
            Assert.AreEqual("<Keyboard>/f", fresh.Jump.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/w", fresh.Move.bindings[1].effectivePath, "An unknown composite part is not guessed.");
            Assert.AreEqual(old, PlayerPrefs.GetString(KeyBindings.LegacyBackupKey));
            Assert.IsNotEmpty(KeyBindings.LoadWarning);
            using var next = new DesktopBinding();
            Assert.AreEqual("<Keyboard>/f", next.Jump.bindings[0].effectivePath, "The recovered action now has a stable binding ID.");
        }

        [UnityTest]
        public IEnumerator RebindingWhilePausedCapturesInputCancelsAndRestoresMapState()
        {
            var keys = DesktopBinding.Shared;
            Time.timeScale = 0f;
            int completed = 0;
            KeyBindings.Listen(keys.Jump, 0, () => completed++);
            Assert.IsFalse(keys.Map.enabled);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return TestScenes.WaitUntil(() => completed == 1, 1f, "paused key rebind");
            Assert.IsTrue(keys.Map.enabled);
            Assert.AreEqual("<Keyboard>/f", keys.Jump.bindings[0].effectivePath);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null; yield return null; yield return null;
            Assert.IsTrue(KeyBindings.Busy, "A captured key still held down cannot leak after the quiet frame.");
            Assert.IsFalse(keys.JoinPressed());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.IsFalse(KeyBindings.Busy);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null; yield return null;
            Assert.IsFalse(KeyBindings.Busy, "A fresh press of the captured key works after it has been released.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            keys.Map.Disable();
            KeyBindings.Listen(keys.Jump, 0, () => completed++);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return TestScenes.WaitUntil(() => completed == 2, 1f, "Escape cancels rebind");
            Assert.AreEqual("<Keyboard>/f", keys.Jump.bindings[0].effectivePath);
            Assert.IsFalse(keys.Map.enabled, "The original disabled state is preserved.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            keys.Map.Enable();
        }

        [UnityTest]
        public IEnumerator PanelControlsUpdateWithoutRebuildingAndBlockEveryGameplayInputSeat()
        {
            var panel = Mount();
            yield return null;
            var rebind = ButtonNamed(panel, "Rebind Jump");
            ButtonNamed(panel, "Sensitivity more").onClick.Invoke();
            ButtonNamed(panel, "Invert mouse Y").onClick.Invoke();
            Assert.AreEqual(1.25f, KeyBindings.Sensitivity); Assert.IsTrue(KeyBindings.InvertY);
            Assert.AreSame(rebind, ButtonNamed(panel, "Rebind Jump"), "Updating settings keeps navigation and scroll objects intact.");
            rebind.onClick.Invoke();
            Assert.IsTrue(panel.IsListening);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East));
            yield return TestScenes.WaitUntil(() => !panel.IsListening, 1f, "controller cancel");
            Assert.AreEqual("<Keyboard>/space", DesktopBinding.Shared.Jump.bindings[0].effectivePath);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.Start));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.J, Key.Enter));
            yield return null;
            TouchBinding.Shared.Enabled = true;
            TouchBinding.Shared.Pulse(TouchAction.Attack); TouchBinding.Shared.Pulse(TouchAction.Start);
            foreach (var binding in new InputBinding[] { DesktopBinding.Shared, new KeyboardBinding(KeyboardBinding.Side.Left), new GamepadBinding(pad), TouchBinding.Shared })
            {
                var commands = default(PlayerCommands);
                binding.Read(ref commands);
                Assert.IsFalse(commands.attack || commands.jump || commands.start, binding.Id);
                Assert.IsFalse(binding.JoinPressed(), binding.Id + " join");
                Assert.IsFalse(binding.StartPressed(), binding.Id + " start");
            }
            ButtonNamed(panel, "Reset controls").onClick.Invoke();
            Assert.AreEqual(1f, KeyBindings.Sensitivity); Assert.IsFalse(KeyBindings.InvertY);
            Assert.IsFalse(PlayerPrefs.HasKey(KeyBindings.OverridesKey));
            rebind.onClick.Invoke();
            Assert.IsTrue(KeyBindings.Listening);
            panel.Close();
            Assert.IsFalse(KeyBindings.Listening); Assert.IsTrue(DesktopBinding.Shared.Map.enabled);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null; yield return null;
            Assert.IsFalse(KeyBindings.GameplayBlocked);
            var touch = default(PlayerCommands); TouchBinding.Shared.Read(ref touch);
            Assert.IsFalse(touch.attack || touch.start, "Touch presses from the menu do not replay.");
        }

        [UnityTest]
        public IEnumerator PauseBackCancelsRebindingThenReturnsToPauseBeforeResuming()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0f;
            KeyBindings.Assign(DesktopBinding.Shared.Pause, 0, "<Keyboard>/p");
            yield return null; yield return null;
            hud.TogglePause();
            hud.transform.Find("Pause/Pause card/Pause controls").GetComponent<Button>().onClick.Invoke();
            var controls = hud.GetComponentInChildren<ControlsPanel>();
            Assert.IsTrue(hud.Paused && hud.PauseSettingsShown);
            Assert.IsTrue(KeyBindings.GameplayBlocked);
            ButtonNamed(controls, "Rebind Jump").onClick.Invoke();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return TestScenes.WaitUntil(() => !controls.IsListening, 1f, "cancel from paused controls");
            Assert.IsTrue(hud.PauseSettingsShown, "The Escape that cancels a rebind stays inside settings.");
            Assert.IsTrue(hud.Paused);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null; yield return null;
            Assert.IsFalse(hud.PauseSettingsShown, "Escape remains Back even when Pause is bound to P.");
            Assert.IsTrue(hud.Paused);
            Assert.IsFalse(controls.isActiveAndEnabled);
            Assert.AreEqual(0f, hud.transform.Find("Pause/Pause settings").GetComponent<CanvasGroup>().alpha);
            Assert.IsFalse(KeyBindings.Listening);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return TestScenes.WaitUntil(() => !KeyBindings.Busy, 1f, "settings-close input quiet period and key release");
            Assert.IsFalse(KeyBindings.GameplayBlocked, "After input settles, no controls lease may survive the CanvasGroup hide.");
            Assert.IsTrue(hud.Paused, "Closing settings returns to pause without replaying the Back press.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return TestScenes.WaitUntil(() => !hud.Paused, 1f, "a fresh Back press resumes gameplay");
            Assert.IsFalse(hud.Paused);
            Assert.AreEqual(1f, Time.timeScale);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        [UnityTest]
        public IEnumerator ControllerBackAndGameplayTransitionsReleasePauseControls()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0f;
            hud.TogglePause();
            var open = hud.transform.Find("Pause/Pause card/Pause controls").GetComponent<Button>();
            open.onClick.Invoke();
            Assert.IsTrue(KeyBindings.GameplayBlocked);
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select));
            yield return null; yield return null;
            Assert.IsTrue(hud.Paused);
            Assert.IsFalse(hud.PauseSettingsShown, "View/Share can navigate Back while the controls input lease is held.");
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null; yield return null;
            open.onClick.Invoke();
            var controls = hud.GetComponentInChildren<ControlsPanel>();
            ButtonNamed(controls, "Rebind Jump").onClick.Invoke();
            hud.StateController.TransitionToState(UIState.GameplayHUD);
            Assert.IsFalse(controls.isActiveAndEnabled);
            Assert.IsFalse(KeyBindings.Listening);
            yield return null; yield return null; yield return null;
            Assert.IsFalse(KeyBindings.GameplayBlocked);
        }

        [UnityTest]
        public IEnumerator LobbyCanvasGroupHideClosesAndRestoresTheSharedControlsEditor()
        {
            yield return TestScenes.Load(Session.HubScene);
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Settings);
            menu.Page<SettingsPage>().ShowTab("controls");
            yield return null;
            var controls = menu.GetComponentInChildren<ControlsPanel>();
            Assert.IsNotNull(controls);
            Assert.IsTrue(KeyBindings.GameplayBlocked);
            var state = Object.FindAnyObjectByType<GameHud>().StateController;
            state.FadeDuration = 0f;
            state.TransitionToState(UIState.GameplayHUD);
            Assert.IsFalse(controls.isActiveAndEnabled);
            yield return null; yield return null;
            Assert.IsFalse(KeyBindings.GameplayBlocked);
            state.TransitionToState(UIState.MainMenu);
            Assert.IsTrue(controls.isActiveAndEnabled);
            Assert.IsTrue(KeyBindings.GameplayBlocked);
            menu.Close();
            yield return null; yield return null;
            Assert.IsFalse(KeyBindings.GameplayBlocked);
        }

        [UnityTest]
        public IEnumerator PauseAudioAndGraphicsChangeLiveValuesAndResetDefaults()
        {
            string savedPack = PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey) ? PlayerPrefs.GetString(GameSoundPacks.PreferenceKey) : null;
            var savedGraphics = GraphicsOptions.Keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            bool hadVolume = PlayerPrefs.HasKey(LobbyMenu.VolumeKey), hadVsync = PlayerPrefs.HasKey(LobbyMenu.VsyncKey), hadMute = PlayerPrefs.HasKey("wv.muted");
            float savedVolume = PlayerPrefs.GetFloat(LobbyMenu.VolumeKey, 1f), liveVolume = AudioListener.volume;
            int savedVsync = PlayerPrefs.GetInt(LobbyMenu.VsyncKey), liveVsync = QualitySettings.vSyncCount;
            bool muted = GameFeedback.Muted;
            try
            {
                yield return TestScenes.Load(Session.DibsScene);
                var hud = Object.FindAnyObjectByType<GameHud>();
                hud.StateController.FadeDuration = 0f;
                hud.TogglePause();
                void Click(string name) => hud.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke();
                Click("Pause settings");
                Click("Reset audio");
                Click("Sound toggle");
                Click("Volume less");
                Assert.IsTrue(GameFeedback.Muted);
                Assert.AreEqual(.9f, AudioListener.volume, .001f);
                Assert.AreEqual(.9f, PlayerPrefs.GetFloat(LobbyMenu.VolumeKey), .001f);
                Click("Reset audio");
                Assert.IsFalse(GameFeedback.Muted);
                Assert.AreEqual(1f, AudioListener.volume);
                Assert.IsFalse(PlayerPrefs.HasKey(LobbyMenu.VolumeKey));
                Click("Pause tab graphics");
                Click("Reset graphics");
                Click("Quality preset more");
                Assert.AreEqual("Ultra", GraphicsOptions.Preset);
                Click("Render scale more");
                Assert.AreEqual(1.75f, GraphicsOptions.RenderScale);
                Click("Frame cap less");
                Assert.AreEqual(144, Application.targetFrameRate);
                Click("V-sync toggle");
                Assert.AreEqual(1, QualitySettings.vSyncCount);
                Assert.AreEqual(1, PlayerPrefs.GetInt(LobbyMenu.VsyncKey));
                Click("Reset graphics");
                Assert.AreEqual(GraphicsOptions.DefaultPreset, GraphicsOptions.Preset);
                Assert.AreEqual(-1, Application.targetFrameRate);
                Assert.AreEqual(0, QualitySettings.vSyncCount);
                Assert.IsFalse(GraphicsOptions.Customised);
                Assert.IsTrue(hud.Paused, "Changing settings never resumes the match.");
            }
            finally
            {
                if (savedPack == null) PlayerPrefs.DeleteKey(GameSoundPacks.PreferenceKey); else PlayerPrefs.SetString(GameSoundPacks.PreferenceKey, savedPack);
                for (int i = 0; i < GraphicsOptions.Keys.Length; i++)
                    if (savedGraphics[i] == null) PlayerPrefs.DeleteKey(GraphicsOptions.Keys[i]); else PlayerPrefs.SetString(GraphicsOptions.Keys[i], savedGraphics[i]);
                if (hadVolume) PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, savedVolume); else PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
                if (hadVsync) PlayerPrefs.SetInt(LobbyMenu.VsyncKey, savedVsync); else PlayerPrefs.DeleteKey(LobbyMenu.VsyncKey);
                GameFeedback.Muted = muted;
                if (!hadMute) PlayerPrefs.DeleteKey("wv.muted");
                PlayerPrefs.Save();
                GraphicsOptions.Load();
                AudioListener.volume = liveVolume;
                QualitySettings.vSyncCount = liveVsync;
            }
        }

        [UnityTest]
        public IEnumerator MouseCancelButtonNeverBecomesTheNewBinding()
        {
            var panel = Mount();
            ButtonNamed(panel, "Rebind Jump").onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            var cancel = (RectTransform)ButtonNamed(panel, "Cancel rebind").transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, cancel.TransformPoint(cancel.rect.center));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return TestScenes.WaitUntil(() => !panel.IsListening, 1f, "mouse cancel button");
            Assert.AreEqual("<Keyboard>/space", DesktopBinding.Shared.Jump.bindings[0].effectivePath);
            Assert.IsTrue(DesktopBinding.Shared.Map.enabled);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        }
    }
}
