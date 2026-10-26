using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundLauncherFailureInputTests
    {
        GameObject root;
        WordboundLauncher launcher;
        WordboundNativeInputScope input;
        Keyboard keyboard, previousKeyboard;
        Gamepad pad, previousPad;
        bool previousTyping;

        [SetUp]
        public void SetUp()
        {
            previousKeyboard = Keyboard.current; previousPad = Gamepad.current;
            previousTyping = DesktopBinding.Typing;
            try
            {
            input = new WordboundNativeInputScope();
            DesktopBinding.Typing = false;
            keyboard = InputSystem.AddDevice<Keyboard>(); pad = InputSystem.AddDevice<Gamepad>();
            root = new GameObject("Owned startup-failure input fixture");
            launcher = root.AddComponent<WordboundLauncher>();
            Set("failure", "Solo could not start. Return to the lobby and try again.");
            SendKey(default); Pad(default);
            }
            catch (Exception setup)
            {
                try { TearDown(); }
                catch (Exception cleanup) { throw new AggregateException(setup, cleanup); }
                throw;
            }
        }

        [Test]
        public void EscapeUsesExistingInputStateOnceAfterRelease()
        {
            Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape));
            Assert.IsTrue(Read());
            Assert.IsFalse(Read(), "The same input frame must not issue a second return.");
            SendKey(default); Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape)); Assert.IsTrue(Read());
        }

        [Test]
        public void PadBackRequiresNeutralAfterSelectionAndFocusLoss()
        {
            Pad(new GamepadState().WithButton(GamepadButton.East));
            Assert.IsFalse(Read(), "The selecting held B must not close the new failure surface.");
            Pad(default); Assert.IsFalse(Read());
            Pad(default); Assert.IsFalse(Read());
            Pad(new GamepadState().WithButton(GamepadButton.East)); Assert.IsTrue(Read());
            Pad(default); Assert.IsFalse(Read());
            Assert.IsFalse(Read(false));
            Pad(new GamepadState().WithButton(GamepadButton.East)); Assert.IsFalse(Read());
            Pad(default); Assert.IsFalse(Read());
            Pad(default); Assert.IsFalse(Read());
            Pad(new GamepadState().WithButton(GamepadButton.East)); Assert.IsTrue(Read());
        }

        [UnityTest]
        public IEnumerator TypingAndInputLeaseDiscardBackUntilReleased()
        {
            Assert.IsFalse(Read());
            DesktopBinding.Typing = true;
            SendKey(new KeyboardState(Key.Escape)); Assert.IsFalse(Read());
            DesktopBinding.Typing = false;
            Assert.IsFalse(Read());
            SendKey(default); Assert.IsFalse(Read());
            using (KeyBindings.BlockGameplay())
            {
                Invoke("QueueStartupFailureReturn");
                SendKey(new KeyboardState(Key.Escape)); Assert.IsFalse(Read());
            }
            int quietThrough = WordboundNativeInputScope.QuietUntil;
            Assert.IsTrue(KeyBindings.GameplayBlocked, "Lease release retains its production quiet interval.");
            double deadline = EditorApplication.timeSinceStartup + 5;
            while (KeyBindings.GameplayBlocked)
            {
                Assert.Less(EditorApplication.timeSinceStartup, deadline,
                    "Input capture did not release: " + WordboundNativeInputScope.Context());
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            Assert.Greater(Time.frameCount, quietThrough);
            Assert.IsFalse(KeyBindings.GameplayBlocked, "The existing lease release must finish its normal quiet frames.");
            Assert.IsFalse(Read());
            SendKey(default); Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape)); Assert.IsTrue(Read());
        }

        [Test]
        public void PointerReturnIsDeferredAndBoundToTheSameFailure()
        {
            Invoke("QueueStartupFailureReturn");
            Set("failure", "A different startup failure.");
            Assert.IsFalse(Read(), "A queued click from an older failure must not execute.");
            Invoke("QueueStartupFailureReturn");
            Assert.IsTrue(Read()); Assert.IsFalse(Read());
            Invoke("QueueStartupFailureReturn");
            Assert.IsFalse(Read(false));
            Assert.IsFalse(Read(), "An unfocused click must not replay on focus regain.");
        }

        [Test]
        public void AbsentFailureOrAnActiveLocalClientDoesNotConsumeBack()
        {
            Set("failure", null);
            Invoke("QueueStartupFailureReturn");
            SendKey(new KeyboardState(Key.Escape)); Assert.IsFalse(Read());
            Assert.IsNull(Get("failureInput"), "Normal launcher operation must not allocate another input owner.");
            Set("failure", "Standalone failure");
            var ownedClient = root.AddComponent<WordboundLocalMatchClient>();
            Set("localClient", ownedClient);
            Invoke("QueueStartupFailureReturn");
            SendKey(default); Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape)); Assert.IsFalse(Read());
            Assert.IsNull(Get("failureInput"), "The active game HUD retains input ownership.");
            Set("localClient", null); Object.DestroyImmediate(ownedClient);
            SendKey(default); Assert.IsFalse(Read());
            Assert.IsNotNull(Get("failureInput"));
            Invoke("DisposeFailureInput");
            Assert.IsNull(Get("failureInput"));
        }

        [Test]
        public void PartiallyCreatedPartyMenuAllowsBackButInitializedMenuOwnsItsInput()
        {
            var menu = root.AddComponent<WordboundPartyMenu>();
            menu.enabled = false;
            Set("menu", menu);
            Assert.IsNull(menu.Coordinator);
            Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape)); Assert.IsTrue(Read());
            Invoke("DisposeFailureInput");
            var assets = Resources.Load<WordboundLaunchAssets>(WordboundLaunchAssets.ResourcePath);
            var data = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsNotNull(assets); Assert.IsNotNull(data);
            menu.Initialize(assets.PublicTrust.text, data.items.text, data.rules.text, assets.MapPoolVersion, assets.PublicCoursesJson);
            Assert.IsNotNull(menu.Coordinator); Assert.IsFalse(menu.Coordinator.Authenticated);
            SendKey(default); Assert.IsFalse(Read());
            SendKey(new KeyboardState(Key.Escape)); Assert.IsFalse(Read());
            Assert.IsNull(Get("failureInput"), "An initialized party menu retains its own input; no second owner is allocated.");
        }

        void SendKey(KeyboardState state) => WordboundNativeInputScope.Sample(keyboard, state);
        void Pad(GamepadState state) => WordboundNativeInputScope.Sample(pad, state);
        bool Read(bool focused = true) => (bool)Invoke("ReadStartupFailureBack", focused);
        object Get(string field) => typeof(WordboundLauncher).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(launcher);
        void Set(string field, object value) => typeof(WordboundLauncher).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(launcher, value);
        object Invoke(string method, params object[] args)
        {
            try { return typeof(WordboundLauncher).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(launcher, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        [TearDown]
        public void TearDown()
        {
            var errors = new System.Collections.Generic.List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            Attempt(() => { if (root) Object.DestroyImmediate(root); });
            Attempt(() => { if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard); });
            Attempt(() => { if (pad != null && pad.added) InputSystem.RemoveDevice(pad); });
            Attempt(() => { if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent(); });
            Attempt(() => { if (previousPad != null && previousPad.added) previousPad.MakeCurrent(); });
            Attempt(() => DesktopBinding.Typing = previousTyping);
            Attempt(() => input?.Dispose());
            Assert.IsEmpty(errors);
        }
    }
}
