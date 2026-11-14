using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopStarterTests
    {
        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        public void FreshStarterResolvesAllOfferedFormatsWithOrderedPhysicalLaps(string mode, RaceFormat format)
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var course = preset.Snapshot.Course();
            var play = WorkshopModes.Select(course, mode, format).Requirements;
            var resolved = CourseResolver.Resolve(course, preset.Catalogue, new CourseResolveRequest(preset.Snapshot.Seed, play));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.AreEqual("", WorkshopPhysicalSites.PlaytestIssue(resolved.Manifest));
            Assert.AreEqual(3, resolved.Manifest.Laps.Count);
            Assert.AreEqual(3, resolved.Manifest.PhysicalSites.Count);
            Assert.AreEqual(1, course.Puzzles.Count);
            var puzzle = course.Puzzles.Single();
            Assert.AreEqual("PlaceObject", puzzle.PresetId);
            Assert.AreEqual("STOOL", puzzle.RecipeId);
            Assert.IsTrue(puzzle.ConsumeLetters);
            Assert.AreEqual(1, puzzle.MinimumPlayers);
            var supply = course.Supplies.Single();
            Assert.IsTrue(supply.Protected && supply.Renewable);
            Assert.AreEqual(puzzle.RequiredLetters, supply.Letters);
            Assert.Contains(supply.Id, puzzle.RecoverySupplyIds);
            Assert.GreaterOrEqual(course.Spawns.Count, play.MaxOccupants);
            foreach (var lap in resolved.Manifest.Laps)
            {
                CollectionAssert.AreEqual(new[] { "StandReady", "Delivery" }, lap.Checkpoints.Select(c => c.Id));
                var site = resolved.Manifest.PhysicalSites.Single(s => s.Lap == lap.Number);
                Assert.AreEqual(lap.Checkpoints[0].Id, site.CheckpointId);
                Assert.Contains(site.PuzzleId, lap.Checkpoints[0].PuzzleIds.ToArray());
                Assert.AreEqual(supply.Id, site.RecoverySupplyId);
                Assert.Greater(site.HoldTicks, 0);
                Assert.AreNotEqual(course.RecoveryAnchors[0].Position, course.RecoveryAnchors[1].Position);
                Assert.IsTrue(preset.Catalogue.Bounds(course.Modules.Single()).Contains(site.Position, site.SocketRadiusMm));
            }
            string wire = CourseJson.ExportManifest(resolved.Manifest);
            var imported = CourseJson.ImportManifest(wire, preset.Catalogue);
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            Assert.AreEqual(wire, CourseJson.ExportManifest(imported.Manifest));
        }

        [Test]
        public void StarterUsesExactLocalRulesAndDoesNotChangeTheGeometryProof()
        {
            var legacy = CourseTemplates.RaceCircuit();
            string before = CourseJson.ExportDocument(legacy);
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            Assert.IsTrue(WorkshopModes.AllowsOneHumanLocal(preset.Play));
            Assert.AreEqual("TimeRun", preset.Play.ModeId);
            Assert.AreEqual(4, preset.Play.TeamCapacity);
            Assert.AreEqual(CourseOwnershipPolicy.TeamIsolated, preset.Play.Ownership);
            Assert.AreEqual(legacy.Id, preset.Snapshot.Course().Id, "Retain the existing autosave lookup identity.");
            Assert.AreEqual(before, CourseJson.ExportDocument(CourseTemplates.RaceCircuit()));
            Assert.IsEmpty(legacy.PhysicalSites);
            Assert.AreEqual(2, legacy.Puzzles.Count);
            Assert.AreEqual(3, legacy.Modules.Count);
            var old = CourseResolver.Resolve(legacy, preset.Catalogue, new CourseResolveRequest(preset.Snapshot.Seed, preset.Play));
            Assert.IsTrue(old.Success);
            StringAssert.Contains("BedBridge", WorkshopPhysicalSites.PlaytestIssue(old.Manifest));
        }

        [Test]
        public void EditedStarterRoundTripsWithoutLosingPhysicalBindingsOrHistory()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var history = new WorkshopHistory(preset.Snapshot);
            string before = history.Current.CourseJsonText;
            history.Select(new[] { "Start" });
            history.Execute("Move starter", d => WorkshopCommands.Move(d, new CoursePoint(2000, 0, 1000)));
            string after = history.Current.CourseJsonText;
            Assert.AreNotEqual(before, after);
            var imported = CourseJson.ImportDocument(after);
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            Assert.AreEqual(after, CourseJson.ExportDocument(imported.Document));
            var resolved = CourseResolver.Resolve(imported.Document, preset.Catalogue,
                new CourseResolveRequest(history.Current.Seed, preset.Play));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.AreEqual("", WorkshopPhysicalSites.PlaytestIssue(resolved.Manifest));
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.CourseJsonText);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(after, history.Current.CourseJsonText);
        }
    }
}
