using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    /// <summary>Rooms joined by doorways. Used by the clear-out to make sure nobody gets trapped.</summary>
    public sealed class RoomGraph
    {
        readonly List<string> rooms = new List<string>();
        readonly Dictionary<string, HashSet<string>> doors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public IReadOnlyList<string> Rooms => rooms;

        public void AddRoom(string room)
        {
            if (string.IsNullOrEmpty(room)) throw new ArgumentException("A room needs a name.");
            if (doors.ContainsKey(room)) throw new ArgumentException($"Room '{room}' is listed twice.");
            rooms.Add(room);
            doors[room] = new HashSet<string>(StringComparer.Ordinal);
        }

        public void AddDoor(string a, string b)
        {
            if (!doors.ContainsKey(a)) throw new ArgumentException($"Door from unknown room '{a}'.");
            if (!doors.ContainsKey(b)) throw new ArgumentException($"Door to unknown room '{b}'.");
            if (a == b) throw new ArgumentException($"Room '{a}' can't have a door to itself.");
            doors[a].Add(b);
            doors[b].Add(a);
        }

        public bool Has(string room) => doors.ContainsKey(room);

        public IEnumerable<string> Neighbours(string room) => doors[room];

        /// <summary>True if every room in <paramref name="open"/> can reach every other one through open rooms.</summary>
        public bool Connected(ICollection<string> open)
        {
            if (open.Count <= 1) return true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            string first = open.First();
            queue.Enqueue(first);
            seen.Add(first);
            while (queue.Count > 0)
            {
                foreach (string n in doors[queue.Dequeue()])
                    if (open.Contains(n) && seen.Add(n)) queue.Enqueue(n);
            }
            return seen.Count == open.Count;
        }

        /// <summary>
        /// Checks a closing order (brief §6: "preserve reachable escape routes"). At every step:
        /// the room closing must have a doorway into a room that is still open, the rooms left open
        /// must all stay connected, and protected rooms never close. Returns the problems found.
        /// </summary>
        public List<string> CheckClosureOrder(IList<string> order, ICollection<string> neverClose)
        {
            var problems = new List<string>();
            var open = new HashSet<string>(rooms, StringComparer.Ordinal);
            if (order.Distinct().Count() != order.Count) problems.Add("a room closes twice");
            foreach (string room in order)
            {
                if (!open.Contains(room))
                {
                    problems.Add($"{room} is not an open room");
                    continue;
                }
                if (neverClose.Contains(room)) problems.Add($"{room} is protected and must never close");
                open.Remove(room);
                if (open.Count == 0)
                {
                    problems.Add($"closing {room} leaves no open room");
                    break;
                }
                if (!doors[room].Any(open.Contains)) problems.Add($"{room} closes with no doorway into an open room");
                if (!Connected(open)) problems.Add($"closing {room} splits the open rooms: {string.Join(", ", open)}");
            }
            return problems;
        }
    }

    public enum RoomPhase { Safe, Warning, Filling, Closed }

    /// <summary>
    /// When each room is packed up by the Movers. Closure i starts filling at
    /// firstAt + i × interval, is announced warn seconds earlier, and is sealed fill seconds later.
    /// Times are seconds since the round started.
    /// </summary>
    public sealed class ClearOutSchedule
    {
        public readonly struct Closure
        {
            public readonly string Room;
            public readonly double WarnAt, FillAt, ClosedAt;

            public Closure(string room, double warnAt, double fillAt, double closedAt)
            {
                Room = room; WarnAt = warnAt; FillAt = fillAt; ClosedAt = closedAt;
            }
        }

        readonly Dictionary<string, Closure> byRoom = new Dictionary<string, Closure>(StringComparer.Ordinal);
        readonly GameRules rules;
        public IReadOnlyList<Closure> Closures { get; }

        public ClearOutSchedule(GameRules rules, IList<string> order)
        {
            this.rules = rules;
            var list = new List<Closure>();
            if (rules.ClearOutEnabled)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    double fill = rules.ClearOutFirstAt + i * rules.ClearOutInterval;
                    var c = new Closure(order[i], fill - rules.ClearOutWarnSeconds, fill, fill + rules.ClearOutFillSeconds);
                    list.Add(c);
                    byRoom[c.Room] = c;
                }
            }
            Closures = list;
        }

        public RoomPhase PhaseOf(string room, double t)
        {
            if (!byRoom.TryGetValue(room, out var c) || t < c.WarnAt) return RoomPhase.Safe;
            if (t < c.FillAt) return RoomPhase.Warning;
            if (t < c.ClosedAt) return RoomPhase.Filling;
            return RoomPhase.Closed;
        }

        /// <summary>Damage per second to anyone inside the room: none until it starts filling, then growing.</summary>
        public float DamagePerSecond(string room, double t)
        {
            if (!byRoom.TryGetValue(room, out var c) || t < c.FillAt) return 0f;
            return rules.ClearOutDamagePerSecond + rules.ClearOutDamageGrowth * (float)(t - c.FillAt);
        }

        public double SecondsUntilFill(string room, double t) => byRoom.TryGetValue(room, out var c) ? c.FillAt - t : double.PositiveInfinity;

        public IEnumerable<string> OpenRooms(IEnumerable<string> all, double t) => all.Where(r => PhaseOf(r, t) != RoomPhase.Closed);
    }

    /// <summary>One piece of original furniture in a room.</summary>
    public sealed class FurniturePlacement
    {
        public string Word;
        public string Room;
        public float X, Z, Yaw;
        /// <summary>Height above the room's floor, for pieces on a balcony or shelf.</summary>
        public float Y;
    }

    public sealed class RoomBox
    {
        public string Name;
        public float MinX, MinZ, MaxX, MaxZ;
        public float FloorY;

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        /// <summary>True if the point is within <paramref name="slack"/> metres of the box, edges included.</summary>
        public bool Touches(float x, float z, float slack) =>
            x >= MinX - slack && x <= MaxX + slack && z >= MinZ - slack && z <= MaxZ + slack;

        public bool Overlaps(RoomBox o) => MinX < o.MaxX && o.MinX < MaxX && MinZ < o.MaxZ && o.MinZ < MaxZ;
    }

    public sealed class Doorway
    {
        public string A, B;
        public float X, Z, Width;
    }

    public sealed class SpawnPoint
    {
        public string Room;
        public float X, Z, Yaw;
    }

    public sealed class HouseObjective
    {
        public string Word, Room;
    }

    /// <summary>A house map from Data/Config/house_*.json: rooms, doorways, spawns, furniture and clear-out orders.</summary>
    public sealed class HouseLayout
    {
        public string Name;
        public readonly List<RoomBox> Rooms = new List<RoomBox>();
        public readonly List<Doorway> Doors = new List<Doorway>();
        public readonly List<SpawnPoint> Spawns = new List<SpawnPoint>();
        public readonly List<FurniturePlacement> Furniture = new List<FurniturePlacement>();
        public readonly List<string> NeverClose = new List<string>();
        public readonly Dictionary<string, List<string>> ClearOutOrders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public string ExtractionRoom;
        public float ExtractionX, ExtractionZ;
        public readonly List<HouseObjective> MovingDay = new List<HouseObjective>();
        public readonly List<SpawnPoint> Keepsakes = new List<SpawnPoint>();

        /// <summary>Floor furniture must stand at least this far from a doorway's centre, so no room starts blocked.</summary>
        public const float DoorClearance = 1.5f;

        public RoomGraph Graph()
        {
            var g = new RoomGraph();
            foreach (var r in Rooms) g.AddRoom(r.Name);
            foreach (var d in Doors) g.AddDoor(d.A, d.B);
            return g;
        }

        public RoomBox Room(string name) => Rooms.FirstOrDefault(r => r.Name == name);

        public string RoomAt(float x, float z) => Rooms.FirstOrDefault(r => r.Contains(x, z))?.Name;

        /// <summary>The letters you get from breaking every piece of original furniture in a room.</summary>
        public LetterBag LettersIn(string room)
        {
            var bag = new LetterBag();
            foreach (var f in Furniture.Where(f => f.Room == room)) bag.Add(LetterBag.FromWord(f.Word));
            return bag;
        }

        public List<string> Validate(ItemCatalogue catalogue)
        {
            var problems = new List<string>();
            RoomGraph g;
            try { g = Graph(); }
            catch (ArgumentException e) { problems.Add(e.Message); return problems; }
            var all = new HashSet<string>(Rooms.Select(r => r.Name));
            if (!g.Connected(all)) problems.Add("not every room can be reached from every other room");
            foreach (var r in Rooms)
                if (r.MaxX <= r.MinX || r.MaxZ <= r.MinZ) problems.Add($"room {r.Name} has no area");
            for (int i = 0; i < Rooms.Count; i++)
                for (int j = i + 1; j < Rooms.Count; j++)
                    if (Rooms[i].Overlaps(Rooms[j])) problems.Add($"rooms {Rooms[i].Name} and {Rooms[j].Name} overlap");
            foreach (var d in Doors)
            {
                if (d.Width < 0.9f) problems.Add($"doorway {d.A}-{d.B} is {d.Width} m wide; a player needs 0.9 m");
                // Rooms don't overlap, so a point touching both boxes lies on the wall they share.
                if (!Room(d.A).Touches(d.X, d.Z, 0.01f) || !Room(d.B).Touches(d.X, d.Z, 0.01f))
                    problems.Add($"doorway {d.A}-{d.B} at ({d.X}, {d.Z}) isn't on a wall the two rooms share");
            }
            foreach (var s in Spawns)
            {
                if (!all.Contains(s.Room)) problems.Add($"a spawn is in unknown room {s.Room}");
                else if (!Room(s.Room).Contains(s.X, s.Z)) problems.Add($"a spawn at ({s.X}, {s.Z}) is outside {s.Room}");
            }
            foreach (var f in Furniture)
            {
                if (!all.Contains(f.Room)) problems.Add($"{f.Word} is in unknown room {f.Room}");
                else if (!Room(f.Room).Contains(f.X, f.Z)) problems.Add($"{f.Word} at ({f.X}, {f.Z}) is outside {f.Room}");
                foreach (var d in Doors)
                {
                    if (d.A != f.Room && d.B != f.Room) continue;
                    float dx = f.X - d.X, dz = f.Z - d.Z;
                    if (f.Y < 1f && dx * dx + dz * dz < DoorClearance * DoorClearance)
                        problems.Add($"{f.Word} at ({f.X}, {f.Z}) is within {DoorClearance} m of the {d.A}-{d.B} doorway");
                }
                if (!LetterBag.IsWord(f.Word)) problems.Add($"furniture word '{f.Word}' must be A-Z");
                else if (!catalogue.TryGet(f.Word, out _)) problems.Add($"furniture {f.Word} has no catalogue entry, so it has no model");
            }
            foreach (string n in NeverClose)
                if (!all.Contains(n)) problems.Add($"protected room {n} doesn't exist");
            foreach (var kv in ClearOutOrders)
                foreach (string p in g.CheckClosureOrder(kv.Value, NeverClose))
                    problems.Add($"clear-out order '{kv.Key}': {p}");
            if (ExtractionRoom != null && !all.Contains(ExtractionRoom)) problems.Add($"extraction room {ExtractionRoom} doesn't exist");
            else if (ExtractionRoom != null && !Room(ExtractionRoom).Contains(ExtractionX, ExtractionZ))
                problems.Add($"extraction point ({ExtractionX}, {ExtractionZ}) is outside {ExtractionRoom}");
            foreach (var objective in MovingDay)
            {
                if (!all.Contains(objective.Room)) problems.Add($"Moving Day objective {objective.Word} uses unknown room {objective.Room}");
                if (!catalogue.TryGet(objective.Word, out var item) || !item.Enabled)
                    problems.Add($"Moving Day objective {objective.Word} must be an enabled recipe");
            }
            foreach (var keepsake in Keepsakes)
                if (!all.Contains(keepsake.Room) || !Room(keepsake.Room).Contains(keepsake.X, keepsake.Z))
                    problems.Add($"keepsake ({keepsake.X}, {keepsake.Z}) is outside {keepsake.Room}");
            return problems;
        }

        public static HouseLayout FromJson(string json, string source = "house.json")
        {
            var root = Json.Parse(json, source);
            var h = new HouseLayout { Name = root["name"].String(), ExtractionRoom = root["extraction"].String(null) };
            foreach (var r in root["rooms"].Items)
            {
                var b = r["bounds"].Floats(4);
                h.Rooms.Add(new RoomBox { Name = r["name"].String(), MinX = b[0], MinZ = b[1], MaxX = b[2], MaxZ = b[3], FloorY = r["floorY"].Float(0f) });
            }
            foreach (var d in root["doors"].Items)
            {
                var between = d["between"].Strings();
                if (between.Count != 2) throw new FormatException($"{source}: {d.Path}.between must name 2 rooms");
                var at = d["at"].Floats(2);
                h.Doors.Add(new Doorway { A = between[0], B = between[1], X = at[0], Z = at[1], Width = d["width"].Float(1.4f) });
            }
            foreach (var s in root["spawns"].Items)
            {
                var at = s["at"].Floats(2);
                h.Spawns.Add(new SpawnPoint { Room = s["room"].String(), X = at[0], Z = at[1], Yaw = s["yaw"].Float(0f) });
            }
            foreach (var f in root["furniture"].Items)
            {
                var at = f["at"].Floats(2);
                h.Furniture.Add(new FurniturePlacement
                {
                    Word = f["word"].String(), Room = f["room"].String(), X = at[0], Z = at[1], Yaw = f["yaw"].Float(0f), Y = f["y"].Float(0f),
                });
            }
            h.NeverClose.AddRange(root["neverClose"].Strings());
            if (root.Has("extractionAt"))
            {
                var at = root["extractionAt"].Floats(2);
                h.ExtractionX = at[0]; h.ExtractionZ = at[1];
            }
            else if (h.ExtractionRoom != null && h.Room(h.ExtractionRoom) != null)
            {
                var room = h.Room(h.ExtractionRoom);
                h.ExtractionX = (room.MinX + room.MaxX) * 0.5f;
                h.ExtractionZ = (room.MinZ + room.MaxZ) * 0.5f;
            }
            if (root.Has("movingDay"))
                foreach (var objective in root["movingDay"].Items)
                    h.MovingDay.Add(new HouseObjective { Word = objective["word"].String(), Room = objective["room"].String() });
            if (root.Has("keepsakes"))
                foreach (var keepsake in root["keepsakes"].Items)
                {
                    var at = keepsake["at"].Floats(2);
                    h.Keepsakes.Add(new SpawnPoint { Room = keepsake["room"].String(), X = at[0], Z = at[1] });
                }
            if (root.Has("clearOutOrders"))
                foreach (string mode in root["clearOutOrders"].Keys)
                    h.ClearOutOrders[mode] = root["clearOutOrders"][mode].Strings();
            return h;
        }
    }
}
