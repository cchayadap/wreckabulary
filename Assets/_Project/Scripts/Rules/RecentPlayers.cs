using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Wreckabulary.Rules
{
    public sealed class RecentPlayer
    {
        public string Name { get; }
        public int Seat { get; }
        public bool You { get; }
        public string Initial => PartyPlates.Initial(Name);

        public RecentPlayer(string name, int seat, bool you)
        {
            Name = RecentPlayers.CleanName(name);
            Seat = Math.Min(Math.Max(seat, 0), RecentPlayers.HighestSeat);
            You = you;
        }
    }

    public sealed class RecentMatch
    {
        public string Mode { get; }
        public string Map { get; }
        public long EndedAt { get; }
        public IReadOnlyList<RecentPlayer> Players { get; }

        public RecentMatch(string mode, string map, long endedAt, IEnumerable<RecentPlayer> players)
        {
            Mode = mode ?? "";
            Map = map ?? "";
            EndedAt = Math.Min(Math.Max(endedAt, 0L), Career.LatestTime);
            Players = (players ?? Array.Empty<RecentPlayer>()).Where(player => player != null && player.Name.Length > 0)
                .GroupBy(player => player.Seat).Select(seat => seat.First()).OrderBy(player => player.Seat)
                .Take(RecentPlayers.PlayersKept).ToArray();
        }
    }

    public sealed class RecentPlayers
    {
        public const string StorageKey = "wv.recent.players";
        public const int Version = 1, MatchesKept = 20, PlayersKept = 4, NameLength = 24, HighestSeat = 15;
        public const long KeptSeconds = 30L * SecondsPerDay;
        public const string JustNow = "Just now", Yesterday = "Yesterday", Empty = "Matches you finish show up here, with who played.";
        const long SecondsPerMinute = 60, SecondsPerHour = 3600, SecondsPerDay = 86400;
        readonly List<RecentMatch> matches = new List<RecentMatch>();

        public IReadOnlyList<RecentMatch> Matches => matches;

        public bool Add(RecentMatch match, long now)
        {
            if (match == null || match.Players.Count == 0 || match.Mode.Length == 0) return false;
            if (matches.Any(kept => kept.EndedAt == match.EndedAt && kept.Mode == match.Mode && kept.Map == match.Map)) return false;
            matches.Add(match);
            Tidy(now);
            return matches.Contains(match);
        }

        public void Clear() => matches.Clear();

        public void Tidy(long now)
        {
            matches.RemoveAll(match => now - match.EndedAt > KeptSeconds);
            var ordered = matches.OrderByDescending(match => match.EndedAt).Take(MatchesKept).ToList();
            matches.Clear();
            matches.AddRange(ordered);
        }

        public IReadOnlyList<RecentPlayer> People()
        {
            var people = new List<RecentPlayer>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var match in matches)
                foreach (var player in match.Players)
                    if (!player.You && seen.Add(player.Name)) people.Add(player);
            return people;
        }

        public string Save()
        {
            var text = new StringBuilder("{\"version\":").Append(Version).Append(",\"matches\":[");
            text.Append(string.Join(",", matches.Select(match =>
                "{\"mode\":" + Quote(match.Mode) + ",\"map\":" + Quote(match.Map) +
                ",\"endedAt\":" + match.EndedAt.ToString(CultureInfo.InvariantCulture) + ",\"players\":[" +
                string.Join(",", match.Players.Select(player =>
                    "{\"name\":" + Quote(player.Name) + ",\"seat\":" + player.Seat.ToString(CultureInfo.InvariantCulture) +
                    ",\"you\":" + (player.You ? "true" : "false") + "}")) + "]}")));
            return text.Append("]}").ToString();
        }

        public static RecentPlayers Load(string text, long now)
        {
            var recent = new RecentPlayers();
            if (string.IsNullOrWhiteSpace(text)) return recent;
            JsonNode root;
            try { root = Json.Parse(text, "recent players"); }
            catch (FormatException) { return recent; }
            if (!root.IsObject || ReadInt(() => root["version"].Int(0)) != Version) return recent;
            foreach (var entry in Read(() => root["matches"].Items) ?? Array.Empty<JsonNode>())
            {
                var match = Read(() => new RecentMatch(entry["mode"].String(), entry["map"].String(""),
                    (long)Math.Min(Math.Max(0d, entry["endedAt"].Number()), Career.LatestTime),
                    (Read(() => entry["players"].Items) ?? Array.Empty<JsonNode>()).Select(player => Read(() =>
                        new RecentPlayer(player["name"].String(), player["seat"].Int(), player["you"].Bool(false))))));
                if (match != null) recent.Add(match, now);
            }
            return recent;
        }

        public static string CleanName(string name)
        {
            var text = new StringBuilder();
            foreach (char c in (name ?? "").Trim())
                if (!char.IsControl(c) && text.Length < NameLength) text.Append(c);
            return text.ToString().Trim();
        }

        public static string When(long endedAt, long now)
        {
            long ago = Math.Max(0L, now - endedAt);
            if (ago < SecondsPerMinute) return JustNow;
            if (ago < SecondsPerHour) return (ago / SecondsPerMinute).ToString(CultureInfo.InvariantCulture) + " min ago";
            if (ago < SecondsPerDay) return (ago / SecondsPerHour).ToString(CultureInfo.InvariantCulture) + " h ago";
            if (ago < 2 * SecondsPerDay) return Yesterday;
            return (ago / SecondsPerDay).ToString(CultureInfo.InvariantCulture) + " days ago";
        }

        public static string Names(RecentMatch match, string youWord)
        {
            if (match == null) return "";
            return string.Join(", ", match.Players.Select(player => player.You && !string.IsNullOrEmpty(youWord) ? youWord : player.Name));
        }

        static T Read<T>(Func<T> read) where T : class
        {
            try { return read(); }
            catch (FormatException) { return null; }
            catch (ArgumentException) { return null; }
        }

        static int ReadInt(Func<int> read)
        {
            try { return read(); }
            catch (FormatException) { return -1; }
        }

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
