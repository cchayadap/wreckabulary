using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundHudLayoutTests
    {
        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(854, 480)]
        public void NativePanelMeasurementFitsWrappedStatusesAndShrinksWhenRowsDisappear(int pixelsWide, int pixelsHigh)
        {
            LayoutWindow window = null;
            try
            {
                window = ScriptableObject.CreateInstance<LayoutWindow>();
                float scale = Mathf.Clamp(Mathf.Min(pixelsWide / 1280f, pixelsHigh / 720f), .6f, 1.8f);
                window.Size = new Vector2(pixelsWide / scale, pixelsHigh / scale);
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure);
                Assert.Greater(window.Layouts, 0); Assert.Greater(window.Repaints, 0);
                Assert.Less(window.CompactHeight, 82, "Two short rows must not retain the former fixed82px panel.");
                Assert.Greater(window.FullHeight, window.CompactHeight);
            }
            finally { if (window) window.Close(); }
        }

        sealed class LayoutWindow : EditorWindow
        {
            static readonly Type Panel = typeof(WordboundMatchHud).GetNestedType("HudPanel", BindingFlags.NonPublic);
            public Vector2 Size;
            public Exception Failure;
            public int Layouts, Repaints;
            public float CompactHeight, FullHeight;
            object status, footer;
            GUIStyle label;
            void OnGUI()
            {
                try
                {
                    if (Size.x <= 0) return;
                    if (Event.current.type == EventType.Layout) Layouts++;
                    if (Event.current.type == EventType.Repaint) Repaints++;
                    status ??= Activator.CreateInstance(Panel, true);
                    footer ??= Activator.CreateInstance(Panel, true);
                    label ??= new GUIStyle(GUI.skin.label) { wordWrap = true };
                    Rows(status, "Time Run", "Lap 1 / 3 · Checkpoint 1 / 32",
                        "Smash the marked furniture, collect its letters, and spell the equipment needed to reach the next room safely.",
                        "24:59 remaining", "Health100 · Stamina100", "Hold [LEFT BUTTON] to raise SHIELD",
                        "Waiting for the host…", "Confirming result…");
                    var top = Draw(status, 18, Mathf.Min(380, Size.x - 36), false);
                    Rows(footer, "Letters: ABCDEFGHIJKLMNOPQRSTUVWXYZ", "[E] Pick up · [Q] Craft · [LEFT BUTTON] Smash · [ESC] Menu",
                        "That spot is blocked. Move to a clear space, keep the nearby object in sight, and try again.",
                        "[E] Select practice station · [ESC] Station details");
                    var bottom = Draw(footer, Size.y - 18, Size.x - 36, true);
                    FullHeight = bottom.height;
                    Assert.GreaterOrEqual(top.yMin, 0); Assert.Less(top.yMax, bottom.yMin);
                    Assert.AreEqual(Size.y - 18, bottom.yMax, .001f);
                    CheckRows(status, top); CheckRows(footer, bottom);
                    Rows(footer, "Letters: —", "[E] Pick up · [Q] Craft · [ESC] Menu");
                    CompactHeight = Draw(footer, Size.y - 18, Size.x - 36, true).height;
                }
                catch (Exception error) { Failure = error; }
            }
            static void Rows(object panel, params string[] values)
            {
                Panel.GetMethod("Begin").Invoke(panel, null);
                foreach (string text in values) Panel.GetMethod("Add").Invoke(panel, new object[] { text });
            }
            Rect Draw(object panel, float y, float width, bool bottom) => (Rect)Panel.GetMethod("Draw")
                .Invoke(panel, new object[] { 18f, y, width, label, GUI.skin.box, bottom });
            static void CheckRows(object panel, Rect area)
            {
                var rects = (IList)Panel.GetField("rects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                int count = (int)Panel.GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                float bottom = 0;
                for (int i = 0; i < count; i++)
                {
                    var row = (Rect)rects[i];
                    Assert.GreaterOrEqual(row.yMin, bottom);
                    Assert.Greater(row.height, 0);
                    Assert.GreaterOrEqual(row.xMin, 0); Assert.LessOrEqual(row.xMax, area.width);
                    Assert.LessOrEqual(row.yMax, area.height);
                    bottom = row.yMax;
                }
            }
        }
    }
}
