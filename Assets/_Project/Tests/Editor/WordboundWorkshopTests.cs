using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopTests
    {
        string folder;
        GameObject owner;
        WordboundWorkshop workshop;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-workshop-" + Guid.NewGuid().ToString("N"));
            owner = new GameObject("Wordbound workshop test");
            workshop = owner.AddComponent<WordboundWorkshop>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void ReopeningTemplateRestoresItsAutosaveBeforeNewEdits()
        {
            var template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var draft = template.Snapshot.Edit();
            draft.Course.Title = "Recovered authoring";
            draft.Seed = "1234567890abcdef";
            new WorkshopStorage(folder).Save(new WorkshopSnapshot(draft));

            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual("Recovered authoring", workshop.History.Current.Course().Title);
            Assert.AreEqual("1234567890abcdef", workshop.History.Current.Seed);
            Assert.IsFalse(workshop.History.CanUndo);
        }

        [Test]
        public void SuppliedDocumentDoesNotSilentlyLoadOrOverwriteAnEarlierSave()
        {
            var template = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var store = new WorkshopStorage(folder);
            store.Save(template.Snapshot);
            var course = template.Snapshot.Course();
            course.Title = "Explicit import";
            workshop.Open(course, template.Catalogue, template.Play, folder);
            Assert.AreEqual("Explicit import", workshop.History.Current.Course().Title);
            Assert.AreEqual(template.Snapshot.Course().Title, store.Recover(course.Id, out _).Course().Title);
        }

        [Test]
        public void PlaytestAndTemplateRoundTripPreserveTheEditingHistory()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "My race");
            string before = workshop.History.Current.RecoveryJson;
            ResolvedCourse dispatched = null;
            workshop.PlaytestRequested += manifest => dispatched = manifest;
            workshop.RequestPlaytest();
            Assert.IsNotNull(dispatched);
            Assert.AreSame(workshop.LastPreview.Manifest, dispatched);
            workshop.Visible = false;
            workshop.ResumeEditing();
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);

            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            Assert.IsTrue(workshop.History.Undo());
            Assert.AreNotEqual("My race", workshop.History.Current.Course().Title);
        }
    }
}
