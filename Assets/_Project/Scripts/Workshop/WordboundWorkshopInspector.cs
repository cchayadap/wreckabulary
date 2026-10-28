using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        enum InspectorSection { Objects, Puzzles, Room }
        InspectorSection inspectorSection;
        bool inspectorAdvancedIds, inspectorValidation, newPuzzle, puzzleLinks, placementPickerOpen;
        string inspectorChoiceOpen = "", inspectorChoiceSearch = "", recipeSearch = "";

        void ShowInspectorSection(InspectorSection section)
        {
            if (inspectorSection == section) return;
            CancelGesture(); inspectorSection = section; inspectorScroll = Vector2.zero;
            inspectorChoiceOpen = siteChoiceOpen = routeChoiceOpen = "";
            if (Event.current != null) GUI.FocusControl(null);
            workshopTyping = false;
        }

        void InspectorTabs()
        {
            GUILayout.BeginHorizontal();
            foreach (InspectorSection section in Enum.GetValues(typeof(InspectorSection)))
            {
                GUI.SetNextControlName("Workshop inspector " + section);
                if (ui.Button(new GUIContent(section.ToString()), inspectorSection == section ? ui.Selected : null,
                    GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true))) ShowInspectorSection(section);
            }
            GUILayout.EndHorizontal();
        }

        void RoomInspector(List<CourseModule> selected)
        {
            GUILayout.BeginHorizontal();
            if (ToolButton("Rotate", LobbyIcons.Turn, "Rotate selection · R")) Edit("Rotate", draft => WorkshopCommands.RotateSelection(draft, Pivot(selected), 1));
            if (ToolButton("Duplicate", LobbyIcons.Duplicate)) Edit("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(draft.GridMm * 2, 0, draft.GridMm * 2)));
            if (ToolButton("Delete", LobbyIcons.Delete)) Edit("Delete", WorkshopCommands.Delete);
            if (ToolButton("Group", LobbyIcons.Group)) Edit("Group", draft => WorkshopCommands.Group(draft, WorkshopCommands.Unique("group", draft.Groups.Keys)));
            if (ToolButton("Ungroup", LobbyIcons.Ungroup)) Edit("Ungroup", WorkshopCommands.Ungroup);
            GUILayout.EndHorizontal();
            if (Button("Move to floor", "Move selection to the current floor"))
                Edit("Change floor", draft => WorkshopCommands.ChangeLayer(draft, draft.ActiveLayer));
            advanced = GUILayout.Toggle(advanced, "Exact position");
            if (advanced)
            {
                xInput = Field("X (metres)", xInput); zInput = Field("Z (metres)", zInput);
                if (Button("Move")) Edit("Move", draft => WorkshopCommands.Move(draft,
                    new CoursePoint(Millimetres(xInput) - selected[0].Position.X, 0, Millimetres(zInput) - selected[0].Position.Z), false));
            }
            if (selected.Count == 2 && Button("Connect modules")) Connect(selected[0], selected[1]);
        }

        void InspectorValidation()
        {
            GUILayout.Space(8);
            inspectorValidation = GUILayout.Toggle(inspectorValidation,
                validation.Ok ? "Checks · ready" : "Checks · " + validation.Issues.Count + " issues");
            if (!inspectorValidation && validation.Ok && (LastPreview == null || LastPreview.Success)) return;
            foreach (var issue in validation.Issues.Take(12))
                if (Button(issue.Message, issue.Code + " · " + issue.EntityId)) FocusIssue(issue);
            if (LastPreview != null && !LastPreview.Success)
                foreach (var issue in LastPreview.Issues.Take(8)) GUILayout.Label(issue.Message, ui.Wrapped);
        }

        bool InspectorChoiceButton(string label, string id, string caption)
        {
            GUI.SetNextControlName("workshop-choice:" + label + ":" + id);
            return ui.Button(new GUIContent(caption, id), null, GUILayout.MinHeight(ui.RowHeight));
        }

        string InspectorChoice(string label, string current, IEnumerable<string> available,
            Func<string, string> caption = null, string emptyChoice = null)
        {
            caption ??= ReferenceCaption;
            var values = available.Distinct(StringComparer.Ordinal).ToArray();
            GUILayout.Label(label);
            if (InspectorChoiceButton(label, "open", (current.Length == 0 ? emptyChoice ?? "Choose…" : caption(current)) + " ▾"))
            { inspectorChoiceOpen = inspectorChoiceOpen == label ? "" : label; inspectorChoiceSearch = ""; }
            if (inspectorChoiceOpen != label) return current;
            if (emptyChoice != null && InspectorChoiceButton(label, "none", emptyChoice))
            { inspectorChoiceOpen = ""; return ""; }
            if (values.Length > 8) inspectorChoiceSearch = Field("Find " + label.ToLowerInvariant(), inspectorChoiceSearch);
            foreach (string id in values.Where(id => ChoiceMatches(id, caption(id), inspectorChoiceSearch)))
                if (InspectorChoiceButton(label, id, caption(id))) { inspectorChoiceOpen = ""; return id; }
            if (values.Length == 0) GUILayout.Label("None available yet.", ui.Wrapped);
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
            else if (stop != null) name = NumberedReference(RoomCaption(stop.ModuleId) + " stop", id,
                display.Checkpoints.Where(c => c.ModuleId == stop.ModuleId).Select(c => c.Id));
            else if (supply != null) name = NumberedReference(RoomCaption(supply.ModuleId) + " · " + supply.Letters, id,
                display.Supplies.Where(s => s.ModuleId == supply.ModuleId && s.Letters == supply.Letters).Select(s => s.Id));
            else if (safe != null) name = NumberedReference(RoomCaption(safe.ModuleId) + " safe return", id,
                display.RecoveryAnchors.Where(r => r.ModuleId == safe.ModuleId).Select(r => r.Id));
            else if (spawn != null) name = RoomCaption(spawn.ModuleId) + " · " + ObjectLabel(spawn.Kind) + " " +
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
            "Module" or "Spawn module" or "Route start" or "Route finish" or "Lap start" or "Lap finish" => "room",
            "Checkpoint" or "Checkpoints in travel order" or "Add to checkpoints in travel order" => "checkpoint",
            "Recovery anchor" or "Safe return anchor" => "recovery",
            "Recipe letters" or "Tool letters" or "Receiver letters" => "supply",
            "Breakable source" or "Wind receiver" => "zone",
            "Tool recipe" => "asset",
            "Connection" or "Arrival connection" or "Connections in travel order" or "Add to connections in travel order" => "connection",
            "Route section" or "Route sections in order" or "Add to route sections in order" => "route",
            "Available after puzzles" or "Required puzzles in order" or "Add to available after puzzles" or "Add to required puzzles in order" => "puzzle",
            "Spawn anchor" => "spawn",
            _ => "label"
        };

        string[] PuzzleRecipeChoices() => Catalogue.Assets.Values.Where(asset => asset.CanCraft)
            .Select(asset => asset.Id).Where(id => ChoiceMatches(id, ObjectLabel(id), recipeSearch))
            .OrderBy(id => id, StringComparer.Ordinal).Take(6).ToArray();

        void PuzzleRecipePicker()
        {
            GUILayout.Label("Recipe");
            recipeSearch = Field("Find recipe", recipeSearch);
            var recipes = PuzzleRecipeChoices();
            for (int i = 0; i < recipes.Length; i++)
            {
                if (i % 2 == 0) GUILayout.BeginHorizontal();
                if (PlacementAssetButton(recipes[i], recipeId == recipes[i])) recipeId = recipes[i];
                if (i % 2 == 1 || i == recipes.Length - 1) GUILayout.EndHorizontal();
            }
            GUILayout.Label("Selected: " + ObjectLabel(recipeId));
        }
    }
}
