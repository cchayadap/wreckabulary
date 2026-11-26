using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class HelperRoute
    {
        public const float DoorReached = .5f;
        public const float StepThrough = 1f;

        readonly HouseLayout layout;
        readonly HouseRoutes routes;
        readonly Dictionary<string, RoomBox> rooms = new Dictionary<string, RoomBox>(StringComparer.Ordinal);
        readonly Func<string, bool> offStorey;
        float storeyFloor;

        public HelperRoute(HouseLayout layout)
        {
            this.layout = layout;
            if (layout == null) return;
            routes = new HouseRoutes(layout);
            foreach (var r in layout.Rooms) rooms[r.Name] = r;
            offStorey = OffStorey;
        }

        bool OffStorey(string room) => !rooms.TryGetValue(room, out var box) || Math.Abs(box.FloorY - storeyFloor) >= RoomBox.StoreyTolerance;

        public bool Next(float x, float y, float z, float goalX, float goalY, float goalZ, out float nextX, out float nextZ)
        {
            nextX = goalX;
            nextZ = goalZ;
            if (Math.Abs(goalY - y) > HelperTargeting.SameLevel) return false;
            if (layout == null) return true;
            var here = HelperPlacement.RoomAt(layout, x, y, z);
            var there = HelperPlacement.RoomAt(layout, goalX, goalY, goalZ);
            if (here == null || there == null) return here == there;
            if (here == there) return true;
            if (!here.SameStorey(there)) return false;
            storeyFloor = here.FloorY;
            if (!routes.TryFirstStep(here.Name, x, z, there.Name, goalX, goalZ, offStorey, out var step) || step.Door == null) return false;
            nextX = step.Door.X;
            nextZ = step.Door.Z;
            float dx = nextX - x, dz = nextZ - z;
            if (dx * dx + dz * dz > DoorReached * DoorReached) return true;
            if (!rooms.TryGetValue(step.Room, out var beyond)) return true;
            float cx = (beyond.MinX + beyond.MaxX) * .5f - nextX, cz = (beyond.MinZ + beyond.MaxZ) * .5f - nextZ;
            float length = (float)Math.Sqrt(cx * cx + cz * cz);
            if (length < 1e-4f) return true;
            nextX += cx / length * StepThrough;
            nextZ += cz / length * StepThrough;
            return true;
        }
    }
}
