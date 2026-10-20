using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class WorkshopRoutes
    {
        public const int SpawnClearanceMm = 500, SpawnSeparationMm = 1000;

        static CourseModule Module(WorkshopDraft draft, string id)
        {
            var value = draft.Course.Modules.SingleOrDefault(m => m.Id == id);
            if (value == null) throw new ArgumentException("Choose an existing module.");
            if (draft.LockedLayers.Contains(value.LayerId)) throw new InvalidOperationException("Unlock the affected floor first.");
            return value;
        }

        static List<string> References(IEnumerable<string> source, IEnumerable<string> available, string label)
        {
            var values = (source ?? throw new ArgumentNullException(nameof(source))).Take(CourseLimits.ListEntries + 1).ToList();
            var known = new HashSet<string>(available, StringComparer.Ordinal);
            if (values.Count > CourseLimits.ListEntries || values.Distinct(StringComparer.Ordinal).Count() != values.Count ||
                values.Any(id => !known.Contains(id))) throw new ArgumentException("Choose distinct existing " + label + ".");
            return values;
        }

        static void Location(CourseModule module, CourseCatalogue catalogue, CoursePoint position, int radius)
        {
            if (radius < 500 || radius > 5000) throw new ArgumentException("Recovery radius must be 0.5–5 metres.");
            if (position.Y != module.Position.Y || !catalogue.Bounds(module).Contains(position, radius))
                throw new ArgumentException("Keep the recovery anchor on its module floor with room for its full radius.");
        }

        public static string AddRecovery(WorkshopDraft draft, CourseCatalogue catalogue, string moduleId,
            CoursePoint position, int radiusMm = 1000)
        {
            var module = Module(draft, moduleId); Location(module, catalogue, position, radiusMm);
            if (draft.Course.RecoveryAnchors.Count >= CourseLimits.Checkpoints) throw new InvalidOperationException("Recovery anchor limit reached.");
            string id = WorkshopCommands.Unique("recovery", draft.Course.RecoveryAnchors.Select(r => r.Id));
            draft.Course.RecoveryAnchors.Add(new CourseRecovery { Id = id, ModuleId = moduleId, Position = position, RadiusMm = radiusMm });
            return id;
        }

        public static void SetRecovery(WorkshopDraft draft, CourseCatalogue catalogue, string id, CoursePoint position,
            int radiusMm, IEnumerable<string> afterPuzzleIds)
        {
            var value = draft.Course.RecoveryAnchors.Single(r => r.Id == id);
            var module = Module(draft, value.ModuleId); Location(module, catalogue, position, radiusMm);
            var prerequisites = References(afterPuzzleIds, draft.Course.Puzzles.Select(p => p.Id), "puzzle prerequisites");
            value.Position = position; value.RadiusMm = radiusMm; value.AfterPuzzleIds = prerequisites;
        }

        public static void DeleteRecovery(WorkshopDraft draft, string id)
        {
            var value = draft.Course.RecoveryAnchors.Single(r => r.Id == id); Module(draft, value.ModuleId);
            if (draft.Course.Checkpoints.Any(c => c.RecoveryAnchorId == id))
                throw new InvalidOperationException("Choose another recovery anchor on every linked checkpoint before deleting this one.");
            draft.Course.RecoveryAnchors.Remove(value);
        }

        static void SpawnFields(WorkshopDraft draft, CourseCatalogue catalogue, string id, string moduleId,
            string kind, CoursePoint position, int yaw)
        {
            var module = Module(draft, moduleId);
            if (kind != "Start" && kind != "Respawn") throw new ArgumentException("Choose Start or Respawn.");
            if (yaw != 0 && yaw != 90 && yaw != 180 && yaw != 270) throw new ArgumentException("Choose a quarter-turn rotation.");
            if (position.Y != module.Position.Y || !catalogue.Bounds(module).Contains(position, SpawnClearanceMm))
                throw new ArgumentException("Keep the spawn on its module floor with 0.5 metres of clearance.");
            foreach (var other in draft.Course.Spawns.Where(s => s.Id != id && s.Kind == kind &&
                Math.Abs((long)s.Position.Y - position.Y) < 2000))
            {
                long x = (long)other.Position.X - position.X, z = (long)other.Position.Z - position.Z;
                if (x * x + z * z < (long)SpawnSeparationMm * SpawnSeparationMm)
                    throw new ArgumentException("Keep spawns of the same kind at least 1 metre apart.");
            }
        }

        public static string AddSpawn(WorkshopDraft draft, CourseCatalogue catalogue, string moduleId,
            string kind, CoursePoint position, int yaw = 0)
        {
            SpawnFields(draft, catalogue, "", moduleId, kind, position, yaw);
            if (draft.Course.Spawns.Count >= 64) throw new InvalidOperationException("Spawn limit reached.");
            string id = WorkshopCommands.Unique("spawn", draft.Course.Spawns.Select(s => s.Id));
            draft.Course.Spawns.Add(new CourseSpawn { Id = id, ModuleId = moduleId, Kind = kind, Position = position, Yaw = yaw });
            return id;
        }

        public static void SetSpawn(WorkshopDraft draft, CourseCatalogue catalogue, string id, string moduleId,
            string kind, CoursePoint position, int yaw)
        {
            var value = draft.Course.Spawns.Single(s => s.Id == id); ExistingModule(draft, value.ModuleId);
            SpawnFields(draft, catalogue, id, moduleId, kind, position, yaw);
            value.ModuleId = moduleId; value.Kind = kind; value.Position = position; value.Yaw = yaw;
        }

        public static void DeleteSpawn(WorkshopDraft draft, string id)
        {
            var value = draft.Course.Spawns.Single(s => s.Id == id); ExistingModule(draft, value.ModuleId);
            draft.Course.Spawns.Remove(value);
        }

        static List<string> CheckpointFields(WorkshopDraft draft, string moduleId, string recoveryId,
            string entryConnectionId, IEnumerable<string> puzzleIds)
        {
            Module(draft, moduleId);
            if (!draft.Course.RecoveryAnchors.Any(r => r.Id == recoveryId)) throw new ArgumentException("Choose an existing recovery anchor.");
            if (entryConnectionId.Length > 0)
            {
                var entry = draft.Course.Connections.SingleOrDefault(c => c.Id == entryConnectionId);
                if (entry == null || !(entry.ToModuleId == moduleId || entry.Bidirectional && entry.FromModuleId == moduleId))
                    throw new ArgumentException("Choose a connection that can enter this checkpoint's module, or Any arrival.");
            }
            return References(puzzleIds, draft.Course.Puzzles.Select(p => p.Id), "checkpoint puzzles");
        }

        public static string AddCheckpoint(WorkshopDraft draft, string moduleId, string recoveryId,
            string entryConnectionId = "", IEnumerable<string> puzzleIds = null)
        {
            var puzzles = CheckpointFields(draft, moduleId, recoveryId, entryConnectionId, puzzleIds ?? Array.Empty<string>());
            if (draft.Course.Checkpoints.Count >= CourseLimits.Checkpoints) throw new InvalidOperationException("Checkpoint limit reached.");
            string id = WorkshopCommands.Unique("checkpoint", draft.Course.Checkpoints.Select(c => c.Id));
            draft.Course.Checkpoints.Add(new CourseCheckpoint { Id = id, ModuleId = moduleId, RecoveryAnchorId = recoveryId,
                EntryConnectionId = entryConnectionId, PuzzleIds = puzzles });
            return id;
        }

        public static void SetCheckpoint(WorkshopDraft draft, string id, string recoveryId,
            string entryConnectionId, IEnumerable<string> puzzleIds)
        {
            var value = draft.Course.Checkpoints.Single(c => c.Id == id);
            var puzzles = CheckpointFields(draft, value.ModuleId, recoveryId, entryConnectionId, puzzleIds);
            if (draft.Course.PhysicalSites.Any(s => s.CheckpointId == id && !puzzles.Contains(s.PuzzleId)))
                throw new InvalidOperationException("Move or delete the linked physical controls before removing their puzzle from this checkpoint.");
            value.RecoveryAnchorId = recoveryId; value.EntryConnectionId = entryConnectionId; value.PuzzleIds = puzzles;
        }

        public static void DeleteCheckpointFromRoutes(WorkshopDraft draft, string id)
        {
            var value = draft.Course.Checkpoints.Single(c => c.Id == id); Module(draft, value.ModuleId);
            if (draft.Course.PhysicalSites.Any(s => s.CheckpointId == id))
                throw new InvalidOperationException("Move or delete the linked physical controls before deleting this checkpoint.");
            foreach (var section in draft.Course.RouteSections.Where(s => s.Alternatives.Any(a => a.CheckpointIds.Contains(id))))
                EditableSection(draft, section);
            foreach (var alternative in draft.Course.RouteSections.SelectMany(s => s.Alternatives)) alternative.CheckpointIds.Remove(id);
            draft.Course.Checkpoints.Remove(value);
        }

        static void EditableSection(WorkshopDraft draft, CourseRouteSection section)
        {
            ExistingModule(draft, section.EntryModuleId); ExistingModule(draft, section.ExitModuleId);
            foreach (var id in section.Alternatives.SelectMany(a => a.ConnectionIds).Distinct())
            {
                var edge = draft.Course.Connections.SingleOrDefault(c => c.Id == id);
                if (edge != null) { ExistingModule(draft, edge.FromModuleId); ExistingModule(draft, edge.ToModuleId); }
            }
        }

        static void ExistingModule(WorkshopDraft draft, string id)
        {
            var module = draft.Course.Modules.SingleOrDefault(m => m.Id == id);
            if (module != null && draft.LockedLayers.Contains(module.LayerId))
                throw new InvalidOperationException("Unlock the affected floor first.");
        }

        public static string AddSection(WorkshopDraft draft, string entryModuleId, string exitModuleId)
        {
            Module(draft, entryModuleId); Module(draft, exitModuleId);
            if (draft.Course.RouteSections.Count >= CourseLimits.Sections) throw new InvalidOperationException("Route section limit reached.");
            string id = WorkshopCommands.Unique("route", draft.Course.RouteSections.Select(s => s.Id));
            string branch = WorkshopCommands.Unique("branch", draft.Course.RouteSections.SelectMany(s => s.Alternatives).Select(a => a.Id));
            draft.Course.RouteSections.Add(new CourseRouteSection { Id = id, EntryModuleId = entryModuleId, ExitModuleId = exitModuleId,
                Alternatives = new List<CourseAlternative> { new CourseAlternative { Id = branch } } });
            return id;
        }

        public static void SetSectionEndpoints(WorkshopDraft draft, string id, string entryModuleId, string exitModuleId)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == id); EditableSection(draft, section);
            Module(draft, entryModuleId); Module(draft, exitModuleId);
            section.EntryModuleId = entryModuleId; section.ExitModuleId = exitModuleId;
        }

        public static string AddAlternative(WorkshopDraft draft, string sectionId)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == sectionId); EditableSection(draft, section);
            if (section.Alternatives.Count >= CourseLimits.Alternatives) throw new InvalidOperationException("Branch limit reached.");
            string id = WorkshopCommands.Unique("branch", draft.Course.RouteSections.SelectMany(s => s.Alternatives).Select(a => a.Id));
            section.Alternatives.Add(new CourseAlternative { Id = id }); return id;
        }

        public static IReadOnlyList<string> Trail(CourseDocument course, string startModuleId, IEnumerable<string> connectionIds)
        {
            if (!course.Modules.Any(m => m.Id == startModuleId)) throw new ArgumentException("Choose an existing route start.");
            var trail = new List<string> { startModuleId };
            foreach (var id in connectionIds)
            {
                var edge = course.Connections.SingleOrDefault(c => c.Id == id);
                string current = trail[trail.Count - 1];
                string next = edge == null ? null : edge.FromModuleId == current ? edge.ToModuleId :
                    edge.Bidirectional && edge.ToModuleId == current ? edge.FromModuleId : null;
                if (next == null) throw new InvalidOperationException("Connection " + id + " cannot continue from " + current +
                    ". Choose a connected path; one-way connections cannot be reversed.");
                trail.Add(next);
            }
            return trail.AsReadOnly();
        }

        static void CheckpointOrder(CourseDocument course, IReadOnlyList<string> trail,
            IList<string> connections, IEnumerable<string> checkpointIds)
        {
            int position = 0;
            foreach (var id in checkpointIds)
            {
                var checkpoint = course.Checkpoints.Single(c => c.Id == id);
                while (position < trail.Count && (trail[position] != checkpoint.ModuleId ||
                    checkpoint.EntryConnectionId.Length > 0 && (position == 0 || connections[position - 1] != checkpoint.EntryConnectionId))) position++;
                if (position == trail.Count) throw new InvalidOperationException("Checkpoint " + id +
                    " is outside this ordered path or its arrival connection does not match. Edit its arrival explicitly or choose another checkpoint.");
            }
        }

        public static void SetAlternative(WorkshopDraft draft, string sectionId, string alternativeId,
            IEnumerable<string> connectionIds, IEnumerable<string> checkpointIds, int weight)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == sectionId); EditableSection(draft, section);
            var alternative = section.Alternatives.Single(a => a.Id == alternativeId);
            var connections = References(connectionIds, draft.Course.Connections.Select(c => c.Id), "route connections");
            var checkpoints = References(checkpointIds, draft.Course.Checkpoints.Select(c => c.Id), "route checkpoints");
            if (weight < 1 || weight > 10000) throw new ArgumentException("Branch weight must be 1–10000.");
            var trail = Trail(draft.Course, section.EntryModuleId, connections);
            foreach (string module in trail) Module(draft, module);
            if (trail[trail.Count - 1] != section.ExitModuleId) throw new InvalidOperationException("The path must reach the route's selected finish module.");
            CheckpointOrder(draft.Course, trail, connections, checkpoints);
            alternative.ConnectionIds = connections; alternative.CheckpointIds = checkpoints; alternative.Weight = weight;
        }

        public static void ReverseSection(WorkshopDraft draft, string sectionId)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == sectionId); EditableSection(draft, section);
            foreach (var alternative in section.Alternatives)
            {
                var connections = alternative.ConnectionIds.AsEnumerable().Reverse().ToList();
                var trail = Trail(draft.Course, section.ExitModuleId, connections);
                if (trail[trail.Count - 1] != section.EntryModuleId) throw new InvalidOperationException("Complete this branch before reversing the route.");
                CheckpointOrder(draft.Course, trail, connections, alternative.CheckpointIds.AsEnumerable().Reverse());
            }
            string previous = section.EntryModuleId; section.EntryModuleId = section.ExitModuleId; section.ExitModuleId = previous;
            foreach (var alternative in section.Alternatives) { alternative.ConnectionIds.Reverse(); alternative.CheckpointIds.Reverse(); }
        }

        public static void DeleteAlternative(WorkshopDraft draft, string sectionId, string alternativeId)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == sectionId); EditableSection(draft, section);
            if (section.Alternatives.Count <= 1) throw new InvalidOperationException("Keep one branch, or delete the whole route from its laps.");
            section.Alternatives.Remove(section.Alternatives.Single(a => a.Id == alternativeId));
        }

        public static void DeleteSectionFromLaps(WorkshopDraft draft, string sectionId)
        {
            var section = draft.Course.RouteSections.Single(s => s.Id == sectionId); EditableSection(draft, section);
            foreach (var lap in draft.Course.Laps) lap.SectionIds.Remove(sectionId);
            draft.Course.RouteSections.Remove(section);
        }

        public static void SetLap(WorkshopDraft draft, CourseCatalogue catalogue, int number, string startModuleId,
            string finishModuleId, string narrativeId, IEnumerable<string> sectionIds)
        {
            if (number < 1 || number > 3) throw new ArgumentException("Choose lap 1, 2 or 3.");
            Module(draft, startModuleId); Module(draft, finishModuleId);
            if (!catalogue.NarrativeEvents.Contains(narrativeId)) throw new ArgumentException("Choose an approved lap story.");
            var sections = References(sectionIds, draft.Course.RouteSections.Select(s => s.Id), "lap route sections");
            if (sections.Count == 0) throw new ArgumentException("Add at least one route section to this lap.");
            string current = startModuleId;
            foreach (string id in sections)
            {
                var section = draft.Course.RouteSections.Single(s => s.Id == id); EditableSection(draft, section);
                if (section.EntryModuleId != current) throw new InvalidOperationException("Route " + id + " must begin at " + current + ".");
                current = section.ExitModuleId;
            }
            if (current != finishModuleId) throw new InvalidOperationException("The ordered sections must reach the lap finish.");
            var value = draft.Course.Laps.SingleOrDefault(l => l.Number == number);
            if (value != null) { ExistingModule(draft, value.StartModuleId); ExistingModule(draft, value.FinishModuleId); }
            else { value = new CourseLap { Number = number }; draft.Course.Laps.Add(value); }
            value.StartModuleId = startModuleId; value.FinishModuleId = finishModuleId;
            value.NarrativeId = narrativeId; value.SectionIds = sections;
        }

        public static void DeleteLap(WorkshopDraft draft, int number)
        {
            var value = draft.Course.Laps.Single(l => l.Number == number);
            ExistingModule(draft, value.StartModuleId); ExistingModule(draft, value.FinishModuleId);
            foreach (string id in value.SectionIds)
            {
                var section = draft.Course.RouteSections.SingleOrDefault(s => s.Id == id);
                if (section != null) EditableSection(draft, section);
            }
            draft.Course.Laps.Remove(value);
        }
    }
}
