using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct RouteStep
    {
        public readonly string Room;
        public readonly Doorway Door;
        public readonly Stairway Stairs;
        public readonly float Metres;

        public RouteStep(string room, Doorway door, Stairway stairs, float metres)
        {
            Room = room; Door = door; Stairs = stairs; Metres = metres;
        }
    }

    public sealed class HouseRoutes
    {
        sealed class Link
        {
            public string From, To;
            public float InX, InZ, OutX, OutZ, Length;
            public Doorway Door;
            public Stairway Stairs;
        }

        readonly Link[] links;
        readonly float[] cost;
        readonly int[] first;
        readonly bool[] done;

        public HouseRoutes(HouseLayout layout)
        {
            var all = new List<Link>();
            foreach (var d in layout.Doors)
            {
                all.Add(new Link { From = d.A, To = d.B, InX = d.X, InZ = d.Z, OutX = d.X, OutZ = d.Z, Door = d });
                all.Add(new Link { From = d.B, To = d.A, InX = d.X, InZ = d.Z, OutX = d.X, OutZ = d.Z, Door = d });
            }
            foreach (var s in layout.Stairs)
            {
                float rise = Math.Abs(layout.Room(s.Upper).FloorY - layout.Room(s.Lower).FloorY);
                float length = (float)Math.Sqrt(s.Run * s.Run + rise * rise);
                all.Add(new Link { From = s.Lower, To = s.Upper, InX = s.FromX, InZ = s.FromZ, OutX = s.ToX, OutZ = s.ToZ, Length = length, Stairs = s });
                all.Add(new Link { From = s.Upper, To = s.Lower, InX = s.ToX, InZ = s.ToZ, OutX = s.FromX, OutZ = s.FromZ, Length = length, Stairs = s });
            }
            links = all.ToArray();
            cost = new float[links.Length];
            first = new int[links.Length];
            done = new bool[links.Length];
        }

        public bool TryFirstStep(string start, float x, float z, string end, float goalX, float goalZ, Func<string, bool> closed, out RouteStep step)
        {
            int n = links.Length;
            for (int i = 0; i < n; i++)
            {
                var l = links[i];
                bool open = l.From == start && (closed == null || !closed(l.To));
                cost[i] = open ? Distance(x, z, l.InX, l.InZ) + l.Length : float.PositiveInfinity;
                first[i] = open ? i : -1;
                done[i] = false;
            }
            float best = float.PositiveInfinity;
            int bestFirst = -1;
            while (true)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && first[i] >= 0 && (u < 0 || cost[i] < cost[u])) u = i;
                if (u < 0 || cost[u] >= best) break;
                done[u] = true;
                var here = links[u];
                if (here.To == end)
                {
                    float total = cost[u] + Distance(here.OutX, here.OutZ, goalX, goalZ);
                    if (total < best) { best = total; bestFirst = first[u]; }
                    continue;
                }
                for (int v = 0; v < n; v++)
                {
                    var next = links[v];
                    if (done[v] || next.From != here.To || next.To == start || (closed != null && closed(next.To))) continue;
                    float c = cost[u] + Distance(here.OutX, here.OutZ, next.InX, next.InZ) + next.Length;
                    if (c < cost[v]) { cost[v] = c; first[v] = first[u]; }
                }
            }
            if (bestFirst < 0) { step = default; return false; }
            var chosen = links[bestFirst];
            step = new RouteStep(chosen.To, chosen.Door, chosen.Stairs, best);
            return true;
        }

        static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
