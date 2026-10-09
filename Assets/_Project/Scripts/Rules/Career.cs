using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Wreckabulary.Rules
{
    public sealed class MatchRecord
    {
        public string Mode = "", Map = "";
        public bool Practice = true;
        public bool Won;
        public int Score, Coins, Xp, Broken, Crafted, Damage;
        public long EndedAt;
    }

    public sealed class ShopOffer
    {
        public readonly string Id, Kind, Value, Name;
        public readonly int Price;

        public ShopOffer(string kind, string value, string name, int price)
        {
            Kind = kind; Value = value; Name = name; Price = price; Id = kind + ":" + value;
        }
    }

    public sealed class Career
    {
        public const int HistoryLength = 20;
        public const int NameLength = 16;
        public const long LatestTime = 253402300799;
        public static readonly IReadOnlyList<string> FreeColours = new[] { "pool", "tomato", "tangerine", "sunflower", "mint" };
        public static readonly IReadOnlyList<string> FreePieces = new[] { "Crewneck", "Hoodie", "Joggers", "Mittens", "Boots", "Cap", "Hood", "Glasses", "Satchel", "TBadge" };
        public static readonly IReadOnlyList<ShopOffer> Shop = new[]
        {
            new ShopOffer("skin", "Candy", "Candy gear", 250),
            new ShopOffer("skin", "Arcade", "Arcade gear", 400),
            new ShopOffer("colour", "sky", "Sky", 120),
            new ShopOffer("colour", "periwinkle", "Periwinkle", 120),
            new ShopOffer("colour", "grape", "Grape", 150),
            new ShopOffer("colour", "bubblegum", "Bubblegum", 150),
            new ShopOffer("colour", "oat", "Oat", 100),
            new ShopOffer("colour", "charcoal", "Charcoal", 180),
            new ShopOffer("theme", "candy", "Candy Carnival", 300),
            new ShopOffer("theme", "lantern", "Lantern Festival", 350),
        }.Concat(CosmeticBundles.All.Where(bundle => bundle.Price > 0)
            .Select(bundle => new ShopOffer("look", bundle.Id, bundle.Name, bundle.Price))).ToArray();

        public string Name = "Housemate";
        public int Coins, Xp, Matches, Wins;
        public readonly List<string> Owned = new List<string>();
        public readonly Dictionary<string, int> Bests = new Dictionary<string, int>();
        public readonly List<MatchRecord> History = new List<MatchRecord>();

        public static int XpToNext(int level) => 100 + 100 * Math.Max(1, level);

        public int Level => LevelOf(Xp, out _);
        public int XpIntoLevel { get { LevelOf(Xp, out int into); return into; } }

        static int LevelOf(int xp, out int into)
        {
            int level = 1;
            into = Math.Max(0, xp);
            while (into >= XpToNext(level)) { into -= XpToNext(level); level++; }
            return level;
        }

        public bool Owns(string kind, string value) =>
            (kind == "skin" && (value == "Classic" || value == "Winter")) ||
            (kind == "colour" && FreeColours.Contains(value)) ||
            (kind == "piece" && FreePieces.Contains(value)) ||
            (kind == "theme" && (value == "sunroom" || value == "winter")) ||
            (kind == "look" && CosmeticBundles.Find(value)?.OwnedBy(this) == true) ||
            Owned.Contains(kind + ":" + value);

        public int PriceOf(ShopOffer offer) => offer.Kind == "look" && CosmeticBundles.Find(offer.Value) is CosmeticBundle bundle
            ? bundle.RemainingPrice(this) : offer.Price;

        public string Buy(string id)
        {
            var offer = Shop.FirstOrDefault(s => s.Id == id);
            if (offer == null) return "That isn't in the shop.";
            if (Owns(offer.Kind, offer.Value)) return "You already own it.";
            int price = PriceOf(offer);
            if (Coins < price) return $"You need {price - Coins} more coins.";
            Coins -= price;
            Grant(id);
            return null;
        }

        public static MatchRecord Reward(string mode, string map, bool practice, bool won, int broken, int crafted, int damage, long endedAt)
        {
            int score = Math.Max(0, broken * 10 + crafted * 25 + damage + (won ? 150 : 0));
            return new MatchRecord
            {
                Mode = mode ?? "", Map = map ?? "", Practice = practice, Won = won,
                Broken = Math.Max(0, broken), Crafted = Math.Max(0, crafted), Damage = Math.Max(0, damage),
                Score = score, Coins = score / 10 + (won ? 20 : 5), Xp = score / 2 + (won ? 60 : 25), EndedAt = endedAt,
            };
        }

        public bool Record(MatchRecord match)
        {
            Coins += match.Coins;
            Xp += match.Xp;
            Matches++;
            if (match.Won) Wins++;
            History.Insert(0, match);
            if (History.Count > HistoryLength) History.RemoveRange(HistoryLength, History.Count - HistoryLength);
            if (Bests.TryGetValue(match.Mode, out int best) && match.Score <= best) return false;
            Bests[match.Mode] = match.Score;
            return match.Score > 0;
        }

        public bool Keep(string skin, string topColour)
        {
            bool skinNew = Grant("skin:" + skin);
            bool colourNew = !FreeColours.Contains(topColour) && Grant("colour:" + topColour);
            return skinNew || colourNew;
        }

        bool Grant(string id)
        {
            if (!Shop.Any(s => s.Id == id) || Owned.Contains(id)) return false;
            if (id.StartsWith("look:", StringComparison.Ordinal) && CosmeticBundles.Find(id.Substring(5)) is CosmeticBundle bundle)
                foreach (string unlock in bundle.Unlocks) Grant(unlock);
            Owned.Add(id);
            return true;
        }

        public string Serialize()
        {
            var s = new StringBuilder("{");
            s.Append("\"name\":").Append(Quote(Name)).Append(",\"coins\":").Append(Coins).Append(",\"xp\":").Append(Xp)
                .Append(",\"matches\":").Append(Matches).Append(",\"wins\":").Append(Wins).Append(",\"owned\":[")
                .Append(string.Join(",", Owned.Select(Quote))).Append("],\"bests\":{")
                .Append(string.Join(",", Bests.OrderBy(b => b.Key, StringComparer.Ordinal).Select(b => Quote(b.Key) + ":" + b.Value)))
                .Append("},\"history\":[");
            s.Append(string.Join(",", History.Select(m =>
                "{\"mode\":" + Quote(m.Mode) + ",\"map\":" + Quote(m.Map) + ",\"practice\":" + Bool(m.Practice) +
                ",\"won\":" + Bool(m.Won) + ",\"score\":" + m.Score + ",\"coins\":" + m.Coins + ",\"xp\":" + m.Xp +
                ",\"broken\":" + m.Broken + ",\"crafted\":" + m.Crafted + ",\"damage\":" + m.Damage +
                ",\"endedAt\":" + m.EndedAt.ToString(CultureInfo.InvariantCulture) + "}")));
            return s.Append("]}").ToString();
        }

        public static Career Deserialize(string text)
        {
            var career = new Career();
            if (string.IsNullOrWhiteSpace(text)) return career;
            JsonNode root;
            try { root = Json.Parse(text, "career"); }
            catch (FormatException) { return career; }
            if (!root.IsObject) return career;
            string name = Read(() => root["name"].String(null));
            if (name != null && name.Trim().Length >= 2) career.Name = name.Trim().Length > NameLength ? name.Trim().Substring(0, NameLength) : name.Trim();
            career.Coins = Math.Max(0, Read(() => root["coins"].Int(0)));
            career.Xp = Math.Max(0, Read(() => root["xp"].Int(0)));
            career.Matches = Math.Max(0, Read(() => root["matches"].Int(0)));
            career.Wins = Math.Min(career.Matches, Math.Max(0, Read(() => root["wins"].Int(0))));
            foreach (string id in Read(() => root["owned"].Strings()) ?? new List<string>())
                career.Grant(id);
            var bests = Read(() => root["bests"].IsObject ? root["bests"] : null);
            if (bests != null)
                foreach (string mode in bests.Keys)
                {
                    int? score = Read<int?>(() => bests[mode].Int());
                    if (score >= 0) career.Bests[mode] = score.Value;
                }
            foreach (var m in Read(() => root["history"].Items) ?? Array.Empty<JsonNode>())
            {
                var match = Read(() => new MatchRecord
                {
                    Mode = m["mode"].String(), Map = m["map"].String(""), Practice = m["practice"].Bool(true), Won = m["won"].Bool(false),
                    Score = Math.Max(0, m["score"].Int(0)), Coins = Math.Max(0, m["coins"].Int(0)), Xp = Math.Max(0, m["xp"].Int(0)),
                    Broken = Math.Max(0, m["broken"].Int(0)), Crafted = Math.Max(0, m["crafted"].Int(0)),
                    Damage = Math.Max(0, m["damage"].Int(0)),
                    EndedAt = (long)Math.Min(Math.Max(0d, m["endedAt"].Number(0)), LatestTime),
                });
                if (match != null && career.History.Count < HistoryLength) career.History.Add(match);
            }
            return career;
        }

        static T Read<T>(Func<T> read)
        {
            try { return read(); }
            catch (FormatException) { return default; }
        }

        static string Bool(bool b) => b ? "true" : "false";

        static string Quote(string text)
        {
            var s = new StringBuilder("\"");
            foreach (char c in text ?? "")
            {
                if (c == '"' || c == '\\') s.Append('\\').Append(c);
                else if (c < ' ') s.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else s.Append(c);
            }
            return s.Append('"').ToString();
        }
    }
}
