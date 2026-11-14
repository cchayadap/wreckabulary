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
            WorkshopPhysicalSites.TransformOwned(course, ids, point, yaw);
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
        public static void SetLayerHeight(WorkshopDraft draft, string layerId, int heightMm)
        {
            var layer = draft.Course.Layers.Single(item => item.Id == layerId);
            if (draft.LockedLayers.Contains(layerId)) throw new InvalidOperationException("Unlock the floor first.");
            var (below, above) = LayerRange(draft.Course, layerId);
            if (Math.Abs((long)heightMm) > CourseLimits.VerticalMm || heightMm <= below || heightMm >= above)
                throw new ArgumentException("Keep the floor between its neighbours and within the height limit.");
            int delta = checked(heightMm - layer.HeightMm);
            if (delta == 0) return;
            Transform(draft.Course, draft.Course.Modules.Where(module => module.LayerId == layerId).ToList(),
                point => Add(point, new CoursePoint(0, delta, 0)), 0);
            layer.HeightMm = heightMm;
        }
        public static (long below, long above) LayerRange(CourseDocument course, string layerId)
        {
            var layer = course.Layers.Single(item => item.Id == layerId);
            var others = course.Layers.Where(item => item != layer).Select(item => (long)item.HeightMm).ToList();
            return (others.Where(height => height < layer.HeightMm).DefaultIfEmpty(-CourseLimits.VerticalMm - 1L).Max(),
                others.Where(height => height > layer.HeightMm).DefaultIfEmpty(CourseLimits.VerticalMm + 1L).Min());
        }
        public static int NextLayerHeight(CourseDocument course, CourseCatalogue catalogue)
        {
            var top = course.Layers.OrderBy(layer => layer.HeightMm).LastOrDefault();
            if (top == null) return 0;
            int storey = course.Modules.Where(module => module.LayerId == top.Id && catalogue.Modules.ContainsKey(module.DefinitionId))
                .Select(module => catalogue.Modules[module.DefinitionId].HeightMm).DefaultIfEmpty(4000).Max();
            return (int)Math.Min(CourseLimits.VerticalMm, (long)top.HeightMm + storey);
        }
        public static CourseBox? LayerFootprint(CourseDocument course, CourseCatalogue catalogue, string layerId)
        {
            var boxes = course.Modules.Where(module => module.LayerId == layerId && catalogue.Modules.ContainsKey(module.DefinitionId))
                .Select(catalogue.Bounds).ToList();
            if (boxes.Count == 0) return null;
            return new CourseBox(new CoursePoint(boxes.Min(box => box.Min.X), boxes.Min(box => box.Min.Y), boxes.Min(box => box.Min.Z)),
                new CoursePoint(boxes.Max(box => box.Max.X), boxes.Max(box => box.Max.Y), boxes.Max(box => box.Max.Z)));
        }
        public static (int width, int depth) WorldSize(CourseModuleDefinition definition, int yaw) =>
            yaw % 180 == 0 ? (definition.WidthMm, definition.DepthMm) : (definition.DepthMm, definition.WidthMm);
        public static string FitDefinition(CourseCatalogue catalogue, CourseModule module, int widthMm, int depthMm)
        {
            var current = catalogue.Modules[module.DefinitionId];
            return catalogue.Modules.Values.OrderBy(definition =>
                {
                    var (width, depth) = WorldSize(definition, module.Yaw);
                    return Math.Abs((long)width - widthMm) + Math.Abs((long)depth - depthMm);
                })
                .ThenBy(definition => definition.HeightMm == current.HeightMm ? 0 : 1)
                .ThenBy(definition => definition.Id == current.Id ? 0 : 1)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal).First().Id;
        }
        public static string StepDefinition(CourseCatalogue catalogue, string definitionId, int direction, bool wrap = false)
        {
            var sizes = catalogue.Modules.Values.OrderBy(definition => (long)definition.WidthMm * definition.DepthMm)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal).Select(definition => definition.Id).ToList();
            int index = sizes.IndexOf(definitionId), next = index + Math.Sign(direction);
            if (index < 0) return null;
            return sizes[wrap ? (next % sizes.Count + sizes.Count) % sizes.Count : Math.Max(0, Math.Min(sizes.Count - 1, next))];
        }
        public static CoursePoint ResizedPosition(CourseCatalogue catalogue, CourseModule module, string definitionId, int sideX, int sideZ)
        {
            var bounds = catalogue.Bounds(module);
            var (width, depth) = WorldSize(catalogue.Modules[definitionId], module.Yaw);
            int Axis(int side, int min, int max, int centre, int size) =>
                side > 0 ? checked(min + size / 2) : side < 0 ? checked(max - (size + 1) / 2) : centre;
            return new CoursePoint(Axis(sideX, bounds.Min.X, bounds.Max.X, module.Position.X, width), module.Position.Y,
                Axis(sideZ, bounds.Min.Z, bounds.Max.Z, module.Position.Z, depth));
        }
        public static void ResizeModule(WorkshopDraft draft, CourseCatalogue catalogue, CoursePlayRequirements play,
            string moduleId, string definitionId, CoursePoint position)
        {
            var module = draft.Course.Modules.Single(item => item.Id == moduleId);
            if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the floor first.");
            if (!catalogue.Modules.ContainsKey(module.DefinitionId) || !catalogue.Modules.ContainsKey(definitionId))
                throw new ArgumentException("Choose an approved room size from this map's catalogue.");
            if (position.Y != module.Position.Y) throw new ArgumentException("Resizing keeps the room on its floor.");
            var known = new HashSet<string>(CourseValidator.Validate(draft.Course, catalogue, play).Issues.Select(IssueKey), StringComparer.Ordinal);
            var before = catalogue.Bounds(module);
            var owned = OwnedPoints(draft.Course, module.Id).Where(item => before.Contains(item.point)).ToList();
            module.DefinitionId = definitionId; module.Position = position;
            var after = catalogue.Bounds(module);
            var orphan = owned.FirstOrDefault(item => !after.Contains(item.point));
            if (orphan.label != null)
                throw new InvalidOperationException("Resize refused · " + orphan.label + " would leave the room. Move it first.");
            var blocked = draft.Course.Modules.FirstOrDefault(item => item != module && catalogue.Modules.ContainsKey(item.DefinitionId) &&
                catalogue.Bounds(item).Intersects(after) && !catalogue.Bounds(item).Intersects(before));
            if (blocked != null) throw new InvalidOperationException("Resize refused · the room would overlap " + blocked.Id + ".");
            var door = draft.Course.Connections.FirstOrDefault(link => (link.Kind == "Walk" || link.Kind == "Door") &&
                (link.FromModuleId == module.Id && OnWall(before, link.From) && !OnWall(after, link.From) ||
                 link.ToModuleId == module.Id && OnWall(before, link.To) && !OnWall(after, link.To)));
            if (door != null) throw new InvalidOperationException("Resize refused · doorway " + door.Id + " would leave its wall.");
            var added = CourseValidator.Validate(draft.Course, catalogue, play).Issues.FirstOrDefault(issue => !known.Contains(IssueKey(issue)));
            if (added != null) throw new InvalidOperationException("Resize refused · " + added.EntityId + ": " + added.Message);
        }
        static bool OnWall(CourseBox box, CoursePoint point) =>
            point.X == box.Min.X || point.X == box.Max.X || point.Z == box.Min.Z || point.Z == box.Max.Z;
        static string IssueKey(CourseIssue issue) => issue.Code + "\n" + issue.EntityId + "\n" + issue.Message;
        static IEnumerable<(CoursePoint point, string label)> OwnedPoints(CourseDocument course, string moduleId)
        {
            foreach (var zone in course.Zones.Where(item => item.ModuleId == moduleId))
            {
                yield return (zone.Bounds.Min, "placement " + zone.Id); yield return (zone.Bounds.Max, "placement " + zone.Id);
                if (zone.Placement == "Fixed") yield return (zone.FixedPosition, "object " + zone.Id);
            }
            foreach (var item in course.Supplies.Where(item => item.ModuleId == moduleId)) yield return (item.Position, "letters " + item.Id);
            foreach (var item in course.Spawns.Where(item => item.ModuleId == moduleId)) yield return (item.Position, "spawn " + item.Id);
            foreach (var item in course.RecoveryAnchors.Where(item => item.ModuleId == moduleId)) yield return (item.Position, "safe return " + item.Id);
            foreach (var item in course.Connections)
            {
                if (item.FromModuleId == moduleId) yield return (item.From, "connection " + item.Id);
                if (item.ToModuleId == moduleId) yield return (item.To, "connection " + item.Id);
            }
            var puzzles = new HashSet<string>(course.Puzzles.Where(item => item.ModuleId == moduleId).Select(item => item.Id), StringComparer.Ordinal);
            foreach (var site in course.PhysicalSites.Where(item => puzzles.Contains(item.PuzzleId)))
            {
                string label = "puzzle control " + site.PuzzleId;
                yield return (site.Position, label);
                foreach (var box in new[] { site.ArrivalBounds, site.ReceiverStartBounds }.Concat(site.FloorPadBounds)
                    .Where(box => !WorkshopPhysicalSites.Empty(box)))
                { yield return (box.Min, label); yield return (box.Max, label); }
                foreach (var arm in site.Fixtures) { yield return (arm.From, label); yield return (arm.To, label); }
            }
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
            WorkshopPhysicalSites.RemoveOwned(draft.Course, ids);
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
            var zones = CopyOwned(copy.Zones, draft.Course.Zones, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
            string Remap(string id, Dictionary<string, string> map) => map.TryGetValue(id, out var next) ? next : id;
            var connections = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var connection in copy.Connections.Where(item => selected.Contains(item.FromModuleId) && selected.Contains(item.ToModuleId)))
            {
                string previous = connection.Id;
                connection.Id = Unique(previous, draft.Course.Connections.Select(item => item.Id)); connections.Add(previous, connection.Id);
                connection.FromModuleId = modules[connection.FromModuleId]; connection.ToModuleId = modules[connection.ToModuleId];
                connection.GatePuzzleId = Remap(connection.GatePuzzleId, puzzles); draft.Course.Connections.Add(connection);
            }
            var checkpoints = CopyOwned(copy.Checkpoints, draft.Course.Checkpoints, item => item.Id, (item, id) => item.Id = id, item => item.ModuleId, (item, id) => item.ModuleId = id, modules);
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
            WorkshopPhysicalSites.DuplicateOwned(copy, draft.Course, puzzles, supplies, zones, checkpoints);
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
        public static void SetPuzzle(WorkshopDraft draft, CoursePuzzle puzzle, CourseCatalogue catalogue)
        {
            var module = draft.Course.Modules.Single(item => item.Id == puzzle.ModuleId);
            if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the layer first.");
            if (!catalogue.PuzzlePresets.Contains(puzzle.PresetId)) throw new ArgumentException("Choose an approved puzzle preset.");
            if (!string.IsNullOrEmpty(puzzle.RecipeId) && (!catalogue.Assets.TryGetValue(puzzle.RecipeId, out var recipe) ||
                !recipe.CanCraft || !LetterBag.FromWord(recipe.Letters).Equals(LetterBag.FromWord(puzzle.RequiredLetters))))
                throw new ArgumentException("Puzzle letters must match an approved recipe.");
            draft.Course.Puzzles.RemoveAll(item => item.Id == puzzle.Id); draft.Course.Puzzles.Add(puzzle);
        }
        public static void Connect(WorkshopDraft draft, CourseConnection connection)
        {
            var from = draft.Course.Modules.Single(module => module.Id == connection.FromModuleId);
            var to = draft.Course.Modules.Single(module => module.Id == connection.ToModuleId);
            if (from.Id == to.Id) throw new ArgumentException("Choose two distinct modules.");
            if (draft.LockedLayers.Contains(from.LayerId) || draft.LockedLayers.Contains(to.LayerId))
                throw new InvalidOperationException("Unlock both connected layers first.");
            draft.Course.Connections.RemoveAll(item => item.Id == connection.Id); draft.Course.Connections.Add(connection);
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
