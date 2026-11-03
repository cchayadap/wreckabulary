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
        Rect routeContentRect, routeTabsRect;
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

        static readonly int[] RouteOrder = { 4, 1, 2, 3, 0 };
        static readonly string[] RouteTabs = { "Start points", "Checkpoints", "Paths", "Laps", "Safe-return points" };

        void RouteInspector()
        {
            GUILayout.Space(4);
            bool race = RaceSelected;
            if (!Section("routes", race ? "Start & finish" : "Start points", ref routesExpanded)) return;
            GUILayout.BeginVertical();
            int tab = race ? routeTab : 4;
            if (race)
            {
                int shown = RouteNavigation(Math.Max(0, Array.IndexOf(RouteOrder, routeTab)), RouteTabs, 2);
                if (Event.current.type == EventType.Repaint) routeTabsRect = GUILayoutUtility.GetLastRect();
                if (RouteOrder[shown] != routeTab) { routeTab = RouteOrder[shown]; routeLoadKey = ""; routeChoiceOpen = ""; }
                tab = routeTab;
            }
            if (tab < 2)
                routeModule = RouteChoice("Room", routeModule, display.Modules.Select(m => m.Id));
            if (tab == 0) RecoveryRoutePanel();
            else if (tab == 1) CheckpointRoutePanel();
            else if (tab == 2) SectionRoutePanel();
            else if (tab == 3) LapRoutePanel();
            else SpawnRoutePanel();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint) routeContentRect = GUILayoutUtility.GetLastRect();
        }

        int RouteNavigation(int selected, string[] labels, int maximum)
        {
            int columns = InspectorColumns(labels, maximum);
            return GUILayout.SelectionGrid(selected, labels, columns, GUILayout.Width(RowWidth),
                GUILayout.MinHeight(ui.RowHeight * Mathf.CeilToInt((float)labels.Length / columns)));
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
            GUILayout.Label("Players who fall or get stuck come back to a safe-return point.", ui.Wrapped, GUILayout.Width(RowWidth));
            routeRecovery = RouteChoice("Safe-return point", routeRecovery,
                display.RecoveryAnchors.Where(r => r.ModuleId == routeModule).Select(r => r.Id));
            if (WideButton("Add safe-return point at room centre"))
                Edit("Add safe-return point", draft =>
                {
                    var module = draft.Course.Modules.SingleOrDefault(m => m.Id == routeModule)
                        ?? throw new ArgumentException("Choose a room first.");
                    routeRecovery = WorkshopRoutes.AddRecovery(draft, Catalogue, module.Id, module.Position);
                });
            var value = display.RecoveryAnchors.FirstOrDefault(r => r.Id == routeRecovery && r.ModuleId == routeModule);
            if (value == null) { GUILayout.Label("Choose a safe-return point to edit it.", ui.Wrapped, GUILayout.Width(RowWidth)); return; }
            LoadRecoveryRoute(value);
            Triple("Position m", "Safe-return point", ref routeX, ref routeY, ref routeZ, GridMetres, .1);
            routeRadius = Field("Radius m", routeRadius, .25, .05, "Safe radius m", .5, 5);
            RouteReferences("Available after puzzles", routeAfter, display.Puzzles.Select(p => p.Id), false);
            if (WideButton("Apply safe-return point"))
                Edit("Edit safe-return point", draft => WorkshopRoutes.SetRecovery(draft, Catalogue, value.Id,
                    new CoursePoint(Millimetres(routeX), Millimetres(routeY), Millimetres(routeZ)), Millimetres(routeRadius), routeAfter));
            if (WideButton("Delete unused safe-return point"))
                Edit("Delete safe-return point", draft => WorkshopRoutes.DeleteRecovery(draft, value.Id));
        }

        void LoadRecoveryRoute(CourseRecovery value)
        {
            if (!ReloadRoute("recovery/" + value.Id)) return;
            routeX = Metres(value.Position.X); routeY = Metres(value.Position.Y); routeZ = Metres(value.Position.Z);
            routeRadius = Metres(value.RadiusMm); ReplaceRouteList(routeAfter, value.AfterPuzzleIds);
        }

        void SpawnRoutePanel()
        {
            routeSpawn = RouteChoice("Start point", routeSpawn, display.Spawns.Select(s => s.Id));
            var value = display.Spawns.FirstOrDefault(s => s.Id == routeSpawn);
            if (ReloadRoute("spawn/" + (value?.Id ?? "")))
            {
                routeSpawnModule = value?.ModuleId ?? "";
                routeSpawnKind = value?.Kind == "Respawn" ? 1 : 0; routeSpawnYaw = (value?.Yaw ?? 0) / 90;
                var at = value?.Position ?? default;
                routeX = Metres(at.X); routeY = Metres(at.Y); routeZ = Metres(at.Z);
            }
            routeSpawnModule = RouteChoice("Room", routeSpawnModule, display.Modules.Select(m => m.Id));
            routeSpawnKind = GUILayout.Toolbar(routeSpawnKind, new[] { "Start point", "Respawn point" }, GUILayout.Width(RowWidth));
            if (WideButton("Use room centre"))
            {
                var module = display.Modules.FirstOrDefault(m => m.Id == routeSpawnModule);
                if (module == null) status = "Choose a room first.";
                else { routeX = Metres(module.Position.X); routeY = Metres(module.Position.Y); routeZ = Metres(module.Position.Z); }
            }
            Triple("Position m", "Spawn", ref routeX, ref routeY, ref routeZ, GridMetres, .1);
            routeSpawnYaw = RouteNavigation(routeSpawnYaw, new[] { "0°", "90°", "180°", "270°" }, 4);
            string kindLabel = routeSpawnKind == 0 ? "start point" : "respawn point";
            if (WideButton(value == null ? "Add " + kindLabel : "Apply " + kindLabel))
                Edit(value == null ? "Add " + kindLabel : "Edit " + kindLabel, draft =>
                {
                    var at = new CoursePoint(Millimetres(routeX), Millimetres(routeY), Millimetres(routeZ));
                    string kind = routeSpawnKind == 0 ? "Start" : "Respawn";
                    if (value == null) routeSpawn = WorkshopRoutes.AddSpawn(draft, Catalogue, routeSpawnModule, kind, at, routeSpawnYaw * 90);
                    else WorkshopRoutes.SetSpawn(draft, Catalogue, value.Id, routeSpawnModule, kind, at, routeSpawnYaw * 90);
                });
            if (value != null)
            {
                if (WideButton("New point")) { routeSpawn = ""; routeLoadKey = ""; }
                if (WideButton("Delete " + (value.Kind == "Respawn" ? "respawn point" : "start point")))
                    Edit("Delete point", draft => WorkshopRoutes.DeleteSpawn(draft, value.Id));
            }
            GUILayout.Label("Start points: " + display.Spawns.Count(s => s.Kind == "Start") + " of " +
                PlayRequirements.MaxOccupants + " needed", ui.Wrapped, GUILayout.Width(RowWidth));
            if (PlayRequirements.Capability == CourseCapability.Arena)
                GUILayout.Label("Respawn points: " + display.Spawns.Count(s => s.Kind == "Respawn") + " of " +
                    PlayRequirements.MaxOccupants + " needed", ui.Wrapped, GUILayout.Width(RowWidth));
            GUILayout.Label("Keep points 1 m apart and 0.5 m from walls. Players are placed by the chosen mode.", ui.Wrapped, GUILayout.Width(RowWidth));
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
            routeAnchor = RouteChoice("Safe-return point", routeAnchor, display.RecoveryAnchors.Select(r => r.Id));
            routeArrival = RouteChoice("Arrives through", routeArrival,
                display.Connections.Where(c => c.ToModuleId == routeModule || c.Bidirectional && c.FromModuleId == routeModule).Select(c => c.Id), "Any arrival");
            RouteReferences("Required puzzles in order", routePuzzles, display.Puzzles.Select(p => p.Id), true);
            if (WideButton(value == null ? "Add checkpoint" : "Apply checkpoint"))
                Edit(value == null ? "Add checkpoint" : "Edit checkpoint", draft =>
                {
                    if (value == null) routeCheckpoint = WorkshopRoutes.AddCheckpoint(draft, routeModule, routeAnchor, routeArrival, routePuzzles);
                    else WorkshopRoutes.SetCheckpoint(draft, value.Id, routeAnchor, routeArrival, routePuzzles);
                });
            if (value != null)
            {
                if (WideButton("New checkpoint in this room")) { routeCheckpoint = ""; routeLoadKey = ""; }
                GUILayout.Label("Deleting removes this checkpoint from every path. Move or delete its puzzle parts first.", ui.Wrapped, GUILayout.Width(RowWidth));
                if (WideButton("Delete checkpoint from all paths"))
                    Edit("Delete checkpoint from all paths", draft => WorkshopRoutes.DeleteCheckpointFromRoutes(draft, value.Id));
            }
            GUILayout.Label("Make puzzles in Fill, then link them here before adding their parts.", ui.Wrapped, GUILayout.Width(RowWidth));
        }

        void SectionRoutePanel()
        {
            routeSection = RouteChoice("Path", routeSection, display.RouteSections.Select(s => s.Id));
            var section = display.RouteSections.FirstOrDefault(s => s.Id == routeSection);
            if (ReloadRoute("section/" + (section?.Id ?? "")))
            {
                routeEntry = section?.EntryModuleId ?? ""; routeExit = section?.ExitModuleId ?? "";
                if (section == null || !section.Alternatives.Any(a => a.Id == routeBranch)) routeBranch = "";
                routeBranchLoadRevision = -1; routeConnections.Clear(); routeCheckpoints.Clear();
            }
            routeEntry = RouteChoice("Path start", routeEntry, display.Modules.Select(m => m.Id));
            routeExit = RouteChoice("Path end", routeExit, display.Modules.Select(m => m.Id));
            if (WideButton(section == null ? "Add path" : "Apply path ends"))
                Edit(section == null ? "Add path" : "Edit path ends", draft =>
                {
                    if (section == null) routeSection = WorkshopRoutes.AddSection(draft, routeEntry, routeExit);
                    else WorkshopRoutes.SetSectionEndpoints(draft, section.Id, routeEntry, routeExit);
                });
            if (section == null) return;
            if (WideButton("New path")) { routeSection = ""; routeLoadKey = ""; return; }
            string previous = routeBranch;
            routeBranch = RouteChoice("Alternative", routeBranch, section.Alternatives.Select(a => a.Id));
            var branch = section.Alternatives.FirstOrDefault(a => a.Id == routeBranch);
            if (previous != routeBranch || routeBranchLoadRevision != History.Revision || routeBranchLoadId != routeBranch)
            {
                routeBranchLoadRevision = History.Revision; routeBranchLoadId = routeBranch;
                ReplaceRouteList(routeConnections, branch?.ConnectionIds ?? Enumerable.Empty<string>());
                ReplaceRouteList(routeCheckpoints, branch?.CheckpointIds ?? Enumerable.Empty<string>());
                routeWeight = (branch?.Weight ?? 1).ToString(CultureInfo.InvariantCulture);
            }
            if (WideButton("Add alternative path"))
                Edit("Add alternative path", draft => routeBranch = WorkshopRoutes.AddAlternative(draft, section.Id));
            if (branch != null)
            {
                GUILayout.Label("Each lap takes one alternative, picked by the layout. Bigger weights are picked more often.", ui.Wrapped, GUILayout.Width(RowWidth));
                if (section.Alternatives.Count > 1) routeWeight = Field("Weight", routeWeight, 1, 1, "Relative weight", 1, 10000);
                RouteReferences("Connections in travel order", routeConnections, display.Connections.Select(c => c.Id), true);
                DrawRouteDirections(section.EntryModuleId);
                RouteReferences("Checkpoints in travel order", routeCheckpoints, display.Checkpoints.Select(c => c.Id), true);
                if (WideButton("Apply alternative path"))
                    Edit("Edit alternative path", draft => WorkshopRoutes.SetAlternative(draft, section.Id, branch.Id,
                        routeConnections, routeCheckpoints, int.Parse(routeWeight, CultureInfo.InvariantCulture)));
                if (WideButton("Delete alternative path"))
                    Edit("Delete alternative path", draft => WorkshopRoutes.DeleteAlternative(draft, section.Id, branch.Id));
            }
            GUILayout.Label("Reversing changes every alternative. Then check checkpoint arrivals, door order and lap ends.", ui.Wrapped, GUILayout.Width(RowWidth));
            if (WideButton("Reverse path"))
                Edit("Reverse path; check doors and laps", draft => WorkshopRoutes.ReverseSection(draft, section.Id));
            if (WideButton("Delete path from all laps"))
                Edit("Delete path from all laps", draft => WorkshopRoutes.DeleteSectionFromLaps(draft, section.Id));
        }

        int routeBranchLoadRevision = -1;
        string routeBranchLoadId = "";

        void DrawRouteDirections(string entry)
        {
            string current = entry;
            foreach (string id in routeConnections)
            {
                var edge = display.Connections.FirstOrDefault(c => c.Id == id);
                if (edge == null) { GUILayout.Label(id + " · missing connection", ui.Wrapped, GUILayout.Width(RowWidth)); break; }
                bool forward = edge.FromModuleId == current;
                bool reverse = edge.Bidirectional && edge.ToModuleId == current;
                if (!forward && !reverse) { GUILayout.Label(ReferenceCaptionFor("connection", id) + " · cannot continue from " + RoomCaption(current), ui.Wrapped, GUILayout.Width(RowWidth)); break; }
                string next = forward ? edge.ToModuleId : edge.FromModuleId;
                GUILayout.Label(RoomCaption(current) + " → " + RoomCaption(next) + (forward ? " · forward" : " · reverse"), ui.Wrapped, GUILayout.Width(RowWidth));
                current = next;
            }
        }

        void LapRoutePanel()
        {
            routeLap = RouteNavigation(routeLap - 1, new[] { "Lap 1", "Lap 2", "Lap 3" }, 3) + 1;
            var value = display.Laps.FirstOrDefault(l => l.Number == routeLap);
            if (ReloadRoute("lap/" + routeLap))
            {
                routeEntry = value?.StartModuleId ?? ""; routeExit = value?.FinishModuleId ?? ""; routeStory = value?.NarrativeId ?? "";
                ReplaceRouteList(routeSections, value?.SectionIds ?? Enumerable.Empty<string>());
            }
            routeEntry = RouteChoice("Lap start", routeEntry, display.Modules.Select(m => m.Id));
            routeExit = RouteChoice("Lap finish", routeExit, display.Modules.Select(m => m.Id));
            routeStory = RouteChoice("Lap message", routeStory, Catalogue.NarrativeEvents);
            RouteReferences("Paths in order", routeSections, display.RouteSections.Select(s => s.Id), true);
            if (WideButton(value == null ? "Add lap" : "Apply lap"))
                Edit("Save lap " + routeLap, draft => WorkshopRoutes.SetLap(draft, Catalogue, routeLap,
                    routeEntry, routeExit, routeStory, routeSections));
            if (value != null && WideButton("Delete lap"))
                Edit("Delete lap " + routeLap, draft => WorkshopRoutes.DeleteLap(draft, routeLap));
            GUILayout.Label("Races need all three laps. Each lap starts where the last one finished and ends at a checkpoint.", ui.Wrapped, GUILayout.Width(RowWidth));
        }

        static void ReplaceRouteList(List<string> target, IEnumerable<string> source) { target.Clear(); target.AddRange(source); }

        void RouteReferences(string label, List<string> selected, IEnumerable<string> available, bool ordered)
        {
            GUILayout.Label(label, ui.Wrapped, GUILayout.Width(RowWidth));
            for (int i = 0; i < selected.Count; i++)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
                GUILayout.Label((ordered ? (i + 1) + ". " : "") + ReferenceCaptionFor(ReferenceKind(label), selected[i]), ui.Wrapped, GUILayout.ExpandWidth(true));
                if (ordered && i > 0 && Button("↑", "Move earlier", ui.RowHeight))
                { string value = selected[i]; selected[i] = selected[i - 1]; selected[i - 1] = value; }
                if (ordered && i < selected.Count - 1 && Button("↓", "Move later", ui.RowHeight))
                { string value = selected[i]; selected[i] = selected[i + 1]; selected[i + 1] = value; }
                bool remove = Button("×", "Remove from this list", ui.RowHeight);
                GUILayout.EndHorizontal();
                if (remove) { selected.RemoveAt(i); i--; }
            }
            string added = RouteChoice("Add to " + label.ToLowerInvariant(), "", available.Where(id => !selected.Contains(id)));
            if (added.Length > 0) selected.Add(added);
        }

        string RouteChoice(string label, string current, IEnumerable<string> available, string emptyChoice = null)
        {
            var values = available.Distinct(StringComparer.Ordinal).ToArray();
            if (ChoiceRow(label, (current.Length == 0 ? emptyChoice ?? "Choose…" : ReferenceCaptionFor(ReferenceKind(label), current)) + " ▾"))
            { routeChoiceOpen = routeChoiceOpen == label ? "" : label; routeChoiceSearch = ""; }
            if (routeChoiceOpen != label) return current;
            if (emptyChoice != null && InspectorChoiceButton(label, "none", emptyChoice)) { routeChoiceOpen = ""; return ""; }
            if (values.Length > 8) routeChoiceSearch = Field("Find", routeChoiceSearch, name: "Find " + label.ToLowerInvariant());
            foreach (string id in values.Where(id => ChoiceMatches(id, ReferenceCaptionFor(ReferenceKind(label), id), routeChoiceSearch)))
                if (InspectorChoiceButton(label, id, ReferenceCaptionFor(ReferenceKind(label), id))) { routeChoiceOpen = ""; return id; }
            if (values.Length == 0) GUILayout.Label("Nothing to choose yet.", ui.Wrapped, GUILayout.Width(RowWidth));
            return current;
        }
    }
}
