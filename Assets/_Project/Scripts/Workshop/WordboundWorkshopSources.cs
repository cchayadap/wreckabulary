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

        sealed class SourceReturn
        {
            public DraftSession Draft;
            public bool Assigned, Flagship, Parcel;
            public WorkshopTemplate? Template;
            public string FlagshipHash;
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

        SourceReturn CurrentSource() => History == null ? null : new SourceReturn {
            Draft = CaptureDraft(), Assigned = UsesAssignedCourse, Flagship = usesFlagship, Parcel = UsesParcelService,
            Template = currentTemplate, FlagshipHash = flagshipDraftHash
        };

        void SaveAndRememberDraft()
        {
            CancelGesture();
            if (History == null) return;
            if (storage != null && History.Revision != savedRevision)
            { storage.Save(History.Current); savedRevision = History.Revision; saveError = ""; }
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
            Visible = true; LastPlaytestDraft = null; saveError = "";
            ResetView(); Refresh(); savedRevision = History.Revision;
        }

        void OpenInSession(WorkshopSnapshot snapshot)
        {
            var keep = step;
            History = new WorkshopHistory(snapshot);
            LastPlaytestDraft = null; saveError = "";
            ResetView(); step = keep; Refresh(); savedRevision = History.Revision;
            RememberLast();
        }

        static bool Unreadable(Exception error) => error is IOException || error is FormatException || error is ArgumentException;

        static string LastMapId(WorkshopStorage scoped, string defaultId)
        {
            string last = scoped.LastMap();
            return last == null || last == defaultId || !scoped.HasRecovery(last) ? null : last;
        }

        bool ReopenLastMap(DraftSession session, WorkshopStorage scoped, string last, WorkshopTemplate? template, bool flagship,
            string label, out string problem)
        {
            problem = null;
            if (last == null) return false;
            try
            {
                session.History = new WorkshopHistory(scoped.Recover(last, out bool backup));
                ActivateDraft(session, false, template, flagship); ApplyRememberedMode();
                ShowStatus(label + " · reopened " + display.Title + (backup ? " from its previous save, because the last save was damaged" : ""));
                return true;
            }
            catch (Exception error) when (Unreadable(error)) { problem = error.Message; return false; }
        }

        void AskAboutDamagedLastMap(WorkshopStorage scoped, string last, string problem, SourceReturn back)
        {
            if (problem == null || RecoveryChoicePending) return;
            BeginUnreadableChoice(scoped, last, problem, "Keep it aside and open " + display.Title, back, last);
        }

        void OpenRecovered(DraftSession session, WorkshopStorage source, string id, SourceReturn back, WorkshopTemplate? template,
            bool flagship, bool parcel, string fresh, string recovered, string previous, string freshChoice)
        {
            string message = fresh, problem = null;
            if (source != null)
                try
                {
                    session.History = new WorkshopHistory(source.Recover(id, out bool backup));
                    message = backup ? previous : recovered;
                }
                catch (Exception error) when (Unreadable(error)) { problem = error.Message; }
            ActivateDraft(session, false, template, flagship, parcel);
            if (problem != null) { BeginUnreadableChoice(source, id, problem, freshChoice, back); return; }
            if (parcel) parcelServiceDraft = CaptureDraft();
            ApplyRememberedMode();
            ShowStatus(message);
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
            var back = CurrentSource();
            string baseFolder = folder ?? storageFolder ?? DefaultFolder;
            var scoped = new WorkshopStorage(Path.Combine(baseFolder, "local-courses", "ParcelServiceV1"));
            if (parcelServiceSource.Course().Id != WordboundSoloEntry.ParcelServiceCourseId)
                throw new InvalidOperationException("The Parcel Service draft has a different course identity.");
            var session = new DraftSession { History = new WorkshopHistory(parcelServiceSource), Catalogue = parcelServiceCatalogue,
                Play = parcelServicePlay, Storage = scoped, Folder = baseFolder };
            string id = WordboundSoloEntry.ParcelServiceCourseId;
            OpenRecovered(session, scoped.HasRecovery(id) ? scoped : null, id, back, null, false, true,
                "Parcel Service · saved course copy · Workshop edits are stored separately.",
                "Recovered Parcel Service autosave", "Recovered previous Parcel Service save", "Start fresh from the saved course");
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
