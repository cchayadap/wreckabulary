using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static partial class CourseValidator
    {
        static void ValidateProgression(CourseDocument d, CourseCatalogue catalogue, CoursePlayRequirements play, CourseValidation v)
        {
            var done = new HashSet<string>(StringComparer.Ordinal);
            var unmet = d.Puzzles.ToDictionary(p => p.Id, p => p.PrerequisiteIds.Count, StringComparer.Ordinal);
            var successors = d.Puzzles.ToDictionary(p => p.Id, p => new List<string>(), StringComparer.Ordinal);
            foreach (var puzzle in d.Puzzles)
                foreach (string prerequisite in puzzle.PrerequisiteIds) successors[prerequisite].Add(puzzle.Id);
            var ready = new Queue<string>(d.Puzzles.Where(p => p.PrerequisiteIds.Count == 0).Select(p => p.Id));
            while (ready.Count > 0)
            {
                string id = ready.Dequeue(); done.Add(id);
                foreach (string next in successors[id]) if (--unmet[next] == 0) ready.Enqueue(next);
            }
            if (done.Count != d.Puzzles.Count) v.Add("puzzle.cycle", "$.puzzles", d.Id, "Puzzle prerequisites contain a cycle.");
            foreach (var section in d.RouteSections)
            {
                if (!d.Modules.Any(m => m.Id == section.EntryModuleId) || !d.Modules.Any(m => m.Id == section.ExitModuleId))
                { v.Add("route.module", "$.routeSections", section.Id, "Route entry and exit must exist."); continue; }
                foreach (var alt in section.Alternatives)
                {
                    var trail = Trail(d, section.EntryModuleId, alt.ConnectionIds, v, alt.Id);
                    if (trail == null) continue;
                    if (trail[trail.Count - 1] != section.ExitModuleId)
                        v.Add("route.exit", "$.routeSections", alt.Id, "Alternative does not reach its declared exit.");
                    int step = 0;
                    foreach (var id in alt.CheckpointIds)
                    {
                        var checkpoint = d.Checkpoints.FirstOrDefault(c => c.Id == id);
                        if (checkpoint == null) { v.Add("route.checkpoint", "$.routeSections", alt.Id, "Unknown checkpoint."); continue; }
                        step = CheckpointStep(checkpoint, trail, alt.ConnectionIds, step);
                        if (step < 0) { v.Add("checkpoint.order", "$.routeSections", id, "Checkpoint is not on the ordered alternative or its entry connection."); break; }
                    }
                }
            }
            if (play.Capability == CourseCapability.Circuit &&
                (d.Laps.Count != 3 || !d.Laps.Select(l => l.Number).OrderBy(x => x).SequenceEqual(new[] { 1, 2, 3 })))
                v.Add("laps.required", "$.laps", d.Id, "Circuit courses require all three laps.");
            foreach (var lap in d.Laps)
            {
                if (lap.Number < 1 || lap.Number > 3 || !d.Modules.Any(m => m.Id == lap.StartModuleId) ||
                    !d.Modules.Any(m => m.Id == lap.FinishModuleId) || lap.SectionIds.Count == 0 ||
                    !catalogue.NarrativeEvents.Contains(lap.NarrativeId))
                { v.Add("lap.invalid", "$.laps", lap.Number.ToString(), "Provide valid endpoints, narrative ID and route sections."); continue; }
                string current = lap.StartModuleId;
                foreach (string id in lap.SectionIds)
                {
                    var section = d.RouteSections.FirstOrDefault(s => s.Id == id);
                    if (section == null || section.EntryModuleId != current)
                    { v.Add("lap.sequence", "$.laps", id, "Sections must form an ordered continuous route."); break; }
                    current = section.ExitModuleId;
                }
                if (current != lap.FinishModuleId) v.Add("lap.finish", "$.laps", lap.Number.ToString(), "The route must reach its lap finish.");
                if (lap.Number > 1 && d.Laps.Any(l => l.Number == lap.Number - 1 && l.FinishModuleId != lap.StartModuleId))
                    v.Add("lap.transition", "$.laps", lap.Number.ToString(), "The next lap starts where the preceding lap ends.");
            }
            if (play.Capability == CourseCapability.Circuit && d.Laps.Count > 0 &&
                d.Spawns.Where(s => s.Kind == "Start").Any(s => s.ModuleId != d.Laps.OrderBy(l => l.Number).First().StartModuleId))
                v.Add("spawn.route", "$.spawns", d.Id, "Circuit start spawns must be in the first lap's start module.");
        }

        internal static List<string> Trail(CourseDocument d, string entry, IList<string> connectionIds, CourseValidation v, string owner)
        {
            var trail = new List<string> { entry };
            string current = entry;
            var edges = d.Connections.ToDictionary(e => e.Id, StringComparer.Ordinal);
            foreach (string id in connectionIds)
            {
                edges.TryGetValue(id, out var edge);
                string next = edge == null ? null : edge.FromModuleId == current ? edge.ToModuleId :
                    edge.Bidirectional && edge.ToModuleId == current ? edge.FromModuleId : null;
                if (next == null) { v.Add("route.connection", "$.routeSections", owner, "Connection " + id + " does not continue the route."); return null; }
                trail.Add(next); current = next;
            }
            return trail;
        }
        internal static int CheckpointStep(CourseCheckpoint checkpoint, IList<string> trail, IList<string> connections, int from)
        {
            for (int i = from; i < trail.Count; i++)
                if (trail[i] == checkpoint.ModuleId &&
                    (string.IsNullOrEmpty(checkpoint.EntryConnectionId) || i > 0 && connections[i - 1] == checkpoint.EntryConnectionId))
                    return i;
            return -1;
        }
        internal static CourseLapSelection SelectLap(CourseDocument d, CourseLap lap, IList<CourseAlternative> alternatives)
        {
            var selected = new CourseLapSelection { Number = lap.Number, StartModuleId = lap.StartModuleId, FinishModuleId = lap.FinishModuleId, NarrativeId = lap.NarrativeId };
            for (int i = 0; i < alternatives.Count; i++)
            {
                var alt = alternatives[i];
                var section = d.RouteSections.Single(s => s.Id == lap.SectionIds[i]);
                var trail = Trail(d, section.EntryModuleId, alt.ConnectionIds, new CourseValidation(), alt.Id);
                int step = 0;
                foreach (string id in alt.CheckpointIds)
                {
                    step = CheckpointStep(d.Checkpoints.Single(c => c.Id == id), trail, alt.ConnectionIds, step);
                    selected.CheckpointIds.Add(id); selected.CheckpointSteps.Add(selected.ConnectionIds.Count + step);
                }
                selected.AlternativeIds.Add(alt.Id);
                selected.ConnectionIds.AddRange(alt.ConnectionIds);
            }
            return selected;
        }

        internal static void ValidateSelectedProgression(CourseManifestData data, CourseValidation v)
        {
            if (data.Play.ToRequirements().Capability != CourseCapability.Circuit) return;
            var d = data.Document;
            var checkpoints = d.Checkpoints.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var puzzles = d.Puzzles.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var supplies = d.Supplies.ToDictionary(s => s.Id, StringComparer.Ordinal);
            var anchors = d.RecoveryAnchors.ToDictionary(r => r.Id, StringComparer.Ordinal);
            var connections = d.Connections.ToDictionary(c => c.Id, StringComparer.Ordinal);
            foreach (var lap in data.Laps)
            {
                if (lap.CheckpointIds.Count == 0 || lap.CheckpointIds.Distinct(StringComparer.Ordinal).Count() != lap.CheckpointIds.Count ||
                    lap.CheckpointSteps.LastOrDefault() != lap.ConnectionIds.Count ||
                    checkpoints[lap.CheckpointIds.Last()].ModuleId != lap.FinishModuleId)
                { v.Add("lap.checkpoints", "$.laps", lap.Number.ToString(), "A lap needs distinct ordered checkpoints ending at its finish."); continue; }
                var trail = Trail(d, lap.StartModuleId, lap.ConnectionIds, v, lap.Number.ToString());
                if (trail == null) continue;
                var done = new HashSet<string>(StringComparer.Ordinal);
                var collected = new HashSet<string>(StringComparer.Ordinal);
                var inventory = new LetterBag();
                string safeModule = lap.StartModuleId;
                int cpIndex = 0;
                for (int step = 0; step <= lap.ConnectionIds.Count; step++)
                {
                    while (cpIndex < lap.CheckpointIds.Count && lap.CheckpointSteps[cpIndex] == step)
                    {
                        var checkpoint = checkpoints[lap.CheckpointIds[cpIndex]];
                        foreach (string id in checkpoint.PuzzleIds)
                        {
                            var puzzle = puzzles[id];
                            if (done.Contains(id)) { v.Add("puzzle.repeated", "$.laps", id, "A required puzzle appears twice in this lap."); continue; }
                            var reachable = Reachable(d, trail[step], done);
                            var safeReachable = Reachable(d, safeModule, done);
                            if (!puzzle.PrerequisiteIds.All(done.Contains) || !reachable.Contains(puzzle.ModuleId))
                            { v.Add("puzzle.prefix", "$.laps", id, "Puzzle prerequisites or location are unavailable at this checkpoint."); continue; }
                            foreach (var supply in d.Supplies)
                                if (reachable.Contains(supply.ModuleId) && supply.AfterPuzzleIds.All(done.Contains) && collected.Add("s/" + supply.Id))
                                    inventory.Add(LetterBag.FromWord(supply.Letters));
                            foreach (var placement in data.Placements.Where(p => p.Purpose == "Resources" && p.TeamIndex <= 0))
                                if (reachable.Contains(placement.ModuleId) && collected.Add("p/" + placement.Id))
                                    inventory.Add(LetterBag.FromWord(placement.Letters));
                            var needed = LetterBag.FromWord(puzzle.RequiredLetters);
                            var recovery = new LetterBag();
                            foreach (string supplyId in puzzle.RecoverySupplyIds)
                            {
                                var supply = supplies[supplyId];
                                if (supply.Protected && supply.Renewable && supply.AfterPuzzleIds.All(done.Contains) &&
                                    reachable.Contains(supply.ModuleId) && safeReachable.Contains(supply.ModuleId) &&
                                    Reachable(d, supply.ModuleId, done).Contains(puzzle.ModuleId))
                                    recovery.Add(LetterBag.FromWord(supply.Letters));
                            }
                            if (!recovery.Contains(needed))
                            { v.Add("resource.recovery", "$.laps", id, "Protected renewable recovery supplies must cover every required letter before this objective, including from the last safe checkpoint."); continue; }
                            var missing = inventory.Missing(needed);
                            if (!missing.IsEmpty) inventory.Add(missing);
                            if (puzzle.ConsumeLetters && !inventory.TryRemove(needed))
                            { v.Add("resource.prefix", "$.laps", id, "Not enough letters at this progression prefix."); continue; }
                            inventory.Add(LetterBag.FromWord(puzzle.RewardLetters));
                            done.Add(id);
                            if (!Reachable(d, puzzle.ModuleId, done).Contains(trail[step]))
                                v.Add("puzzle.return", "$.laps", id, "Completing the puzzle must allow return to its route checkpoint.");
                        }
                        var anchor = anchors[checkpoint.RecoveryAnchorId];
                        if (!anchor.AfterPuzzleIds.All(done.Contains) || !Reachable(d, trail[step], done).Contains(anchor.ModuleId) ||
                            !Reachable(d, anchor.ModuleId, done).Contains(trail[step]))
                            v.Add("recovery.unreachable", "$.laps", checkpoint.Id, "Recovery must remain reachable and allow return to the checkpoint.");
                        safeModule = anchor.ModuleId; cpIndex++;
                    }
                    if (step == lap.ConnectionIds.Count) break;
                    var connection = connections[lap.ConnectionIds[step]];
                    if (!string.IsNullOrEmpty(connection.GatePuzzleId) && !done.Contains(connection.GatePuzzleId))
                    { v.Add("gate.prefix", "$.laps", connection.Id, "The selected route crosses a gate before completing its puzzle."); break; }
                }
            }
        }
    }
}
