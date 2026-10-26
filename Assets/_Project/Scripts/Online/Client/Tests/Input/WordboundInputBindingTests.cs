using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundInputBindingTests
    {
        readonly List<InputDevice> created = new List<InputDevice>();
        Keyboard previousKeyboard, keyboard;
        Mouse previousMouse, mouse;
        Gamepad previousPad, pad;
        WordboundInputBinding binding;
        bool typing;
        float scale;
        WordboundNativeInputScope input;

        [SetUp]
        public void SetUp()
        {
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current; previousPad = Gamepad.current;
            typing = DesktopBinding.Typing; scale = Time.timeScale;
            try
            {
                input = new WordboundNativeInputScope();
                Assert.IsFalse(KeyBindings.GameplayBlocked, "Input fixture must start without an inherited blocker: " + WordboundNativeInputScope.Context());
                DesktopBinding.Typing = false; Time.timeScale = 1;
                keyboard = Add<Keyboard>(); keyboard.MakeCurrent();
                mouse = Add<Mouse>(); mouse.MakeCurrent();
                pad = Add<Gamepad>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(pad, new GamepadState());
                WordboundNativeInputScope.Pump();
                binding = new WordboundInputBinding();
            }
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        T Add<T>() where T : InputDevice, new()
        {
            var device = InputSystem.AddDevice<T>(); created.Add(device); return device;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                binding?.Dispose(); binding = null;
                foreach (var device in created) if (device != null && device.added) InputSystem.RemoveDevice(device);
                created.Clear();
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
            finally
            {
                try { input?.Dispose(); }
                finally { input = null; DesktopBinding.Typing = typing; Time.timeScale = scale; }
            }
        }

        PlayerCommands Read(GamepadState state)
        {
            WordboundNativeInputScope.Sample(pad, state);
            var commands = default(PlayerCommands); binding.Read(ref commands); return commands;
        }

        void UsePad()
        {
            Read(new GamepadState { leftStick = new Vector2(.8f, 0) });
            Read(new GamepadState());
            binding.ReadMenu(0);
        }

        [Test]
        public void ControlHintsFollowConsumedDesktopOverridesWithoutChangingInput()
        {
            var keys = (DesktopBinding)typeof(WordboundInputBinding)
                .GetField("desktop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binding);
            binding.ControlHint(WordboundControl.Interact);
            int version = KeyBindings.Version, revision = binding.Revision;
            keys.Interact.ApplyBindingOverride(0, "<Keyboard>/f");
            keys.Spell.ApplyBindingOverride(0, "<Keyboard>/t");
            keys.Pause.ApplyBindingOverride(0, "<Keyboard>/p");
            WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.F, Key.T, Key.P));
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual("F", binding.ControlHint(WordboundControl.Interact));
                Assert.AreEqual("T", binding.ControlHint(WordboundControl.Spell));
                Assert.AreEqual("P", binding.ControlHint(WordboundControl.Menu));
            }
            Assert.AreEqual(version, KeyBindings.Version);
            Assert.AreEqual(revision, binding.Revision);
            Assert.IsTrue(keys.Interact.WasPressedThisFrame());
            var commands = default(PlayerCommands); binding.Read(ref commands);
            Assert.IsTrue(commands.grab && commands.spellDown && binding.StartPressed());
            keys.Interact.ApplyBindingOverride(0, "<Keyboard>/g");
            Assert.AreEqual("G", binding.ControlHint(WordboundControl.Interact));
            WordboundNativeInputScope.Sample(keyboard, new KeyboardState());
            WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.F));
            binding.Read(ref commands); Assert.IsFalse(commands.grab);
            WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.G));
            binding.Read(ref commands); Assert.IsTrue(commands.grab);
            keys.Interact.ApplyBindingOverride(0, "");
            Assert.AreEqual("Unbound", binding.ControlHint(WordboundControl.Interact));
        }

        [Test]
        public void ReadingHintsDoesNotSelectADeviceConsumeButtonsOrHideAnUnpluggedPad()
        {
            string desktop = binding.ControlHint(WordboundControl.Interact);
            int revision = binding.Revision;
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
            Assert.AreEqual(desktop, binding.ControlHint(WordboundControl.Interact));
            Assert.IsNull(binding.ActiveGamepad);
            Assert.AreEqual(revision, binding.Revision);
            UsePad();
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
            revision = binding.Revision;
            string attack = pad.buttonWest.shortDisplayName;
            if (string.IsNullOrEmpty(attack)) attack = pad.buttonWest.displayName;
            Assert.AreEqual(attack, binding.ControlHint(WordboundControl.Attack));
            Assert.AreNotEqual(binding.ControlHint(WordboundControl.CycleGear), binding.ControlHint(WordboundControl.Menu));
            Assert.IsTrue(pad.buttonWest.wasPressedThisFrame);
            Assert.AreEqual(revision, binding.Revision);
            var commands = default(PlayerCommands); binding.Read(ref commands);
            Assert.IsTrue(commands.attack && commands.attackHeld);
            InputSystem.RemoveDevice(pad);
            Assert.AreEqual("", binding.ControlHint(WordboundControl.Interact));
            Assert.AreSame(pad, binding.ActiveGamepad, "Hints do not perform a device transition.");
            Assert.AreEqual(revision, binding.Revision);
            Assert.IsTrue(binding.RefreshDevice());
            Assert.AreEqual(desktop, binding.ControlHint(WordboundControl.Interact));
        }

        [Test]
        public void ActiveHintUsesExistingLocalizedPhysicalKeyFallback()
        {
            const string layout = "WordboundHintLocalizedDevice";
            InputDevice localized = null;
            try
            {
                InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"controls\":[{\"name\":\"q\",\"layout\":\"Button\",\"displayName\":\"木\"}]}");
                localized = InputSystem.AddDevice(layout);
                var keys = (DesktopBinding)typeof(WordboundInputBinding)
                    .GetField("desktop", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binding);
                keys.Spell.ApplyBindingOverride(0, "<" + layout + ">/q");
                Assert.IsFalse(KeyBindings.IsLatin(keys.Spell.GetBindingDisplayString()));
                Assert.AreEqual("Q", binding.ControlHint(WordboundControl.Spell));
            }
            finally
            {
                if (localized != null && localized.added) InputSystem.RemoveDevice(localized);
                InputSystem.RemoveLayout(layout);
            }
        }

        [Test]
        public void PlayerViewKeepsOriginalBindingAndOwnsOnlyItsFixedAdapter()
        {
            using var legacyDesktop = new DesktopBinding();
            foreach (var source in new InputBinding[] { binding, legacyDesktop, new GamepadBinding(pad), new UnreadableBinding() })
            {
                var root = new GameObject("Wordbound prompt forwarding");
                try
                {
                    var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                    view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), source);
                    Assert.AreSame(source, ViewField("binding").GetValue(view), "The invoking seat identity must remain exact.");
                    var effective = (InputBinding)ViewField("effectiveBinding").GetValue(view);
                    if (source is DesktopBinding || source is GamepadBinding)
                    {
                        Assert.AreNotSame(source, effective);
                        Assert.AreSame(source, ((WordboundInputBinding)effective).BorrowedSource);
                        Assert.AreSame(effective, ViewField("ownedBinding").GetValue(view));
                    }
                    else Assert.AreSame(source, effective);
                    foreach (WordboundControl control in Enum.GetValues(typeof(WordboundControl)))
                    {
                        string hint = view.ControlHint(control);
                        if (source is UnreadableBinding) Assert.AreEqual("", hint);
                        else Assert.IsNotEmpty(hint, source.GetType().Name + " " + control);
                    }
                    Assert.AreEqual("", view.ControlHint((WordboundControl)99));
                    if (source is DesktopBinding desktopSource)
                    {
                        Assert.AreEqual(KeyBindings.Display(desktopSource.Interact), view.ControlHint(WordboundControl.Interact));
                        Assert.AreEqual(KeyBindings.Display(desktopSource.Spell), view.ControlHint(WordboundControl.Spell));
                        Assert.AreEqual(KeyBindings.Display(desktopSource.Pause), view.ControlHint(WordboundControl.Menu));
                        Assert.AreEqual(KeyBindings.Display(desktopSource.Bag), view.ControlHint(WordboundControl.CycleGear));
                    }
                    if (source is GamepadBinding padSource)
                    {
                        Assert.AreEqual(ExpectedButtonName(padSource.Pad.buttonWest, "X"), view.ControlHint(WordboundControl.Attack));
                        Assert.AreEqual(ExpectedButtonName(padSource.Pad.buttonNorth, "Y"), view.ControlHint(WordboundControl.Spell));
                        Assert.AreEqual(ExpectedButtonName(padSource.Pad.startButton, "Start"), view.ControlHint(WordboundControl.Menu));
                    }
                }
                finally { DestroyViewThroughHandlers(root); }
            }
            Assert.IsTrue(legacyDesktop.Map.enabled, "The destruction handler must preserve the borrowed source map.");
        }

        static string ExpectedButtonName(ButtonControl button, string fallback)
        {
            string name = button.shortDisplayName;
            if (string.IsNullOrEmpty(name)) name = button.displayName;
            return !string.IsNullOrEmpty(name) && KeyBindings.IsLatin(name) ? name : fallback;
        }

        static void DestroyViewThroughHandlers(GameObject root)
        {
            var view = root ? root.GetComponent<WordboundPlayerView>() : null;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                if (view) typeof(WordboundPlayerView).GetMethod("OnDisable", flags).Invoke(view, null);
            }
            finally
            {
                try
                {
                    if (view)
                    {
                        var destroy = typeof(WordboundPlayerView).GetMethod("OnDestroy", flags);
                        destroy.Invoke(view, null);
                        Assert.IsNull(ViewField("ownedBinding").GetValue(view), "The production destruction handler releases its ownership.");
                        destroy.Invoke(view, null);
                        Assert.IsNull(ViewField("ownedBinding").GetValue(view), "Repeated destruction is harmless.");
                    }
                }
                finally { if (root) Object.DestroyImmediate(root); }
            }
        }

        static FieldInfo ViewField(string name) => typeof(WordboundPlayerView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static void ProcessView(WordboundPlayerView view, bool focused) => typeof(WordboundPlayerView)
            .GetMethod("ProcessInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { focused });

        [Test]
        public void BorrowedDesktopDestroyHandlerPreservesSourceMapAndRemappedControls()
        {
            using var source = new DesktopBinding();
            source.Interact.ApplyBindingOverride(0, "<Keyboard>/f");
            source.Spell.ApplyBindingOverride(0, "<Keyboard>/t");
            source.Pause.ApplyBindingOverride(0, "<Keyboard>/p");
            source.Bag.ApplyBindingOverride(0, "<Keyboard>/h");
            int version = KeyBindings.Version;
            var seats = Session.Bindings.ToArray();
            string overrides = source.Map.SaveBindingOverridesAsJson();
            var root = new GameObject("Borrowed desktop input test");
            WordboundInputBinding adapted = null;
            try
            {
                var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), source);
                adapted = (WordboundInputBinding)ViewField("effectiveBinding").GetValue(view);
                Assert.AreSame(source, ViewField("binding").GetValue(view));
                Assert.AreSame(source, adapted.BorrowedSource);
                WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.F, Key.T, Key.H, Key.P));
                Assert.AreEqual("F", view.ControlHint(WordboundControl.Interact));
                Assert.AreEqual("T", view.ControlHint(WordboundControl.Spell));
                Assert.AreEqual("P", view.ControlHint(WordboundControl.Menu));
                Assert.AreEqual("H", view.ControlHint(WordboundControl.CycleGear));
                Assert.AreEqual(KeyBindings.Display(source.Attack), view.ControlHint(WordboundControl.Attack));
                var command = default(PlayerCommands); adapted.Read(ref command);
                Assert.IsTrue(command.grab && command.spellDown && command.swap);
                Assert.IsTrue(source.Interact.WasPressedThisFrame(), "Hints do not consume the source action.");
                Assert.IsTrue(view.MenuOpen);
                ProcessView(view, true);
                Assert.IsFalse(view.MenuOpen, "The displayed remapped Pause action must open/close the actual view menu.");
                ProcessView(view, false);
                adapted.Read(ref command);
                Assert.IsFalse(command.grab || command.spellDown || command.swap, "Focus loss must require release.");
                WordboundNativeInputScope.Sample(keyboard, new KeyboardState()); adapted.Read(ref command);
                WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.F, Key.T, Key.H)); adapted.Read(ref command);
                Assert.IsTrue(command.grab && command.spellDown && command.swap);
                source.Spell.ApplyBindingOverride(0, "<Keyboard>/g");
                Assert.AreEqual("G", view.ControlHint(WordboundControl.Spell));
                source.Spell.ApplyBindingOverride(0, "");
                Assert.AreEqual("Unbound", view.ControlHint(WordboundControl.Spell));
                source.Spell.ApplyBindingOverride(0, "<Keyboard>/t");
            }
            finally { DestroyViewThroughHandlers(root); }
            Assert.AreEqual("", adapted.ControlHint(WordboundControl.Spell), "The production destruction handler must dispose only the wrapper.");
            Assert.IsTrue(source.Map.enabled);
            Assert.AreEqual(overrides, source.Map.SaveBindingOverridesAsJson());
            Assert.AreEqual(version, KeyBindings.Version);
            CollectionAssert.AreEqual(seats, Session.Bindings);
        }

        [Test]
        public void BorrowedGamepadDestroyHandlerPreservesSourceAfterWordboundActions()
        {
            var source = new GamepadBinding(pad);
            var other = Add<Gamepad>();
            var root = new GameObject("Borrowed gamepad input test");
            WordboundInputBinding adapted = null;
            try
            {
                var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), source);
                adapted = (WordboundInputBinding)ViewField("effectiveBinding").GetValue(view);
                Assert.AreSame(source, ViewField("binding").GetValue(view));
                Assert.AreSame(source, adapted.BorrowedSource);
                WordboundNativeInputScope.Sample(keyboard, new KeyboardState(Key.F));
                WordboundNativeInputScope.Sample(other, new GamepadState().WithButton(GamepadButton.West));
                Assert.IsFalse(adapted.RefreshDevice());
                Assert.AreSame(pad, adapted.ActiveGamepad);
                Assert.IsTrue(view.UsesGamepad);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West)
                    .WithButton(GamepadButton.North).WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.RightStick));
                var command = default(PlayerCommands); adapted.Read(ref command);
                Assert.IsTrue(command.attack && command.attackHeld && command.aimHeld && command.spellDown && command.swap);
                Assert.AreEqual(ExpectedButtonName(pad.buttonNorth, "Y"), view.ControlHint(WordboundControl.Spell));
                Assert.AreEqual(ExpectedButtonName(pad.rightShoulder, "RB"), view.ControlHint(WordboundControl.Aim));
                Assert.AreEqual(ExpectedButtonName(pad.startButton, "Start"), view.ControlHint(WordboundControl.Menu));
                WordboundNativeInputScope.Sample(pad, new GamepadState());
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.Start));
                ProcessView(view, true); Assert.IsFalse(view.MenuOpen);
                ProcessView(view, false);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
                adapted.Read(ref command); Assert.IsFalse(command.attack || command.attackHeld);
                WordboundNativeInputScope.Sample(pad, new GamepadState()); adapted.Read(ref command);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
                adapted.Read(ref command); Assert.IsTrue(command.attack && command.attackHeld);
            }
            finally { DestroyViewThroughHandlers(root); }
            Assert.AreEqual("", adapted.ControlHint(WordboundControl.Attack));
            Assert.IsTrue(pad.added && pad.enabled);
            var original = default(PlayerCommands); source.Read(ref original);
            Assert.IsTrue(original.attack, "The caller still owns a usable original binding.");
        }

        [Test]
        public void BorrowedPadMenuUsesSharedReleaseRepeatConfirmAndBackPolicy()
        {
            using var adapted = new WordboundInputBinding(new GamepadBinding(pad));
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.South));
            adapted.SuspendGameplay();
            Assert.IsFalse(adapted.ReadMenu(0).Confirm, "Opening craft while confirm is held must not activate it.");
            WordboundNativeInputScope.Sample(pad, new GamepadState()); adapted.ReadMenu(.1f);
            WordboundNativeInputScope.Sample(pad, new GamepadState { leftStick = new Vector2(0, -1) });
            Assert.AreEqual(1, adapted.ReadMenu(.2f).Step);
            Assert.AreEqual(0, adapted.ReadMenu(.3f).Step);
            Assert.AreEqual(1, adapted.ReadMenu(.56f).Step);
            WordboundNativeInputScope.Sample(pad, new GamepadState()); adapted.ReadMenu(.6f);
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.South));
            Assert.IsTrue(adapted.ReadMenu(.7f).Confirm);
            WordboundNativeInputScope.Sample(pad, new GamepadState()); adapted.ReadMenu(.8f);
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.East));
            Assert.IsTrue(adapted.ReadMenu(.9f).Back);
        }

        [Test]
        public void BorrowedPadCraftCommandAndBackUseTheActualHudAndViewMenuHandlers()
        {
            var source = new GamepadBinding(pad);
            var root = new GameObject("Borrowed pad craft menu routing");
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                view.Initialize(controller, root.AddComponent<Camera>(), source);
                var hud = root.AddComponent<WordboundMatchHud>(); hud.enabled = false;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(WordboundMatchHud).GetField("controller", flags).SetValue(hud, controller);
                typeof(WordboundMatchHud).GetField("view", flags).SetValue(hud, view);
                typeof(WordboundMatchHud).GetField("initialized", flags).SetValue(hud, true);
                var readCommands = typeof(WordboundMatchHud).GetMethod("ReadCommands", flags);
                var readMenu = typeof(WordboundMatchHud).GetMethod("ReadMenuCommands", flags);
                view.MenuCommandsRead += command => readMenu.Invoke(hud, new object[] { command });
                var adapted = (WordboundInputBinding)ViewField("effectiveBinding").GetValue(view);
                view.SetMenuOpen(false);
                WordboundNativeInputScope.Sample(pad, new GamepadState());
                var command = default(PlayerCommands); adapted.Read(ref command);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.North));
                adapted.Read(ref command); Assert.IsTrue(command.spellDown);
                readCommands.Invoke(hud, new object[] { command });
                Assert.IsTrue(view.MenuOpen);
                Assert.IsTrue((bool)typeof(WordboundMatchHud).GetField("crafting", flags).GetValue(hud));
                WordboundNativeInputScope.Sample(pad, new GamepadState()); ProcessView(view, true);
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.East));
                ProcessView(view, true);
                Assert.IsFalse(view.MenuOpen);
                Assert.IsFalse((bool)typeof(WordboundMatchHud).GetField("crafting", flags).GetValue(hud));
                int lost = 0; view.MenuRequested += () => lost++;
                InputSystem.DisableDevice(pad); ProcessView(view, true);
                Assert.IsTrue(view.MenuOpen); Assert.AreEqual(1, lost);
                ProcessView(view, true); Assert.AreEqual(1, lost);
                Assert.AreSame(source, ViewField("binding").GetValue(view));
                Assert.AreSame(source, adapted.BorrowedSource);
            }
            finally { DestroyViewThroughHandlers(root); }
        }

        [Test]
        public void RemovedOrDisabledBorrowedPadNeverFallsBackToAnotherDevice()
        {
            using var adapted = new WordboundInputBinding(new GamepadBinding(pad));
            var other = Add<Gamepad>();
            WordboundNativeInputScope.Sample(other, new GamepadState().WithButton(GamepadButton.West));
            InputSystem.DisableDevice(pad);
            Assert.IsTrue(adapted.RefreshDevice());
            Assert.IsFalse(adapted.RefreshDevice(), "Loss is reported once per availability transition.");
            Assert.AreSame(pad, adapted.ActiveGamepad);
            Assert.IsFalse(adapted.ReadsMouse || adapted.StartPressed());
            Assert.AreEqual("", adapted.ControlHint(WordboundControl.Attack));
            var command = default(PlayerCommands); adapted.Read(ref command);
            Assert.IsFalse(command.attack || command.attackHeld || command.spellDown);
            Assert.IsFalse(adapted.ReadMenu(1).Confirm);
            InputSystem.EnableDevice(pad); Assert.IsFalse(adapted.RefreshDevice());
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
            adapted.Read(ref command); Assert.IsFalse(command.attack || command.attackHeld);
            WordboundNativeInputScope.Sample(pad, new GamepadState()); adapted.Read(ref command);
            WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.West));
            adapted.Read(ref command); Assert.IsTrue(command.attack && command.attackHeld);
            InputSystem.RemoveDevice(pad);
            Assert.IsTrue(adapted.RefreshDevice());
            Assert.AreSame(pad, adapted.ActiveGamepad);
            adapted.Read(ref command); Assert.IsFalse(command.attack || command.attackHeld);
            Assert.AreEqual("", adapted.ControlHint(WordboundControl.Attack));
        }

        sealed class UnreadableBinding : InputBinding
        {
            public override string Id => "test-custom";
            public override void Read(ref PlayerCommands commands) => throw new InvalidOperationException("Hints must not read commands.");
            public override bool JoinPressed() => throw new InvalidOperationException("Hints must not read join.");
            public override bool StartPressed() => throw new InvalidOperationException("Hints must not read start.");
        }

        [Test]
        public void NativeInputScopesPreserveTemporaryOriginalSettingsAcrossRepeatedUse()
        {
            var original = InputSystem.settings;
            var originalFlags = original.hideFlags;
            try
            {
                original.hideFlags = HideFlags.HideAndDontSave;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    InputSettings scoped;
                    using (var nested = new WordboundNativeInputScope())
                    {
                        scoped = InputSystem.settings;
                        Assert.IsTrue(original, "Installing test settings must not destroy manager-owned temporary settings.");
                        Assert.AreNotSame(original, scoped);
                        Assert.AreEqual(HideFlags.HideAndDontSave, original.hideFlags);
                        WordboundNativeInputScope.Pump();
                    }
                    Assert.AreSame(original, InputSystem.settings);
                    Assert.IsTrue(original);
                    Assert.AreEqual(HideFlags.HideAndDontSave, original.hideFlags);
                    Assert.IsFalse(scoped, "Only the inactive scope-owned settings should be destroyed.");
                }
            }
            finally { if (original) original.hideFlags = originalFlags; }
        }

        [Test]
        public void QuietConnectedPadDoesNotStealDesktopButDeliberateStickInputControlsMovementAndLook()
        {
            Assert.IsTrue(binding.ReadsMouse);
            binding.RefreshDevice();
            Assert.IsNull(binding.ActiveGamepad);
            var commands = Read(new GamepadState { leftStick = new Vector2(.8f, .4f), rightStick = new Vector2(.7f, -.6f) });
            Assert.AreSame(pad, binding.ActiveGamepad);
            Assert.IsFalse(binding.ReadsMouse);
            Assert.Greater(commands.move.x, .4f);
            Assert.Greater(commands.move.y, .1f);
            Assert.LessOrEqual(commands.move.magnitude, 1);
            Assert.Greater(commands.look.x, .3f);
            Assert.Less(commands.look.y, -.2f);
        }

        [Test]
        public void LowStickNoiseDoesNotSwitchDevices()
        {
            var commands = Read(new GamepadState { leftStick = new Vector2(.05f, .04f), rightStick = new Vector2(.06f, 0) });
            Assert.IsTrue(binding.ReadsMouse);
            Assert.IsNull(binding.ActiveGamepad);
            Assert.AreEqual(Vector2.zero, commands.move);
        }

        [Test]
        public void NativeInputScopeRestoresItsQuietFramesWithoutReleasingAnExistingGameplayLease()
        {
            int before = WordboundNativeInputScope.QuietUntil;
            using (KeyBindings.BlockGameplay())
            {
                using (var nested = new WordboundNativeInputScope())
                {
                    using (KeyBindings.BlockGameplay()) { }
                    Assert.AreEqual(Time.frameCount + 1, WordboundNativeInputScope.QuietUntil);
                    Assert.IsTrue(KeyBindings.GameplayBlocked);
                }
                Assert.AreEqual(before, WordboundNativeInputScope.QuietUntil);
                Assert.IsTrue(KeyBindings.GameplayBlocked, "Restoring fixture quiet frames must not release an existing input lease.");
            }
            Assert.AreEqual(Time.frameCount + 1, WordboundNativeInputScope.QuietUntil,
                "The production quiet window remains active within the enclosing fixture.");
        }

        [Test]
        public void WordboundAddsHeldAttackAndRightShoulderThrowAimWithoutChangingLegacyGamepadMapping()
        {
            UsePad();
            Assert.AreSame(pad, binding.ActiveGamepad, "The real neutral transition must select this gamepad before attacking.");
            var state = new GamepadState().WithButton(GamepadButton.West).WithButton(GamepadButton.RightShoulder);
            var commands = Read(state);
            Assert.IsTrue(pad.buttonWest.wasPressedThisFrame, WordboundNativeInputScope.Context(pad));
            Assert.IsTrue(commands.attack, WordboundNativeInputScope.Context(pad));
            Assert.IsTrue(commands.attackHeld);
            Assert.IsTrue(commands.aimHeld);
            var legacy = new GamepadBinding(pad);
            var unchanged = default(PlayerCommands); legacy.Read(ref unchanged);
            Assert.IsTrue(unchanged.attack);
            Assert.IsFalse(unchanged.attackHeld);
            Assert.IsFalse(unchanged.aimHeld);
            Read(new GamepadState());
            commands = Read(new GamepadState { leftTrigger = 1, rightTrigger = 1 });
            Assert.IsTrue(commands.blockHeld);
            Assert.IsTrue(commands.grab);
            Assert.IsTrue(commands.grabHeld);
            Assert.IsFalse(commands.aimHeld);
        }

        [Test]
        public void SwapSpellJumpAndPauseKeepTheirExistingGamepadButtons()
        {
            UsePad();
            var commands = Read(new GamepadState().WithButton(GamepadButton.RightStick).WithButton(GamepadButton.North)
                .WithButton(GamepadButton.South).WithButton(GamepadButton.Start));
            Assert.IsTrue(commands.swap);
            Assert.IsTrue(commands.spellDown);
            Assert.IsTrue(commands.spellHeld);
            Assert.IsTrue(commands.jump);
            Assert.IsTrue(binding.StartPressed());
        }

        [Test]
        public void DesktopReclaimsInputAndStationaryHeldStickCannotImmediatelyStealItBack()
        {
            var held = new GamepadState { leftStick = new Vector2(.8f, 0) };
            Read(held);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); WordboundNativeInputScope.Pump();
            binding.RefreshDevice();
            Assert.IsTrue(binding.ReadsMouse);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            WordboundNativeInputScope.Sample(pad, held);
            binding.RefreshDevice();
            Assert.IsTrue(binding.ReadsMouse);
            Read(new GamepadState());
            Read(held);
            Assert.AreSame(pad, binding.ActiveGamepad);
        }

        [Test]
        public void RemovingActivePadReportsLossAndHotplugRequiresFreshIntent()
        {
            Read(new GamepadState().WithButton(GamepadButton.West));
            InputSystem.RemoveDevice(pad);
            Assert.IsTrue(binding.RefreshDevice());
            Assert.IsTrue(binding.ReadsMouse);
            Assert.IsFalse(binding.RefreshDevice());
            pad = Add<Gamepad>();
            WordboundNativeInputScope.Sample(pad, new GamepadState());
            binding.RefreshDevice();
            Assert.IsNull(binding.ActiveGamepad);
            var commands = Read(new GamepadState().WithButton(GamepadButton.South));
            Assert.AreSame(pad, binding.ActiveGamepad);
            Assert.IsFalse(commands.jump, "The press selecting a newly connected pad must not also act in gameplay.");
            Assert.IsFalse(commands.attackHeld);
            Read(new GamepadState());
            commands = Read(new GamepadState().WithButton(GamepadButton.South));
            Assert.IsTrue(commands.jump);
        }

        [Test]
        public void TypingAndGameplayInputLeaseSuppressGamepadAttackDefenseAndPause()
        {
            UsePad();
            Read(new GamepadState().WithButton(GamepadButton.West));
            using (KeyBindings.BlockGameplay())
            {
                var blocked = Read(new GamepadState { leftTrigger = 1 }.WithButton(GamepadButton.West).WithButton(GamepadButton.Start));
                Assert.IsFalse(blocked.attack || blocked.attackHeld || blocked.blockHeld || binding.StartPressed());
            }
            DesktopBinding.Typing = true;
            var typingCommands = Read(new GamepadState().WithButton(GamepadButton.West).WithButton(GamepadButton.Start));
            Assert.IsFalse(typingCommands.attack || typingCommands.attackHeld || binding.StartPressed());
        }

        [Test]
        public void ActualPlayerViewDefaultsToAutomaticBindingAndPadLossOpensMenuAndReleasesInput()
        {
            var root = new GameObject("Wordbound automatic input native test");
            try
            {
                var camera = root.AddComponent<Camera>();
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, camera);
                var automatic = (WordboundInputBinding)typeof(WordboundPlayerView)
                    .GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                int released = 0, menus = 0;
                view.CommandsRead += commands => { if (!commands.attackHeld && !commands.blockHeld) released++; };
                view.MenuRequested += () => menus++;
                WordboundNativeInputScope.Sample(pad, new GamepadState { leftTrigger = 1 });
                Process(view, true);
                Assert.AreSame(pad, automatic.ActiveGamepad);
                view.SetMenuOpen(false);
                int before = released;
                int menusBeforeLoss = menus;
                InputSystem.RemoveDevice(pad);
                Process(view, true);
                Assert.IsTrue(view.MenuOpen);
                Assert.AreEqual(menusBeforeLoss + 1, menus);
                Assert.Greater(released, before, "PlayerView must send a released frame to its actual HUD subscribers.");
                Assert.AreEqual(0, view.ReadMovement().MoveX);
                Assert.AreEqual(0, view.ReadMovement().MoveZ);
                Assert.IsFalse(view.ReadMovement().Jump);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TabCyclesOnlyWordboundEquipmentAndOriginalDesktopBindingStaysUnchanged()
        {
            using (var legacy = new DesktopBinding())
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); WordboundNativeInputScope.Pump();
                Assert.IsTrue(keyboard.tabKey.isPressed, "The Tab key state must reach the input system before reading its action.");
                var commands = default(PlayerCommands); binding.Read(ref commands);
                Assert.IsTrue(commands.swap);
                commands = default;
                legacy.Read(ref commands);
                Assert.IsFalse(commands.swap);
            }
        }

        [Test]
        public void MenuOrFocusSuspensionRequiresActionReleaseButKeepsCameraAndMovementResponsive()
        {
            UsePad();
            var held = new GamepadState { leftStick = new Vector2(.8f, 0), rightStick = new Vector2(.8f, 0) }
                .WithButton(GamepadButton.West).WithButton(GamepadButton.LeftShoulder);
            Assert.IsTrue(Read(held).attackHeld);
            binding.SuspendGameplay();
            var commands = Read(held);
            Assert.IsFalse(commands.attack || commands.attackHeld || commands.blockHeld || commands.drop);
            Assert.Greater(commands.move.x, .4f);
            Assert.Greater(commands.look.x, .4f);
            Read(new GamepadState());
            commands = Read(held);
            Assert.IsTrue(commands.attackHeld);
            Assert.IsFalse(commands.drop, "A resumed held drop must start a fresh hold timer.");
        }

        [Test]
        public void ControllerMenuHasBoundedRepeatConfirmBackAndScrollWithoutGameplayActions()
        {
            UsePad();
            Read(new GamepadState().WithButton(GamepadButton.DpadDown));
            Assert.AreEqual(1, binding.ReadMenu(1).Step);
            Assert.AreEqual(0, binding.ReadMenu(1.1f).Step);
            Assert.AreEqual(1, binding.ReadMenu(1.36f).Step);
            Read(new GamepadState().WithButton(GamepadButton.DpadUp));
            Assert.AreEqual(-1, binding.ReadMenu(1.37f).Step);
            Read(new GamepadState { rightStick = new Vector2(0, -.8f) }.WithButton(GamepadButton.South));
            var menu = binding.ReadMenu(1.5f);
            Assert.IsTrue(menu.Confirm);
            Assert.IsFalse(menu.Back);
            Assert.Less(menu.Scroll, -.4f);
            Read(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsTrue(binding.ReadMenu(1.6f).Back);
        }

        [Test]
        public void HeldMenuConfirmAndDirectionCannotActivateAcrossFocusOrMenuTransition()
        {
            UsePad();
            var held = new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.DpadDown);
            Read(held);
            binding.SuspendGameplay();
            var menu = binding.ReadMenu(1);
            Assert.AreEqual(0, menu.Step);
            Assert.IsFalse(menu.Confirm);
            Read(new GamepadState()); binding.ReadMenu(2);
            Read(held); menu = binding.ReadMenu(3);
            Assert.AreEqual(1, menu.Step);
            Assert.IsTrue(menu.Confirm);
        }

        [Test]
        public void UnfocusedViewDoesNotSwitchDeviceOrAcceptMenuInputAndFocusLossReleasesImmediately()
        {
            var root = new GameObject("Wordbound focus transition native test");
            try
            {
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>());
                int released = 0, menuInputs = 0;
                view.CommandsRead += _ => released++;
                view.MenuCommandsRead += _ => menuInputs++;
                WordboundNativeInputScope.Sample(pad, new GamepadState().WithButton(GamepadButton.South));
                Process(view, false);
                Assert.IsFalse(view.UsesGamepad);
                Assert.AreEqual(0, menuInputs);
                view.SetMenuOpen(false);
                int before = released;
                typeof(WordboundPlayerView).GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, new object[] { false });
                Assert.IsTrue(view.MenuOpen);
                Assert.Greater(released, before);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Process(WordboundPlayerView view, bool focused) => typeof(WordboundPlayerView)
            .GetMethod("ProcessInput", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { focused });
    }
}
