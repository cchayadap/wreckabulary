using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopRoutesTests
    {
        static WorkshopHistory Existing() => new WorkshopHistory(new WorkshopSnapshot(CourseTemplates.RaceCircuit()));
        static readonly string[] North = { "UpA", "Door", "Return" };
        static readonly string[] South = { "UpB", "Door", "Return" };
        static readonly string[] NorthStops = { "Begin", "UpperA", "Finish" };

        static void RejectWithoutChange(WorkshopHistory history, TestDelegate operation)
        {
            string before = history.Current.RecoveryJson; int revision = history.Revision;
            Assert.Throws<InvalidOperationException>(operation);
            Assert.AreEqual(before, history.Current.RecoveryJson); Assert.AreEqual(revision, history.Revision);
        }

        [Test]
        public void AuthorAllThreeLapsFromGeometryWithExplicitStableReferences()
        {
            var document = CourseTemplates.RaceCircuit();
            document.Puzzles.Clear(); document.Supplies.Clear(); document.Checkpoints.Clear();
            document.RecoveryAnchors.Clear(); document.RouteSections.Clear(); document.Laps.Clear(); document.Spawns.Clear();
            foreach (var edge in document.Connections) edge.GatePuzzleId = "";
            var history = new WorkshopHistory(new WorkshopSnapshot(document)); string original = history.Current.CourseJsonText;
            string begin = "", upper = "", finish = "", section = "";
            history.Execute("Author route", draft =>
            {
                WorkshopRoutes.AddSpawn(draft, CourseTemplates.ProofCatalogue(), "Start", "Start", new CoursePoint(-2000, 0, -6000));
                WorkshopRoutes.AddSpawn(draft, CourseTemplates.ProofCatalogue(), "Start", "Start", new CoursePoint(2000, 0, -6000));
                string home = WorkshopRoutes.AddRecovery(draft, CourseTemplates.ProofCatalogue(), "Start", new CoursePoint(0, 0, -8000));
                string loft = WorkshopRoutes.AddRecovery(draft, CourseTemplates.ProofCatalogue(), "Upper", new CoursePoint(0, 6000, -8000));
                begin = WorkshopRoutes.AddCheckpoint(draft, "Start", home);
                upper = WorkshopRoutes.AddCheckpoint(draft, "Upper", loft, "UpA");
                finish = WorkshopRoutes.AddCheckpoint(draft, "Start", home, "Return");
                section = WorkshopRoutes.AddSection(draft, "Start", "Start");
                string branch = draft.Course.RouteSections.Single().Alternatives.Single().Id;
                WorkshopRoutes.SetAlternative(draft, section, branch, North, new[] { begin, upper, finish }, 1);
                for (int lap = 1; lap <= 3; lap++)
                    WorkshopRoutes.SetLap(draft, CourseTemplates.ProofCatalogue(), lap, "Start", "Start", "FindAddress", new[] { section });
            });
            string authored = history.Current.CourseJsonText;
            var mode = ModeDefinition.For("TeamRace", RaceFormat.Solo);
            var resolved = history.Preview(CourseTemplates.ProofCatalogue(), MatchStateValidation.Requirements(mode));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            CollectionAssert.AreEqual(new[] { begin, upper, finish }, history.Current.Course().RouteSections.Single().Alternatives.Single().CheckpointIds);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(original, history.Current.CourseJsonText);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(authored, history.Current.CourseJsonText);
            Assert.AreEqual(authored, CourseJson.ExportDocument(CourseJson.ImportDocument(authored).Document));
        }

        [Test]
        public void ReorderRejectsCheckpointOutsideTravelOrderWithoutCommitting()
        {
            var history = Existing();
            RejectWithoutChange(history, () => history.Execute("Invalid order", draft =>
                WorkshopRoutes.SetAlternative(draft, "Circuit", "North", North, new[] { "Begin", "Finish", "UpperA" }, 1)));
        }

        [Test]
        public void AlternativeWeightsAndBranchChoicesRemainAuthoredAndDeterministic()
        {
            var history = Existing();
            history.Execute("Weights", draft =>
            {
                WorkshopRoutes.SetAlternative(draft, "Circuit", "North", North, NorthStops, 9);
                WorkshopRoutes.SetAlternative(draft, "Circuit", "South", South, new[] { "Begin", "UpperB", "Finish" }, 1);
            });
            var section = history.Current.Course().RouteSections.Single();
            CollectionAssert.AreEqual(new[] { 9, 1 }, section.Alternatives.Select(a => a.Weight));
            var requirements = MatchStateValidation.Requirements(ModeDefinition.For("TeamRace", RaceFormat.Solo));
            var first = history.Preview(CourseTemplates.ProofCatalogue(), requirements);
            var second = history.Preview(CourseTemplates.ProofCatalogue(), requirements);
            Assert.IsTrue(first.Success, string.Join("\n", first.Issues)); Assert.AreEqual(first.Manifest.Hash, second.Manifest.Hash);
        }

        [Test]
        public void ReverseRefusesOneWayAndDoesNotChangeItsDirection()
        {
            var history = Existing();
            history.Execute("One way", draft => draft.Course.Connections.Single(c => c.Id == "UpA").Bidirectional = false);
            RejectWithoutChange(history, () => history.Execute("Reverse", draft => WorkshopRoutes.ReverseSection(draft, "Circuit")));
            Assert.IsFalse(history.Current.Course().Connections.Single(c => c.Id == "UpA").Bidirectional);
        }

        [Test]
        public void ReverseRequiresExplicitArrivalRepairAndPreservesCheckpointIds()
        {
            var history = Existing();
            RejectWithoutChange(history, () => history.Execute("Reverse", draft => WorkshopRoutes.ReverseSection(draft, "Circuit")));
            history.Execute("Explicit arrival repair", draft =>
            {
                foreach (var checkpoint in draft.Course.Checkpoints.ToArray())
                    WorkshopRoutes.SetCheckpoint(draft, checkpoint.Id, checkpoint.RecoveryAnchorId, "", checkpoint.PuzzleIds);
            });
            string[] ids = history.Current.Course().Checkpoints.Select(c => c.Id).ToArray();
            history.Execute("Reverse", draft => WorkshopRoutes.ReverseSection(draft, "Circuit"));
            var reversed = history.Current.Course();
            CollectionAssert.AreEqual(North.Reverse(), reversed.RouteSections.Single().Alternatives.Single(a => a.Id == "North").ConnectionIds);
            CollectionAssert.AreEqual(NorthStops.Reverse(), reversed.RouteSections.Single().Alternatives.Single(a => a.Id == "North").CheckpointIds);
            CollectionAssert.AreEqual(ids, reversed.Checkpoints.Select(c => c.Id));
            Assert.IsTrue(reversed.Checkpoints.All(c => c.EntryConnectionId == ""));
        }

        [Test]
        public void RecoveryRequiresClearanceAndCannotBeDeletedWhileReferenced()
        {
            var history = Existing();
            Assert.Throws<ArgumentException>(() => history.Execute("Outside", draft =>
                WorkshopRoutes.AddRecovery(draft, CourseTemplates.ProofCatalogue(), "Start", new CoursePoint(10000, 0, 0))));
            RejectWithoutChange(history, () => history.Execute("Delete recovery", draft => WorkshopRoutes.DeleteRecovery(draft, "StartSafe")));
            history.Execute("Anchor", draft => WorkshopRoutes.SetRecovery(draft, CourseTemplates.ProofCatalogue(), "StartSafe",
                new CoursePoint(-2000, 0, -7000), 1500, Array.Empty<string>()));
            Assert.AreEqual(1500, history.Current.Course().RecoveryAnchors.Single(r => r.Id == "StartSafe").RadiusMm);
        }

        [Test]
        public void ExplicitCheckpointDeletionDetachesEveryBranchAndUndoRestoresThem()
        {
            var history = Existing(); string before = history.Current.CourseJsonText;
            history.Execute("Delete stop from routes", draft => WorkshopRoutes.DeleteCheckpointFromRoutes(draft, "Begin"));
            var course = history.Current.Course();
            Assert.IsFalse(course.Checkpoints.Any(c => c.Id == "Begin"));
            Assert.IsFalse(course.RouteSections.SelectMany(s => s.Alternatives).Any(a => a.CheckpointIds.Contains("Begin")));
            Assert.IsTrue(course.Puzzles.Any(p => p.Id == "BedBridge"));
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.CourseJsonText);
        }

        [Test]
        public void PhysicalCheckpointCannotBeDeletedOrUnlinkedImplicitly()
        {
            var document = CourseTemplates.RaceCircuit(); document.Schema = 2;
            document.Puzzles.Single(p => p.Id == "BedBridge").PresetId = "PlaceObject";
            document.PhysicalSites.Add(new CoursePhysicalSite { Lap = 1, PuzzleId = "BedBridge",
                CheckpointId = "Begin", RecoverySupplyId = "BedSupply", Position = default });
            var history = new WorkshopHistory(new WorkshopSnapshot(document));
            RejectWithoutChange(history, () => history.Execute("Delete stop", draft => WorkshopRoutes.DeleteCheckpointFromRoutes(draft, "Begin")));
            RejectWithoutChange(history, () => history.Execute("Unlink puzzle", draft =>
                WorkshopRoutes.SetCheckpoint(draft, "Begin", "StartSafe", "", Array.Empty<string>())));
        }

        [Test]
        public void LockedFloorProtectsRecoveryCheckpointAndRouteEdits()
        {
            var history = Existing();
            history.Execute("Lock loft", draft => draft.LockedLayers.Add("Loft"));
            RejectWithoutChange(history, () => history.Execute("Recovery", draft =>
                WorkshopRoutes.SetRecovery(draft, CourseTemplates.ProofCatalogue(), "UpperSafe", new CoursePoint(0, 6000, -7000), 1000, Array.Empty<string>())));
            RejectWithoutChange(history, () => history.Execute("Checkpoint", draft =>
                WorkshopRoutes.SetCheckpoint(draft, "UpperA", "UpperSafe", "UpA", new[] { "FanDoor" })));
            RejectWithoutChange(history, () => history.Execute("Route", draft =>
                WorkshopRoutes.SetAlternative(draft, "Circuit", "North", North, NorthStops, 3)));
        }

        [Test]
        public void NewBranchUsesFreshIdAndLastBranchNeedsExplicitRouteDeletion()
        {
            var history = Existing(); string added = "";
            history.Execute("Add branch", draft => added = WorkshopRoutes.AddAlternative(draft, "Circuit"));
            Assert.AreNotEqual("North", added); Assert.AreNotEqual("South", added);
            history.Execute("Remove branches", draft =>
            {
                WorkshopRoutes.DeleteAlternative(draft, "Circuit", added);
                WorkshopRoutes.DeleteAlternative(draft, "Circuit", "South");
            });
            RejectWithoutChange(history, () => history.Execute("Last branch", draft => WorkshopRoutes.DeleteAlternative(draft, "Circuit", "North")));
            history.Execute("Delete route from laps", draft => WorkshopRoutes.DeleteSectionFromLaps(draft, "Circuit"));
            Assert.IsEmpty(history.Current.Course().RouteSections);
            Assert.IsTrue(history.Current.Course().Laps.All(l => l.SectionIds.Count == 0));
        }

        [Test]
        public void LapRejectsUnconnectedEndpointsWithoutChangingImportedDefinition()
        {
            var history = Existing();
            RejectWithoutChange(history, () => history.Execute("Wrong finish", draft =>
                WorkshopRoutes.SetLap(draft, CourseTemplates.ProofCatalogue(), 1, "Start", "Upper", "FindAddress", new[] { "Circuit" })));
            history.Execute("Lap story", draft => WorkshopRoutes.SetLap(draft, CourseTemplates.ProofCatalogue(), 2,
                "Start", "Start", "RestoreHome", new[] { "Circuit" }));
            Assert.AreEqual("RestoreHome", history.Current.Course().Laps.Single(l => l.Number == 2).NarrativeId);
            Assert.AreEqual("Circuit", history.Current.Course().RouteSections.Single().Id);
        }

        [Test]
        public void MissingOldConnectionCanBeExplicitlyRepairedWithoutRegeneratingIds()
        {
            var history = Existing();
            history.Execute("Remove connection", draft => draft.Course.Connections.RemoveAll(c => c.Id == "UpA"));
            history.Execute("Repair branch", draft => WorkshopRoutes.SetAlternative(draft, "Circuit", "North",
                South, new[] { "Begin", "UpperB", "Finish" }, 1));
            var section = history.Current.Course().RouteSections.Single();
            Assert.AreEqual("Circuit", section.Id);
            CollectionAssert.AreEqual(South, section.Alternatives.Single(a => a.Id == "North").ConnectionIds);
            Assert.IsFalse(history.Current.Course().Connections.Any(c => c.Id == "UpA"));
        }

        [Test]
        public void SpawnCreationAndMovePreserveIdAndUndoWithExplicitKindAndFloor()
        {
            var document = CourseTemplates.RaceCircuit(); document.Spawns.Clear();
            var history = new WorkshopHistory(new WorkshopSnapshot(document)); string id = "";
            history.Execute("Add spawn", draft => id = WorkshopRoutes.AddSpawn(draft, CourseTemplates.ProofCatalogue(),
                "Start", "Start", new CoursePoint(0, 0, 0), 90));
            string original = history.Current.CourseJsonText;
            history.Execute("Move spawn to loft", draft => WorkshopRoutes.SetSpawn(draft, CourseTemplates.ProofCatalogue(),
                id, "Upper", "Respawn", new CoursePoint(0, 6000, 0), 270));
            var spawn = history.Current.Course().Spawns.Single();
            Assert.AreEqual(id, spawn.Id); Assert.AreEqual("Upper", spawn.ModuleId);
            Assert.AreEqual("Respawn", spawn.Kind); Assert.AreEqual(270, spawn.Yaw);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(original, history.Current.CourseJsonText);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(id, history.Current.Course().Spawns.Single().Id);
        }

        [Test]
        public void SpawnClearanceSpacingRotationAndOldFloorLocksAreEnforced()
        {
            var document = CourseTemplates.RaceCircuit(); document.Spawns.Clear();
            var history = new WorkshopHistory(new WorkshopSnapshot(document)); string id = "";
            history.Execute("First spawn", draft => id = WorkshopRoutes.AddSpawn(draft, CourseTemplates.ProofCatalogue(),
                "Start", "Start", default));
            Assert.Throws<ArgumentException>(() => history.Execute("Overlap", draft => WorkshopRoutes.AddSpawn(draft,
                CourseTemplates.ProofCatalogue(), "Start", "Start", new CoursePoint(999, 0, 0))));
            Assert.Throws<ArgumentException>(() => history.Execute("Wall", draft => WorkshopRoutes.AddSpawn(draft,
                CourseTemplates.ProofCatalogue(), "Start", "Start", new CoursePoint(9800, 0, 0))));
            Assert.Throws<ArgumentException>(() => history.Execute("Rotation", draft => WorkshopRoutes.AddSpawn(draft,
                CourseTemplates.ProofCatalogue(), "Start", "Start", new CoursePoint(1000, 0, 0), 45)));
            history.Execute("Exact spacing", draft => WorkshopRoutes.AddSpawn(draft, CourseTemplates.ProofCatalogue(),
                "Start", "Start", new CoursePoint(1000, 0, 0)));
            history.Execute("Lock origin", draft => draft.LockedLayers.Add("Ground"));
            RejectWithoutChange(history, () => history.Execute("Move locked spawn", draft => WorkshopRoutes.SetSpawn(draft,
                CourseTemplates.ProofCatalogue(), id, "Upper", "Start", new CoursePoint(0, 6000, 0), 0)));
        }

        [Test]
        public void DeletingStartsLeavesExplicitCapacityValidationInsteadOfGeneratingReplacements()
        {
            var history = Existing();
            history.Execute("Delete starts", draft =>
            {
                foreach (string id in draft.Course.Spawns.Select(s => s.Id).ToArray()) WorkshopRoutes.DeleteSpawn(draft, id);
            });
            Assert.IsEmpty(history.Current.Course().Spawns);
            var validation = CourseValidator.Validate(history.Current.Course(), CourseTemplates.ProofCatalogue(),
                MatchStateValidation.Requirements(ModeDefinition.For("TeamRace", RaceFormat.Solo)));
            Assert.IsTrue(validation.Issues.Any(i => i.Code == "spawn.capacity"));
            Assert.IsTrue(history.Undo()); Assert.IsNotEmpty(history.Current.Course().Spawns);
        }
    }
}
