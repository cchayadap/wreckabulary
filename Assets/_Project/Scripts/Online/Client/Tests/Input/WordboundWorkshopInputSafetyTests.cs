using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundWorkshopInputSafetyTests
    {
        GameObject root;
        WordboundWorkshop workshop;
        Gamepad pad, previousPad;
        string folder;
        bool oldTyping;
        WordboundNativeInputScope input;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; oldTyping = DesktopBinding.Typing;
            try
            {
                input = new WordboundNativeInputScope();
                DesktopBinding.Typing = false; pad = InputSystem.AddDevice<Gamepad>();
                folder = Path.Combine(Path.GetTempPath(), "wordbound-pad-" + Guid.NewGuid().ToString("N"));
                root = new GameObject("Workshop input safety native test");
                workshop = root.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
                workshop.Open(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue(),
                    MatchStateValidation.Requirements(ModeDefinition.For("Freestyle")), folder);
                Sample(default, true); Sample(default, true);
            }
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (root) Object.DestroyImmediate(root);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
                if (folder != null && Path.GetFullPath(folder).StartsWith(Path.Combine(Path.GetTempPath(), "wordbound-pad-"), StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            finally
            {
                try { input?.Dispose(); }
                finally { input = null; DesktopBinding.Typing = oldTyping; }
            }
        }

        [Test]
        public void ActualUndoPathIgnoresUnfocusedAndHeldResumeInputUntilNeutral()
        {
            string original = workshop.History.Current.Course().Title;
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Pending user edit");
            var held = new GamepadState().WithButton(GamepadButton.LeftShoulder);
            Sample(held, false); Sample(held, true);
            Assert.AreEqual("Pending user edit", workshop.History.Current.Course().Title);
            Sample(default, true); Sample(default, true); Sample(held, true);
            Assert.AreEqual(original, workshop.History.Current.Course().Title);
        }

        [Test]
        public void ActualUndoPathIsSuppressedWhileEditingTextAndAfterPadReplacement()
        {
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Typing edit");
            Set("workshopTyping", true);
            var held = new GamepadState().WithButton(GamepadButton.LeftShoulder);
            Sample(held, true); Set("workshopTyping", false); Sample(held, true);
            Assert.AreEqual("Typing edit", workshop.History.Current.Course().Title);
            InputSystem.RemoveDevice(pad); pad = InputSystem.AddDevice<Gamepad>();
            Sample(held, true);
            Assert.AreEqual("Typing edit", workshop.History.Current.Course().Title);
            Sample(default, true); Sample(default, true); Sample(held, true);
            Assert.AreNotEqual("Typing edit", workshop.History.Current.Course().Title);
        }

        [Test]
        public void UnplugCancelsUncommittedControllerDragWithoutCancellingAnIdleMousePlacement()
        {
            string id = workshop.History.Current.Course().Modules.First().Id;
            workshop.History.Execute("Select", draft => draft.Selection.Add(id));
            string before = workshop.History.Current.CourseJsonText;
            Sample(new GamepadState { leftStick = new Vector2(.9f, 0) }, true);
            Assert.IsTrue(Get<bool>("controllerMoving"));
            InputSystem.RemoveDevice(pad);
            Invoke("ControllerInput", true);
            Assert.IsFalse(Get<bool>("controllerMoving"));
            Assert.AreEqual(before, workshop.History.Current.CourseJsonText);
            Set("placing", "Mouse placement");
            Invoke("ControllerInput", true);
            Assert.AreEqual("Mouse placement", Get<string>("placing"));
        }

        void Sample(GamepadState state, bool focused)
        {
            WordboundNativeInputScope.Sample(pad, state);
            Invoke("ControllerInput", focused);
        }
        T Get<T>(string name) => (T)typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workshop);
        void Set(string name, object value) => typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workshop, value);
        void Invoke(string name, params object[] args)
        {
            try { typeof(WordboundWorkshop).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
