using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public static class WordboundWorkshopOverlay
    {
        public const float LineWidth = 2;
        public static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y);

        public static bool ClipLine(Rect viewport, ref Vector2 from, ref Vector2 to)
        {
            const float inset = LineWidth * .5f + .5f;
            if (!Finite(from) || !Finite(to) || !Finite(viewport.position) || !Finite(viewport.size) ||
                viewport.width <= inset * 2 || viewport.height <= inset * 2) return false;
            var bounds = Rect.MinMaxRect(viewport.xMin + inset, viewport.yMin + inset, viewport.xMax - inset, viewport.yMax - inset);
            double dx = (double)to.x - from.x, dy = (double)to.y - from.y, first = 0, last = 1;
            if (!Clip(-dx, from.x - bounds.xMin, ref first, ref last) || !Clip(dx, bounds.xMax - from.x, ref first, ref last) ||
                !Clip(-dy, from.y - bounds.yMin, ref first, ref last) || !Clip(dy, bounds.yMax - from.y, ref first, ref last)) return false;
            var start = from;
            from = new Vector2(Mathf.Clamp((float)(start.x + dx * first), bounds.xMin, bounds.xMax),
                Mathf.Clamp((float)(start.y + dy * first), bounds.yMin, bounds.yMax));
            to = new Vector2(Mathf.Clamp((float)(start.x + dx * last), bounds.xMin, bounds.xMax),
                Mathf.Clamp((float)(start.y + dy * last), bounds.yMin, bounds.yMax));
            return (to - from).sqrMagnitude > .0001f;
        }

        static bool Clip(double direction, double offset, ref double first, ref double last)
        {
            if (direction == 0) return offset >= 0;
            double along = offset / direction;
            if (direction < 0) { if (along > last) return false; first = Math.Max(first, along); }
            else { if (along < first) return false; last = Math.Min(last, along); }
            return true;
        }

        public static bool PlaceLabel(Vector2 anchor, Vector2 size, Rect viewport, IList<Rect> occupied, out Rect result)
        {
            result = default;
            if (!Finite(anchor) || !Finite(size) || size.x <= 0 || size.y <= 0 || !viewport.Contains(anchor)) return false;
            for (int candidate = 0; candidate < 8; candidate++)
            {
                float x = anchor.x - size.x * .5f, y = anchor.y - size.y - 8;
                if (candidate == 1) { x = anchor.x + 12; y = anchor.y - size.y * .5f; }
                if (candidate == 2) { x = anchor.x - size.x - 12; y = anchor.y - size.y * .5f; }
                if (candidate == 3) y = anchor.y + 10;
                if (candidate >= 4)
                {
                    x = anchor.x + (candidate % 2 == 0 ? 12 : -size.x - 12);
                    y = candidate < 6 ? anchor.y - size.y - 10 : anchor.y + 10;
                }
                var rect = new Rect(x, y, size.x, size.y);
                if (rect.xMin < viewport.xMin + 4 || rect.yMin < viewport.yMin + 4 ||
                    rect.xMax > viewport.xMax - 4 || rect.yMax > viewport.yMax - 4) continue;
                bool overlap = false;
                for (int i = 0; i < occupied.Count; i++)
                    if (rect.Overlaps(occupied[i])) { overlap = true; break; }
                if (overlap) continue;
                result = rect;
                occupied.Add(Rect.MinMaxRect(rect.xMin - 3, rect.yMin - 3, rect.xMax + 3, rect.yMax + 3));
                return true;
            }
            return false;
        }
    }

    public sealed partial class WordboundWorkshop
    {
        struct ViewLabel
        {
            public Vector2 Anchor;
            public string Caption, Detail;
            public int Priority;
        }
        readonly List<ViewLabel> viewLabels = new List<ViewLabel>();
        readonly List<Rect> labelSpace = new List<Rect>();
        GUIStyle viewLabel, importantViewLabel;
        Rect LocalViewport => new Rect(0, 0, viewport.width, viewport.height);

        void BeginOverlay()
        {
            viewLabels.Clear(); labelSpace.Clear();
            labelSpace.Add(ToolbarReserved);
        }

        void AddViewLabel(CoursePoint position, string caption, string detail, int priority)
        {
            if (!view.TryProject(position, viewport, out var anchor) || !LocalViewport.Contains(anchor)) return;
            viewLabels.Add(new ViewLabel { Anchor = anchor, Caption = caption.Length <= 25 ? caption : caption.Substring(0, 24) + "…",
                Detail = detail, Priority = priority });
        }

        void DrawViewLabels()
        {
            if (viewLabel == null)
            {
                viewLabel = new GUIStyle(normalBox) { wordWrap = false, clipping = TextClipping.Clip, fontSize = 12,
                    padding = new RectOffset(6, 6, 2, 2) };
                importantViewLabel = new GUIStyle(viewLabel); importantViewLabel.normal.textColor = ui.Ink;
                importantViewLabel.font = heading.font;
            }
            viewLabels.Sort((a, b) => { int priority = b.Priority.CompareTo(a.Priority); return priority != 0 ? priority : string.CompareOrdinal(a.Detail, b.Detail); });
            foreach (var label in viewLabels)
            {
                var style = label.Priority >= 2 ? importantViewLabel : viewLabel;
                var content = new GUIContent(label.Caption, label.Detail);
                var size = style.CalcSize(content); size.y = 22;
                if (WordboundWorkshopOverlay.PlaceLabel(label.Anchor, size, LocalViewport, labelSpace, out var rect))
                    GUI.Label(rect, content, style);
            }
        }

        void OverlayBox(Rect rect, string caption, GUIStyle style = null)
        {
            if (!LocalViewport.Overlaps(rect)) return;
            labelSpace.Add(Rect.MinMaxRect(rect.xMin - 3, rect.yMin - 3, rect.xMax + 3, rect.yMax + 3));
            GUI.Box(rect, caption, style ?? GUI.skin.box);
        }

        void DrawWorldLine(CoursePoint from, CoursePoint to, Color color)
        {
            if (Event.current.type != EventType.Repaint || !view.ProjectSegment(from, to, viewport, out var a, out var b)) return;
            var matrix = GUI.matrix; var previous = GUI.color;
            try
            {
                var delta = b - a;
                GUI.color = color;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
                GUI.DrawTexture(new Rect(a.x, a.y - WordboundWorkshopOverlay.LineWidth * .5f,
                    delta.magnitude, WordboundWorkshopOverlay.LineWidth), Texture2D.whiteTexture);
            }
            finally { GUI.matrix = matrix; GUI.color = previous; }
        }

        string RoomCaption(string id)
        {
            string prefix = History.Current.ActiveLayer + "_";
            return ObjectLabel(id.StartsWith(prefix, StringComparison.Ordinal) ? id.Substring(prefix.Length) : id);
        }

        bool RoomLabelVisible(CourseModule module)
        {
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
            foreach (var corner in Corners(Catalogue.Bounds(module)))
            {
                if (!view.TryProject(corner, viewport, out var at)) return false;
                min = Vector2.Min(min, at); max = Vector2.Max(max, at);
            }
            return max.x - min.x >= 96 && max.y - min.y >= 36;
        }
    }
}
