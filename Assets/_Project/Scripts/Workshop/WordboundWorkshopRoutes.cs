using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        bool routesExpanded;
        int routeTab, routeRevision = -1, routeLap = 1;
        string routeModule = "", routeRecovery = "", routeCheckpoint = "", routeSection = "", routeBranch = "";
        string routeSpawn = "", routeSpawnModule = "";
        int routeSpawnKind, routeSpawnYaw;
        string routeEntry = "", routeExit = "", routeArrival = "", routeAnchor = "", routeStory = "", routeWeight = "1";
        string routeX = "0", routeY = "0", routeZ = "0", routeRadius = "1", routeChoiceOpen = "", routeChoiceSearch = "";
        string routeLoadKey = "";
        WorkshopHistory routeHistory;
        readonly List<string> routePuzzles = new List<string>(), routeConnections = new List<string>(),
            routeCheckpoints = new List<string>(), routeSections = new List<string>(), routeAfter = new List<string>();

        void RouteInspector()
        {
            GUILayout.Space(12);
            routesExpanded = GUILayout.Toggle(routesExpanded, "Routes & checkpoints", heading);
            if (!routesExpanded) return;
            int next = GUILayout.Toolbar(routeTab, new[] { "Safe", "Stops", "Routes", "Laps", "Spawns" });
            if (next != routeTab) { routeTab = next; routeLoadKey = ""; }
            if (routeTab < 2)
                routeModule = RouteChoice("Module", routeModule, display.Modules.Select(m => m.Id));
            if (routeTab == 0) RecoveryRoutePanel();
            else if (routeTab == 1) CheckpointRoutePanel();
            else if (routeTab == 2) SectionRoutePanel();
            else if (routeTab == 3) LapRoutePanel();
            else SpawnRoutePanel();
        }

        bool ReloadRoute(string key)
        {
            bool replaced = !ReferenceEquals(routeHistory, History);
            if (!replaced && key == routeLoadKey && routeRevision == History.Revision) return false;
            if (replaced) { routeBranchLoadRevision = -1; routeBranchLoadId = ""; routeChoiceOpen = ""; }
            routeHistory = History;
            routeLoadKey = key; routeRevision = History.Revision; return true;
        }

        void RecoveryRoutePanel()
        {
            routeRecovery = RouteChoice("Recovery anchor", routeRecovery,
                display.RecoveryAnchors.Where(r => r.ModuleId == routeModule).Select(r => r.Id));
            if (Button("Add recovery at module centre"))
                Edit("Add recovery anchor", draft =>
                {
                    var module = draft.Course.Modules.SingleOrDefault(m => m.Id == routeModule)
                        ?? throw new ArgumentException("Choose a module first.");
                    routeRecovery = WorkshopRoutes.AddRecovery(draft, Catalogue, module.Id, module.Position);
                });
            var value = display.RecoveryAnchors.FirstOrDefault(r => r.Id == routeRecovery && r.ModuleId == routeModule);
            if (value == null) { GUILayout.Label("Choose an anchor to edit its safe return position."); return; }
            LoadRecoveryRoute(value);
            GUILayout.BeginHorizontal(); routeX = Field("X m", routeX); routeY = Field("Y m", routeY); routeZ = Field("Z m", routeZ); GUILayout.EndHorizontal();
            routeRadius = Field("Safe radius m", routeRadius);
            RouteReferences("Available after puzzles", routeAfter, display.Puzzles.Select(p => p.Id), false);
            if (Button("Apply recovery anchor"))
                Edit("Edit recovery anchor", draft => WorkshopRoutes.SetRecovery(draft, Catalogue, value.Id,
                    new CoursePoint(Millimetres(routeX), Millimetres(routeY), Millimetres(routeZ)), Millimetres(routeRadius), routeAfter));
            if (Button("Delete unused recovery anchor"))
                Edit("Delete recovery anchor", draft => WorkshopRoutes.DeleteRecovery(draft, value.Id));
        }

        void LoadRecoveryRoute(CourseRecovery value)
        {
            if (!ReloadRoute("recovery/" + value.Id)) return;
            routeX = Metres(value.Position.X); routeY = Metres(value.Position.Y); routeZ = Metres(value.Position.Z);
            routeRadius = Metres(value.RadiusMm); ReplaceRouteList(routeAfter, value.AfterPuzzleIds);
        }

        void SpawnRoutePanel()
        {
            routeSpawn = RouteChoice("Spawn anchor", routeSpawn, display.Spawns.Select(s => s.Id));
            var value = display.Spawns.FirstOrDefault(s => s.Id == routeSpawn);
            if (ReloadRoute("spawn/" + (value?.Id ?? "")))
            {
                routeSpawnModule = value?.ModuleId ?? "";
                routeSpawnKind = value?.Kind == "Respawn" ? 1 : 0; routeSpawnYaw = (value?.Yaw ?? 0) / 90;
                var at = value?.Position ?? default;
                routeX = Metres(at.X); routeY = Metres(at.Y); routeZ = Metres(at.Z);
            }
            routeSpawnModule = RouteChoice("Spawn module", routeSpawnModule, display.Modules.Select(m => m.Id));
            routeSpawnKind = GUILayout.Toolbar(routeSpawnKind, new[] { "Start", "Respawn" });
            if (Button("Use selected module centre"))
            {
                var module = display.Modules.FirstOrDefault(m => m.Id == routeSpawnModule);
                if (module == null) status = "Choose a spawn module first.";
                else { routeX = Metres(module.Position.X); routeY = Metres(module.Position.Y); routeZ = Metres(module.Position.Z); }
            }
            GUILayout.BeginHorizontal(); routeX = Field("X m", routeX); routeY = Field("Y m", routeY); routeZ = Field("Z m", routeZ); GUILayout.EndHorizontal();
            routeSpawnYaw = GUILayout.Toolbar(routeSpawnYaw, new[] { "0°", "90°", "180°", "270°" });
            if (Button(value == null ? "Add spawn anchor" : "Apply spawn anchor"))
                Edit(value == null ? "Add spawn anchor" : "Edit spawn anchor", draft =>
                {
                    var at = new CoursePoint(Millimetres(routeX), Millimetres(routeY), Millimetres(routeZ));
                    string kind = routeSpawnKind == 0 ? "Start" : "Respawn";
                    if (value == null) routeSpawn = WorkshopRoutes.AddSpawn(draft, Catalogue, routeSpawnModule, kind, at, routeSpawnYaw * 90);
                    else WorkshopRoutes.SetSpawn(draft, Catalogue, value.Id, routeSpawnModule, kind, at, routeSpawnYaw * 90);
                });
            if (value != null)
            {
                if (Button("New spawn anchor")) { routeSpawn = ""; routeLoadKey = ""; }
                if (Button("Delete spawn anchor")) Edit("Delete spawn anchor", draft => WorkshopRoutes.DeleteSpawn(draft, value.Id));
            }
            GUILayout.Label("Start anchors: " + display.Spawns.Count(s => s.Kind == "Start") + " / " +
                PlayRequirements.MaxOccupants + " required");
            if (PlayRequirements.Capability == CourseCapability.Arena)
                GUILayout.Label("Respawn anchors: " + display.Spawns.Count(s => s.Kind == "Respawn") + " / " +
                    PlayRequirements.MaxOccupants + " required");
            GUILayout.Label("Keep 0.5 m clearance and 1 m between anchors of the same kind. Player/team allocation follows the selected mode; validation checks capacity.");
        }

        void CheckpointRoutePanel()
        {
            routeCheckpoint = RouteChoice("Checkpoint", routeCheckpoint,
                display.Checkpoints.Where(c => c.ModuleId == routeModule).Select(c => c.Id));
            var value = display.Checkpoints.FirstOrDefault(c => c.Id == routeCheckpoint && c.ModuleId == routeModule);
            if (ReloadRoute("checkpoint/" + routeModule + "/" + (value?.Id ?? "")))
            {
                routeAnchor = value?.RecoveryAnchorId ?? ""; routeArrival = value?.EntryConnectionId ?? "";
                ReplaceRouteList(routePuzzles, value?.PuzzleIds ?? Enumerable.Empty<string>());
            }
            routeAnchor = RouteChoice("Safe return anchor", routeAnchor, display.RecoveryAnchors.Select(r => r.Id));
            routeArrival = RouteChoice("Arrival connection", routeArrival,
                display.Connections.Where(c => c.ToModuleId == routeModule || c.Bidirectional && c.FromModuleId == routeModule).Select(c => c.Id), "Any arrival");
            RouteReferences("Required puzzles in order", routePuzzles, display.Puzzles.Select(p => p.Id), true);
            if (Button(value == null ? "Add checkpoint" : "Apply checkpoint"))
                Edit(value == null ? "Add checkpoint" : "Edit checkpoint", draft =>
                {
                    if (value == null) routeCheckpoint = WorkshopRoutes.AddCheckpoint(draft, routeModule, routeAnchor, routeArrival, routePuzzles);
                    else WorkshopRoutes.SetCheckpoint(draft, value.Id, routeAnchor, routeArrival, routePuzzles);
                });
            if (value != null)
            {
                if (Button("New checkpoint on this module")) { routeCheckpoint = ""; routeLoadKey = ""; }
                GUILayout.Label("Deleting removes this stop from every branch. Linked physical controls must be moved or deleted first.");
                if (Button("Delete checkpoint from all routes"))
                    Edit("Delete checkpoint from all routes", draft => WorkshopRoutes.DeleteCheckpointFromRoutes(draft, value.Id));
            }
            GUILayout.Label("Use Add puzzle + letters above for a new objective, then link it here before adding physical controls.");
        }

        void SectionRoutePanel()
        {
            routeSection = RouteChoice("Route section", routeSection, display.RouteSections.Select(s => s.Id));
            var section = display.RouteSections.FirstOrDefault(s => s.Id == routeSection);
            if (ReloadRoute("section/" + (section?.Id ?? "")))
            {
                routeEntry = section?.EntryModuleId ?? ""; routeExit = section?.ExitModuleId ?? "";
                if (section == null || !section.Alternatives.Any(a => a.Id == routeBranch)) routeBranch = "";
                routeBranchLoadRevision = -1; routeConnections.Clear(); routeCheckpoints.Clear();
            }
            routeEntry = RouteChoice("Route start", routeEntry, display.Modules.Select(m => m.Id));
            routeExit = RouteChoice("Route finish", routeExit, display.Modules.Select(m => m.Id));
            if (Button(section == null ? "Add route + first branch" : "Apply route endpoints"))
                Edit(section == null ? "Add route section" : "Edit route endpoints", draft =>
                {
                    if (section == null) routeSection = WorkshopRoutes.AddSection(draft, routeEntry, routeExit);
                    else WorkshopRoutes.SetSectionEndpoints(draft, section.Id, routeEntry, routeExit);
                });
            if (section == null) return;
            if (Button("New route section")) { routeSection = ""; routeLoadKey = ""; return; }
            string previous = routeBranch;
            routeBranch = RouteChoice("Branch", routeBranch, section.Alternatives.Select(a => a.Id));
            var branch = section.Alternatives.FirstOrDefault(a => a.Id == routeBranch);
            if (previous != routeBranch || routeBranchLoadRevision != History.Revision || routeBranchLoadId != routeBranch)
            {
                routeBranchLoadRevision = History.Revision; routeBranchLoadId = routeBranch;
                ReplaceRouteList(routeConnections, branch?.ConnectionIds ?? Enumerable.Empty<string>());
                ReplaceRouteList(routeCheckpoints, branch?.CheckpointIds ?? Enumerable.Empty<string>());
                routeWeight = (branch?.Weight ?? 1).ToString(CultureInfo.InvariantCulture);
            }
            if (Button("Add weighted branch"))
                Edit("Add weighted branch", draft => routeBranch = WorkshopRoutes.AddAlternative(draft, section.Id));
            if (branch != null)
            {
                GUILayout.Label("One branch is selected per section and lap by the seed. Larger weights are more likely.");
                routeWeight = Field("Relative weight", routeWeight);
                RouteReferences("Connections in travel order", routeConnections, display.Connections.Select(c => c.Id), true);
                DrawRouteDirections(section.EntryModuleId);
                RouteReferences("Checkpoints in travel order", routeCheckpoints, display.Checkpoints.Select(c => c.Id), true);
                if (Button("Apply branch"))
                    Edit("Edit route branch", draft => WorkshopRoutes.SetAlternative(draft, section.Id, branch.Id,
                        routeConnections, routeCheckpoints, int.Parse(routeWeight, CultureInfo.InvariantCulture)));
                if (Button("Delete branch"))
                    Edit("Delete branch", draft => WorkshopRoutes.DeleteAlternative(draft, section.Id, branch.Id));
            }
            GUILayout.Label("Reverse affects every branch. Repair checkpoint arrivals explicitly, then review gate order and linked lap endpoints.");
            if (Button("Reverse saved route"))
                Edit("Reverse route; review gates and laps", draft => WorkshopRoutes.ReverseSection(draft, section.Id));
            if (Button("Delete route from all laps"))
                Edit("Delete route from all laps", draft => WorkshopRoutes.DeleteSectionFromLaps(draft, section.Id));
        }

        int routeBranchLoadRevision = -1;
        string routeBranchLoadId = "";

        void DrawRouteDirections(string entry)
        {
            string current = entry;
            foreach (string id in routeConnections)
            {
                var edge = display.Connections.FirstOrDefault(c => c.Id == id);
                if (edge == null) { GUILayout.Label(id + " · missing connection"); break; }
                bool forward = edge.FromModuleId == current;
                bool reverse = edge.Bidirectional && edge.ToModuleId == current;
                if (!forward && !reverse) { GUILayout.Label(id + " · cannot continue from " + current); break; }
                string next = forward ? edge.ToModuleId : edge.FromModuleId;
                GUILayout.Label(current + " → " + next + (forward ? " · forward" : " · reverse"));
                current = next;
            }
        }

        void LapRoutePanel()
        {
            routeLap = GUILayout.Toolbar(routeLap - 1, new[] { "Lap 1", "Lap 2", "Lap 3" }) + 1;
            var value = display.Laps.FirstOrDefault(l => l.Number == routeLap);
            if (ReloadRoute("lap/" + routeLap))
            {
                routeEntry = value?.StartModuleId ?? ""; routeExit = value?.FinishModuleId ?? ""; routeStory = value?.NarrativeId ?? "";
                ReplaceRouteList(routeSections, value?.SectionIds ?? Enumerable.Empty<string>());
            }
            routeEntry = RouteChoice("Lap start", routeEntry, display.Modules.Select(m => m.Id));
            routeExit = RouteChoice("Lap finish", routeExit, display.Modules.Select(m => m.Id));
            routeStory = RouteChoice("Lap story", routeStory, Catalogue.NarrativeEvents);
            RouteReferences("Route sections in order", routeSections, display.RouteSections.Select(s => s.Id), true);
            if (Button(value == null ? "Add lap" : "Apply lap"))
                Edit("Save lap " + routeLap, draft => WorkshopRoutes.SetLap(draft, Catalogue, routeLap,
                    routeEntry, routeExit, routeStory, routeSections));
            if (value != null && Button("Delete lap"))
                Edit("Delete lap " + routeLap, draft => WorkshopRoutes.DeleteLap(draft, routeLap));
            GUILayout.Label("Race courses require all three laps. Each lap starts at the preceding finish and ends with an ordered checkpoint.");
        }

        static void ReplaceRouteList(List<string> target, IEnumerable<string> source) { target.Clear(); target.AddRange(source); }

        void RouteReferences(string label, List<string> selected, IEnumerable<string> available, bool ordered)
        {
            GUILayout.Label(label);
            for (int i = 0; i < selected.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((ordered ? (i + 1) + ". " : "") + selected[i], GUILayout.ExpandWidth(true));
                if (ordered && i > 0 && Button("↑", "Move " + selected[i] + " earlier"))
                { string value = selected[i]; selected[i] = selected[i - 1]; selected[i - 1] = value; }
                if (ordered && i < selected.Count - 1 && Button("↓", "Move " + selected[i] + " later"))
                { string value = selected[i]; selected[i] = selected[i + 1]; selected[i + 1] = value; }
                bool remove = Button("×", "Remove " + selected[i] + " from this list");
                GUILayout.EndHorizontal();
                if (remove) { selected.RemoveAt(i); i--; }
            }
            string added = RouteChoice("Add to " + label.ToLowerInvariant(), "", available.Where(id => !selected.Contains(id)));
            if (added.Length > 0) selected.Add(added);
        }

        string RouteChoice(string label, string current, IEnumerable<string> available, string emptyChoice = null)
        {
            var values = available.Distinct(StringComparer.Ordinal).ToArray();
            GUILayout.Label(label);
            if (Button((current.Length == 0 ? emptyChoice ?? "Choose…" : current) + " ▾", label))
            { routeChoiceOpen = routeChoiceOpen == label ? "" : label; routeChoiceSearch = ""; }
            if (routeChoiceOpen != label) return current;
            if (emptyChoice != null && Button(emptyChoice)) { routeChoiceOpen = ""; return ""; }
            if (values.Length > 8) routeChoiceSearch = Field("Find " + label.ToLowerInvariant(), routeChoiceSearch);
            foreach (string id in values.Where(id => id.IndexOf(routeChoiceSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                if (Button(id)) { routeChoiceOpen = ""; return id; }
            if (values.Length == 0) GUILayout.Label("Add an authored source first.");
            return current;
        }
    }
}
