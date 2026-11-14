using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundParcelServiceContentTests
    {
        static readonly string[] MainAreas = { "Yard", "Counter", "Dock", "Hall", "Lockers", "Mezzanine", "Garage" };

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void DepotResolvesDeterministicallyWithClearRoutesAndASeededParcelBay(string mode)
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var play = MatchStateValidation.Requirements(ModeDefinition.For(mode));
            string firstBay = null; bool varied = false;
            for (int seed = 1; seed <= 128; seed++)
            {
                var result = content.Resolve(seed.ToString("x16"), play);
                Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(i => i.EntityId + ": " + i.Message)));
                var course = result.Manifest;
                Assert.AreEqual(10, course.Geometry.Count); Assert.AreEqual(16, course.Connections.Count);
                Assert.AreEqual(8, course.Spawns.Count); Assert.AreEqual(WordboundParcelServiceContent.FurnitureCount, course.Placements.Count);
                Assert.IsTrue(course.Placements.All(p => p.Purpose == "Props"));
                Assert.AreEqual(WordboundParcelServiceContent.FurnitureLetters, course.Placements.Sum(p => content.Geometry.Assets[p.AssetId].Letters.Length));
                Assert.AreEqual(WordboundParcelServiceContent.PhysicalSiteCount, course.PhysicalSites.Count);
                Assert.AreEqual(course.Hash, content.Resolve(seed.ToString("x16"), play).Manifest.Hash);
                var roundTrip = CourseJson.ImportManifest(CourseJson.ExportManifest(course), content.Geometry);
                Assert.IsTrue(roundTrip.Success); Assert.AreEqual(course.Hash, roundTrip.Manifest.Hash);
                var bay = course.Placements.Where(p => p.ZoneId == "SortingParcelBay").ToArray();
                Assert.AreEqual(3, bay.Length);
                string positions = string.Join("|", bay.Select(p => p.Position.X + "," + p.Position.Z + "," + p.Yaw));
                if (firstBay == null) firstBay = positions; else varied |= positions != firstBay;
                if (mode == "TimeRun")
                {
                    Assert.AreEqual(3, course.Laps.Count);
                    Assert.AreEqual("Yard", course.Laps[0].StartModuleId);
                    Assert.AreEqual("Garage", course.Laps[0].FinishModuleId);
                    Assert.AreEqual("Yard", course.Laps[1].FinishModuleId);
                    Assert.AreEqual(course.Laps[0].FinishModuleId, course.Laps[1].StartModuleId);
                    Assert.AreEqual(course.Laps[1].FinishModuleId, course.Laps[2].StartModuleId);
                }
                else Assert.AreEqual(0, course.Laps.Count);
            }
            Assert.IsTrue(varied, "Optional parcel positions must actually vary between sample seeds.");
        }

        [Test]
        public void TimeTrialAlternatesBaysAndConveyorsAndClimbsTheMezzanineOnceEveryRun()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var play = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int seed = 1; seed <= 128; seed++)
            {
                var course = content.Resolve(seed.ToString("x16"), play).Manifest;
                var outbound = new[] { course.Laps[0], course.Laps[2] };
                for (int section = 0; section < 3; section++)
                    Assert.AreNotEqual(outbound[0].AlternativeIds[section], outbound[1].AlternativeIds[section], "Lap three must take the other outbound route.");
                Assert.AreEqual(1, outbound.Count(lap => lap.AlternativeIds.Contains("DispatchMezzanine")));
                Assert.AreEqual(1, outbound.Count(lap => lap.Checkpoints.Any(c => c.ModuleId == "Mezzanine")));
                Assert.AreEqual(9, course.Laps.Sum(lap => lap.Checkpoints.Sum(c => c.PuzzleIds.Count)));
                Assert.AreEqual(new[] { "Yard", "Dock", "Hall" }, outbound[0].Checkpoints.Take(3).Select(c => c.ModuleId).ToArray());
                Assert.AreEqual("Garage", course.Laps[1].Checkpoints[0].ModuleId);
                Assert.AreEqual("Counter", course.Laps[1].Checkpoints[course.Laps[1].Checkpoints.Count - 2].ModuleId);
                foreach (var lap in course.Laps) seen.UnionWith(lap.AlternativeIds);
            }
            CollectionAssert.IsSubsetOf(new[] { "IntakeNorth", "IntakeSouth", "SortNorth", "SortSouth", "DispatchFloor",
                "DispatchMezzanine", "ReturnHall", "ReturnLockers", "HomeGate" }, seen);
        }

        [Test]
        public void DepotIsAboutSeventyTwoByFortyMetresWithAMezzanineAndTwoRoutesBetweenAreas()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument();
            var bounds = document.Modules.Select(m => content.Geometry.Bounds(m)).ToArray();
            Assert.AreEqual(72000, bounds.Max(b => b.Max.X) - bounds.Min(b => b.Min.X));
            Assert.AreEqual(40000, bounds.Max(b => b.Max.Z) - bounds.Min(b => b.Min.Z));
            foreach (var module in content.Geometry.Modules.Values)
                Assert.IsTrue(module.WidthMm >= 1000 && module.DepthMm >= 1000 && module.WidthMm <= 100000 && module.DepthMm <= 100000);
            var mezzanine = document.Modules.Single(m => m.Id == "Mezzanine");
            Assert.AreEqual(WordboundParcelServiceContent.MezzanineHeightMm, mezzanine.Position.Y);
            Assert.AreEqual(1, document.Modules.Count(m => m.Position.Y > 0));
            Assert.IsTrue(document.Connections.All(c => c.WidthMm >= 900 && c.WidthMm <= 10000));
            var stairs = document.Connections.Where(c => c.Kind == "Stairs").ToArray();
            Assert.AreEqual(2, stairs.Length);
            foreach (var stair in stairs)
            {
                Assert.AreEqual("Mezzanine", stair.ToModuleId);
                double run = Math.Abs(stair.To.X - stair.From.X) + Math.Abs(stair.To.Z - stair.From.Z);
                Assert.LessOrEqual(Math.Atan2(stair.To.Y - stair.From.Y, run) * 180 / Math.PI, 30.0);
            }
            foreach (string area in MainAreas)
            {
                Assert.IsTrue(document.RecoveryAnchors.Any(a => a.ModuleId == area), area + " needs a recovery anchor.");
                Assert.IsTrue(document.Checkpoints.Any(c => c.ModuleId == area), area + " needs a checkpoint.");
            }
            var main = document.Connections.Where(c => MainAreas.Contains(c.FromModuleId) && MainAreas.Contains(c.ToModuleId)).ToArray();
            foreach (var removed in main)
            {
                var graph = main.Where(c => c != removed).ToArray();
                var seen = new HashSet<string>(StringComparer.Ordinal) { "Yard" };
                for (bool grew = true; grew;)
                {
                    grew = false;
                    foreach (var c in graph)
                        if (seen.Contains(c.FromModuleId) != seen.Contains(c.ToModuleId)) { seen.Add(c.FromModuleId); seen.Add(c.ToModuleId); grew = true; }
                }
                CollectionAssert.AreEquivalent(MainAreas, seen, "Closing " + removed.Id + " must leave a second route.");
            }
        }

        [Test]
        public void InitialSoloSpawnHasRearCameraRoomAndPreservesTheOtherSevenSeats()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument();
            var start = document.Spawns.Single(spawn => spawn.Id == "Start0");
            var clear = WordboundParcelServiceContent.StartSeat; var rear = WordboundParcelServiceContent.RearStartSeat;
            Assert.AreEqual("Yard", start.ModuleId); Assert.AreEqual(0, start.Yaw);
            Assert.AreEqual(clear, start.Position);
            Assert.IsTrue(document.ReservedVolumes.Single(volume => volume.Id == "Route_Yard").Bounds.Contains(start.Position, 300));
            for (int slot = 1; slot < 8; slot++)
                Assert.AreEqual(new CoursePoint(clear.X + slot % 4 * 1600, 0, slot < 4 ? rear.Z : -1200),
                    document.Spawns.Single(spawn => spawn.Id == "Start" + slot).Position);
            Assert.IsFalse(document.Spawns.Any(spawn => spawn.Position.Equals(rear)), "The rear-row seat stays free for Start0's camera.");
            var room = content.Geometry.Bounds(document.Modules.Single(module => module.Id == "Yard"));
            Assert.IsTrue(room.Contains(new CoursePoint(clear.X, 1000, clear.Z - 3300), 250),
                "The default rear camera endpoint plus probe/skin margin remains inside the yard.");
            var corridor = new CourseBox(new CoursePoint(clear.X - 350, 0, clear.Z - 3550), new CoursePoint(clear.X + 350, 2200, clear.Z + 350));
            Assert.IsFalse(document.Zones.Any(zone => zone.Bounds.Intersects(corridor)), "No authored prop bay may cross initial player/camera space.");
            Assert.IsFalse(document.Supplies.Any(supply => corridor.Contains(supply.Position)));
            Assert.IsFalse(document.PhysicalSites.Any(site => corridor.Contains(site.Position)));
            Assert.IsFalse(document.RecoveryAnchors.Any(anchor => corridor.Contains(anchor.Position)));
        }

        [Test]
        public void EveryRouteStopAndDoorwayJoinsAClearAisle()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument();
            var modules = document.Modules.ToDictionary(m => m.Id, StringComparer.Ordinal);
            CourseBox Reserve(string id) => document.ReservedVolumes.Single(v => v.Id == id).Bounds;
            void OnAisle(string module, CoursePoint point, string label) =>
                Assert.IsTrue(Reserve("Route_" + module).Contains(point), label + " must sit on the " + module + " aisle.");
            foreach (var supply in document.Supplies.Where(s => MainAreas.Contains(s.ModuleId))) OnAisle(supply.ModuleId, supply.Position, supply.Id);
            foreach (var anchor in document.RecoveryAnchors) OnAisle(anchor.ModuleId, anchor.Position, anchor.Id);
            foreach (var site in document.PhysicalSites)
            {
                var module = document.Puzzles.Single(p => p.Id == site.PuzzleId).ModuleId;
                OnAisle(module, new CoursePoint(site.Position.X, site.Position.Y, site.Position.Z - 1500), site.PuzzleId + " approach");
                Assert.IsTrue(document.ReservedVolumes.Any(v => v.Id == "Socket_" + site.PuzzleId && v.Bounds.Contains(site.Position)));
            }
            var table = document.Zones.Single(z => z.Id == "SortingBenchProp");
            OnAisle("Hall", new CoursePoint(table.FixedPosition.X, 0, table.FixedPosition.Z - 1150), "Map table approach");
            foreach (var connection in document.Connections)
                foreach (string side in new[] { connection.FromModuleId, connection.ToModuleId }.Where(MainAreas.Contains))
                {
                    var module = modules[side]; var room = content.Geometry.Bounds(module);
                    bool stairs = connection.Kind == "Stairs";
                    var at = side == connection.FromModuleId ? connection.From : connection.To;
                    var other = side == connection.FromModuleId ? connection.To : connection.From;
                    bool alongX = stairs ? other.Z != at.Z : at.X == room.Min.X || at.X == room.Max.X;
                    int sign = stairs ? -Math.Sign(alongX ? other.Z - at.Z : other.X - at.X) :
                        alongX ? (at.X == room.Min.X ? 1 : -1) : (at.Z == room.Min.Z ? 1 : -1);
                    var entry = stairs ? (alongX ? new CoursePoint(at.X, at.Y, at.Z + sign * 1500) : new CoursePoint(at.X + sign * 1500, at.Y, at.Z)) :
                        alongX ? new CoursePoint(at.X + sign * 1500, at.Y, at.Z) : new CoursePoint(at.X, at.Y, at.Z + sign * 1500);
                    var junction = alongX ? new CoursePoint(module.Position.X, at.Y, entry.Z) : new CoursePoint(entry.X, at.Y, module.Position.Z);
                    Assert.IsTrue(document.ReservedVolumes.Any(v => v.Bounds.Contains(entry) && v.Bounds.Contains(junction)),
                        connection.Id + " needs a clear lane from " + side + " to its aisle.");
                    Assert.IsTrue(Reserve("Route_" + side).Contains(junction) || Reserve("Cross_" + side).Contains(junction),
                        connection.Id + " lane must meet the " + side + " aisles.");
                }
        }

        [Test]
        public void EveryAreaHasItsOwnLandmarkFurnitureAndEveryStopKeepsAClearApproach()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument();
            var props = document.Zones.Where(z => z.Placement == "Fixed").ToArray();
            var landmarks = new Dictionary<string, string[]>(StringComparer.Ordinal) {
                ["Yard"] = new[] { "PLANT", "BARREL", "CRATE", "BOX" }, ["Dock"] = new[] { "CRATE", "BOX", "CHEST" },
                ["Hall"] = new[] { "TABLE", "SHELF", "BOX" }, ["Counter"] = new[] { "DESK", "CHAIR", "CLOCK", "PLANT" },
                ["Lockers"] = new[] { "CABINET", "STOOL", "SHELF" }, ["Garage"] = new[] { "BARREL", "CRATE", "TABLE" },
                ["Mezzanine"] = new[] { "DESK", "CHAIR", "SOFA", "PLANT" } };
            foreach (string area in MainAreas)
            {
                var furniture = props.Where(z => z.ModuleId == area).Select(z => z.Pool.Single().AssetId).ToArray();
                Assert.GreaterOrEqual(furniture.Length, 9, area + " needs enough furniture to read as its own area.");
                CollectionAssert.IsSubsetOf(landmarks[area], furniture, area + " landmark furniture.");
            }
            CourseBox Footprint(CourseZone zone)
            {
                var asset = content.Geometry.Assets[zone.Pool.Single().AssetId]; bool sideways = zone.FixedYaw % 180 != 0;
                int x = (sideways ? asset.DepthMm : asset.WidthMm) / 2, z = (sideways ? asset.WidthMm : asset.DepthMm) / 2;
                var at = zone.FixedPosition;
                return new CourseBox(new CoursePoint(at.X - x, at.Y, at.Z - z), new CoursePoint(at.X + x, at.Y + asset.HeightMm, at.Z + z));
            }
            void Clear(string label, CoursePoint at, int west, int south, int east, int north)
            {
                var approach = new CourseBox(new CoursePoint(at.X - west, at.Y, at.Z - south), new CoursePoint(at.X + east, at.Y + 2000, at.Z + north));
                Assert.IsFalse(props.Any(zone => Footprint(zone).Intersects(approach)), label + " needs a clear 1.5 m approach.");
            }
            foreach (var anchor in document.RecoveryAnchors) Clear(anchor.Id, anchor.Position, 1500, 1500, 1500, 1500);
            foreach (var supply in document.Supplies) Clear(supply.Id, supply.Position, 1500, 1500, 1500, 1500);
            foreach (var site in document.PhysicalSites) Clear(site.PuzzleId, site.Position, 750, 2250, 750, 750);
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void EverySampleSeedFitsTheSoloEntityBudget(string modeId)
        {
            var items = TestData.Catalogue(); var content = WordboundParcelServiceContent.Create(items);
            var mode = ModeDefinition.For(modeId);
            var rules = mode.CreateGameplayRules(TestData.LoadRuleBook().For("Dibs"));
            for (int seed = 1; seed <= 32; seed++)
            {
                var course = content.Resolve(seed.ToString("x16"), MatchStateValidation.Requirements(mode)).Manifest;
                var budget = new WordboundEntityBudget(mode, course, content.Geometry, items, rules);
                Assert.Less(budget.FixedBodies, WordboundEntityBudget.MaximumBodies);
                Assert.GreaterOrEqual(budget.OptionalBodies, mode.IsRace ? mode.TeamCapacity : mode.MaximumPlayers);
                foreach (var puzzle in course.Puzzles)
                {
                    Assert.IsTrue(mode.Recipes.Contains(puzzle.RecipeId) && items.Get(puzzle.RecipeId).Enabled && items.Get(puzzle.RecipeId).CanDeploy, puzzle.Id);
                    Assert.AreEqual("PlaceObject", puzzle.PresetId);
                }
            }
        }

        [Test]
        public void CreationDoesNotChangeOriginalContentOrEnableDisabledRecipes()
        {
            var items = TestData.Catalogue(); var original = WordboundFlagshipContent.Create(items);
            string before = original.Hash, geometry = original.Geometry.Hash;
            var content = WordboundParcelServiceContent.Create(items);
            Assert.AreEqual(WordboundParcelServiceContent.CourseId, content.CopyDocument().Id);
            Assert.AreNotEqual(WordboundParcelServiceContent.LegacyCourseId, WordboundParcelServiceContent.CourseId);
            foreach (string id in new[] { "BOX", "CRATE", "CHEST", "DESK", "SHELF", "BARREL", "CABINET", "CHAIR", "PLANT" })
            {
                Assert.IsFalse(items.Get(id).Enabled); Assert.IsFalse(content.Geometry.Assets[id].CanCraft);
                Assert.IsFalse(ModeDefinition.For("Freestyle").Recipes.Contains(id));
                Assert.IsFalse(original.Geometry.Assets.ContainsKey(id));
            }
            Assert.IsFalse(content.Geometry.Assets.ContainsKey("LAMP"));
            Assert.AreEqual(before, WordboundFlagshipContent.Create(items).Hash);
            Assert.AreEqual(geometry, WordboundFlagshipContent.Create(items).Geometry.Hash);
            var edited = content.CopyDocument(); edited.Title = "Edited";
            Assert.AreEqual("Parcel Depot", content.CopyDocument().Title);
        }

        [Test]
        public void RealMapFurnitureCanBeCarriedAndReturnsEveryOptionalLetterOnlyOnce()
        {
            var items = TestData.Catalogue(); var content = WordboundParcelServiceContent.Create(items);
            var course = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("Freestyle"))).Manifest;
            Assert.IsNotNull(course);
            var economy = new Economy(items, new GameRules { StarterLetters = "" }); economy.AddPlayer(0, 0);
            foreach (var prop in course.Placements)
            {
                string word = content.Geometry.Assets[prop.AssetId].Letters;
                var item = economy.PlaceFurniture(word);
                Assert.AreEqual(ItemOrigin.Map, item.Origin);
                Assert.IsTrue(economy.PickUp(0, item.Id, item.Revision).Ok);
                Assert.IsTrue(economy.DropCarried(0).Ok);
                var tiles = economy.Damage(item.Id, 1000);
                Assert.AreEqual(new string(word.OrderBy(c => c).ToArray()), new string(tiles.Tiles.Select(t => t.Letter).OrderBy(c => c).ToArray()));
                Assert.AreEqual(0, economy.Damage(item.Id, 1000).Count);
            }
            Assert.AreEqual(WordboundParcelServiceContent.FurnitureLetters, economy.Tiles.Count);
            Assert.AreEqual(economy.Minted, economy.Tiles.Count); Assert.AreEqual(0, economy.Spent);
            TestData.AssertConserved(economy);
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void RequiredResourcesRemainValidWhenEveryOptionalPropIsRemoved(string mode)
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument(); document.Zones.Clear();
            var result = CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For(mode))));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
            foreach (var puzzle in result.Manifest.Puzzles)
                Assert.IsTrue(result.Manifest.Supplies.Any(s => puzzle.RecoverySupplyIds.Contains(s.SourceId) &&
                    s.Renewable && s.Protected && s.Letters == puzzle.RequiredLetters && s.ModuleId == puzzle.ModuleId));
        }

        [Test]
        public void ObstructedRoutesAndUnsupportedPublicModesAreRejected()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument(); var prop = document.Zones.Single(z => z.Id == "SortingCrateNorth");
            prop.FixedPosition = new CoursePoint(-4000, 0, 6000);
            prop.Bounds = new CourseBox(new CoursePoint(-5000, 0, 5000), new CoursePoint(-3000, 0, 7000));
            var blocked = CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"))));
            Assert.IsFalse(blocked.Success);
            Assert.IsTrue(blocked.Issues.Any(i => i.Code == "placement.exhausted" && i.EntityId == "SortingCrateNorth"), string.Join("; ", blocked.Issues));
            Assert.IsFalse(content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TeamRace"))).Success);
        }
    }
}
