using System;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        sealed class DraftSession
        {
            public WorkshopHistory History;
            public CourseCatalogue Catalogue;
            public CoursePlayRequirements Play;
            public WorkshopStorage Storage;
            public string Folder;
        }

        DraftSession assignedDraft;
        int sourceDecision;
        bool controllerSourceDecision;
        public bool HasAssignedCourse => assignedDraft != null;
        public bool UsesAssignedCourse { get; private set; }

        DraftSession CaptureDraft() => new DraftSession {
            History = History, Catalogue = Catalogue, Play = PlayRequirements, Storage = storage, Folder = storageFolder
        };

        void SaveAndRememberDraft()
        {
            CancelGesture();
            if (History == null) return;
            if (storage != null && History.Revision != savedRevision)
            { storage.Save(History.Current); savedRevision = History.Revision; }
            if (UsesAssignedCourse) assignedDraft = CaptureDraft();
            else if (currentTemplate.HasValue) drafts[currentTemplate.Value] = CaptureDraft();
        }

        void ActivateDraft(DraftSession draft, bool assigned, WorkshopTemplate? template)
        {
            History = draft.History; Catalogue = draft.Catalogue; PlayRequirements = draft.Play;
            storage = draft.Storage; storageFolder = draft.Folder;
            UsesAssignedCourse = assigned; currentTemplate = template;
            Visible = true; LastPlaytestDraft = null;
            ResetView(); Refresh(); savedRevision = History.Revision;
        }

        public void OpenAssignedCourse()
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing courses.");
            if (!HasAssignedCourse) throw new InvalidOperationException("No course is assigned to this workshop.");
            if (UsesAssignedCourse) return;
            SaveAndRememberDraft();
            ActivateDraft(assignedDraft, true, null);
            ShowStatus("Assigned course · your edits and Undo history are retained.");
        }

        void QueueSourceChoice(int choice)
        {
            if (RecoveryChoicePending || OnlineStarting || sourceDecision != 0 || choice < 1 || choice > 4) return;
            if (choice == 1 && !HasAssignedCourse) return;
            sourceDecision = choice;
        }

        void QueueControllerSourceChoice()
        {
            if (sourceDecision != 0) return;
            QueueSourceChoice(1);
            controllerSourceDecision = sourceDecision != 0;
        }

        void ProcessSourceChoice(bool focused)
        {
            int decision = sourceDecision; sourceDecision = 0;
            bool controller = controllerSourceDecision; controllerSourceDecision = false;
            if (decision == 0 || !focused || !Visible || DesktopBinding.Typing || KeyBindings.GameplayBlocked ||
                controller && workshopTyping) return;
            Attempt(() => {
                if (decision == 1) OpenAssignedCourse();
                else OpenTemplate((WorkshopTemplate)(decision - 2));
            });
        }

    }
}
