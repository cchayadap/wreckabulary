using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundMenuGuiTests
    {
        [TestCase("journal", "journalOpen", false, true)]
        [TestCase("help", "helpOpen", false, true)]
        [TestCase("quick-tips", "showQuickTips", true, false)]
        public void OptionalPageConfirmationDuringRepaintKeepsTheMeasuredLayoutUntilNextUpdate(string button, string stateField, bool before, bool after)
        {
            var root = new GameObject("Wordbound menu layout native test");
            MenuWindow window = null;
            Gamepad previousPad = Gamepad.current, pad = null;
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, root.AddComponent<Camera>()); view.enabled = false;
                pad = InputSystem.AddDevice<Gamepad>();
                var binding = (WordboundInputBinding)typeof(WordboundPlayerView)
                    .GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                typeof(WordboundInputBinding).GetField("gamepad", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, new GamepadBinding(pad));
                var hud = root.AddComponent<WordboundMatchHud>(); hud.enabled = false;
                var story = root.AddComponent<WordboundStoryView>(); story.enabled = false;
                var clue = (WordboundStoryNotice)Activator.CreateInstance(typeof(WordboundStoryNotice),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { "native-clue", 1, 0, "Chapter", "Room", "Collected clue", new CoursePoint() }, null);
                typeof(WordboundStoryView).GetProperty("Journal").SetValue(story, new[] { clue });
                Field("controller").SetValue(hud, controller); Field("view").SetValue(hud, view); Field("story").SetValue(hud, story);
                if (button == "quick-tips") Field("helpOpen").SetValue(hud, true);
                ((WordboundMenuFocus)Field("menuFocus").GetValue(hud)).Reset(button);
                window = ScriptableObject.CreateInstance<MenuWindow>(); window.Hud = hud; window.StateField = stateField;
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                window.QueueJournalOnRepaint = true;
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure);
                Assert.Greater(window.LayoutCount, 0, "The actual editor window must process Layout.");
                Assert.Greater(window.RepaintCount, 0, "The actual editor window must process Repaint.");
                Assert.IsFalse(window.ChangedDuringRepaint);
                Assert.AreEqual(before, (bool)Field(stateField).GetValue(hud));
                Invoke(hud, "ExecuteMenuAction", true);
                Assert.AreEqual(after, (bool)Field(stateField).GetValue(hud));
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure);
                Assert.IsFalse(window.ChangedDuringRepaint);
            }
            finally
            {
                if (window) window.Close();
                var hud = root.GetComponent<WordboundMatchHud>();
                if (hud) ((WordboundUiStyles)Field("ui").GetValue(hud)).Dispose();
                Object.DestroyImmediate(root);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
            }
        }

        [Test]
        public void PendingMenuActionsAreDiscardedOnLostFocusAndRunAtMostOnce()
        {
            var root = new GameObject("Wordbound deferred menu native test");
            try
            {
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>());
                var hud = root.AddComponent<WordboundMatchHud>(); Field("view").SetValue(hud, view);
                int actions = 0;
                Invoke(hud, "QueueMenuAction", (Action)(() => actions++));
                Assert.AreEqual(0, actions);
                Invoke(hud, "ExecuteMenuAction", false);
                Invoke(hud, "ExecuteMenuAction", true);
                Assert.AreEqual(0, actions);
                Invoke(hud, "QueueMenuAction", (Action)(() => actions++));
                Invoke(hud, "ExecuteMenuAction", true); Invoke(hud, "ExecuteMenuAction", true);
                Assert.AreEqual(1, actions);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static object Invoke(WordboundMatchHud hud, string name, params object[] args)
        {
            try { return typeof(WordboundMatchHud).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        sealed class MenuWindow : EditorWindow
        {
            public WordboundMatchHud Hud;
            public string StateField;
            public bool QueueJournalOnRepaint, ChangedDuringRepaint;
            public int LayoutCount, RepaintCount;
            public Exception Failure;
            bool measuredJournal;
            void OnGUI()
            {
                if (!Hud) return;
                try
                {
                    bool before = (bool)Field(StateField).GetValue(Hud);
                    if (Event.current.type == EventType.Layout) { measuredJournal = before; LayoutCount++; }
                    if (Event.current.type == EventType.Repaint && QueueJournalOnRepaint)
                    {
                        QueueJournalOnRepaint = false;
                        ((WordboundMenuFocus)Field("menuFocus").GetValue(Hud)).Confirm();
                    }
                    Invoke(Hud, "Menu", 640f, 480f, true);
                    if (Event.current.type == EventType.Repaint)
                    {
                        RepaintCount++;
                        ChangedDuringRepaint |= measuredJournal != before || before != (bool)Field(StateField).GetValue(Hud);
                    }
                }
                catch (Exception error) { Failure = error; }
            }
        }
    }
}
