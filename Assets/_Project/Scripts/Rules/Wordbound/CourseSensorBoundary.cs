namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseSensorBoundary
    {
        public const int FloorContactToleranceMm = 1;

        public static bool ContainsFootPoint(CourseBox box, WorldVector point) =>
            ContainsFootPoint(box, point.X, point.Y, point.Z);

        public static bool ContainsFootPoint(CourseBox box, float x, float y, float z) =>
            WorldVector.Finite(x) && WorldVector.Finite(y) && WorldVector.Finite(z) &&
            x >= box.Min.X / 1000f && x <= box.Max.X / 1000f &&
            box.Min.Y - (double)y * 1000 <= FloorContactToleranceMm && y <= box.Max.Y / 1000f &&
            z >= box.Min.Z / 1000f && z <= box.Max.Z / 1000f;
    }
}
