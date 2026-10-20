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
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "My world");
            string before = workshop.History.Current.RecoveryJson;
            ResolvedCourse dispatched = null;
            workshop.PlaytestRequested += manifest => dispatched = manifest;
            workshop.RequestPlaytest();
            Assert.IsNotNull(dispatched);
            Assert.AreSame(workshop.LastPreview.Manifest, dispatched);
            Assert.AreEqual(before, workshop.LastPlaytestDraft.RecoveryJson);
            workshop.Visible = false;
            workshop.ResumeEditing();
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);

            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            Assert.AreEqual(before, workshop.History.Current.RecoveryJson);
            Assert.IsTrue(workshop.History.Undo());
            Assert.AreNotEqual("My world", workshop.History.Current.Course().Title);
        }
        [Test]
        public void PlaytestCapturesExactDraftBeforeLauncherChangesEditingState()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.History.Execute("Seed", draft => draft.Seed = "1234567890abcdef");
            var expected = workshop.History.Current;
            string expectedManifest = CourseResolver.Resolve(expected.Course(), workshop.Catalogue,
                new CourseResolveRequest(expected.Seed, workshop.PlayRequirements)).Manifest.Hash;
            ResolvedCourse dispatched = null;
            workshop.PlaytestRequested += manifest =>
            {
                dispatched = manifest;
                workshop.History.Execute("Later edit", draft => draft.Course.Title = "After dispatch");
            };
            workshop.RequestPlaytest();
            Assert.AreSame(expected, workshop.LastPlaytestDraft);
            Assert.AreEqual(expectedManifest, dispatched.Hash);
            Assert.AreEqual("1234567890abcdef", workshop.LastPlaytestDraft.Seed);
            Assert.AreNotEqual(workshop.History.Current.CourseJsonText, workshop.LastPlaytestDraft.CourseJsonText);
        }
        [Test]
        public void InvalidDraftCannotDispatchAnEarlierSuccessfulPlaytest()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            int dispatches = 0;
            workshop.PlaytestRequested += _ => dispatches++;
            workshop.RequestPlaytest();
            Assert.AreEqual(1, dispatches);
            Assert.IsNotNull(workshop.LastPlaytestDraft);
            workshop.History.Execute("Incomplete draft", draft => draft.Course.Spawns.Clear());
            workshop.RequestPlaytest();
            Assert.AreEqual(1, dispatches);
            Assert.IsNull(workshop.LastPlaytestDraft);
            Assert.IsFalse(workshop.LastPreview.Success);
        }
        [TestCase(800, 600, 0, 0)]
        [TestCase(1280, 720, 55, -30)]
        [TestCase(640, 960, -80, 35)]
        public void ViewportProjectsAndPicksTheActiveFloorWithoutChangingCourseData(int width, int height, int orbitX, int orbitY)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            using (var view = new WordboundWorkshopViewport(owner.transform))
            {
                view.Sync(preset.Snapshot, preset.Catalogue); view.Frame(); view.Resize(width, height);
                view.Orbit(new Vector2(orbitX, orbitY));
                var module = preset.Snapshot.Course().Modules.Find(item => item.Id == "Start");
                var rect = new Rect(0, 0, width, height);
                var screen = view.Project(module.Position, rect);
                Assert.IsTrue(view.Surface(screen, rect, module.Position.Y, out var point));
                Assert.LessOrEqual(Math.Abs(point.X - module.Position.X), 5);
                Assert.LessOrEqual(Math.Abs(point.Z - module.Position.Z), 5);
                Assert.AreEqual("Start", view.Pick(screen, rect));
                var fixedZone = preset.Snapshot.Course().Zones.Find(item => item.Placement == "Fixed");
                var asset = preset.Catalogue.Assets[fixedZone.Pool[0].AssetId];
                var centre = new CoursePoint(fixedZone.FixedPosition.X, fixedZone.FixedPosition.Y + asset.HeightMm / 2, fixedZone.FixedPosition.Z);
                Assert.AreEqual(fixedZone.Id, view.PickFixed(view.Project(centre, rect), rect));
                var history = new WorkshopHistory(preset.Snapshot); history.Select(new[] { "Start" });
                var drag = new WorkshopTransformDrag(history, WorkshopHandle.Plane, module.Position);
                drag.Move(1500, 500);
                string before = history.Current.RecoveryJson;
                view.TransformPreview(drag, module.Position);
                Assert.AreEqual(before, history.Current.RecoveryJson);
            }
        }
        [Test]
        public void ViewportDisposalReleasesOnlyItsOwnedCameraAndRenderTarget()
        {
            var other = new GameObject("Unrelated camera").AddComponent<Camera>();
            try
            {
                var view = new WordboundWorkshopViewport(owner.transform);
                var image = view.Texture;
                view.Dispose();
                Assert.IsTrue(other);
                Assert.IsTrue(image == null);
                Assert.AreEqual(0, owner.GetComponentsInChildren<Camera>(true).Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(other.gameObject); }
        }
    }
}
