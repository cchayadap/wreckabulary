using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopTests
    {
        static CourseDocument Course()
        {
            var course = new CourseDocument { Id = "workshop-test", Title = "Workshop test", SupportedModes = new List<string> { "Freestyle" } };
            course.Layers.Add(new CourseLayer { Id = "ground", HeightMm = 0 });
            course.Layers.Add(new CourseLayer { Id = "upper", HeightMm = 4000 });
            course.Modules.Add(new CourseModule { Id = "a", DefinitionId = "Room", LayerId = "ground", Position = new CoursePoint(0, 0, 0) });
            course.Modules.Add(new CourseModule { Id = "b", DefinitionId = "Room", LayerId = "ground", Position = new CoursePoint(8000, 0, 0) });
            course.Connections.Add(new CourseConnection { Id = "ab", FromModuleId = "a", ToModuleId = "b", From = new CoursePoint(4000, 0, 0), To = new CoursePoint(4000, 0, 0), GatePuzzleId = "p" });
            course.Puzzles.Add(new CoursePuzzle { Id = "p", ModuleId = "a", PresetId = "Socket", RequiredLetters = "BAT", RecipeId = "BAT", RecoverySupplyIds = new List<string> { "s" } });
            course.Supplies.Add(new CourseSupply { Id = "s", ModuleId = "a", Letters = "BAT", Position = new CoursePoint(1000, 0, 1000) });
            course.Spawns.Add(new CourseSpawn { Id = "start", ModuleId = "a", Position = new CoursePoint(-2000, 0, 0) });
            course.RecoveryAnchors.Add(new CourseRecovery { Id = "recovery", ModuleId = "a", Position = new CoursePoint(-2000, 0, 1000) });
            course.Checkpoints.Add(new CourseCheckpoint { Id = "cp", ModuleId = "a", RecoveryAnchorId = "recovery", PuzzleIds = new List<string> { "p" } });
            course.Zones.Add(new CourseZone
            {
                Id = "zone", ModuleId = "a", Bounds = new CourseBox(new CoursePoint(-3000, 0, -3000), new CoursePoint(3000, 0, 3000)),
                LayerIds = new List<string> { "ground" }, Pool = new List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "BAT" } }
            });
            return course;
        }
        static WorkshopHistory History() => new WorkshopHistory(new WorkshopSnapshot(Course()));

        [Test]
        public void SnapshotAndTransactionDoNotLeakMutableAliases()
        {
            var source = Course(); var snapshot = new WorkshopSnapshot(source);
            source.Modules[0].Position = new CoursePoint(9999, 0, 0);
            Assert.AreEqual(0, snapshot.Course().Modules.Single(module => module.Id == "a").Position.X);
            var history = new WorkshopHistory(snapshot); WorkshopDraft leaked = null;
            history.Execute("Rename", draft => { leaked = draft; draft.Course.Title = "Changed"; });
            leaked.Course.Title = "Mutated outside transaction";
            var exposed = history.Current.Course(); exposed.Modules.Clear();
            Assert.AreEqual("Changed", history.Current.Course().Title);
            Assert.AreEqual(2, history.Current.Course().Modules.Count);
        }
        [Test]
        public void InvalidTransactionRollsBackAndDoesNotLoseRedo()
        {
            var history = History(); history.Execute("Rename", draft => draft.Course.Title = "Next");
            history.Undo(); string before = history.Current.RecoveryJson; int revision = history.Revision;
            Assert.Throws<ArgumentException>(() => history.Execute("Bad", draft => draft.Course.Modules.Add(draft.Course.Modules[0])));
            Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.AreEqual(revision, history.Revision); Assert.IsTrue(history.CanRedo);
            history.Redo(); Assert.AreEqual("Next", history.Current.Course().Title);
        }
        [Test]
        public void MovingSelectionTransformsOwnedObjectsAndOnlyItsConnectionEndpoint()
        {
            var history = History(); history.Select(new[] { "a" });
            history.Execute("Move", draft => WorkshopCommands.Move(draft, new CoursePoint(730, 0, -260)));
            var course = history.Current.Course();
            Assert.AreEqual(new CoursePoint(500, 0, -500), course.Modules.Single(module => module.Id == "a").Position);
            Assert.AreEqual(new CoursePoint(1500, 0, 500), course.Supplies.Single().Position);
            Assert.AreEqual(new CoursePoint(4500, 0, -500), course.Connections.Single().From);
            Assert.AreEqual(new CoursePoint(4000, 0, 0), course.Connections.Single().To);
            history.Undo(); Assert.AreEqual(new CoursePoint(1000, 0, 1000), history.Current.Course().Supplies.Single().Position);
        }
        [Test]
        public void QuarterTurnAndLayerChangeCarryAttachedData()
        {
            var history = History(); history.Select(new[] { "a" });
            history.Execute("Rotate", draft => WorkshopCommands.RotateSelection(draft, new CoursePoint(0, 0, 0), 1));
            Assert.AreEqual(new CoursePoint(1000, 0, -1000), history.Current.Course().Supplies.Single().Position);
            Assert.AreEqual(90, history.Current.Course().Modules.Single(module => module.Id == "a").Yaw);
            history.Execute("Layer", draft => WorkshopCommands.ChangeLayer(draft, "upper"));
            var result = history.Current.Course();
            Assert.AreEqual(4000, result.Supplies.Single().Position.Y);
            Assert.AreEqual("upper", result.Modules.Single(module => module.Id == "a").LayerId);
            CollectionAssert.AreEqual(new[] { "upper" }, result.Zones.Single().LayerIds);
            Assert.AreEqual(0, result.Connections.Single().To.Y);
        }
        [Test]
        public void GroupSelectionExpandsAndLockedMembersBlockWholeMove()
        {
            var history = History(); history.Select(new[] { "a", "b" });
            history.Execute("Group", draft => WorkshopCommands.Group(draft, "pair"));
            history.Select(new[] { "a" });
            CollectionAssert.AreEqual(new[] { "a", "b" }, history.Current.Selection);
            history.Execute("Lock", draft => draft.LockedLayers.Add("ground"));
            string before = history.Current.RecoveryJson;
            Assert.Throws<InvalidOperationException>(() => history.Execute("Move", draft => WorkshopCommands.Move(draft, new CoursePoint(500, 0, 0))));
            Assert.AreEqual(before, history.Current.RecoveryJson);
        }
        [Test]
        public void DuplicateRemapsOwnedSubgraphWithoutAliasingOriginal()
        {
            var history = History(); history.Select(new[] { "a", "b" });
            history.Execute("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(0, 0, 10000)));
            var course = history.Current.Course();
            Assert.AreEqual(4, course.Modules.Count); Assert.AreEqual(2, course.Puzzles.Count); Assert.AreEqual(2, course.Connections.Count);
            var duplicate = course.Puzzles.Single(puzzle => puzzle.Id != "p");
            Assert.AreNotEqual("a", duplicate.ModuleId);
            Assert.AreNotEqual("s", duplicate.RecoverySupplyIds.Single());
            Assert.AreEqual(duplicate.Id, course.Connections.Single(connection => connection.Id != "ab").GatePuzzleId);
            Assert.AreEqual(new CoursePoint(1000, 0, 11000), course.Supplies.Single(supply => supply.Id != "s").Position);
            Assert.AreEqual(new CoursePoint(1000, 0, 1000), course.Supplies.Single(supply => supply.Id == "s").Position);
        }
        [Test]
        public void HistoryIsBoundedAndNewEditsDiscardRedo()
        {
            var history = History();
            for (int i = 0; i < 70; i++) { int value = i; history.Execute("Title", draft => draft.Course.Title = "Title " + value); }
            int undos = 0; while (history.Undo()) undos++;
            Assert.AreEqual(64, undos); Assert.IsTrue(history.CanRedo);
            history.Execute("Branch", draft => draft.Course.Title = "Branch");
            Assert.IsFalse(history.CanRedo);
        }
        [Test]
        public void RecoveryRoundTripsEditorSettingsWhileShareContainsOnlyCourse()
        {
            var history = History(); history.Select(new[] { "a", "b" });
            history.Execute("Settings", draft =>
            {
                WorkshopCommands.Group(draft, "pair"); draft.HiddenLayers.Add("upper");
                draft.LockedLayers.Add("upper"); draft.Seed = "0123456789abcdef"; draft.GridMm = 1000;
            });
            var restored = WorkshopSnapshot.Recover(history.Current.RecoveryJson);
            Assert.AreEqual(history.Current.RecoveryJson, restored.RecoveryJson);
            Assert.IsFalse(restored.CourseJsonText.Contains("hiddenLayers"));
            Assert.IsFalse(CourseJson.ImportDocument(restored.RecoveryJson).Success);
            Assert.IsTrue(CourseJson.ImportDocument(restored.CourseJsonText).Success);
            Assert.Throws<FormatException>(() => WorkshopSnapshot.Recover("{\"schema\":1,\"map\":\"home\",\"props\":[]}"));
        }
        [Test]
        public void LockedSeedRejectsRerollUntilExplicitlyUnlocked()
        {
            var history = History();
            Assert.IsFalse(history.Reroll("0123456789abcdef"));
            history.Execute("Unlock seed", draft => draft.SeedLocked = false);
            Assert.IsTrue(history.Reroll("0123456789abcdef"));
            Assert.AreEqual("0123456789abcdef", history.Current.Seed);
            history.Undo(); Assert.AreEqual("0000000000000001", history.Current.Seed);
        }
        [TestCase(WorkshopTemplate.RaceCircuit)]
        [TestCase(WorkshopTemplate.CombatArena)]
        [TestCase(WorkshopTemplate.FreestyleWorld)]
        public void StartingTemplatesResolveExactDraftWithoutChangingHistory(WorkshopTemplate template)
        {
            var preset = WorkshopTemplates.Create(template); var history = new WorkshopHistory(preset.Snapshot);
            string before = history.Current.RecoveryJson;
            var preview = history.Preview(preset.Catalogue, preset.Play);
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.AreEqual(0, history.Revision); Assert.IsFalse(history.CanUndo);
        }
        [Test]
        public void FixedPlacementFollowsModuleTransform()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot); history.Select(new[] { "Start" });
            var original = history.Current.Course().Zones.Single(zone => zone.Placement == "Fixed");
            history.Execute("Move", draft => WorkshopCommands.Move(draft, new CoursePoint(500, 0, 1000)));
            var moved = history.Current.Course().Zones.Single(zone => zone.Placement == "Fixed");
            Assert.AreEqual(new CoursePoint(original.FixedPosition.X + 500, original.FixedPosition.Y, original.FixedPosition.Z + 1000), moved.FixedPosition);
            history.Execute("Rotate", draft => WorkshopCommands.RotateSelection(draft, new CoursePoint(0, 0, 0), 1));
            Assert.AreEqual(180, history.Current.Course().Zones.Single(zone => zone.Placement == "Fixed").FixedYaw);
        }
        [Test]
        public void AutosaveFallsBackToPreviousValidSnapshotAndRejectsTraversalIds()
        {
            string folder = Path.Combine(Path.GetTempPath(), "wreckabulary-workshop-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new WorkshopStorage(folder); var history = History();
                store.Save(history.Current);
                history.Execute("Title", draft => draft.Course.Title = "Newer");
                store.Save(history.Current);
                File.WriteAllText(Path.Combine(folder, "workshop-test.autosave.json"), "interrupted save");
                var recovered = store.Recover("workshop-test", out bool backup);
                Assert.IsTrue(backup); Assert.AreEqual("Workshop test", recovered.Course().Title);
                Assert.Throws<ArgumentException>(() => store.Recover("../outside", out _));
                string shared = store.SaveShare(history.Current);
                Assert.IsTrue(File.Exists(shared));
                Assert.AreEqual("Newer", store.LoadShare("workshop-test").Course().Title);
                Assert.Throws<FormatException>(() => store.ImportCourse("{\"schema\":1,\"map\":\"home\",\"props\":[]}"));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
