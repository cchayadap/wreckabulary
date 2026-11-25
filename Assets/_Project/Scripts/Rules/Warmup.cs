using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class Warmup
    {
        public const float DefaultSeconds = 30f, ReadyHoldSeconds = 1f, RespawnSeconds = 1f, CardSeconds = 5f;
        public const string Title = "WARM-UP", StartWord = "Start", ReadyWord = "READY";
        public const string Tip = "Spell anything, no letters needed. Nothing counts yet.";
        public const string TypeAnything = "Warm-up: type any recipe, no letters needed.";

        static readonly string[] VersusIds =
        {
            HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush,
        };
        static readonly string[] CrewIds = { HouseModes.MovingOut, HouseModes.Escape, HouseModes.MovingDay };
        static readonly string[] ModeIds = VersusIds.Concat(CrewIds).ToArray();

        sealed class Seat
        {
            public bool Votes, Ready;
            public float Held, Down;
        }

        readonly Dictionary<int, Seat> seats = new Dictionary<int, Seat>();
        float length, elapsed;

        public Warmup(float seconds = DefaultSeconds) => Length = seconds;

        public static IReadOnlyList<string> Modes => ModeIds;
        public static IReadOnlyList<string> VersusModes => VersusIds;
        public static IReadOnlyList<string> CrewModes => CrewIds;

        public static bool Applies(string modeId) => modeId != null && Array.IndexOf(ModeIds, modeId) >= 0;

        public static bool Crew(string modeId) => modeId != null && Array.IndexOf(CrewIds, modeId) >= 0;

        public float Length { get => length; set => length = Math.Max(0f, value); }
        public float Elapsed => elapsed;
        public float TimeLeft => Math.Max(0f, length - elapsed);
        public int Voters { get; private set; }
        public int ReadyCount { get; private set; }
        public bool AllReady => Voters > 0 && ReadyCount == Voters;
        public bool Ends => TimeLeft <= 0f || AllReady;
        public string Clock => FormatClock(TimeLeft);

        public void Join(int seat, bool votes)
        {
            Leave(seat);
            seats[seat] = new Seat { Votes = votes };
            if (votes) Voters++;
        }

        public void Leave(int seat)
        {
            if (!seats.TryGetValue(seat, out var left)) return;
            seats.Remove(seat);
            if (left.Votes) Voters--;
            if (left.Ready) ReadyCount--;
        }

        public bool IsReady(int seat) => seats.TryGetValue(seat, out var found) && found.Ready;

        public float HoldProgress(int seat) =>
            !seats.TryGetValue(seat, out var found) || !found.Votes ? 0f : found.Ready ? 1f : Math.Min(1f, found.Held / ReadyHoldSeconds);

        public void Tick(float seconds) => elapsed += Math.Max(0f, seconds);

        public bool Hold(int seat, bool held, float seconds)
        {
            if (!seats.TryGetValue(seat, out var found) || !found.Votes) return false;
            if (found.Ready) return true;
            if (!held) { found.Held = 0f; return false; }
            found.Held += Math.Max(0f, seconds);
            if (found.Held < ReadyHoldSeconds) return false;
            found.Ready = true;
            ReadyCount++;
            return true;
        }

        public bool Down(int seat, bool down, float seconds)
        {
            if (!seats.TryGetValue(seat, out var found)) return false;
            if (!down) { found.Down = 0f; return false; }
            found.Down += Math.Max(0f, seconds);
            if (found.Down < RespawnSeconds) return false;
            found.Down = 0f;
            return true;
        }

        public int Card(int count) => count <= 0 ? 0 : (int)(elapsed / CardSeconds) % count;

        public string ReadyLine(string startKey = null) =>
            Voters == 0 ? "" : ReadyCount == 0 ? ReadyPrompt(startKey) : $"{ReadyCount} of {Voters} ready";

        public static string ReadyPrompt(string startKey = null) =>
            $"Hold {(string.IsNullOrEmpty(startKey) ? StartWord : startKey)} when ready";

        public static string FormatClock(float seconds)
        {
            int whole = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }

    public static class MatchShowcase
    {
        public const float DefaultSeconds = 10f;
        public const string Lead = "MATCH STARTS IN", YourTeam = "YOUR TEAM", YourCrew = "YOUR CREW", Versus = "VS", BotTag = "BOT";

        public static int Count(float timeLeft) => Math.Max(1, (int)Math.Ceiling(Math.Max(0f, timeLeft)));

        public const int TickFrom = 3;
        public const float SlideSeconds = .45f, SlideStagger = .07f;

        public static bool Ticks(int count) => count > 0 && count <= TickFrom;

        public static int SlideSide(int seat, int seats, int row, int rows)
        {
            if (rows > 1) return row * 2 < rows - 1 ? -1 : row * 2 > rows - 1 ? 1 : 0;
            return seat * 2 < seats - 1 ? -1 : seat * 2 > seats - 1 ? 1 : 0;
        }

        public static float SlideDelay(int seat, int seats, int row, int rows)
        {
            int side = SlideSide(seat, seats, row, rows);
            int fromEdge = side < 0 ? seat : side > 0 ? seats - 1 - seat : seat;
            return Math.Max(0, fromEdge) * SlideStagger;
        }

        public static float Slide(float elapsed, float delay)
        {
            float t = Math.Min(1f, Math.Max(0f, (elapsed - delay) / SlideSeconds));
            float rest = 1f - t;
            return 1f - rest * rest * rest;
        }

        public static string CountLine(float timeLeft) => Lead + " " + Count(timeLeft);

        public static string SkipPrompt(string startKey = null) =>
            $"Hold {(string.IsNullOrEmpty(startKey) ? Warmup.StartWord : startKey)} to skip";

        public static string SkipLine(Warmup votes, string startKey = null) =>
            votes == null || votes.Voters == 0 ? "" : votes.ReadyCount == 0 ? SkipPrompt(startKey) : $"{votes.ReadyCount} of {votes.Voters} ready";

        public static bool Sided(IReadOnlyList<int> teams) =>
            teams != null && teams.Where(t => t != Teams.NoTeam).GroupBy(t => t).Any(side => side.Count() > 1);

        public static IReadOnlyList<IReadOnlyList<int>> Rows(IReadOnlyList<int> teams)
        {
            if (teams == null || teams.Count == 0) return Array.Empty<IReadOnlyList<int>>();
            var seats = Enumerable.Range(0, teams.Count).ToList();
            if (!Sided(teams)) return new IReadOnlyList<int>[] { seats };
            return seats.GroupBy(seat => teams[seat]).OrderBy(side => side.Key).Select(side => (IReadOnlyList<int>)side.ToList()).ToList();
        }

        public static string RowTitle(IReadOnlyList<int> teams, IReadOnlyList<int> row, int yourTeam = Teams.NoTeam)
        {
            if (row == null || row.Count == 0 || !Sided(teams)) return "";
            int team = teams[row[0]];
            return team != Teams.NoTeam && team == yourTeam ? YourTeam : Teams.SideName(team).ToUpperInvariant();
        }

        public static IReadOnlyList<IReadOnlyList<int>> Rows(IReadOnlyList<int> teams, string modeId) =>
            !Warmup.Crew(modeId) || teams == null || teams.Count == 0 ? Rows(teams) : new IReadOnlyList<int>[] { Enumerable.Range(0, teams.Count).ToList() };

        public static string RowTitle(IReadOnlyList<int> teams, IReadOnlyList<int> row, int yourTeam, string modeId) =>
            !Warmup.Crew(modeId) ? RowTitle(teams, row, yourTeam) : row == null || row.Count == 0 ? "" : YourCrew;
    }
}
