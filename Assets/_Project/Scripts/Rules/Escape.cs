using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class Escape
    {
        public static readonly string[] ThreeLetter =
        { "BAT", "BED", "MAT", "BOW", "BOX", "FAN", "MUG", "PAN", "PIE", "POT" };
        public static readonly string[] FourLetter =
        { "BALL", "BOMB", "FOAM", "LAMP", "SOAP", "SOFA", "BOOK", "CAKE", "DESK", "SODA", "VASE" };
        public static readonly string[] FiveLetter =
        { "BLADE", "PLATE", "TABLE", "APPLE", "ARROW", "BROOM", "CHAIR", "CHEST", "CLOCK", "CRATE",
          "PLANT", "SHELF", "SPEAR", "STOOL", "WATER" };

        public const int DoorCount = 3;
        public static readonly int[] DoorLengths = { 3, 4, 5 };

        public static string[] Pool(int length) => length <= 3 ? ThreeLetter : length == 4 ? FourLetter : FiveLetter;

        public static string PickWord(int length, Random rng, ICollection<string> used)
        {
            var pool = Pool(length);
            var spare = new List<string>();
            foreach (var word in pool)
                if (used == null || !used.Contains(word)) spare.Add(word);
            if (spare.Count == 0) spare.AddRange(pool);
            return spare[rng.Next(spare.Count)];
        }

        public static string DoorKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        public static HashSet<string> Reachable(HouseLayout layout, string start, ICollection<string> blocked)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (layout == null || string.IsNullOrEmpty(start) || layout.Room(start) == null) return seen;
            var queue = new Queue<string>();
            seen.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string room = queue.Dequeue();
                foreach (var door in layout.Doors)
                {
                    string other = door.A == room ? door.B : door.B == room ? door.A : null;
                    if (other == null || layout.Room(other) == null) continue;
                    if (blocked != null && blocked.Contains(DoorKey(door.A, door.B))) continue;
                    if (seen.Add(other)) queue.Enqueue(other);
                }
                foreach (var stair in layout.Stairs)
                {
                    string other = stair.Lower == room ? stair.Upper : stair.Upper == room ? stair.Lower : null;
                    if (other == null || layout.Room(other) == null) continue;
                    if (seen.Add(other)) queue.Enqueue(other);
                }
            }
            return seen;
        }

        public static List<Doorway> Frontier(HouseLayout layout, HashSet<string> reachable)
        {
            var list = new List<Doorway>();
            foreach (var door in layout.Doors)
            {
                bool a = reachable.Contains(door.A), b = reachable.Contains(door.B);
                if (a != b) list.Add(door);
            }
            return list;
        }

        public static List<Doorway> PathDoors(HouseLayout layout, string from, string to)
        {
            var path = new List<Doorway>();
            if (layout == null || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return path;
            if (layout.Room(from) == null || layout.Room(to) == null) return path;

            var cameFrom = new Dictionary<string, string>(StringComparer.Ordinal);
            var cameDoor = new Dictionary<string, Doorway>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            cameFrom[from] = null;
            queue.Enqueue(from);

            while (queue.Count > 0 && !cameFrom.ContainsKey(to))
            {
                string room = queue.Dequeue();
                foreach (var door in layout.Doors)
                {
                    string other = door.A == room ? door.B : door.B == room ? door.A : null;
                    if (other == null || layout.Room(other) == null || cameFrom.ContainsKey(other)) continue;
                    cameFrom[other] = room;
                    cameDoor[other] = door;
                    if (other == to) break;
                    queue.Enqueue(other);
                }
                foreach (var stair in layout.Stairs)
                {
                    string other = stair.Lower == room ? stair.Upper : stair.Upper == room ? stair.Lower : null;
                    if (other == null || layout.Room(other) == null || cameFrom.ContainsKey(other)) continue;
                    cameFrom[other] = room;
                    if (other == to) break;
                    queue.Enqueue(other);
                }
            }

            if (!cameFrom.ContainsKey(to)) return path;
            var walk = to;
            while (cameFrom.TryGetValue(walk, out var previous) && previous != null)
            {
                if (cameDoor.TryGetValue(walk, out var door)) path.Insert(0, door);
                walk = previous;
            }
            return path;
        }

        public static bool IsBehind(HouseLayout layout, string room, string start, ICollection<string> blocked) =>
            layout != null && layout.Room(room) != null && !Reachable(layout, start, blocked).Contains(room);
    }
}
