using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WorkshopModeChoice
    {
        public string ModeId { get; }
        public RaceFormat Format { get; }
        public string Label { get; }
        public CoursePlayRequirements Requirements { get; }
        public bool AllowsOneHumanLocal => ModeId == "TimeRun" || ModeId == "Freestyle";
        internal WorkshopModeChoice(string mode, RaceFormat format)
        {
            var definition = WorkshopModes.Definition(mode, format);
            ModeId = mode; Format = format;
            Requirements = MatchStateValidation.Requirements(definition);
            Label = mode == "TimeRun" ? "TimeRun" : mode == "Freestyle" ? "Freestyle" :
                mode == "FreeForAll" ? "Free for all" : (mode == "TeamRace" ? "Team race" : "Brawl race") + " · " + format;
        }
    }

    public sealed class WorkshopModeSummary
    {
        public string ModeId { get; }
        public string Name { get; }
        public string HowToWin { get; }
        public string Players { get; }
        public bool IsRace { get; }
        public bool HasTeamFormat { get; }
        internal WorkshopModeSummary(ModeDefinition mode)
        {
            ModeId = mode.Id; Name = WorkshopModes.Name(mode.Id); IsRace = mode.IsRace; HasTeamFormat = mode.HasRaceFormat;
            string minutes = mode.TimeLimitSeconds == 0 ? "no time limit" :
                (mode.TimeLimitSeconds / 60d).ToString("0.#", CultureInfo.InvariantCulture) + " min";
            HowToWin = mode.Objective == WordboundObjective.DeliverKeepsake ? "Carry the keepsake through " + mode.Laps + " laps · " + minutes :
                mode.Objective == WordboundObjective.Knockouts ? "First to " + mode.KnockoutTarget + " knockouts · " + minutes :
                "Explore and build freely · " + minutes;
            string players = mode.MinimumPlayers == mode.MaximumPlayers ? mode.MinimumPlayers + " players" :
                mode.MinimumPlayers + "–" + mode.MaximumPlayers + " players";
            string teams = mode.TeamCount == 1 ? "all together" : mode.TeamCount == 2 ? mode.TeamCapacity + " vs " + mode.TeamCapacity :
                mode.TeamCapacity == 1 ? "everyone for themselves" : mode.TeamCount + " teams of " + mode.TeamCapacity;
            Players = players + " · " + teams + (mode.EnemyDamage ? " · knockdowns on" : "");
        }
    }

    public static class WorkshopModes
    {
        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(CourseValidator.ModeIds.ToArray());
        static readonly Dictionary<string, ModeDefinition> definitions = new Dictionary<string, ModeDefinition>(StringComparer.Ordinal);
        static readonly Dictionary<string, WorkshopModeSummary> summaries = new Dictionary<string, WorkshopModeSummary>(StringComparer.Ordinal);

        public static ModeDefinition Definition(string modeId, RaceFormat format = RaceFormat.Solo)
        {
            string key = modeId + "/" + (int)format;
            lock (definitions)
            {
                if (!definitions.TryGetValue(key, out var definition)) definitions[key] = definition = ModeDefinition.For(modeId, format);
                return definition;
            }
        }

        public static IReadOnlyList<WorkshopModeChoice> Choices(CourseDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var choices = new List<WorkshopModeChoice>();
            foreach (string mode in new[] { "TimeRun", "Freestyle", "TeamRace", "BrawlRace", "FreeForAll" })
            {
                if (!document.SupportedModes.Contains(mode)) continue;
                foreach (var format in mode == "TeamRace" || mode == "BrawlRace"
                    ? new[] { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad } : new[] { RaceFormat.Solo })
                    choices.Add(new WorkshopModeChoice(mode, format));
            }
            return choices.AsReadOnly();
        }

        public static WorkshopModeChoice Select(CourseDocument document, string modeId, RaceFormat format)
        {
            var choice = Choices(document).SingleOrDefault(item => item.ModeId == modeId && item.Format == format);
            return choice ?? throw new ArgumentException("Choose a mode and format enabled by this map.");
        }

        public static bool AllowsOneHumanLocal(CoursePlayRequirements play)
        {
            if (play == null || play.ModeId != "TimeRun" && play.ModeId != "Freestyle") return false;
            var expected = MatchStateValidation.Requirements(Definition(play.ModeId));
            return play.Capability == expected.Capability && play.Ownership == expected.Ownership &&
                play.TeamCount == expected.TeamCount && play.TeamCapacity == expected.TeamCapacity &&
                play.MaxOccupants == expected.MaxOccupants;
        }

        public static RaceFormat FormatOf(CoursePlayRequirements play)
        {
            if (play == null || play.ModeId != "TeamRace" && play.ModeId != "BrawlRace") return RaceFormat.Solo;
            var format = (RaceFormat)play.TeamCapacity;
            return Enum.IsDefined(typeof(RaceFormat), format) ? format : RaceFormat.Solo;
        }

        public static string Name(string modeId) => modeId switch
        {
            "TimeRun" => "Time Trial", "TeamRace" => "Team Relay", "BrawlRace" => "Brawl Relay",
            "FreeForAll" => "Free-for-All", "Freestyle" => "Freeplay", _ => modeId ?? ""
        };

        public static WorkshopModeSummary Summary(string modeId, RaceFormat format = RaceFormat.Solo)
        {
            string key = modeId + "/" + (int)format;
            lock (summaries)
            {
                if (!summaries.TryGetValue(key, out var summary)) summaries[key] = summary = new WorkshopModeSummary(Definition(modeId, format));
                return summary;
            }
        }

        public static IReadOnlyList<string> Needs(CourseDocument document, CourseCatalogue catalogue, string modeId,
            RaceFormat format = RaceFormat.Solo, int limit = 3)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var probe = document.Clone();
            if (!probe.SupportedModes.Contains(modeId)) probe.SupportedModes.Add(modeId);
            var play = MatchStateValidation.Requirements(Definition(modeId, format));
            return CourseValidator.Validate(probe, catalogue, play).Issues.Select(issue => issue.Message)
                .Distinct(StringComparer.Ordinal).Take(Math.Max(0, limit)).ToList().AsReadOnly();
        }

        public static void SetSupported(WorkshopDraft draft, string modeId, bool supported)
        {
            if (draft?.Course == null) throw new ArgumentNullException(nameof(draft));
            if (!CourseValidator.ModeIds.Contains(modeId)) throw new ArgumentException("Unknown Wordbound mode.", nameof(modeId));
            var modes = draft.Course.SupportedModes;
            if (supported) { if (!modes.Contains(modeId)) modes.Add(modeId); return; }
            if (!modes.Contains(modeId)) return;
            if (modes.Count == 1) throw new InvalidOperationException("A map needs at least one mode.");
            modes.RemoveAll(mode => mode == modeId);
        }

        public static void EnableFreestyleSandbox(WorkshopDraft draft) => SetSupported(draft, "Freestyle", true);
    }
}
