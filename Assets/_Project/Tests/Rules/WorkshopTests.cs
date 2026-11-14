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

        [TestCase(false)]
        [TestCase(true)]
        public void LargeSavedCourseRoundTripsRecoveryAndEscapedCourseProperty(bool escapedProperty)
        {
            var course = Course();
            for (int i = 0; i < 250; i++) course.Modules.Add(new CourseModule
            {
                Id = "room-" + i, DefinitionId = "Room", LayerId = "ground", Position = new CoursePoint(i * 500, 0, 10000)
            });
            var history = new WorkshopHistory(new WorkshopSnapshot(course));
            history.Select(new[] { "a", "b" });
            history.Execute("Workspace", draft =>
            {
                WorkshopCommands.Group(draft, "pair"); draft.HiddenLayers.Add("upper");
                draft.LockedLayers.Add("upper"); draft.Seed = "0123456789abcdef"; draft.GridMm = 1000;
            });
            Assert.Greater(history.Current.CourseJsonText.Length, 24578, "Exercise a document that exceeds an ordinary JSON string token.");
            string saved = history.Current.RecoveryJson;
            if (escapedProperty) saved = saved.Replace("\"course\":", "\"cour\\u0073e\":");
            var restored = WorkshopSnapshot.Recover(saved);
            Assert.AreEqual(history.Current.RecoveryJson, restored.RecoveryJson);
            Assert.AreEqual(history.Current.CourseJsonText, restored.CourseJsonText);
            Assert.IsTrue(CourseJson.ImportDocument(restored.CourseJsonText).Success);
        }

        [TestCase("metadata")]
        [TestCase("escaped-metadata")]
        [TestCase("nested-course")]
        [TestCase("course-array")]
        [TestCase("course-object")]
        [TestCase("after-course")]
        public void RecoveryLargeTokenExceptionIsLimitedToTheRootCourseValue(string location)
        {
            string large = RecoveryQuote(new string('x', 30000));
            string json;
            switch (location)
            {
                case "metadata": json = "{\"activeLayer\":" + large + "}"; break;
                case "escaped-metadata": json = "{\"activeL\\u0061yer\":" + large + "}"; break;
                case "nested-course": json = "{\"wrapper\":{\"course\":" + large + "}}"; break;
                case "course-array": json = "{\"course\":[" + large + "]}"; break;
                case "course-object": json = "{\"course\":{\"course\":" + large + "}}"; break;
                default: json = "{\"course\":\"{}\",\"activeLayer\":" + large + "}"; break;
            }
            Assert.AreEqual("JSON string token too long.", Assert.Throws<FormatException>(() => WorkshopSnapshot.Recover(json)).Message);
        }

        [TestCase("ordinary-string")]
        [TestCase("byte-limit")]
        [TestCase("depth")]
        [TestCase("schema")]
        [TestCase("duplicate-field")]
        [TestCase("escape")]
        public void RecoveryStillAppliesTheStrictEmbeddedCourseBoundary(string mutation)
        {
            var snapshot = new WorkshopSnapshot(Course());
            string changed = snapshot.CourseJsonText;
            switch (mutation)
            {
                case "ordinary-string": changed = changed.Replace("Workshop test", new string('x', 30000)); break;
                case "byte-limit": changed = new string(' ', CourseLimits.DocumentBytes + 1); break;
                case "depth": changed = new string('[', CourseLimits.JsonDepth + 1) + "0" + new string(']', CourseLimits.JsonDepth + 1); break;
                case "schema": changed = changed.Replace("\"schema\":1", "\"schema\":99"); break;
                case "duplicate-field": changed = "{\"schema\":1," + changed.Substring(1); break;
                case "escape": changed = changed.Replace("Workshop test", "Workshop\\qtest"); break;
            }
            Assert.AreNotEqual(snapshot.CourseJsonText, changed);
            string recovery = snapshot.RecoveryJson.Replace(RecoveryQuote(snapshot.CourseJsonText), RecoveryQuote(changed));
            Assert.AreNotEqual(snapshot.RecoveryJson, recovery);
            var failure = Assert.Throws<FormatException>(() => WorkshopSnapshot.Recover(recovery));
            if (mutation == "ordinary-string") StringAssert.Contains("JSON string token too long.", failure.Message);
        }

        [Test]
        public void RecoveryEnvelopeKeepsItsByteDepthAndTokenBounds()
        {
            Assert.Throws<FormatException>(() => WorkshopSnapshot.Recover(new string(' ', CourseLimits.DocumentBytes * 3 + 1)));
            Assert.AreEqual("JSON nesting limit exceeded.", Assert.Throws<FormatException>(() =>
                WorkshopSnapshot.Recover(new string('[', CourseLimits.JsonDepth + 1) + "0" + new string(']', CourseLimits.JsonDepth + 1))).Message);
            Assert.AreEqual("JSON token limit exceeded.", Assert.Throws<FormatException>(() =>
                WorkshopSnapshot.Recover("[" + string.Join(",", Enumerable.Repeat("0", 250000)) + "]")).Message);
        }

        static string RecoveryQuote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

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
        public void OutOfBoundsTransformsRollBackBeforeSnapshotPublication()
        {
            var history = History(); history.Select(new[] { "a" });
            string before = history.Current.RecoveryJson;
            Assert.Throws<ArgumentException>(() => history.Execute("Too far", draft =>
                WorkshopCommands.Move(draft, new CoursePoint(CourseLimits.HorizontalMm + 500, 0, 0), false)));
            Assert.AreEqual(before, history.Current.RecoveryJson);
        }
        [Test]
        public void ZoneMinimumCannotBeCombinedWithProbabilisticPlacement()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            string before = history.Current.RecoveryJson;
            Assert.Throws<ArgumentException>(() => history.Execute("Invalid probability", draft =>
            {
                var zone = draft.Course.Zones.Single(item => item.Placement == "Random");
                zone.MinCount = 1; zone.ChancePermille = 500;
                WorkshopCommands.SetZone(draft, zone, preset.Catalogue);
            }));
            Assert.AreEqual(before, history.Current.RecoveryJson);
        }
        [Test]
        public void PuzzleAndGateEditsRejectUnapprovedRecipesAndLockedConnections()
        {
            var catalogue = CourseTemplates.ProofCatalogue();
            var history = new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.RaceCircuit()));
            Assert.Throws<ArgumentException>(() => history.Execute("Bad recipe", draft =>
                WorkshopCommands.SetPuzzle(draft, new CoursePuzzle
                { Id = "bad", ModuleId = "Start", PresetId = "PlaceObject", RecipeId = "DOWNLOAD", RequiredLetters = "DOWNLOAD" }, catalogue)));
            history.Execute("Lock", draft => draft.LockedLayers.Add("Ground"));
            Assert.Throws<InvalidOperationException>(() => history.Execute("Gate", draft => WorkshopCommands.Gate(draft, "UpA", "BedBridge")));
        }
        [Test]
        public void AutosaveFallsBackToPreviousValidSnapshotAndRejectsTraversalIds()
        {
            string folder = Path.Combine(Path.GetTempPath(), "wreckabulary-workshop-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new WorkshopStorage(folder); var history = History();
                Assert.IsFalse(store.HasRecovery("workshop-test"));
                store.Save(history.Current);
                Assert.IsTrue(store.HasRecovery("workshop-test"));
                history.Execute("Title", draft => draft.Course.Title = "Newer");
                store.Save(history.Current);
                File.WriteAllText(Path.Combine(folder, "workshop-test.autosave.json"), "interrupted save");
                var recovered = store.Recover("workshop-test", out bool backup);
                Assert.IsTrue(backup); Assert.AreEqual("Workshop test", recovered.Course().Title);
                File.Delete(Path.Combine(folder, "workshop-test.autosave.json"));
                Assert.IsTrue(store.HasRecovery("workshop-test"));
                Assert.AreEqual("Workshop test", store.Recover("workshop-test", out backup).Course().Title);
                Assert.IsTrue(backup);
                Assert.Throws<ArgumentException>(() => store.Recover("../outside", out _));
                string shared = store.SaveShare(history.Current);
                Assert.IsTrue(File.Exists(shared));
                Assert.AreEqual("Newer", store.LoadShare("workshop-test").Course().Title);
                Assert.Throws<FormatException>(() => store.ImportCourse("{\"schema\":1,\"map\":\"home\",\"props\":[]}"));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [Test]
        public void HandleDragPreviewsWithoutMutatingAndCommitsOneUndoStep()
        {
            var history = History(); history.Select(new[] { "a" });
            string before = history.Current.RecoveryJson;
            var drag = new WorkshopTransformDrag(history, WorkshopHandle.X, new CoursePoint(0, 0, 0));
            drag.Move(730, 9999); drag.Move(1270, -9999);
            Assert.AreEqual(new CoursePoint(1500, 0, 0), drag.Delta);
            Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.IsTrue(drag.Commit(history));
            Assert.AreEqual(1500, history.Current.Course().Modules.Single(module => module.Id == "a").Position.X);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.IsFalse(history.CanUndo);
        }
        [Test]
        public void HandleDragCannotCommitOverAConcurrentDraftChange()
        {
            var history = History(); history.Select(new[] { "a" });
            var drag = new WorkshopTransformDrag(history, WorkshopHandle.Plane, new CoursePoint(0, 0, 0));
            drag.Move(1000, 500);
            history.Execute("Rename", draft => draft.Course.Title = "Concurrent edit");
            string before = history.Current.RecoveryJson;
            Assert.Throws<InvalidOperationException>(() => drag.Commit(history));
            Assert.AreEqual(before, history.Current.RecoveryJson);
        }
        [Test]
        public void RotationHandleSnapsQuarterTurnsAndRejectsLockedSelection()
        {
            var history = History(); history.Select(new[] { "a" });
            var drag = new WorkshopTransformDrag(history, WorkshopHandle.Rotate, new CoursePoint(0, 0, 0));
            drag.Rotate(95); Assert.AreEqual(1, drag.QuarterTurns);
            Assert.Throws<ArgumentException>(() => drag.Rotate(double.NaN));
            drag.Commit(history);
            Assert.AreEqual(90, history.Current.Course().Modules.Single(module => module.Id == "a").Yaw);
            history.Execute("Lock", draft => draft.LockedLayers.Add("ground"));
            Assert.Throws<InvalidOperationException>(() => new WorkshopTransformDrag(history, WorkshopHandle.Plane, new CoursePoint(0, 0, 0)));
        }
        [Test]
        public void ControllerSelectionIsStableAndSkipsHiddenOrLockedLayers()
        {
            var history = History();
            Assert.AreEqual("a", WorkshopSelection.Step(history.Current, 1));
            history.Select(new[] { "a" }); Assert.AreEqual("b", WorkshopSelection.Step(history.Current, 1));
            history.Select(new[] { "b" }); Assert.AreEqual("a", WorkshopSelection.Step(history.Current, 1));
            history.Execute("Hide", draft => draft.HiddenLayers.Add("ground"));
            Assert.IsNull(WorkshopSelection.Step(history.Current, 1));
        }
        [Test]
        public void FixedObjectDragMovesOnlyTheObjectAndPublishesOneUndo()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            var original = history.Current.Course().Zones.Single(zone => zone.Placement == "Fixed");
            string before = history.Current.RecoveryJson;
            var drag = new WorkshopPlacementDrag(history, original.Id);
            drag.Move(700, 300); drag.Rotate(-90);
            Assert.AreEqual(before, history.Current.RecoveryJson);
            drag.Commit(history, preset.Catalogue);
            var after = history.Current.Course().Zones.Single(zone => zone.Id == original.Id);
            Assert.AreEqual(original.FixedPosition.X + 500, after.FixedPosition.X);
            Assert.AreEqual(original.FixedPosition.Z + 500, after.FixedPosition.Z);
            Assert.AreEqual(0, after.FixedYaw);
            Assert.AreEqual(preset.Snapshot.Course().Modules.First().Position, history.Current.Course().Modules.First().Position);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.IsFalse(history.CanUndo);
        }
        [Test]
        public void FixedObjectDragRejectsOutOfBoundsAndStaleCommits()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var history = new WorkshopHistory(preset.Snapshot);
            string zoneId = history.Current.Course().Zones.Single(zone => zone.Placement == "Fixed").Id;
            var drag = new WorkshopPlacementDrag(history, zoneId); drag.Move(500000, 0);
            string before = history.Current.RecoveryJson;
            Assert.Throws<ArgumentException>(() => drag.Commit(history, preset.Catalogue));
            Assert.AreEqual(before, history.Current.RecoveryJson);
            history.Execute("Rename", draft => draft.Course.Title = "Changed");
            Assert.Throws<InvalidOperationException>(() => drag.Commit(history, preset.Catalogue));
        }
        static readonly CoursePlayRequirements TimeRun = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
        static readonly CoursePlayRequirements Freestyle =
            new CoursePlayRequirements("Freestyle", CourseCapability.Explore, 0, 0, 8, CourseOwnershipPolicy.Shared);
        static CourseCatalogue SizedCatalogue()
        {
            var proof = CourseTemplates.ProofCatalogue();
            return new CourseCatalogue(proof.Modules.Values.Concat(new[] {
                    new CourseModuleDefinition("Hall", 30000, 20000, 4000), new CourseModuleDefinition("Nook", 6000, 6000, 4000) }),
                proof.Assets.Values, proof.PuzzlePresets, proof.Themes, proof.NarrativeEvents);
        }
        static string[] Issues(CourseDocument course) => CourseValidator.Validate(course, CourseTemplates.ProofCatalogue(), TimeRun)
            .Issues.Select(issue => issue.Code + " " + issue.EntityId).OrderBy(text => text, StringComparer.Ordinal).ToArray();
        [Test]
        public void FloorHeightMovesItsRoomsKeepsFloorOrderAndUndoesAsOneStep()
        {
            var history = new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.RaceCircuit()));
            history.Execute("Rename", draft => draft.Course.Title = "Kept redo"); history.Undo();
            var before = history.Current; int revision = history.Revision;
            foreach (var (layer, height) in new[] { ("Loft", 0), ("Loft", -1000), ("Loft", CourseLimits.VerticalMm + 1), ("Ground", 6000), ("Ground", 9000) })
                Assert.Throws<ArgumentException>(() => history.Execute("Bad floor", draft => WorkshopCommands.SetLayerHeight(draft, layer, height)), layer + " " + height);
            Assert.Throws<InvalidOperationException>(() => history.Execute("Locked floor", draft =>
            { draft.LockedLayers.Add("Loft"); WorkshopCommands.SetLayerHeight(draft, "Loft", 7000); }));
            Assert.AreSame(before, history.Current); Assert.AreEqual(revision, history.Revision); Assert.IsTrue(history.CanRedo);
            Assert.IsTrue(history.Execute("Floor height", draft => WorkshopCommands.SetLayerHeight(draft, "Loft", 6500)));
            Assert.AreEqual(revision + 1, history.Revision);
            var raised = history.Current.Course();
            Assert.AreEqual(6500, raised.Layers.Single(layer => layer.Id == "Loft").HeightMm);
            Assert.IsTrue(raised.Modules.Where(module => module.LayerId == "Loft").All(module => module.Position.Y == 6500));
            Assert.AreEqual(0, raised.Modules.Single(module => module.Id == "Start").Position.Y);
            var stairs = raised.Connections.Single(connection => connection.Id == "UpA");
            Assert.AreEqual(0, stairs.From.Y); Assert.AreEqual(6500, stairs.To.Y);
            Assert.AreEqual(6500, raised.Supplies.Single(supply => supply.Id == "FanSupply").Position.Y);
            Assert.AreEqual(6500, raised.RecoveryAnchors.Single(anchor => anchor.Id == "UpperSafe").Position.Y);
            Assert.AreEqual(6500, raised.Zones.Single(zone => zone.Id == "SideProps").Bounds.Min.Y);
            CollectionAssert.AreEqual(Issues(before.Course()), Issues(raised));
            Assert.IsTrue(history.Undo()); Assert.AreSame(before, history.Current);
        }
        [Test]
        public void FloorFootprintIsTheUnionOfItsRoomsAndTheNextFloorClearsTheTopRooms()
        {
            var course = CourseTemplates.RaceCircuit(); var catalogue = CourseTemplates.ProofCatalogue();
            string json = CourseJson.ExportDocument(course);
            var ground = WorkshopCommands.LayerFootprint(course, catalogue, "Ground").Value;
            Assert.AreEqual(new CoursePoint(-10000, 0, -10000), ground.Min); Assert.AreEqual(new CoursePoint(10000, 4000, 10000), ground.Max);
            var loft = WorkshopCommands.LayerFootprint(course, catalogue, "Loft").Value;
            Assert.AreEqual(new CoursePoint(-10000, 6000, -10000), loft.Min); Assert.AreEqual(new CoursePoint(30000, 10000, 10000), loft.Max);
            Assert.AreEqual(10000, WorkshopCommands.NextLayerHeight(course, catalogue));
            Assert.AreEqual(json, CourseJson.ExportDocument(course), "Footprints are derived and never stored.");
            course.Layers.Add(new CourseLayer { Id = "Attic", HeightMm = 12000 });
            Assert.IsNull(WorkshopCommands.LayerFootprint(course, catalogue, "Attic"));
            Assert.AreEqual(16000, WorkshopCommands.NextLayerHeight(course, catalogue));
            var history = new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.RaceCircuit()));
            Assert.IsTrue(history.Execute("Add floor", draft => WorkshopCommands.AddLayer(draft, WorkshopCommands.NextLayerHeight(draft.Course, catalogue))));
            Assert.AreEqual(10000, history.Current.Course().Layers.Single(layer => layer.Id == history.Current.ActiveLayer).HeightMm);
        }
        [Test]
        public void RoomEdgeDragSwapsToTheNearestApprovedSizeKeepsTheOppositeWallAndUndoesOnce()
        {
            var catalogue = SizedCatalogue();
            var history = new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.FreestyleWorld()));
            var before = history.Current; int revision = history.Revision;
            Assert.IsTrue(CourseValidator.Validate(before.Course(), catalogue, Freestyle).Ok);
            var drag = new WorkshopRoomResizeDrag(history, catalogue, "Garden", 1, 0);
            drag.Drag(38500, 4000);
            Assert.AreEqual("Hall", drag.DefinitionId); Assert.AreEqual(new CoursePoint(25000, 0, 0), drag.Position);
            Assert.AreEqual(10000, drag.Preview.Min.X); Assert.AreEqual(40000, drag.Preview.Max.X);
            Assert.AreSame(before, history.Current, "Dragging previews without changing the draft.");
            Assert.IsTrue(drag.Commit(history, Freestyle));
            Assert.AreEqual(revision + 1, history.Revision);
            var resized = history.Current.Course();
            var garden = resized.Modules.Single(module => module.Id == "Garden");
            Assert.AreEqual("Hall", garden.DefinitionId); Assert.AreEqual(new CoursePoint(25000, 0, 0), garden.Position);
            Assert.IsTrue(CourseValidator.Validate(resized, catalogue, Freestyle).Ok);
            Assert.AreEqual(before.Course().Zones.Single(zone => zone.Id == "GardenProps").Bounds, resized.Zones.Single(zone => zone.Id == "GardenProps").Bounds);
            Assert.AreEqual(before.Course().Layers.Count, resized.Layers.Count);
            Assert.IsTrue(history.Undo()); Assert.AreSame(before, history.Current); Assert.IsFalse(history.CanUndo);
            Assert.Throws<InvalidOperationException>(() => drag.Commit(history, Freestyle));
        }
        [Test]
        public void RoomResizeRefusesOrphanedContentsOverlapsAndUnapprovedSizesWithoutChangingTheDraft()
        {
            var catalogue = SizedCatalogue();
            var history = new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.FreestyleWorld()));
            history.Execute("Rename", draft => draft.Course.Title = "Kept redo"); history.Undo();
            var before = history.Current; int revision = history.Revision;
            var shrink = new WorkshopRoomResizeDrag(history, catalogue, "Garden", 1, 1);
            shrink.Drag(16000, -4000);
            Assert.AreEqual("Nook", shrink.DefinitionId);
            var orphan = Assert.Throws<InvalidOperationException>(() => shrink.Commit(history, Freestyle));
            StringAssert.StartsWith("Resize refused", orphan.Message); StringAssert.Contains("GardenProps", orphan.Message);
            var grow = new WorkshopRoomResizeDrag(history, catalogue, "Garden", -1, 0);
            grow.Drag(0, 0);
            Assert.AreEqual("Hall", grow.DefinitionId); Assert.AreEqual(15000, grow.Position.X);
            var overlap = Assert.Throws<InvalidOperationException>(() => grow.Commit(history, Freestyle));
            StringAssert.StartsWith("Resize refused", overlap.Message); StringAssert.Contains("overlap Start", overlap.Message);
            Assert.Throws<ArgumentException>(() => history.Execute("Unknown size", draft =>
                WorkshopCommands.ResizeModule(draft, catalogue, Freestyle, "Garden", "Missing", new CoursePoint(20000, 0, 0))));
            Assert.Throws<InvalidOperationException>(() => history.Execute("Locked resize", draft =>
            {
                draft.LockedLayers.Add("Ground");
                WorkshopCommands.ResizeModule(draft, catalogue, Freestyle, "Garden", "Hall", new CoursePoint(25000, 0, 0));
            }));
            var same = new WorkshopRoomResizeDrag(history, catalogue, "Garden", 1, 0);
            same.Drag(30200, 0);
            Assert.IsFalse(same.Changed); Assert.IsFalse(same.Commit(history, Freestyle));
            Assert.AreSame(before, history.Current); Assert.AreEqual(revision, history.Revision); Assert.IsTrue(history.CanRedo);
            Assert.AreEqual("Hall", WorkshopCommands.StepDefinition(catalogue, "Room", 1));
            Assert.AreEqual("Nook", WorkshopCommands.StepDefinition(catalogue, "Hall", 1, true));
            Assert.AreEqual("Hall", WorkshopCommands.StepDefinition(catalogue, "Hall", 1));
        }
    }
}
