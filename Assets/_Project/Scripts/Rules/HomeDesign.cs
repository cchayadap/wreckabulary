using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Wreckabulary.Rules
{
    public sealed class HomeProp
    {
        public string Id;
        public string Word;
        public double X;
        public double Z;
        public int Yaw;
        public string Skin = "Classic";

        public HomeProp Clone() => new HomeProp { Id = Id, Word = Word, X = X, Z = Z, Yaw = Yaw, Skin = Skin };
    }

    public sealed class HomeLayout
    {
        public int Schema = 1;
        public string Map;
        public string Name;
        public List<HomeProp> Props = new List<HomeProp>();

        public HomeLayout Clone() => new HomeLayout
        {
            Schema = Schema, Map = Map, Name = Name,
            Props = Props == null ? null : Props.Select(p => p?.Clone()).ToList()
        };

        public string ToJson()
        {
            var b = new StringBuilder("{\"schema\":").Append(Schema).Append(",\"map\":");
            Quote(b, Map); b.Append(",\"name\":"); Quote(b, Name); b.Append(",\"props\":[");
            for (int i = 0; i < (Props?.Count ?? 0); i++)
            {
                if (i != 0) b.Append(',');
                var p = Props[i];
                if (p == null) { b.Append("null"); continue; }
                b.Append("{\"id\":"); Quote(b, p.Id); b.Append(",\"word\":"); Quote(b, p.Word);
                b.Append(",\"x\":").Append(Number(p.X)).Append(",\"z\":").Append(Number(p.Z));
                b.Append(",\"yaw\":").Append(p.Yaw).Append(",\"skin\":"); Quote(b, p.Skin); b.Append('}');
            }
            return b.Append("]}").ToString();
        }

        static string Number(double n) => n == 0 ? "0" : n.ToString("R", CultureInfo.InvariantCulture);

        static void Quote(StringBuilder b, string text)
        {
            if (text == null) { b.Append("null"); return; }
            b.Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    default:
                        if (c < 32 || char.IsSurrogate(c)) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
        }
    }

    public class HomeValidation
    {
        public bool Ok => Errors.Count == 0;
        public readonly List<string> Errors = new List<string>();
        public HomeLayout Layout;
    }

    public sealed class HomeWordRejection
    {
        public string Word;
        public string Reason;
    }

    public sealed class HomeBatchResult : HomeValidation
    {
        public readonly List<HomeProp> Added = new List<HomeProp>();
        public readonly List<HomeWordRejection> Rejected = new List<HomeWordRejection>();
    }

    public sealed class HomeExport : HomeValidation
    {
        public string Json;
    }

    public sealed class HomeMigration : HomeValidation
    {
        public int Moved, Dropped;
    }

    public sealed class HomeFurniture
    {
        public string Word, Room, Skin;
        public float X, Z, Yaw;
    }

    public sealed class HomeDesigner
    {
        public const int MaxProps = 128;
        public const int MaxTextLength = 2048;
        public const int MaxBatchWords = 64;
        public const int MaxJsonLength = 65536;
        public const double Grid = .5;
        public const double RoomMargin = .15;
        public const double PropGap = .1;
        public const double SpawnRadius = .75;
        public const double DoorMargin = .35;
        const double Epsilon = 1e-6;
        readonly Func<string, HouseLayout> mapFor;
        readonly ItemCatalogue catalogue;
        static readonly Regex IdPattern = new Regex(@"\A[A-Za-z0-9_-]{1,48}\z");
        static readonly Regex JsonNumber = new Regex(@"\G-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?");

        public HomeDesigner(IReadOnlyDictionary<string, HouseLayout> maps, ItemCatalogue items)
            : this(id => maps != null && maps.TryGetValue(id, out var house) ? house : null, items) { }

        public HomeDesigner(Func<string, HouseLayout> maps, ItemCatalogue items)
        {
            mapFor = maps ?? throw new ArgumentNullException(nameof(maps));
            catalogue = items ?? throw new ArgumentNullException(nameof(items));
        }

        public HomeLayout CreateLayout(string map, string name = "My Cozy House") =>
            new HomeLayout { Map = map, Name = TrimName(name ?? "") };

        public static bool Supports(string id) => id == "pinwheel" || id == "courtyard";

        public static double LegacyScale(string id) => id == "pinwheel" ? 1.4 : id == "courtyard" ? 1.5 : 1;

        public static readonly IReadOnlyList<string> PlayableModes = new[] { "Dibs", "Duos" };

        public static bool Playable(string mode) => mode == "Dibs" || mode == "Duos";

        public bool TryFurnish(HomeLayout layout, out List<HomeFurniture> furniture, out string error)
        {
            furniture = null;
            var valid = Validate(layout);
            if (!valid.Ok) { error = string.Join("\n", valid.Errors); return false; }
            var house = House(valid.Layout.Map);
            furniture = new List<HomeFurniture>();
            foreach (var prop in valid.Layout.Props)
            {
                var footprint = Shape(prop, new List<string>()).Value;
                var room = house.Rooms.First(r => Inside(r, footprint));
                furniture.Add(new HomeFurniture
                {
                    Word = prop.Word, Room = room.Name, Skin = prop.Skin, X = (float)prop.X, Z = (float)prop.Z, Yaw = prop.Yaw
                });
            }
            error = null;
            return true;
        }

        HouseLayout House(string id)
        {
            if (!Supports(id)) return null;
            try { return mapFor(id); }
            catch (KeyNotFoundException) { return null; }
        }

        public HomeValidation Validate(HomeLayout layout)
        {
            var result = new HomeValidation();
            if (layout == null) { result.Errors.Add("layout: expected an object."); return result; }
            if (layout.Schema != 1) result.Errors.Add("schema: only schema 1 is supported.");
            var house = House(layout.Map);
            if (house == null) result.Errors.Add("map: choose pinwheel or courtyard.");
            if (!ValidName(layout.Name)) result.Errors.Add("name: use a trimmed name of 1–48 characters.");
            if (layout.Props == null || layout.Props.Count > MaxProps)
            {
                result.Errors.Add(TooManyProps);
                return result;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rectangles = new List<Rect>();
            for (int i = 0; i < layout.Props.Count; i++)
            {
                var p = layout.Props[i];
                var errors = new List<string>();
                var rect = Shape(p, errors);
                if (p != null && p.Id != null && !ids.Add(p.Id)) errors.Add("id: prop IDs must be unique.");
                if (rect.HasValue)
                {
                    if (house != null) Geometry(house, rect.Value, rectangles, errors);
                    rectangles.Add(rect.Value);
                }
                foreach (string error in errors) result.Errors.Add($"{error} [prop {i + 1}]");
            }
            if (result.Ok) result.Layout = layout.Clone();
            return result;
        }

        public HomeValidation PlacementCheck(HomeLayout layout, HomeProp prop, string ignoreId = null)
        {
            var result = Validate(layout);
            result.Layout = null;
            if (!result.Ok) return result;
            bool replacing = ignoreId != null && layout.Props.Any(p => p.Id == ignoreId);
            if (!replacing && layout.Props.Count >= MaxProps) result.Errors.Add(HouseFull);
            var shape = Shape(prop, result.Errors);
            var others = layout.Props.Where(p => p.Id != ignoreId).ToList();
            if (prop != null && others.Any(p => p.Id == prop.Id)) result.Errors.Add("id: prop IDs must be unique.");
            if (shape.HasValue)
                Geometry(House(layout.Map), shape.Value, others.Select(p => Shape(p, new List<string>()).Value).ToList(), result.Errors);
            return result;
        }

        public HomeBatchResult AddWords(HomeLayout layout, string text, string room)
        {
            var result = new HomeBatchResult();
            var valid = Validate(layout);
            result.Errors.AddRange(valid.Errors);
            if (!result.Ok) return result;
            if (text == null || text.Length > MaxTextLength)
            {
                result.Errors.Add("text: enter at most 2048 characters."); return result;
            }
            var words = Tokenize(text);
            if (words.Count > MaxBatchWords)
            {
                result.Errors.Add("text: enter at most 64 words per batch."); return result;
            }
            var house = House(layout.Map);
            var target = house.Rooms.Find(r => r.Name == room);
            if (target == null) { result.Errors.Add("room: choose an authored room."); return result; }
            var next = valid.Layout;
            var occupied = next.Props.Select(p => Shape(p, new List<string>()).Value).ToList();
            var ids = new HashSet<string>(next.Props.Select(p => p.Id), StringComparer.Ordinal);
            foreach (string word in words)
            {
                string reason = null;
                if (!Supplied(word, out _)) reason = "word: choose one of the 40 supplied object words.";
                else if (next.Props.Count >= MaxProps) reason = HouseFull;
                if (reason != null) { Reject(result, word, reason); continue; }
                int serial = 1;
                while (ids.Contains("p" + serial)) serial++;
                var candidate = new HomeProp { Id = "p" + serial, Word = word };
                bool placed = false;
                int firstX = (int)Math.Ceiling((target.MinX + RoomMargin) / Grid);
                int lastX = (int)Math.Floor((target.MaxX - RoomMargin) / Grid);
                int firstZ = (int)Math.Ceiling((target.MinZ + RoomMargin) / Grid);
                int lastZ = (int)Math.Floor((target.MaxZ - RoomMargin) / Grid);
                for (int z = firstZ; z <= lastZ && !placed; z++)
                    for (int x = firstX; x <= lastX && !placed; x++)
                        foreach (int yaw in new[] { 0, 90, 180, 270 })
                        {
                            candidate.X = x * Grid; candidate.Z = z * Grid; candidate.Yaw = yaw;
                            var rect = Shape(candidate, new List<string>()).Value;
                            var errors = new List<string>();
                            if (!Inside(target, rect)) continue;
                            Geometry(house, rect, occupied, errors);
                            if (errors.Count != 0) continue;
                            next.Props.Add(candidate.Clone()); occupied.Add(rect); ids.Add(candidate.Id);
                            result.Added.Add(candidate.Clone()); placed = true; break;
                        }
                if (!placed) Reject(result, word, "room: no safe snapped position remains in this room.");
            }
            result.Layout = next;
            return result;
        }

        public HomeValidation Import(string json)
        {
            var parsed = Parse(json);
            return parsed.Ok ? Validate(parsed.Layout) : parsed;
        }

        public HomeMigration ImportLegacy(string json)
        {
            var parsed = Parse(json);
            if (parsed.Ok) return Migrate(parsed.Layout);
            var failed = new HomeMigration();
            failed.Errors.AddRange(parsed.Errors);
            return failed;
        }

        public HomeMigration Migrate(HomeLayout legacy)
        {
            var result = new HomeMigration();
            var house = legacy == null ? null : House(legacy.Map);
            if (house == null || legacy.Props == null)
            {
                result.Errors.AddRange(Validate(legacy).Errors);
                return result;
            }
            double scale = LegacyScale(legacy.Map);
            var next = new HomeLayout { Schema = legacy.Schema, Map = legacy.Map, Name = legacy.Name };
            var occupied = new List<Rect>();
            foreach (var prop in legacy.Props)
            {
                var scaled = prop?.Clone();
                if (scaled != null) { scaled.X = Snap(prop.X * scale); scaled.Z = Snap(prop.Z * scale); }
                if (scaled == null || !TryPlace(house, scaled, occupied, out var placed)) { result.Dropped++; continue; }
                if (placed.X != scaled.X || placed.Z != scaled.Z || placed.Yaw != scaled.Yaw) result.Moved++;
                next.Props.Add(placed);
                occupied.Add(Shape(placed, new List<string>()).Value);
            }
            var valid = Validate(next);
            result.Errors.AddRange(valid.Errors);
            result.Layout = valid.Layout;
            return result;
        }

        static double Snap(double v) => Finite(v) ? Math.Round(v / Grid) * Grid : v;

        bool TryPlace(HouseLayout house, HomeProp prop, List<Rect> occupied, out HomeProp placed)
        {
            placed = null;
            if (Shape(prop, new List<string>()) == null) return false;
            var yaws = new[] { prop.Yaw }.Concat(new[] { 0, 90, 180, 270 }.Where(y => y != prop.Yaw)).ToArray();
            var spots = new List<(double x, double z)>();
            for (int dx = -MigrationReach; dx <= MigrationReach; dx++)
                for (int dz = -MigrationReach; dz <= MigrationReach; dz++)
                    spots.Add((prop.X + dx * Grid, prop.Z + dz * Grid));
            foreach (var (x, z) in spots.OrderBy(s => (s.x - prop.X) * (s.x - prop.X) + (s.z - prop.Z) * (s.z - prop.Z)))
                foreach (int yaw in yaws)
                {
                    var candidate = prop.Clone();
                    candidate.X = x; candidate.Z = z; candidate.Yaw = yaw;
                    var errors = new List<string>();
                    Geometry(house, Shape(candidate, errors).Value, occupied, errors);
                    if (errors.Count > 0) continue;
                    placed = candidate;
                    return true;
                }
            return false;
        }

        HomeValidation Parse(string json)
        {
            try
            {
                CheckJsonLexemes(json);
                var root = Json.Parse(json, "home.json");
                ExactKeys(root, "schema", "map", "name", "props");
                if (root["schema"].Number() != 1) return Failure("schema: only schema 1 is supported.");
                var layout = new HomeLayout { Map = root["map"].String(), Name = root["name"].String() };
                if (!root["props"].IsArray) return Failure(TooManyProps);
                if (root["props"].Items.Count > MaxProps) return Failure(TooManyProps);
                foreach (var p in root["props"].Items)
                {
                    ExactKeys(p, "id", "word", "x", "z", "yaw", "skin");
                    double yaw = p["yaw"].Number();
                    if (yaw != 0 && yaw != 90 && yaw != 180 && yaw != 270)
                        return Failure("yaw: use 0, 90, 180 or 270 degrees.");
                    layout.Props.Add(new HomeProp
                    {
                        Id = p["id"].String(), Word = p["word"].String(),
                        X = p["x"].Number(), Z = p["z"].Number(), Yaw = (int)yaw, Skin = p["skin"].String()
                    });
                }
                return new HomeValidation { Layout = layout };
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            {
                return Failure("json: " + e.Message);
            }
        }

        public HomeExport Export(HomeLayout layout)
        {
            var valid = Validate(layout);
            var result = new HomeExport { Layout = valid.Layout };
            result.Errors.AddRange(valid.Errors);
            if (result.Ok) result.Json = valid.Layout.ToJson();
            return result;
        }

        static readonly string TooManyProps = $"props: provide an array of at most {MaxProps} objects.";
        static readonly string HouseFull = $"props: the house already has {MaxProps} objects.";
        const int MigrationReach = 12;

        static HomeValidation Failure(string error)
        {
            var result = new HomeValidation(); result.Errors.Add(error); return result;
        }

        static void ExactKeys(JsonNode node, params string[] keys)
        {
            if (!node.IsObject || keys.Any(k => !node.Has(k)) || node.Keys.Any(k => !keys.Contains(k)))
                throw node.Error("expected exactly " + string.Join(", ", keys));
        }

        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
        static bool Snapped(double v) => Finite(v) && Finite(v * 2) && v * 2 == Math.Truncate(v * 2);
        static bool ValidName(string name) => name != null && name.Length >= 1 && name.Length <= 48 && name == TrimName(name);

        static string TrimName(string name)
        {
            int first = 0, last = name.Length;
            while (first < last && WhiteSpace(name[first])) first++;
            while (last > first && WhiteSpace(name[last - 1])) last--;
            return name.Substring(first, last - first);
        }

        bool Supplied(string word, out ItemDefinition item)
        {
            item = null;
            return word != null && catalogue.TryGet(word, out item) && !string.IsNullOrEmpty(item.Model) &&
                item.Size != null && item.Size.Length == 3 && Finite(item.Size[0]) && Finite(item.Size[2]) &&
                item.Size[0] > 0 && item.Size[2] > 0;
        }

        readonly struct Rect
        {
            public readonly double MinX, MinZ, MaxX, MaxZ;
            public Rect(double x, double z, double width, double depth)
            { MinX = x - width / 2; MaxX = x + width / 2; MinZ = z - depth / 2; MaxZ = z + depth / 2; }
        }

        Rect? Shape(HomeProp prop, List<string> errors)
        {
            if (prop == null) { errors.Add("prop: expected an object."); return null; }
            if (prop.Id == null || !IdPattern.IsMatch(prop.Id)) errors.Add("id: use 1–48 letters, digits, underscores or hyphens.");
            bool supplied = Supplied(prop.Word, out var item);
            if (!supplied) errors.Add("word: choose one of the 40 supplied object words.");
            bool grid = Snapped(prop.X) && Snapped(prop.Z);
            if (!grid) errors.Add("grid: use finite half-metre coordinates.");
            bool yaw = prop.Yaw == 0 || prop.Yaw == 90 || prop.Yaw == 180 || prop.Yaw == 270;
            if (!yaw) errors.Add("yaw: use 0, 90, 180 or 270 degrees.");
            if (prop.Skin != "Classic" && prop.Skin != "Candy" && prop.Skin != "Arcade")
                errors.Add("skin: choose Classic, Candy or Arcade.");
            if (!supplied || !grid || !yaw) return null;
            double width = Math.Max(.4, item.Size[0]), depth = Math.Max(.4, item.Size[2]);
            return prop.Yaw % 180 == 0 ? new Rect(prop.X, prop.Z, width, depth) : new Rect(prop.X, prop.Z, depth, width);
        }

        static bool Inside(RoomBox room, Rect r) =>
            r.MinX >= room.MinX + RoomMargin - Epsilon && r.MaxX <= room.MaxX - RoomMargin + Epsilon &&
            r.MinZ >= room.MinZ + RoomMargin - Epsilon && r.MaxZ <= room.MaxZ - RoomMargin + Epsilon;

        static double CircleDistance(Rect r, double x, double z)
        {
            double dx = Math.Max(r.MinX - x, Math.Max(0, x - r.MaxX));
            double dz = Math.Max(r.MinZ - z, Math.Max(0, z - r.MaxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        static double RectDistance(Rect a, Rect b)
        {
            double dx = Math.Max(a.MinX - b.MaxX, Math.Max(0, b.MinX - a.MaxX));
            double dz = Math.Max(a.MinZ - b.MaxZ, Math.Max(0, b.MinZ - a.MaxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }

        static void Geometry(HouseLayout house, Rect r, List<Rect> others, List<string> errors)
        {
            if (!house.Rooms.Any(room => Inside(room, r))) errors.Add("room: keep the entire footprint 0.15 m inside one room.");
            if (house.Doors.Any(d => CircleDistance(r, d.X, d.Z) < d.Width / 2 + DoorMargin - Epsilon))
                errors.Add("door: this footprint blocks a reserved doorway.");
            if (house.Spawns.Any(s => CircleDistance(r, s.X, s.Z) < SpawnRadius - Epsilon))
                errors.Add("spawn: this footprint blocks a reserved spawn.");
            if (others.Any(other => RectDistance(r, other) < PropGap - Epsilon))
                errors.Add("overlap: leave at least 0.1 m between object footprints.");
        }

        static List<string> Tokenize(string text)
        {
            var result = new List<string>(); int start = 0;
            for (int i = 0; i <= text.Length; i++)
                if (i == text.Length || Separator(text[i]))
                {
                    if (i > start)
                    {
                        var token = text.Substring(start, i - start).ToCharArray();
                        for (int c = 0; c < token.Length; c++)
                            if (token[c] >= 'a' && token[c] <= 'z') token[c] = (char)(token[c] - 'a' + 'A');
                        result.Add(new string(token));
                    }
                    start = i + 1;
                }
            return result;
        }

        static bool Separator(char c) => c == ',' || c == ';' || WhiteSpace(c);

        static bool WhiteSpace(char c) => c == '\t' || c == '\n' || c == '\v' || c == '\f' ||
            c == '\r' || c == ' ' || c == '\u00a0' || c == '\u1680' || (c >= '\u2000' && c <= '\u200a') ||
            c == '\u2028' || c == '\u2029' || c == '\u202f' || c == '\u205f' || c == '\u3000' || c == '\ufeff';

        static void Reject(HomeBatchResult result, string word, string reason) =>
            result.Rejected.Add(new HomeWordRejection { Word = word, Reason = reason });

        static void CheckJsonLexemes(string text)
        {
            if (text == null || text.Length > MaxJsonLength) throw new FormatException("provide at most 65536 JSON characters.");
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == ':' || c == ',') continue;
                if (c == '{' || c == '[') { if (++depth > 16) throw new FormatException("JSON nesting exceeds 16 levels."); continue; }
                if (c == '}' || c == ']') { depth--; continue; }
                if (c == '"')
                {
                    bool closed = false;
                    while (++i < text.Length)
                    {
                        c = text[i];
                        if (c == '"') { closed = true; break; }
                        if (c < 32) throw new FormatException("unescaped control character in string.");
                        if (c != '\\') continue;
                        if (++i >= text.Length) throw new FormatException("unterminated escape.");
                        c = text[i];
                        if (c == 'u')
                        {
                            for (int h = 0; h < 4; h++)
                                if (++i >= text.Length || !Uri.IsHexDigit(text[i])) throw new FormatException("invalid Unicode escape.");
                        }
                        else if ("\"\\/bfnrt".IndexOf(c) < 0) throw new FormatException("invalid string escape.");
                    }
                    if (!closed) throw new FormatException("unterminated string.");
                    continue;
                }
                int end;
                if (c == '-' || (c >= '0' && c <= '9'))
                {
                    var match = JsonNumber.Match(text, i);
                    if (!match.Success) throw new FormatException("invalid JSON number.");
                    end = i + match.Length;
                }
                else
                {
                    string token = c == 't' ? "true" : c == 'f' ? "false" : c == 'n' ? "null" : null;
                    if (token == null || i + token.Length > text.Length || string.CompareOrdinal(text, i, token, 0, token.Length) != 0)
                        throw new FormatException("invalid JSON token.");
                    end = i + token.Length;
                }
                if (end < text.Length && " \t\r\n,]}:".IndexOf(text[end]) < 0) throw new FormatException("invalid JSON token boundary.");
                i = end - 1;
            }
        }
    }
}
