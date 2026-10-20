using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundFlagshipTests
    {
        static ItemCatalogue Items()
        {
            string copied = Path.Combine(TestContext.CurrentContext.TestDirectory, "FlagshipCatalogue.json");
            return File.Exists(copied) ? ItemCatalogue.FromJson(File.ReadAllText(copied)) : TestData.Catalogue();
        }
        static WordboundFlagshipContent Content() => WordboundFlagshipContent.Create(Items());
        static CoursePlayRequirements Play(string mode, RaceFormat format = RaceFormat.Solo) =>
            MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
        static ResolvedCourse Resolve(WordboundFlagshipContent content, string mode = "TimeRun", RaceFormat format = RaceFormat.Solo,
            string seed = "0123456789abcdef")
        {
            var result = content.Resolve(seed, Play(mode, format));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
            return result.Manifest;
        }

        [Test]
        public void FourDestinationsContainMeaningfulRoomsSixFamiliesAndAllThreeStories()
        {
            var content = Content(); var d = content.CopyDocument();
            CollectionAssert.AreEqual(new[] { "Packing Yard", "Kitchen Works", "Attic Stacks", "Rooftop Garden" }, content.Destinations.Select(x => x.Name));
            CollectionAssert.AreEqual(new[] { 0, 8000, 16000, 24000 }, content.Destinations.Select(x => x.HeightMm));
            Assert.AreEqual(34, d.Modules.Count); Assert.AreEqual(34, content.Rooms.Count);
            Assert.IsTrue(content.Rooms.All(room => !string.IsNullOrWhiteSpace(room.Purpose)));
            Assert.AreEqual(72, d.Puzzles.Count); Assert.AreEqual(72, content.PuzzleSites.Count);
            Assert.AreEqual(6, d.Puzzles.Select(p => p.PresetId).Distinct().Count());
            Assert.IsTrue(d.Puzzles.All(p => p.MinimumPlayers == 1));
            CollectionAssert.AreEqual(new[] { "FindAddress", "WrongDelivery", "RestoreHome" }, d.Laps.Select(l => l.NarrativeId));
            foreach (int lap in new[] { 1, 2, 3 })
                Assert.GreaterOrEqual(content.Story.Count(beat => beat.Lap == lap && beat.Optional), 4);
            Assert.AreEqual(8, d.Spawns.Count);
        }

        [Test]
        public void EquipmentDimensionsAndMechanismBoundsComeFromActualCatalogue()
        {
            var items = Items(); var content = WordboundFlagshipContent.Create(items);
            foreach (var entry in content.Equipment)
            {
                var source = items.Get(entry.Id); var shape = content.Geometry.Assets[entry.Id];
                Assert.AreEqual((int)Math.Round(source.Size[0] * 1000d, MidpointRounding.AwayFromZero), shape.WidthMm);
                Assert.AreEqual((int)Math.Round(source.Size[1] * 1000d, MidpointRounding.AwayFromZero), shape.HeightMm);
                Assert.AreEqual((int)Math.Round(source.Size[2] * 1000d, MidpointRounding.AwayFromZero), shape.DepthMm);
                Assert.AreEqual(source.Durability, entry.Durability);
            }
            foreach (var site in content.PuzzleSites)
            {
                var source = items.Get(site.RecipeId);
                if (site.PresetId == "BuildRoute")
                {
                    Assert.Less((site.Target.Y - site.Position.Y) / 1000d, source.Deploy.Strength * source.Deploy.Strength / (2 * 9.81));
                    Assert.Greater((site.Target.Z - site.Position.Z) / 1000d, source.Size[2] / 2 + .7);
                }
                if (site.PresetId == "RedirectWind" || site.PresetId == "TimedMechanism")
                    Assert.Less((site.Target.Z - site.Position.Z) / 1000d, source.Deploy.Radius);
                if (site.PresetId == "TimedMechanism") Assert.Less(site.ActiveWindowSeconds, source.Deploy.LifetimeSeconds);
                if (site.PresetId == "CollectLetters")
                {
                    var zone = content.CopyDocument().Zones.Single(z => z.Id == site.PropSourceZoneId);
                    Assert.AreEqual("Props", zone.Purpose); Assert.AreEqual("Fixed", zone.Placement);
                    Assert.AreEqual("TABLE", zone.Pool.Single().AssetId);
                }
            }
        }

        [Test]
        public void PhysicalSitesBindExactLapCheckpointSourcesAndSoloVolumes()
        {
            var content = Content(); var d = content.CopyDocument();
            foreach (var site in content.PuzzleSites)
            {
                var checkpoint = d.Checkpoints.Single(c => c.Id == site.CheckpointId);
                Assert.Contains(site.PuzzleId, checkpoint.PuzzleIds);
                Assert.AreEqual(site.ModuleId, checkpoint.ModuleId);
                Assert.IsTrue(d.Laps.Single(l => l.Number == site.Lap).SectionIds.SelectMany(id =>
                    d.RouteSections.Single(s => s.Id == id).Alternatives).Any(a => a.CheckpointIds.Contains(site.CheckpointId)));
                var supply = d.Supplies.Single(s => s.Id == site.RecoverySupplyId);
                Assert.AreEqual(site.RecipeId, supply.Letters);
                Assert.IsTrue(supply.Protected && supply.Renewable);
                var module = d.Modules.Single(m => m.Id == site.ModuleId);
                var room = new CourseBox(new CoursePoint(module.Position.X - 12000, module.Position.Y, module.Position.Z - 12000),
                    new CoursePoint(module.Position.X + 12000, module.Position.Y + 6000, module.Position.Z + 12000));
                Assert.IsTrue(room.Contains(site.ArrivalBounds.Min) && room.Contains(site.ArrivalBounds.Max));
                Assert.AreEqual(1, site.MinimumPlayers);
                if (site.PresetId == "CollectLetters")
                {
                    var tools = d.Supplies.Single(s => s.Id == site.ToolRecoverySupplyId);
                    Assert.AreEqual(site.ToolRecipeId, tools.Letters);
                    Assert.IsTrue(tools.Protected && tools.Renewable);
                    Assert.Greater(content.Equipment.Single(e => e.Id == site.ToolRecipeId).BreakPower, 1);
                }
                if (site.PresetId == "RedirectWind")
                {
                    var receiver = d.Zones.Single(z => z.Id == site.WindReceiverZoneId);
                    Assert.AreEqual("Props", receiver.Purpose);
                    Assert.AreEqual(site.WindReceiverRecipeId, receiver.Pool.Single().AssetId);
                    Assert.IsTrue(site.ReceiverStartBounds.Contains(receiver.FixedPosition));
                    Assert.IsFalse(site.ArrivalBounds.Contains(receiver.FixedPosition));
                    var recovery = d.Supplies.Single(s => s.Id == site.ReceiverRecoverySupplyId);
                    Assert.AreEqual(site.WindReceiverRecipeId, recovery.Letters);
                    Assert.IsTrue(recovery.Protected && recovery.Renewable);
                }
                if (site.PresetId == "BalanceWeight")
                {
                    var item = content.Equipment.Single(e => e.Id == site.RecipeId);
                    Assert.AreEqual(2, site.FloorPadBounds.Count);
                    foreach (var pad in site.FloorPadBounds)
                    {
                        Assert.GreaterOrEqual(pad.Min.X, site.Position.X - item.SizeMm.X / 2);
                        Assert.LessOrEqual(pad.Max.X, site.Position.X + item.SizeMm.X / 2);
                        Assert.GreaterOrEqual(pad.Min.Z, site.Position.Z - item.SizeMm.Z / 2);
                        Assert.LessOrEqual(pad.Max.Z, site.Position.Z + item.SizeMm.Z / 2);
                    }
                }
            }
            Assert.AreEqual(12, content.PuzzleSites.Select(s => s.PropSourceZoneId).Where(id => id.Length > 0).Distinct().Count());
            Assert.AreEqual(12, content.PuzzleSites.Select(s => s.WindReceiverZoneId).Where(id => id.Length > 0).Distinct().Count());
        }

        [Test]
        public void FactoryFreezesCallerDataAndBindsRelevantStatsIntoHash()
        {
            var items = Items(); var content = WordboundFlagshipContent.Create(items);
            string originalHash = content.Hash, originalJson = CourseJson.ExportDocument(content.CopyDocument());
            var leaked = content.CopyDocument(); leaked.Modules.Clear(); leaked.Title = "Changed";
            items.Get("BAT").Melee.Reach += .1f;
            Assert.AreEqual(originalJson, CourseJson.ExportDocument(content.CopyDocument()));
            Assert.AreEqual(originalHash, content.Hash);
            Assert.AreNotEqual(content.Hash, WordboundFlagshipContent.Create(items).Hash);
        }

        [Test]
        public void MissingRequiredEffectFailsBeforeAPlayableCourseIsProduced()
        {
            var items = Items(); items.Get("FAN").Deploy = null;
            Assert.Catch(() => WordboundFlagshipContent.Create(items));
        }

        [Test]
        public void RequiredCheckpointCentresStayBeyondRampApproachesAndLandings()
        {
            var d = Content().CopyDocument();
            foreach (var checkpoint in d.Checkpoints)
                Assert.AreEqual(d.Modules.Single(m => m.Id == checkpoint.ModuleId).Position,
                    d.RecoveryAnchors.Single(a => a.Id == checkpoint.RecoveryAnchorId).Position);
            foreach (var rise in d.Connections.Where(c => c.Kind == "Stairs"))
            {
                var from = d.Modules.Single(m => m.Id == rise.FromModuleId).Position;
                var to = d.Modules.Single(m => m.Id == rise.ToModuleId).Position;
                long direction = rise.To.Z - rise.From.Z;
                Assert.Greater((rise.From.Z - from.Z) * direction, 0);
                Assert.Greater((to.Z - rise.To.Z) * direction, 0);
                Assert.LessOrEqual(Math.Abs((double)(rise.To.Y - rise.From.Y) / direction), Math.Tan(35 * Math.PI / 180));
            }
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        public void ActualResolvedRoutesRetainRequiredLengthAndEvolvingLaps(string mode, RaceFormat format)
        {
            var content = Content(); var course = Resolve(content, mode, format);
            var measures = WordboundFlagshipRoutes.Measure(course);
            Assert.AreEqual(3, measures.Count);
            Assert.That(measures[0].AuthoredMetres, Is.InRange(751d, 752d));
            Assert.That(measures[2].AuthoredMetres, Is.InRange(799d, 800d));
            foreach (var measure in measures)
            {
                Assert.AreEqual(measure.AuthoredMetres, measure.ShortestOrderedCheckpointMetres, .000001);
                Assert.GreaterOrEqual(measure.RequiredRoomVisits, 32); Assert.AreEqual(3, measure.VerticalConnections);
            }
            Assert.AreEqual(course.Laps[0].FinishModuleId, course.Laps[1].StartModuleId);
            Assert.AreEqual(course.Laps[1].FinishModuleId, course.Laps[2].StartModuleId);
            Assert.IsTrue(course.Laps[0].AlternativeIds.Contains("L1_Packing_Main"));
            Assert.IsTrue(course.Laps[2].AlternativeIds.Contains("L3_Packing_Repair"));
            Assert.IsTrue(course.Laps[1].ConnectionIds.First().StartsWith("Rooftop_", StringComparison.Ordinal));
            Assert.IsTrue(content.CopyDocument().Supplies.All(s => s.Protected && s.Renewable));
            string encoded = CourseJson.ExportManifest(course);
            var imported = CourseJson.ImportManifest(encoded, content.Geometry);
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            Assert.AreEqual(course.Hash, imported.Manifest.Hash);
        }

        [Test]
        public void MissingMandatoryRoomCheckpointCannotPassTheLengthAudit()
        {
            var content = Content(); var d = content.CopyDocument();
            d.RouteSections.Single(s => s.Id == "L1_Packing").Alternatives.Single().CheckpointIds.RemoveAt(0);
            var resolved = CourseResolver.Resolve(d, content.Geometry, new CourseResolveRequest("0123456789abcdef", Play("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.Throws<InvalidOperationException>(() => WordboundFlagshipRoutes.Measure(resolved.Manifest));
        }

        [Test]
        public void MovedCheckpointCannotReuseTheOriginalCentreDistanceClaim()
        {
            var content = Content(); var d = content.CopyDocument();
            var anchor = d.RecoveryAnchors.First();
            anchor.Position = new CoursePoint(anchor.Position.X + 2000, anchor.Position.Y, anchor.Position.Z);
            var resolved = CourseResolver.Resolve(d, content.Geometry, new CourseResolveRequest("0123456789abcdef", Play("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.Throws<InvalidOperationException>(() => WordboundFlagshipRoutes.Measure(resolved.Manifest));
        }

        [Test]
        public void GeometricallyShortenedCourseCannotPassUsingTheSameLabels()
        {
            var content = Content(); var d = content.CopyDocument();
            CoursePoint Half(CoursePoint p) => new CoursePoint(p.X / 2, p.Y, p.Z / 2);
            foreach (var module in d.Modules) module.Position = Half(module.Position);
            foreach (var connection in d.Connections)
            {
                connection.From = Half(connection.From); connection.To = Half(connection.To);
                if (connection.Kind == "Stairs")
                {
                    connection.From = new CoursePoint(connection.From.X, connection.From.Y, 6000);
                    connection.To = new CoursePoint(connection.To.X, connection.To.Y, -6000);
                }
            }
            foreach (var spawn in d.Spawns) spawn.Position = Half(spawn.Position);
            foreach (var recovery in d.RecoveryAnchors) recovery.Position = Half(recovery.Position);
            foreach (var supply in d.Supplies) supply.Position = Half(supply.Position);
            d.Zones.Clear(); d.ReservedVolumes.Clear();
            var geometry = new CourseCatalogue(new[] { new CourseModuleDefinition("FlagshipRoom", 12000, 12000, 6000) },
                content.Geometry.Assets.Values, content.Geometry.PuzzlePresets, content.Geometry.Themes, content.Geometry.NarrativeEvents);
            var resolved = CourseResolver.Resolve(d, geometry, new CourseResolveRequest("0123456789abcdef", Play("TimeRun")));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            Assert.Throws<InvalidOperationException>(() => WordboundFlagshipRoutes.Measure(resolved.Manifest));
        }

        [Test]
        public void FreestyleKeepsTheWholeReachableWorldWithoutClaimingRaceLaps()
        {
            var course = Resolve(Content(), "Freestyle");
            Assert.AreEqual(34, course.Geometry.Count); Assert.AreEqual(0, course.Laps.Count);
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        [Category("FlagshipSeeds")]
        public void OneThousandSeedsPerSupportedConfiguration(string mode, RaceFormat format)
        {
            var content = Content(); var play = Play(mode, format);
            bool mainSeen = false, repairSeen = false;
            double min = double.PositiveInfinity, max = 0;
            for (int i = 0; i < 1000; i++)
            {
                string seed = i.ToString("x16", CultureInfo.InvariantCulture);
                var course = Resolve(content, mode, format, seed);
                if (course.Laps.Count > 0)
                {
                    mainSeen |= course.Laps[1].AlternativeIds.Contains("L2_Packing_Main");
                    repairSeen |= course.Laps[1].AlternativeIds.Contains("L2_Packing_Repair");
                    foreach (var measure in WordboundFlagshipRoutes.Measure(course))
                    { min = Math.Min(min, measure.ShortestOrderedCheckpointMetres); max = Math.Max(max, measure.AuthoredMetres); }
                }
                if (i % 31 == 0 || i == 999)
                    Assert.AreEqual(course.Hash, Resolve(content, mode, format, seed).Hash, "Seed replay " + seed);
                if (play.Ownership == CourseOwnershipPolicy.TeamIsolated && play.TeamCount == 2)
                {
                    var first = course.Placements.Where(p => p.TeamIndex == 0).Select(p => (p.ZoneId, p.AssetId, p.Position.X, p.Position.Y, p.Position.Z, p.Yaw));
                    var second = course.Placements.Where(p => p.TeamIndex == 1).Select(p => (p.ZoneId, p.AssetId, p.Position.X, p.Position.Y, p.Position.Z, p.Yaw));
                    CollectionAssert.AreEqual(first, second, "Equal team opportunities for " + seed);
                }
            }
            if (play.Capability == CourseCapability.Circuit) Assert.IsTrue(mainSeen && repairSeen);
            TestContext.WriteLine(mode + "/" + format + ": 1000 seeds" +
                (play.Capability == CourseCapability.Circuit ? "; required-route metres " + min + "–" + max : "; complete reachable world"));
        }
    }
}
