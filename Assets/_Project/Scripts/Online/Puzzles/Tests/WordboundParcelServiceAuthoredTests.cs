using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundParcelServiceAuthoredTests
    {
        string items, rules;
        [SetUp] public void LoadSources()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string folder = Path.Combine(dir.FullName, "Assets/_Project/Data/Config");
                    if (!File.Exists(Path.Combine(folder, "items.json"))) continue;
                    items = File.ReadAllText(Path.Combine(folder, "items.json"));
                    rules = File.ReadAllText(Path.Combine(folder, "rules.json")); return;
                }
            Assert.Fail("Real item catalogue and rule book are required.");
        }
        WordboundAuthoredPuzzleContent Content(string modeId, bool withoutProps = false)
        {
            var factory = WordboundParcelServiceContent.Create(ItemCatalogue.FromJson(items));
            var mode = ModeDefinition.For(modeId); var document = factory.CopyDocument();
            if (withoutProps) document.Zones.Clear();
            var resolved = CourseResolver.Resolve(document, factory.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(resolved.Success, string.Join("; ", resolved.Issues.Select(i => i.Message)));
            return new WordboundAuthoredPuzzleContent(mode, factory.Geometry, resolved.Manifest, items, rules);
        }
        static MatchBinding Binding(IWordboundPuzzleContent content) => new MatchBinding("parcel-local",
            MatchSnapshotCodec.ProtocolVersion, content.Mode.Hash, content.Course.SourceHash, content.Course.Hash,
            new string('a', 64), content.CatalogueHash, new[] { new WordboundSeat("creator", 0, 0) });
        static AuthoritativeMatch Match(IWordboundPuzzleContent content, MatchBinding binding) => new AuthoritativeMatch(binding,
            content.Mode, content.Course, content.Geometry, content.Items, content.Rules, new NoCompletion());

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void ExactAuthoredCourseStartsWithRealBoundedFurnitureAndUnsolvedStations(string mode)
        {
            var content = Content(mode); var binding = Binding(content); content.Validate(binding, content.Rules);
            var match = Match(content, binding); var frame = match.Capture(1);
            Assert.AreEqual("", MatchStateValidation.Validate(frame, binding, content.Mode, content.Course, content.Items, content.Rules));
            Assert.AreEqual(6, content.Plan.Sites.Count); Assert.AreEqual(0, content.Plan.Platforms.Count);
            foreach (var puzzle in content.Course.Puzzles)
                Assert.IsTrue(content.Items.Get(puzzle.RecipeId).CanDeploy, "PlaceObject needs a deployed crafted item.");
            Assert.AreEqual(WordboundParcelServiceContent.FurnitureCount, frame.Bodies.Count(b => b.Kind == WorldBodyKind.Item && b.Active));
            foreach (var placement in content.Course.Placements)
            {
                var body = frame.Bodies.Single(b => b.Kind == WorldBodyKind.Item && b.SourceId == placement.Id);
                Assert.IsTrue(body.Active); Assert.IsFalse(body.Kinematic);
                var item = frame.Economy.Items.Single(i => body.Id == "item:" + i.Id);
                Assert.AreEqual(ItemOrigin.Map, item.Origin); Assert.AreEqual(content.Geometry.Assets[placement.AssetId].Letters, item.Word);
            }
            Assert.IsTrue(frame.Mechanisms.All(m => !m.Solved)); Assert.IsNull(frame.Progress.Result);
            Assert.IsTrue(frame.Bodies.Any(b => b.Kind == WorldBodyKind.Keepsake && b.Active));
            Assert.IsTrue(match.EntityBudget.FixedBodies < WordboundEntityBudget.MaximumBodies);
            Assert.IsTrue(match.EntityBudget.OptionalBodies >= content.Mode.MaximumPlayers);
        }

        [TestCase("BOX"), TestCase("CRATE"), TestCase("CHEST"), TestCase("DESK"), TestCase("SHELF")]
        public void AMapPropDoesNotBecomeAnEnabledWordboundCraftRecipe(string id)
        {
            var content = Content("Freestyle"); var binding = Binding(content); var match = Match(content, binding);
            var result = match.Advance(17, new[] { new AuthenticatedCommand("creator",
                new PlayerCommand(1, 0, WordboundAction.BeginCraft, value: id)) });
            Assert.IsTrue(result.Advanced); Assert.AreEqual("RecipeDisabled", result.Receipts.Single().Effect.Code);
            Assert.IsFalse(content.Items.Get(id).Enabled); Assert.IsFalse(content.Geometry.Assets[id].CanCraft);
            Assert.IsTrue(match.Capture(2).Economy.Items.All(i => i.Origin == ItemOrigin.Map));
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void RequiredStationsStillBindWhenOptionalFurnitureIsAbsent(string mode)
        {
            var content = Content(mode, true); var binding = Binding(content); var frame = Match(content, binding).Capture(1);
            Assert.AreEqual("", MatchStateValidation.Validate(frame, binding, content.Mode, content.Course, content.Items, content.Rules));
            Assert.AreEqual(0, frame.Economy.Items.Count); Assert.AreEqual(6, content.Plan.Sites.Count);
            foreach (var site in content.Plan.Sites)
                Assert.IsTrue(content.Course.Supplies.Any(s => s.SourceId == site.RecoverySupplyId && s.Renewable && s.Protected));
        }

        [Test]
        public void SavedCourseEditsRequireTheirOwnContentBinding()
        {
            var content = Content("TimeRun"); var binding = Binding(content); var edited = content.Course.CopyDocument();
            edited.Title = "Edited parcel service";
            var next = CourseResolver.Resolve(edited, content.Geometry,
                new CourseResolveRequest(content.Course.Seed, MatchStateValidation.Requirements(content.Mode)));
            Assert.IsTrue(next.Success);
            var changed = new WordboundAuthoredPuzzleContent(content.Mode, content.Geometry, next.Manifest, items, rules);
            Assert.AreNotEqual(content.Course.SourceHash, changed.Course.SourceHash);
            Assert.AreNotEqual(content.CatalogueHash, changed.CatalogueHash);
            Assert.Throws<ArgumentException>(() => changed.Validate(binding, changed.Rules));
        }

        sealed class NoCompletion : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actor, string recipe, WorldVector position) => false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState body) => false;
        }
    }
}
