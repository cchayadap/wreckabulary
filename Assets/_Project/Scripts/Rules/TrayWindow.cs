using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct TrayThumb
    {
        public readonly float Offset, Length;

        public TrayThumb(float offset, float length) { Offset = offset; Length = length; }
    }

    public static class TrayWindow
    {
        public const int MostShownRows = BagRows.MostTrayRows;
        public const float ColumnWide = 32f, ColumnGap = 4f, ArrowTall = 40f, ArrowGap = 4f, TrackWide = 6f;
        public const float ArrowDimmed = .3f;

        public static float ColumnTall => BagRows.GridTall(MostShownRows, BagRows.TrayStepY, BagRows.TrayTileTall);

        public static float TrackTall => ColumnTall - 2f * (ArrowTall + ArrowGap);

        public static int ShownRows(int rows) => BagRows.TrayRowsFor(rows);

        public static bool Scrolls(int rows) => Rows(rows) > MostShownRows;

        public static int MostTop(int rows) => Math.Max(0, Rows(rows) - MostShownRows);

        public static int Clamp(int top, int rows) => Math.Max(0, Math.Min(MostTop(rows), top));

        public static int Scroll(int top, int step, int rows) => Clamp(Clamp(top, rows) + step, rows);

        public static bool CanUp(int top, int rows) => Clamp(top, rows) > 0;

        public static bool CanDown(int top, int rows) => Clamp(top, rows) < MostTop(rows);

        public static int WheelStep(float wheel) => wheel > 0f ? -1 : wheel < 0f ? 1 : 0;

        public static bool Shows(int top, int rows, int cell)
        {
            if (cell < 0) return false;
            int row = BagRows.RowOf(cell), first = Clamp(top, rows);
            return row >= first && row < first + ShownRows(rows);
        }

        public static float CellTop(int cell, int top) => -(BagRows.RowOf(cell) - Math.Max(0, top)) * BagRows.TrayStepY;

        public static int Reveal(int top, int rows, int cell)
        {
            top = Clamp(top, rows);
            if (cell < 0) return top;
            int row = BagRows.RowOf(cell), shown = ShownRows(rows);
            if (row < top) return Clamp(row, rows);
            if (row >= top + shown) return Clamp(row - shown + 1, rows);
            return top;
        }

        public static bool ShowsAnyFilled(int top, int rows, int filled)
        {
            int first = Clamp(top, rows) * BagRows.SlotsPerRow;
            return filled > first;
        }

        public static int Follow(int top, int rows, int heldBefore, int held, int filled, int landed)
        {
            top = Clamp(top, rows);
            if (held > heldBefore) return Reveal(top, rows, landed >= 0 ? landed : held - 1);
            if (held < heldBefore && !ShowsAnyFilled(top, rows, filled))
                return filled <= 0 ? 0 : Clamp(BagRows.RowOf(filled - 1) - ShownRows(rows) + 1, rows);
            return top;
        }

        public static int Landed(IReadOnlyList<char> before, int beforeCount, IReadOnlyList<char> after)
        {
            if (after == null) return -1;
            int known = before == null ? 0 : Math.Min(beforeCount, before.Count);
            int same = Math.Min(known, after.Count);
            for (int i = 0; i < same; i++)
                if (before[i] != after[i]) return i;
            return after.Count > beforeCount ? Math.Min(beforeCount, after.Count - 1) : -1;
        }

        public static TrayThumb Thumb(int top, int rows)
        {
            int all = Rows(rows);
            float track = TrackTall;
            if (all <= MostShownRows) return new TrayThumb(0f, track);
            return new TrayThumb(track * Clamp(top, rows) / all, track * MostShownRows / all);
        }

        public static HudBox Column(int rows, float width, bool aside, bool touch)
        {
            var frame = BagRows.Frame(rows, width, aside, touch);
            float scale = frame.Tray.Width / BagRows.TrayWidth;
            float tall = ColumnTall * scale;
            return new HudBox(frame.Tray.X - (ColumnGap + ColumnWide) * scale, frame.Tray.Top - tall, ColumnWide * scale, tall);
        }

        static int Rows(int rows) => Math.Max(BagRows.BaseRows, Math.Min(BagRows.MostRows, rows));
    }
}
