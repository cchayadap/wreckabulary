using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopPlacementTests
    {
        [Test]
        public void SettingsAndRedrawPreserveAuthoredRotationGridAndPurpose()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            history.Execute("Authored settings", draft =>
            {
                var zone = draft.Course.Zones.Single(item => item.Id == "FixedStool");
                zone.GridMm = 250; zone.Purpose = "Decoration";
            });
            var before = history.Current;
            var original = before.Course().Zones.Single(item => item.Id == "FixedStool");
            var settings = new WordboundWorkshopPlacement { Mode = 0, SpacingMm = 750, Pool = WordboundWorkshopPlacement.ParsePool("STOOL") };
            var destination = new CoursePoint(-5000, 0, 3500);
            history.Execute("Reposition", draft => settings.Apply(draft, preset.Catalogue, original.ModuleId, original.Id, position: destination));
            var actual = history.Current.Course().Zones.Single(item => item.Id == original.Id);
            Assert.AreEqual(destination, actual.FixedPosition);
            Assert.AreEqual(90, actual.FixedYaw); CollectionAssert.AreEqual(new[] { 90 }, actual.Yaws);
            Assert.AreEqual(250, actual.GridMm); Assert.AreEqual("Decoration", actual.Purpose);
            Assert.AreEqual(original.Bounds, actual.Bounds);
            Assert.AreEqual(750, actual.SpacingMm);
            Assert.IsTrue(history.Undo()); Assert.AreSame(before, history.Current);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(destination, history.Current.Course().Zones.Single(item => item.Id == original.Id).FixedPosition);
        }

        [Test]
        public void RandomRangeAndWeightsApplyInOneTransactionWithoutChangingOtherZones()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            var original = history.Current.Course().Zones.Single(item => item.Id == "GardenProps");
            var fixedBefore = history.Current.Course().Zones.Single(item => item.Id == "FixedStool");
            var settings = new WordboundWorkshopPlacement
            {
                Mode = 2, MinCount = 0, MaxCount = 4, ChancePermille = 375, SpacingMm = 1000,
                Pool = WordboundWorkshopPlacement.ParsePool("STOOL:3, BED:1")
            };
            var bounds = new CourseBox(new CoursePoint(15000, 0, -6000), new CoursePoint(26000, 0, 6000));
            history.Execute("Apply range", draft => settings.Apply(draft, preset.Catalogue, original.ModuleId, original.Id, bounds));
            settings.Pool[0].Weight = 99;
            var actual = history.Current.Course().Zones.Single(item => item.Id == original.Id);
            Assert.AreEqual(bounds, actual.Bounds); Assert.AreEqual(375, actual.ChancePermille);
            Assert.AreEqual(3, actual.Pool.Single(item => item.AssetId == "STOOL").Weight);
            Assert.AreEqual(1, actual.Pool.Single(item => item.AssetId == "BED").Weight);
            CollectionAssert.AreEqual(original.Yaws, actual.Yaws);
            Assert.AreEqual(fixedBefore.FixedPosition, history.Current.Course().Zones.Single(item => item.Id == "FixedStool").FixedPosition);
            Assert.IsTrue(history.Undo()); Assert.IsFalse(history.CanUndo);
            Assert.AreEqual(preset.Snapshot.RecoveryJson, history.Current.RecoveryJson);
        }

        [TestCase("range")]
        [TestCase("count")]
        [TestCase("weight")]
        [TestCase("locked")]
        [TestCase("owner")]
        public void InvalidPlacementKeepsDraftAndRedo(string invalid)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            history.Execute("Rename", draft => draft.Course.Title = "Saved redo");
            history.Undo();
            string before = history.Current.RecoveryJson;
            int revision = history.Revision;
            var settings = new WordboundWorkshopPlacement { Mode = 1, Pool = WordboundWorkshopPlacement.ParsePool("BED") };
            CourseBox? bounds = null;
            if (invalid == "range") bounds = new CourseBox(new CoursePoint(20, 0, 0), new CoursePoint(10, 0, 0));
            if (invalid == "count") settings.MinCount = 5;
            if (invalid == "weight") settings.Pool[0].Weight = 0;
            Assert.Catch(() => history.Execute("Invalid apply", draft =>
            {
                if (invalid == "locked") draft.LockedLayers.Add("Ground");
                settings.Apply(draft, preset.Catalogue, invalid == "owner" ? "Start" : "Garden", "GardenProps", bounds);
            }));
            Assert.AreEqual(before, history.Current.RecoveryJson); Assert.AreEqual(revision, history.Revision);
            Assert.IsTrue(history.CanRedo);
            history.Redo(); Assert.AreEqual("Saved redo", history.Current.Course().Title);
        }

        [TestCase("")]
        [TestCase("BAT:0")]
        [TestCase("BAT:-1")]
        [TestCase("BAT:10001")]
        [TestCase("BAT:1.5")]
        [TestCase("BAT:1, BAT:2")]
        [TestCase("BAT:1:2")]
        public void InvalidWeightsHaveNoSilentFallback(string text)
        {
            Assert.Throws<ArgumentException>(() => WordboundWorkshopPlacement.ParsePool(text));
        }

        [Test]
        public void ChangingRandomZoneToFixedStartsAtItsRoomCentreAndCanBeUndone()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            var settings = new WordboundWorkshopPlacement { Mode = 0, Pool = WordboundWorkshopPlacement.ParsePool("STOOL") };
            history.Execute("Make fixed", draft => settings.Apply(draft, preset.Catalogue, "Garden", "GardenProps"));
            var course = history.Current.Course();
            var zone = course.Zones.Single(item => item.Id == "GardenProps");
            Assert.AreEqual("Fixed", zone.Placement);
            Assert.AreEqual(course.Modules.Single(item => item.Id == "Garden").Position, zone.FixedPosition);
            Assert.AreEqual(1, zone.MinCount); Assert.AreEqual(1, zone.MaxCount); Assert.AreEqual(1000, zone.ChancePermille);
            Assert.IsTrue(zone.Bounds.Contains(zone.FixedPosition));
            Assert.IsTrue(history.Undo()); Assert.AreEqual(preset.Snapshot.RecoveryJson, history.Current.RecoveryJson);
        }

        [Test]
        public void HeightRotationAndFloorRangesRoundTripAndUndoAsOneEdit()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            history.Execute("Add floor", draft => draft.Course.Layers.Add(new CourseLayer { Id = "Upper", HeightMm = 4000 }));
            var before = history.Current;
            int revision = history.Revision;
            var original = before.Course().Zones.Single(item => item.Id == "GardenProps");
            var otherBefore = before.Course().Zones.Single(item => item.Id == "FixedStool");
            var bounds = new CourseBox(new CoursePoint(original.Bounds.Min.X, 250, original.Bounds.Min.Z),
                new CoursePoint(original.Bounds.Max.X, 750, original.Bounds.Max.Z));
            var settings = new WordboundWorkshopPlacement
            {
                Mode = 1, MinCount = 0, MaxCount = 2, Pool = WordboundWorkshopPlacement.ParsePool("STOOL"),
                Yaws = new List<int> { 90, 270 }, LayerIds = new List<string> { "Ground", "Upper" }
            };
            Assert.IsTrue(history.Execute("Ranges", draft => settings.Apply(draft, preset.Catalogue, original.ModuleId, original.Id, bounds)));
            var applied = history.Current;
            Assert.AreEqual(revision + 1, history.Revision);
            settings.Yaws.Clear(); settings.LayerIds.Clear();
            var actual = applied.Course().Zones.Single(item => item.Id == original.Id);
            Assert.AreEqual(bounds, actual.Bounds);
            CollectionAssert.AreEqual(new[] { 90, 270 }, actual.Yaws);
            CollectionAssert.AreEquivalent(new[] { "Ground", "Upper" }, actual.LayerIds);
            Assert.AreEqual(original.ModuleId, actual.ModuleId);
            Assert.AreEqual(original.GridMm, actual.GridMm); Assert.AreEqual(original.Purpose, actual.Purpose);
            var other = applied.Course().Zones.Single(item => item.Id == "FixedStool");
            Assert.AreEqual(otherBefore.Bounds, other.Bounds); Assert.AreEqual(otherBefore.FixedPosition, other.FixedPosition);
            string exported = CourseJson.ExportDocument(applied.Course());
            var imported = CourseJson.ImportDocument(exported);
            Assert.IsNotNull(imported.Document, imported.Validation.ToString());
            Assert.AreEqual(exported, CourseJson.ExportDocument(imported.Document));
            Assert.IsTrue(history.Undo()); Assert.AreSame(before, history.Current);
            Assert.IsTrue(history.Redo()); Assert.AreSame(applied, history.Current);
        }

        [TestCase("height")]
        [TestCase("empty yaw")]
        [TestCase("duplicate yaw")]
        [TestCase("arbitrary yaw")]
        [TestCase("empty floors")]
        [TestCase("owner floor")]
        [TestCase("unknown floor")]
        [TestCase("duplicate floor")]
        public void InvalidHeightRotationOrFloorsPreserveSnapshotAndRedo(string invalid)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            history.Execute("Rename", draft => draft.Course.Title = "Kept redo"); history.Undo();
            var before = history.Current;
            int revision = history.Revision;
            var settings = new WordboundWorkshopPlacement
            {
                Mode = 1, Pool = WordboundWorkshopPlacement.ParsePool("STOOL"),
                Yaws = new List<int> { 0, 90 }, LayerIds = new List<string> { "Ground" }
            };
            CourseBox? bounds = null;
            if (invalid == "height") bounds = new CourseBox(new CoursePoint(15000, 500, -6000), new CoursePoint(26000, 250, 6000));
            if (invalid == "empty yaw") settings.Yaws.Clear();
            if (invalid == "duplicate yaw") settings.Yaws.Add(90);
            if (invalid == "arbitrary yaw") settings.Yaws.Add(45);
            if (invalid == "empty floors") settings.LayerIds.Clear();
            if (invalid == "owner floor") settings.LayerIds = new List<string> { "Upper" };
            if (invalid == "unknown floor") settings.LayerIds.Add("Missing");
            if (invalid == "duplicate floor") settings.LayerIds.Add("Ground");
            Assert.Throws<ArgumentException>(() => history.Execute("Invalid ranges", draft =>
            {
                if (invalid == "owner floor") draft.Course.Layers.Add(new CourseLayer { Id = "Upper", HeightMm = 4000 });
                settings.Apply(draft, preset.Catalogue, "Garden", "GardenProps", bounds);
            }));
            Assert.AreSame(before, history.Current); Assert.AreEqual(revision, history.Revision); Assert.IsTrue(history.CanRedo);
            Assert.IsTrue(history.Redo()); Assert.AreEqual("Kept redo", history.Current.Course().Title);
        }

        [Test]
        public void OmittedRotationAndFloorOverridesPreserveImportedRanges()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            history.Execute("Authored ranges", draft =>
            {
                draft.Course.Layers.Add(new CourseLayer { Id = "Upper", HeightMm = 4000 });
                var zone = draft.Course.Zones.Single(item => item.Id == "GardenProps");
                zone.Yaws = new List<int> { 90, 270 }; zone.LayerIds.Add("Upper");
                zone.Bounds = new CourseBox(new CoursePoint(zone.Bounds.Min.X, 250, zone.Bounds.Min.Z),
                    new CoursePoint(zone.Bounds.Max.X, 750, zone.Bounds.Max.Z));
            });
            var original = history.Current.Course().Zones.Single(item => item.Id == "GardenProps");
            var settings = new WordboundWorkshopPlacement { Mode = 1, Pool = WordboundWorkshopPlacement.ParsePool("STOOL"), SpacingMm = 750 };
            history.Execute("Existing caller", draft => settings.Apply(draft, preset.Catalogue, "Garden", "GardenProps"));
            var actual = history.Current.Course().Zones.Single(item => item.Id == "GardenProps");
            Assert.AreEqual(original.Bounds, actual.Bounds);
            CollectionAssert.AreEqual(original.Yaws, actual.Yaws); CollectionAssert.AreEqual(original.LayerIds, actual.LayerIds);
        }

        [Test]
        public void PanelStagesRangesRejectsIntermediateTextAndCancelsWithoutChangingHistory()
        {
            string folder = Path.Combine(Path.GetTempPath(), "wordbound-range-" + Guid.NewGuid().ToString("N"));
            Assert.IsFalse(Directory.Exists(folder));
            var owner = new GameObject("Owned range panel test");
            try
            {
                var workshop = owner.AddComponent<WordboundWorkshop>();
                workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
                workshop.History.Execute("Authored ranges", draft =>
                {
                    draft.Course.Layers.Add(new CourseLayer { Id = "Upper", HeightMm = 4000 });
                    var zone = draft.Course.Zones.Single(item => item.Id == "GardenProps");
                    zone.Yaws = new List<int> { 90, 270 }; zone.LayerIds.Add("Upper");
                    zone.Bounds = new CourseBox(new CoursePoint(zone.Bounds.Min.X, 250, zone.Bounds.Min.Z),
                        new CoursePoint(zone.Bounds.Max.X, 750, zone.Bounds.Max.Z));
                });
                workshop.History.Select(new[] { "Garden" });
                workshop.History.Execute("Rename", draft => draft.Course.Title = "Panel redo"); workshop.History.Undo();
                Invoke(workshop, "Refresh");
                var selected = workshop.History.Current.Course().Zones.Single(item => item.Id == "GardenProps");
                Invoke(workshop, "SelectZone", selected);
                var before = workshop.History.Current;
                int revision = workshop.History.Revision;
                Assert.AreEqual("0.25", Read<string>(workshop, "rangeMinY"));
                Assert.AreEqual("0.75", Read<string>(workshop, "rangeMaxY"));
                CollectionAssert.AreEqual(selected.Yaws, Read<List<int>>(workshop, "rangeYaws"));
                CollectionAssert.AreEqual(selected.LayerIds, Read<List<string>>(workshop, "rangeLayers"));
                Set(workshop, "rangeMinY", "-"); Set(workshop, "rangeMaxY", "1.25");
                Set(workshop, "rangeYaws", new List<int> { 0 }); Set(workshop, "rangeLayers", new List<string> { "Ground" });
                Assert.AreSame(before, workshop.History.Current);
                Invoke(workshop, "ApplyPlacementSettings", "Garden");
                Assert.AreSame(before, workshop.History.Current); Assert.AreEqual(revision, workshop.History.Revision);
                Assert.IsTrue(workshop.History.CanRedo); Assert.IsNotEmpty(Read<string>(workshop, "status"));
                Invoke(workshop, "CancelPlacementSettings");
                Assert.AreSame(before, workshop.History.Current); Assert.AreEqual(revision, workshop.History.Revision);
                Assert.AreEqual("0.25", Read<string>(workshop, "rangeMinY"));
                Assert.AreEqual("0.75", Read<string>(workshop, "rangeMaxY"));
                CollectionAssert.AreEqual(selected.Yaws, Read<List<int>>(workshop, "rangeYaws"));
                CollectionAssert.AreEqual(selected.LayerIds, Read<List<string>>(workshop, "rangeLayers"));
                Invoke(workshop, "ApplyPlacementSettings", "Garden");
                Assert.AreSame(before, workshop.History.Current); Assert.AreEqual(revision, workshop.History.Revision);
                Assert.IsTrue(workshop.History.CanRedo); Assert.AreEqual("No changes", Read<string>(workshop, "status"));
                Assert.IsTrue(workshop.History.Redo()); Assert.AreEqual("Panel redo", workshop.History.Current.Course().Title);
                Assert.IsTrue(workshop.History.Undo());
                Invoke(workshop, "Refresh");
                Set(workshop, "rangeMinY", "0.5"); Set(workshop, "rangeMaxY", "1.25");
                Set(workshop, "rangeYaws", new List<int> { 180 }); Set(workshop, "rangeLayers", new List<string> { "Ground" });
                revision = workshop.History.Revision;
                Invoke(workshop, "ApplyPlacementSettings", "Garden");
                var applied = workshop.History.Current;
                var actual = applied.Course().Zones.Single(item => item.Id == "GardenProps");
                Assert.AreEqual(revision + 1, workshop.History.Revision); Assert.IsFalse(workshop.History.CanRedo);
                Assert.AreEqual("Applied", Read<string>(workshop, "status"));
                Assert.AreEqual(500, actual.Bounds.Min.Y); Assert.AreEqual(1250, actual.Bounds.Max.Y);
                Assert.AreEqual(selected.Bounds.Min.X, actual.Bounds.Min.X); Assert.AreEqual(selected.Bounds.Max.X, actual.Bounds.Max.X);
                Assert.AreEqual(selected.Bounds.Min.Z, actual.Bounds.Min.Z); Assert.AreEqual(selected.Bounds.Max.Z, actual.Bounds.Max.Z);
                CollectionAssert.AreEqual(new[] { 180 }, actual.Yaws); CollectionAssert.AreEqual(new[] { "Ground" }, actual.LayerIds);
                Assert.IsTrue(workshop.History.Undo()); Assert.AreSame(before, workshop.History.Current);
                Assert.IsTrue(workshop.History.Redo()); Assert.AreSame(applied, workshop.History.Current);
            }
            finally
            {
                try { UnityEngine.Object.DestroyImmediate(owner); }
                finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            }
        }

        static object Invoke(WordboundWorkshop workshop, string name, params object[] args) =>
            typeof(WordboundWorkshop).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args);
        static T Read<T>(WordboundWorkshop workshop, string name) =>
            (T)typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workshop);
        static void Set(WordboundWorkshop workshop, string name, object value) =>
            typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workshop, value);
    }
}
