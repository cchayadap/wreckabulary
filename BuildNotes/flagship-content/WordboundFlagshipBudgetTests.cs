using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WordboundFlagshipBudgetTests
    {
        sealed class NoInteraction : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => false;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState placedItem) => false;
        }

        [TestCase("TimeRun", RaceFormat.Solo, 1)]
        [TestCase("TimeRun", RaceFormat.Solo, 4)]
        [TestCase("TeamRace", RaceFormat.Squad, 8)]
        [TestCase("BrawlRace", RaceFormat.Squad, 8)]
        [TestCase("Freestyle", RaceFormat.Solo, 8)]
        public void MeasureCanonicalStartupAndThreeLapRefillBudget(string modeId, RaceFormat format, int players)
        {
            var items = ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "FlagshipCatalogue.json")));
            var content = WordboundFlagshipContent.Create(items);
            var mode = ModeDefinition.For(modeId, format);
            var resolved = content.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            var course = resolved.Manifest;
            var seats = Enumerable.Range(0, players).Select(i =>
                new WordboundSeat("budget" + i, mode.HasRaceFormat ? i / mode.TeamCapacity : 0,
                    mode.HasRaceFormat ? i % mode.TeamCapacity : i)).ToArray();
            Assert.IsEmpty(mode.ValidateRoster(seats));
            var binding = new MatchBinding("flagship-budget", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                course.SourceHash, course.Hash, new string('a', 64), new string('b', 64), seats);
            var rules = mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            var match = new AuthoritativeMatch(binding, mode, course, content.Geometry, items, rules, new NoInteraction());
            var snapshot = match.Capture(1);
            var plan = new WordboundReconciliationPlan(Array.Empty<string>(), snapshot);
            Assert.AreEqual(snapshot.Bodies.Count, plan.Add.Count);
            match.PublishSnapshot(out string json);
            var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
            Assert.IsTrue(imported.Success, imported.Error);
            int bodyTiles = snapshot.Bodies.Count(b => b.Kind == WorldBodyKind.Tile);
            int props = snapshot.Bodies.Count(b => b.Kind == WorldBodyKind.Item);
            int fixtures = snapshot.Bodies.Count(b => b.Kind == WorldBodyKind.Platform);
            int fixedBodies = snapshot.Bodies.Count - bodyTiles;
            TestContext.WriteLine($"{modeId}/{format}/{players}: initial={snapshot.Bodies.Count}, tiles={bodyTiles}, " +
                $"items={props}, platforms={fixtures}, resources={snapshot.Resources.Count}, canonicalBytes={Encoding.UTF8.GetByteCount(json)}");
            if (!mode.IsRace)
            {
                TestContext.WriteLine("Freestyle has no three-lap bound; repeated recovery/crafting may accumulate item bodies indefinitely.");
                return;
            }
            int fullSupplyTiles = 0;
            foreach (var lap in course.Laps)
            {
                var solved = lap.Checkpoints.SelectMany(c => c.PuzzleIds).ToHashSet(StringComparer.Ordinal);
                int letters = course.Supplies.Where(s => s.AfterPuzzleIds.All(solved.Contains)).Sum(s => s.Letters.Length);
                fullSupplyTiles += letters;
                TestContext.WriteLine($"  lap {lap.Number}: available supply letters after all selected puzzles={letters}");
            }
            int teams = seats.Select(s => s.Team).Distinct().Count();
            int requiredCraftTombstones = course.Laps.Sum(lap => lap.Checkpoints.Sum(c => c.PuzzleIds.Count)) * teams;
            int markedPropLetters = course.Placements.Where(p => course.PhysicalSites.Any(s => s.PropSourceZoneId == p.ZoneId))
                .Sum(p => content.Geometry.Assets[p.AssetId].Letters.Length);
            int refilledEstimate = fixedBodies + fullSupplyTiles + requiredCraftTombstones;
            int withUnusedSmashLetters = refilledEstimate + markedPropLetters;
            TestContext.WriteLine($"  three-lap refill scenario: base={fixedBodies}, retained supply tiles={fullSupplyTiles}, " +
                $"required crafted tombstones={requiredCraftTombstones}, total={refilledEstimate}; " +
                $"plus unused marked-prop letters={withUnusedSmashLetters}; unchanged reconciliation limit=512");
            TestContext.WriteLine("  Estimate assumes each unlocked supply has recovered before leaving its lap; it is not a played simulation " +
                "or a universal maximum. Optional crafting, extra recoveries, expired deployments and destroyed random props add more bodies.");
        }
    }
}
