using System;

namespace Wreckabulary.Rules
{
    public readonly struct SheetBox
    {
        public readonly float Left, Bottom, Right, Top;

        public SheetBox(float left, float bottom, float right, float top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public float Width => Right - Left;
        public float Height => Top - Bottom;

        public bool Overlaps(SheetBox other) => Left < other.Right && other.Left < Right && Bottom < other.Top && other.Bottom < Top;

        public bool Inside(SheetBox outer, float slack = 0f) =>
            Left >= outer.Left - slack && Right <= outer.Right + slack && Bottom >= outer.Bottom - slack && Top <= outer.Top + slack;

        public override string ToString() => $"({Left:0.#}, {Bottom:0.#}) to ({Right:0.#}, {Top:0.#})";
    }

    public enum SheetSide { Right, Left }

    public readonly struct SheetSpot
    {
        public readonly SheetBox Box;
        public readonly SheetSide Side;
        public readonly bool Beside;

        public SheetSpot(SheetBox box, SheetSide side, bool beside)
        {
            Box = box;
            Side = side;
            Beside = beside;
        }
    }

    public static class SheetPlacement
    {
        public const float Gap = 12f, Margin = 12f;

        public static float RoomRight(SheetBox card, SheetBox safe) => safe.Right - Margin - (card.Right + Gap);

        public static float RoomLeft(SheetBox card, SheetBox safe) => card.Left - Gap - (safe.Left + Margin);

        public static SheetSpot Place(SheetBox card, float width, float height, SheetBox safe)
        {
            float right = RoomRight(card, safe), left = RoomLeft(card, safe);
            SheetSide side;
            float x;
            bool beside = true;
            if (right >= width) { side = SheetSide.Right; x = card.Right + Gap; }
            else if (left >= width) { side = SheetSide.Left; x = card.Left - Gap - width; }
            else
            {
                beside = false;
                side = right >= left ? SheetSide.Right : SheetSide.Left;
                x = Clamp(side == SheetSide.Right ? card.Right + Gap : card.Left - Gap - width, safe.Left + Margin, safe.Right - Margin - width);
            }
            float top = Clamp(card.Top, safe.Bottom + Margin + height, safe.Top - Margin);
            return new SheetSpot(new SheetBox(x, top - height, x + width, top), side, beside);
        }

        public static bool AlwaysBeside(float safeWidth, float cardWidth, float width) => (safeWidth - cardWidth - 2f * (Gap + Margin)) * .5f >= width;

        static float Clamp(float value, float min, float max) => max < min ? max : Math.Max(min, Math.Min(max, value));
    }
}
