using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundDirectorTeam
    {
        public int Team { get; }
        public int Objectives { get; }
        public IReadOnlyList<long> ObjectiveTicks { get; }
        public bool Finished { get; }
        public int TrailBias { get; }
        public int LeadBias { get; }
        public int ExpressUsed { get; }
        public int InspectionUsed { get; }

        public WordboundDirectorTeam(int team, int objectives, IEnumerable<long> objectiveTicks, bool finished,
            int trailBias = 0, int leadBias = 0, int expressUsed = 0, int inspectionUsed = 0)
        {
            var ticks = (objectiveTicks ?? Enumerable.Empty<long>()).ToArray();
            if (team < 0 || objectives < 0 || trailBias < 0 || leadBias < 0 || expressUsed < 0 || inspectionUsed < 0)
                throw new ArgumentOutOfRangeException(nameof(team), "Director inputs cannot be negative.");
            if (ticks.Length != Math.Min(objectives, WordboundRaceDirector.RecentObjectives) ||
                ticks.Any(t => t < 0) || ticks.Zip(ticks.Skip(1), (a, b) => a <= b).Any(ordered => !ordered))
                throw new ArgumentException("Keep the last four objective ticks in order.", nameof(objectiveTicks));
            Team = team; Objectives = objectives; ObjectiveTicks = Array.AsReadOnly(ticks); Finished = finished;
            TrailBias = trailBias; LeadBias = leadBias; ExpressUsed = expressUsed; InspectionUsed = inspectionUsed;
        }
    }

    public sealed class WordboundDirectorBias
    {
        public int Team { get; }
        public long EtaTick { get; }
        public int TrailBias { get; }
        public int LeadBias { get; }

        public WordboundDirectorBias(int team, long etaTick, int trailBias, int leadBias)
        { Team = team; EtaTick = etaTick; TrailBias = trailBias; LeadBias = leadBias; }
    }

    public sealed class WordboundDirectorPlan
    {
        public static WordboundDirectorPlan Off { get; } = new WordboundDirectorPlan(false, -1, -1, 0, 0, 0,
            Array.Empty<WordboundDirectorBias>());

        public bool Active { get; }
        public int TrailTeam { get; }
        public int LeadTeam { get; }
        public long GapTicks { get; }
        public int ExpressPermille { get; }
        public int InspectionPermille { get; }
        public IReadOnlyList<WordboundDirectorBias> Teams { get; }

        public WordboundDirectorPlan(bool active, int trailTeam, int leadTeam, long gapTicks, int expressPermille,
            int inspectionPermille, IEnumerable<WordboundDirectorBias> teams)
        {
            Active = active; TrailTeam = trailTeam; LeadTeam = leadTeam; GapTicks = gapTicks;
            ExpressPermille = expressPermille; InspectionPermille = inspectionPermille;
            Teams = Array.AsReadOnly((teams ?? Enumerable.Empty<WordboundDirectorBias>()).ToArray());
        }
    }

    public static class WordboundRaceDirector
    {
        public const int Version = 1;
        public const int WarmupTicks = 7200;
        public const int FinalMinuteTicks = 3600;
        public const int RecentObjectives = 4;
        public const int RecentWeight = 3;
        public const int TrailGapTicks = 1200;
        public const int TrailRampTicks = 6000;
        public const int TrailMaximumPermille = 150;
        public const int LeadGapTicks = 1800;
        public const int LeadRampTicks = 5400;
        public const int LeadMaximumPermille = 100;
        public const int SlewPermille = 50;
        public const int ExpressPerMatch = 4;
        public const int InspectionPerMatch = 3;
        public const int InspectionHoldTicks = 300;

        internal static string Canonical { get; } = string.Join("|", new object[] { "wordbound.director", Version,
            WarmupTicks, FinalMinuteTicks, RecentObjectives, RecentWeight, TrailGapTicks, TrailRampTicks,
            TrailMaximumPermille, LeadGapTicks, LeadRampTicks, LeadMaximumPermille, SlewPermille, ExpressPerMatch,
            InspectionPerMatch, InspectionHoldTicks }.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)));

        public static int TotalObjectives(IEnumerable<int> checkpointsPerLap)
        {
            var laps = (checkpointsPerLap ?? throw new ArgumentNullException(nameof(checkpointsPerLap))).ToArray();
            if (laps.Length == 0 || laps.Any(c => c < 0)) throw new ArgumentException("Every lap needs a checkpoint count.");
            return laps.Sum(c => c + 1);
        }

        public static long Eta(long tick, int totalObjectives, long timeLimitTicks, WordboundDirectorTeam team)
        {
            if (team == null) throw new ArgumentNullException(nameof(team));
            if (tick < 0 || totalObjectives <= 0 || timeLimitTicks <= 0 || team.Objectives > totalObjectives)
                throw new ArgumentOutOfRangeException(nameof(totalObjectives));
            if (team.ObjectiveTicks.Any(t => t > tick))
                throw new ArgumentOutOfRangeException(nameof(tick), "Objective ticks cannot be after the draw tick.");
            int done = team.Objectives;
            long planned = timeLimitTicks / totalObjectives;
            long overall = done >= 1 ? tick / done : planned;
            long recent = done >= RecentObjectives ? (tick - team.ObjectiveTicks[0]) / RecentObjectives : overall;
            long pace = (RecentWeight * recent + overall) / (RecentWeight + 1);
            return tick + (totalObjectives - done) * pace;
        }

        public static int TrailTarget(long gapTicks) =>
            (int)Math.Max(0, Math.Min(TrailMaximumPermille, (gapTicks - TrailGapTicks) * TrailMaximumPermille / TrailRampTicks));

        public static int LeadTarget(long gapTicks) =>
            (int)Math.Max(0, Math.Min(LeadMaximumPermille, (gapTicks - LeadGapTicks) * LeadMaximumPermille / LeadRampTicks));

        public static int Slew(int previous, int target) =>
            previous + Math.Max(-SlewPermille, Math.Min(SlewPermille, target - previous));

        public static bool Enabled(ModeDefinition mode) =>
            mode != null && mode.Ruleset.DirectorFor(mode.Id) && mode.TimeLimitSeconds > 0;

        public static bool Active(ModeDefinition mode, long drawTick, IReadOnlyList<WordboundDirectorTeam> teams)
        {
            if (!Enabled(mode) || teams == null || teams.Count != 2 || teams.Any(t => t == null || t.Finished)) return false;
            long limit = (long)mode.TimeLimitSeconds * ModeDefinition.TickRate;
            return drawTick >= WarmupTicks && drawTick + mode.Ruleset.Deck.LeadTicks <= limit - FinalMinuteTicks;
        }

        public static WordboundDirectorPlan Plan(ModeDefinition mode, long drawTick, int totalObjectives,
            IReadOnlyList<WordboundDirectorTeam> teams)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            if (teams == null || teams.Any(t => t == null)) throw new ArgumentNullException(nameof(teams));
            if (teams.Select(t => t.Team).Distinct().Count() != teams.Count)
                throw new ArgumentException("Director teams must be unique.", nameof(teams));
            var ordered = teams.OrderBy(t => t.Team).ToArray();
            if (!Enabled(mode))
                return new WordboundDirectorPlan(false, -1, -1, 0, 0, 0,
                    ordered.Select(t => new WordboundDirectorBias(t.Team, 0, 0, 0)));
            long limit = (long)mode.TimeLimitSeconds * ModeDefinition.TickRate;
            var eta = ordered.Select(t => Eta(drawTick, totalObjectives, limit, t)).ToArray();
            bool active = Active(mode, drawTick, ordered);
            int trailer = -1, leader = -1;
            long gap = 0;
            if (active && eta[0] != eta[1])
            {
                trailer = eta[0] > eta[1] ? 0 : 1;
                leader = 1 - trailer;
                gap = eta[trailer] - eta[leader];
            }
            var biases = ordered.Select((t, i) => new WordboundDirectorBias(t.Team, eta[i],
                Slew(t.TrailBias, i == trailer ? TrailTarget(gap) : 0),
                Slew(t.LeadBias, i == leader ? LeadTarget(gap) : 0))).ToArray();
            var deck = mode.Ruleset.Deck;
            int express = trailer >= 0 && ordered[trailer].ExpressUsed < deck.ExpressPerMatch &&
                ordered[trailer].Objectives < totalObjectives - 1 ? biases[trailer].TrailBias : 0;
            int inspection = leader >= 0 && ordered[leader].InspectionUsed < deck.InspectionPerMatch &&
                ordered[leader].Objectives < totalObjectives - 1 ? biases[leader].LeadBias : 0;
            return new WordboundDirectorPlan(active, trailer >= 0 ? ordered[trailer].Team : -1,
                leader >= 0 ? ordered[leader].Team : -1, gap, express, inspection, biases);
        }
    }
}
