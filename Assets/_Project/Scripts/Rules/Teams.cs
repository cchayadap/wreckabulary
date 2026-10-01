using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public static class Teams
    {
        public const int NoTeam = -1;

        public static bool AreTeammates(int a, int b) => a != NoTeam && a == b;

        public static bool AreHostile(int a, int b) => !AreTeammates(a, b);

        /// <summary>
        /// Team per seat, in join order. Free-for-all (team size 1) gives everyone their own
        /// team; Duos with 4 players gives 0, 1, 0, 1 so friends who joined together are split
        /// unless the lobby swaps them.
        /// </summary>
        public static int[] Assign(int players, int teamSize)
        {
            if (players < 0) throw new ArgumentOutOfRangeException(nameof(players));
            if (teamSize < 1) throw new ArgumentOutOfRangeException(nameof(teamSize));
            var teams = new int[players];
            if (teamSize == 1)
            {
                for (int i = 0; i < players; i++) teams[i] = i;
                return teams;
            }
            int teamCount = Math.Max(1, (players + teamSize - 1) / teamSize);
            for (int i = 0; i < players; i++) teams[i] = i % teamCount;
            return teams;
        }
    }

    public enum RoundState { Ongoing, Won, Draw }

    public readonly struct RoundOutcome
    {
        public readonly RoundState State;
        public readonly int WinningTeam;

        public RoundOutcome(RoundState state, int team)
        {
            State = state;
            WinningTeam = team;
        }

        public override string ToString() => State == RoundState.Won ? $"team {WinningTeam} wins" : State.ToString();
    }

    public readonly struct Combatant
    {
        public readonly int PlayerId;
        public readonly int Team;
        public readonly LifeState Life;

        public Combatant(int playerId, int team, LifeState life)
        {
            PlayerId = playerId;
            Team = team;
            Life = life;
        }
    }

    /// <summary>
    /// Last player (or team) standing. Evaluate once per tick, after every hit of that tick
    /// has been applied, so two knockouts on the same tick are a draw rather than a win for
    /// whichever event ran first.
    /// </summary>
    public static class WinCheck
    {
        public static RoundOutcome Evaluate(IEnumerable<Combatant> combatants)
        {
            var standing = combatants.Where(c => c.Life == LifeState.Alive).Select(c => c.Team).Distinct().ToList();
            if (standing.Count >= 2) return new RoundOutcome(RoundState.Ongoing, Teams.NoTeam);
            if (standing.Count == 1) return new RoundOutcome(RoundState.Won, standing[0]);
            return new RoundOutcome(RoundState.Draw, Teams.NoTeam);
        }

        /// <summary>Downed players whose whole team is down can't be revived, so they are out.</summary>
        public static List<int> Unrevivable(IEnumerable<Combatant> combatants)
        {
            var list = combatants.ToList();
            var teamsWithSomeoneUp = new HashSet<int>(list.Where(c => c.Life == LifeState.Alive).Select(c => c.Team));
            return list.Where(c => c.Life == LifeState.Downed && !teamsWithSomeoneUp.Contains(c.Team)).Select(c => c.PlayerId).ToList();
        }
    }

    /// <summary>Rounds won per team; the first to <see cref="RoundsToWin"/> takes the match.</summary>
    public sealed class MatchScore
    {
        readonly Dictionary<int, int> wins = new Dictionary<int, int>();
        public int RoundsToWin { get; }
        public int RoundsPlayed { get; private set; }
        public int MatchWinner { get; private set; } = Teams.NoTeam;

        public MatchScore(int roundsToWin)
        {
            if (roundsToWin < 1) throw new ArgumentOutOfRangeException(nameof(roundsToWin));
            RoundsToWin = roundsToWin;
        }

        public int Wins(int team) => wins.TryGetValue(team, out int w) ? w : 0;
        public bool IsOver => MatchWinner != Teams.NoTeam;

        /// <summary>Records a finished round. A draw counts as played but gives nobody a win.</summary>
        public void Record(RoundOutcome outcome)
        {
            if (IsOver) throw new InvalidOperationException("The match is already over.");
            if (outcome.State == RoundState.Ongoing) throw new ArgumentException("The round hasn't finished.");
            RoundsPlayed++;
            if (outcome.State != RoundState.Won) return;
            int w = Wins(outcome.WinningTeam) + 1;
            wins[outcome.WinningTeam] = w;
            if (w >= RoundsToWin) MatchWinner = outcome.WinningTeam;
        }
    }
}
