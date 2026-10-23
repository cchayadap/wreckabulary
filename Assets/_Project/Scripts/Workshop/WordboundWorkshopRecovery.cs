using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        public bool RecoveryChoicePending { get; private set; }
        WorkshopSnapshot authoredOpening, recoveredOpening;
        string recoveryError = "";
        string recoveryCourseId, authoredLabel, recoveredLabel;
        Vector2 recoveryScroll;
        GUIStyle recoveryText;
        bool recoveryUsesBackup;
        int recoveryDecision, recoverySelection;

        public void OpenAuthored(CourseDocument course, CourseCatalogue catalogue, CoursePlayRequirements play,
            string authoredSeed, string folder = null)
        {
            var initial = new WorkshopSnapshot(new WorkshopDraft {
                Course = course, Seed = authoredSeed, ActiveLayer = course.Layers.Count == 0 ? "" : course.Layers[0].Id
            });
            Open(course, catalogue, play, folder);
            History = new WorkshopHistory(initial);
            ResetView(); Refresh(); savedRevision = History.Revision;
            authoredOpening = initial; recoveredOpening = null; recoveryError = "";
            recoveryCourseId = course.Id; authoredLabel = course.Title + " · " + course.Id;
            recoveredLabel = ""; recoveryScroll = Vector2.zero;
            recoveryDecision = recoverySelection = 0; recoveryUsesBackup = false;
            if (!storage.HasRecovery(course.Id)) return;
            RecoveryChoicePending = true;
            SuspendWorkshopInput();
            if (view != null) view.Active = false;
            try { recoveredOpening = storage.Recover(course.Id, out recoveryUsesBackup); CacheRecoveredLabel(); }
            catch (Exception error) { recoveryError = error.Message; recoverySelection = 1; }
        }

        void RequireRecoveryChoice()
        {
            if (RecoveryChoicePending) throw new InvalidOperationException("Choose a saved draft or the saved course before editing.");
        }

        void QueueRecoveryChoice(int decision)
        {
            if (!RecoveryChoicePending || recoveryDecision != 0 || decision < 1 || decision > 3 ||
                decision == 1 && recoveredOpening == null) return;
            recoveryDecision = decision;
        }

        void ProcessRecoveryChoice(bool focused)
        {
            if (view != null) view.Active = false;
            if (!focused || !Visible)
            { recoveryDecision = 0; SuspendWorkshopInput(); return; }
            RecoveryControllerInput(focused);
            int decision = recoveryDecision; recoveryDecision = 0;
            if (decision == 0) return;
            if (decision == 3)
            {
                SuspendWorkshopInput(); Visible = false; CloseRequested?.Invoke(); return;
            }
            try
            {
                if (decision == 1 && recoveredOpening == null) return;
                if (decision == 1)
                {
                    var latest = storage.Recover(recoveryCourseId, out bool backup);
                    if (latest.RecoveryJson != recoveredOpening.RecoveryJson || backup != recoveryUsesBackup)
                    {
                        recoveredOpening = latest; recoveryUsesBackup = backup;
                        CacheRecoveredLabel();
                        recoveryError = "The draft changed while this choice was open. Review it and choose again.";
                        return;
                    }
                }
                string archive = decision == 2 ? storage.ArchiveRecovery(recoveryCourseId) : "";
                History = new WorkshopHistory(decision == 1 ? recoveredOpening : authoredOpening);
                ResetView(); Refresh(); savedRevision = History.Revision;
                RecoveryChoicePending = false; SuspendWorkshopInput();
                status = decision == 1 ? (recoveryUsesBackup ? "Recovered backup draft" : "Recovered draft") :
                    "Using saved course. Previous draft kept in " + archive;
                authoredOpening = recoveredOpening = null;
            }
            catch (Exception error) { recoveryError = "Could not open this choice: " + error.Message; }
        }

        void CacheRecoveredLabel() => recoveredLabel = (recoveryUsesBackup ? "Backup draft: " : "Draft: ") +
            recoveredOpening.Course().Title + " · seed " + recoveredOpening.Seed;

        void RecoveryControllerInput(bool focused)
        {
            var pad = Gamepad.current;
            if (!workshopPadGate.CanRead(pad, focused && Visible, DesktopBinding.Typing || KeyBindings.GameplayBlocked)) return;
            int first = recoveredOpening == null ? 1 : 0;
            if (pad.dpad.down.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame)
                recoverySelection = recoverySelection == 2 ? first : recoverySelection + 1;
            if (pad.dpad.up.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame)
                recoverySelection = recoverySelection == first ? 2 : recoverySelection - 1;
            if (pad.buttonSouth.wasPressedThisFrame) QueueRecoveryChoice(recoverySelection + 1);
            if (pad.buttonEast.wasPressedThisFrame) QueueRecoveryChoice(3);
        }

        void RecoveryPanel()
        {
            float width = Mathf.Min(620, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) * .5f, 60, width, Mathf.Min(440, Screen.height - 80)), panel);
            recoveryScroll = GUILayout.BeginScrollView(recoveryScroll);
            GUILayout.Label("Continue your course", heading);
            if (recoveryText == null) recoveryText = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label("A draft is already saved for this course ID. Choose before editing. Nothing is saved until you choose.", recoveryText);
            GUILayout.Label("Course: " + authoredLabel, recoveryText);
            if (recoveredOpening != null) GUILayout.Label(recoveredLabel, recoveryText);
            if (recoveryError.Length != 0) GUILayout.Label("Draft kept unchanged. " + recoveryError, recoveryText);
            GUILayout.Label("Recover uses this course's current catalogue and presentation. Validate the recovered draft before playing.", recoveryText);
            GUI.enabled = recoveredOpening != null;
            if (Button((recoverySelection == 0 ? "› " : "") + "Recover draft")) QueueRecoveryChoice(1);
            GUI.enabled = true;
            if (Button((recoverySelection == 1 ? "› " : "") + "Use saved course")) QueueRecoveryChoice(2);
            GUILayout.Label("Use saved course keeps a separate copy of the old draft before starting again.", recoveryText);
            if (Button((recoverySelection == 2 ? "› " : "") + "Back")) QueueRecoveryChoice(3);
            GUILayout.Label("Controller: D-pad chooses · A confirms · B goes back", recoveryText);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
