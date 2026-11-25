using System;

namespace Wreckabulary.Rules
{
    public static class TrashGlyph
    {
        public const double LidLeft = -.66, LidRight = .66, LidBottom = .5, LidTop = .66;
        public const double HandleHalf = .22, HandleTop = .84, HandleHoleHalf = .1, HandleHoleTop = .74;
        public const double BodyTop = .4, BodyBottom = -.84, BodyTopHalf = .52, BodyBottomHalf = .4;
        public const double GrooveHalf = .055, GrooveStep = .24, GrooveTop = .2, GrooveBottom = -.64;
        public const int Grooves = 3;

        public static bool Inside(double x, double y) => InLid(x, y) || InHandle(x, y) || (InBody(x, y) && !InGroove(x, y));

        public static float Coverage(int px, int py, int size, int samples)
        {
            if (size <= 0 || samples <= 0) return 0f;
            int inside = 0;
            for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    double x = (px + (sx + .5) / samples) / size * 2.0 - 1.0;
                    double y = (py + (sy + .5) / samples) / size * 2.0 - 1.0;
                    if (Inside(x, y)) inside++;
                }
            return inside / (float)(samples * samples);
        }

        static bool InLid(double x, double y) => x >= LidLeft && x <= LidRight && y >= LidBottom && y <= LidTop;

        static bool InHandle(double x, double y) =>
            Math.Abs(x) <= HandleHalf && y > LidTop && y <= HandleTop && !(Math.Abs(x) < HandleHoleHalf && y < HandleHoleTop);

        static bool InBody(double x, double y)
        {
            if (y > BodyTop || y < BodyBottom) return false;
            double t = (y - BodyBottom) / (BodyTop - BodyBottom);
            return Math.Abs(x) <= BodyBottomHalf + (BodyTopHalf - BodyBottomHalf) * t;
        }

        static bool InGroove(double x, double y)
        {
            if (y > GrooveTop || y < GrooveBottom) return false;
            for (int i = 0; i < Grooves; i++)
                if (Math.Abs(x - (i - (Grooves - 1) * .5) * GrooveStep) <= GrooveHalf) return true;
            return false;
        }
    }
}
