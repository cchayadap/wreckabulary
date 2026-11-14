using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPuzzlePolicyTests
    {
        string itemJson;
        WordboundPuzzleProof proof;
        [OneTimeSetUp]
        public void LoadActualCatalogue()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string path = Path.Combine(dir.FullName, "Assets/_Project/Data/Config/items.json");
                    if (!File.Exists(path)) continue;
                    itemJson = File.ReadAllText(path); proof = WordboundPuzzleProof.Create(itemJson); return;
                }
            Assert.Fail("Actual repository item catalogue is required.");
        }

        [TestCase("TimeRun", RaceFormat.Solo), TestCase("TeamRace", RaceFormat.Squad), TestCase("BrawlRace", RaceFormat.Squad)]
        public void ProofResolvesActualCatalogueAndCompleteThreeLapObjectives(string mode, RaceFormat format)
        {
            var p = WordboundPuzzleProof.Create(itemJson, mode, format);
            Assert.AreEqual(3, p.Course.Laps.Count);
            CollectionAssert.AreEquivalent(new[] { "BED", "STOOL" }, p.Course.Puzzles.Select(x => x.RecipeId));
            Assert.IsTrue(p.Course.Puzzles.All(x => x.PresetId == "PlaceObject"));
            Assert.IsTrue(CourseValidator.ValidateResolved(p.Course, p.Geometry, MatchStateValidation.Requirements(p.Mode)).Ok);
            foreach (var asset in p.Geometry.Assets.Values)
            {
                var item = p.Items.Get(asset.Id);
                Assert.AreEqual((int)Math.Ceiling(item.Size[0] * 1000d), asset.WidthMm);
                Assert.AreEqual((int)Math.Ceiling(item.Size[1] * 1000d), asset.HeightMm);
            }
        }

        [Test]
        public void ProofBundlePinsExactSourceAndDoesNotExposeMutableCatalogue()
        {
            var again = WordboundPuzzleProof.Create(itemJson);
            Assert.AreEqual(proof.CatalogueHash, again.CatalogueHash);
            var changedSource = WordboundPuzzleProof.Create(itemJson + "\n");
            Assert.AreEqual(proof.Course.Hash, changedSource.Course.Hash);
            Assert.AreNotEqual(proof.CatalogueHash, changedSource.CatalogueHash);
            var mutable = proof.Items; mutable.Get("BED").Deploy.Strength = 100;
            Assert.AreNotEqual(100, proof.Items.Get("BED").Deploy.Strength);
            Assert.Throws<NotSupportedException>(() => WordboundPuzzleProof.Create(itemJson, "Freestyle"));
            Assert.Throws<ArgumentException>(() => WordboundPuzzleProof.Create(itemJson, seed: "not-a-seed"));
        }

        [Test]
        public void PhysicalPlanRejectsAnotherSourceManifestOrSignedContentBundle()
        {
            MatchBinding Binding(string source, string manifest, string bundle) => new MatchBinding("proof", "wordbound-v1",
                proof.Mode.Hash, source, manifest, new string('a', 64), bundle, new[] { new WordboundSeat("a", 0) });
            Assert.DoesNotThrow(() => proof.Validate(Binding(proof.Course.SourceHash, proof.Course.Hash, proof.CatalogueHash)));
            Assert.Throws<ArgumentException>(() => proof.Validate(Binding(new string('b', 64), proof.Course.Hash, proof.CatalogueHash)));
            Assert.Throws<ArgumentException>(() => proof.Validate(Binding(proof.Course.SourceHash, new string('b', 64), proof.CatalogueHash)));
            Assert.Throws<ArgumentException>(() => proof.Validate(Binding(proof.Course.SourceHash, proof.Course.Hash, new string('b', 64))));
        }

        [Test]
        public void SocketRequiresUprightOrientedActiveDeploymentAtTheAuthoredPosition()
        {
            var socket = proof.Plan.Sockets[0];
            WorldBodyState Body(float x = 0, float yaw = 0, float tilt = 0, bool active = true, bool kinematic = true)
            {
                double y = yaw * Math.PI / 360, t = tilt * Math.PI / 360;
                return new WorldBodyState("item:1", WorldBodyKind.Item, new WorldVector(x, 0, 0),
                    new WorldVector((float)Math.Sin(t), (float)Math.Sin(y), 0),
                    (float)(Math.Cos(t) * Math.Cos(y)), active: active, kinematic: kinematic);
            }
            Assert.IsTrue(WordboundPuzzlePolicy.InSocket(socket, Body()));
            Assert.IsTrue(WordboundPuzzlePolicy.InSocket(socket, Body(.25f, 14)));
            Assert.IsFalse(WordboundPuzzlePolicy.InSocket(socket, Body(.251f)));
            Assert.IsFalse(WordboundPuzzlePolicy.InSocket(socket, Body(yaw: 16)));
            Assert.IsFalse(WordboundPuzzlePolicy.InSocket(socket, Body(tilt: 6)));
            Assert.IsFalse(WordboundPuzzlePolicy.InSocket(socket, Body(active: false)));
            Assert.IsFalse(WordboundPuzzlePolicy.InSocket(socket, Body(kinematic: false)));
        }

        [Test]
        public void PlatformCycleIsBoundedContinuousAndReconstructsFromLargeCanonicalTicks()
        {
            foreach (var path in proof.Plan.Platforms)
            {
                int period = 2 * (path.TravelTicks + path.DwellTicks);
                Assert.AreEqual(0, path.From.DistanceSquared(path.Position(0)));
                Assert.AreEqual(0, path.From.DistanceSquared(path.Position(path.DwellTicks)));
                Assert.AreEqual(0, path.To.DistanceSquared(path.Position(path.TravelTicks + path.DwellTicks)));
                Assert.AreEqual(0, path.From.DistanceSquared(path.Position(period)));
                Assert.AreEqual(0, path.Position(long.MaxValue % period).DistanceSquared(path.Position(long.MaxValue)));
                for (int tick = 1; tick <= period; tick++)
                    Assert.LessOrEqual(path.Position(tick).DistanceSquared(path.Position(tick - 1)), .0011f);
                Assert.Throws<ArgumentOutOfRangeException>(() => path.Position(-1));
            }
        }

        [Test]
        public void HoldRequiresContinuousSameItemAndRestartsAfterMissingOrSkippedSamples()
        {
            var state = new MatchMechanismState("BedBridge", 0, 0);
            var first = WordboundPuzzlePolicy.Observe(state, 10, "item:1");
            Assert.AreEqual(11, first.PhaseTick); Assert.AreEqual(1, first.ProgressTicks);
            state = new MatchMechanismState("BedBridge", 0, 0, activatedTick: 11, phaseTick: 11, progressTicks: 1, itemEntityId: "item:1");
            var next = WordboundPuzzlePolicy.Observe(state, 11, "item:1");
            Assert.AreEqual(11, next.PhaseTick); Assert.AreEqual(2, next.ProgressTicks);
            Assert.AreEqual(13, WordboundPuzzlePolicy.Observe(state, 12, "item:1").PhaseTick);
            Assert.AreEqual(12, WordboundPuzzlePolicy.Observe(state, 11, "item:2").PhaseTick);
            var reset = WordboundPuzzlePolicy.Observe(state, 11, "");
            Assert.IsFalse(reset.Active); Assert.AreEqual(0, reset.ProgressTicks);
            Assert.Throws<ArgumentException>(() => WordboundPuzzlePolicy.Observe(state, long.MaxValue, "item:1"));
        }

        [Test]
        public void TimerSurvivesRealCanonicalSnapshotRoundTripAndResumesWithoutLocalState()
        {
            var f = new Fixture(proof);
            string id = f.CraftAndDeployBed();
            var socket = proof.Plan.Sockets.Single(s => s.PuzzleId == "BedBridge");
            for (int i = 0; i < socket.HoldTicks; i++)
            {
                var state = f.Frame.Mechanisms.Single(m => m.PuzzleId == socket.PuzzleId && m.Team == 0 && m.Lap == 0);
                f.Step(timing: WordboundPuzzlePolicy.Observe(state, f.Frame.Progress.Tick, id));
            }
            var frame = f.Frame;
            var imported = MatchSnapshotCodec.Import(MatchSnapshotCodec.Export(frame), frame.Binding, proof.Mode, proof.Course, f.Items, f.Rules);
            Assert.IsTrue(imported.Success, imported.Error);
            var retained = imported.Snapshot.Mechanisms.Single(m => m.PuzzleId == socket.PuzzleId && m.Team == 0 && m.Lap == 0);
            Assert.IsTrue(WordboundPuzzlePolicy.Ready(socket, retained, frame.Progress.Tick, id));
            var resumed = WordboundPuzzlePolicy.Observe(retained, frame.Progress.Tick, id);
            Assert.AreEqual(retained.PhaseTick, resumed.PhaseTick);
            Assert.AreEqual(retained.ProgressTicks + 1, resumed.ProgressTicks);
            Assert.IsFalse(WordboundPuzzlePolicy.Ready(socket, retained, frame.Progress.Tick + 1, id));
            Assert.IsFalse(WordboundPuzzlePolicy.LiveRecipe(frame, frame.Bodies.Single(b => b.Id == id), "BED", 1));
        }

        sealed class Query : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => true;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => true;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState body) => false;
        }
        sealed class Fixture
        {
            readonly AuthoritativeMatch match;
            long sequence;
            public ItemCatalogue Items { get; }
            public GameRules Rules { get; }
            public MatchSnapshot Frame => match.Capture(1);
            public Fixture(WordboundPuzzleProof p)
            {
                Items = p.Items; Rules = p.Mode.CreateGameplayRules(new GameRules());
                var binding = new MatchBinding("proof", "wordbound-v1", p.Mode.Hash, p.Course.SourceHash, p.Course.Hash,
                    new string('a', 64), p.CatalogueHash, new[] { new WordboundSeat("a", 0) });
                match = new AuthoritativeMatch(binding, p.Mode, p.Course, p.Geometry, Items, Rules, new Query());
            }
            public void Step(PlayerCommand command = null, WorldVector? player = null, HostMechanismObservation timing = null)
            {
                var pose = Frame.Bodies.Single(b => b.Id == "player:0");
                var result = match.Advance((match.Tick + 1) * 17, command == null ? null : new[] { new AuthenticatedCommand("a", command) },
                    player.HasValue ? new[] { pose.At(player.Value) } : null, hostMechanisms: timing == null ? null : new[] { timing });
                Assert.IsTrue(result.Advanced);
                if (command != null) Assert.AreEqual("Ok", result.Receipts.Single().Effect.Code);
                if (match.Tick % 30 == 0) Assert.IsTrue(match.Acknowledge(match.PublishSnapshot(out _)));
            }
            PlayerCommand Command(WordboundAction action, string target = "", int slot = -1, string value = "") =>
                new PlayerCommand(++sequence, match.Epoch, action, target, slot: slot, value: value);
            public string CraftAndDeployBed()
            {
                foreach (var tile in Frame.Bodies.Where(b => b.Kind == WorldBodyKind.Tile && b.SourceId.Contains("BedSupply")).ToArray())
                    Step(Command(WordboundAction.CollectLetter, tile.Id), tile.Position);
                Step(Command(WordboundAction.BeginCraft, value: "BED"));
                for (int i = 0; Frame.Economy.Players[0].Craft != null && i < 600; i++) Step();
                Assert.IsNull(Frame.Economy.Players[0].Craft);
                Step(Command(WordboundAction.Deploy, slot: 0), new WorldVector(0, 0, -1.5f));
                return "item:" + Frame.Economy.Items.Single(i => i.Word == "BED" && i.Origin == ItemOrigin.Crafted).Id;
            }
        }
    }
}
