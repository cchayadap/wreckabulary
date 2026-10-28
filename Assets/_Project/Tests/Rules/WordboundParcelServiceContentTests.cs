using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundParcelServiceContentTests
    {
        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void CompactCourseResolvesDeterministicallyWithClearRoutesAndASeededParcelBay(string mode)
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var play = MatchStateValidation.Requirements(ModeDefinition.For(mode));
            string firstBay = null; bool varied = false;
            for (int seed = 1; seed <= 128; seed++)
            {
                var result = content.Resolve(seed.ToString("x16"), play);
                Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(i => i.EntityId + ": " + i.Message)));
                var course = result.Manifest;
                Assert.AreEqual(3, course.Geometry.Count); Assert.AreEqual(2, course.Connections.Count);
                Assert.AreEqual(8, course.Spawns.Count); Assert.AreEqual(WordboundParcelServiceContent.FurnitureCount, course.Placements.Count);
                Assert.IsTrue(course.Placements.All(p => p.Purpose == "Props"));
                Assert.AreEqual(WordboundParcelServiceContent.FurnitureLetters, course.Placements.Sum(p => content.Geometry.Assets[p.AssetId].Letters.Length));
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
                    Assert.AreEqual("Service", course.Laps[0].StartModuleId);
                    Assert.AreEqual("Dispatch", course.Laps[0].FinishModuleId);
                    Assert.AreEqual(course.Laps[0].FinishModuleId, course.Laps[1].StartModuleId);
                    Assert.AreEqual(course.Laps[1].FinishModuleId, course.Laps[2].StartModuleId);
                }
                else Assert.AreEqual(0, course.Laps.Count);
            }
            Assert.IsTrue(varied, "Optional parcel positions must actually vary between sample seeds.");
        }

        [Test]
        public void CreationDoesNotChangeOriginalContentOrEnableDisabledRecipes()
        {
            var items = TestData.Catalogue(); var original = WordboundFlagshipContent.Create(items);
            string before = original.Hash, geometry = original.Geometry.Hash;
            var content = WordboundParcelServiceContent.Create(items);
            Assert.AreEqual(WordboundParcelServiceContent.CourseId, content.CopyDocument().Id);
            foreach (string id in new[] { "BOX", "CRATE", "CHEST", "DESK", "SHELF" })
            {
                Assert.IsFalse(items.Get(id).Enabled); Assert.IsFalse(content.Geometry.Assets[id].CanCraft);
                Assert.IsFalse(ModeDefinition.For("Freestyle").Recipes.Contains(id));
                Assert.IsFalse(original.Geometry.Assets.ContainsKey(id));
            }
            Assert.IsFalse(content.Geometry.Assets.ContainsKey("LAMP"));
            Assert.AreEqual(before, WordboundFlagshipContent.Create(items).Hash);
            Assert.AreEqual(geometry, WordboundFlagshipContent.Create(items).Geometry.Hash);
            var edited = content.CopyDocument(); edited.Title = "Edited";
            Assert.AreEqual("Parcel Service", content.CopyDocument().Title);
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
                    s.Renewable && s.Protected && s.Letters == puzzle.RequiredLetters));
        }

        [Test]
        public void ObstructedRoutesAndUnsupportedPublicModesAreRejected()
        {
            var content = WordboundParcelServiceContent.Create(TestData.Catalogue());
            var document = content.CopyDocument(); var prop = document.Zones.First(z => z.Placement == "Fixed");
            prop.FixedPosition = new CoursePoint(4000, 0, 0);
            prop.Bounds = new CourseBox(new CoursePoint(3000, 0, -1000), new CoursePoint(5000, 0, 1000));
            Assert.IsFalse(CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun")))).Success);
            Assert.IsFalse(content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For("TeamRace"))).Success);
        }
    }
}
