using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        public void WordboundAddsHeldAttackAndRightShoulderThrowAimWithoutChangingLegacyGamepadMapping()
        {
            UsePad();
            var state = new GamepadState().WithButton(GamepadButton.West).WithButton(GamepadButton.RightShoulder);
            var commands = Read(state);
            Assert.IsTrue(commands.attack);
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
