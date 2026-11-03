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
        public string CloseLabel { get; set; }
        public bool Visible { get; set; } = true;
        public WordboundUiTheme Theme;
        readonly WordboundUiStyles ui = new WordboundUiStyles();
        int styleGeneration;
        Rect ToolbarRect
        {
            get
            {
                var rect = ui.ToolbarRect(viewport.size);
                if (!showRandomness)
                {
                    rect.width = Mathf.Min(rect.width, ui.RowHeight * 3 + 132 + ui.Padding * 2 + ui.Gap * 6);
                    rect.height = ui.ChromeHeight;
                }
                return rect;
            }
        }
        Rect ToolbarReserved => Rect.MinMaxRect(0, 0, ToolbarRect.xMax + 8, ToolbarRect.yMax + 8);
        WorkshopStorage storage;
        string storageFolder;
        CourseDocument display;
        CourseValidation validation;
        readonly Dictionary<WorkshopTemplate, DraftSession> drafts = new Dictionary<WorkshopTemplate, DraftSession>();
        WorkshopTemplate? currentTemplate;
        Vector2 inspectorScroll, pointerStart;
        float autosaveAt;
        int displayRevision = -1, savedRevision = -1, zoneMode = 1, presetIndex;
        bool dragging, marquee, panning, drawingZone, addSelection, advanced, sharing;
        bool showWorkshopHelp, showRandomness, titleEditing;
        string search = "", placing, status = "", title = "", seed = "", xInput = "0", zInput = "0", layerHeight = "4";
        string zoneId = "", pool = "STOOL", minCount = "1", maxCount = "3", probability = "100", spacing = "0.5";
        string puzzleId = "", prerequisite = "", gateId = "", recipeId = "BED", shareText = "", saveError = "", lastPlaytestIssue = "";
        bool shareDraftOnly;
        Rect viewport;
        string resizingZone;
        CoursePoint resizeAnchor, resizePoint;
        GUIStyle panel, heading, selectedBox, ghostBox, normalBox, issueBox, sectionStyle, rowLabel, rowField, cardStyle, cardTitle, hintStyle;
        const string SectionPrefix = "wv.workshop.section.", TitleControl = "workshop-text:Map name";
        const float ScrubPixels = 6, MinimumField = 44;
        static readonly int ScrubHint = "WordboundWorkshopScrub".GetHashCode();
        readonly Dictionary<string, bool> sections = new Dictionary<string, bool>(StringComparer.Ordinal);
        float rowWidth = 200;
        string scrubbing, scrubReleased, floorHeight = "0";
        double scrubValue;
        static string DefaultFolder => Path.Combine(Application.persistentDataPath, "wordbound-workshop");

        public void Open(CourseDocument course, CourseCatalogue catalogue, CoursePlayRequirements play, string folder = null)
            => OpenSource(course, catalogue, play, folder, false);

        void OpenSource(CourseDocument course, CourseCatalogue catalogue, CoursePlayRequirements play, string folder, bool assigned)
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing courses.");
            SaveAndRememberDraft();
            Catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
            PlayRequirements = play ?? throw new ArgumentNullException(nameof(play));
            History = new WorkshopHistory(new WorkshopSnapshot(course));
            storageFolder = folder ?? storageFolder ?? DefaultFolder;
            storage = new WorkshopStorage(storageFolder);
            currentTemplate = null; UsesAssignedCourse = assigned; UsesParcelService = false; usesFlagship = false; Visible = true; LastPlaytestDraft = null; ResetView(); Refresh(); savedRevision = History.Revision;
        }
        public void OpenTemplate(WorkshopTemplate template, string folder = null)
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing courses.");
            if (currentTemplate == template && (folder == null || folder == storageFolder)) return;
            var preset = WorkshopTemplates.Create(template);
            SaveAndRememberDraft();
            if (drafts.TryGetValue(template, out var retained) && (folder == null || folder == retained.Folder))
            { ActivateDraft(retained, false, template); ShowStatus(TemplateLabel(template) + " · your edits and Undo history are retained."); return; }
            var back = CurrentSource();
            string baseFolder = folder ?? storageFolder ?? DefaultFolder;
            var templateStorage = new WorkshopStorage(Path.Combine(baseFolder, "templates", template.ToString()));
            var legacyStorage = new WorkshopStorage(baseFolder);
            string id = preset.Snapshot.Course().Id;
            var session = new DraftSession { History = new WorkshopHistory(preset.Snapshot), Catalogue = preset.Catalogue,
                Play = preset.Play, Storage = templateStorage, Folder = baseFolder };
            string last = LastMapId(templateStorage, id);
            if (ReopenLastMap(session, templateStorage, last, template, false, TemplateLabel(template), out string problem)) return;
            var recoveryStorage = templateStorage.HasRecovery(id) ? templateStorage :
                !HasAssignedCourse && legacyStorage.HasRecovery(id) ? legacyStorage : null;
            OpenRecovered(session, recoveryStorage, id, back, template, false, false, TemplateLabel(template),
                "Recovered template autosave", "Recovered previous template save", "Start fresh from the template");
            AskAboutDamagedLastMap(templateStorage, last, problem, back);
        }
        public void ResumeEditing() { Visible = true; Refresh(); }
        public void RequestClose()
        {
            if (OnlineStarting || RecoveryChoicePending || History == null || storage == null) return;
            Attempt(() => {
                CancelGesture(); CommitTypedTitle(); storage.Save(History.Current); savedRevision = History.Revision; saveError = "";
                CloseRequested?.Invoke(); Visible = false;
            });
        }
        public CourseResolution Preview()
        {
            RequireRecoveryChoice();
            if (!SelectedModeEnabled) throw new InvalidOperationException("Choose a mode this map supports in Rules before checking it.");
            CancelGesture();
            LastPreview = History.Preview(Catalogue, PlayRequirements);
            if (view != null) view.Sync(History.Current, Catalogue, LastPreview.Success ? LastPreview.Manifest : null);
            lastPlaytestIssue = LastPreview.Success ? WorkshopPhysicalSites.PlaytestIssue(LastPreview.Manifest) : "";
            status = !LastPreview.Success ? LastPreview.Issues.FirstOrDefault()?.Message ?? "The check found a problem" :
                lastPlaytestIssue.Length == 0 ? "Map checked · ready to test with this mode and layout" : "Map checked · " + lastPlaytestIssue;
            return LastPreview;
        }
        public void RequestPlaytest()
        {
            LastPlaytestDraft = null;
            if (!SelectedModeEnabled) { ShowStatus("Choose a mode this map supports in Rules before testing."); return; }
            if (localPlaytest && !CanPlaytestLocally)
            { ShowStatus("Test alone works in Time Run or Freestyle with one player. Choose one in Rules."); return; }
            if (PlaytestRequested == null) { status = "Testing is unavailable in this view."; return; }
            var result = Preview();
            if (result.Success)
            {
                if (lastPlaytestIssue.Length != 0) { ShowStatus(lastPlaytestIssue); return; }
                LastPlaytestDraft = History.Current;
                PlaytestRequested.Invoke(result.Manifest);
            }
        }
        void ResetView()
        {
            workshopPadGate.Suspend();
            displayRevision = -1; savedRevision = -1; placing = null; dragging = marquee = panning = drawingZone = sharing = false;
            libraryOpen = filesOpen = savingAs = titleEditing = false; SetPendingImport(null); lastPlaytestIssue = "";
            zoneId = ""; resizingZone = null; LastPreview = null; step = WorkshopStep.Build; lastFill = WorkshopStep.Objects;
            inspectorScroll = Vector2.zero; inspectorChoiceOpen = ""; newPuzzle = false; CancelGesture(); Frame();
        }
        void Refresh()
        {
            if (History == null || History.Revision == displayRevision) return;
            display = History.Current.Course(); validation = CourseValidator.Validate(display, Catalogue, PlayRequirements);
            if (!titleEditing) title = display.Title;
            seed = History.Current.Seed;
            var selected = display.Modules.FirstOrDefault(module => History.Current.Selection.Contains(module.Id));
            if (selected != null) { xInput = Metres(selected.Position.X); zInput = Metres(selected.Position.Z); }
            var selectedZone = display.Zones.FirstOrDefault(item => item.Id == zoneId && History.Current.Selection.Contains(item.ModuleId));
            if (selectedZone == null) zoneId = ""; else LoadPlacementFields(selectedZone);
            layerHeight = Metres(WorkshopCommands.NextLayerHeight(display, Catalogue));
            floorHeight = Metres(display.Layers.FirstOrDefault(layer => layer.Id == History.Current.ActiveLayer)?.HeightMm ?? 0);
            displayRevision = History.Revision; autosaveAt = Time.unscaledTime + 1f; LastPreview = null; lastPlaytestIssue = "";
            if (view != null) view.Sync(History.Current, Catalogue);
        }
        void Update()
        {
            if (History == null) return;
            if (RecoveryChoicePending) { ProcessRecoveryChoice(Application.isFocused); return; }
            ProcessSourceChoice(Application.isFocused);
            Refresh();
            if (view != null) view.Active = Visible;
            ControllerInput(Application.isFocused);
            if (History.Revision == savedRevision || Time.unscaledTime < autosaveAt) return;
            SaveNow();
        }
        bool SaveNow()
        {
            try
            {
                storage.Save(History.Current); savedRevision = History.Revision; saveError = "";
                RememberLast();
                return true;
            }
            catch (Exception error) { saveError = error.Message; status = "Not saved: " + error.Message; autosaveAt = Time.unscaledTime + 5f; return false; }
        }
        void RememberLast()
        {
            try { storage.RememberLastMap(History.Current.Course().Id); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { }
        }
        string SaveState => saveError.Length != 0 ? "Not saved" : History.Revision == savedRevision ? "Saved ✓" : "Saving…";
        void OnDisable()
        {
            SuspendWorkshopInput();
            if (view != null) view.Active = false;
            CancelGesture();
            recoveryDecision = 0;
            if (RecoveryChoicePending) return;
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
        bool Button(string label, string tooltip = null, float width = 0)
        {
            GUI.SetNextControlName(label);
            var height = GUILayout.MinHeight(label.Contains("\n") ? ui.ModuleHeight : ui.RowHeight);
            return width > 0 ? ui.Button(new GUIContent(label, tooltip ?? label), null, height, GUILayout.Width(width))
                : ui.Button(new GUIContent(label, tooltip ?? label), null, height);
        }
        bool WideButton(string label, string tooltip = null, GUIStyle style = null)
        {
            GUI.SetNextControlName(label);
            return ui.Button(new GUIContent(label, tooltip ?? label), style,
                GUILayout.MinHeight(label.Contains("\n") ? ui.ModuleHeight : ui.RowHeight), GUILayout.Width(RowWidth));
        }
        bool ToolButton(string label, string icon, string tooltip = null)
        {
            GUI.SetNextControlName(label);
            bool clicked = ui.Button(new GUIContent("", tooltip ?? label), null,
                GUILayout.Width(ui.RowHeight), GUILayout.Height(ui.RowHeight));
            if (Event.current.type != EventType.Repaint) return clicked;
            var rect = GUILayoutUtility.GetLastRect();
            float size = Mathf.Min(22, rect.height - 10);
            var tint = GUI.color;
            try
            {
                GUI.color = GUI.enabled ? ui.Ink : ui.DisabledText.normal.textColor;
                GUI.DrawTexture(new Rect(rect.center - Vector2.one * size * .5f, Vector2.one * size),
                    LobbyIcons.Get(icon).texture, ScaleMode.ScaleToFit, true);
            }
            finally { GUI.color = tint; }
            return clicked;
        }
        void Hint(string text) => GUILayout.Label(text, hintStyle, GUILayout.Width(RowWidth));
        void Awake() => LoadSections();
        void LoadSections()
        {
            showWorkshopHelp = SavedSection("view", showWorkshopHelp); placementLimits = SavedSection("range", placementLimits);
            routesExpanded = SavedSection("routes", routesExpanded); puzzleLinks = SavedSection("unlocks", puzzleLinks);
        }
        static bool SavedSection(string name, bool fallback) => PlayerPrefs.GetInt(SectionPrefix + name, fallback ? 1 : 0) != 0;
        void RememberSection(string name, bool open) { sections[name] = open; PlayerPrefs.SetInt(SectionPrefix + name, open ? 1 : 0); }
        bool Section(string name, string caption, ref bool open)
        {
            GUI.SetNextControlName("workshop-section:" + (name ?? caption));
            bool next = GUILayout.Toggle(open, (open ? "▾  " : "▸  ") + caption, sectionStyle,
                GUILayout.Width(RowWidth), GUILayout.MinHeight(ui.RowHeight));
            if (next != open && name != null) RememberSection(name, next);
            open = next;
            return next;
        }
        bool Section(string name, string caption, bool fallback)
        {
            if (!sections.TryGetValue(name, out bool open)) open = SavedSection(name, fallback);
            Section(name, caption, ref open);
            sections[name] = open;
            return open;
        }
        bool Advanced(ref bool open)
        {
            GUILayout.Space(ui.Gap);
            return Section(null, "Advanced", ref open);
        }
        string Field(string label, string value, double step = 0, double fine = 0, string name = null,
            double min = double.MinValue, double max = double.MaxValue)
        {
            float width = RowWidth, head = Mathf.Max(LabelWidth(width), Mathf.Min(CaptionWidth(label), Mathf.Round(width * .55f)));
            GUILayout.BeginHorizontal();
            value = Cell(label, name ?? label, value, step, fine, head, width, min, max);
            GUILayout.EndHorizontal();
            return value;
        }
        string[] Cells(string label, string name, string[] captions, string[] values, double step, double fine,
            double min = double.MinValue, double max = double.MaxValue)
        {
            float width = RowWidth, head = LabelWidth(width), caption = captions.Max(text => CaptionWidth(text));
            bool inline = (width - head - 4) / values.Length - caption - 8 >= MinimumField;
            if (!inline) GUILayout.Label(new GUIContent(label, name), rowLabel, GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            if (inline) GUILayout.Label(new GUIContent(label, name), rowLabel, GUILayout.Width(head), GUILayout.MinHeight(ui.RowHeight));
            float each = (width - (inline ? head + 4 : 0)) / values.Length;
            for (int i = 0; i < values.Length; i++)
                values[i] = Cell(captions[i], name + " " + captions[i], values[i], step, fine, CaptionWidth(captions[i]), each, min, max);
            GUILayout.EndHorizontal();
            return values;
        }
        void Pair(string label, string name, string firstCaption, ref string first, string secondCaption, ref string second,
            double step, double fine = 0, double min = double.MinValue, double max = double.MaxValue)
        {
            var values = Cells(label, name, new[] { firstCaption, secondCaption }, new[] { first, second }, step, fine, min, max);
            first = values[0]; second = values[1];
        }
        void Triple(string label, string name, ref string x, ref string y, ref string z, double step, double fine = 0)
        {
            var values = Cells(label, name, new[] { "X", "Y", "Z" }, new[] { x, y, z }, step, fine);
            x = values[0]; y = values[1]; z = values[2];
        }
        string Cell(string caption, string name, string value, double step, double fine, float captionWidth, float width,
            double min = double.MinValue, double max = double.MaxValue)
        {
            GUILayout.Label(new GUIContent(caption, step > 0 ? name + " · drag sideways to adjust, Shift for fine steps" : name), rowLabel,
                GUILayout.Width(captionWidth), GUILayout.MinHeight(ui.RowHeight));
            if (step > 0) value = Scrub(GUILayoutUtility.GetLastRect(), name, value, step, fine, min, max);
            GUI.SetNextControlName("workshop-text:" + name);
            return GUILayout.TextField(value ?? "", rowField, GUILayout.Width(Mathf.Max(24, width - captionWidth - 8)),
                GUILayout.MinHeight(ui.RowHeight));
        }
        bool ChoiceRow(string label, string caption)
        {
            float width = RowWidth, head = LabelWidth(width);
            bool inline = rowLabel.CalcSize(new GUIContent(label)).x <= head;
            if (!inline) GUILayout.Label(label, ui.Wrapped, GUILayout.Width(width));
            GUILayout.BeginHorizontal();
            if (inline) GUILayout.Label(new GUIContent(label, label), rowLabel, GUILayout.Width(head), GUILayout.MinHeight(ui.RowHeight));
            GUI.SetNextControlName("workshop-choice:" + label + ":open");
            bool clicked = ui.Button(new GUIContent(caption, caption), null,
                GUILayout.Width(Mathf.Max(40, width - (inline ? head + 8 : 4))), GUILayout.MinHeight(ui.RowHeight));
            GUILayout.EndHorizontal();
            return clicked;
        }
        string Scrub(Rect handle, string name, string value, double step, double fine, double min, double max)
        {
            int id = GUIUtility.GetControlID(ScrubHint, FocusType.Passive, handle);
            var input = Event.current;
            switch (input.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (input.button != 0 || !GUI.enabled || !handle.Contains(input.mousePosition)) break;
                    GUIUtility.hotControl = id; GUI.FocusControl(null); workshopTyping = false;
                    scrubbing = name; scrubValue = Number(value); input.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != id) break;
                    double unit = input.shift && fine > 0 ? fine : step;
                    scrubValue = Math.Max(min, Math.Min(max, scrubValue + input.delta.x / ScrubPixels * unit));
                    double snapped = Math.Max(min, Math.Min(max, Math.Round(scrubValue / unit, MidpointRounding.AwayFromZero) * unit)) + 0d;
                    value = snapped.ToString("0.###", CultureInfo.InvariantCulture);
                    GUI.changed = true; input.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0; scrubbing = null; scrubReleased = name; input.Use();
                    break;
            }
            return value;
        }
        bool ScrubReleased(string name)
        {
            if (scrubReleased != name) return false;
            scrubReleased = null;
            return true;
        }
        static double Number(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0;
        float RowWidth => Mathf.Max(80, rowWidth);
        float GridMetres => History.Current.GridMm / 1000f;
        static float LabelWidth(float width) => Mathf.Round(Mathf.Clamp(width * .34f, 44, 120));
        float CaptionWidth(string caption) => Mathf.Ceil(rowLabel.CalcSize(new GUIContent(caption)).x) + 2;
        static float StepPanelWidth(float screenWidth) => Mathf.Round(Mathf.Clamp(screenWidth * .27f, 300, 460));
        void Styles()
        {
            if (panel != null && styleGeneration == ui.Generation) return;
            styleGeneration = ui.Generation;
            panel = ui.Panel; heading = ui.Heading;
            objectTileName = null;
            viewLabel = importantViewLabel = recoveryText = null;
            sectionStyle = new GUIStyle(GUI.skin.label) { font = heading.font, fontSize = GUI.skin.label.fontSize + 1,
                alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip };
            rowLabel = new GUIStyle(ui.SingleLine);
            rowField = new GUIStyle(GUI.skin.textField);
            rowField.padding = new RectOffset(6, 6, rowField.padding.top, rowField.padding.bottom);
            normalBox = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            selectedBox = new GUIStyle(normalBox); selectedBox.normal.textColor = ui.Ink;
            ghostBox = new GUIStyle(normalBox); ghostBox.normal.textColor = new Color(.6f, .6f, .6f);
            issueBox = new GUIStyle(normalBox); issueBox.normal.textColor = ui.Error;
            cardStyle = new GUIStyle(ui.Panel) { padding = new RectOffset(10, 10, 8, 8), margin = new RectOffset(0, 0, 4, 6) };
            cardTitle = new GUIStyle(sectionStyle) { wordWrap = true, clipping = TextClipping.Overflow };
            hintStyle = new GUIStyle(ui.Wrapped);
            hintStyle.normal.textColor = Color.Lerp(ui.Ink, ui.DisabledText.normal.textColor, .35f);
        }
        void OnGUI()
        {
            if (!Visible || History == null) return;
            using var themeScope = ui.Apply(Theme);
            if (RecoveryChoicePending) { Styles(); RecoveryPanel(); return; }
            Styles(); Refresh();
            if (OnlineStarting) { OnlinePreparationPanel(); return; }
            if (sharing) { SharePanel(); return; }
            if (libraryOpen) { LibraryPanel(); return; }
            if (filesOpen) { FilesPanel(); return; }
            if (GUIUtility.hotControl == 0) scrubbing = null;
            scrubReleased = null;
            var screen = new Vector2(Screen.width, Screen.height);
            float side = StepPanelWidth(Screen.width), top = 16 + ui.ChromeHeight, bottom = ui.FooterRect(screen).y - 8;
            inspectorWidth = side;
            viewport = new Rect(side + 16, top, Mathf.Max(1, Screen.width - side - 24), Mathf.Max(1, bottom - top));
            TopBar();
            GUILayout.BeginArea(new Rect(8, viewport.y, side, viewport.height), panel); StepPanel(); GUILayout.EndArea();
            WorldView();
            Footer();
            CaptureWorkshopTextFocus();
            string footerText = string.IsNullOrEmpty(GUI.tooltip) ? status : GUI.tooltip;
            GUI.Label(ui.StatusRect(screen), new GUIContent(footerText, footerText), ui.SingleLine);
        }
        void TopBar()
        {
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, ui.ChromeHeight), panel); GUILayout.BeginHorizontal();
            TitleField(Mathf.Round(Mathf.Clamp(Screen.width * .22f, 160, 380)));
            GUILayout.Space(ui.Gap);
            var format = WorkshopModes.FormatOf(PlayRequirements);
            string mode = "Mode: " + WorkshopModes.Name(PlayRequirements.ModeId) +
                (PlayRequirements.ModeId == "TeamRace" || PlayRequirements.ModeId == "BrawlRace" ? " · " + format : "") + " ▾";
            if (Button(mode, "Choose how this map is played")) ShowStep(WorkshopStep.Rules);
            GUILayout.Space(ui.Gap);
            string saved = SaveState;
            float savedWidth = new[] { "Saved ✓", "Saving…", "Not saved" }.Max(text => CaptionWidth(text));
            GUILayout.Label(new GUIContent(saved, saveError.Length == 0 ? "Your map saves itself a moment after each change." : saveError),
                rowLabel, GUILayout.Width(savedWidth), GUILayout.MinHeight(ui.RowHeight));
            GUILayout.FlexibleSpace();
            GUI.enabled = PlaytestRequested != null;
            if (Button("Test", "Play this map on your own · runs the check first"))
                Attempt(() => { RequestPlaytest(); if (LastPlaytestDraft == null) ShowStep(WorkshopStep.Share); });
            GUI.enabled = true;
            if (Button("Share", "Share code and map files")) OpenShare();
            if (Button(CloseLabel ?? "Exit", "Leave the workshop · your map is saved")) RequestClose();
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }
        void TitleField(float width)
        {
            var input = Event.current;
            bool focused = GUI.GetNameOfFocusedControl() == TitleControl;
            if (focused && input.type == EventType.KeyDown && (input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter))
            { CommitTitle(); GUI.FocusControl(null); input.Use(); focused = false; }
            else if (focused && input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            { title = display.Title; titleEditing = false; GUI.FocusControl(null); input.Use(); focused = false; }
            GUI.SetNextControlName(TitleControl);
            title = GUILayout.TextField(title ?? "", 80, rowField, GUILayout.Width(width), GUILayout.MinHeight(ui.RowHeight));
            if (titleEditing && !focused) CommitTitle();
            titleEditing = focused;
        }
        void CommitTypedTitle()
        {
            if (!titleEditing || History == null) return;
            CommitTitle();
            if (Event.current != null) GUI.FocusControl(null);
        }
        void CommitTitle()
        {
            titleEditing = false;
            string next = (title ?? "").Trim();
            if (next == display.Title) { title = display.Title; return; }
            if (next.Length == 0) { title = display.Title; ShowStatus("A map needs a name."); return; }
            Edit("Rename", draft => draft.Course.Title = next);
            title = display.Title;
        }
        void Footer()
        {
            GUILayout.BeginArea(ui.FooterRect(new Vector2(Screen.width, Screen.height)), panel); GUILayout.BeginHorizontal();
            GUI.enabled = History.CanUndo; if (ToolButton("Undo", LobbyIcons.Undo, "Undo · Ctrl+Z")) StepHistory(false); GUI.enabled = true;
            GUI.enabled = History.CanRedo; if (ToolButton("Redo", LobbyIcons.Redo, "Redo · Ctrl+Y")) StepHistory(true); GUI.enabled = true;
            GUILayout.Space(ui.Gap);
            string floor = "Floor: " + History.Current.ActiveLayer;
            GUILayout.Label(floor, rowLabel, GUILayout.Width(CaptionWidth(floor)), GUILayout.MinHeight(ui.RowHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal(); GUILayout.EndArea();
            MapToolbar();
        }
        void MapToolbar()
        {
            GUILayout.BeginArea(new Rect(viewport.position + ToolbarRect.position, ToolbarRect.size), panel);
            GUILayout.BeginHorizontal();
            if (Button("Grid " + Metres(History.Current.GridMm) + " m", "Change how far things snap when you move them"))
                Edit("Grid", draft => draft.GridMm = draft.GridMm == 500 ? 1000 : draft.GridMm == 1000 ? 100 : 500);
            if (ToolButton("Layout", LobbyIcons.Dice, showRandomness ? "Hide layout choices" : "Layout · keep it or shuffle each game"))
                showRandomness = !showRandomness;
            if (ToolButton("Frame", LobbyIcons.Frame, "Frame the selection, or the whole floor · F")) FrameSelection();
            if (ToolButton("Top view", LobbyIcons.Eye, "Look straight down")) { EnsureView(); view.Top(); }
            GUILayout.EndHorizontal();
            if (showRandomness)
            {
                GUILayout.BeginHorizontal();
                int locked = History.Current.SeedLocked ? 0 : 1;
                int next = GUILayout.Toolbar(locked, new[] { "Keep layout", "Shuffle each game" }, GUILayout.MinHeight(ui.RowHeight));
                if (next != locked) Edit(next == 0 ? "Keep layout" : "Shuffle each game", draft => draft.SeedLocked = next == 0);
                GUI.enabled = !History.Current.SeedLocked;
                if (ToolButton("Try another layout", LobbyIcons.Dice, "Try another random layout"))
                    Attempt(() => History.Reroll(Guid.NewGuid().ToString("N").Substring(0, 16)));
                GUI.enabled = true; GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }
        void FocusIssue(CourseIssue issue)
        {
            string id = issue.EntityId ?? "", path = issue.Path ?? "";
            var zone = display.Zones.FirstOrDefault(item => item.Id == id);
            var puzzle = display.Puzzles.FirstOrDefault(item => item.Id == id);
            var supply = display.Supplies.FirstOrDefault(item => item.Id == id);
            var spawn = display.Spawns.FirstOrDefault(item => item.Id == id);
            var safe = display.RecoveryAnchors.FirstOrDefault(item => item.Id == id);
            var stop = display.Checkpoints.FirstOrDefault(item => item.Id == id);
            var link = display.Connections.FirstOrDefault(item => item.Id == id);
            var route = display.RouteSections.FirstOrDefault(item => item.Id == id);
            string owner = display.Modules.FirstOrDefault(item => item.Id == id)?.Id ?? zone?.ModuleId ?? puzzle?.ModuleId ??
                supply?.ModuleId ?? spawn?.ModuleId ?? safe?.ModuleId ?? stop?.ModuleId ?? link?.FromModuleId ?? route?.EntryModuleId;
            var module = display.Modules.FirstOrDefault(item => item.Id == owner);
            if (module != null)
            {
                if (History.Current.ActiveLayer != module.LayerId || History.Current.HiddenLayers.Contains(module.LayerId))
                    History.Execute("Show floor", draft => { draft.ActiveLayer = module.LayerId; draft.HiddenLayers.Remove(module.LayerId); });
                History.Select(new[] { module.Id }, false, false);
                view?.Focus(module.Position); Refresh();
            }
            if (zone != null) SelectZone(zone);
            else if (puzzle != null || supply != null || path.StartsWith("$.puzzles", StringComparison.Ordinal) ||
                path.StartsWith("$.supplies", StringComparison.Ordinal) || path.StartsWith("$.physicalSites", StringComparison.Ordinal))
            { ShowStep(WorkshopStep.Puzzles); newPuzzle = false; sitePuzzle = ""; if (puzzle != null) puzzleId = puzzle.Id; }
            else if (spawn != null || path.StartsWith("$.spawns", StringComparison.Ordinal)) { ShowRoute(4); if (spawn != null) routeSpawn = spawn.Id; }
            else if (safe != null || path.StartsWith("$.recoveryAnchors", StringComparison.Ordinal))
            { ShowRoute(0); if (safe != null) { routeModule = safe.ModuleId; routeRecovery = safe.Id; } }
            else if (stop != null || path.StartsWith("$.checkpoints", StringComparison.Ordinal))
            { ShowRoute(1); if (stop != null) { routeModule = stop.ModuleId; routeCheckpoint = stop.Id; } }
            else if (route != null || path.StartsWith("$.routeSections", StringComparison.Ordinal)) { ShowRoute(2); if (route != null) routeSection = route.Id; }
            else if (path.StartsWith("$.laps", StringComparison.Ordinal)) ShowRoute(3);
            else if (path.StartsWith("$.supportedModes", StringComparison.Ordinal) || path.StartsWith("$.play", StringComparison.Ordinal)) ShowStep(WorkshopStep.Rules);
            else if (module != null || link != null) ShowStep(WorkshopStep.Build);
            status = issue.Message;
        }
        void ShowRoute(int tab)
        {
            ShowStep(WorkshopStep.Rules);
            if (!routesExpanded) RememberSection("routes", true);
            routesExpanded = true; routeTab = tab; routeLoadKey = ""; routeChoiceOpen = "";
        }
        void DrawZone(CoursePoint from, CoursePoint to)
        {
            Attempt(() =>
            {
                string nextId = zoneId;
                var settings = PlacementSettings();
                History.Execute("Placement", draft =>
                {
                    var module = draft.Course.Modules.First(item => draft.Selection.Contains(item.Id));
                    from = new CoursePoint(WorkshopCommands.Snap(from.X, draft.GridMm), module.Position.Y, WorkshopCommands.Snap(from.Z, draft.GridMm));
                    to = new CoursePoint(WorkshopCommands.Snap(to.X, draft.GridMm), module.Position.Y, WorkshopCommands.Snap(to.Z, draft.GridMm));
                    var existing = draft.Course.Zones.FirstOrDefault(item => item.Id == zoneId);
                    var bounds = zoneMode == 0 ? Catalogue.Bounds(module) :
                        ResizeBounds(existing?.Bounds ?? new CourseBox(from, from), from, to);
                    nextId = settings.Apply(draft, Catalogue, module.Id, zoneId, bounds, zoneMode == 0 ? to : (CoursePoint?)null);
                });
                zoneId = nextId; Refresh(); status = "Objects placed · undo available";
            });
        }
        void Connect(CourseModule first, CourseModule second)
        {
            Edit("Connect rooms", draft =>
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
