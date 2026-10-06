using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
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

        public float DamagePerSecond(string room, double t)
        {
            if (!byRoom.TryGetValue(room, out var c) || t < c.FillAt) return 0f;
            return rules.ClearOutDamagePerSecond + rules.ClearOutDamageGrowth * (float)(t - c.FillAt);
        }

        public double SecondsUntilFill(string room, double t) => byRoom.TryGetValue(room, out var c) ? c.FillAt - t : double.PositiveInfinity;

        public IEnumerable<string> OpenRooms(IEnumerable<string> all, double t) => all.Where(r => PhaseOf(r, t) != RoomPhase.Closed);
    }

    public sealed class FurniturePlacement
    {
        public string Word;
        public string Room;
        public float X, Z, Yaw;
        public float Y;
    }

    public sealed class RoomBox
    {
        public string Name;
        public float MinX, MinZ, MaxX, MaxZ;
        public float FloorY;

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public bool Touches(float x, float z, float slack) =>
            x >= MinX - slack && x <= MaxX + slack && z >= MinZ - slack && z <= MaxZ + slack;

        public bool Overlaps(RoomBox o) => MinX < o.MaxX && o.MinX < MaxX && MinZ < o.MaxZ && o.MinZ < MaxZ;

        public const float StoreyTolerance = 0.5f;

        public bool SameStorey(RoomBox o) => Math.Abs(FloorY - o.FloorY) < StoreyTolerance;
    }

    public sealed class Doorway
    {
        public string A, B;
        public float X, Z, Width;
    }

    public sealed class Stairway
    {
        public string Lower, Upper;
        public float FromX, FromZ, ToX, ToZ, Width;

        public const float MaxSlopeDegrees = 35f;
        public const float StepOff = 0.7f;

        public float Run => (float)Math.Sqrt((ToX - FromX) * (ToX - FromX) + (ToZ - FromZ) * (ToZ - FromZ));
        bool AlongZ => Math.Abs(ToX - FromX) < 0.001f;
        public bool AlongAnAxis => Run > 0f && (AlongZ || Math.Abs(ToZ - FromZ) < 0.001f);

        public float MinX => AlongZ ? FromX - Width * 0.5f : Math.Min(FromX, ToX);
        public float MaxX => AlongZ ? FromX + Width * 0.5f : Math.Max(FromX, ToX);
        public float MinZ => AlongZ ? Math.Min(FromZ, ToZ) : FromZ - Width * 0.5f;
        public float MaxZ => AlongZ ? Math.Max(FromZ, ToZ) : FromZ + Width * 0.5f;

        public bool Covers(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        public bool Within(RoomBox r) => MinX >= r.MinX && MaxX <= r.MaxX && MinZ >= r.MinZ && MaxZ <= r.MaxZ;

        public void Along(float metres, out float x, out float z)
        {
            float t = metres / Run;
            x = FromX + (ToX - FromX) * t;
            z = FromZ + (ToZ - FromZ) * t;
        }
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

    public sealed class HouseLayout
    {
        public string Name;
        public readonly List<RoomBox> Rooms = new List<RoomBox>();
        public readonly List<Doorway> Doors = new List<Doorway>();
        public readonly List<Stairway> Stairs = new List<Stairway>();
        public readonly List<SpawnPoint> Spawns = new List<SpawnPoint>();
        public readonly List<FurniturePlacement> Furniture = new List<FurniturePlacement>();
        public readonly List<string> NeverClose = new List<string>();
        public readonly Dictionary<string, List<string>> ClearOutOrders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        public string ExtractionRoom;
        public float ExtractionX, ExtractionZ;
        public readonly List<HouseObjective> MovingDay = new List<HouseObjective>();
        public readonly List<SpawnPoint> Keepsakes = new List<SpawnPoint>();
        public string LobbyRoom;

        public const float DoorClearance = 1.5f;
        public const float StandingSlack = 0.3f;

        public RoomGraph Graph()
        {
            var g = new RoomGraph();
            foreach (var r in Rooms) g.AddRoom(r.Name);
            foreach (var d in Doors) g.AddDoor(d.A, d.B);
            foreach (var s in Stairs) g.AddDoor(s.Lower, s.Upper);
            return g;
        }

        public RoomBox Room(string name) => Rooms.FirstOrDefault(r => r.Name == name);

        public string RoomAt(float x, float z)
        {
            RoomBox best = null;
            foreach (var r in Rooms)
                if (r.Contains(x, z) && (best == null || r.FloorY < best.FloorY - RoomBox.StoreyTolerance)) best = r;
            return best?.Name;
        }

        public string RoomAt(float x, float y, float z)
        {
            RoomBox best = null;
            foreach (var r in Rooms)
                if (r.Contains(x, z) && r.FloorY <= y + StandingSlack && (best == null || r.FloorY > best.FloorY + RoomBox.StoreyTolerance)) best = r;
            return best?.Name ?? RoomAt(x, z);
        }

        public List<float> StoreyFloors()
        {
            var floors = new List<float>();
            foreach (float y in Rooms.Select(r => r.FloorY).OrderBy(y => y))
                if (floors.Count == 0 || y >= floors[floors.Count - 1] + RoomBox.StoreyTolerance) floors.Add(y);
            return floors;
        }

        public string StoreyLabel(int storey)
        {
            if (storey <= 0) return "GROUND FLOOR";
            if (StoreyFloors().Count == 2) return "UPSTAIRS";
            int lastTwo = storey % 100;
            string suffix = lastTwo >= 11 && lastTwo <= 13 ? "TH" : storey % 10 == 1 ? "ST" : storey % 10 == 2 ? "ND" : storey % 10 == 3 ? "RD" : "TH";
            return storey + suffix + " FLOOR";
        }

        public string WithStorey(string room)
        {
            var box = Room(room);
            if (box == null || StoreyFloors().Count < 2) return room;
            return room + " (" + StoreyLabel(StoreyOf(box)).ToLowerInvariant() + ")";
        }

        public int StoreyOf(RoomBox room) => LastStoreyAtOrBelow(room.FloorY + 0.0001f);

        public int StoreyAt(float y) => LastStoreyAtOrBelow(y + StandingSlack);

        int LastStoreyAtOrBelow(float y)
        {
            var floors = StoreyFloors();
            int storey = 0;
            for (int i = 1; i < floors.Count; i++)
                if (floors[i] <= y) storey = i;
            return storey;
        }

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
                    if (Rooms[i].SameStorey(Rooms[j]) && Rooms[i].Overlaps(Rooms[j])) problems.Add($"rooms {Rooms[i].Name} and {Rooms[j].Name} overlap");
            foreach (var d in Doors)
            {
                if (d.Width < 0.9f) problems.Add($"doorway {d.A}-{d.B} is {d.Width} m wide; a player needs 0.9 m");
                if (!Room(d.A).SameStorey(Room(d.B))) problems.Add($"doorway {d.A}-{d.B} joins two storeys; use stairs");
                else if (!Room(d.A).Touches(d.X, d.Z, 0.01f) || !Room(d.B).Touches(d.X, d.Z, 0.01f))
                    problems.Add($"doorway {d.A}-{d.B} at ({d.X}, {d.Z}) isn't on a wall the two rooms share");
            }
            foreach (var s in Stairs) ValidateStairs(s, problems);
            if (LobbyRoom != null && !all.Contains(LobbyRoom)) problems.Add($"lobby room {LobbyRoom} doesn't exist");
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

        void ValidateStairs(Stairway s, List<string> problems)
        {
            string name = $"stairs {s.Lower}-{s.Upper}";
            RoomBox lower = Room(s.Lower), upper = Room(s.Upper);
            float rise = upper.FloorY - lower.FloorY;
            if (rise < RoomBox.StoreyTolerance) problems.Add($"{name} must climb from {s.Lower} to a higher storey");
            if (s.Width < 0.9f) problems.Add($"{name} are {s.Width} m wide; a player needs 0.9 m");
            if (!s.AlongAnAxis)
            {
                problems.Add($"{name} must run straight along X or Z");
                return;
            }
            double slope = Math.Atan2(rise, s.Run) * 180.0 / Math.PI;
            if (slope > Stairway.MaxSlopeDegrees + 0.01) problems.Add($"{name} climb at {slope:0.#}°; at most {Stairway.MaxSlopeDegrees}°");
            if (!s.Within(lower) || !s.Within(upper)) problems.Add($"{name} must lie inside both {s.Lower} and {s.Upper}");
            s.Along(-Stairway.StepOff, out float onX, out float onZ);
            s.Along(s.Run + Stairway.StepOff, out float offX, out float offZ);
            if (!lower.Contains(onX, onZ) || !upper.Contains(offX, offZ)) problems.Add($"{name} need clear floor to step on and off");
            foreach (var r in Rooms.Where(r => r.FloorY > lower.FloorY + RoomBox.StoreyTolerance && r.FloorY < upper.FloorY - RoomBox.StoreyTolerance))
                if (r.MinX < s.MaxX && r.MaxX > s.MinX && r.MinZ < s.MaxZ && r.MaxZ > s.MinZ)
                    problems.Add($"{name} pass through the floor of {r.Name}; stairs join neighbouring storeys");
            bool alongZ = Math.Abs(s.ToX - s.FromX) < 0.001f;
            bool ApronMeets(Stairway t, float endX, float endZ, float stepX, float stepZ) => alongZ
                ? t.MinX < s.MaxX && t.MaxX > s.MinX && t.MinZ < Math.Max(endZ, stepZ) && t.MaxZ > Math.Min(endZ, stepZ)
                : t.MinZ < s.MaxZ && t.MaxZ > s.MinZ && t.MinX < Math.Max(endX, stepX) && t.MaxX > Math.Min(endX, stepX);
            s.Along(0f, out float footX, out float footZ);
            s.Along(s.Run, out float topX, out float topZ);
            foreach (var t in Stairs.Where(t => t != s))
            {
                string other = $"the stairs {t.Lower}-{t.Upper}";
                bool InRoom(string room) => t.Lower == room || t.Upper == room;
                if ((InRoom(s.Lower) && ApronMeets(t, footX, footZ, onX, onZ)) || (InRoom(s.Upper) && ApronMeets(t, topX, topZ, offX, offZ)))
                    problems.Add($"{name} step on or off over {other}");
                if (Stairs.IndexOf(t) > Stairs.IndexOf(s) && (InRoom(s.Lower) || InRoom(s.Upper))
                    && t.MinX < s.MaxX && t.MaxX > s.MinX && t.MinZ < s.MaxZ && t.MaxZ > s.MinZ)
                    problems.Add($"{name} overlap {other}");
            }
            bool OnThem(string room, float x, float z) => (room == s.Lower || room == s.Upper) && s.Covers(x, z);
            foreach (var f in Furniture.Where(f => f.Y < 1f))
            {
                if (OnThem(f.Room, f.X, f.Z)) problems.Add($"{f.Word} at ({f.X}, {f.Z}) stands on the {name}");
                float ex = f.Room == s.Lower ? footX : topX, ez = f.Room == s.Lower ? footZ : topZ;
                if ((f.Room == s.Lower || f.Room == s.Upper) && (f.X - ex) * (f.X - ex) + (f.Z - ez) * (f.Z - ez) < DoorClearance * DoorClearance)
                    problems.Add($"{f.Word} at ({f.X}, {f.Z}) is within {DoorClearance} m of an end of the {name}");
            }
            foreach (var p in Spawns.Concat(Keepsakes))
                if (OnThem(p.Room, p.X, p.Z)) problems.Add($"a spawn or keepsake at ({p.X}, {p.Z}) is on the {name}");
            if (ExtractionRoom != null && OnThem(ExtractionRoom, ExtractionX, ExtractionZ))
                problems.Add($"the extraction point is on the {name}");
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
            if (root.Has("stairs"))
                foreach (var s in root["stairs"].Items)
                {
                    var between = s["between"].Strings();
                    if (between.Count != 2) throw new FormatException($"{source}: {s.Path}.between must name the lower and the upper room");
                    var from = s["from"].Floats(2);
                    var to = s["to"].Floats(2);
                    h.Stairs.Add(new Stairway
                    {
                        Lower = between[0], Upper = between[1], FromX = from[0], FromZ = from[1], ToX = to[0], ToZ = to[1], Width = s["width"].Float(1.6f),
                    });
                }
            h.LobbyRoom = root["lobbyRoom"].String(null);
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
