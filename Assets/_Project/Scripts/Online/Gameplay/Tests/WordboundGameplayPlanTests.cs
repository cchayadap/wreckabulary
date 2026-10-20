using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundGameplayPlanTests
    {
        sealed class Query : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => true;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState item) => false;
        }
        sealed class Fixture
        {
            public AuthoritativeMatch Match;
            public ResolvedCourse Course;
            public CourseCatalogue Geometry;
            public ItemCatalogue Items;
            public MatchSnapshot Frame => Match.Capture(1);
            public MatchTickResult Command(WordboundAction action, WorldBodyState target, int revision = 0)
            {
                var player = Frame.Bodies.Single(b => b.Id == "player:0");
                return Match.Advance(17, new[] { new AuthenticatedCommand("a", new PlayerCommand(1, 0, action, target.Id, revision)) },
                    new[] { player.At(target.Position) });
            }
            public WordboundBodyPresentation Present(MatchSnapshot frame, WorldBodyState body) =>
                WordboundGameplayPlan.Presentation(body, frame, Course, Geometry, Items);
        }
        static Fixture Create(string modeId = "Freestyle", int count = 2, RaceFormat format = RaceFormat.Solo)
        {
            var mode = ModeDefinition.For(modeId, format);
            var f = new Fixture { Geometry = CourseTemplates.ProofCatalogue(), Items = new ItemCatalogue() };
            foreach (var asset in f.Geometry.Assets.Values)
                f.Items.Add(new ItemDefinition { Id = asset.Letters, Enabled = true, Durability = 100,
                    Size = new[] { 2f, 1f, 1f }, Deploy = new DeployStats { FootprintX = 2, FootprintZ = 1 } });
            var document = modeId == "Freestyle" ? CourseTemplates.FreestyleWorld() :
                modeId == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.RaceCircuit();
            var result = CourseResolver.Resolve(document, f.Geometry,
                new CourseResolveRequest("0123456789abcdef", MatchStateValidation.Requirements(mode)));
            Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
            f.Course = result.Manifest;
            var seats = Enumerable.Range(0, count).Select(i => new WordboundSeat(((char)('a' + i)).ToString(),
                i % mode.TeamCount, i / mode.TeamCount)).ToArray();
            var binding = new MatchBinding("match", "wordbound-v1", mode.Hash, f.Course.SourceHash, f.Course.Hash,
                new string('a', 64), new string('b', 64), seats);
            f.Match = new AuthoritativeMatch(binding, mode, f.Course, f.Geometry, f.Items,
                mode.CreateGameplayRules(new GameRules()), new Query());
            return f;
        }

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void InitialReconciliationCoversEveryCanonicalBodyWithBoundedPresentation(string mode)
        {
            var f = Create(mode); var frame = f.Frame;
            var plan = new WordboundReconciliationPlan(Array.Empty<string>(), frame);
            Assert.AreEqual(frame.Bodies.Count, plan.Add.Count);
            Assert.IsEmpty(plan.Keep); Assert.IsEmpty(plan.Remove);
            foreach (var body in plan.Add)
            {
                var visual = f.Present(frame, body);
                Assert.Greater(visual.Size.X, 0);
                Assert.IsTrue(WordboundIdentity.IsId(visual.VisualKey));
            }
        }

        [Test]
        public void CollectionRemovesTheExactConsumedTileWithoutDuplicatingOtherBodies()
        {
            var f = Create("TimeRun"); var before = f.Frame;
            var tile = before.Bodies.First(b => b.Kind == WorldBodyKind.Tile && b.OwnerTeam == 0);
            Assert.AreEqual("Ok", f.Command(WordboundAction.CollectLetter, tile).Receipts.Single().Effect.Code);
            var plan = new WordboundReconciliationPlan(before.Bodies.Select(b => b.Id), f.Frame);
            CollectionAssert.AreEqual(new[] { tile.Id }, plan.Remove);
            Assert.IsEmpty(plan.Add);
            Assert.AreEqual(before.Bodies.Count - 1, plan.Keep.Count);
        }

        [Test]
        public void CarriedFurnitureKeepsItsCanonicalBodyAndUsesItsRealHolder()
        {
            var f = Create(); var before = f.Frame;
            var prop = before.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            int revision = before.Economy.Items.Single(i => prop.Id == "item:" + i.Id).Revision;
            Assert.AreEqual("Ok", f.Command(WordboundAction.PickUp, prop, revision).Receipts.Single().Effect.Code);
            var after = f.Frame; var carried = after.Bodies.Single(b => b.Id == prop.Id);
            Assert.IsFalse(carried.Active); Assert.IsTrue(carried.Kinematic);
            Assert.AreEqual(0, f.Present(after, carried).CarrierSeat);
            var plan = new WordboundReconciliationPlan(before.Bodies.Select(b => b.Id), after);
            Assert.IsEmpty(plan.Add); Assert.IsEmpty(plan.Remove);
            Assert.AreEqual(before.Bodies.Count, plan.Keep.Count);
        }

        [Test]
        public void MapPropDimensionsUseThePinnedGeometryAssetInsteadOfRecipeGuesses()
        {
            var f = Create(); var frame = f.Frame;
            var prop = frame.Bodies.First(b => b.Kind == WorldBodyKind.Item &&
                f.Course.Placements.Single(p => p.Id == b.SourceId).AssetId == "STOOL");
            var view = f.Present(frame, prop);
            Assert.AreEqual("STOOL", view.VisualKey);
            Assert.AreEqual(.6f, view.Size.X); Assert.AreEqual(.8f, view.Size.Y);
            Assert.AreNotEqual(f.Items.All.Single(i => i.Id == "STOOL").Size[0], view.Size.X);
        }

        [Test]
        public void ReconciliationRejectsDuplicateExistingIdsAndUnboundedRegistrations()
        {
            var frame = Create().Frame;
            Assert.Throws<ArgumentException>(() => new WordboundReconciliationPlan(new[] { "player:0", "player:0" }, frame));
            Assert.Throws<ArgumentException>(() => new WordboundReconciliationPlan(
                Enumerable.Range(0, 513).Select(i => "item:" + i), frame));
        }

        [Test]
        public void PresentationCannotUseABodyFromAnotherFrame()
        {
            var f = Create(); var frame = f.Frame; var body = frame.Bodies[0];
            Assert.Throws<ArgumentException>(() => f.Present(frame, body.At(body.Position)));
        }

        [Test]
        public void CommandFrontierPreservesPendingSequencesAndResetsOnlyForAnotherEpoch()
        {
            var frame = Create().Frame;
            Assert.AreEqual(9, WordboundGameplayPlan.NextSequence(frame, "a", 8, frame.Epoch));
            Assert.AreEqual(1, WordboundGameplayPlan.NextSequence(frame, "a", 8, frame.Epoch - 1));
            Assert.Throws<ArgumentException>(() => WordboundGameplayPlan.NextSequence(frame, "outsider", 0, 0));
            Assert.Throws<InvalidOperationException>(() => WordboundGameplayPlan.NextSequence(frame, "a", long.MaxValue - 1, 0));
            var wire = WordboundGameplayPlan.Command(new PlayerCommand(9007199254740993L, 0, WordboundAction.BeginCraft, value: "BED"));
            Assert.AreEqual("9007199254740993", wire["sequence"]);
            Assert.IsFalse(wire.ContainsKey("actor"));
        }

        [Test]
        public void ReadyClientsDoNotDependOnHostOnlyCompositionFlags()
        {
            var frame = Create().Frame;
            Assert.IsFalse(WordboundGameplayPlan.IsPaused(true, true, false, false, true, frame));
            Assert.IsTrue(WordboundGameplayPlan.IsPaused(false, true, false, false, true, frame));
            Assert.IsTrue(WordboundGameplayPlan.IsPaused(true, false, false, false, true, frame));
            Assert.IsTrue(WordboundGameplayPlan.IsPaused(true, true, false, false, true, null));
        }

        [Test]
        public void HostsRemainPausedUntilBodiesAndDurableWindowPermitSimulation()
        {
            var frame = Create().Frame;
            Assert.IsTrue(WordboundGameplayPlan.IsPaused(true, true, true, false, false, frame));
            Assert.IsTrue(WordboundGameplayPlan.IsPaused(true, true, true, true, true, frame));
            Assert.IsFalse(WordboundGameplayPlan.IsPaused(true, true, true, true, false, frame));
        }

        [TestCase("TeamRace", RaceFormat.Squad), TestCase("FreeForAll", RaceFormat.Solo), TestCase("Freestyle", RaceFormat.Solo)]
        public void EightSeatsRetainDistinctStableBodyIdsAndSharedSourceOwnership(string mode, RaceFormat format)
        {
            var f = Create(mode, 8, format); var frame = f.Frame;
            var plan = new WordboundReconciliationPlan(Array.Empty<string>(), frame);
            var players = plan.Add.Where(b => b.Kind == WorldBodyKind.Player).ToArray();
            Assert.AreEqual(8, players.Length);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 8).Select(i => "player:" + i), players.Select(p => p.Id));
            foreach (var body in plan.Add) Assert.DoesNotThrow(() => f.Present(frame, body));
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(frame.Binding.Seats[i].PlayerId, players.Single(b => b.Id == "player:" + i).SourceId);
        }
    }
}
