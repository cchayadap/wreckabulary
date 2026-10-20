using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.CapacityEvidence
{
    public sealed class FlagshipCapacityTests
    {
        sealed class NoInteraction : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actorId, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actorId, string recipeId, WorldVector position) => false;
            public bool PuzzleSatisfied(string actorId, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        public void EveryFlagshipModeAndAuthoredAlternativeFitsThePinnedBudget(string modeId, RaceFormat format)
        {
            var items = ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory, "FlagshipCatalogue.json")));
            var content = WordboundFlagshipContent.Create(items);
            var mode = ModeDefinition.For(modeId, format);
            var rules = mode.CreateGameplayRules(TestData.RulesFor("Dibs"));
            var expected = content.CopyDocument().RouteSections.SelectMany(s => s.Alternatives).Select(a => a.Id)
                .ToHashSet(StringComparer.Ordinal);
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var seats = Enumerable.Range(0, mode.MaximumPlayers).Select(i =>
                new WordboundSeat("budget" + i, mode.HasRaceFormat ? i / mode.TeamCapacity : 0,
                    mode.HasRaceFormat ? i % mode.TeamCapacity : i)).ToArray();
            for (int variant = 0; variant < (mode.IsRace ? 3 : 1); variant++)
            {
                var document = content.CopyDocument();
                if (variant != 0)
                    foreach (var section in document.RouteSections)
                        section.Alternatives = new List<CourseAlternative> {
                            variant == 1 ? section.Alternatives.First() : section.Alternatives.Last() };
                var resolved = CourseResolver.Resolve(document, content.Geometry,
                    new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
                Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
                var course = resolved.Manifest;
                selected.UnionWith(course.Laps.SelectMany(l => l.AlternativeIds));
                var binding = new MatchBinding("flagship-capacity", MatchSnapshotCodec.ProtocolVersion, mode.Hash,
                    course.SourceHash, course.Hash, new string('a', 64), new string('b', 64), seats);
                var authority = new AuthoritativeMatch(binding, mode, course, content.Geometry, items, rules, new NoInteraction());
                var state = authority.Capture(1);
                var budget = authority.EntityBudget;
                Assert.LessOrEqual(state.Bodies.Count, WordboundEntityBudget.MaximumBodies);
                Assert.LessOrEqual(budget.MaximumRetainedItems, WordboundEntityBudget.MaximumCanonicalEntities);
                Assert.GreaterOrEqual(budget.OptionalBodies, mode.HasRaceFormat ? mode.TeamCapacity : mode.MaximumPlayers);
                authority.PublishSnapshot(out string json);
                var imported = MatchSnapshotCodec.Import(json, binding, mode, course, items, rules);
                Assert.IsTrue(imported.Success, imported.Error);
                TestContext.WriteLine($"{modeId}/{format}/variant{variant}: K={budget.FixedBodies}, " +
                    $"pools={budget.PoolCount}, pool={budget.PoolBodies}, C={budget.CriticalRecoveryBodies}, " +
                    $"H={budget.PhysicalAuxiliaryBodies}, B={budget.ActorAndBurstBodies}, O={budget.OptionalBodies}, " +
                    $"retainedItems={budget.MaximumRetainedItems}, startup={state.Bodies.Count}, " +
                    $"canonicalBytes={Encoding.UTF8.GetByteCount(json)}");
            }
            if (mode.IsRace)
            {
                CollectionAssert.AreEquivalent(expected, selected, "Both authored choices of every route section must be covered.");
                TestContext.WriteLine("Covered every authored alternative: " + selected.Count);
            }
        }
    }
}
