using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class WorkshopCommands
    {
        public static int Snap(int value, int gridMm) => gridMm < 100 || gridMm > 5000
            ? throw new ArgumentOutOfRangeException(nameof(gridMm))
            : checked((int)Math.Round(value / (double)gridMm, MidpointRounding.AwayFromZero) * gridMm);
        static CoursePoint Add(CoursePoint a, CoursePoint b) => new CoursePoint(checked(a.X + b.X), checked(a.Y + b.Y), checked(a.Z + b.Z));
        static CoursePoint Rotate(CoursePoint point, CoursePoint pivot, int turns)
        {
            long x = (long)point.X - pivot.X, z = (long)point.Z - pivot.Z;
            for (int i = 0; i < turns; i++) { long previous = x; x = z; z = -previous; }
            return new CoursePoint(checked((int)(x + pivot.X)), point.Y, checked((int)(z + pivot.Z)));
        }
        static List<CourseModule> Selected(WorkshopDraft draft)
        {
            var modules = draft.Course.Modules.Where(module => draft.Selection.Contains(module.Id)).ToList();
            if (modules.Any(module => draft.LockedLayers.Contains(module.LayerId)))
                throw new InvalidOperationException("Unlock the selected layer before editing.");
            return modules;
        }
        public static string Unique(string prefix, IEnumerable<string> taken)
        {
            var all = new HashSet<string>(taken, StringComparer.Ordinal);
            string stem = new string((prefix ?? "object").Where(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '-')).Take(34).ToArray());
            if (stem.Length == 0) stem = "object";
            for (int i = 1; i < 100000; i++) if (!all.Contains(stem + "_" + i)) return stem + "_" + i;
            throw new InvalidOperationException("No unused object ID.");
        }
        public static void AddModule(WorkshopDraft draft, string definitionId, CoursePoint position)
        {
            var layer = draft.Course.Layers.Single(layer => layer.Id == draft.ActiveLayer);
            if (draft.LockedLayers.Contains(layer.Id)) throw new InvalidOperationException("Unlock the layer first.");
            var module = new CourseModule
            {
                Id = Unique("module", draft.Course.Modules.Select(item => item.Id)), DefinitionId = definitionId,
                LayerId = layer.Id, Position = new CoursePoint(Snap(position.X, draft.GridMm), layer.HeightMm, Snap(position.Z, draft.GridMm))
            };
            draft.Course.Modules.Add(module); draft.Selection = new List<string> { module.Id };
        }
        public static void Move(WorkshopDraft draft, CoursePoint delta, bool snap = true)
        {
            var selected = Selected(draft);
            if (delta.Y != 0) throw new ArgumentException("Use ChangeLayer to move between floors.");
            if (snap) delta = new CoursePoint(Snap(delta.X, draft.GridMm), 0, Snap(delta.Z, draft.GridMm));
            Transform(draft.Course, selected, point => Add(point, delta), 0);
        }
        public static void RotateSelection(WorkshopDraft draft, CoursePoint pivot, int quarterTurns)
        {
            int turns = ((quarterTurns % 4) + 4) % 4;
            Transform(draft.Course, Selected(draft), point => Rotate(point, pivot, turns), turns * 90);
        }
        static void Transform(CourseDocument course, List<CourseModule> modules, Func<CoursePoint, CoursePoint> point, int yaw)
        {
            var ids = new HashSet<string>(modules.Select(module => module.Id), StringComparer.Ordinal);
            foreach (var module in modules) { module.Position = point(module.Position); module.Yaw = (module.Yaw + yaw) % 360; }
            foreach (var supply in course.Supplies.Where(item => ids.Contains(item.ModuleId))) supply.Position = point(supply.Position);
            foreach (var spawn in course.Spawns.Where(item => ids.Contains(item.ModuleId)))
            { spawn.Position = point(spawn.Position); spawn.Yaw = (spawn.Yaw + yaw) % 360; }
            foreach (var recovery in course.RecoveryAnchors.Where(item => ids.Contains(item.ModuleId))) recovery.Position = point(recovery.Position);
            foreach (var connection in course.Connections)
            {
                if (ids.Contains(connection.FromModuleId)) connection.From = point(connection.From);
                if (ids.Contains(connection.ToModuleId)) connection.To = point(connection.To);
            }
            foreach (var zone in course.Zones.Where(item => ids.Contains(item.ModuleId)))
            {
                var a = point(zone.Bounds.Min); var b = point(zone.Bounds.Max);
                zone.Bounds = new CourseBox(new CoursePoint(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)),
                    new CoursePoint(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z)));
                zone.Yaws = zone.Yaws.Select(angle => (angle + yaw) % 360).ToList();
                if (zone.Placement == "Fixed")
                {
                    zone.FixedPosition = point(zone.FixedPosition);
                    zone.FixedYaw = (zone.FixedYaw + yaw) % 360;
                }
            }
        }
        public static void ChangeLayer(WorkshopDraft draft, string layerId)
        {
            var layer = draft.Course.Layers.Single(item => item.Id == layerId);
            if (draft.LockedLayers.Contains(layerId)) throw new InvalidOperationException("Unlock the target layer first.");
            foreach (var module in Selected(draft))
            {
                int delta = checked(layer.HeightMm - module.Position.Y);
                string previous = module.LayerId;
                Transform(draft.Course, new List<CourseModule> { module }, point => Add(point, new CoursePoint(0, delta, 0)), 0);
                module.LayerId = layerId;
                foreach (var zone in draft.Course.Zones.Where(item => item.ModuleId == module.Id))
                    zone.LayerIds = zone.LayerIds.Select(id => id == previous ? layerId : id).Distinct().ToList();
            }
            draft.ActiveLayer = layerId;
        }
        public static void AddLayer(WorkshopDraft draft, int heightMm)
        {
            if (Math.Abs((long)heightMm) > CourseLimits.VerticalMm || draft.Course.Layers.Any(layer => layer.HeightMm == heightMm))
                throw new ArgumentException("Use a distinct bounded floor height.");
            string id = Unique("floor", draft.Course.Layers.Select(layer => layer.Id));
            draft.Course.Layers.Add(new CourseLayer { Id = id, HeightMm = heightMm }); draft.ActiveLayer = id;
        }
        public static void Group(WorkshopDraft draft, string groupId)
        {
            var ids = Selected(draft).Select(module => module.Id).ToList();
            if (ids.Count < 2) throw new InvalidOperationException("Select at least two modules.");
            foreach (var members in draft.Groups.Values) members.RemoveAll(ids.Contains);
            draft.Groups[groupId] = ids;
        }
        public static void Ungroup(WorkshopDraft draft)
        {
            foreach (string id in draft.Groups.Where(group => group.Value.Any(draft.Selection.Contains)).Select(group => group.Key).ToArray())
                draft.Groups.Remove(id);
        }
        public static void Delete(WorkshopDraft draft)
        {
            var ids = new HashSet<string>(Selected(draft).Select(module => module.Id), StringComparer.Ordinal);
            draft.Course.Modules.RemoveAll(module => ids.Contains(module.Id));
            draft.Course.Connections.RemoveAll(connection => ids.Contains(connection.FromModuleId) || ids.Contains(connection.ToModuleId));
            draft.Course.Puzzles.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Course.Supplies.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Course.Spawns.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Course.RecoveryAnchors.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Course.Checkpoints.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Course.Zones.RemoveAll(item => ids.Contains(item.ModuleId));
            draft.Selection.Clear();
        }
        public static void Duplicate(WorkshopDraft draft, CoursePoint offset)
        {
            var selected = new HashSet<string>(Selected(draft).Select(module => module.Id), StringComparer.Ordinal);
            var copy = draft.Course.Clone();
            var modules = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var module in copy.Modules.Where(module => selected.Contains(module.Id)))
            {
                string old = module.Id;
                module.Id = Unique(old, draft.Course.Modules.Select(item => item.Id)); modules.Add(old, module.Id);
                draft.Course.Modules.Add(module);
            }
            var puzzles = CopyOwned(copy.Puzzles, draft.Course.Puzzles, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            var supplies = CopyOwned(copy.Supplies, draft.Course.Supplies, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            CopyOwned(copy.Spawns, draft.Course.Spawns, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            var recoveries = CopyOwned(copy.RecoveryAnchors, draft.Course.RecoveryAnchors, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            CopyOwned(copy.Zones, draft.Course.Zones, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            string Remap(string id, Dictionary<string, string> map) => map.TryGetValue(id, out var next) ? next : id;
            var connections = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var connection in copy.Connections.Where(item => selected.Contains(item.FromModuleId) && selected.Contains(item.ToModuleId)))
            {
                string previous = connection.Id;
                connection.Id = Unique(previous, draft.Course.Connections.Select(item => item.Id)); connections.Add(previous, connection.Id);
                connection.FromModuleId = modules[connection.FromModuleId]; connection.ToModuleId = modules[connection.ToModuleId];
                connection.GatePuzzleId = Remap(connection.GatePuzzleId, puzzles); draft.Course.Connections.Add(connection);
            }
            CopyOwned(copy.Checkpoints, draft.Course.Checkpoints, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            var newIds = new HashSet<string>(modules.Values, StringComparer.Ordinal);
            foreach (var puzzle in draft.Course.Puzzles.Where(item => newIds.Contains(item.ModuleId)))
            {
                puzzle.PrerequisiteIds = puzzle.PrerequisiteIds.Select(id => Remap(id, puzzles)).ToList();
                puzzle.RecoverySupplyIds = puzzle.RecoverySupplyIds.Select(id => Remap(id, supplies)).ToList();
            }
            foreach (var item in draft.Course.Supplies.Where(item => newIds.Contains(item.ModuleId)))
                item.AfterPuzzleIds = item.AfterPuzzleIds.Select(id => Remap(id, puzzles)).ToList();
            foreach (var item in draft.Course.RecoveryAnchors.Where(item => newIds.Contains(item.ModuleId)))
                item.AfterPuzzleIds = item.AfterPuzzleIds.Select(id => Remap(id, puzzles)).ToList();
            foreach (var checkpoint in draft.Course.Checkpoints.Where(item => newIds.Contains(item.ModuleId)))
            {
                checkpoint.PuzzleIds = checkpoint.PuzzleIds.Select(id => Remap(id, puzzles)).ToList();
                checkpoint.RecoveryAnchorId = Remap(checkpoint.RecoveryAnchorId, recoveries);
                checkpoint.EntryConnectionId = Remap(checkpoint.EntryConnectionId, connections);
            }
            draft.Selection = modules.Values.ToList();
            Move(draft, offset);
        }
        static Dictionary<string, string> CopyOwned<T>(IEnumerable<T> source, List<T> target, Func<T, string> id,
            Action<T, string> setId, Func<T, string> owner, Action<T, string> setOwner, Dictionary<string, string> owners)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in source.Where(item => owners.ContainsKey(owner(item))))
            {
                string previous = id(item), next = Unique(previous, target.Select(id));
                map.Add(previous, next); setId(item, next); setOwner(item, owners[owner(item)]); target.Add(item);
            }
            return map;
        }
        public static void SetZone(WorkshopDraft draft, CourseZone zone, CourseCatalogue catalogue)
        {
            var module = draft.Course.Modules.Single(item => item.Id == zone.ModuleId);
            if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the layer first.");
            if (zone.Pool == null || zone.Pool.Any(item => !catalogue.Assets.ContainsKey(item.AssetId)))
                throw new ArgumentException("Choose approved asset IDs.");
            draft.Course.Zones.RemoveAll(item => item.Id == zone.Id); draft.Course.Zones.Add(zone);
        }
        public static void LinkPuzzle(WorkshopDraft draft, string puzzleId, string prerequisiteId)
        {
            var puzzle = draft.Course.Puzzles.Single(item => item.Id == puzzleId);
            if (draft.LockedLayers.Contains(draft.Course.Modules.Single(module => module.Id == puzzle.ModuleId).LayerId))
                throw new InvalidOperationException("Unlock the layer first.");
            if (puzzleId == prerequisiteId || !draft.Course.Puzzles.Any(item => item.Id == prerequisiteId))
                throw new ArgumentException("Choose a different existing prerequisite puzzle.");
            if (!puzzle.PrerequisiteIds.Contains(prerequisiteId)) puzzle.PrerequisiteIds.Add(prerequisiteId);
        }
        public static void Gate(WorkshopDraft draft, string connectionId, string puzzleId)
        {
            if (!draft.Course.Puzzles.Any(puzzle => puzzle.Id == puzzleId)) throw new ArgumentException("Choose an existing puzzle.");
            var connection = draft.Course.Connections.Single(item => item.Id == connectionId);
            if (draft.Course.Modules.Any(module => (module.Id == connection.FromModuleId || module.Id == connection.ToModuleId) && draft.LockedLayers.Contains(module.LayerId)))
                throw new InvalidOperationException("Unlock both connected layers first.");
            connection.GatePuzzleId = puzzleId;
        }
    }
}
