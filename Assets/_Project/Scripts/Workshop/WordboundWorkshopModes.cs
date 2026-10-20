using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        WordboundFlagshipContent flagshipContent;
        public void ConfigureFlagship(WordboundFlagshipContent content) => flagshipContent = content;
        public void OpenFlagship(WordboundFlagshipContent content, string folder = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            ConfigureFlagship(content);
            Open(content.CopyDocument(), content.Geometry, MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")), folder);
            ShowStatus("Flagship draft · TimeRun · preview validates this exact seed");
        }
        bool localPlaytest;
        public bool CanPlaytestLocally => History != null && SelectedModeEnabled &&
            WorkshopModes.AllowsOneHumanLocal(PlayRequirements);
        public bool LocalPlaytestOnly => localPlaytest;
        bool SelectedModeEnabled => History != null && PlayRequirements != null &&
            (display != null && displayRevision == History.Revision ? display : History.Current.Course())
                .SupportedModes.Contains(PlayRequirements.ModeId);

        public void ConfigureLocalPlaytest(bool enabled)
        {
            localPlaytest = enabled;
            if (enabled && History != null && !CanPlaytestLocally)
                ShowStatus("Choose TimeRun or Freestyle for a one-player local playtest.");
        }

        public void SelectMode(string modeId, RaceFormat format = RaceFormat.Solo)
        {
            if (History == null) throw new InvalidOperationException("Open a map before selecting a mode.");
            var choice = WorkshopModes.Select(History.Current.Course(), modeId, format);
            CancelGesture(); PlayRequirements = choice.Requirements;
            LastPreview = null; LastPlaytestDraft = null; displayRevision = -1; Refresh();
            ShowStatus(choice.Label + (localPlaytest && !choice.AllowsOneHumanLocal
                ? " · preview available; local playtest requires TimeRun or Freestyle."
                : " selected · preview uses this exact mode and seed."));
        }

        public void EnableFreestyleSandbox()
        {
            if (History == null) throw new InvalidOperationException("Open a map first.");
            CancelGesture();
            History.Execute("Enable Freestyle sandbox", WorkshopModes.EnableFreestyleSandbox);
            SelectMode("Freestyle");
            ShowStatus("Freestyle sandbox enabled and selected · undo is available.");
        }

        public void ShowStatus(string message)
        {
            status = message ?? "";
            if (status.Length > 512) status = status.Substring(0, 512);
        }

        void ModePanel()
        {
            GUILayout.Label("Preview / playtest mode", heading);
            var choices = WorkshopModes.Choices(display);
            foreach (var group in choices.GroupBy(c => c.ModeId))
            {
                var choice = group.First();
                bool selected = PlayRequirements.ModeId == choice.ModeId;
                if (Button((selected ? "● " : "") + choice.Label.Split('·')[0].Trim()) && !selected)
                    Attempt(() => SelectMode(choice.ModeId, choice.Format));
                if (selected && group.Count() > 1)
                {
                    var formats = group.ToArray();
                    int current = Array.FindIndex(formats, item => item.Requirements.TeamCapacity == PlayRequirements.TeamCapacity);
                    int next = GUILayout.Toolbar(current, formats.Select(item => item.Format.ToString()).ToArray());
                    if (next >= 0 && next != current) Attempt(() => SelectMode(formats[next].ModeId, formats[next].Format));
                }
            }
            if (!SelectedModeEnabled) GUILayout.Label("This map no longer enables the selected mode. Choose one above.");
            if (localPlaytest && !CanPlaytestLocally)
                GUILayout.Label("Local playtest: TimeRun or Freestyle, one player.");
            if (!display.SupportedModes.Contains("Freestyle") && Button("Enable Freestyle sandbox",
                "Add Freestyle to this map's supported modes as an undoable edit, then select it."))
                Attempt(EnableFreestyleSandbox);
            GUILayout.Label("The mode is resolved before preview. Player and team limits are kept in that exact manifest.");
        }
    }
}
