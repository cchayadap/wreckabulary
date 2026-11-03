using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        bool libraryOpen, filesOpen, savingAs;
        string saveAsName = "", libraryError = "";
        WorkshopShareImport pendingImport;
        string pendingImportTitle = "";
        Vector2 libraryScroll;
        IReadOnlyList<WorkshopMapEntry> libraryEntries = Array.Empty<WorkshopMapEntry>();
        IReadOnlyList<WorkshopFileEntry> fileEntries = Array.Empty<WorkshopFileEntry>();
        const string TemplatesFolder = "templates/", FlagshipSource = "templates/Flagship", ParcelSource = "local-courses/ParcelServiceV1";

        sealed class ChecklistItem
        {
            public string Message;
            public Action Show;
        }

        List<ChecklistItem> Checklist()
        {
            var items = new List<ChecklistItem>();
            void Add(string message, Action show) { if (items.All(item => item.Message != message)) items.Add(new ChecklistItem { Message = message, Show = show }); }
            if (!SelectedModeEnabled) Add("This map doesn't support the chosen mode.", () => ShowStep(WorkshopStep.Rules));
            foreach (var issue in validation.Issues.Where(issue => issue.Code != "mode.unsupported").Take(12))
            { var found = issue; Add(found.Message, () => FocusIssue(found)); }
            if (LastPreview != null && !LastPreview.Success)
                foreach (var issue in LastPreview.Issues.Take(8)) { var found = issue; Add(found.Message, () => FocusIssue(found)); }
            if (lastPlaytestIssue.Length != 0) Add(lastPlaytestIssue, () => { ShowStep(WorkshopStep.Puzzles); sections["controls"] = true; });
            if (localPlaytest && SelectedModeEnabled && !CanPlaytestLocally)
                Add("Test alone works in Time Run or Freestyle. Friends can play this mode online.", () => ShowStep(WorkshopStep.Rules));
            return items;
        }

        void ShareStep()
        {
            var items = Checklist();
            if (Section("checklist", items.Count == 0 ? "Checklist · no problems" : "Checklist · " + items.Count + " to fix", true))
            {
                if (items.Count == 0) GUILayout.Label(LastPreview != null && LastPreview.Success ? "✓ Checked · ready to play" :
                    "✓ No problems found yet · Check map tests the whole map", ui.Wrapped, GUILayout.Width(RowWidth));
                float show = Mathf.Ceil(GUI.skin.button.CalcSize(new GUIContent("Show")).x) + 4;
                foreach (var item in items)
                {
                    GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
                    GUILayout.Label(item.Message, ui.Wrapped, GUILayout.Width(Mathf.Max(40, RowWidth - show - 8)));
                    if (Button("Show", "Select what this is about", show)) { var chosen = item; Attempt(() => chosen.Show()); }
                    GUILayout.EndHorizontal();
                }
                GUI.enabled = SelectedModeEnabled;
                if (WideButton("Check map", "Check the whole map with the chosen mode and layout")) Attempt(() => Preview());
                GUI.enabled = true;
            }
            if (Section("play", "Play", true))
            {
                GUI.enabled = PlaytestRequested != null && SelectedModeEnabled && (!localPlaytest || CanPlaytestLocally);
                if (WideButton("Test alone", "Play this map on your own · runs the check first", ui.Primary)) Attempt(RequestPlaytest);
                GUI.enabled = true;
                if (OnlinePlaytestRequested != null)
                {
                    GUI.enabled = SelectedModeEnabled;
                    if (WideButton("Play online with friends", "Share this exact map with a private party. Connect as a guest first; leave any existing party."))
                        Attempt(RequestOnlinePlaytest);
                    GUI.enabled = true;
                }
            }
            if (Section("save", "Save", true))
            {
                GUILayout.Label(SaveState + " · " + display.Title + (inspectorAdvancedIds ? " · " + display.Id : ""), ui.Wrapped, GUILayout.Width(RowWidth));
                if (saveError.Length != 0) GUILayout.Label(saveError, ui.Wrapped, GUILayout.Width(RowWidth));
                if (WideButton("Save", "Save now · your map also saves itself after each change"))
                    Attempt(() => { CommitTypedTitle(); if (SaveNow()) ShowStatus("Saved ✓ · " + display.Title); });
                if (savingAs)
                {
                    saveAsName = Field("Name", saveAsName, name: "New map name");
                    GUILayout.BeginHorizontal(GUILayout.Width(RowWidth));
                    if (ui.Button(new GUIContent("Save as new map", "Save a copy with this name and keep editing the copy"), ui.Primary,
                        GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true))) Attempt(SaveAsNewMap);
                    if (ui.Button(new GUIContent("Cancel"), null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true))) savingAs = false;
                    GUILayout.EndHorizontal();
                }
                else if (WideButton("Save as new map…", "Save a copy under a new name; the original stays in My maps"))
                { savingAs = true; saveAsName = CopyName(display.Title); }
                if (WideButton("My maps", "See every map you have saved and open one")) Attempt(OpenLibrary);
            }
            if (Section("sharing", "Share", true))
            {
                if (WideButton("Share code", "Copy a code friends can paste into their workshop")) OpenShare();
                if (WideButton("Open file", "Open a .wordbound.json or .course.json map file")) Attempt(OpenFiles);
            }
            if (Advanced(ref shareAdvanced))
            {
                if (WideButton("Save to file", "Write a map file into the workshop folder")) Attempt(SaveToFile);
                GUILayout.Label("Workshop folder: " + storage.Folder, ui.Wrapped, GUILayout.Width(RowWidth));
            }
        }

        static string CopyName(string title)
        {
            const string suffix = " copy";
            string name = title.Length + suffix.Length > 80 ? title.Substring(0, 80 - suffix.Length).TrimEnd() : title;
            return name + suffix;
        }

        void KeepCurrentMap()
        {
            if (History.Revision == savedRevision && storage.HasRecovery(History.Current.Course().Id)) return;
            storage.Save(History.Current); savedRevision = History.Revision; saveError = "";
        }

        void SaveAsNewMap()
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before saving a new map.");
            CancelGesture(); CommitTypedTitle();
            KeepCurrentMap();
            var copy = storage.SaveAs(History.Current, saveAsName);
            OpenInSession(copy);
            RememberMode();
            savingAs = false;
            ShowStatus("Saved as a new map · " + copy.Course().Title + " · the original stays in My maps");
        }

        void SaveToFile()
        {
            string path;
            try { path = storage.SavePlayableShare(History.Current, Catalogue, PlayRequirements); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                path = storage.SaveShare(History.Current);
                ShowStatus("Saved " + Path.GetFileName(path) + " · layout only, because " + error.Message);
                return;
            }
            ShowStatus("Saved " + Path.GetFileName(path) + " · map, mode and layout");
        }

        void OpenLibrary()
        {
            RequireRecoveryChoice();
            CancelGesture(); CommitTypedTitle();
            if (History.Revision != savedRevision) SaveNow();
            libraryScroll = Vector2.zero; libraryOpen = true;
            try { libraryEntries = WorkshopStorage.Library(storageFolder ?? storage.Folder); libraryError = ""; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { libraryEntries = Array.Empty<WorkshopMapEntry>(); libraryError = error.Message; }
        }

        bool SameFolder(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);

        bool IsCurrentMap(WorkshopMapEntry entry) => SameFolder(entry.Folder, storage.Folder) && entry.CourseId == display.Id;

        static bool TemplateSource(string source, out WorkshopTemplate template)
        {
            template = default;
            if (!source.StartsWith(TemplatesFolder, StringComparison.Ordinal)) return false;
            string name = source.Substring(TemplatesFolder.Length);
            foreach (WorkshopTemplate value in Enum.GetValues(typeof(WorkshopTemplate)))
                if (value.ToString() == name) { template = value; return true; }
            return false;
        }

        string SourceLabel(WorkshopMapEntry entry) => entry.Source == "" ? HasAssignedCourse ? "Assigned course" : "Workshop" :
            entry.Source == FlagshipSource ? "Flagship" : entry.Source == ParcelSource ? "Parcel Service" :
            TemplateSource(entry.Source, out var template) ? TemplateLabel(template) : entry.Source;

        string OpenBlocker(WorkshopMapEntry entry)
        {
            if (!entry.Readable) return entry.Problem;
            if (entry.Source == "") return HasAssignedCourse || SameFolder(entry.Folder, storage.Folder) ? "" :
                "Saved by an older version · open its template to continue it.";
            if (entry.Source == FlagshipSource) return flagshipContent != null ? "" : "The Flagship course isn't available here.";
            if (entry.Source == ParcelSource) return HasParcelService && parcelServiceUnavailable == null ? "" : "Parcel Service isn't available here.";
            return TemplateSource(entry.Source, out _) ? "" : "This map belongs to a course that isn't available here.";
        }

        void OpenSavedMap(WorkshopMapEntry entry)
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing maps.");
            string blocker = OpenBlocker(entry);
            if (blocker.Length != 0) throw new InvalidOperationException(blocker);
            if (entry.Source == "") { if (HasAssignedCourse) OpenAssignedCourse(); }
            else if (entry.Source == FlagshipSource) OpenFlagship(flagshipContent);
            else if (entry.Source == ParcelSource) OpenParcelService();
            else if (TemplateSource(entry.Source, out var template)) OpenTemplate(template);
            libraryOpen = false;
            if (RecoveryChoicePending) return;
            if (!SameFolder(entry.Folder, storage.Folder)) throw new InvalidOperationException("This map belongs to another course.");
            if (display.Id == entry.CourseId) { ShowStatus("Editing " + display.Title); return; }
            KeepCurrentMap();
            var snapshot = storage.Recover(entry.CourseId, out bool backup);
            OpenInSession(snapshot);
            ApplyRememberedMode();
            ShowStatus("Opened " + snapshot.Course().Title + (backup ? " · from its previous save" : ""));
        }

        static string When(DateTime utc) => utc == DateTime.MinValue ? "date unknown" :
            utc.ToLocalTime().ToString("d MMM yyyy · HH:mm", CultureInfo.InvariantCulture);

        Rect ModalRect(float maximum)
        {
            float width = Mathf.Min(maximum, Screen.width - 32);
            return new Rect((Screen.width - width) * .5f, 24, width, Mathf.Max(1, Screen.height - 48));
        }

        void LibraryPanel()
        {
            GUILayout.BeginArea(ModalRect(760), panel);
            GUILayout.Label("My maps", heading);
            GUILayout.Label("Your maps save themselves. Open one to keep editing it.", ui.Wrapped);
            libraryScroll = GUILayout.BeginScrollView(libraryScroll);
            float open = Mathf.Ceil(GUI.skin.button.CalcSize(new GUIContent("Editing")).x) + 12;
            foreach (var entry in libraryEntries)
            {
                bool current = IsCurrentMap(entry);
                string blocker = OpenBlocker(entry);
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label((current ? "● " : "") + (entry.Readable ? entry.Title : "Unreadable save · " + entry.CourseId) + "\n" +
                    SourceLabel(entry) + " · " + When(entry.SavedUtc) + (inspectorAdvancedIds ? " · " + entry.CourseId : ""), ui.Wrapped, GUILayout.ExpandWidth(true));
                GUI.enabled = !current && blocker.Length == 0;
                if (Button(current ? "Editing" : "Open", blocker.Length == 0 ? "Open " + entry.Title : blocker, open)) { var chosen = entry; Attempt(() => OpenSavedMap(chosen)); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (libraryEntries.Count == 0) GUILayout.Label("No saved maps yet · make a change and it saves itself.", ui.Wrapped);
            GUILayout.Space(ui.Gap * 2);
            GUILayout.Label("Templates", sectionStyle);
            GUILayout.Label("Opens the map you last worked on for that template. Use Save as new map for another copy.", ui.Wrapped);
            var sources = new List<(string label, int choice)>();
            if (HasAssignedCourse) sources.Add(("Assigned course", 1));
            foreach (WorkshopTemplate template in Enum.GetValues(typeof(WorkshopTemplate))) sources.Add((TemplateLabel(template), 2 + (int)template));
            if (flagshipContent != null) sources.Add(("Flagship", 5));
            if (HasParcelService) sources.Add(("Parcel Service", 6));
            int columns = Mathf.Max(1, Mathf.Min(3, Mathf.FloorToInt((ModalRect(760).width - 40) / 200)));
            for (int i = 0; i < sources.Count; i++)
            {
                if (i % columns == 0) GUILayout.BeginHorizontal();
                var (label, choice) = sources[i];
                bool active = choice == 1 ? UsesAssignedCourse : choice == 5 ? usesFlagship : choice == 6 ? UsesParcelService :
                    currentTemplate.HasValue && 2 + (int)currentTemplate.Value == choice;
                if (ui.Button(new GUIContent((active ? "● " : "") + label), active ? ui.Selected : null, GUILayout.MinHeight(ui.RowHeight), GUILayout.ExpandWidth(true)))
                { libraryOpen = false; if (!active) QueueSourceChoice(choice); }
                if (i % columns == columns - 1 || i == sources.Count - 1) GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (libraryError.Length != 0) GUILayout.Label(libraryError, ui.Wrapped);
            GUILayout.Label(status, ui.Wrapped);
            if (Button("Close")) libraryOpen = false;
            GUILayout.EndArea();
        }

        void OpenFiles()
        {
            RequireRecoveryChoice();
            CancelGesture();
            filesOpen = true; libraryScroll = Vector2.zero; SetPendingImport(null);
            RefreshFiles();
        }

        void RefreshFiles()
        {
            try { fileEntries = storage.Files(); libraryError = ""; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { fileEntries = Array.Empty<WorkshopFileEntry>(); libraryError = error.Message; }
        }

        void FilesPanel()
        {
            GUILayout.BeginArea(ModalRect(760), panel);
            GUILayout.Label("Open a map file", heading);
            if (pendingImport != null) { ImportConfirmation(); GUILayout.EndArea(); return; }
            GUILayout.Label("Map files in your workshop folder. To open a file from a friend, put it in this folder and refresh:", ui.Wrapped);
            GUILayout.Label(storage.Folder, ui.Wrapped);
            libraryScroll = GUILayout.BeginScrollView(libraryScroll);
            float open = Mathf.Ceil(GUI.skin.button.CalcSize(new GUIContent("Open")).x) + 12;
            foreach (var file in fileEntries)
            {
                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label(file.Name + "\n" + (file.Playable ? "Map, mode and layout" : "Layout only") + " · " + When(file.SavedUtc),
                    ui.Wrapped, GUILayout.ExpandWidth(true));
                if (Button("Open", "Open " + Path.GetFileName(file.Path), open))
                { var chosen = file; Attempt(() => SetPendingImport(ParseFile(chosen))); }
                GUILayout.EndHorizontal();
            }
            if (fileEntries.Count == 0) GUILayout.Label("No .wordbound.json or .course.json files here yet. Save to file writes one.", ui.Wrapped);
            GUILayout.EndScrollView();
            if (libraryError.Length != 0) GUILayout.Label(libraryError, ui.Wrapped);
            GUILayout.Label(status, ui.Wrapped);
            GUILayout.BeginHorizontal();
            if (Button("Refresh")) RefreshFiles();
            if (Button("Close")) filesOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void SetPendingImport(WorkshopShareImport value)
        {
            pendingImport = value;
            pendingImportTitle = value == null ? "" : value.Snapshot.Course().Title;
        }

        WorkshopShareImport ParseFile(WorkshopFileEntry file)
        {
            RequireImport();
            return storage.OpenFile(file.Path, Catalogue);
        }

        WorkshopShareImport ParseImport(string json)
        {
            RequireImport();
            return WorkshopShare.Import(json, Catalogue);
        }

        void RequireImport()
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before importing a map.");
            if (History == null) throw new InvalidOperationException("Open a map before importing.");
        }

        void ImportConfirmation()
        {
            GUILayout.Label("Replace the map you're editing?", sectionStyle);
            GUILayout.Label("“" + pendingImportTitle + "” opens in its place. Your current map “" + display.Title + "” stays in My maps.", ui.Wrapped);
            GUILayout.Label(pendingImport.HasPlaySettings ? "The file includes its mode and layout." : "The file has the layout only · your chosen mode stays.", ui.Wrapped);
            GUILayout.BeginHorizontal();
            if (Button("Replace", "Import this map")) Attempt(() =>
            {
                ApplyImport(pendingImport);
                SetPendingImport(null); sharing = filesOpen = false; Frame();
            });
            if (Button("Cancel")) SetPendingImport(null);
            GUILayout.EndHorizontal();
            GUILayout.Label(status, ui.Wrapped);
        }

        void SharePanel()
        {
            GUILayout.BeginArea(new Rect(Screen.width * .12f, Screen.height * .1f, Screen.width * .76f, Screen.height * .8f), panel);
            GUILayout.Label("Share map", heading);
            if (pendingImport != null) { ImportConfirmation(); GUILayout.EndArea(); return; }
            int selected = shareDraftOnly ? 1 : 0;
            int next = GUILayout.Toolbar(selected, new[] { "Playable code", "Layout only" }, GUILayout.MinHeight(ui.RowHeight));
            if (next != selected) Attempt(() => PrepareShare(next == 1));
            GUILayout.Label(shareDraftOnly ? "Just the rooms, objects and puzzles · no mode or layout seed." :
                "The map with its mode and layout · friends play exactly this.", ui.Wrapped);
            GUI.SetNextControlName("Shared course JSON");
            shareText = GUILayout.TextArea(shareText, WorkshopShare.MaximumBytes, GUILayout.ExpandHeight(true));
            GUILayout.Label(status, ui.Wrapped);
            GUILayout.BeginHorizontal();
            if (Button("Copy")) { GUIUtility.systemCopyBuffer = shareText; ShowStatus("Copied · paste it to a friend"); }
            if (Button("Paste"))
            {
                string pasted = GUIUtility.systemCopyBuffer;
                if (pasted.Length > WorkshopShare.MaximumBytes) status = "That map code is too large.";
                else shareText = pasted;
            }
            if (Button("Import…", "Open the pasted code as your map")) Attempt(() => SetPendingImport(ParseImport(shareText)));
            if (Button("Save to file", "Write this map into the workshop folder")) Attempt(() => ShowStatus("Saved " + Path.GetFileName(shareDraftOnly ?
                storage.SaveShare(History.Current) : storage.SavePlayableShare(History.Current, Catalogue, PlayRequirements))));
            if (Button("Done")) { sharing = false; SetPendingImport(null); }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        void OpenShare()
        {
            CommitTypedTitle();
            sharing = true; SetPendingImport(null);
            try { PrepareShare(false); }
            catch (Exception error) when (error is ArgumentException || error is FormatException)
            {
                PrepareShare(true);
                ShowStatus("Layout only · " + error.Message);
            }
        }

        void PrepareShare(bool draftOnly)
        {
            string prepared = draftOnly ? History.Current.CourseJsonText : WorkshopShare.Export(History.Current, Catalogue, PlayRequirements);
            shareText = prepared; shareDraftOnly = draftOnly;
        }

        public void ImportSharedMap(string json) => ApplyImport(ParseImport(json));

        void ApplyImport(WorkshopShareImport imported)
        {
            RequireImport();
            if (imported == null) throw new ArgumentNullException(nameof(imported));
            CancelGesture();
            var snapshot = storage.PrepareImport(History.Current, imported.Snapshot, out string kept);
            savedRevision = History.Revision; saveError = "";
            var source = snapshot.Edit();
            History.Execute("Import map", target => Replace(target, source));
            if (imported.HasPlaySettings) PlayRequirements = imported.Mode.Requirements;
            CancelGesture(); LastPreview = null; LastPlaytestDraft = null; displayRevision = -1; Refresh();
            if (imported.HasPlaySettings) RememberMode();
            bool renamed = snapshot.Course().Id != imported.Snapshot.Course().Id, archived = kept != display.Id;
            ShowStatus((imported.HasPlaySettings ? "Map, layout and mode imported" : "Map imported · layout and mode were not included") +
                (renamed ? " · saved as a new map so nothing was overwritten" : "") +
                (archived ? " · your previous map is in My maps" : ""));
        }
    }
}
