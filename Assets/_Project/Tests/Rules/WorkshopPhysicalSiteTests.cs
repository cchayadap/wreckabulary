using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopPhysicalSiteTests
    {
        static CourseCatalogue Catalogue()
        {
            var source = CourseTemplates.ProofCatalogue();
            return new CourseCatalogue(source.Modules.Values, source.Assets.Values.Concat(new[] {
                new CourseAssetDefinition("TABLE", "TABLE", 1200, 720, 700),
                new CourseAssetDefinition("HAMMER", "HAMMER", 500, 1000, 500),
                new CourseAssetDefinition("CLOCK", "CLOCK", 500, 700, 300)
            }), source.PuzzlePresets, new[] { "Cozy" }, new[] { "FindAddress", "WrongDelivery", "RestoreHome" });
        }
        static WorkshopHistory Create(string family = "TimedMechanism", string recipe = "CLOCK")
        {
            var document = CourseTemplates.RaceCircuit();
            var puzzle = document.Puzzles.Single(p => p.Id == "BedBridge");
            puzzle.PresetId = family; puzzle.RecipeId = recipe; puzzle.RequiredLetters = recipe;
            document.Supplies.Single(s => s.Id == "BedSupply").Letters = recipe;
            var history = new WorkshopHistory(new WorkshopSnapshot(document));
            history.Execute("Create physical controls", draft => WorkshopPhysicalSites.Create(draft, Catalogue(), 1, "BedBridge"));
            history.Select(new[] { "Start" });
            return history;
        }

        [TestCase("CollectLetters", "TABLE")]
        [TestCase("PlaceObject", "TABLE")]
        [TestCase("BuildRoute", "BED")]
        [TestCase("RedirectWind", "FAN")]
        [TestCase("BalanceWeight", "TABLE")]
        [TestCase("TimedMechanism", "CLOCK")]
        public void CreatePersistsEachFamilyAndItsExplicitReferences(string family, string recipe)
        {
            var history = Create(family, recipe); var document = history.Current.Course();
            Assert.AreEqual(2, document.Schema);
            Assert.AreEqual(1, document.PhysicalSites.Count);
            var site = document.PhysicalSites.Single();
            Assert.AreEqual("Begin", site.CheckpointId); Assert.AreEqual("BedSupply", site.RecoverySupplyId);
            var validation = CourseValidator.Validate(document, Catalogue(), MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")));
            Assert.IsTrue(validation.Ok, string.Join("\n", validation.Issues));
            if (family == "RedirectWind")
            {
                var source = document.Zones.Single(z => z.Id == site.WindReceiverZoneId);
                Assert.AreEqual("Fixed", source.Placement);
                Assert.IsTrue(site.ReceiverStartBounds.Contains(source.FixedPosition));
                Assert.Contains(site.ReceiverRecoverySupplyId, document.Puzzles.Single(p => p.Id == site.PuzzleId).RecoverySupplyIds);
            }
            string persisted = history.Current.CourseJsonText;
            Assert.AreEqual(persisted, CourseJson.ExportDocument(CourseJson.ImportDocument(persisted).Document));
            Assert.IsTrue(history.Undo()); Assert.IsEmpty(history.Current.Course().PhysicalSites);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(persisted, history.Current.CourseJsonText);
        }

        [Test]
        public void ModuleMoveRotationAndLayerChangeCarryEveryPhysicalElement()
        {
            var history = Create(); var before = history.Current.Course().PhysicalSites.Single();
            var arm = before.Fixtures.Single();
            history.Execute("Move", draft => WorkshopCommands.Move(draft, new CoursePoint(500, 0, 1000), false));
            var moved = history.Current.Course().PhysicalSites.Single();
            Assert.AreEqual(new CoursePoint(before.Position.X + 500, before.Position.Y, before.Position.Z + 1000), moved.Position);
            Assert.AreEqual(new CoursePoint(arm.From.X + 500, arm.From.Y, arm.From.Z + 1000), moved.Fixtures.Single().From);
            history.Execute("Rotate", draft => WorkshopCommands.RotateSelection(draft, moved.Position, 1));
            var rotated = history.Current.Course().PhysicalSites.Single();
            Assert.AreEqual(90, rotated.Yaw);
            Assert.AreEqual(new CoursePoint(arm.SizeMm.Z, arm.SizeMm.Y, arm.SizeMm.X), rotated.Fixtures.Single().SizeMm);
            var previous = rotated.Fixtures.Single().To;
            history.Execute("Change floor", draft => WorkshopCommands.ChangeLayer(draft, "Loft"));
            var raised = history.Current.Course().PhysicalSites.Single();
            Assert.AreEqual(6000, raised.Position.Y);
            Assert.AreEqual(new CoursePoint(previous.X, previous.Y + 6000, previous.Z), raised.Fixtures.Single().To);
            Assert.AreEqual(rotated.ArrivalBounds.Min.Y + 6000, raised.ArrivalBounds.Min.Y);
        }

        [Test]
        public void DuplicationRemapsReferencesAndDeleteRemovesOnlyOwnedProfiles()
        {
            var history = Create(); string original = history.Current.CourseJsonText;
            history.Execute("Duplicate", draft => WorkshopCommands.Duplicate(draft, new CoursePoint(40000, 0, 0)));
            var copied = history.Current.Course(); Assert.AreEqual(2, copied.PhysicalSites.Count);
            var added = copied.PhysicalSites.Single(s => s.PuzzleId != "BedBridge");
            var puzzle = copied.Puzzles.Single(p => p.Id == added.PuzzleId);
            Assert.AreEqual(puzzle.ModuleId, copied.Checkpoints.Single(c => c.Id == added.CheckpointId).ModuleId);
            Assert.Contains(added.RecoverySupplyId, puzzle.RecoverySupplyIds);
            Assert.AreNotEqual(copied.PhysicalSites.First().Fixtures.Single().Id, added.Fixtures.Single().Id);
            Assert.AreEqual(40000, added.Position.X);
            history.Execute("Delete copied module", WorkshopCommands.Delete);
            Assert.AreEqual(1, history.Current.Course().PhysicalSites.Count);
            Assert.AreEqual("BedBridge", history.Current.Course().PhysicalSites.Single().PuzzleId);
            Assert.IsTrue(history.Undo()); Assert.IsTrue(history.Undo()); Assert.AreEqual(original, history.Current.CourseJsonText);
        }

        [Test]
        public void ReceiverDragPreviewsWithoutMutatingAndCommitsSourceAndSensorTogether()
        {
            var history = Create("RedirectWind", "FAN"); string before = history.Current.CourseJsonText;
            var source = history.Current.Course().PhysicalSites.Single();
            var drag = new WorkshopSiteDrag(history, 1, source.PuzzleId, WorkshopSitePart.ReceiverStart);
            drag.Move(731, -261);
            Assert.AreEqual(before, history.Current.CourseJsonText);
            Assert.AreEqual(source.ReceiverStartBounds.Min.X + 500, drag.Preview.ReceiverStartBounds.Min.X);
            Assert.IsTrue(drag.Commit(history));
            var next = history.Current.Course().PhysicalSites.Single();
            var zone = history.Current.Course().Zones.Single(z => z.Id == next.WindReceiverZoneId);
            Assert.IsTrue(next.ReceiverStartBounds.Contains(zone.FixedPosition));
            Assert.AreEqual(500, zone.FixedPosition.X);
            Assert.AreEqual(400, zone.FixedPosition.Z);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.CourseJsonText);
        }

        [Test]
        public void StaleDragAndLockedLayersCannotEditPhysicalControls()
        {
            var history = Create();
            var drag = new WorkshopSiteDrag(history, 1, "BedBridge", WorkshopSitePart.Socket);
            history.Execute("Rename", draft => draft.Course.Title = "Changed");
            Assert.Throws<InvalidOperationException>(() => drag.Commit(history));
            history.Execute("Lock", draft => draft.LockedLayers.Add("Ground"));
            string before = history.Current.CourseJsonText;
            Assert.Throws<InvalidOperationException>(() => history.Execute("Move", draft =>
                WorkshopPhysicalSites.MovePart(draft, 1, "BedBridge", WorkshopSitePart.Socket, new CoursePoint(500, 0, 0))));
            Assert.AreEqual(before, history.Current.CourseJsonText);
        }

        [Test]
        public void GeometryOnlyRaceHasActionablePhysicalPlaytestGuidance()
        {
            var result = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), Catalogue(),
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"))));
            Assert.IsTrue(result.Success);
            StringAssert.Contains("BedBridge", WorkshopPhysicalSites.PlaytestIssue(result.Manifest));
            StringAssert.Contains("lap 1", WorkshopPhysicalSites.PlaytestIssue(result.Manifest));
        }
    }
}
