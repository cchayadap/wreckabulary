using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        static Fixture Create(string modeId = "Freestyle", int count = 2, RaceFormat format = RaceFormat.Solo, bool machinery = false)
        {
            var mode = ModeDefinition.For(modeId, format);
            var f = new Fixture { Geometry = CourseTemplates.ProofCatalogue(), Items = new ItemCatalogue() };
            foreach (var asset in f.Geometry.Assets.Values)
                f.Items.Add(new ItemDefinition { Id = asset.Letters, Enabled = true, Durability = 100,
                    Size = new[] { 2f, 1f, 1f }, Deploy = new DeployStats { FootprintX = 2, FootprintZ = 1 } });
            var document = modeId == "Freestyle" ? CourseTemplates.FreestyleWorld() :
                modeId == "FreeForAll" ? CourseTemplates.CombatArena() : CourseTemplates.RaceCircuit();
            if (machinery)
            {
                f.Items.Add(new ItemDefinition { Id = "CLOCK", Enabled = true, Durability = 100, Size = new[] { .5f, .6f, .3f } });
                f.Geometry = new CourseCatalogue(f.Geometry.Modules.Values, f.Geometry.Assets.Values.Concat(new[] {
                    new CourseAssetDefinition("CLOCK", "CLOCK", 500, 300, 600) }),
                    f.Geometry.PuzzlePresets, f.Geometry.Themes, f.Geometry.NarrativeEvents);
                var puzzle = document.Puzzles.Single(p => p.Id == "BedBridge");
                puzzle.PresetId = "TimedMechanism"; puzzle.RecipeId = puzzle.RequiredLetters = "CLOCK";
                document.Supplies.Single(s => s.Id == "BedSupply").Letters = "CLOCK";
                document.PhysicalSites.Add(new CoursePhysicalSite {
                    Lap = 1, PuzzleId = puzzle.Id, CheckpointId = "Begin", RecoverySupplyId = "BedSupply",
                    ActiveWindowTicks = 120,
                    ArrivalBounds = new CourseBox(new CoursePoint(-250, 0, 1500), new CoursePoint(250, 1800, 2000)),
                    Fixtures = new List<CourseMovingFixture> { new CourseMovingFixture {
                        Id = "SortingArm", From = new CoursePoint(-1000, 0, 1000), To = new CoursePoint(1000, 0, 1000),
                        SizeMm = new CoursePoint(600, 1200, 200), PeriodTicks = 240, PhaseTicks = 40 } }
                });
            }
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

        [TestCase("TimeRun"), TestCase("TeamRace"), TestCase("BrawlRace"), TestCase("FreeForAll"), TestCase("Freestyle")]
        public void SharedFramePresentationMatchesPreviousLookupSemanticsForEveryBody(string mode)
        {
            var f = Create(mode); var frame = f.Frame;
            var context = new WordboundPresentationContext(frame, f.Course, f.Geometry, f.Items);
            foreach (var body in frame.Bodies)
                SamePresentation(LegacyPresentation(f, frame, body), context.Presentation(body));
        }

        [Test]
        public void StandalonePresentationStillAcceptsUnusedNullDependencies()
        {
            var f = Create("TimeRun"); var frame = f.Frame;
            foreach (var kind in new[] { WorldBodyKind.Player, WorldBodyKind.Tile, WorldBodyKind.Keepsake, WorldBodyKind.Platform })
            {
                var body = frame.Bodies.First(value => value.Kind == kind);
                var expected = f.Present(frame, body);
                SamePresentation(expected, WordboundGameplayPlan.Presentation(body, frame,
                    kind == WorldBodyKind.Platform ? f.Course : null, null, null));
                Assert.Throws<ArgumentException>(() => WordboundGameplayPlan.Presentation(
                    body.At(body.Position), frame, null, null, null));
            }
        }

        [Test]
        public void PresentationContextUsesExactReferencesAndRetainsItsOwnInventorySnapshot()
        {
            var f = Create(); var before = f.Frame;
            var prop = before.Bodies.First(b => b.Kind == WorldBodyKind.Item);
            var oldContext = new WordboundPresentationContext(before, f.Course, f.Geometry, f.Items);
            Assert.Throws<ArgumentException>(() => oldContext.Presentation(null));
            Assert.Throws<ArgumentException>(() => oldContext.Presentation(prop.At(prop.Position)));
            int revision = before.Economy.Items.Single(i => prop.Id == "item:" + i.Id).Revision;
            Assert.AreEqual("Ok", f.Command(WordboundAction.PickUp, prop, revision).Receipts.Single().Effect.Code);
            var after = f.Frame; var carried = after.Bodies.Single(b => b.Id == prop.Id);
            var newContext = new WordboundPresentationContext(after, f.Course, f.Geometry, f.Items);
            Assert.Throws<ArgumentException>(() => oldContext.Presentation(carried));
            Assert.Throws<ArgumentException>(() => newContext.Presentation(prop));
            var exposed = after.Economy.Items.Single(i => carried.Id == "item:" + i.Id);
            exposed.State = ItemState.World; exposed.HolderId = 7; exposed.Word = "INVALID";
            Assert.AreEqual(-1, oldContext.Presentation(prop).CarrierSeat);
            Assert.AreEqual(0, newContext.Presentation(carried).CarrierSeat);
            SamePresentation(LegacyPresentation(f, after, carried), newContext.Presentation(carried));
            Assert.AreEqual(ItemState.Carried, after.Economy.Items.Single(i => carried.Id == "item:" + i.Id).State);
        }

        [Test]
        public void IndexedMovingFixtureKeepsItsAuthoredDimensionsAndConnectionPlatforms()
        {
            var f = Create("TimeRun", machinery: true); var frame = f.Frame;
            var context = new WordboundPresentationContext(frame, f.Course, f.Geometry, f.Items);
            var fixture = frame.Bodies.First(b => b.Kind == WorldBodyKind.Platform && b.SourceId == "SortingArm");
            var shape = context.Presentation(fixture);
            Assert.AreEqual(.6f, shape.Size.X); Assert.AreEqual(1.2f, shape.Size.Y); Assert.AreEqual(.2f, shape.Size.Z);
            Assert.AreEqual("platform", shape.VisualKey);
            Assert.IsTrue(frame.Bodies.Any(b => b.Kind == WorldBodyKind.Platform && b.SourceId != "SortingArm"));
            foreach (var body in frame.Bodies)
                SamePresentation(LegacyPresentation(f, frame, body), context.Presentation(body));
        }

        [Test]
        public void IndexedPresentationRejectsDuplicateBodiesAndMissingEconomyEntries()
        {
            var f = Create(); var original = f.Frame;
            MatchSnapshot WithBodies(IEnumerable<WorldBodyState> bodies) => new MatchSnapshot(original.Binding,
                original.Epoch, original.Sequence, original.NextWorldEntityId, original.Progress, original.Economy,
                original.Commands, original.Actors, bodies, original.Resources, original.Mechanisms, original.RandomStreams,
                original.NarrativeFlags, original.ThrownFlights, original.Practice);
            Assert.Throws<ArgumentException>(() => new WordboundPresentationContext(
                WithBodies(original.Bodies.Concat(original.Bodies.Take(1))), f.Course, f.Geometry, f.Items));
            var missing = new WorldBodyState("item:999999", WorldBodyKind.Item, default);
            var context = new WordboundPresentationContext(WithBodies(original.Bodies.Concat(new[] { missing })),
                f.Course, f.Geometry, f.Items);
            Assert.Throws<KeyNotFoundException>(() => context.Presentation(missing));
        }

        [TestCase(32), TestCase(128), TestCase(256)]
        public void PresentationAllocationProbeRecordsBoundedItemLookupCost(int count)
        {
            var f = Create(); var original = f.Frame;
            var economy = new Economy(f.Items, new GameRules());
            var bodies = Enumerable.Range(0, count).Select(_ => {
                var item = economy.PlaceFurniture("STOOL");
                return new WorldBodyState("item:" + item.Id, WorldBodyKind.Item, default);
            }).ToArray();
            var frame = new MatchSnapshot(original.Binding, original.Epoch, original.Sequence, original.NextWorldEntityId,
                original.Progress, economy.Capture(), original.Commands, original.Actors, bodies,
                original.Resources, original.Mechanisms, original.RandomStreams, original.NarrativeFlags,
                original.ThrownFlights, original.Practice);
            var parity = new WordboundPresentationContext(frame, f.Course, f.Geometry, f.Items);
            foreach (var body in bodies) SamePresentation(LegacyPresentation(f, frame, body), parity.Presentation(body));
            double legacySum = 0, indexedSum = 0;
            Action legacy = () => {
                foreach (var body in bodies) legacySum += LegacyPresentation(f, frame, body).Size.X;
            };
            Action indexed = () => {
                var context = new WordboundPresentationContext(frame, f.Course, f.Geometry, f.Items);
                foreach (var body in bodies) indexedSum += context.Presentation(body).Size.X;
            };
            legacy(); indexed(); legacySum = indexedSum = 0;
            var oldCost = MeasurePresentation(legacy, 4);
            var newCost = MeasurePresentation(indexed, 4);
            Assert.AreEqual(legacySum, indexedSum);
            Assert.Less(newCost.Item1, oldCost.Item1 / 2, "One frame context must avoid repeated complete inventory copies.");
            TestContext.WriteLine(FormattableString.Invariant($"WORDBOUND_PRESENTATION_PROBE: syntheticItemBodies={count}; iterations=4; legacyBytes={oldCost.Item1}; indexedBytes={newCost.Item1}; legacyMs={oldCost.Item2:F3}; indexedMs={newCost.Item2:F3}; scope=pure-presentation-only"));
        }

        static Tuple<long, double> MeasurePresentation(Action action, int iterations)
        {
            long bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp();
            for (int i = 0; i < iterations; i++) action();
            long elapsed = Stopwatch.GetTimestamp() - ticks;
            return Tuple.Create(GC.GetAllocatedBytesForCurrentThread() - bytes, elapsed * 1000d / Stopwatch.Frequency);
        }

        static void SamePresentation(WordboundBodyPresentation expected, WordboundBodyPresentation actual)
        {
            Assert.AreEqual(expected.VisualKey, actual.VisualKey); Assert.AreEqual(expected.CarrierSeat, actual.CarrierSeat);
            Assert.AreEqual(expected.Size.X, actual.Size.X); Assert.AreEqual(expected.Size.Y, actual.Size.Y);
            Assert.AreEqual(expected.Size.Z, actual.Size.Z);
        }

        static WordboundBodyPresentation LegacyPresentation(Fixture f, MatchSnapshot frame, WorldBodyState body) =>
            WordboundGameplayPlan.Presentation(body, frame, f.Course, f.Geometry, f.Items);

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
