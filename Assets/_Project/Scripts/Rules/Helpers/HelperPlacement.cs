using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public enum HelperPlaceKind { Mover, Stayer, Wall }

    public enum HelperRefusal { None, Spawn, Downed, Doorway, Exit, Key, Van, OutsideRooms, Stairs, OffTheFloor, Seals }

    public readonly struct HelperGuard
    {
        public readonly float X, Z, Radius;
        public readonly HelperRefusal Reason;

        public HelperGuard(float x, float z, float radius, HelperRefusal reason)
        {
            X = x; Z = z; Radius = radius; Reason = reason;
        }
    }

    public readonly struct HelperFootprint
    {
        public readonly float MinX, MinZ, MaxX, MaxZ;

        public HelperFootprint(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = Math.Min(minX, maxX); MaxX = Math.Max(minX, maxX); MinZ = Math.Min(minZ, maxZ); MaxZ = Math.Max(minZ, maxZ);
        }

        public static HelperFootprint Around(float x, float z, float sizeX, float sizeZ, float yawDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            float c = (float)Math.Abs(Math.Cos(yaw)), s = (float)Math.Abs(Math.Sin(yaw));
            float halfX = (sizeX * c + sizeZ * s) * .5f, halfZ = (sizeX * s + sizeZ * c) * .5f;
            return new HelperFootprint(x - halfX, z - halfZ, x + halfX, z + halfZ);
        }

        public float DistanceTo(float x, float z)
        {
            float dx = Math.Max(0f, Math.Max(MinX - x, x - MaxX)), dz = Math.Max(0f, Math.Max(MinZ - z, z - MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    public static class HelperPlacement
    {
        public const float SpawnClear = 1.5f;
        public const float DownedClear = 1.5f;
        public const float DoorClear = 1.2f;
        public const float ObjectiveClear = 2.5f;
        public const float FreeWidth = .9f;
        public const float FloorTolerance = .2f;
        public static readonly bool EveryCover = true;

        public static bool Guards(ItemDefinition cover) =>
            cover?.Deploy != null && cover.Deploy.Effect == DeployEffect.Cover && (EveryCover || HelperBook.IsWall(cover));

        public static string Message(HelperRefusal refusal) => refusal == HelperRefusal.None ? "" : "NOT HERE";

        public static HelperRefusal Check(HouseLayout layout, HelperPlaceKind kind, float x, float y, float z, in HelperFootprint footprint,
            IReadOnlyList<HelperGuard> guards, IReadOnlyList<HelperFootprint> blockers)
        {
            if (guards != null)
                for (int i = 0; i < guards.Count; i++)
                    if (footprint.DistanceTo(guards[i].X, guards[i].Z) < guards[i].Radius) return guards[i].Reason;
            if (layout == null) return HelperRefusal.None;
            for (int i = 0; i < layout.Stairs.Count; i++)
                if (Overlaps(footprint, layout.Stairs[i])) return HelperRefusal.Stairs;
            var room = RoomAt(layout, x, y, z);
            if (room == null) return HelperRefusal.OutsideRooms;
            for (int i = 0; i < layout.Spawns.Count; i++)
                if (OnStorey(layout, layout.Spawns[i].Room, room) && footprint.DistanceTo(layout.Spawns[i].X, layout.Spawns[i].Z) < SpawnClear) return HelperRefusal.Spawn;
            if (kind == HelperPlaceKind.Mover) return HelperRefusal.None;
            for (int i = 0; i < layout.Doors.Count; i++)
            {
                var d = layout.Doors[i];
                if ((OnStorey(layout, d.A, room) || OnStorey(layout, d.B, room)) && footprint.DistanceTo(d.X, d.Z) < DoorClear) return HelperRefusal.Doorway;
            }
            if (Math.Abs(y - room.FloorY) > FloorTolerance) return HelperRefusal.OffTheFloor;
            if (kind == HelperPlaceKind.Wall && Seals(room, footprint, blockers)) return HelperRefusal.Seals;
            return HelperRefusal.None;
        }

        public static RoomBox RoomAt(HouseLayout layout, float x, float y, float z)
        {
            RoomBox best = null;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                var r = layout.Rooms[i];
                if (!r.Contains(x, z) || r.FloorY > y + HouseLayout.StandingSlack) continue;
                if (best == null || r.FloorY > best.FloorY + RoomBox.StoreyTolerance) best = r;
            }
            return best;
        }

        public static bool Seals(RoomBox room, in HelperFootprint wall, IReadOnlyList<HelperFootprint> blockers)
        {
            bool northSouth = wall.MinZ - room.MinZ >= FreeWidth && room.MaxZ - wall.MaxZ >= FreeWidth;
            if (northSouth && WidestGap(room.MinX, room.MaxX, wall.MinX, wall.MaxX, wall.MinZ, wall.MaxZ, blockers, true) < FreeWidth) return true;
            bool eastWest = wall.MinX - room.MinX >= FreeWidth && room.MaxX - wall.MaxX >= FreeWidth;
            return eastWest && WidestGap(room.MinZ, room.MaxZ, wall.MinZ, wall.MaxZ, wall.MinX, wall.MaxX, blockers, false) < FreeWidth;
        }

        static float WidestGap(float low, float high, float wallLow, float wallHigh, float bandLow, float bandHigh,
            IReadOnlyList<HelperFootprint> blockers, bool alongX)
        {
            int count = 1 + (blockers?.Count ?? 0);
            var starts = new float[count];
            var ends = new float[count];
            starts[0] = wallLow; ends[0] = wallHigh;
            int n = 1;
            for (int i = 0; blockers != null && i < blockers.Count; i++)
            {
                var b = blockers[i];
                float bandMin = alongX ? b.MinZ : b.MinX, bandMax = alongX ? b.MaxZ : b.MaxX;
                if (bandMax <= bandLow - FreeWidth || bandMin >= bandHigh + FreeWidth) continue;
                starts[n] = alongX ? b.MinX : b.MinZ;
                ends[n] = alongX ? b.MaxX : b.MaxZ;
                n++;
            }
            Array.Sort(starts, ends, 0, n);
            float widest = 0f, reached = low;
            for (int i = 0; i < n; i++)
            {
                widest = Math.Max(widest, starts[i] - reached);
                reached = Math.Max(reached, ends[i]);
            }
            return Math.Max(widest, high - reached);
        }

        static bool OnStorey(HouseLayout layout, string name, RoomBox here)
        {
            for (int i = 0; i < layout.Rooms.Count; i++)
                if (layout.Rooms[i].Name == name) return layout.Rooms[i].SameStorey(here);
            return true;
        }

        static bool Overlaps(in HelperFootprint f, Stairway s) => f.MinX < s.MaxX && s.MinX < f.MaxX && f.MinZ < s.MaxZ && s.MinZ < f.MaxZ;
    }
}
