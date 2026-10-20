using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop : MonoBehaviour
    {
        public WorkshopHistory History { get; private set; }
        public CourseCatalogue Catalogue { get; private set; }
        public CoursePlayRequirements PlayRequirements { get; private set; }
        public CourseResolution LastPreview { get; private set; }
        public WorkshopSnapshot LastPlaytestDraft { get; private set; }
        public event Action<ResolvedCourse> PlaytestRequested;
        public event Action CloseRequested;
        public bool Visible { get; set; } = true;
        WorkshopStorage storage;
        string storageFolder;
        CourseDocument display;
        CourseValidation validation;
        readonly Dictionary<WorkshopTemplate, WorkshopHistory> drafts = new Dictionary<WorkshopTemplate, WorkshopHistory>();
        WorkshopTemplate? currentTemplate;
        Vector2 paletteScroll, inspectorScroll, pointerStart;
        float autosaveAt;
        int displayRevision = -1, savedRevision = -1, zoneMode = 1, presetIndex;
        bool dragging, marquee, panning, drawingZone, addSelection, advanced, sharing;
        string search = "", placing, status = "", title = "", seed = "", xInput = "0", zInput = "0", layerHeight = "4";
        string zoneId = "", pool = "STOOL", minCount = "1", maxCount = "3", probability = "100", spacing = "0.5";
        string puzzleId = "", prerequisite = "", gateId = "", recipeId = "BED", shareText = "";
        Rect viewport;
        string resizingZone;
        CoursePoint resizeAnchor, resizePoint;
        GUIStyle panel, heading, selectedBox, ghostBox, normalBox, issueBox;

        public void Open(CourseDocument course, CourseCatalogue catalogue, CoursePlayRequirements play, string folder = null)
        {
            if (History != null && storage != null && History.Revision != savedRevision)
            { storage.Save(History.Current); savedRevision = History.Revision; }
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            PlayRequirements = play ?? throw new ArgumentNullException(nameof(play));
            History = new WorkshopHistory(new WorkshopSnapshot(course));
            storageFolder = folder ?? storageFolder ?? Path.Combine(Application.persistentDataPath, "wordbound-workshop");
            storage = new WorkshopStorage(storageFolder);
            currentTemplate = null; Visible = true; LastPlaytestDraft = null; ResetView(); Refresh(); savedRevision = History.Revision;
        }
        public void OpenTemplate(WorkshopTemplate template, string folder = null)
        {
            if (currentTemplate.HasValue && History != null) drafts[currentTemplate.Value] = History;
            var preset = WorkshopTemplates.Create(template);
            Open(preset.Snapshot.Course(), preset.Catalogue, preset.Play, folder);
            currentTemplate = template;
            if (drafts.TryGetValue(template, out var saved)) History = saved;
            else if (storage.HasRecovery(preset.Snapshot.Course().Id))
            {
                History = new WorkshopHistory(storage.Recover(preset.Snapshot.Course().Id, out bool backup));
                status = backup ? "Recovered previous template save" : "Recovered template autosave";
            }
            ResetView(); Refresh(); savedRevision = History.Revision;
        }
        public void ResumeEditing() { Visible = true; Refresh(); }
        public CourseResolution Preview()
        {
            if (!SelectedModeEnabled) throw new InvalidOperationException("Choose a mode enabled by this map before previewing.");
            CancelGesture();
            LastPreview = History.Preview(Catalogue, PlayRequirements);
            if (view != null) view.Sync(History.Current, Catalogue, LastPreview.Success ? LastPreview.Manifest : null);
            status = LastPreview.Success ? "Preview ready · exact draft and seed" : LastPreview.Issues.FirstOrDefault()?.Message ?? "Preview failed";
            return LastPreview;
        }
        public void RequestPlaytest()
        {
            LastPlaytestDraft = null;
            if (!SelectedModeEnabled) { ShowStatus("Choose a mode enabled by this map before playtesting."); return; }
            if (localPlaytest && !CanPlaytestLocally)
            { ShowStatus("Choose TimeRun or Freestyle for a one-player local playtest."); return; }
            if (PlaytestRequested == null) { status = "Playtest is unavailable in this view."; return; }
            var result = Preview();
            if (result.Success)
            {
                string physicalIssue = WorkshopPhysicalSites.PlaytestIssue(result.Manifest);
                if (physicalIssue.Length != 0) { ShowStatus(physicalIssue); return; }
                LastPlaytestDraft = History.Current;
                PlaytestRequested.Invoke(result.Manifest);
            }
        }
        void ResetView()
        {
            displayRevision = -1; savedRevision = -1; placing = null; dragging = marquee = panning = drawingZone = sharing = false;
            zoneId = ""; resizingZone = null; LastPreview = null; CancelGesture(); Frame();
        }
        void Refresh()
        {
            if (History == null || History.Revision == displayRevision) return;
            display = History.Current.Course(); validation = CourseValidator.Validate(display, Catalogue, PlayRequirements);
            title = display.Title; seed = History.Current.Seed;
            var selected = display.Modules.FirstOrDefault(module => History.Current.Selection.Contains(module.Id));
            if (selected != null) { xInput = Metres(selected.Position.X); zInput = Metres(selected.Position.Z); }
            var selectedZone = display.Zones.FirstOrDefault(item => item.Id == zoneId && History.Current.Selection.Contains(item.ModuleId));
            if (selectedZone == null) zoneId = ""; else LoadPlacementFields(selectedZone);
            displayRevision = History.Revision; autosaveAt = Time.unscaledTime + 1f; LastPreview = null;
            if (view != null) view.Sync(History.Current, Catalogue);
        }
        void Update()
        {
            if (History == null) return;
            Refresh();
            if (view != null) view.Active = Visible;
            if (Visible && !sharing) ControllerInput();
            if (History.Revision == savedRevision || Time.unscaledTime < autosaveAt) return;
            try { storage.Save(History.Current); savedRevision = History.Revision; }
            catch (Exception error) { status = "Autosave: " + error.Message; autosaveAt = Time.unscaledTime + 5f; }
        }
        void OnDisable()
        {
            if (view != null) view.Active = false;
            CancelGesture();
            if (History == null || storage == null || History.Revision == savedRevision) return;
            try { storage.Save(History.Current); savedRevision = History.Revision; }
            catch (Exception error) { Debug.LogWarning("Wordbound autosave: " + error.Message); }
        }
        void Edit(string label, Action<WorkshopDraft> edit)
        {
            try { History.Execute(label, edit); status = label; Refresh(); }
            catch (Exception error) { status = error.Message; }
        }
        void Attempt(Action action)
        {
            try { action(); Refresh(); }
            catch (Exception error) { status = error.Message; }
        }
        bool Button(string label, string tooltip = null)
        {
            GUI.SetNextControlName(label);
            return GUILayout.Button(new GUIContent(label, tooltip ?? label), GUILayout.MinHeight(27));
        }
        string Field(string label, string value)
        {
            GUILayout.Label(label); GUI.SetNextControlName(label);
            return GUILayout.TextField(value, GUILayout.MinHeight(24));
        }
        void Styles()
        {
            if (panel != null) return;
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10) };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            normalBox = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            selectedBox = new GUIStyle(normalBox); selectedBox.normal.textColor = new Color(.45f, 1f, .8f);
            ghostBox = new GUIStyle(normalBox); ghostBox.normal.textColor = new Color(.6f, .6f, .6f);
            issueBox = new GUIStyle(normalBox); issueBox.normal.textColor = new Color(1f, .55f, .4f);
        }
        void OnGUI()
        {
            if (!Visible || History == null) return;
            Styles(); Refresh();
            if (sharing) { SharePanel(); return; }
            float left = Mathf.Min(220, Screen.width * .22f), right = Mathf.Min(300, Screen.width * .28f);
            viewport = new Rect(left + 8, 56, Screen.width - left - right - 24, Screen.height - 140);
            Header();
            GUILayout.BeginArea(new Rect(8, 56, left - 8, Screen.height - 140), panel); Palette(); GUILayout.EndArea();
            WorldView();
            GUILayout.BeginArea(new Rect(Screen.width - right - 8, 56, right, Screen.height - 140), panel); Inspector(); GUILayout.EndArea();
            Footer();
            if (sharing) SharePanel();
            if (!string.IsNullOrEmpty(GUI.tooltip)) GUI.Label(new Rect(14, Screen.height - 24, Screen.width - 28, 22), GUI.tooltip);
        }
        void Header()
        {
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, 42), panel); GUILayout.BeginHorizontal();
            GUILayout.Label("Wordbound workshop", heading, GUILayout.Width(190));
            GUI.SetNextControlName("Course title"); title = GUILayout.TextField(title, GUILayout.MinWidth(140));
            if (Button("Rename")) Edit("Rename", draft => draft.Course.Title = title.Trim());
            GUILayout.Label(PlayRequirements.ModeId, GUILayout.Width(90));
            if (Button("Share")) { shareText = History.Current.CourseJsonText; sharing = true; }
            if (Button("Close")) Attempt(() => { CancelGesture(); storage.Save(History.Current); savedRevision = History.Revision; CloseRequested?.Invoke(); Visible = false; });
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        void Palette()
        {
            GUILayout.Label("Palette", heading);
            search = Field("Search modules", search);
            paletteScroll = GUILayout.BeginScrollView(paletteScroll);
            ModePanel(); GUILayout.Space(12);
            foreach (var definition in Catalogue.Modules.Values.Where(item => item.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                if (Button(definition.Id + "\n" + Metres(definition.WidthMm) + " × " + Metres(definition.DepthMm) + " m", "Select, then click the map to place."))
                { placing = definition.Id; drawingZone = false; status = "Click a floor to place " + placing; }
            GUILayout.Space(12); GUILayout.Label("Start from", heading);
            if (flagshipContent != null && Button("Flagship draft")) Attempt(() => OpenFlagship(flagshipContent));
            foreach (WorkshopTemplate template in Enum.GetValues(typeof(WorkshopTemplate)))
                if (Button(TemplateLabel(template))) Attempt(() => OpenTemplate(template));
            GUILayout.Label("Templates start with geometry. Race playtests also need physical controls at every selected puzzle checkpoint.");
            GUILayout.Space(12);
            FloorPanel();
            GUILayout.Space(12);
            if (Button("Frame map")) Frame();
            if (Button("Top view")) { EnsureView(); view.Top(); }
            if (Button("Recover autosave"))
                Attempt(() =>
                {
                    var recovered = storage.Recover(display.Id, out bool backup);
                    History.Execute("Recover", draft => Replace(draft, recovered.Edit()));
                    status = backup ? "Recovered previous save" : "Recovered autosave";
                });
            GUILayout.Label("Drag to move · Shift to add\nRight drag to orbit · Middle to pan\nWheel to zoom · F frames selection");
            ControllerPanel();
            GUILayout.EndScrollView();
        }
        void Inspector()
        {
            inspectorScroll = GUILayout.BeginScrollView(inspectorScroll);
            var selected = display.Modules.Where(module => History.Current.Selection.Contains(module.Id)).ToList();
            GUILayout.Label(selected.Count == 0 ? "Properties" : selected.Count + " selected", heading);
            if (selected.Count > 0)
            {
                GUILayout.Label(string.Join(", ", selected.Select(module => module.Id)));
                if (SelectedFixed != null) FixedInspector();
                else
                {
                GUILayout.BeginHorizontal();
                if (Button("Rotate")) Edit("Rotate", draft => WorkshopCommands.RotateSelection(draft, Pivot(selected), 1));
                if (Button("Duplicate")) Edit("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(draft.GridMm * 2, 0, draft.GridMm * 2)));
                if (Button("Delete")) Edit("Delete", WorkshopCommands.Delete);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (Button("Group")) Edit("Group", draft => WorkshopCommands.Group(draft, WorkshopCommands.Unique("group", draft.Groups.Keys)));
                if (Button("Ungroup")) Edit("Ungroup", WorkshopCommands.Ungroup);
                if (Button("To this floor")) Edit("Change floor", draft => WorkshopCommands.ChangeLayer(draft, draft.ActiveLayer));
                GUILayout.EndHorizontal();
                advanced = GUILayout.Toggle(advanced, "Advanced position");
                if (advanced)
                {
                    xInput = Field("X (metres)", xInput); zInput = Field("Z (metres)", zInput);
                    if (Button("Move"))
                        Edit("Move", draft => WorkshopCommands.Move(draft, new CoursePoint(Millimetres(xInput) - selected[0].Position.X, 0, Millimetres(zInput) - selected[0].Position.Z), false));
                }
                if (selected.Count == 2 && Button("Connect modules")) Connect(selected[0], selected[1]);
                }
                var placementOwner = display.Zones.FirstOrDefault(item => item.Id == zoneId)?.ModuleId;
                var inspected = selected.FirstOrDefault(item => item.Id == placementOwner) ?? selected[0];
                GUILayout.Space(10); ZoneInspector(inspected);
                GUILayout.Space(10); PuzzleInspector(inspected);
                GUILayout.Space(10); PhysicalSiteInspector(inspected);
            }
            RouteInspector();
            GUILayout.Space(12); GUILayout.Label("Validation", heading);
            if (validation.Ok) GUILayout.Label("Ready for preview");
            foreach (var issue in validation.Issues.Take(12))
                if (Button((string.IsNullOrEmpty(issue.EntityId) ? "" : issue.EntityId + ": ") + issue.Message, issue.Code))
                    FocusIssue(issue);
            if (LastPreview != null && !LastPreview.Success)
                foreach (var issue in LastPreview.Issues.Take(8)) GUILayout.Label(issue.Message);
            GUILayout.EndScrollView();
        }
        void FocusIssue(CourseIssue issue)
        {
            string id = issue.EntityId ?? "";
            string owner = display.Modules.FirstOrDefault(item => item.Id == id)?.Id
                ?? display.Zones.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.Puzzles.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.Supplies.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.Spawns.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.RecoveryAnchors.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.Checkpoints.FirstOrDefault(item => item.Id == id)?.ModuleId
                ?? display.Connections.FirstOrDefault(item => item.Id == id)?.FromModuleId;
            var module = display.Modules.FirstOrDefault(item => item.Id == owner);
            if (module != null)
            {
                Edit("Focus issue", draft => { draft.ActiveLayer = module.LayerId; draft.HiddenLayers.Remove(module.LayerId); });
                History.Select(new[] { module.Id }, false, false);
                view?.Focus(module.Position); Refresh();
            }
            status = issue.Message;
        }
        void ZoneInspector(CourseModule module)
        {
            GUILayout.Label("Placement", heading);
            int nextMode = GUILayout.Toolbar(zoneMode, new[] { "Fixed", "Zone", "Pool" });
            if (nextMode != zoneMode)
            {
                CancelGesture(); zoneMode = nextMode;
                if (zoneMode != 2) pool = pool.Split(',')[0].Split(':')[0].Trim();
            }
            PlacementAssetPicker();
            if (zoneMode != 0)
            {
                GUILayout.BeginHorizontal(); minCount = Field("Min", minCount); maxCount = Field("Max", maxCount); GUILayout.EndHorizontal();
                probability = Field("Chance % · min > 0 needs 100", probability); spacing = Field("Spacing (metres)", spacing);
            }
            var editing = display.Zones.FirstOrDefault(item => item.Id == zoneId && item.ModuleId == module.Id);
            if (editing != null)
            {
                GUILayout.Label("Editing " + editing.Id);
                if (zoneMode != 0) PlacementRangeFields();
                if (Button("Apply placement settings")) ApplyPlacementSettings(module.Id);
            }
            if (Button(zoneMode == 0 ? editing == null ? "Place object" : "Reposition object" : editing == null ? "Draw zone" : "Redraw bounds"))
            { CancelGesture(); drawingZone = true; status = zoneMode == 0 ? "Click the fixed position · Esc cancels" : "Drag the placement bounds · Esc cancels"; }
            foreach (var zone in display.Zones.Where(item => item.ModuleId == module.Id))
            {
                GUILayout.BeginHorizontal();
                if (Button(zone.Id)) SelectZone(zone);
                if (Button("×", "Delete " + zone.Id)) Edit("Delete zone", draft =>
                {
                    if (draft.LockedLayers.Contains(module.LayerId)) throw new InvalidOperationException("Unlock the floor first.");
                    draft.Course.Zones.RemoveAll(item => item.Id == zone.Id);
                });
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(zoneId) && Button("New placement")) { CancelGesture(); zoneId = ""; }
        }
        void PuzzleInspector(CourseModule module)
        {
            GUILayout.Label("Puzzle link", heading);
            var presets = Catalogue.PuzzlePresets;
            if (presets.Count == 0) return;
            presetIndex %= presets.Count;
            if (Button(presets[presetIndex], "Choose the next puzzle preset")) presetIndex = (presetIndex + 1) % presets.Count;
            recipeId = Field("Recipe ID", recipeId);
            if (Button("Add puzzle + letters"))
                Edit("Add puzzle", draft =>
                {
                    if (!Catalogue.Assets.TryGetValue(recipeId, out var asset)) throw new ArgumentException("Choose an approved recipe.");
                    string id = WorkshopCommands.Unique("puzzle", draft.Course.Puzzles.Select(item => item.Id));
                    string supply = WorkshopCommands.Unique("letters", draft.Course.Supplies.Select(item => item.Id));
                    WorkshopCommands.SetPuzzle(draft, new CoursePuzzle
                    {
                        Id = id, ModuleId = module.Id, PresetId = presets[presetIndex], RecipeId = recipeId,
                        RequiredLetters = asset.Letters, RecoverySupplyIds = new List<string> { supply }
                    }, Catalogue);
                    draft.Course.Supplies.Add(new CourseSupply { Id = supply, ModuleId = module.Id, Letters = asset.Letters, Position = module.Position });
                    puzzleId = id;
                });
            puzzleId = Field("Puzzle ID", puzzleId); prerequisite = Field("After puzzle ID", prerequisite);
            if (Button("Link prerequisite")) Edit("Link puzzle", draft => WorkshopCommands.LinkPuzzle(draft, puzzleId, prerequisite));
            gateId = Field("Connection ID", gateId);
            if (Button("Complete puzzle → open gate")) Edit("Link gate", draft => WorkshopCommands.Gate(draft, gateId, puzzleId));
            foreach (var puzzle in display.Puzzles.Where(item => item.ModuleId == module.Id))
                if (Button(puzzle.Id + " · " + puzzle.PresetId))
                { CancelGesture(); puzzleId = puzzle.Id; sitePuzzle = ""; }
            foreach (var connection in display.Connections.Where(item => item.FromModuleId == module.Id || item.ToModuleId == module.Id))
                if (Button(connection.Id + " · " + connection.Kind + " → " + connection.ToModuleId)) gateId = connection.Id;
        }
        void Footer()
        {
            GUILayout.BeginArea(new Rect(8, Screen.height - 78, Screen.width - 16, 52), panel); GUILayout.BeginHorizontal();
            GUILayout.Label("Floor " + History.Current.ActiveLayer);
            GUI.enabled = History.CanUndo; if (Button("Undo")) { CancelGesture(); History.Undo(); Refresh(); } GUI.enabled = true;
            GUI.enabled = History.CanRedo; if (Button("Redo")) { CancelGesture(); History.Redo(); Refresh(); } GUI.enabled = true;
            GUI.enabled = SelectedModeEnabled; if (Button("Preview")) Attempt(() => Preview());
            GUI.enabled = PlaytestRequested != null && SelectedModeEnabled && (!localPlaytest || CanPlaytestLocally);
            if (Button("Playtest")) Attempt(RequestPlaytest); GUI.enabled = true;
            GUILayout.EndHorizontal(); GUILayout.EndArea();
            GUI.Label(new Rect(12, Screen.height - 27, Screen.width - 24, 23), status);
            GUILayout.BeginArea(new Rect(viewport.x + 8, viewport.y + 8, Mathf.Min(440, viewport.width - 16), 90), panel);
            GUILayout.BeginHorizontal();
            GUI.SetNextControlName("Seed"); seed = GUILayout.TextField(seed, 16);
            if (Button("Set seed")) Edit("Set seed", draft => draft.Seed = seed);
            if (Button(History.Current.SeedLocked ? "Locked" : "Unlocked")) Edit("Seed lock", draft => draft.SeedLocked = !draft.SeedLocked);
            GUI.enabled = !History.Current.SeedLocked;
            if (Button("Reroll")) Attempt(() => History.Reroll(Guid.NewGuid().ToString("N").Substring(0, 16)));
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Grid " + Metres(History.Current.GridMm) + " m");
            if (Button("Grid")) Edit("Grid", draft => draft.GridMm = draft.GridMm == 500 ? 1000 : draft.GridMm == 1000 ? 100 : 500);
            GUILayout.Label("Esc cancels a drag");
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        void DrawZone(CoursePoint from, CoursePoint to)
        {
            Attempt(() =>
            {
                string nextId = zoneId;
                var settings = PlacementSettings();
                History.Execute("Placement zone", draft =>
                {
                    var module = draft.Course.Modules.First(item => draft.Selection.Contains(item.Id));
                    from = new CoursePoint(WorkshopCommands.Snap(from.X, draft.GridMm), module.Position.Y, WorkshopCommands.Snap(from.Z, draft.GridMm));
                    to = new CoursePoint(WorkshopCommands.Snap(to.X, draft.GridMm), module.Position.Y, WorkshopCommands.Snap(to.Z, draft.GridMm));
                    var existing = draft.Course.Zones.FirstOrDefault(item => item.Id == zoneId);
                    var bounds = zoneMode == 0 ? Catalogue.Bounds(module) :
                        ResizeBounds(existing?.Bounds ?? new CourseBox(from, from), from, to);
                    nextId = settings.Apply(draft, Catalogue, module.Id, zoneId, bounds, zoneMode == 0 ? to : (CoursePoint?)null);
                });
                zoneId = nextId; Refresh(); status = "Placement updated · undo available";
            });
        }
        void Connect(CourseModule first, CourseModule second)
        {
            Edit("Connect modules", draft =>
            {
                var from = first.Position.Y <= second.Position.Y ? first : second;
                var to = from == first ? second : first;
                var a = Catalogue.Bounds(from); var b = Catalogue.Bounds(to);
                CoursePoint start = from.Position, finish = to.Position;
                bool adjacentX = (a.Max.X == b.Min.X || a.Min.X == b.Max.X) && from.Position.Z == to.Position.Z;
                bool adjacentZ = (a.Max.Z == b.Min.Z || a.Min.Z == b.Max.Z) && from.Position.X == to.Position.X;
                string kind = from.Position.Y != to.Position.Y ? "Stairs" : "Bridge";
                if (from.Position.Y == to.Position.Y && (adjacentX || adjacentZ))
                {
                    kind = "Walk";
                    start = finish = adjacentX ? new CoursePoint(to.Position.X > from.Position.X ? a.Max.X : a.Min.X, from.Position.Y, from.Position.Z)
                        : new CoursePoint(from.Position.X, from.Position.Y, to.Position.Z > from.Position.Z ? a.Max.Z : a.Min.Z);
                }
                string id = WorkshopCommands.Unique("link", draft.Course.Connections.Select(item => item.Id));
                WorkshopCommands.Connect(draft, new CourseConnection { Id = id, FromModuleId = from.Id, ToModuleId = to.Id, Kind = kind, From = start, To = finish });
                gateId = id;
            });
        }
        void SharePanel()
        {
            GUILayout.BeginArea(new Rect(Screen.width * .12f, Screen.height * .13f, Screen.width * .76f, Screen.height * .72f), panel);
            GUILayout.Label("Course JSON", heading); GUI.SetNextControlName("Shared course JSON");
            shareText = GUILayout.TextArea(shareText, CourseLimits.DocumentBytes, GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            if (Button("Copy")) GUIUtility.systemCopyBuffer = shareText;
            if (Button("Paste"))
            {
                string pasted = GUIUtility.systemCopyBuffer;
                if (pasted.Length > CourseLimits.DocumentBytes) status = "Course JSON is too large.";
                else shareText = pasted;
            }
            if (Button("Import"))
                Attempt(() =>
                {
                    var imported = storage.ImportCourse(shareText);
                    History.Execute("Import course", draft => Replace(draft, imported.Edit()));
                    sharing = false; Refresh(); Frame();
                });
            if (Button("Save file")) Attempt(() => status = storage.SaveShare(History.Current));
            if (Button("Done")) sharing = false;
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        static void DrawLine(Vector2 from, Vector2 to, Color color)
        {
            var matrix = GUI.matrix; var previous = GUI.color; var delta = to - from;
            GUI.color = color;
            GUI.matrix *= Matrix4x4.TRS(from, Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.DrawTexture(new Rect(0, -1, Mathf.Max(3, delta.magnitude), 2), Texture2D.whiteTexture);
            GUI.matrix = matrix; GUI.color = previous;
        }
        static void Replace(WorkshopDraft target, WorkshopDraft source)
        {
            target.Course = source.Course; target.Seed = source.Seed; target.SeedLocked = source.SeedLocked; target.GridMm = source.GridMm;
            target.ActiveLayer = source.ActiveLayer; target.Selection = source.Selection; target.Groups = source.Groups;
            target.HiddenLayers = source.HiddenLayers; target.LockedLayers = source.LockedLayers;
        }
        static Rect DragRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        static CoursePoint[] Corners(CourseBox box) => new[]
        {
            box.Min, new CoursePoint(box.Max.X, box.Min.Y, box.Min.Z),
            new CoursePoint(box.Max.X, box.Min.Y, box.Max.Z), new CoursePoint(box.Min.X, box.Min.Y, box.Max.Z)
        };
        static CourseBox ResizeBounds(CourseBox original, CoursePoint a, CoursePoint b) => new CourseBox(
            new CoursePoint(Math.Min(a.X, b.X), original.Min.Y, Math.Min(a.Z, b.Z)),
            new CoursePoint(Math.Max(a.X, b.X), original.Max.Y, Math.Max(a.Z, b.Z)));
        static CoursePoint Pivot(List<CourseModule> modules) => new CoursePoint((int)modules.Average(module => (double)module.Position.X),
            (int)modules.Average(module => (double)module.Position.Y), (int)modules.Average(module => (double)module.Position.Z));
        static void Toggle(List<string> list, string id) { if (!list.Remove(id)) list.Add(id); }
        static string Metres(int mm) => (mm / 1000d).ToString("0.###", CultureInfo.InvariantCulture);
        static int Millimetres(string text)
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double metres) || double.IsNaN(metres) || double.IsInfinity(metres))
                throw new ArgumentException("Enter a number.");
            return checked((int)Math.Round(metres * 1000));
        }
        static string TemplateLabel(WorkshopTemplate template) => template == WorkshopTemplate.RaceCircuit ? "Race Circuit" :
            template == WorkshopTemplate.CombatArena ? "Combat Arena" : "Freestyle World";
    }
}
