using System;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundWorkshopOnlineContent
    {
        public static WordboundClientContent Create(WorkshopSnapshot draft, ResolvedCourse resolved,
            CourseCatalogue geometry, CoursePlayRequirements selected, string itemJson, string ruleJson)
        {
            if (draft == null || resolved == null || geometry == null || selected == null)
                throw new ArgumentException("Preview the current map before hosting online.");
            if (resolved.Seed != draft.Seed || resolved.SourceHash != CourseJson.Sha256(draft.CourseJsonText))
                throw new ArgumentException("The map changed. Preview it again before hosting online.");
            var choice = WorkshopModes.Choices(draft.Course()).SingleOrDefault(candidate =>
                candidate.ModeId == selected.ModeId && candidate.Requirements.Capability == selected.Capability &&
                candidate.Requirements.TeamCount == selected.TeamCount && candidate.Requirements.TeamCapacity == selected.TeamCapacity &&
                candidate.Requirements.MaxOccupants == selected.MaxOccupants && candidate.Requirements.Ownership == selected.Ownership);
            if (choice == null || !CourseValidator.ValidateResolved(resolved, geometry, choice.Requirements).Ok)
                throw new ArgumentException("Preview the current map with the selected mode and team size before hosting online.");
            return WordboundClientContent.FromAuthored(draft.CourseJsonText, CourseJson.ExportManifest(resolved),
                geometry, itemJson, ruleJson, choice.ModeId, choice.Format.ToString(), draft.Seed);
        }
    }
}
