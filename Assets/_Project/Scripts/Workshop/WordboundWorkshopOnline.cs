using System;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        public event Action<WorkshopSnapshot, ResolvedCourse, CoursePlayRequirements> OnlinePlaytestRequested;
        public Func<bool> CanStartOnline { get; set; }
        public bool OnlineStarting { get; private set; }

        public void RequestOnlinePlaytest()
        {
            if (OnlineStarting) return;
            if (OnlinePlaytestRequested == null || CanStartOnline?.Invoke() != true)
            {
                ShowStatus("To play online with friends, connect as a guest and leave your current party first. Exit the workshop to return to the party screen.");
                return;
            }
            if (!SelectedModeEnabled)
            {
                ShowStatus("Choose a mode this map supports in Rules before playing online.");
                return;
            }
            var preview = Preview();
            if (!preview.Success) return;
            string physicalIssue = WorkshopPhysicalSites.PlaytestIssue(preview.Manifest);
            if (physicalIssue.Length != 0) { ShowStatus(physicalIssue); return; }
            OnlineStarting = true;
            ShowStatus("Preparing your private party and sharing this exact map…");
            try { OnlinePlaytestRequested.Invoke(History.Current, preview.Manifest, PlayRequirements); }
            catch (Exception error) { FinishOnlinePreparation(error.Message); }
        }

        public void FinishOnlinePreparation(string message = null)
        {
            OnlineStarting = false;
            if (message != null) ShowStatus(message);
        }

        void OnlinePreparationPanel()
        {
            float width = Mathf.Min(560, ScreenWidth - 32);
            GUILayout.BeginArea(new Rect((ScreenWidth - width) * .5f, 80, width, 150), panel);
            GUILayout.Label("Preparing online play", heading);
            GUILayout.Label(status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndArea();
        }
    }
}
