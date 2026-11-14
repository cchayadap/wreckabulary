using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundArenaSeedTests
    {
        [Test, Category("ArenaSeeds"), Explicit("Run the bounded arena-only content gate explicitly.")]
        public void ThousandArenaSeedsReplayExactlyWithinSourcePlacementAndBodyBounds()
        {
            var document = CourseTemplates.CombatArena();
            var geometry = CourseTemplates.ProofCatalogue();
            var mode = ModeDefinition.For("FreeForAll");
            var play = MatchStateValidation.Requirements(mode);
            var items = TestData.Catalogue();
            var rules = mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            int maximumBodies = 0, minimumOptional = int.MaxValue;
            string sourceHash = null;
            for (int index = 0; index < 1000; index++)
            {
                string seed = index.ToString("x16", CultureInfo.InvariantCulture);
                var first = CourseResolver.Resolve(document, geometry, new CourseResolveRequest(seed, play));
                var replay = CourseResolver.Resolve(document, geometry, new CourseResolveRequest(seed, play));
                Assert.IsTrue(first.Success, seed + ": " + string.Join("\n", first.Issues));
                Assert.IsTrue(replay.Success, seed);
                string json = CourseJson.ExportManifest(first.Manifest);
                Assert.AreEqual(json, CourseJson.ExportManifest(replay.Manifest), seed);
                var imported = CourseJson.ImportManifest(json, geometry);
                Assert.IsTrue(imported.Success, seed + ": " + string.Join("\n", imported.Issues));
                Assert.AreEqual(json, CourseJson.ExportManifest(imported.Manifest), seed);
                var course = first.Manifest;
                if (sourceHash == null) sourceHash = course.SourceHash;
                Assert.AreEqual(sourceHash, course.SourceHash);
                Assert.AreEqual(168, course.Supplies.Count);
                Assert.AreEqual(16, course.CopyDocument().Spawns.Count);
                Assert.AreEqual(8, course.CopyDocument().Spawns.Count(s => s.Kind == "Start"));
                Assert.AreEqual(8, course.CopyDocument().Spawns.Count(s => s.Kind == "Respawn"));
                foreach (var supply in course.Supplies)
                {
                    var authored = document.Supplies.Single(s => s.Id == supply.SourceId);
                    Assert.AreEqual(authored.Position, supply.Position);
                    Assert.AreEqual(authored.Letters, supply.Letters);
                    Assert.AreEqual(-1, supply.TeamIndex);
                    Assert.IsTrue(supply.Renewable && !supply.Protected);
                }
                foreach (var placement in course.Placements)
                {
                    var zone = document.Zones.Single(z => z.Id == placement.ZoneId);
                    var asset = geometry.Assets[placement.AssetId];
                    int x = (placement.Yaw % 180 == 0 ? asset.WidthMm : asset.DepthMm) / 2;
                    int z = (placement.Yaw % 180 == 0 ? asset.DepthMm : asset.WidthMm) / 2;
                    Assert.IsTrue(zone.Bounds.Contains(new CoursePoint(placement.Position.X - x,
                        placement.Position.Y, placement.Position.Z - z)), seed);
                    Assert.IsTrue(zone.Bounds.Contains(new CoursePoint(placement.Position.X + x,
                        placement.Position.Y, placement.Position.Z + z)), seed);
                    Assert.AreEqual(-1, placement.TeamIndex);
                }
                foreach (var zone in document.Zones)
                    Assert.That(course.Placements.Count(p => p.ZoneId == zone.Id), Is.InRange(zone.MinCount, zone.MaxCount));
                int optional = course.Supplies.Sum(s => s.Letters.Length) +
                    course.Placements.Where(p => p.Purpose == "Resources").Sum(p => p.Letters.Length);
                int count = 8 + course.Placements.Count(p => p.Purpose != "Resources") + optional;
                var budget = new WordboundEntityBudget(mode, course, geometry, items, rules);
                Assert.AreEqual(288, budget.ActorAndBurstBodies);
                Assert.GreaterOrEqual(budget.OptionalBodies, optional);
                Assert.LessOrEqual(count, 194);
                maximumBodies = Math.Max(maximumBodies, count);
                minimumOptional = Math.Min(minimumOptional, budget.OptionalBodies);
            }
            TestContext.WriteLine("Seeds=1000; source=" + sourceHash + "; maxInitialBodies=" + maximumBodies +
                "; minimumOptionalReserve=" + minimumOptional + "; cap=" + WordboundEntityBudget.MaximumBodies);
        }
    }
}
