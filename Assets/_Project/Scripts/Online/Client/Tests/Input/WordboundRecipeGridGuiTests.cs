using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundRecipeGridGuiTests
    {
        [TestCase(1280, 720, 14)]
        [TestCase(1920, 1080, 14)]
        [TestCase(854, 480, 22)]
        public void ActualMenuSeparatesRecipeListPreviewAndPinnedActionsAndDefersSelection(int width, int height, int font)
        {
            using var f = new WordboundRecipeGridFixture(font);
            var previous = Gamepad.current; Gamepad pad = null; GridWindow window = null;
            try
            {
                pad = InputSystem.AddDevice<Gamepad>();
                var binding = (WordboundInputBinding)typeof(WordboundPlayerView).GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.View);
                typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                typeof(WordboundInputBinding).GetField("gamepad", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, new GamepadBinding(pad));
                f.Set("persistentNotice", "Reconnecting…");
                ((WordboundMenuFocus)f.Get("menuFocus")).Reset("recipe:STOOL");
                window = ScriptableObject.CreateInstance<GridWindow>(); window.Fixture = f; window.Size = new Vector2(width, height);
                window.position = new Rect(80, 80, width, height); window.ShowUtility();
                window.ConfirmTile = true;
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.Greater(window.Repaints, 0); Assert.Greater(window.Layouts, 0);
                Assert.IsFalse(window.SelectionChangedDuringGui); Assert.AreNotEqual("STOOL", f.Get("selectedRecipe"));
                f.Call("ExecuteMenuAction", true); Assert.AreEqual("STOOL", f.Get("selectedRecipe"));
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.IsTrue(f.View.MenuOpen);
                Assert.AreEqual("Reconnecting…", f.Get("persistentNotice"));
                Assert.AreEqual("Reconnecting…", f.Get("menuConnectionStatus"));
            }
            finally
            {
                if (window) window.Close();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }

        sealed class GridWindow : EditorWindow
        {
            internal WordboundRecipeGridFixture Fixture;
            internal Vector2 Size;
            internal bool ConfirmTile, SelectionChangedDuringGui;
            internal int Layouts, Repaints;
            internal Exception Failure;
            void OnGUI()
            {
                if (Fixture == null || !Fixture.Hud) return;
                try
                {
                    string before = (string)Fixture.Get("selectedRecipe");
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint && ConfirmTile)
                    { ConfirmTile = false; ((WordboundMenuFocus)Fixture.Get("menuFocus")).Confirm(); }
                    Fixture.Call("Menu", Size.x, Size.y, true);
                    SelectionChangedDuringGui |= before != (string)Fixture.Get("selectedRecipe");
                    if (Event.current.type != EventType.Repaint) return;
                    Repaints++;
                    var viewport = (Rect)Fixture.Get("recipeViewportRect");
                    var detail = (Rect)Fixture.Get("recipeDetailRect");
                    var back = (Rect)Fixture.Get("recipeReturnRect");
                    var craft = (Rect)Fixture.Get("recipeCraftRect");
                    var styles = (WordboundUiStyles)Fixture.Get("ui");
                    Rect area = styles.MenuRect(Size.x, Size.y);
                    Assert.GreaterOrEqual(viewport.height, 30, "At least one scrollable inspection area remains at maximum text size.");
                    Assert.LessOrEqual(viewport.xMax + styles.Gap, detail.xMin);
                    Assert.AreEqual(viewport.yMin, detail.yMin, .01f);
                    Assert.AreEqual(viewport.yMax, detail.yMax, .01f);
                    Assert.GreaterOrEqual(viewport.width, 180, "Recipe icons and maximum-size names retain a useful list width.");
                    Assert.GreaterOrEqual(detail.width, 200, "The selected-item preview has its own usable column.");
                    Assert.GreaterOrEqual(craft.height, styles.RowHeight);
                    Assert.GreaterOrEqual(craft.xMin, detail.xMin);
                    Assert.LessOrEqual(craft.xMax, detail.xMax);
                    Assert.LessOrEqual(craft.yMax, detail.yMax);
                    Assert.GreaterOrEqual(craft.yMin, detail.yMin);
                    Assert.LessOrEqual(detail.yMax, back.yMin + 1);
                    Assert.LessOrEqual(back.yMax, area.height - styles.Padding + 1);
                    Assert.LessOrEqual(detail.xMax, area.width - styles.Padding + 1);
                    Assert.GreaterOrEqual(viewport.xMin, styles.Padding);
                    Assert.AreEqual(1, (int)Fixture.Get("recipeColumns"), "One recipe per row keeps browsing readable.");
                    var name = (GUIStyle)Fixture.Get("recipeName");
                    Assert.AreEqual(Fixture.Theme.BodySize, name.fontSize);
                    Assert.AreEqual(styles.SingleLine.normal.textColor, name.normal.textColor);
                    var iconArea = new Rect(0, 0, 120, 60);
                    var fit = (Rect)typeof(WordboundMatchHud).GetMethod("FitRecipeIcon", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { iconArea, 192f, 192f });
                    Assert.AreEqual(1, fit.width / fit.height, .0001f);
                    Assert.IsTrue(iconArea.Contains(fit.min)); Assert.LessOrEqual(fit.xMax, iconArea.xMax); Assert.LessOrEqual(fit.yMax, iconArea.yMax);
                }
                catch (Exception error) { Failure = error; }
            }
        }
    }
}
