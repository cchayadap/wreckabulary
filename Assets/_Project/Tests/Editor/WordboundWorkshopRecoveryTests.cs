using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopRecoveryTests
    {
        string folder;
        GameObject owner;
        WordboundWorkshop workshop;
        WorkshopStorage storage;
        WorkshopTemplatePreset template;
        const string AuthoredSeed = "fedcba9876543210";

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-recovery-" + Guid.NewGuid().ToString("N"));
            storage = new WorkshopStorage(folder);
            template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            owner = new GameObject("Authored workshop recovery test");
            workshop = owner.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            string prefix = Path.Combine(Path.GetTempPath(), "wordbound-recovery-");
            if (folder != null && Path.GetFullPath(folder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        [Test]
        public void CleanAuthoredOpenSetsSeedWithoutAnUndoOrAutosaveEdit()
        {
            Open();
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(AuthoredSeed, workshop.History.Current.Seed);
            Assert.AreEqual(0, workshop.History.Revision);
            Assert.IsFalse(workshop.History.CanUndo);
            Invoke("OnDisable");
            Assert.IsFalse(storage.HasRecovery(template.Snapshot.Course().Id));
        }

        [Test]
        public void ExistingDraftBlocksAutosavePreviewTemplateAndShutdownUntilChoice()
        {
            var saved = SaveDraft("Existing edited course");
            byte[] before = File.ReadAllBytes(Primary);
            Open();
            Assert.IsTrue(workshop.RecoveryChoicePending);
            workshop.History.Select(new[] { workshop.History.Current.Course().Modules.First().Id });
            Set("autosaveAt", -1f);
            Invoke("Update");
            Invoke("OnDisable");
            Assert.Throws<InvalidOperationException>(() => workshop.Preview());
            Assert.Throws<InvalidOperationException>(() => workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Primary));
            Assert.AreEqual(saved.RecoveryJson, storage.Recover(saved.Course().Id, out _).RecoveryJson);
        }

        [Test]
        public void BackAndReopenLeaveBothRecoveryFilesUntouched()
        {
            SaveDraft("Older draft"); SaveDraft("Newest draft");
            byte[] primary = File.ReadAllBytes(Primary), backup = File.ReadAllBytes(Primary + ".bak");
            Open();
            int closed = 0; workshop.CloseRequested += () => closed++;
            Choose(3);
            Assert.AreEqual(1, closed); Assert.IsFalse(workshop.Visible);
            Invoke("OnDisable"); workshop.ResumeEditing();
            Assert.IsTrue(workshop.RecoveryChoicePending);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Primary));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Primary + ".bak"));
        }

        [Test]
        public void RecoverIsDeferredAndPreservesDraftSeedSelectionAndUndoBoundary()
        {
            var saved = SaveDraft("My unsaved design");
            Open();
            Invoke("QueueRecoveryChoice", 1);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual(AuthoredSeed, workshop.History.Current.Seed);
            Invoke("ProcessRecoveryChoice", true);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(saved.RecoveryJson, workshop.History.Current.RecoveryJson);
            Assert.IsFalse(workshop.History.CanUndo);
            Assert.AreSame(template.Catalogue, workshop.Catalogue);
            Assert.AreSame(template.Play, workshop.PlayRequirements);
        }

        [Test]
        public void SavedCourseChoiceArchivesExactPrimaryAndBackupBeforeAllowingSaves()
        {
            SaveDraft("Older draft"); SaveDraft("Latest draft");
            byte[] primary = File.ReadAllBytes(Primary), backup = File.ReadAllBytes(Primary + ".bak");
            Open(); Choose(2);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(template.Snapshot.CourseJsonText, workshop.History.Current.CourseJsonText);
            Assert.AreEqual(AuthoredSeed, workshop.History.Current.Seed);
            string archive = Directory.GetDirectories(Path.Combine(folder, "recovery-archives")).Single();
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Path.Combine(archive, Path.GetFileName(Primary))));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(Path.Combine(archive, Path.GetFileName(Primary) + ".bak")));
            workshop.History.Execute("New draft edit", draft => draft.Course.Title = "Replacement design");
            Invoke("OnDisable");
            Assert.AreEqual("Replacement design", storage.Recover(template.Snapshot.Course().Id, out _).Course().Title);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(Path.Combine(archive, Path.GetFileName(Primary))));
        }

        [Test]
        public void CorruptPrimaryRequiresChoiceAndRecoversTheValidBackup()
        {
            var expected = SaveDraft("Backup design"); SaveDraft("Interrupted design");
            File.WriteAllText(Primary, "interrupted write");
            Open(); Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual("interrupted write", File.ReadAllText(Primary));
            Choose(1);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(expected.RecoveryJson, workshop.History.Current.RecoveryJson);
            Assert.AreEqual("interrupted write", File.ReadAllText(Primary));
        }

        [Test]
        public void MismatchedCourseCannotBeRecoveredAndNewChoicePreservesItsBytes()
        {
            var other = template.Snapshot.Edit(); other.Course.Id = "unrelated-course";
            string unrelated = new WorkshopSnapshot(other).RecoveryJson;
            File.WriteAllText(Primary, unrelated);
            Assert.Throws<FormatException>(() => storage.Recover(template.Snapshot.Course().Id, out _));
            Open(); Choose(1);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual(unrelated, File.ReadAllText(Primary));
            Choose(2);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            string archive = Directory.GetDirectories(Path.Combine(folder, "recovery-archives")).Single();
            Assert.AreEqual(unrelated, File.ReadAllText(Path.Combine(archive, Path.GetFileName(Primary))));
        }

        [Test]
        public void FocusLossDiscardsQueuedChoiceAndLeavesRecoveryUnchanged()
        {
            var saved = SaveDraft("Do not overwrite"); Open();
            Invoke("QueueRecoveryChoice", 2);
            Invoke("ProcessRecoveryChoice", false);
            Invoke("ProcessRecoveryChoice", true);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual(saved.RecoveryJson, storage.Recover(saved.Course().Id, out _).RecoveryJson);
            Assert.IsFalse(Directory.Exists(Path.Combine(folder, "recovery-archives")));
        }

        [Test]
        public void FocusCallbackDiscardsChoiceEvenWithoutAnUnfocusedUpdate()
        {
            var saved = SaveDraft("Pending focus change"); Open();
            Invoke("QueueRecoveryChoice", 2);
            Invoke("OnApplicationFocus", false);
            Invoke("ProcessRecoveryChoice", true);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual(saved.RecoveryJson, storage.Recover(saved.Course().Id, out _).RecoveryJson);
            Assert.IsFalse(Directory.Exists(Path.Combine(folder, "recovery-archives")));
        }

        [Test]
        public void ChangedDraftRequiresAnotherConfirmationBeforeRecovery()
        {
            SaveDraft("Initially shown"); Open();
            var latest = SaveDraft("Saved while choice was open"); Choose(1);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Assert.AreEqual(AuthoredSeed, workshop.History.Current.Seed);
            Choose(1);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(latest.RecoveryJson, workshop.History.Current.RecoveryJson);
        }

        [Test]
        public void FailedArchiveKeepsTheChoicePendingAndOriginalDraftIntact()
        {
            var saved = SaveDraft("Still recoverable"); Open();
            File.WriteAllText(Path.Combine(folder, "recovery-archives"), "Path occupied by a file");
            Choose(2);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            Invoke("OnDisable");
            Assert.AreEqual(saved.RecoveryJson, storage.Recover(saved.Course().Id, out _).RecoveryJson);
            Choose(1);
            Assert.IsFalse(workshop.RecoveryChoicePending);
            Assert.AreEqual(saved.RecoveryJson, workshop.History.Current.RecoveryJson);
        }

        string Primary => Path.Combine(folder, template.Snapshot.Course().Id + ".autosave.json");
        void Open() => workshop.OpenAuthored(template.Snapshot.Course(), template.Catalogue, template.Play, AuthoredSeed, folder);
        WorkshopSnapshot SaveDraft(string title)
        {
            var draft = template.Snapshot.Edit(); draft.Course.Title = title;
            draft.Seed = "1234567890abcdef"; draft.GridMm = 100;
            draft.Selection.Add(draft.Course.Modules.First().Id);
            var saved = new WorkshopSnapshot(draft); storage.Save(saved); return saved;
        }
        void Choose(int decision) { Invoke("QueueRecoveryChoice", decision); Invoke("ProcessRecoveryChoice", true); }
        void Set(string name, object value) => typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workshop, value);
        void Invoke(string name, params object[] args)
        {
            try { typeof(WordboundWorkshop).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
