using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    internal sealed class WordboundNativeInputScope : IDisposable
    {
        const string PlayerUpdates = "RUN_PLAYER_UPDATES_IN_EDIT_MODE";
        const HideFlags OwnedFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        static readonly FieldInfo QuietInputField = typeof(KeyBindings).GetField("quietUntil", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(KeyBindings).FullName, "quietUntil");
        readonly InputSettings original;
        readonly InputSettings temporary;
        readonly bool originalPlayerUpdates;
        readonly int originalQuietUntil;
        bool disposed;

        public WordboundNativeInputScope()
        {
            originalQuietUntil = QuietUntil;
            original = InputSystem.settings;
            if (!original) throw new InvalidOperationException("The native input fixture requires live original InputSettings.");
            var featureField = typeof(InputSettings).GetField("m_FeatureFlags", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(InputSettings).FullName, "m_FeatureFlags");
            var originalFeatures = (HashSet<string>)featureField.GetValue(original);
            originalPlayerUpdates = originalFeatures?.Contains(PlayerUpdates) == true;
            temporary = Object.Instantiate(original);
            temporary.hideFlags = OwnedFlags;
            try
            {
                temporary.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                temporary.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                temporary.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                if (originalFeatures != null)
                    foreach (string feature in originalFeatures) temporary.SetInternalFeatureFlag(feature, true);
                temporary.SetInternalFeatureFlag(PlayerUpdates, true);
                var originalFlags = original.hideFlags;
                try
                {
                    if (originalFlags == HideFlags.HideAndDontSave) original.hideFlags = OwnedFlags;
                    InputSystem.settings = temporary;
                }
                finally { if (original) original.hideFlags = originalFlags; }
            }
            catch (Exception setupError)
            {
                try { Restore(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        public static void Pump()
        {
            InputSystem.Update();
            Assert.AreEqual(InputUpdateType.Dynamic, InputState.currentUpdateType,
                "This fixture must process real game input actions through a Dynamic input update.");
        }

        public static int QuietUntil => (int)QuietInputField.GetValue(null);

        public static string Context(Gamepad pad = null) => "frame=" + Time.frameCount + "; quietUntil=" + QuietUntil +
            "; update=" + InputState.currentUpdateType + "; typing=" + DesktopBinding.Typing +
            "; gameplayBlocked=" + KeyBindings.GameplayBlocked + "; listening=" + KeyBindings.Listening +
            (pad == null ? "" : "; pad=" + pad.deviceId + "; currentPad=" + (Gamepad.current?.deviceId ?? -1) +
                "; added=" + pad.added + "; enabled=" + pad.enabled + "; stick=" + pad.leftStick.ReadValue() +
                "; westHeld=" + pad.buttonWest.isPressed + "; westPressed=" + pad.buttonWest.wasPressedThisFrame +
                "; lbHeld=" + pad.leftShoulder.isPressed + "; lbPressed=" + pad.leftShoulder.wasPressedThisFrame);

        public static void Sample(Keyboard keyboard, KeyboardState state)
        {
            Assert.IsTrue(keyboard.added && keyboard.enabled, "The synthetic keyboard must be active before sampling.");
            InputSystem.QueueStateEvent(keyboard, state);
            Pump();
        }

        public static void Sample(Gamepad pad, GamepadState state)
        {
            Assert.IsTrue(pad.added && pad.enabled, "The synthetic gamepad must be active before sampling.");
            InputSystem.QueueStateEvent(pad, state);
            Pump();
            Assert.AreEqual(state.leftStick, pad.leftStick.ReadUnprocessedValue(), "Raw left stick state did not reach the gamepad.");
            Assert.AreEqual(state.rightStick, pad.rightStick.ReadUnprocessedValue(), "Raw right stick state did not reach the gamepad.");
            Assert.AreEqual(state.leftTrigger, pad.leftTrigger.ReadUnprocessedValue(), "Raw left trigger state did not reach the gamepad.");
            Assert.AreEqual(state.rightTrigger, pad.rightTrigger.ReadUnprocessedValue(), "Raw right trigger state did not reach the gamepad.");
            Button(pad.buttonSouth, GamepadButton.South, state.buttons);
            Button(pad.buttonNorth, GamepadButton.North, state.buttons);
            Button(pad.buttonWest, GamepadButton.West, state.buttons);
            Button(pad.buttonEast, GamepadButton.East, state.buttons);
            Button(pad.leftShoulder, GamepadButton.LeftShoulder, state.buttons);
            Button(pad.rightShoulder, GamepadButton.RightShoulder, state.buttons);
            Button(pad.leftStickButton, GamepadButton.LeftStick, state.buttons);
            Button(pad.rightStickButton, GamepadButton.RightStick, state.buttons);
            Button(pad.startButton, GamepadButton.Start, state.buttons);
            Button(pad.selectButton, GamepadButton.Select, state.buttons);
            Button(pad.dpad.up, GamepadButton.DpadUp, state.buttons);
            Button(pad.dpad.down, GamepadButton.DpadDown, state.buttons);
            Button(pad.dpad.left, GamepadButton.DpadLeft, state.buttons);
            Button(pad.dpad.right, GamepadButton.DpadRight, state.buttons);
        }

        static void Button(ButtonControl control, GamepadButton button, uint buttons) =>
            Assert.AreEqual((buttons & (1u << (int)button)) != 0, control.isPressed,
                "Raw " + button + " state did not reach the gamepad.");

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Restore();
        }

        void Restore()
        {
            try
            {
                try { if (temporary) temporary.SetInternalFeatureFlag(PlayerUpdates, originalPlayerUpdates); }
                finally { InputSystem.settings = original; }
            }
            finally
            {
                try { if (temporary && !ReferenceEquals(InputSystem.settings, temporary)) Object.DestroyImmediate(temporary); }
                finally { QuietInputField.SetValue(null, originalQuietUntil); }
            }
        }
    }
}
