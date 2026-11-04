using System;
using System.IO;
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
        string recoveryCourseId, authoredLabel, recoveredLabel, recoveryFreshLabel = "";
        Vector2 recoveryScroll;
        GUIStyle recoveryText;
        bool recoveryUsesBackup, recoveryUnreadable;
        int recoveryDecision, recoverySelection;
        WorkshopStorage recoveryStorage;
        SourceReturn recoveryReturn;

        public void OpenAuthored(CourseDocument course, CourseCatalogue catalogue, CoursePlayRequirements play,
            string authoredSeed, string folder = null)
        {
            var initial = new WorkshopSnapshot(new WorkshopDraft {
                Course = course, Seed = authoredSeed, ActiveLayer = course.Layers.Count == 0 ? "" : course.Layers[0].Id
            });
            OpenSource(course, catalogue, play, folder, true);
            History = new WorkshopHistory(initial);
            ResetView(); Refresh(); savedRevision = History.Revision;
            assignedDraft = CaptureDraft();
            authoredOpening = initial; recoveredOpening = null; recoveryError = "";
            recoveryCourseId = course.Id; authoredLabel = course.Title + " · " + course.Id;
            recoveredLabel = ""; recoveryScroll = Vector2.zero;
            recoveryDecision = recoverySelection = 0; recoveryUsesBackup = false;
            recoveryUnreadable = false; recoveryStorage = storage; recoveryReturn = null; recoveryFreshLabel = "";
            if (!storage.HasRecovery(course.Id)) return;
            RecoveryChoicePending = true;
            SuspendWorkshopInput();
            if (view != null) view.Active = false;
            try { recoveredOpening = storage.Recover(course.Id, out recoveryUsesBackup); CacheRecoveredLabel(); }
            catch (Exception error) { recoveryError = error.Message; recoverySelection = 1; }
        }

        void BeginUnreadableChoice(WorkshopStorage source, string id, string error, string freshLabel, SourceReturn back, string label = null)
        {
            authoredOpening = History.Current; recoveredOpening = null; recoveryError = error ?? "";
            recoveryCourseId = id; authoredLabel = label ?? History.Current.Course().Title; recoveredLabel = "";
            recoveryScroll = Vector2.zero; recoveryDecision = 0; recoverySelection = 1; recoveryUsesBackup = false;
            recoveryUnreadable = true; recoveryStorage = source; recoveryFreshLabel = freshLabel; recoveryReturn = back;
            RecoveryChoicePending = true;
            SuspendWorkshopInput();
            if (view != null) view.Active = false;
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
            if (decision == 3 && recoveryUnreadable && recoveryReturn != null)
            {
                var back = recoveryReturn;
                RecoveryChoicePending = false; recoveryReturn = null; recoveryUnreadable = false; authoredOpening = null;
                SuspendWorkshopInput();
                flagshipDraftHash = back.FlagshipHash;
                ActivateDraft(back.Draft, back.Assigned, back.Template, back.Flagship, back.Parcel);
                ShowStatus("The unreadable save was left untouched.");
                return;
            }
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
                string kept = decision != 2 ? "" : recoveryUnreadable ? (recoveryStorage ?? storage).SetAside(recoveryCourseId) :
                    storage.ArchiveRecovery(recoveryCourseId);
                History = new WorkshopHistory(decision == 1 ? recoveredOpening : authoredOpening);
                ResetView(); Refresh(); savedRevision = History.Revision;
                RecoveryChoicePending = false; SuspendWorkshopInput();
                status = decision == 1 ? (recoveryUsesBackup ? "Recovered backup draft" : "Recovered draft") :
                    recoveryUnreadable ? "Opened " + History.Current.Course().Title + " · the unreadable save was kept as " + Path.GetFileName(kept) :
                    "Using saved course. Previous draft kept in " + kept;
                if (recoveryUnreadable && UsesParcelService) parcelServiceDraft = CaptureDraft();
                authoredOpening = recoveredOpening = null; recoveryUnreadable = false; recoveryReturn = null;
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
            float width = Mathf.Min(620, ScreenWidth - 32);
            GUILayout.BeginArea(new Rect((ScreenWidth - width) * .5f, 60, width, Mathf.Min(460, ScreenHeight - 80)), panel);
            recoveryScroll = GUILayout.BeginScrollView(recoveryScroll);
            if (recoveryText == null) recoveryText = new GUIStyle(GUI.skin.label) { wordWrap = true };
            if (recoveryUnreadable)
            {
                GUILayout.Label("This saved map can't be opened", heading);
                GUILayout.Label("The last save of “" + authoredLabel + "” is damaged and has no usable backup.", recoveryText);
                if (recoveryError.Length != 0) GUILayout.Label(recoveryError, recoveryText);
                GUILayout.Label("Starting fresh keeps the damaged file next to your saves, so nothing is deleted.", recoveryText);
                if (Button((recoverySelection == 1 ? "› " : "") + recoveryFreshLabel)) QueueRecoveryChoice(2);
                if (Button((recoverySelection == 2 ? "› " : "") + "Back")) QueueRecoveryChoice(3);
            }
            else
            {
                GUILayout.Label("Continue your course", heading);
                GUILayout.Label("A draft is already saved for this course. Choose before editing. Nothing is saved until you choose.", recoveryText);
                GUILayout.Label("Course: " + authoredLabel, recoveryText);
                if (recoveredOpening != null) GUILayout.Label(recoveredLabel, recoveryText);
                if (recoveryError.Length != 0) GUILayout.Label("Draft kept unchanged. " + recoveryError, recoveryText);
                GUILayout.Label("Recover uses this course's current rooms and objects. Check the recovered map before playing.", recoveryText);
                GUI.enabled = recoveredOpening != null;
                if (Button((recoverySelection == 0 ? "› " : "") + "Recover draft")) QueueRecoveryChoice(1);
                GUI.enabled = true;
                if (Button((recoverySelection == 1 ? "› " : "") + "Use saved course")) QueueRecoveryChoice(2);
                GUILayout.Label("Use saved course keeps a separate copy of the old draft before starting again.", recoveryText);
                if (Button((recoverySelection == 2 ? "› " : "") + "Back")) QueueRecoveryChoice(3);
            }
            GUILayout.Label("Controller: D-pad chooses · A confirms · B goes back", recoveryText);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
