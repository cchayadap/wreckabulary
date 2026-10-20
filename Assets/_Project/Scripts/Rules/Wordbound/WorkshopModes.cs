using System;
using System.Collections.Generic;
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
            var definition = ModeDefinition.For(mode, format);
            ModeId = mode; Format = format;
            Requirements = MatchStateValidation.Requirements(definition);
            Label = mode == "TimeRun" ? "TimeRun" : mode == "Freestyle" ? "Freestyle" :
                mode == "FreeForAll" ? "Free for all" : (mode == "TeamRace" ? "Team race" : "Brawl race") + " · " + format;
        }
    }

    public static class WorkshopModes
    {
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
            var expected = MatchStateValidation.Requirements(ModeDefinition.For(play.ModeId));
            return play.Capability == expected.Capability && play.Ownership == expected.Ownership &&
                play.TeamCount == expected.TeamCount && play.TeamCapacity == expected.TeamCapacity &&
                play.MaxOccupants == expected.MaxOccupants;
        }

        public static void EnableFreestyleSandbox(WorkshopDraft draft)
        {
            if (draft?.Course == null) throw new ArgumentNullException(nameof(draft));
            if (!draft.Course.SupportedModes.Contains("Freestyle")) draft.Course.SupportedModes.Add("Freestyle");
        }
    }
}
