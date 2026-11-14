using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundArrivalVariantTests
    {
        static ItemCatalogue Items()
        {
            string copied = Path.Combine(AppContext.BaseDirectory, "FlagshipCatalogue.json");
            return File.Exists(copied) ? ItemCatalogue.FromJson(File.ReadAllText(copied)) : TestData.Catalogue();
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        public void FurnishedVariantKeepsCanonicalClearancesAndDeterministicTeamBindings(string mode, RaceFormat format)
        {
            var content = WordboundFlagshipContent.Create(Items()); var source = content.CopyDocument();
            string before = CourseJson.ExportDocument(source);
            var variant = WordboundArrivalVariant.Create(source, content.Geometry);
            Assert.AreEqual(before, CourseJson.ExportDocument(source));
            Assert.AreEqual(before, CourseJson.ExportDocument(content.CopyDocument()));
            Assert.AreEqual(WordboundArrivalVariant.CourseId, variant.Id); Assert.AreNotEqual(source.Id, variant.Id);
            Assert.AreEqual(source.Spawns.Count, variant.Spawns.Count);
            Assert.AreEqual(source.RecoveryAnchors.Count, variant.RecoveryAnchors.Count);
            Assert.AreEqual(source.Zones.Count + 18, variant.Zones.Count);
            Assert.AreEqual(source.Supplies.Count + 1, variant.Supplies.Count);
            var requirements = MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
            for (int seed = 1; seed <= 24; seed++)
            {
                var request = new CourseResolveRequest(seed.ToString("x16"), requirements);
                var resolved = CourseResolver.Resolve(variant, content.Geometry, request);
                Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Issues.Select(i => i.Code + " " + i.EntityId + ": " + i.Message)));
                var props = resolved.Manifest.Placements.Where(p => p.ModuleId == WordboundArrivalVariant.EntryModuleId).ToArray();
                Assert.GreaterOrEqual(props.Length, 18);
                Assert.IsTrue(props.All(p => p.Purpose == "Props" && content.Geometry.Assets[p.AssetId].Letters.Length > 0));
                Assert.IsTrue(resolved.Manifest.Supplies.Any(s => s.ModuleId == WordboundArrivalVariant.EntryModuleId &&
                    s.Letters == "HAMMER" && s.Renewable && s.Protected));
                var repeated = CourseResolver.Resolve(variant, content.Geometry, request);
                Assert.IsTrue(repeated.Success); Assert.AreEqual(resolved.Manifest.Hash, repeated.Manifest.Hash);
                var roundTrip = CourseJson.ImportManifest(CourseJson.ExportManifest(resolved.Manifest), content.Geometry);
                Assert.IsTrue(roundTrip.Success); Assert.AreEqual(resolved.Manifest.Hash, roundTrip.Manifest.Hash);
            }
        }

        [TestCase("TABLE"), TestCase("SOFA"), TestCase("STOOL")]
        public void FurnishingUsesOriginalMapFurnitureAndReturnsItsLettersExactlyOnce(string id)
        {
            var items = Items(); var content = WordboundFlagshipContent.Create(items);
            var variant = WordboundArrivalVariant.Create(content.CopyDocument(), content.Geometry);
            Assert.IsTrue(variant.Zones.Any(z => z.ModuleId == WordboundArrivalVariant.EntryModuleId && z.Pool.Any(p => p.AssetId == id)));
            string word = content.Geometry.Assets[id].Letters;
            var economy = new Economy(items, new GameRules()); var item = economy.PlaceFurniture(word);
            Assert.AreEqual(ItemOrigin.Map, item.Origin); Assert.AreEqual(ItemState.World, item.State);
            var tiles = economy.Damage(item.Id, 1000);
            Assert.AreEqual(new string(word.OrderBy(c => c).ToArray()), new string(tiles.Tiles.Select(t => t.Letter).OrderBy(c => c).ToArray()));
            Assert.IsTrue(item.IsGone); Assert.AreEqual(0, economy.Damage(item.Id, 1000).Count);
            Assert.AreEqual(economy.Minted, economy.Tiles.Count); Assert.AreEqual(0, economy.Spent);
            TestData.AssertConserved(economy);
        }

        [Test]
        public void LocalAuthoringEditsAreClonedAndExistingEntryFurnitureIsNeverOverwritten()
        {
            var content = WordboundFlagshipContent.Create(Items()); var edited = content.CopyDocument();
            edited.Spawns[0].Yaw = 90; string before = CourseJson.ExportDocument(edited);
            var variant = WordboundArrivalVariant.Create(edited, content.Geometry);
            Assert.AreEqual(90, variant.Spawns[0].Yaw); Assert.AreEqual(before, CourseJson.ExportDocument(edited));
            edited.Zones.Add(variant.Zones.Single(z => z.Id == "Arrival_SortingTable"));
            Assert.Throws<ArgumentException>(() => WordboundArrivalVariant.Create(edited, content.Geometry));
            Assert.Throws<ArgumentException>(() => WordboundArrivalVariant.Create(variant, content.Geometry));
        }
    }
}
