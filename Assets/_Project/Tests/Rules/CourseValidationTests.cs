using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public class CourseValidationTests
    {
        [TestCase("null-reference"), TestCase("duplicate-id"), TestCase("missing-layer"), TestCase("overlap")]
        [TestCase("unknown-asset"), TestCase("cycle"), TestCase("multiplayer-puzzle"), TestCase("insufficient-starts")]
        [TestCase("steep-stairs"), TestCase("bad-landing"), TestCase("bad-door-edge"), TestCase("broken-route"), TestCase("missing-lap"), TestCase("zero-height-reserve")]
        public void InvalidAuthoringReturnsIssuesWithoutThrowing(string mutation)
        {
            var d = CourseTemplates.RaceCircuit();
            switch (mutation)
            {
                case "null-reference": d.Modules[0].LayerId = null; break;
                case "duplicate-id": d.Modules[1].Id = d.Modules[0].Id; break;
                case "missing-layer": d.Modules[0].LayerId = "Missing"; break;
                case "overlap": d.Modules[2].Position = d.Modules[1].Position; break;
                case "unknown-asset": d.Zones[0].Pool[0].AssetId = "ExternalURL"; break;
                case "cycle": d.Puzzles[0].PrerequisiteIds.Add(d.Puzzles[1].Id); break;
                case "multiplayer-puzzle": d.Puzzles[0].MinimumPlayers = 2; break;
                case "insufficient-starts": d.Spawns.RemoveAt(0); break;
                case "steep-stairs": d.Connections[0].To = new CoursePoint(0, 6000, -3000); break;
                case "bad-landing": d.Connections[0].From = new CoursePoint(-9000, 0, -3000); d.Connections[0].To = new CoursePoint(9000, 6000, -3000); break;
                case "bad-door-edge": d.Connections[3].From = d.Connections[3].To = new CoursePoint(10000, 6000, 9900); break;
                case "broken-route": d.RouteSections[0].Alternatives[0].ConnectionIds.Reverse(); break;
                case "missing-lap": d.Laps.RemoveAt(2); break;
                case "zero-height-reserve": d.ReservedVolumes.Add(new CourseReservedVolume { Id = "Zero", Bounds = new CourseBox(new CoursePoint(0, 0, 0), new CoursePoint(1000, 0, 1000)) }); break;
            }
            var result = CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest("0000000000000000", CourseCoreTests.Play("TeamRace")));
            Assert.IsFalse(result.Success, mutation); Assert.IsNotEmpty(result.Issues); Assert.IsNull(result.Manifest);
        }

        [TestCase("unprotected"), TestCase("not-renewable"), TestCase("own-gate"), TestCase("missing-multiplicity")]
        public void ResourcesCannotBecomeUnrecoverableAtAProgressionPrefix(string mutation)
        {
            var d = CourseTemplates.RaceCircuit();
            switch (mutation)
            {
                case "unprotected": d.Supplies[0].Protected = false; break;
                case "not-renewable": d.Supplies[0].Renewable = false; break;
                case "own-gate": d.Supplies[0].AfterPuzzleIds.Add("BedBridge"); break;
                case "missing-multiplicity": d.Puzzles[0].RecipeId = ""; d.Puzzles[0].RequiredLetters = "BBED"; break;
            }
            var result = CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest("0000000000000000", CourseCoreTests.Play("BrawlRace")));
            Assert.IsFalse(result.Success); Assert.IsNull(result.Manifest);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "resource.recovery"), CourseCoreTests.Errors(result.Issues));
            Assert.AreEqual(16, result.AttemptsUsed);
        }

        [Test]
        public void DensityFailureIsBoundedAndNeverDropsRequiredItems()
        {
            var d = CourseTemplates.FreestyleWorld();
            var zone = d.Zones.Single(z => z.Placement == "Fixed");
            zone.FixedPosition = new CoursePoint(0, 0, -6500);
            zone.Bounds = new CourseBox(new CoursePoint(-1000, 0, -7500), new CoursePoint(1000, 0, -5500));
            var result = CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest("0000000000000001", CourseCoreTests.Play("Freestyle")));
            Assert.IsFalse(result.Success); Assert.IsNull(result.Manifest);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "placement.exhausted"));
            Assert.AreEqual(0, result.AttemptsUsed);
        }

        [Test]
        public void ExpectedMatchProjectionAndCatalogueAreRequired()
        {
            var cat = CourseTemplates.ProofCatalogue();
            var result = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), cat,
                new CourseResolveRequest("0000000000000001", CourseCoreTests.Play("TeamRace", 1)));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            Assert.IsFalse(CourseValidator.ValidateResolved(result.Manifest, cat, CourseCoreTests.Play("TeamRace", 4)).Ok);
            var changedCat = new CourseCatalogue(cat.Modules.Values, cat.Assets.Values, cat.PuzzlePresets, cat.Themes, cat.NarrativeEvents.Concat(new[] { "NewStory" }));
            Assert.IsFalse(CourseJson.ImportManifest(CourseJson.ExportManifest(result.Manifest), changedCat).Success);
        }

        [Test]
        public void RecoveryLettersInAOneWayDeadEndCannotUnlockTheirOwnReturnGate()
        {
            var d = CourseTemplates.RaceCircuit();
            d.Modules.Add(new CourseModule { Id = "Depot", DefinitionId = "Room", LayerId = "Ground", Position = new CoursePoint(-20000, 0, 0) });
            d.Connections.Add(new CourseConnection {
                Id = "DepotEntry", FromModuleId = "Start", ToModuleId = "Depot", Bidirectional = false,
                From = new CoursePoint(-10000, 0, 0), To = new CoursePoint(-10000, 0, 0)
            });
            d.Connections.Add(new CourseConnection {
                Id = "DepotExit", FromModuleId = "Depot", ToModuleId = "Start", Bidirectional = false, GatePuzzleId = "BedBridge",
                From = new CoursePoint(-10000, 0, 3000), To = new CoursePoint(-10000, 0, 3000)
            });
            d.Supplies[0].ModuleId = "Depot"; d.Supplies[0].Position = new CoursePoint(-20000, 0, 0);
            var cat = CourseTemplates.ProofCatalogue(); var play = CourseCoreTests.Play("TimeRun");
            Assert.IsTrue(CourseValidator.Validate(d, cat, play).Ok);
            var result = CourseResolver.Resolve(d, cat, new CourseResolveRequest("0000000000000000", play));
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Issues.Any(i => i.Code == "resource.recovery"), CourseCoreTests.Errors(result.Issues));
        }

        [Test]
        public void MaximumModuleCountHasAConnectedBoundedProofAndOneExtraIsRejected()
        {
            var d = CourseTemplates.FreestyleWorld();
            d.Modules.RemoveAt(1); d.Connections.Clear(); d.Zones.Clear();
            string prior = "Start"; var previous = new CoursePoint(0, 0, 0);
            for (int i = 1; i < CourseLimits.Modules; i++)
            {
                int row = i / 16, column = row % 2 == 0 ? i % 16 : 15 - i % 16;
                var position = new CoursePoint(column * 20000, 0, row * 20000);
                string id = "Room" + i;
                d.Modules.Add(new CourseModule { Id = id, DefinitionId = "Room", LayerId = "Ground", Position = position });
                var door = new CoursePoint((previous.X + position.X) / 2, 0, (previous.Z + position.Z) / 2);
                d.Connections.Add(new CourseConnection { Id = "Door" + i, FromModuleId = prior, ToModuleId = id, From = door, To = door });
                prior = id; previous = position;
            }
            var cat = CourseTemplates.ProofCatalogue(); var play = CourseCoreTests.Play("Freestyle");
            var result = CourseResolver.Resolve(d, cat, new CourseResolveRequest("0000000000000000", play));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            Assert.AreEqual(256, result.Manifest.Geometry.Count);
            var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(result.Manifest), cat);
            Assert.IsTrue(imported.Success, CourseCoreTests.Errors(imported.Issues));
            d.Modules.Add(new CourseModule { Id = "Excess", DefinitionId = "Room", LayerId = "Ground" });
            Assert.IsFalse(CourseValidator.ValidateStructure(d).Ok);
        }

        [TestCase("nothex"), TestCase("0123456789ABCDEF"), TestCase("00000000000000000")]
        public void SeedIsExactlyLowercaseHex64(string seed)
        {
            var result = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest(seed, CourseCoreTests.Play("TimeRun")));
            Assert.IsFalse(result.Success); Assert.AreEqual(0, result.AttemptsUsed);
        }
    }
}
