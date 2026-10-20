using System;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WorkshopTemplate { RaceCircuit, CombatArena, FreestyleWorld }

    public sealed class WorkshopTemplatePreset
    {
        public WorkshopSnapshot Snapshot { get; }
        public CourseCatalogue Catalogue { get; }
        public CoursePlayRequirements Play { get; }
        internal WorkshopTemplatePreset(CourseDocument course, CoursePlayRequirements play)
        {
            Snapshot = new WorkshopSnapshot(course); Catalogue = CourseTemplates.ProofCatalogue(); Play = play;
        }
    }
    public static class WorkshopTemplates
    {
        public static WorkshopTemplatePreset Create(WorkshopTemplate template)
        {
            switch (template)
            {
                case WorkshopTemplate.RaceCircuit:
                    return new WorkshopTemplatePreset(CourseTemplates.RaceCircuit(),
                        new CoursePlayRequirements("TeamRace", CourseCapability.Circuit, 2, 1, 2, CourseOwnershipPolicy.TeamIsolated));
                case WorkshopTemplate.CombatArena:
                    return new WorkshopTemplatePreset(CourseTemplates.CombatArena(),
                        new CoursePlayRequirements("FreeForAll", CourseCapability.Arena, 0, 1, 8, CourseOwnershipPolicy.Individual));
                case WorkshopTemplate.FreestyleWorld:
                    return new WorkshopTemplatePreset(CourseTemplates.FreestyleWorld(),
                        new CoursePlayRequirements("Freestyle", CourseCapability.Explore, 0, 0, 8, CourseOwnershipPolicy.Shared));
                default: throw new ArgumentOutOfRangeException(nameof(template));
            }
        }
    }
}
