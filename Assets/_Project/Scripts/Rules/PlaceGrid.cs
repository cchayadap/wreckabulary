using System;

namespace Wreckabulary.Rules
{
    public readonly struct PlaceGridPlan
    {
        public readonly int Columns, Rows;
        public readonly float Width, Height;

        public PlaceGridPlan(int columns, int rows, float width, float height)
        {
            Columns = columns; Rows = rows; Width = width; Height = height;
        }

        public bool Fits => Columns > 0;
    }

    public static class PlaceGrid
    {
        public const float PictureAspect = 16f / 9f;
        public const int WidestRow = 3;
        const float Tolerance = .01f;

        public static float CardHeight(float width, float inset, float caption) =>
            Math.Max(0f, width - inset * 2) / PictureAspect + caption + inset;

        public static float CappedWidth(float width, float spacing) => Math.Max(0f, (width - (WidestRow - 1) * spacing) / WidestRow);

        public static PlaceGridPlan Fit(int count, float width, float height, float spacing, float inset, float caption, float minimum)
        {
            var best = default(PlaceGridPlan);
            if (count <= 0 || width <= 0 || height <= 0) return best;
            float widest = CappedWidth(width, spacing);
            for (int columns = 1; columns <= count; columns++)
            {
                int rows = (count + columns - 1) / columns;
                float card = Math.Min(widest, (width - (columns - 1) * spacing) / columns);
                if (card <= inset * 2 || card < minimum - Tolerance) continue;
                float tall = CardHeight(card, inset, caption);
                if (rows * tall + (rows - 1) * spacing > height + Tolerance) continue;
                if (!best.Fits || card > best.Width + Tolerance || Math.Abs(card - best.Width) <= Tolerance && columns > best.Columns)
                    best = new PlaceGridPlan(columns, rows, card, tall);
            }
            return best;
        }
    }
}
