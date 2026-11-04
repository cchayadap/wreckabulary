using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        WordboundFlagshipContent flagshipContent;
        readonly Dictionary<string, IReadOnlyList<string>> modeNeeds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        WorkshopHistory modeNeedsHistory;
        int modeNeedsRevision = -1;
        static readonly RaceFormat[] TeamFormats = { RaceFormat.Solo, RaceFormat.Duo, RaceFormat.Squad };

        public void ConfigureFlagship(WordboundFlagshipContent content) => flagshipContent = content;
        public void OpenFlagship(WordboundFlagshipContent content, string folder = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing courses.");
            if (usesFlagship && flagshipDraftHash == content.Hash && (folder == null || folder == storageFolder)) return;
            SaveAndRememberDraft();
            ConfigureFlagship(content);
            if (flagshipDraft != null && flagshipDraftHash == content.Hash && (folder == null || folder == flagshipDraft.Folder))
            {
                ActivateDraft(flagshipDraft, false, null, true);
                ShowStatus("Flagship draft · your edits and Undo history are retained."); return;
            }
            var back = CurrentSource();
            string baseFolder = folder ?? storageFolder ?? DefaultFolder;
            var scoped = new WorkshopStorage(Path.Combine(baseFolder, "templates", "Flagship"));
            var legacy = new WorkshopStorage(baseFolder);
            var snapshot = new WorkshopSnapshot(content.CopyDocument());
            string id = snapshot.Course().Id;
            var session = new DraftSession { History = new WorkshopHistory(snapshot), Catalogue = content.Geometry,
                Play = MatchStateValidation.Requirements(WorkshopModes.Definition("TimeRun")), Storage = scoped, Folder = baseFolder };
            flagshipDraftHash = content.Hash;
            string last = LastMapId(scoped, id);
            if (ReopenLastMap(session, scoped, last, null, true, "Flagship", out string problem)) return;
            var recovery = scoped.HasRecovery(id) ? scoped : !HasAssignedCourse && legacy.HasRecovery(id) ? legacy : null;
            OpenRecovered(session, recovery, id, back, null, true, false, "Lost Address draft · Time Trial · Check map tests this exact layout",
                "Recovered Flagship autosave", "Recovered previous Flagship save", "Start fresh from the Flagship course");
            AskAboutDamagedLastMap(scoped, last, problem, back);
        }
        bool localPlaytest;
        public bool CanPlaytestLocally => History != null && SelectedModeEnabled &&
            WorkshopModes.AllowsOneHumanLocal(PlayRequirements);
        public bool LocalPlaytestOnly => localPlaytest;
        bool SelectedModeEnabled => History != null && PlayRequirements != null &&
            (display != null && displayRevision == History.Revision ? display : History.Current.Course())
                .SupportedModes.Contains(PlayRequirements.ModeId);
        bool RaceSelected => PlayRequirements != null && WorkshopModes.Definition(PlayRequirements.ModeId).IsRace;

        public void ConfigureLocalPlaytest(bool enabled)
        {
            localPlaytest = enabled;
            if (enabled && History != null && !CanPlaytestLocally)
                ShowStatus("Test alone works in Time Trial or Freeplay with one player.");
        }

        public void SelectMode(string modeId, RaceFormat format = RaceFormat.Solo)
        {
            if (History == null) throw new InvalidOperationException("Open a map before selecting a mode.");
            var choice = WorkshopModes.Select(History.Current.Course(), modeId, format);
            CancelGesture(); PlayRequirements = choice.Requirements;
            LastPreview = null; LastPlaytestDraft = null; displayRevision = -1; Refresh();
            RememberMode();
            ShowStatus(WorkshopModes.Name(choice.ModeId) + (localPlaytest && !choice.AllowsOneHumanLocal
                ? " · Check map works; Test alone needs Time Trial or Freeplay."
                : " chosen · Check map and Test use this exact mode and layout."));
        }

        public void EnableFreestyleSandbox()
        {
            if (History == null) throw new InvalidOperationException("Open a map first.");
            CancelGesture();
            History.Execute("Add Freeplay", WorkshopModes.EnableFreestyleSandbox);
            SelectMode("Freestyle");
            ShowStatus("Freeplay added to this map and chosen · undo is available.");
        }

        public void ShowStatus(string message)
        {
            status = message ?? "";
            if (status.Length > 512) status = status.Substring(0, 512);
        }

        bool RemembersMode => currentTemplate.HasValue || usesFlagship || UsesParcelService;

        void RememberMode()
        {
            if (!RemembersMode || storage == null || History == null || PlayRequirements == null) return;
            try { storage.SaveSettings(History.Current.Course().Id, PlayRequirements.ModeId, WorkshopModes.FormatOf(PlayRequirements)); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { }
        }

        void ApplyRememberedMode()
        {
            if (!RemembersMode || storage == null || History == null) return;
            var course = History.Current.Course();
            if (!storage.TryLoadSettings(course.Id, out string mode, out RaceFormat format)) return;
            if (PlayRequirements != null && PlayRequirements.ModeId == mode && WorkshopModes.FormatOf(PlayRequirements) == format) return;
            try { PlayRequirements = WorkshopModes.Select(course, mode, format).Requirements; LastPreview = null; displayRevision = -1; Refresh(); }
            catch (ArgumentException) { }
        }

        void SetModeSupported(string mode, bool supported)
        {
            CancelGesture();
            History.Execute((supported ? "Add " : "Remove ") + WorkshopModes.Name(mode), draft => WorkshopModes.SetSupported(draft, mode, supported));
            displayRevision = -1; Refresh();
            if (supported && !SelectedModeEnabled) SelectMode(mode);
            else if (!supported && PlayRequirements.ModeId == mode)
            {
                var first = WorkshopModes.Choices(display).First();
                SelectMode(first.ModeId, first.Format);
            }
            ShowStatus(WorkshopModes.Name(mode) + (supported ? " added to this map" : " removed from this map") + " · undo is available.");
        }

        IReadOnlyList<string> ModeNeeds(string mode, RaceFormat format)
        {
            if (!ReferenceEquals(modeNeedsHistory, History) || modeNeedsRevision != History.Revision || !ReferenceEquals(display, modeNeedsDisplay))
            { modeNeeds.Clear(); modeNeedsHistory = History; modeNeedsRevision = History.Revision; modeNeedsDisplay = display; }
            string key = mode + "/" + format;
            if (!modeNeeds.TryGetValue(key, out var needs))
            {
                try { needs = WorkshopModes.Needs(display, Catalogue, mode, format, 2); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { needs = new[] { error.Message }; }
                modeNeeds[key] = needs;
            }
            return needs;
        }
        CourseDocument modeNeedsDisplay;

        void ModeCards()
        {
            foreach (string mode in WorkshopModes.All)
            {
                bool supported = display.SupportedModes.Contains(mode), selected = PlayRequirements.ModeId == mode;
                var format = selected ? WorkshopModes.FormatOf(PlayRequirements) : RaceFormat.Solo;
                var summary = WorkshopModes.Summary(mode, format);
                float inner = Mathf.Max(40, RowWidth - cardStyle.padding.horizontal);
                GUILayout.BeginVertical(cardStyle, GUILayout.Width(RowWidth));
                GUILayout.Label((selected ? "● " : "") + summary.Name + (selected ? " · chosen" : ""), cardTitle, GUILayout.Width(inner));
                GUILayout.Label("How to win: " + summary.HowToWin, ui.Wrapped, GUILayout.Width(inner));
                GUILayout.Label(summary.Players, ui.Wrapped, GUILayout.Width(inner));
                var needs = ModeNeeds(mode, format);
                if (needs.Count > 0) GUILayout.Label((supported ? "Still needs: " : "Would need: ") + string.Join(" · ", needs), hintStyle, GUILayout.Width(inner));
                else if (supported) GUILayout.Label("✓ Ready to play", ui.Wrapped, GUILayout.Width(inner));
                bool play = supported && !selected;
                float each = play ? Mathf.Floor((inner - ui.Gap) * .5f) : inner;
                GUILayout.BeginHorizontal(GUILayout.Width(inner));
                bool last = supported && display.SupportedModes.Count == 1;
                GUI.enabled = !last;
                GUI.SetNextControlName("Workshop mode " + mode);
                bool next = ui.Toggle(supported, new GUIContent(supported ? "✓ On this map" : "Add to this map",
                    last ? "A map needs at least one mode" : supported ? "Remove this mode from the map" : "Let this map be played in " + summary.Name),
                    null, GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(each));
                GUI.enabled = true;
                if (next != supported) Attempt(() => SetModeSupported(mode, next));
                if (play && Button("Play this mode", "Use " + summary.Name + " for Check map, Test and sharing", each))
                    Attempt(() => SelectMode(mode));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        void WhoPlays()
        {
            var format = WorkshopModes.FormatOf(PlayRequirements);
            var summary = WorkshopModes.Summary(PlayRequirements.ModeId, format);
            GUILayout.Label(summary.Name + " · " + summary.Players, ui.Wrapped, GUILayout.Width(RowWidth));
            if (summary.HasTeamFormat && SelectedModeEnabled)
            {
                GUILayout.Label("Players per team", rowLabel, GUILayout.Width(RowWidth));
                int current = Array.IndexOf(TeamFormats, format);
                int next = GUILayout.Toolbar(current, new[] { "Solo · 1", "Duo · 2", "Squad · 4" }, GUILayout.MinHeight(ui.RowHeight), GUILayout.Width(RowWidth));
                if (next >= 0 && next != current) Attempt(() => SelectMode(PlayRequirements.ModeId, TeamFormats[next]));
            }
            if (!SelectedModeEnabled) Hint("This map doesn't support the chosen mode. Add it above or play another mode.");
            else if (localPlaytest && !CanPlaytestLocally) Hint("Test alone works in Time Trial or Freeplay. Friends can play this mode online.");
        }
    }
}
