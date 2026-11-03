using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        enum WorkshopStep { Build, Objects, Puzzles, Rules, Share }
        WorkshopStep step, lastFill = WorkshopStep.Objects;
        static readonly string[] StepTabs = { "Build", "Fill", "Rules", "Test & Share" };
        static readonly string[] StepHints = { "Add rooms and floors", "Add objects and puzzles to a room",
            "Choose modes, players, start and finish", "Check, test, save and share" };
        float inspectorWidth = 300;
        Rect inspectorScrollRect;
        bool fillAdvanced, rulesAdvanced, shareAdvanced;
        float InspectorContentWidth => Mathf.Max(1, inspectorWidth - panel.padding.horizontal -
            GUI.skin.verticalScrollbar.fixedWidth - GUI.skin.verticalScrollbar.margin.left - 4);

        int InspectorColumns(string[] labels, int maximum)
        {
            float cell = labels.Max(label => GUI.skin.button.CalcSize(new GUIContent(label)).x) + GUI.skin.button.margin.horizontal;
            return Mathf.Max(1, Mathf.Min(maximum, Mathf.FloorToInt(InspectorContentWidth / cell)));
        }
        bool inspectorAdvancedIds, newPuzzle, puzzleLinks, placementPickerOpen, roomObjectsOpen;
        string inspectorChoiceOpen = "", inspectorChoiceSearch = "", recipeSearch = "";

        void ShowStep(WorkshopStep next)
        {
            if (step == next) return;
            CancelGesture(); step = next; inspectorScroll = Vector2.zero;
            if (next == WorkshopStep.Objects || next == WorkshopStep.Puzzles) lastFill = next;
            inspectorChoiceOpen = siteChoiceOpen = routeChoiceOpen = "";
            if (Event.current != null) GUI.FocusControl(null);
            workshopTyping = false;
        }

        static int StepTab(WorkshopStep value) => value == WorkshopStep.Build ? 0 :
            value == WorkshopStep.Objects || value == WorkshopStep.Puzzles ? 1 : value == WorkshopStep.Rules ? 2 : 3;

        List<CourseModule> SelectedRooms() => display.Modules.Where(module => History.Current.Selection.Contains(module.Id)).ToList();

        string SelectionHint()
        {
            int selected = History.Current.Selection.Count(id => display.Modules.Any(module => module.Id == id));
            if (step == WorkshopStep.Build)
                return placing != null ? "Click the map to place " + ObjectLabel(placing) + " · Esc cancels." :
                    selected == 0 ? "Pick a room to add, or click a room in the map." : "";
            if (step == WorkshopStep.Objects) return selected == 0 ? "Select a room to add objects." : "";
            if (step == WorkshopStep.Puzzles) return selected == 0 ? "Select a room to add puzzles." : "";
            return "";
        }

        void StepPanel()
        {
            rowWidth = InspectorContentWidth;
            StepTabsRow();
            if (controllerTools) ControllerPanel();
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            string hint = SelectionHint();
            if (hint.Length != 0) Hint(hint);
            if (step == WorkshopStep.Build) BuildStep();
            else if (step == WorkshopStep.Objects || step == WorkshopStep.Puzzles) FillStep();
            else if (step == WorkshopStep.Rules) RulesStep();
            else ShareStep();
            GUILayout.EndScrollView();
            if (Event.current.type == EventType.Repaint) inspectorScrollRect = GUILayoutUtility.GetLastRect();
        }

        void StepTabsRow()
        {
            int current = StepTab(step);
            float available = inspectorWidth - panel.padding.horizontal - 4;
            float total = StepTabs.Sum(label => GUI.skin.button.CalcSize(new GUIContent(label)).x + GUI.skin.button.margin.horizontal);
            int columns = total <= available ? StepTabs.Length : 2;
            for (int i = 0; i < StepTabs.Length; i++)
            {
                if (i % columns == 0) GUILayout.BeginHorizontal();
                GUI.SetNextControlName("Workshop step " + StepTabs[i]);
                if (ui.Button(new GUIContent(StepTabs[i], StepHints[i]), i == current ? ui.Selected : null,
                    GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                    ShowStep(i == 0 ? WorkshopStep.Build : i == 1 ? lastFill : i == 2 ? WorkshopStep.Rules : WorkshopStep.Share);
                if (i % columns == columns - 1 || i == StepTabs.Length - 1) GUILayout.EndHorizontal();
            }
            GUILayout.Space(ui.Gap);
        }

        void BuildStep()
        {
            var selected = SelectedRooms();
            if (selected.Count > 0) SelectedRoomPanel(selected);
            if (Section("rooms", "Add a room · " + Catalogue.Modules.Count, true)) RoomPicker();
            if (Section("floors", "Floors · " + display.Layers.Count, true)) FloorPanel();
            if (Advanced(ref advanced)) BuildAdvanced(selected);
            GUILayout.Space(ui.Gap);
            if (Section("view", "Mouse & controller help", ref showWorkshopHelp)) HelpPanel();
        }

        void RoomPicker()
        {
            if (Catalogue.Modules.Count > 8) search = Field("Find", search, name: "Find room");
            var rooms = Catalogue.Modules.Values.Where(item => item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                ObjectLabel(item.Id).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            foreach (var definition in rooms)
            {
                GUI.SetNextControlName(ObjectLabel(definition.Id));
                if (ui.Button(new GUIContent(ObjectLabel(definition.Id) + " · " + Metres(definition.WidthMm) + " × " + Metres(definition.DepthMm) + " m",
                    "Choose, then click the map to place it"), placing == definition.Id ? ui.Selected : null,
                    GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(RowWidth)))
                { CancelGesture(); placing = definition.Id; status = "Click the map to place " + ObjectLabel(placing) + " · Esc cancels"; }
            }
            if (rooms.Count == 0) GUILayout.Label("No matching rooms.", ui.Wrapped, GUILayout.Width(RowWidth));
        }

        void SelectedRoomPanel(List<CourseModule> selected)
        {
            GUILayout.Label(selected.Count == 1 ? RoomCaption(selected[0].Id) : selected.Count + " rooms", heading, GUILayout.Width(RowWidth));
            GUILayout.BeginHorizontal();
            if (ToolButton("Rotate", LobbyIcons.Turn, "Rotate · R")) Edit("Rotate", draft => WorkshopCommands.RotateSelection(draft, Pivot(selected), 1));
            if (ToolButton("Duplicate", LobbyIcons.Duplicate, "Duplicate · Ctrl+D")) Edit("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(draft.GridMm * 2, 0, draft.GridMm * 2)));
            if (ToolButton("Delete", LobbyIcons.Delete, "Delete · Del")) Edit("Delete", WorkshopCommands.Delete);
            GUILayout.EndHorizontal();
            if (selected.Count == 1) RoomSize(selected[0]);
            if (selected.Count == 2 && WideButton("Connect rooms", "Add a walkway, bridge or stairs between the two rooms")) Connect(selected[0], selected[1]);
            if (selected.Count == 1 && WideButton("Fill this room ›", "Add objects and puzzles to this room")) ShowStep(lastFill);
            var links = display.Connections.Where(link => selected.Any(module => module.Id == link.FromModuleId || module.Id == link.ToModuleId)).ToList();
            if (Section("connections", "Connections · " + links.Count, false))
            {
                foreach (var link in links)
                    GUILayout.Label(ReferenceCaptionFor("connection", link.Id) + " · " + link.Kind +
                        (link.GatePuzzleId.Length == 0 ? "" : " · opens after " + PuzzleCaption(link.GatePuzzleId)), ui.Wrapped, GUILayout.Width(RowWidth));
                if (links.Count == 0) GUILayout.Label("None yet · select two rooms, then Connect rooms.", ui.Wrapped, GUILayout.Width(RowWidth));
            }
            GUILayout.Space(ui.Gap);
        }

        void BuildAdvanced(List<CourseModule> selected)
        {
            if (selected.Count > 0)
            {
                GUILayout.BeginHorizontal();
                if (ToolButton("Group", LobbyIcons.Group, "Group the selected rooms")) Edit("Group", draft => WorkshopCommands.Group(draft, WorkshopCommands.Unique("group", draft.Groups.Keys)));
                if (ToolButton("Ungroup", LobbyIcons.Ungroup, "Ungroup the selected rooms")) Edit("Ungroup", WorkshopCommands.Ungroup);
                GUILayout.EndHorizontal();
                if (WideButton("Move to this floor", "Move the selection to the floor you are viewing"))
                    Edit("Change floor", draft => WorkshopCommands.ChangeLayer(draft, draft.ActiveLayer));
                Pair("Position m", "Room position", "X", ref xInput, "Z", ref zInput, GridMetres, .1);
                if (WideButton("Move to position")) Edit("Move", draft => WorkshopCommands.Move(draft,
                    new CoursePoint(Millimetres(xInput) - selected[0].Position.X, 0, Millimetres(zInput) - selected[0].Position.Z), false));
            }
            FloorAdvanced();
        }

        void HelpPanel()
        {
            GUILayout.Label("Drag to move · Shift adds to the selection\nDrag room edges to resize\nDrag a value's name to adjust it\n" +
                "Right drag orbits · Middle drag pans\nWheel zooms · F frames the selection", ui.Wrapped, GUILayout.Width(RowWidth));
            ControllerPanel();
        }

        void FillStep()
        {
            int fill = step == WorkshopStep.Puzzles ? 1 : 0;
            int next = GUILayout.Toolbar(fill, new[] { "Objects", "Puzzles" }, GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(RowWidth));
            if (next != fill) ShowStep(next == 1 ? WorkshopStep.Puzzles : WorkshopStep.Objects);
            var selected = SelectedRooms();
            if (selected.Count == 0) return;
            var placementOwner = display.Zones.FirstOrDefault(item => item.Id == zoneId)?.ModuleId;
            var inspected = selected.FirstOrDefault(item => item.Id == placementOwner) ?? selected[0];
            if (SelectedFixed == null) GUILayout.Label(RoomCaption(inspected.Id), heading, GUILayout.Width(RowWidth));
            if (step == WorkshopStep.Objects)
            {
                if (SelectedFixed == null) ZoneInspector(inspected);
                else
                {
                    FixedInspector();
                    if (Section(null, "Place more objects in this room", ref roomObjectsOpen)) ZoneInspector(inspected);
                }
            }
            else
            {
                PuzzleInspector(inspected);
                if (!newPuzzle) PhysicalSiteInspector(inspected);
            }
        }

        void RulesStep()
        {
            if (Section("modes", "Modes on this map", true)) ModeCards();
            if (Section("players", "Who plays", true)) WhoPlays();
            RouteInspector();
            if (Advanced(ref rulesAdvanced))
            {
                seed = Field("Seed", seed, name: "Layout seed");
                if (WideButton("Use this seed", "The seed picks one layout for random objects and alternative paths"))
                    Edit("Set seed", draft => draft.Seed = seed);
                inspectorAdvancedIds = GUILayout.Toggle(inspectorAdvancedIds, "Show internal names", GUILayout.Width(RowWidth));
            }
        }

        void RoomSize(CourseModule module)
        {
            if (!Catalogue.Modules.TryGetValue(module.DefinitionId, out var definition)) return;
            var (width, depth) = WorkshopCommands.WorldSize(definition, module.Yaw);
            var size = new GUIContent(ObjectLabel(definition.Id) + " · " + Metres(width) + " × " + Metres(depth) + " m",
                "Drag a room edge or corner in the map to resize · snaps to approved room sizes");
            if (Catalogue.Modules.Count < 2) { GUILayout.Label(size, ui.Wrapped, GUILayout.Width(RowWidth)); return; }
            GUILayout.BeginHorizontal();
            if (Button("‹", "Smaller room", ui.RowHeight)) StepRoomSize(-1);
            GUILayout.Label(size, rowLabel, GUILayout.Width(Mathf.Max(40, RowWidth - ui.RowHeight * 2 - 14)), GUILayout.MinHeight(ui.RowHeight));
            if (Button("›", "Larger room", ui.RowHeight)) StepRoomSize(1);
            GUILayout.EndHorizontal();
        }

        void StepRoomSize(int direction, bool wrap = false)
        {
            var rooms = display.Modules.Where(item => History.Current.Selection.Contains(item.Id)).ToList();
            if (rooms.Count != 1 || !Catalogue.Modules.ContainsKey(rooms[0].DefinitionId)) { status = "Select one room to resize."; return; }
            var room = rooms[0];
            string next = WorkshopCommands.StepDefinition(Catalogue, room.DefinitionId, direction, wrap);
            if (next == room.DefinitionId) { status = direction < 0 ? "Already the smallest room size" : "Already the largest room size"; return; }
            var position = WorkshopCommands.ResizedPosition(Catalogue, room, next, 1, 1);
            CancelGesture();
            Edit("Resize room", draft => WorkshopCommands.ResizeModule(draft, Catalogue, PlayRequirements, room.Id, next, position));
        }

        bool InspectorChoiceButton(string label, string id, string caption)
        {
            GUI.SetNextControlName("workshop-choice:" + label + ":" + id);
            return ui.Button(new GUIContent(caption, id), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(RowWidth));
        }

        string InspectorChoice(string label, string current, IEnumerable<string> available,
            Func<string, string> caption = null, string emptyChoice = null)
        {
            caption ??= ReferenceCaption;
            var values = available.Distinct(StringComparer.Ordinal).ToArray();
            if (ChoiceRow(label, (current.Length == 0 ? emptyChoice ?? "Choose…" : caption(current)) + " ▾"))
            { inspectorChoiceOpen = inspectorChoiceOpen == label ? "" : label; inspectorChoiceSearch = ""; }
            if (inspectorChoiceOpen != label) return current;
            if (emptyChoice != null && InspectorChoiceButton(label, "none", emptyChoice))
            { inspectorChoiceOpen = ""; return ""; }
            if (values.Length > 8) inspectorChoiceSearch = Field("Find", inspectorChoiceSearch, name: "Find " + label.ToLowerInvariant());
            foreach (string id in values.Where(id => ChoiceMatches(id, caption(id), inspectorChoiceSearch)))
                if (InspectorChoiceButton(label, id, caption(id))) { inspectorChoiceOpen = ""; return id; }
            if (values.Length == 0) GUILayout.Label("None available yet.", ui.Wrapped, GUILayout.Width(RowWidth));
            return current;
        }

        static bool ChoiceMatches(string id, string caption, string search) =>
            id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || caption.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        static string PresetCaption(string id) => id switch
        {
            "CollectLetters" => "Collect letters", "PlaceObject" => "Place an object", "BuildRoute" => "Build a crossing",
            "RedirectWind" => "Redirect wind", "BalanceWeight" => "Balance switches", "TimedMechanism" => "Timed mechanism",
            _ => ObjectLabel(id)
        };

        string ZoneCaption(CourseZone zone)
        {
            string name = zone.Pool.Count == 0 ? "Empty placement" : ObjectLabel(zone.Pool[0].AssetId);
            if (zone.Pool.Count > 1) name += " mix";
            else if (zone.Placement != "Fixed") name += " area";
            var siblings = display.Zones.Where(z => z.ModuleId == zone.ModuleId && z.Placement == zone.Placement &&
                z.Pool.FirstOrDefault()?.AssetId == zone.Pool.FirstOrDefault()?.AssetId).ToArray();
            if (siblings.Length > 1) name += " " + (Array.FindIndex(siblings, z => z.Id == zone.Id) + 1);
            return inspectorAdvancedIds ? name + " · " + zone.Id : name;
        }

        string PuzzleCaption(string id)
        {
            var puzzle = display.Puzzles.FirstOrDefault(p => p.Id == id);
            if (puzzle == null) return ObjectLabel(id);
            string name = RoomCaption(puzzle.ModuleId) + " · " + ObjectLabel(puzzle.RecipeId);
            var siblings = display.Puzzles.Where(p => p.ModuleId == puzzle.ModuleId && p.RecipeId == puzzle.RecipeId).ToArray();
            if (siblings.Length > 1) name += " " + (Array.FindIndex(siblings, p => p.Id == id) + 1);
            return inspectorAdvancedIds ? name + " · " + id : name;
        }

        string ReferenceCaption(string id) => ReferenceCaptionFor("", id);

        string ReferenceCaptionFor(string kind, string id)
        {
            if (string.IsNullOrEmpty(id)) return "Choose…";
            string name;
            var module = display.Modules.FirstOrDefault(m => (kind == "" || kind == "room") && m.Id == id);
            var puzzle = display.Puzzles.FirstOrDefault(p => (kind == "" || kind == "puzzle") && p.Id == id);
            var zone = display.Zones.FirstOrDefault(z => (kind == "" || kind == "zone") && z.Id == id);
            var edge = display.Connections.FirstOrDefault(c => (kind == "" || kind == "connection") && c.Id == id);
            var stop = display.Checkpoints.FirstOrDefault(c => (kind == "" || kind == "checkpoint") && c.Id == id);
            var supply = display.Supplies.FirstOrDefault(s => (kind == "" || kind == "supply") && s.Id == id);
            var safe = display.RecoveryAnchors.FirstOrDefault(r => (kind == "" || kind == "recovery") && r.Id == id);
            var spawn = display.Spawns.FirstOrDefault(s => (kind == "" || kind == "spawn") && s.Id == id);
            var route = display.RouteSections.FirstOrDefault(r => (kind == "" || kind == "route") && r.Id == id);
            if (module != null) name = RoomCaption(id);
            else if (puzzle != null) return PuzzleCaption(id);
            else if (zone != null) return ZoneCaption(zone);
            else if (edge != null) name = NumberedReference(RoomCaption(edge.FromModuleId) + " → " + RoomCaption(edge.ToModuleId), id,
                display.Connections.Where(c => c.FromModuleId == edge.FromModuleId && c.ToModuleId == edge.ToModuleId).Select(c => c.Id));
            else if (stop != null) name = NumberedReference(RoomCaption(stop.ModuleId) + " checkpoint", id,
                display.Checkpoints.Where(c => c.ModuleId == stop.ModuleId).Select(c => c.Id));
            else if (supply != null) name = NumberedReference(RoomCaption(supply.ModuleId) + " · " + supply.Letters, id,
                display.Supplies.Where(s => s.ModuleId == supply.ModuleId && s.Letters == supply.Letters).Select(s => s.Id));
            else if (safe != null) name = NumberedReference(RoomCaption(safe.ModuleId) + " safe-return point", id,
                display.RecoveryAnchors.Where(r => r.ModuleId == safe.ModuleId).Select(r => r.Id));
            else if (spawn != null) name = RoomCaption(spawn.ModuleId) + " · " + (spawn.Kind == "Respawn" ? "Respawn point" : "Start point") + " " +
                (display.Spawns.Where(s => s.ModuleId == spawn.ModuleId && s.Kind == spawn.Kind).ToList().FindIndex(s => s.Id == id) + 1);
            else if (route != null) name = NumberedReference(RoomCaption(route.EntryModuleId) + " → " + RoomCaption(route.ExitModuleId), id,
                display.RouteSections.Where(r => r.EntryModuleId == route.EntryModuleId && r.ExitModuleId == route.ExitModuleId).Select(r => r.Id));
            else name = ObjectLabel(id);
            return inspectorAdvancedIds ? name + " · " + id : name;
        }

        static string NumberedReference(string caption, string id, IEnumerable<string> source)
        {
            var siblings = source.ToList();
            return siblings.Count > 1 ? caption + " " + (siblings.IndexOf(id) + 1) : caption;
        }

        static string ReferenceKind(string label) => label switch
        {
            "Room" or "Path start" or "Path end" or "Lap start" or "Lap finish" => "room",
            "Checkpoint" or "Checkpoints in travel order" or "Add to checkpoints in travel order" => "checkpoint",
            "Safe-return point" => "recovery",
            "Recipe letters" or "Tool letters" or "Receiver letters" => "supply",
            "Breakable source" or "Wind receiver" => "zone",
            "Tool recipe" or "Receiver recipe" => "asset",
            "Connection" or "Arrives through" or "Connections in travel order" or "Add to connections in travel order" => "connection",
            "Path" or "Paths in order" or "Add to paths in order" => "route",
            "Available after puzzles" or "Required puzzles in order" or "Add to available after puzzles" or "Add to required puzzles in order" => "puzzle",
            "Start point" => "spawn",
            _ => "label"
        };

        string[] PuzzleRecipeChoices() => Catalogue.Assets.Values.Where(asset => asset.CanCraft)
            .Select(asset => asset.Id).Where(id => ChoiceMatches(id, ObjectLabel(id), recipeSearch))
            .OrderBy(id => id, StringComparer.Ordinal).Take(6).ToArray();

        void PuzzleRecipePicker()
        {
            recipeSearch = Field("Find", recipeSearch, name: "Find recipe");
            var recipes = PuzzleRecipeChoices();
            for (int i = 0; i < recipes.Length; i++)
            {
                if (i % 2 == 0) GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
                if (PlacementAssetButton(recipes[i], recipeId == recipes[i])) recipeId = recipes[i];
                if (i % 2 == 1 || i == recipes.Length - 1) GUILayout.EndHorizontal();
            }
            if (recipes.Length == 0) GUILayout.Label("No matching recipes.", ui.Wrapped, GUILayout.Width(RowWidth));
        }
    }
}
