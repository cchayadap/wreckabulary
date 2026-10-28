using System;
using System.IO;
using UnityEngine;
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

        DraftSession assignedDraft, flagshipDraft, parcelServiceDraft;
        WorkshopSnapshot parcelServiceSource;
        CourseCatalogue parcelServiceCatalogue;
        CoursePlayRequirements parcelServicePlay;
        string parcelServiceIdentity, parcelServiceUnavailable;
        public bool HasParcelService => parcelServiceSource != null || parcelServiceUnavailable != null;
        public bool UsesParcelService { get; private set; }
        bool usesFlagship;
        string flagshipDraftHash;
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
            if (UsesParcelService) parcelServiceDraft = CaptureDraft();
            else if (UsesAssignedCourse) assignedDraft = CaptureDraft();
            else if (usesFlagship) flagshipDraft = CaptureDraft();
            else if (currentTemplate.HasValue) drafts[currentTemplate.Value] = CaptureDraft();
        }

        void ActivateDraft(DraftSession draft, bool assigned, WorkshopTemplate? template, bool flagship = false, bool parcelService = false)
        {
            History = draft.History; Catalogue = draft.Catalogue; PlayRequirements = draft.Play;
            storage = draft.Storage; storageFolder = draft.Folder;
            UsesAssignedCourse = assigned; UsesParcelService = parcelService; currentTemplate = template; usesFlagship = flagship;
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

        public void ConfigureParcelService(CourseDocument document, CourseCatalogue catalogue, CoursePlayRequirements play, string seed)
        {
            if (document == null || document.Id != WordboundSoloEntry.ParcelServiceCourseId || catalogue == null ||
                play == null || !WordboundSoloEntry.Supports(play.ModeId) || !WorkshopModes.AllowsOneHumanLocal(play))
                throw new ArgumentException("Parcel Service needs its saved local course and Time Run or Freestyle requirements.");
            var snapshot = new WorkshopSnapshot(new WorkshopDraft { Course = document, Seed = seed });
            string identity = WordboundIdentity.Sha256(snapshot.RecoveryJson + "|" + catalogue.Hash + "|" + play.ModeId);
            if (parcelServiceDraft != null && parcelServiceIdentity != identity)
                throw new InvalidOperationException("Reopen the workshop to load a different saved Parcel Service source; its current draft is retained.");
            parcelServiceSource = snapshot; parcelServiceCatalogue = catalogue; parcelServicePlay = play;
            parcelServiceIdentity = identity; parcelServiceUnavailable = null;
        }

        public void ConfigureParcelServiceUnavailable(string reason) => parcelServiceUnavailable =
            string.IsNullOrEmpty(reason) ? "The saved Parcel Service course is unavailable." : reason;

        public void OpenParcelService(string folder = null)
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before changing courses.");
            if (!HasParcelService) throw new InvalidOperationException("No saved Parcel Service course is assigned.");
            if (parcelServiceUnavailable != null) throw new InvalidOperationException(parcelServiceUnavailable);
            if (UsesParcelService && (folder == null || folder == storageFolder)) return;
            SaveAndRememberDraft();
            if (parcelServiceDraft != null && (folder == null || folder == parcelServiceDraft.Folder))
            {
                ActivateDraft(parcelServiceDraft, false, null, parcelService: true);
                ShowStatus("Parcel Service · your edits and Undo history are retained."); return;
            }
            string baseFolder = folder ?? storageFolder ?? Path.Combine(Application.persistentDataPath, "wordbound-workshop");
            var scoped = new WorkshopStorage(Path.Combine(baseFolder, "local-courses", "ParcelServiceV1"));
            var snapshot = parcelServiceSource;
            bool recovered = scoped.HasRecovery(WordboundSoloEntry.ParcelServiceCourseId), backup = false;
            if (recovered) snapshot = scoped.Recover(WordboundSoloEntry.ParcelServiceCourseId, out backup);
            if (snapshot.Course().Id != WordboundSoloEntry.ParcelServiceCourseId)
                throw new InvalidOperationException("The Parcel Service draft has a different course identity.");
            ActivateDraft(new DraftSession { History = new WorkshopHistory(snapshot), Catalogue = parcelServiceCatalogue,
                Play = parcelServicePlay, Storage = scoped, Folder = baseFolder }, false, null, parcelService: true);
            parcelServiceDraft = CaptureDraft();
            ShowStatus(recovered ? backup ? "Recovered previous Parcel Service save" : "Recovered Parcel Service autosave" :
                "Parcel Service · saved course copy · Workshop edits are stored separately.");
        }

        void QueueSourceChoice(int choice)
        {
            if (RecoveryChoicePending || OnlineStarting || sourceDecision != 0 || choice < 1 || choice > 6) return;
            if (choice == 1 && !HasAssignedCourse) return;
            if (choice == 5 && flagshipContent == null) return;
            if (choice == 6 && !HasParcelService) return;
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
                else if (decision == 5) OpenFlagship(flagshipContent);
                else if (decision == 6) OpenParcelService();
                else OpenTemplate((WorkshopTemplate)(decision - 2));
            });
        }

    }
}
