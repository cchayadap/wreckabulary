using System;
using System.Linq;
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
    }
}
