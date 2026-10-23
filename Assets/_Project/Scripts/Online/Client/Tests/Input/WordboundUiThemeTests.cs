using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundUiThemeTests
    {
        [Test]
        public void ThemeRejectsUnreadableGeometryAndNonopaqueOrNonfiniteColours()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            try
            {
                Assert.DoesNotThrow(theme.Validate);
                theme.BodySize = 22; Assert.Throws<ArgumentException>(theme.Validate);
                theme.RowHeight = 40; theme.ModuleHeight = 64; Assert.DoesNotThrow(theme.Validate);
                theme.Panel = new Color(1, 1, 1, .5f); Assert.Throws<ArgumentException>(theme.Validate);
                theme.Panel = Color.white; theme.Text = new Color(float.NaN, 0, 0, 1); Assert.Throws<ArgumentException>(theme.Validate);
                theme.Text = new Color(0, float.PositiveInfinity, 0, 1); Assert.Throws<ArgumentException>(theme.Validate);
            }
            finally { Object.DestroyImmediate(theme); }
        }

        [Test]
        public void OwnerScopeRestoresGuiStateAfterFailureWithoutMutatingBorrowedSkinOrFonts()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var styles = new WordboundUiStyles();
            ThemeWindow window = null;
            try
            {
                window = Open(() => {
                    var skin = GUI.skin; var button = skin.button; var background = button.normal.background;
                    var colour = button.normal.textColor; var font = skin.font;
                    GUI.color = Color.cyan; GUI.contentColor = Color.yellow; GUI.backgroundColor = Color.magenta;
                    GUI.enabled = false; var matrix = Matrix4x4.Translate(new Vector3(3, 4, 0)); GUI.matrix = matrix;
                    try
                    {
                        using var scope = styles.Apply(theme);
                        Assert.AreNotSame(skin, GUI.skin); Assert.AreNotSame(button, GUI.skin.button);
                        GUI.enabled = true; GUI.matrix = Matrix4x4.identity;
                        throw new ExpectedFailure();
                    }
                    catch (ExpectedFailure) { }
                    Assert.AreSame(skin, GUI.skin); Assert.AreSame(button, skin.button); Assert.AreSame(background, button.normal.background);
                    Assert.AreEqual(colour, button.normal.textColor); Assert.AreSame(font, skin.font);
                    Assert.AreEqual(Color.cyan, GUI.color); Assert.AreEqual(Color.yellow, GUI.contentColor);
                    Assert.AreEqual(Color.magenta, GUI.backgroundColor); Assert.IsFalse(GUI.enabled); Assert.AreEqual(matrix, GUI.matrix);
                });
                Send(window, EventType.Layout); Send(window, EventType.Repaint);
            }
            finally { if (window) window.Close(); styles.Dispose(); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void ThemeChangesWaitForLayoutAndReleaseOnlyOwnedSkinAndTextures()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var styles = new WordboundUiStyles(); ThemeWindow window = null;
            GUISkin borrowed = null, owned = null;
            try
            {
                window = Open(() => { borrowed = GUI.skin; using var scope = styles.Apply(theme); owned = GUI.skin; });
                Send(window, EventType.Layout);
                int generation = styles.Generation; var oldSkin = owned;
                var oldTextures = new List<Texture2D>((List<Texture2D>)Field(styles, "textures"));
                Assert.AreEqual(8, oldTextures.Count);
                theme.Padding++;
                Send(window, EventType.Repaint);
                Assert.AreEqual(generation, styles.Generation); Assert.AreSame(oldSkin, owned);
                Send(window, EventType.Layout);
                Assert.AreEqual(generation + 1, styles.Generation); Assert.IsFalse(oldSkin);
                foreach (var texture in oldTextures) Assert.IsFalse(texture, "Theme replacement must release native textures.");
                var finalSkin = owned;
                var finalTextures = new List<Texture2D>((List<Texture2D>)Field(styles, "textures"));
                window.Draw = null; styles.Dispose(); styles.Dispose();
                Assert.IsFalse(finalSkin); foreach (var texture in finalTextures) Assert.IsFalse(texture);
                Assert.IsTrue(borrowed); Assert.IsTrue(theme);
            }
            finally { if (window) window.Close(); styles.Dispose(); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void InvalidThemeUsesReadableFallbackWithoutChangingAssignedAsset()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var styles = new WordboundUiStyles(); ThemeWindow window = null;
            try
            {
                theme.RowHeight = 1;
                window = Open(() => { using var scope = styles.Apply(theme); }); Send(window, EventType.Layout);
                Assert.IsNotEmpty(styles.ConfigurationError); Assert.GreaterOrEqual(styles.RowHeight, 30);
                Assert.AreEqual(1, theme.RowHeight);
            }
            finally { if (window) window.Close(); styles.Dispose(); Object.DestroyImmediate(theme); }
        }

        [TestCase(760, 500, 34, 16, 6)]
        [TestCase(420, 340, 52, 24, 12)]
        public void WorkshopToolbarDrawnBoundsAndPointerExclusionTrackResizeAndCustomRows(
            float width, float height, int row, int padding, int gap)
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var root = new GameObject("Theme toolbar bounds test"); ThemeWindow window = null;
            WordboundUiStyles styles = null;
            try
            {
                var workshop = root.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
                styles = (WordboundUiStyles)Field(workshop, "ui");
                theme.RowHeight = row; theme.Padding = padding; theme.Gap = gap;
                window = Open(() => { using var scope = styles.Apply(theme); }); Send(window, EventType.Layout);
                typeof(WordboundWorkshop).GetField("viewport", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(workshop, new Rect(228, 90, width, height));
                var drawn = Property<Rect>(workshop, "ToolbarRect"); var excluded = Property<Rect>(workshop, "ToolbarReserved");
                Assert.GreaterOrEqual(drawn.height, row * 2 + padding * 2 + gap * 2);
                Assert.LessOrEqual(drawn.xMax, width - 8); Assert.Less(drawn.yMax, height);
                Assert.IsTrue(excluded.Contains(drawn.min)); Assert.IsTrue(excluded.Contains(drawn.max - Vector2.one * .01f));
                Assert.IsFalse(excluded.Contains(new Vector2(drawn.center.x, excluded.yMax + .01f)));
                Assert.AreEqual(styles.ToolbarRect(new Vector2(width, height)), drawn);
            }
            finally { if (window) window.Close(); styles?.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(theme); }
        }

        [Test]
        public void MultilineModuleAndMenuUseMeasuredAvailableSpace()
        {
            var theme = ScriptableObject.CreateInstance<WordboundUiTheme>();
            var styles = new WordboundUiStyles(); ThemeWindow window = null;
            try
            {
                window = Open(() => {
                    using var scope = styles.Apply(theme);
                    float measured = GUI.skin.button.CalcHeight(new GUIContent("Flagship Room\n12 × 12 m"), 174);
                    Assert.LessOrEqual(measured, styles.ModuleHeight);
                    foreach (var size in new[] { new Vector2(960, 540), new Vector2(1280, 720) })
                    {
                        Rect menu = styles.MenuRect(size.x, size.y);
                        Assert.GreaterOrEqual(menu.xMin, 12); Assert.GreaterOrEqual(menu.yMin, 12);
                        Assert.LessOrEqual(menu.xMax, size.x - 12); Assert.LessOrEqual(menu.yMax, size.y - 12);
                    }
                }); Send(window, EventType.Layout); Send(window, EventType.Repaint);
            }
            finally { if (window) window.Close(); styles.Dispose(); Object.DestroyImmediate(theme); }
        }

        static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        static ThemeWindow Open(Action draw)
        {
            var window = ScriptableObject.CreateInstance<ThemeWindow>(); window.Draw = draw;
            window.position = new Rect(100, 100, 700, 600); window.ShowUtility(); return window;
        }
        static void Send(ThemeWindow window, EventType type)
        {
            int before = type == EventType.Layout ? window.Layouts : window.Repaints;
            window.SendEvent(new Event { type = type }); Assert.IsNull(window.Failure);
            Assert.Greater(type == EventType.Layout ? window.Layouts : window.Repaints, before);
        }
        sealed class ExpectedFailure : Exception { }
        sealed class ThemeWindow : EditorWindow
        {
            public Action Draw;
            public Exception Failure;
            public int Layouts, Repaints;
            void OnGUI()
            {
                if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint) return;
                var skin = GUI.skin; var colour = GUI.color; var content = GUI.contentColor;
                var background = GUI.backgroundColor; var matrix = GUI.matrix; bool enabled = GUI.enabled;
                try
                {
                    if (Event.current.type == EventType.Layout) Layouts++; else Repaints++;
                    Draw?.Invoke();
                }
                catch (Exception error) { Failure = error; }
                finally
                {
                    GUI.skin = skin; GUI.color = colour; GUI.contentColor = content;
                    GUI.backgroundColor = background; GUI.matrix = matrix; GUI.enabled = enabled;
                }
            }
        }
    }
}
