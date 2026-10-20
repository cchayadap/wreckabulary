using System;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundWorkshopPlaytest
    {
        public static IWordboundPuzzleContent Create(WorkshopSnapshot draft, ResolvedCourse resolved,
            CourseCatalogue geometry, CoursePlayRequirements selected, string itemJson, string ruleJson)
        {
            if (draft == null || resolved == null || geometry == null || selected == null)
                throw new ArgumentException("Preview the current draft before starting a playtest.");
            if (selected.ModeId != "TimeRun" && selected.ModeId != "Freestyle")
                throw new ArgumentException("Select Time Run or Freestyle for a local playtest.");
            if (resolved.Seed != draft.Seed || resolved.SourceHash != CourseJson.Sha256(draft.CourseJsonText))
                throw new ArgumentException("The draft changed. Preview it again before playing.");
            var mode = ModeDefinition.For(selected.ModeId);
            var expected = MatchStateValidation.Requirements(mode);
            if (selected.Capability != expected.Capability || selected.TeamCount != expected.TeamCount ||
                selected.TeamCapacity != expected.TeamCapacity || selected.MaxOccupants != expected.MaxOccupants ||
                selected.Ownership != expected.Ownership ||
                !CourseValidator.ValidateResolved(resolved, geometry, expected).Ok)
                throw new ArgumentException("Preview the draft with the selected local mode before playing.");
            var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(resolved), geometry);
            if (!imported.Success || imported.Manifest.Hash != resolved.Hash)
                throw new ArgumentException("The playtest preview could not be verified.");
            return new WordboundAuthoredPuzzleContent(mode, geometry, resolved, itemJson, ruleJson);
        }
    }
}
