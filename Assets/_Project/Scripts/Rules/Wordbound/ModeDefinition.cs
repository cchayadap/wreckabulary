using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Wreckabulary.Rules.Wordbound
{
    public enum RaceFormat { Solo = 1, Duo = 2, Squad = 4 }
    public enum WordboundObjective { DeliverKeepsake, Knockouts, Explore }
    public enum WordboundOwnership { TeamIsolated, ProtectedProgress, Shared }

    public sealed class ModeDefinition
    {
        static readonly ReadOnlyCollection<string> recipeIds = Array.AsReadOnly(new[]
        {
            "BAT", "BROOM", "HAMMER", "SPEAR", "SHIELD", "PLATE",
            "TABLE", "SOFA", "BED", "STOOL", "FAN", "CLOCK"
        });

        public const int Version = 1;
        public const int TickRate = 60;
        public const int ReconnectSeconds = 120;
        public const int KeepsakeRecoverySeconds = 10;
        public const int RecoveryProtectionSeconds = 2;
        public const int MaximumHitStunSeconds = 1;
        public const int ProvisionalWindowSeconds = 2;
        public string Id { get; }
        public string Name { get; }
        public RaceFormat Format { get; }
        public WordboundObjective Objective { get; }
        public WordboundOwnership Ownership { get; }
        public int MinimumPlayers { get; }
        public int MaximumPlayers { get; }
        public int TeamCount { get; }
        public int TeamCapacity { get; }
        public int TimeLimitSeconds { get; }
        public int Laps { get; }
        public int KnockoutTarget { get; }
        public bool EnemyDamage { get; }
        public bool HazardDamage { get; }
        public bool PublicQueue { get; }
        public bool SupportsFill { get; }
        public bool IsRace => Objective == WordboundObjective.DeliverKeepsake;
        public bool HasRaceFormat => Id == "TeamRace" || Id == "BrawlRace";
        public string CourseKind => IsRace ? "Circuit" : Objective == WordboundObjective.Knockouts ? "Arena" : "Explore";
        public IReadOnlyList<string> Recipes => recipeIds;
        public string Hash { get; }

        ModeDefinition(string id, string name, RaceFormat format, WordboundObjective objective,
            WordboundOwnership ownership, int minimum, int maximum, int teams, int capacity,
            int seconds, bool damage, bool hazards, bool publicQueue, bool fill)
        {
            Id = id; Name = name; Format = format; Objective = objective; Ownership = ownership;
            MinimumPlayers = minimum; MaximumPlayers = maximum; TeamCount = teams; TeamCapacity = capacity;
            TimeLimitSeconds = seconds; EnemyDamage = damage; HazardDamage = hazards;
            PublicQueue = publicQueue; SupportsFill = fill;
            Laps = IsRace ? 3 : 0;
            KnockoutTarget = objective == WordboundObjective.Knockouts ? 10 : 0;
            Hash = WordboundIdentity.Sha256(string.Join("|", new object[] { "wordbound.mode", Version, id, (int)format,
                (int)objective, (int)ownership, minimum, maximum, teams, capacity, seconds, damage, hazards,
                publicQueue, fill, Laps, KnockoutTarget, TickRate, ReconnectSeconds, KeepsakeRecoverySeconds,
                RecoveryProtectionSeconds, MaximumHitStunSeconds, ProvisionalWindowSeconds, string.Join(",", recipeIds) }
                .Select(value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture))));
        }

        public static ModeDefinition For(string id, RaceFormat format = RaceFormat.Solo)
        {
            if (format != RaceFormat.Solo && format != RaceFormat.Duo && format != RaceFormat.Squad)
                throw new ArgumentOutOfRangeException(nameof(format));
            if (id != "TeamRace" && id != "BrawlRace" && format != RaceFormat.Solo)
                throw new ArgumentException("Only Team Race and Brawl Race have Solo/Duo/Squad formats.", nameof(format));
            int capacity = (int)format;
            return id switch
            {
                "TimeRun" => new ModeDefinition(id, "Time Run", format, WordboundObjective.DeliverKeepsake,
                    WordboundOwnership.TeamIsolated, 1, 4, 1, 4, 1500, false, true, true, true),
                "TeamRace" => new ModeDefinition(id, "Team Race", format, WordboundObjective.DeliverKeepsake,
                    WordboundOwnership.TeamIsolated, 2, 2 * capacity, 2, capacity, 1500, false, true, true, true),
                "BrawlRace" => new ModeDefinition(id, "Brawl Race", format, WordboundObjective.DeliverKeepsake,
                    WordboundOwnership.ProtectedProgress, 2, 2 * capacity, 2, capacity, 1500, true, true, true, true),
                "FreeForAll" => new ModeDefinition(id, "Free-for-All", format, WordboundObjective.Knockouts,
                    WordboundOwnership.Shared, 2, 8, 8, 1, 480, true, true, true, false),
                "Freestyle" => new ModeDefinition(id, "Freestyle", format, WordboundObjective.Explore,
                    WordboundOwnership.Shared, 1, 8, 1, 8, 0, false, false, false, false),
                _ => throw new ArgumentException("Unknown Wordbound mode.", nameof(id))
            };
        }

        public bool CanDamage(int attackerTeam, int victimTeam) =>
            EnemyDamage && attackerTeam >= 0 && victimTeam >= 0 && attackerTeam != victimTeam;

        public bool CanInteract(int actorTeam, int ownerTeam, bool progressionResource) =>
            actorTeam >= 0 && (ownerTeam < 0 || actorTeam == ownerTeam ||
                (Ownership != WordboundOwnership.TeamIsolated && !progressionResource));

        public bool PlayersCollide(int firstTeam, int secondTeam) =>
            Id != "TeamRace" || firstTeam == secondTeam;

        public IReadOnlyList<string> ValidateRoster(IEnumerable<WordboundSeat> source)
        {
            var errors = new List<string>();
            var seats = source?.ToArray() ?? Array.Empty<WordboundSeat>();
            if (seats.Length < MinimumPlayers || seats.Length > MaximumPlayers)
                errors.Add($"This mode needs {MinimumPlayers}–{MaximumPlayers} players.");
            var players = new HashSet<string>(StringComparer.Ordinal);
            var positions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                if (seat == null) { errors.Add("A roster seat is missing."); continue; }
                if (!WordboundIdentity.IsId(seat.PlayerId) || !players.Add(seat.PlayerId))
                    errors.Add("Player IDs must be valid and unique.");
                if (seat.Team < 0 || seat.Team >= TeamCount || seat.Slot < 0 || seat.Slot >= TeamCapacity ||
                    !positions.Add(seat.Team + ":" + seat.Slot))
                    errors.Add("Team seats must be valid and unique.");
            }
            if (HasRaceFormat && (!seats.Any(s => s != null && s.Team == 0) || !seats.Any(s => s != null && s.Team == 1)))
                errors.Add("Both race teams need at least one human.");
            return errors.AsReadOnly();
        }

        public GameRules CreateGameplayRules(GameRules defaults)
        {
            var rules = (defaults ?? throw new ArgumentNullException(nameof(defaults))).Clone();
            rules.Mode = Id;
            rules.TeamSize = TeamCapacity;
            rules.FriendlyFire = false;
            rules.DownedEnabled = false;
            rules.RoundsToWin = 1;
            rules.RespawnSeconds = 2f;
            rules.ReconnectGraceSeconds = ReconnectSeconds;
            rules.RoundTimeLimitSeconds = TimeLimitSeconds;
            rules.ClearOutEnabled = false;
            rules.HitStunMax = Math.Min(MaximumHitStunSeconds, rules.HitStunMax);
            rules.SpawnProtectionSeconds = RecoveryProtectionSeconds;
            rules.StarterLetters = "";
            return rules;
        }
    }

    public sealed class WordboundSeat
    {
        public string PlayerId { get; }
        public int Team { get; }
        public int Slot { get; }
        public WordboundSeat(string playerId, int team, int slot = 0)
        { PlayerId = playerId; Team = team; Slot = slot; }
    }

    public static class WordboundIdentity
    {
        public static bool IsId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 64 &&
            value.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == ':');

        public static bool IsHash(string value) => value != null && value.Length == 64 &&
            value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        public static string Sha256(string value)
        {
            using var algorithm = SHA256.Create();
            var digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value));
            var result = new StringBuilder(64);
            foreach (byte b in digest) result.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return result.ToString();
        }
    }
}
