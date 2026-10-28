using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundHudClarityTests
    {
        [Test]
        public void ActionNoticeExpiresWithoutExpiringConnectionFailure()
        {
            var root = new GameObject("HUD notice policy test");
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, root.AddComponent<Camera>());
                var hud = root.AddComponent<WordboundMatchHud>();
                Field("controller").SetValue(hud, controller);
                Field("view").SetValue(hud, view);
                Field("returnLabel").SetValue(hud, "Return to workshop");
                Invoke(hud, "SetNotice", "Move closer and keep the object in sight.");
                double expiry = (double)Field("noticeUntil").GetValue(hud);
                Assert.That(expiry - Time.unscaledTimeAsDouble, Is.InRange(3.9, 4.0));
                Assert.AreEqual("Move closer and keep the object in sight.", Invoke(hud, "TransientNotice", expiry - .001));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry + 100));
                hud.ShowFailure("Reconnecting…");
                Assert.AreEqual("Reconnecting…", Invoke(hud, "ContextPrompt"));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry + 100));
                Assert.AreEqual("Reconnecting…", Invoke(hud, "ContextPrompt"));
                hud.ShowFailure("");
                Assert.AreEqual("Preparing the world…", Invoke(hud, "ContextPrompt"));
                typeof(WordboundGameplayController).GetProperty("Fault").SetValue(controller, "test connection unavailable");
                Assert.AreEqual("Connection interrupted · Return to workshop", Invoke(hud, "ContextPrompt"));
                Invoke(hud, "SetNotice", "");
                Assert.AreEqual("Connection interrupted · Return to workshop", Invoke(hud, "ContextPrompt"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HelpBackReturnsToEquipmentBeforeResuming()
        {
            var root = new GameObject("HUD help back test");
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, root.AddComponent<Camera>()); view.SetMenuOpen(true);
                var hud = root.AddComponent<WordboundMatchHud>();
                Field("controller").SetValue(hud, controller); Field("view").SetValue(hud, view);
                Field("initialized").SetValue(hud, true); Field("helpOpen").SetValue(hud, true);
                Invoke(hud, "ReadMenuCommands", new WordboundMenuCommands(0, false, true));
                Assert.IsFalse((bool)Field("helpOpen").GetValue(hud));
                Assert.IsTrue(view.MenuOpen);
                Assert.AreEqual("help", ((WordboundMenuFocus)Field("menuFocus").GetValue(hud)).Selected);
                Invoke(hud, "ReadMenuCommands", new WordboundMenuCommands(0, false, true));
                Assert.IsFalse(view.MenuOpen);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void OptionalHelpReadsActualDesktopAndControllerBindings()
        {
            var root = new GameObject("HUD help binding test");
            Gamepad prior = Gamepad.current, pad = null;
            try
            {
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>());
                var hud = root.AddComponent<WordboundMatchHud>(); Field("view").SetValue(hud, view);
                var binding = (WordboundInputBinding)typeof(WordboundPlayerView).GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (bool usePad in new[] { false, true })
                {
                    if (usePad)
                    {
                        pad = InputSystem.AddDevice<Gamepad>();
                        typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                    }
                    var rows = (string[])Invoke(hud, "HelpControls");
                    Assert.AreEqual(8, rows.Length);
                    Assert.That(rows[0], Does.StartWith("[" + view.ControlHint(WordboundControl.Interact) + "]"));
                    Assert.That(rows[1], Does.StartWith("[" + view.ControlHint(WordboundControl.Attack) + "]"));
                    Assert.That(rows[2], Does.StartWith("[" + view.ControlHint(WordboundControl.Spell) + "]"));
                    Assert.That(rows[3], Does.Contain("[" + view.ControlHint(WordboundControl.Aim) + "]"));
                    Assert.That(rows[4], Does.Contain("[" + view.ControlHint(WordboundControl.Drop) + "]"));
                    Assert.That(rows[6], Does.Contain("[" + view.ControlHint(WordboundControl.Block) + "]"));
                    Assert.That(rows[7], Does.StartWith("[" + view.ControlHint(WordboundControl.Menu) + "]"));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (prior != null && prior.added) prior.MakeCurrent();
            }
        }

        [Test]
        public void UnknownBindingShowsConciseActionsWithoutInventedKeysOrDuplicatedLabels()
        {
            var root = new GameObject("Unknown HUD hint test");
            try
            {
                var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), new ScriptedBinding());
                var hud = root.AddComponent<WordboundMatchHud>(); Field("view").SetValue(hud, view);
                Assert.AreEqual("Craft", Invoke(hud, "Hint", WordboundControl.Spell, "Craft"));
                Assert.AreEqual("Menu", Invoke(hud, "Hint", WordboundControl.Menu, "Menu"));
                Assert.AreEqual("Smash furniture", Invoke(hud, "Hint", WordboundControl.Attack, "Smash furniture"));
                CollectionAssert.AreEqual(new[] { "Pick up / use", "Smash", "Craft from letters", "Throw", "Hold Drop",
                    "Change equipment", "Hold Block with a shield", "Resume / menu" }, (string[])Invoke(hud, "HelpControls"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void EmptyLetterBagIsHiddenAndLongBagIsSummarizedWithoutMutation()
        {
            var bag = LetterBag.FromWord("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            string before = bag.ToString();
            Assert.AreEqual("", Static("CompactLetters", ""));
            Assert.AreEqual("ABT", Static("CompactLetters", "ABT"));
            Assert.AreEqual("ABCDEFGHIJKLMNOPQR… +8", Static("CompactLetters", before));
            Assert.AreEqual(before, bag.ToString());
        }

        [Test]
        public void ContextCopyDistinguishesUsePickupLetterAndKeepsake()
        {
            Assert.AreEqual("Collect letter", Static("InteractionCaption", WorldBodyKind.Tile, null, false));
            Assert.AreEqual("Take keepsake", Static("InteractionCaption", WorldBodyKind.Keepsake, null, false));
            Assert.AreEqual("Use FAN", Static("InteractionCaption", WorldBodyKind.Item, "FAN", true));
            Assert.AreEqual("Pick up FAN", Static("InteractionCaption", WorldBodyKind.Item, "FAN", false));
            Assert.AreEqual("", Static("InteractionCaption", WorldBodyKind.Item, null, false));
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(854, 480)]
        public void ActualOverlayUsesEditableThemeAndCompactPanelsDoNotOverlap(int pixelsWide, int pixelsHigh)
        {
            var root = new GameObject("HUD compact layout test");
            OverlayWindow window = null;
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                theme.Panel = new Color(.15f, .2f, .3f); theme.Text = new Color(.9f, .95f, 1f);
                theme.BodySize = 22; theme.RowHeight = 42; theme.ModuleHeight = 64;
                var hud = root.AddComponent<WordboundMatchHud>(); hud.enabled = false; hud.Theme = theme;
                float scale = Mathf.Clamp(Mathf.Min(pixelsWide / 1280f, pixelsHigh / 720f), .6f, 1.8f);
                window = ScriptableObject.CreateInstance<OverlayWindow>(); window.Hud = hud;
                window.Size = new Vector2(pixelsWide / scale, pixelsHigh / scale);
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.Greater(window.Repaints, 0);
            }
            finally
            {
                if (window) window.Close(); Object.DestroyImmediate(root); Object.DestroyImmediate(theme);
            }
        }

        static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static object Invoke(WordboundMatchHud hud, string method, params object[] args) => typeof(WordboundMatchHud)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, args);
        static object Static(string method, params object[] args) => typeof(WordboundMatchHud)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        sealed class OverlayWindow : EditorWindow
        {
            public WordboundMatchHud Hud;
            public Vector2 Size;
            public Exception Failure;
            public int Repaints;
            void OnGUI()
            {
                if (!Hud) return;
                try
                {
                    Rows("statusPanel", "Time Run · 24:58", "Lap 1/3 · Checkpoint 1/32", "Health 100 · Stamina 100");
                    Rows("inventoryPanel", "Letters ABCDEFGHIJKLMNOPQR… +8", "SHIELD");
                    Rows("actionPanel", "Move closer and keep the object in sight.");
                    Rows("menuHintPanel", "[Q] Craft · [ESC] Menu");
                    var before = GUI.backgroundColor;
                    Invoke(Hud, "DrawOverlay", Size.x, Size.y);
                    Assert.AreEqual(before, GUI.backgroundColor);
                    var label = (GUIStyle)Field("overlayLabel").GetValue(Hud);
                    var box = (GUIStyle)Field("overlayBox").GetValue(Hud);
                    var styles = (WordboundUiStyles)Field("ui").GetValue(Hud);
                    Assert.AreEqual(Hud.Theme.Text, label.normal.textColor);
                    Assert.AreSame(styles.Panel.normal.background, box.normal.background);
                    float available = Size.x - 36;
                    var left = Draw("inventoryPanel", 18, available * .23f, 0, label, box);
                    var center = Draw("actionPanel", Size.x * .5f, available * .5f, .5f, label, box);
                    var right = Draw("menuHintPanel", Size.x - 18, available * .23f, 1, label, box);
                    Assert.Less(left.xMax, center.xMin); Assert.Less(center.xMax, right.xMin);
                    foreach (var rect in new[] { left, center, right })
                    { Assert.GreaterOrEqual(rect.xMin, 18); Assert.LessOrEqual(rect.xMax, Size.x - 18); Assert.AreEqual(Size.y - 18, rect.yMax, .001f); }
                    Rows("actionPanel");
                    Assert.AreEqual(0, Draw("actionPanel", Size.x * .5f, available * .5f, .5f, label, box).height);
                    Rows("actionPanel", "[E] Collect letter");
                    Assert.Less(Draw("actionPanel", Size.x * .5f, available * .5f, .5f, label, box).width, available * .5f);
                    if (Event.current.type == EventType.Repaint) Repaints++;
                }
                catch (Exception failure) { Failure = failure; }
            }
            void Rows(string field, params string[] values)
            {
                var panel = Field(field).GetValue(Hud); var type = panel.GetType();
                type.GetMethod("Begin").Invoke(panel, null);
                foreach (string value in values) type.GetMethod("Add").Invoke(panel, new object[] { value });
            }
            Rect Draw(string field, float anchor, float maximum, float alignment, GUIStyle label, GUIStyle box)
            {
                var panel = Field(field).GetValue(Hud);
                return (Rect)panel.GetType().GetMethod("DrawCompact").Invoke(panel,
                    new object[] { anchor, Size.y - 18, maximum, label, box, true, alignment });
            }
        }
    }
}
