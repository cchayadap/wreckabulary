using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundCourseEditing
    {
        public static CourseDocument MoveModule(CourseDocument source, string moduleId, CoursePoint destination)
        {
            var draft = Draft(source, moduleId);
            var module = draft.Course.Modules.Single(m => m.Id == moduleId);
            if (destination.Y != module.Position.Y)
                throw new ArgumentException("Use the layer selector to change a module's floor.");
            WorkshopCommands.Move(draft, new CoursePoint(checked(destination.X - module.Position.X), 0,
                checked(destination.Z - module.Position.Z)), false);
            return draft.Course;
        }
        public static CourseDocument RotateModule(CourseDocument source, string moduleId, int quarterTurns)
        {
            var draft = Draft(source, moduleId);
            WorkshopCommands.RotateSelection(draft, draft.Course.Modules.Single(m => m.Id == moduleId).Position, quarterTurns);
            return draft.Course;
        }
        public static CourseDocument ChangeLayer(CourseDocument source, string moduleId, string layerId)
        {
            var draft = Draft(source, moduleId);
            WorkshopCommands.ChangeLayer(draft, layerId);
            return draft.Course;
        }
        static WorkshopDraft Draft(CourseDocument source, string moduleId)
        {
            var draft = new WorkshopSnapshot(source).Edit();
            if (!draft.Course.Modules.Any(m => m.Id == moduleId)) throw new ArgumentException("Select an existing module.");
            draft.Selection = new List<string> { moduleId };
            return draft;
        }
    }
}
