using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class RaceCheckpoint
    {
        public string Id { get; }
        public IReadOnlyList<string> Puzzles { get; }
        public RaceCheckpoint(string id, params string[] puzzles)
        {
            if (!WordboundIdentity.IsId(id)) throw new ArgumentException("Invalid checkpoint ID.", nameof(id));
            if (puzzles == null || puzzles.Length > 64 || puzzles.Any(p => !WordboundIdentity.IsId(p)) ||
                puzzles.Distinct(StringComparer.Ordinal).Count() != puzzles.Length)
                throw new ArgumentException("Puzzle IDs must be bounded and unique.", nameof(puzzles));
            Id = id; Puzzles = Array.AsReadOnly((string[])puzzles.Clone());
        }
    }

    public sealed class RaceLap
    {
        public string Start { get; }
        public string Finish { get; }
        public string Story { get; }
        public IReadOnlyList<RaceCheckpoint> Checkpoints { get; }
        public RaceLap(string start, string finish, string story, params RaceCheckpoint[] checkpoints)
        {
            if (!WordboundIdentity.IsId(start) || !WordboundIdentity.IsId(finish))
                throw new ArgumentException("A lap needs valid start and finish IDs.");
            if (story == null || story.Length > 160) throw new ArgumentException("Story label is too long.");
            if (checkpoints == null || checkpoints.Length < 1 || checkpoints.Length > 256 ||
                checkpoints.Any(c => c == null) ||
                checkpoints.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != checkpoints.Length ||
                checkpoints.SelectMany(c => c.Puzzles).Distinct(StringComparer.Ordinal).Count() != checkpoints.Sum(c => c.Puzzles.Count))
                throw new ArgumentException("Each lap needs unique, ordered checkpoints and puzzle IDs.");
            Start = start; Finish = finish; Story = story;
            Checkpoints = Array.AsReadOnly((RaceCheckpoint[])checkpoints.Clone());
        }
    }

    public enum ProgressEventKind { PuzzleSatisfied, CheckpointReached, FinishReached, Knockout, Fell }

    public sealed class ProgressEvent
    {
        public ProgressEventKind Kind { get; }
        public string PlayerId { get; }
        public string Target { get; }
        public int Lap { get; }
        public ProgressEvent(ProgressEventKind kind, string playerId, string target = "", int lap = 0)
        { Kind = kind; PlayerId = playerId; Target = target; Lap = lap; }
    }

    public sealed class TeamProgress
    {
        readonly HashSet<string> puzzles = new HashSet<string>(StringComparer.Ordinal);
        public int Team { get; internal set; }
        public int Lap { get; internal set; }
        public int NextCheckpoint { get; internal set; }
        public int OrderedObjectives { get; internal set; }
        public long ProgressTick { get; internal set; }
        public string SafeCheckpoint { get; internal set; }
        public string KeepsakeCarrier { get; internal set; }
        public string KeepsakeLocation { get; internal set; }
        public long KeepsakeRecoveryAt { get; internal set; } = -1;
        public int RecoveryGeneration { get; internal set; }
        public bool Finished { get; internal set; }
        public IReadOnlyList<string> CompletedPuzzles => Array.AsReadOnly(puzzles.OrderBy(p => p, StringComparer.Ordinal).ToArray());
        internal bool HasPuzzle(string id) => puzzles.Contains(id);
        internal bool AddPuzzle(string id) => puzzles.Add(id);
        internal void ClearPuzzles() => puzzles.Clear();
        internal TeamProgress Copy()
        {
            var result = new TeamProgress
            {
                Team = Team, Lap = Lap, NextCheckpoint = NextCheckpoint, OrderedObjectives = OrderedObjectives,
                ProgressTick = ProgressTick, SafeCheckpoint = SafeCheckpoint, KeepsakeCarrier = KeepsakeCarrier,
                KeepsakeLocation = KeepsakeLocation, KeepsakeRecoveryAt = KeepsakeRecoveryAt,
                RecoveryGeneration = RecoveryGeneration, Finished = Finished
            };
            foreach (string puzzle in puzzles) result.puzzles.Add(puzzle);
            return result;
        }
    }

    public sealed class PlayerProgress
    {
        public string Id { get; internal set; }
        public int Team { get; internal set; }
        public int Slot { get; internal set; }
        public int Knockouts { get; internal set; }
        public bool Connected { get; internal set; } = true;
        public long ReconnectDeadlineMs { get; internal set; } = -1;
        public long ProtectedUntil { get; internal set; }
        public long RespawnAt { get; internal set; } = -1;
        public bool Eliminated { get; internal set; }
        public int RecoveryGeneration { get; internal set; }
        internal PlayerProgress Copy() => (PlayerProgress)MemberwiseClone();
    }

    public sealed class MatchResult
    {
        public string Outcome { get; }
        public IReadOnlyList<int> WinningTeams { get; }
        public long Tick { get; }
        public bool Confirmed { get; internal set; }
        internal MatchResult(string outcome, IEnumerable<int> winners, long tick)
        { Outcome = outcome; WinningTeams = Array.AsReadOnly(winners.OrderBy(t => t).ToArray()); Tick = tick; }
        internal MatchResult Copy() => new MatchResult(Outcome, WinningTeams, Tick) { Confirmed = Confirmed };
    }

    public sealed class ProgressSnapshot
    {
        public string DefinitionHash { get; }
        public long Tick { get; }
        public long ElapsedTicks { get; }
        public long WallClockMs { get; }
        public bool Migrating { get; }
        public IReadOnlyList<TeamProgress> Teams { get; }
        public IReadOnlyList<PlayerProgress> Players { get; }
        public MatchResult Result { get; }
        internal ProgressSnapshot(string definitionHash, long tick, long elapsed, long wallClock, bool migrating,
            IEnumerable<TeamProgress> teams, IEnumerable<PlayerProgress> players, MatchResult result)
        {
            DefinitionHash = definitionHash;
            Tick = tick; ElapsedTicks = elapsed; WallClockMs = wallClock; Migrating = migrating;
            Teams = Array.AsReadOnly(teams.OrderBy(t => t.Team).Select(t => t.Copy()).ToArray());
            Players = Array.AsReadOnly(players.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Copy()).ToArray());
            Result = result?.Copy();
        }
    }

    public sealed class MatchProgress
    {
        readonly Dictionary<string, PlayerProgress> players;
        readonly Dictionary<int, TeamProgress> teams;
        readonly ReadOnlyCollection<RaceLap> laps;
        readonly string definitionHash;
        public ModeDefinition Mode { get; }
        public long Tick { get; private set; }
        public long ElapsedTicks { get; private set; }
        public long WallClockMs { get; private set; }
        public bool Migrating { get; private set; }
        public MatchResult Result { get; private set; }
        public IReadOnlyList<RaceLap> Laps => laps;
        public TeamProgress Team(int team) => teams.TryGetValue(team, out var value) ? value.Copy() : null;
        public PlayerProgress Player(string player) => player != null && players.TryGetValue(player, out var value) ? value.Copy() : null;

        public MatchProgress(ModeDefinition mode, IEnumerable<WordboundSeat> roster,
            IEnumerable<RaceLap> route = null, long wallClockMs = 0)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            var seats = roster?.ToArray() ?? Array.Empty<WordboundSeat>();
            var errors = mode.ValidateRoster(seats);
            if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors), nameof(roster));
            var lapArray = route?.ToArray() ?? Array.Empty<RaceLap>();
            if (mode.IsRace && (lapArray.Length != mode.Laps || lapArray.Any(l => l == null)))
                throw new ArgumentException("A race requires exactly three resolved lap plans.", nameof(route));
            if (!mode.IsRace && lapArray.Length != 0)
                throw new ArgumentException("A non-race mode cannot use a race route.", nameof(route));
            if (wallClockMs < 0 || wallClockMs > long.MaxValue - 120000)
                throw new ArgumentOutOfRangeException(nameof(wallClockMs));
            laps = Array.AsReadOnly(lapArray);
            definitionHash = DefinitionHash(mode, seats, lapArray);
            players = seats.ToDictionary(s => s.PlayerId, s => new PlayerProgress
            {
                Id = s.PlayerId, Team = s.Team, Slot = s.Slot,
                ProtectedUntil = ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate
            }, StringComparer.Ordinal);
            teams = seats.Select(s => s.Team).Distinct().ToDictionary(t => t, t => new TeamProgress
            { Team = t, SafeCheckpoint = mode.IsRace ? laps[0].Start : "spawn", KeepsakeLocation = mode.IsRace ? laps[0].Start : "" });
            WallClockMs = wallClockMs;
        }

        public void Advance(long nextTick, long wallClockMs, IEnumerable<ProgressEvent> hostObservations = null)
        {
            if (nextTick != Tick + 1 || nextTick < 1) throw new ArgumentOutOfRangeException(nameof(nextTick), "Resolve consecutive ticks.");
            var observations = hostObservations?.Take(4097).ToArray() ?? Array.Empty<ProgressEvent>();
            if (observations.Length > 4096 || observations.Any(o => o == null ||
                !Enum.IsDefined(typeof(ProgressEventKind), o.Kind)))
                throw new ArgumentException("Observation batch is invalid.", nameof(hostObservations));
            UpdateWallClock(wallClockMs);
            if (Migrating || Result != null) return;
            Tick = nextTick;
            ElapsedTicks++;
            foreach (var p in players.Values)
                if (p.Eliminated && p.RespawnAt >= 0 && Tick >= p.RespawnAt)
                {
                    p.Eliminated = false; p.RespawnAt = -1; p.RecoveryGeneration++;
                    p.ProtectedUntil = Tick + ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate;
                }
            foreach (var team in teams.Values)
                if (team.KeepsakeRecoveryAt >= 0 && Tick >= team.KeepsakeRecoveryAt)
                    RecoverKeepsake(team);

            var ordered = observations.OrderBy(o => o.PlayerId, StringComparer.Ordinal)
                .ThenBy(o => o.Kind).ThenBy(o => o.Target, StringComparer.Ordinal).ToArray();
            var eligibleAttackers = new HashSet<string>(players.Values
                .Where(p => p.Connected && !p.Eliminated).Select(p => p.Id), StringComparer.Ordinal);
            foreach (var observation in ordered.Where(o => o.Kind == ProgressEventKind.PuzzleSatisfied))
                ApplyPuzzle(observation);
            foreach (var observation in ordered.Where(o => o.Kind == ProgressEventKind.CheckpointReached))
                ApplyCheckpoint(observation);
            foreach (var observation in ordered.Where(o => o.Kind == ProgressEventKind.Fell))
                RecoverPlayer(observation.PlayerId);
            var candidates = new HashSet<int>();
            foreach (var observation in ordered.Where(o => o.Kind == ProgressEventKind.FinishReached))
                if (ApplyFinish(observation)) candidates.Add(players[observation.PlayerId].Team);
            var knockedOut = new HashSet<string>(StringComparer.Ordinal);
            foreach (var observation in ordered.Where(o => o.Kind == ProgressEventKind.Knockout))
                ApplyKnockout(observation, knockedOut, eligibleAttackers);
            if (Mode.Objective == WordboundObjective.Knockouts)
                foreach (var p in players.Values.Where(p => p.Knockouts >= Mode.KnockoutTarget)) candidates.Add(p.Team);
            if (candidates.Count != 0)
                Result = new MatchResult(candidates.Count == 1 ? "Win" : "Draw", candidates, Tick);
            else if (Mode.TimeLimitSeconds > 0 && ElapsedTicks >= (long)Mode.TimeLimitSeconds * ModeDefinition.TickRate)
                Timeout();
        }

        public bool TakeKeepsake(string playerId)
        {
            if (!CanAct(playerId, out var p) || !Mode.IsRace) return false;
            var team = teams[p.Team];
            if (team.Finished || team.KeepsakeCarrier != null) return false;
            team.KeepsakeCarrier = playerId; team.KeepsakeRecoveryAt = -1; team.KeepsakeLocation = "";
            return true;
        }

        public bool DropKeepsake(string playerId, string reachableLocation = null)
        {
            if (!players.TryGetValue(playerId ?? "", out var p)) return false;
            var team = teams[p.Team];
            if (!Mode.IsRace || team.KeepsakeCarrier != playerId) return false;
            team.KeepsakeCarrier = null;
            team.KeepsakeLocation = reachableLocation ?? "";
            team.KeepsakeRecoveryAt = Tick + ModeDefinition.KeepsakeRecoverySeconds * ModeDefinition.TickRate;
            return true;
        }

        public bool RecoverPlayer(string playerId)
        {
            if (!CanAct(playerId, out var p)) return false;
            DropKeepsake(playerId);
            p.Eliminated = true;
            p.RespawnAt = Tick + 2 * ModeDefinition.TickRate;
            return true;
        }

        public bool Disconnect(string playerId, long wallClockMs)
        {
            UpdateWallClock(wallClockMs);
            if (!players.TryGetValue(playerId ?? "", out var p) || !p.Connected) return false;
            p.Connected = false;
            p.ReconnectDeadlineMs = checked(wallClockMs + ModeDefinition.ReconnectSeconds * 1000L);
            DropKeepsake(playerId);
            return true;
        }

        public bool Reconnect(string playerId, long wallClockMs)
        {
            UpdateWallClock(wallClockMs);
            if (!players.TryGetValue(playerId ?? "", out var p) || p.Connected ||
                p.ReconnectDeadlineMs <= wallClockMs || p.ReconnectDeadlineMs < 0) return false;
            p.Connected = true; p.ReconnectDeadlineMs = -1;
            return true;
        }

        public void BeginMigration() => Migrating = true;
        public void CompleteMigration(long wallClockMs) { UpdateWallClock(wallClockMs); Migrating = false; }

        public ProgressSnapshot Capture() => new ProgressSnapshot(definitionHash, Tick, ElapsedTicks, WallClockMs, Migrating,
            teams.Values, players.Values, Result);

        public void Restore(ProgressSnapshot snapshot)
        {
            if (snapshot == null || snapshot.DefinitionHash != definitionHash ||
                snapshot.Tick < 0 || snapshot.Tick > long.MaxValue - 120000 || snapshot.ElapsedTicks != snapshot.Tick ||
                snapshot.WallClockMs < 0 || snapshot.WallClockMs > long.MaxValue - 120000 ||
                snapshot.Players.Count != players.Count || snapshot.Teams.Count != teams.Count ||
                snapshot.Players.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != players.Count ||
                snapshot.Teams.Select(t => t.Team).Distinct().Count() != teams.Count ||
                !snapshot.Players.All(p => players.TryGetValue(p.Id, out var expected) && p.Team == expected.Team && p.Slot == expected.Slot) ||
                !snapshot.Teams.All(t => teams.ContainsKey(t.Team)))
                throw new ArgumentException("Snapshot roster does not match this match.", nameof(snapshot));
            foreach (var p in snapshot.Players)
                if (p.Knockouts < 0 || p.RecoveryGeneration < 0 || p.ProtectedUntil < 0 ||
                    p.ReconnectDeadlineMs < -1 || p.RespawnAt < -1 ||
                    p.ReconnectDeadlineMs > snapshot.WallClockMs + ModeDefinition.ReconnectSeconds * 1000L ||
                    p.ProtectedUntil > snapshot.Tick + ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate ||
                    (p.RespawnAt >= 0 && (p.RespawnAt <= snapshot.Tick || p.RespawnAt > snapshot.Tick + 2 * ModeDefinition.TickRate)) ||
                    (Mode.Objective != WordboundObjective.Knockouts && p.Knockouts != 0) ||
                    (p.Connected && p.ReconnectDeadlineMs != -1) || (!p.Connected && p.ReconnectDeadlineMs < 0) ||
                    (p.Eliminated != (p.RespawnAt >= 0)))
                    throw new ArgumentException("Snapshot player state is invalid.", nameof(snapshot));
            foreach (var t in snapshot.Teams)
            {
                if (t.Lap < 0 || t.Lap > Mode.Laps || t.NextCheckpoint < 0 ||
                    t.ProgressTick < 0 || t.ProgressTick > snapshot.Tick || t.OrderedObjectives < 0 ||
                    t.KeepsakeRecoveryAt < -1 || t.RecoveryGeneration < 0 ||
                    (Mode.IsRace && t.Lap < Mode.Laps && t.NextCheckpoint > laps[t.Lap].Checkpoints.Count) ||
                    (Mode.IsRace && t.Finished != (t.Lap == Mode.Laps)) ||
                    (t.KeepsakeCarrier != null && (!players.TryGetValue(t.KeepsakeCarrier, out var p) || p.Team != t.Team ||
                        t.KeepsakeRecoveryAt != -1 || t.KeepsakeLocation != "")))
                    throw new ArgumentException("Snapshot progression is invalid.", nameof(snapshot));
                if (t.KeepsakeLocation == null || (t.KeepsakeLocation != "" && !WordboundIdentity.IsId(t.KeepsakeLocation)) ||
                    (t.KeepsakeRecoveryAt >= 0 && (t.KeepsakeRecoveryAt <= snapshot.Tick ||
                        t.KeepsakeRecoveryAt > snapshot.Tick + ModeDefinition.KeepsakeRecoverySeconds * ModeDefinition.TickRate)) ||
                    (t.KeepsakeCarrier != null && snapshot.Players.Any(p => p.Id == t.KeepsakeCarrier && (!p.Connected || p.Eliminated))))
                    throw new ArgumentException("Snapshot keepsake state is invalid.", nameof(snapshot));
                if (!Mode.IsRace)
                {
                    if (t.Lap != 0 || t.NextCheckpoint != 0 || t.OrderedObjectives != 0 || t.ProgressTick != 0 ||
                        t.SafeCheckpoint != "spawn" || t.KeepsakeCarrier != null || t.KeepsakeLocation != "" ||
                        t.KeepsakeRecoveryAt != -1 || t.RecoveryGeneration != 0 || t.Finished || t.CompletedPuzzles.Count != 0)
                        throw new ArgumentException("Non-race snapshot contains race progression.", nameof(snapshot));
                    continue;
                }
                int expectedObjectives = laps.Take(t.Lap).Sum(l => l.Checkpoints.Count + 1) + t.NextCheckpoint;
                string expectedSafe = t.Finished ? laps[laps.Count - 1].Finish :
                    t.NextCheckpoint == 0 ? laps[t.Lap].Start : laps[t.Lap].Checkpoints[t.NextCheckpoint - 1].Id;
                var requiredPuzzles = t.Finished ? Array.Empty<string>() :
                    laps[t.Lap].Checkpoints.Take(t.NextCheckpoint).SelectMany(c => c.Puzzles).ToArray();
                var allowedPuzzles = t.Finished ? Array.Empty<string>() :
                    laps[t.Lap].Checkpoints.Take(Math.Min(t.NextCheckpoint + 1, laps[t.Lap].Checkpoints.Count)).SelectMany(c => c.Puzzles).ToArray();
                var keepsakeLocations = laps.Take(t.Lap).SelectMany(l => new[] { l.Start, l.Finish }.Concat(l.Checkpoints.Select(c => c.Id)))
                    .Concat(t.Finished ? Array.Empty<string>() : new[] { laps[t.Lap].Start }.Concat(laps[t.Lap].Checkpoints.Take(t.NextCheckpoint).Select(c => c.Id)));
                if (t.Finished && t.NextCheckpoint != 0 || t.OrderedObjectives != expectedObjectives || t.SafeCheckpoint != expectedSafe ||
                    requiredPuzzles.Any(id => !t.HasPuzzle(id)) || t.CompletedPuzzles.Any(id => !allowedPuzzles.Contains(id)) ||
                    (t.OrderedObjectives == 0 ? t.ProgressTick != 0 : t.ProgressTick == 0) ||
                    (t.KeepsakeCarrier == null && t.KeepsakeRecoveryAt == -1 && !keepsakeLocations.Contains(t.KeepsakeLocation)))
                    throw new ArgumentException("Snapshot ordered objectives or puzzle prefix is invalid.", nameof(snapshot));
            }
            bool expired = Mode.TimeLimitSeconds > 0 && snapshot.ElapsedTicks >= (long)Mode.TimeLimitSeconds * ModeDefinition.TickRate;
            var finishers = snapshot.Teams.Where(t => t.Finished).Select(t => t.Team).ToArray();
            var scorers = Mode.Objective == WordboundObjective.Knockouts ?
                snapshot.Players.Where(p => p.Knockouts >= Mode.KnockoutTarget).Select(p => p.Team).ToArray() : Array.Empty<int>();
            if (snapshot.Result == null)
            {
                if (expired || finishers.Length != 0 || scorers.Length != 0)
                    throw new ArgumentException("Snapshot omits a completed match result.", nameof(snapshot));
            }
            else
            {
                var r = snapshot.Result;
                int[] winners = finishers.Length != 0 ? finishers : scorers;
                string outcome;
                if (winners.Length == 0 && expired && Mode.Id == "TimeRun") outcome = "TimeExpired";
                else
                {
                    if (winners.Length == 0 && expired && Mode.IsRace)
                    {
                        int best = snapshot.Teams.Max(t => t.OrderedObjectives);
                        long earliest = snapshot.Teams.Where(t => t.OrderedObjectives == best).Min(t => t.ProgressTick);
                        winners = snapshot.Teams.Where(t => t.OrderedObjectives == best && t.ProgressTick == earliest).Select(t => t.Team).ToArray();
                    }
                    else if (winners.Length == 0 && expired && Mode.Objective == WordboundObjective.Knockouts)
                    {
                        int best = snapshot.Players.Max(p => p.Knockouts);
                        winners = snapshot.Players.Where(p => p.Knockouts == best).Select(p => p.Team).ToArray();
                    }
                    outcome = winners.Length == 1 ? "Win" : "Draw";
                    if (winners.Length == 0) throw new ArgumentException("Snapshot declares an unsupported match result.", nameof(snapshot));
                }
                if (r.Tick != snapshot.Tick || r.Outcome != outcome ||
                    !r.WinningTeams.SequenceEqual(winners.OrderBy(t => t)) || r.WinningTeams.Distinct().Count() != r.WinningTeams.Count)
                    throw new ArgumentException("Snapshot result disagrees with authoritative progression.", nameof(snapshot));
            }
            players.Clear(); teams.Clear();
            foreach (var p in snapshot.Players) players.Add(p.Id, p.Copy());
            foreach (var t in snapshot.Teams) teams.Add(t.Team, t.Copy());
            Tick = snapshot.Tick; ElapsedTicks = snapshot.ElapsedTicks; WallClockMs = snapshot.WallClockMs;
            Migrating = snapshot.Migrating; Result = snapshot.Result?.Copy();
        }

        internal void ConfirmResult(long acknowledgedTick)
        {
            if (Result != null && acknowledgedTick >= Result.Tick) Result.Confirmed = true;
        }

        bool CanAct(string playerId, out PlayerProgress player)
        {
            player = null;
            return !Migrating && Result == null && playerId != null && players.TryGetValue(playerId, out player) &&
                player.Connected && !player.Eliminated;
        }

        bool ValidRaceEvent(ProgressEvent e, out TeamProgress team)
        {
            team = null;
            if (!Mode.IsRace || !CanAct(e.PlayerId, out var player)) return false;
            team = teams[player.Team];
            return !team.Finished && e.Lap == team.Lap && team.Lap < laps.Count;
        }

        void ApplyPuzzle(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return;
            var lap = laps[team.Lap];
            if (team.NextCheckpoint >= lap.Checkpoints.Count) return;
            if (lap.Checkpoints[team.NextCheckpoint].Puzzles.Contains(e.Target)) team.AddPuzzle(e.Target);
        }

        void ApplyCheckpoint(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return;
            var lap = laps[team.Lap];
            if (team.NextCheckpoint >= lap.Checkpoints.Count) return;
            var checkpoint = lap.Checkpoints[team.NextCheckpoint];
            if (checkpoint.Id != e.Target || checkpoint.Puzzles.Any(p => !team.HasPuzzle(p))) return;
            team.NextCheckpoint++; team.OrderedObjectives++; team.ProgressTick = Tick;
            team.SafeCheckpoint = checkpoint.Id;
        }

        bool ApplyFinish(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return false;
            var lap = laps[team.Lap];
            if (e.Target != lap.Finish || team.NextCheckpoint != lap.Checkpoints.Count || team.KeepsakeCarrier != e.PlayerId) return false;
            team.Lap++; team.NextCheckpoint = 0; team.ClearPuzzles();
            team.OrderedObjectives++; team.ProgressTick = Tick;
            team.Finished = team.Lap == Mode.Laps;
            team.SafeCheckpoint = team.Finished ? lap.Finish : laps[team.Lap].Start;
            return team.Finished;
        }

        void ApplyKnockout(ProgressEvent e, HashSet<string> knockedOut, HashSet<string> eligibleAttackers)
        {
            if (!Mode.EnemyDamage || !players.TryGetValue(e.PlayerId ?? "", out var attacker) ||
                !players.TryGetValue(e.Target ?? "", out var victim) || !eligibleAttackers.Contains(attacker.Id) || !victim.Connected ||
                victim.Eliminated || victim.ProtectedUntil > Tick || !Mode.CanDamage(attacker.Team, victim.Team) ||
                !knockedOut.Add(victim.Id)) return;
            if (Mode.Objective == WordboundObjective.Knockouts) attacker.Knockouts++;
            DropKeepsake(victim.Id);
            victim.Eliminated = true; victim.RespawnAt = Tick + 2 * ModeDefinition.TickRate;
        }

        void RecoverKeepsake(TeamProgress team)
        {
            team.KeepsakeCarrier = null; team.KeepsakeLocation = team.SafeCheckpoint;
            team.KeepsakeRecoveryAt = -1; team.RecoveryGeneration++;
        }

        void Timeout()
        {
            if (Mode.Id == "TimeRun") { Result = new MatchResult("TimeExpired", Array.Empty<int>(), Tick); return; }
            if (Mode.IsRace)
            {
                int best = teams.Values.Max(t => t.OrderedObjectives);
                long earliest = teams.Values.Where(t => t.OrderedObjectives == best).Min(t => t.ProgressTick);
                var winners = teams.Values.Where(t => t.OrderedObjectives == best && t.ProgressTick == earliest).Select(t => t.Team).ToArray();
                Result = new MatchResult(winners.Length == 1 ? "Win" : "Draw", winners, Tick);
            }
            else if (Mode.Objective == WordboundObjective.Knockouts)
            {
                int best = players.Values.Max(p => p.Knockouts);
                var winners = players.Values.Where(p => p.Knockouts == best).Select(p => p.Team).ToArray();
                Result = new MatchResult(winners.Length == 1 ? "Win" : "Draw", winners, Tick);
            }
        }

        void UpdateWallClock(long now)
        {
            if (now < WallClockMs || now > long.MaxValue - 120000) throw new ArgumentOutOfRangeException(nameof(now));
            WallClockMs = now;
        }

        static string DefinitionHash(ModeDefinition mode, WordboundSeat[] seats, RaceLap[] route)
        {
            var fields = new List<string> { mode.Hash, seats.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            foreach (var seat in seats.OrderBy(s => s.PlayerId, StringComparer.Ordinal))
            {
                fields.Add(seat.PlayerId);
                fields.Add(seat.Team.ToString(System.Globalization.CultureInfo.InvariantCulture));
                fields.Add(seat.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            fields.Add(route.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var lap in route)
            {
                fields.Add(lap.Start);
                fields.Add(lap.Finish);
                fields.Add(lap.Checkpoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var checkpoint in lap.Checkpoints)
                {
                    fields.Add(checkpoint.Id);
                    fields.Add(checkpoint.Puzzles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    fields.AddRange(checkpoint.Puzzles);
                }
            }
            return WordboundIdentity.Sha256(string.Concat(fields.Select(field =>
                field.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + field)));
        }
    }
}
