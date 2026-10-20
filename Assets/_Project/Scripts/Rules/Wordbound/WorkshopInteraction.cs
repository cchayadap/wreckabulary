using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WorkshopHandle { Plane, X, Z, Rotate }

    public sealed class WorkshopTransformDrag
    {
        readonly int revision;
        readonly CoursePoint pivot;
        readonly WorkshopHandle handle;
        readonly int grid;
        public CoursePoint Delta { get; private set; }
        public int QuarterTurns { get; private set; }
        public IReadOnlyList<string> Selection { get; }

        public WorkshopTransformDrag(WorkshopHistory history, WorkshopHandle handle, CoursePoint pivot)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (history.Current.Selection.Count == 0) throw new InvalidOperationException("Select a module first.");
            var course = history.Current.Course();
            if (course.Modules.Any(module => history.Current.Selection.Contains(module.Id) && history.Current.LockedLayers.Contains(module.LayerId)))
                throw new InvalidOperationException("Unlock the selected floor first.");
            revision = history.Revision; grid = history.Current.GridMm; this.handle = handle; this.pivot = pivot;
            Selection = Array.AsReadOnly(history.Current.Selection.ToArray());
        }

        public void Move(int x, int z)
        {
            if (handle == WorkshopHandle.Rotate) throw new InvalidOperationException("Use the rotation handle.");
            Delta = new CoursePoint(handle == WorkshopHandle.Z ? 0 : WorkshopCommands.Snap(x, grid), 0,
                handle == WorkshopHandle.X ? 0 : WorkshopCommands.Snap(z, grid));
        }

        public void Rotate(double degrees)
        {
            if (handle != WorkshopHandle.Rotate || double.IsNaN(degrees) || double.IsInfinity(degrees))
                throw new ArgumentException("Use a finite rotation angle.");
            QuarterTurns = checked((int)Math.Round(degrees / 90, MidpointRounding.AwayFromZero));
        }

        public bool Commit(WorkshopHistory history)
        {
            if (history.Revision != revision) throw new InvalidOperationException("The draft changed. Start the drag again.");
            return history.Execute(handle == WorkshopHandle.Rotate ? "Rotate" : "Move", draft =>
            {
                if (handle == WorkshopHandle.Rotate) WorkshopCommands.RotateSelection(draft, pivot, QuarterTurns);
                else WorkshopCommands.Move(draft, Delta, false);
            });
        }
    }

    public static class WorkshopSelection
    {
        public static string Step(WorkshopSnapshot snapshot, int direction)
        {
            var modules = snapshot.Course().Modules.Where(module => module.LayerId == snapshot.ActiveLayer &&
                !snapshot.HiddenLayers.Contains(module.LayerId) && !snapshot.LockedLayers.Contains(module.LayerId))
                .Select(module => module.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (modules.Length == 0) return null;
            int index = Array.FindIndex(modules, id => snapshot.Selection.Contains(id));
            return modules[(index < 0 ? direction < 0 ? modules.Length - 1 : 0 :
                (index + (direction < 0 ? -1 : 1) + modules.Length) % modules.Length)];
        }
    }
}
