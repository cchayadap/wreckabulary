using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundKitchenPilotContentTests
    {
        const string ItemPath = "Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Items.json";
        static string ItemJson() => File.ReadAllText(Path.Combine(TestData.Root, ItemPath));
        static ItemCatalogue Items() => ItemCatalogue.FromJson(ItemJson(), ItemPath);
        static bool Kitchen(ResolvedCoursePlacement placement) => placement.ZoneId.StartsWith("KitchenPilot_", StringComparison.Ordinal);

        [Test]
        public void AugmentedSourcePreservesEveryOriginalDefinitionAndAddsOnlyDisabledFurniture()
        {
            string source = TestData.Read("items.json"), augmented = ItemJson();
            string originalPrefix = source.Substring(0, source.LastIndexOf(']')).TrimEnd();
            Assert.IsTrue(augmented.StartsWith(originalPrefix + ",", StringComparison.Ordinal),
                "The local item source must retain all original definition bytes before appending the three furniture definitions.");
            var original = TestData.Catalogue(); var items = Items();
            CollectionAssert.AreEqual(original.All.Select(item => item.Id), items.All.Take(original.All.Count).Select(item => item.Id));
            CollectionAssert.AreEqual(new[] { "FRIDGE", "COUNTER", "STOVE" }, items.All.Skip(original.All.Count).Select(item => item.Id));
            CollectionAssert.AreEqual(original.Enabled.Select(item => item.Id), items.Enabled.Select(item => item.Id));
            Assert.IsEmpty(items.Validate(64));
            var content = WordboundKitchenPilotContent.Create(items);
            foreach (string id in new[] { "FRIDGE", "COUNTER", "STOVE" })
            {
                Assert.IsFalse(items.Get(id).Enabled); Assert.IsFalse(content.Geometry.Assets[id].CanCraft);
                Assert.AreEqual(id, content.Geometry.Assets[id].Letters);
                foreach (string mode in content.CopyDocument().SupportedModes)
                    Assert.IsFalse(ModeDefinition.For(mode).Recipes.Contains(id));
            }
        }

        [Test]
        public void CloneRetainsTheWholeFlagshipAndCannotMutateTheOriginalOrAnotherCopy()
        {
            var originalItems = TestData.Catalogue(); var items = Items();
            var original = WordboundFlagshipContent.Create(originalItems);
            var augmentedBase = WordboundFlagshipContent.Create(items);
            Assert.AreEqual(original.Hash, augmentedBase.Hash);
            string originalDocument = CourseJson.ExportDocument(original.CopyDocument()), geometry = original.Geometry.Hash;
            var content = WordboundKitchenPilotContent.Create(items); var document = content.CopyDocument();
            Assert.AreEqual(WordboundKitchenPilotContent.CourseId, document.Id);
            Assert.AreEqual(34, document.Modules.Count); Assert.AreEqual(72, document.PhysicalSites.Count);
            Assert.AreEqual(3, document.Laps.Count); Assert.AreEqual(4, document.Layers.Count);
            var added = document.Zones.Where(zone => zone.Id.StartsWith("KitchenPilot_", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(WordboundKitchenPilotContent.FurnitureCount, added.Length);
            CollectionAssert.AreEquivalent(new[] { "FRIDGE", "COUNTER", "STOVE", "COUNTER" }, added.Select(zone => zone.Pool.Single().AssetId));
            Assert.AreEqual(WordboundKitchenPilotContent.FurnitureLetters,
                added.Sum(zone => content.Geometry.Assets[zone.Pool.Single().AssetId].Letters.Length));
            Assert.IsTrue(added.All(zone => zone.ModuleId == "Kitchen_ServiceLanding" && zone.Purpose == "Props" &&
                zone.Placement == "Fixed" && zone.MinCount == 1 && zone.MaxCount == 1 && zone.ChancePermille == 1000));
            document.Zones.RemoveAll(zone => zone.Id.StartsWith("KitchenPilot_", StringComparison.Ordinal));
            document.Id = original.CopyDocument().Id; document.Title = original.CopyDocument().Title;
            Assert.AreEqual(originalDocument, CourseJson.ExportDocument(document),
                "All starts, puzzles, recovery, resources, route branches and layers must survive unchanged.");
            var restoredGeometry = new CourseCatalogue(content.Geometry.Modules.Values,
                content.Geometry.Assets.Values.Where(asset => original.Geometry.Assets.ContainsKey(asset.Id)),
                content.Geometry.PuzzlePresets, content.Geometry.Themes, content.Geometry.NarrativeEvents);
            Assert.AreEqual(geometry, restoredGeometry.Hash);
            Assert.AreEqual(originalDocument, CourseJson.ExportDocument(original.CopyDocument()));
            Assert.AreEqual(original.Hash, WordboundFlagshipContent.Create(items).Hash);
            var fresh = content.CopyDocument(); fresh.Modules.Clear(); fresh.Zones.Clear();
            Assert.AreEqual(34, content.CopyDocument().Modules.Count);
            Assert.AreEqual(added.Length, content.CopyDocument().Zones.Count(zone => zone.Id.StartsWith("KitchenPilot_", StringComparison.Ordinal)));
        }

        [TestCase("FRIDGE"), TestCase("COUNTER"), TestCase("STOVE")]
        public void MissingFurnitureCannotBecomeAnUnknownWordAtPickupOrThrow(string missing)
        {
            var incomplete = new ItemCatalogue();
            foreach (var item in Items().All.Where(item => item.Id != missing)) incomplete.Add(item);
            var error = Assert.Throws<ArgumentException>(() => WordboundKitchenPilotContent.Create(incomplete));
            StringAssert.Contains(missing, error.Message);
            Assert.Throws<ArgumentNullException>(() => WordboundKitchenPilotContent.Create(null));
        }

        [TestCase("enabled"), TestCase("model"), TestCase("category"), TestCase("family"), TestCase("hands")]
        [TestCase("size-null"), TestCase("size-length"), TestCase("size-zero"), TestCase("size-large"), TestCase("size-nan")]
        [TestCase("grip-null"), TestCase("grip-infinity"), TestCase("grip-outside"), TestCase("scale"), TestCase("skin"), TestCase("effect")]
        public void DisabledCatalogueValidationCannotBypassRequiredModelAndCarryFacts(string invalid)
        {
            var items = Items(); var item = items.Get("FRIDGE");
            switch (invalid)
            {
                case "enabled": item.Enabled = true; break;
                case "model": item.Model = "Items/TABLE"; break;
                case "category": item.Category = ItemCategory.Combat; break;
                case "family": item.Family = HandlingFamily.Thrown; break;
                case "hands": item.Hands = 0; break;
                case "size-null": item.Size = null; break;
                case "size-length": item.Size = new[] { 1f, 1f }; break;
                case "size-zero": item.Size[0] = 0; break;
                case "size-large": item.Size[0] = 10; break;
                case "size-nan": item.Size[0] = float.NaN; break;
                case "grip-null": item.Grip = null; break;
                case "grip-infinity": item.Grip[0] = float.PositiveInfinity; break;
                case "grip-outside": item.Grip[1] = item.Size[1] + 1; break;
                case "scale": item.HeldScale = float.NaN; break;
                case "skin": item.Skins.Clear(); break;
                case "effect": item.Deploy = new DeployStats(); break;
            }
            var error = Assert.Throws<ArgumentException>(() => WordboundKitchenPilotContent.Create(items));
            StringAssert.Contains("FRIDGE", error.Message);
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo), TestCase("TeamRace", RaceFormat.Duo), TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo), TestCase("BrawlRace", RaceFormat.Duo), TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        public void SupportedModesKeepExistingPlacementsAndRoutesWithinTheRealResourceBudget(string modeId, RaceFormat format)
        {
            var items = Items(); var content = WordboundKitchenPilotContent.Create(items);
            var original = WordboundFlagshipContent.Create(items); var mode = ModeDefinition.For(modeId, format);
            var play = MatchStateValidation.Requirements(mode); var rules = mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            int copies = play.TeamCount > 0 && play.Ownership == CourseOwnershipPolicy.TeamIsolated ? play.TeamCount : 1;
            foreach (string seed in new[] { "0000000000000001", "0123456789abcdef" })
            {
                var resolution = content.Resolve(seed, play);
                Assert.IsTrue(resolution.Success, string.Join("; ", resolution.Issues.Select(issue => issue.ToString())));
                var course = resolution.Manifest; var baseResult = original.Resolve(seed, play);
                Assert.IsTrue(baseResult.Success);
                var baseline = baseResult.Manifest; var furniture = course.Placements.Where(Kitchen).ToArray();
                Assert.AreEqual(WordboundKitchenPilotContent.FurnitureCount * copies, furniture.Length);
                Assert.AreEqual(WordboundKitchenPilotContent.FurnitureLetters * copies,
                    furniture.Sum(prop => content.Geometry.Assets[prop.AssetId].Letters.Length));
                if (play.TeamCount > 0 && play.Ownership == CourseOwnershipPolicy.TeamIsolated)
                {
                    CollectionAssert.AreEqual(Enumerable.Range(0, copies), furniture.Select(prop => prop.TeamIndex).Distinct().OrderBy(team => team));
                    Assert.IsTrue(furniture.GroupBy(prop => prop.TeamIndex).All(team => team.Count() == WordboundKitchenPilotContent.FurnitureCount));
                }
                else Assert.IsTrue(furniture.All(prop => prop.TeamIndex == -1));
                Assert.AreEqual(34, course.Geometry.Count); Assert.AreEqual(72, course.PhysicalSites.Count);
                Assert.AreEqual(mode.IsRace ? 3 : 0, course.Laps.Count);
                Assert.AreEqual(course.Hash, content.Resolve(seed, play).Manifest.Hash);
                Assert.AreNotEqual(baseline.Hash, course.Hash); Assert.AreNotEqual(baseline.CatalogueHash, course.CatalogueHash);
                CollectionAssert.AreEqual(baseline.Placements.Select(PlacementIdentity), course.Placements.Where(prop => !Kitchen(prop)).Select(PlacementIdentity));
                CollectionAssert.AreEqual(baseline.Supplies.Select(s => (s.Id, s.Letters, s.Position, s.TeamIndex)),
                    course.Supplies.Select(s => (s.Id, s.Letters, s.Position, s.TeamIndex)));
                CollectionAssert.AreEqual(baseline.Spawns.Select(s => (s.Id, s.Position, s.Yaw)), course.Spawns.Select(s => (s.Id, s.Position, s.Yaw)));
                CollectionAssert.AreEqual(baseline.Laps.Select(lap => string.Join("|", lap.AlternativeIds)),
                    course.Laps.Select(lap => string.Join("|", lap.AlternativeIds)));
                foreach (var prop in furniture)
                {
                    Assert.AreEqual("Props", prop.Purpose); Assert.AreEqual("Kitchen_ServiceLanding", prop.ModuleId);
                    var document = content.CopyDocument(); var bounds = Shape(prop, content.Geometry.Assets[prop.AssetId]);
                    Assert.IsFalse(document.ReservedVolumes.Any(reserved => reserved.Bounds.Intersects(bounds)));
                    Assert.IsTrue(content.Geometry.Bounds(document.Modules.Single(room => room.Id == prop.ModuleId)).Contains(bounds.Min));
                    Assert.IsTrue(content.Geometry.Bounds(document.Modules.Single(room => room.Id == prop.ModuleId)).Contains(bounds.Max));
                    Assert.AreEqual(8000, prop.Position.Y);
                    Assert.AreEqual(document.Zones.Single(zone => zone.Id == prop.ZoneId).FixedPosition, prop.Position);
                    Assert.AreEqual(document.Zones.Single(zone => zone.Id == prop.ZoneId).FixedYaw, prop.Yaw);
                }
                var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(course), content.Geometry);
                Assert.IsTrue(imported.Success); Assert.AreEqual(course.Hash, imported.Manifest.Hash);
                var before = new WordboundEntityBudget(mode, baseline, original.Geometry, items, rules);
                var after = new WordboundEntityBudget(mode, course, content.Geometry, items, rules);
                Assert.AreEqual(before.FixedBodies + furniture.Length, after.FixedBodies);
                Assert.AreEqual(before.CriticalRecoveryBodies, after.CriticalRecoveryBodies);
                Assert.AreEqual(before.PhysicalAuxiliaryBodies, after.PhysicalAuxiliaryBodies);
                Assert.Less(after.FixedBodies, WordboundEntityBudget.MaximumBodies);
                Assert.LessOrEqual(after.MaximumRetainedItems, WordboundEntityBudget.MaximumCanonicalEntities);
                Assert.GreaterOrEqual(after.OptionalBodies, mode.IsRace ? mode.TeamCapacity : mode.MaximumPlayers);
            }
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void OriginalMandatoryResourcesRemainSufficientWithoutAnyKitchenFurniture(string modeId)
        {
            var content = WordboundKitchenPilotContent.Create(Items()); var document = content.CopyDocument();
            document.Zones.RemoveAll(zone => zone.Id.StartsWith("KitchenPilot_", StringComparison.Ordinal));
            var result = CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(ModeDefinition.For(modeId, RaceFormat.Solo))));
            Assert.IsTrue(result.Success, string.Join("; ", result.Issues.Select(issue => issue.ToString())));
            Assert.IsFalse(result.Manifest.Placements.Any(Kitchen));
            Assert.AreEqual(72, result.Manifest.PhysicalSites.Count);
        }

        [Test]
        public void MovingAnApplianceIntoTheCentralRouteIsRejected()
        {
            var content = WordboundKitchenPilotContent.Create(Items()); var document = content.CopyDocument();
            var room = document.Modules.Single(module => module.Id == "Kitchen_ServiceLanding");
            var prop = document.Zones.Single(zone => zone.Id == "KitchenPilot_Fridge");
            prop.FixedPosition = room.Position;
            prop.Bounds = new CourseBox(new CoursePoint(room.Position.X - 1000, room.Position.Y, room.Position.Z - 1000),
                new CoursePoint(room.Position.X + 1000, room.Position.Y, room.Position.Z + 1000));
            var result = CourseResolver.Resolve(document, content.Geometry,
                new CourseResolveRequest("0000000000000001", MatchStateValidation.Requirements(ModeDefinition.For("TimeRun", RaceFormat.Solo))));
            Assert.IsFalse(result.Success); Assert.IsTrue(result.Issues.Any(issue => issue.Code == "placement.exhausted"));
        }

        [Test]
        public void MapEconomyUsesOwnWordsAndRuleDerivedToughnessWithoutEnablingCrafts()
        {
            var items = Items(); var rules = new GameRules { StarterLetters = "" };
            var economy = new Economy(items, rules); economy.AddPlayer(0, 0);
            foreach (string word in new[] { "FRIDGE", "COUNTER", "STOVE", "COUNTER" })
            {
                Assert.IsNotNull(items.Get(word));
                var item = economy.PlaceFurniture(word);
                Assert.AreEqual(ItemOrigin.Map, item.Origin);
                Assert.AreEqual(rules.FurnitureToughnessBase + rules.FurnitureToughnessPerLetter * word.Length, item.Durability);
                Assert.IsTrue(economy.PickUp(0, item.Id, item.Revision).Ok);
                Assert.IsTrue(economy.DropCarried(0).Ok);
                var fragments = economy.Damage(item.Id, 1000);
                CollectionAssert.AreEqual(word.OrderBy(letter => letter), fragments.Tiles.Select(tile => tile.Letter).OrderBy(letter => letter));
                Assert.AreEqual(0, economy.Damage(item.Id, 1000).Count);
            }
            Assert.AreEqual(WordboundKitchenPilotContent.FurnitureLetters, economy.Minted);
            Assert.AreEqual(WordboundKitchenPilotContent.FurnitureLetters, economy.Tiles.Count);
            TestData.AssertConserved(economy);
        }

        static object PlacementIdentity(ResolvedCoursePlacement prop) =>
            (prop.Id, prop.ZoneId, prop.AssetId, prop.ModuleId, prop.Purpose, prop.Position, prop.Yaw, prop.TeamIndex);

        static CourseBox Shape(ResolvedCoursePlacement prop, CourseAssetDefinition asset)
        {
            int width = prop.Yaw % 180 == 0 ? asset.WidthMm : asset.DepthMm;
            int depth = prop.Yaw % 180 == 0 ? asset.DepthMm : asset.WidthMm;
            return new CourseBox(new CoursePoint(prop.Position.X - width / 2, prop.Position.Y, prop.Position.Z - depth / 2),
                new CoursePoint(prop.Position.X + (width + 1) / 2, prop.Position.Y + asset.HeightMm, prop.Position.Z + (depth + 1) / 2));
        }
    }
}
