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

    public sealed class WorkshopRoomResizeDrag
    {
        readonly int revision;
        readonly CourseModule original;
        readonly CourseCatalogue catalogue;
        readonly CourseBox bounds;
        public string ModuleId => original.Id;
        public int SideX { get; }
        public int SideZ { get; }
        public string DefinitionId { get; private set; }
        public CoursePoint Position { get; private set; }
        public CourseBox Preview => catalogue.Bounds(new CourseModule { DefinitionId = DefinitionId, Position = Position, Yaw = original.Yaw });
        public bool Changed => DefinitionId != original.DefinitionId;

        public WorkshopRoomResizeDrag(WorkshopHistory history, CourseCatalogue catalogue, string moduleId, int sideX, int sideZ)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            this.catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            if (sideX < -1 || sideX > 1 || sideZ < -1 || sideZ > 1 || sideX == 0 && sideZ == 0)
                throw new ArgumentException("Drag an edge or corner of the room.");
            original = history.Current.Course().Modules.Single(module => module.Id == moduleId);
            if (history.Current.LockedLayers.Contains(original.LayerId)) throw new InvalidOperationException("Unlock the selected floor first.");
            if (!catalogue.Modules.ContainsKey(original.DefinitionId)) throw new InvalidOperationException("This room has no approved size.");
            revision = history.Revision; SideX = sideX; SideZ = sideZ;
            bounds = catalogue.Bounds(original); DefinitionId = original.DefinitionId; Position = original.Position;
        }

        public void Drag(int x, int z)
        {
            long width = SideX > 0 ? (long)x - bounds.Min.X : SideX < 0 ? (long)bounds.Max.X - x : (long)bounds.Max.X - bounds.Min.X;
            long depth = SideZ > 0 ? (long)z - bounds.Min.Z : SideZ < 0 ? (long)bounds.Max.Z - z : (long)bounds.Max.Z - bounds.Min.Z;
            DefinitionId = WorkshopCommands.FitDefinition(catalogue, original,
                (int)Math.Max(0, Math.Min(int.MaxValue, width)), (int)Math.Max(0, Math.Min(int.MaxValue, depth)));
            Position = WorkshopCommands.ResizedPosition(catalogue, original, DefinitionId, SideX, SideZ);
        }

        public bool Commit(WorkshopHistory history, CoursePlayRequirements play)
        {
            if (history.Revision != revision) throw new InvalidOperationException("The draft changed. Start the drag again.");
            if (!Changed) return false;
            return history.Execute("Resize room", draft =>
                WorkshopCommands.ResizeModule(draft, catalogue, play, ModuleId, DefinitionId, Position));
        }
    }

    public sealed class WorkshopPlacementDrag
    {
        readonly int revision, grid, originalYaw;
        readonly CoursePoint original;
        public string ZoneId { get; }
        public CoursePoint Position { get; private set; }
        public int Yaw { get; private set; }
        public WorkshopPlacementDrag(WorkshopHistory history, string zoneId)
        {
            var course = history.Current.Course();
            var zone = course.Zones.Single(item => item.Id == zoneId && item.Placement == "Fixed");
            var module = course.Modules.Single(item => item.Id == zone.ModuleId);
            if (history.Current.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the selected floor first.");
            revision = history.Revision; grid = history.Current.GridMm; ZoneId = zoneId;
            original = Position = zone.FixedPosition; originalYaw = Yaw = zone.FixedYaw;
        }
        public void Move(int x, int z) => Position = new CoursePoint(
            checked(original.X + WorkshopCommands.Snap(x, grid)), original.Y, checked(original.Z + WorkshopCommands.Snap(z, grid)));
        public void MoveTo(int x, int z) => Position = new CoursePoint(x, original.Y, z);
        public void Rotate(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) throw new ArgumentException("Use a finite rotation angle.");
            int turns = checked((int)Math.Round(degrees / 90, MidpointRounding.AwayFromZero));
            Yaw = ((originalYaw + turns % 4 * 90) % 360 + 360) % 360;
        }
        public bool Commit(WorkshopHistory history, CourseCatalogue catalogue)
        {
            if (history.Revision != revision) throw new InvalidOperationException("The draft changed. Start the drag again.");
            return history.Execute("Move object", draft =>
            {
                var zone = draft.Course.Zones.Single(item => item.Id == ZoneId);
                zone.FixedPosition = Position; zone.FixedYaw = Yaw; zone.Yaws = new List<int> { Yaw };
                WorkshopCommands.SetZone(draft, zone, catalogue);
            });
        }
    }
}
