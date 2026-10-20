using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPhysicalEvidenceTests
    {
        static WordboundAuthorityTests.Fixture Create(bool machinery = false)
        {
            var f = WordboundAuthorityTests.Create();
            var source = f.Source;
            var draft = source.Course.CopyDocument();
            var puzzle = draft.Puzzles.Single(p => p.Id == "BedBridge");
            puzzle.PresetId = "PlaceObject";
            var geometry = CourseTemplates.ProofCatalogue();
            var site = new CoursePhysicalSite
            {
                Lap = 1, PuzzleId = puzzle.Id, CheckpointId = draft.Checkpoints.First(c => c.PuzzleIds.Contains(puzzle.Id)).Id,
                RecoverySupplyId = "BedSupply", Position = new CoursePoint(0, 0, 0)
            };
            if (machinery)
            {
                var clock = source.Catalogue.Get("CLOCK");
                geometry = new CourseCatalogue(geometry.Modules.Values, geometry.Assets.Values.Concat(new[]
                    { new CourseAssetDefinition("CLOCK", "CLOCK", (int)(clock.Size[0] * 1000),
                        (int)(clock.Size[2] * 1000), (int)(clock.Size[1] * 1000)) }),
                    geometry.PuzzlePresets, geometry.Themes, geometry.NarrativeEvents);
                puzzle.PresetId = "TimedMechanism"; puzzle.RecipeId = puzzle.RequiredLetters = "CLOCK";
                draft.Supplies.Single(s => s.Id == "BedSupply").Letters = "CLOCK";
                site.ActiveWindowTicks = 120;
                site.ArrivalBounds = new CourseBox(new CoursePoint(-250, 0, 1500), new CoursePoint(250, 1800, 2000));
                site.Fixtures.Add(new CourseMovingFixture
                {
                    Id = "SortingArm", From = new CoursePoint(-1000, 0, 1000), To = new CoursePoint(1000, 0, 1000),
                    SizeMm = new CoursePoint(600, 1200, 200), PeriodTicks = 240, PhaseTicks = 40
                });
            }
            draft.PhysicalSites.Add(site);
            var resolved = CourseResolver.Resolve(draft, geometry,
                new CourseResolveRequest(source.Course.Seed, source.Course.Play));
            Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
            source.Course = resolved.Manifest;
            source.Binding = new MatchBinding(source.Binding.MatchId, MatchSnapshotCodec.ProtocolVersion,
                source.Mode.Hash, source.Course.SourceHash, source.Course.Hash, source.Binding.RosterHash,
                source.Binding.CatalogueHash, source.Binding.Seats);
            f.Match = new AuthoritativeMatch(source.Binding, source.Mode, source.Course,
                geometry, source.Catalogue, source.Rules, f.World);
            return f;
        }

        static string DeployBed(WordboundAuthorityTests.Fixture f)
        {
            string item = f.CraftBed();
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.Deploy, slot: 0)).Receipts.Single().Effect.Code);
            return item;
        }

        static HostMechanismObservation Observation(WordboundAuthorityTests.Fixture f, string item, MatchPhysicalStage stage,
            long phase, int ticks, long stageTick, string actor = "a", string hash = null, string auxiliary = "") =>
            new HostMechanismObservation("BedBridge", 0, 0, true, phase, ticks, item,
                new MatchPhysicalEvidence(stage, actor, auxiliary, stageTick, hash ?? f.Source.Course.PhysicalSites[0].Hash));

        [Test]
        public void RecipeAndHostBooleanAloneCannotCompleteAnAuthoredPhysicalPuzzle()
        {
            var f = Create(); string item = DeployBed(f);
            Assert.AreEqual("MechanismIncomplete", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item))
                .Receipts.Single().Effect.Code);
            Assert.IsFalse(f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0).Solved);
        }

        [Test]
        public void CompletedPhysicalStagePersistsThroughCheckpointAndMigrationWithoutReplayingTheCrossing()
        {
            var f = Create(); string item = DeployBed(f);
            long start = f.Match.Tick + 1;
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Armed, start, 1, start) });
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Complete, start, 2, start + 1) });
            var stamp = f.Match.PublishSnapshot(out string json);
            var restored = f.Source.Import(json);
            Assert.IsTrue(restored.Success, restored.Error);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(restored.Snapshot, stamp, 1, f.State.Progress.WallClockMs + 1000);
            Assert.AreEqual(MatchPhysicalStage.Complete, f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0).Physical.Stage);
            Assert.AreEqual("Ok", f.Step(f.Command("a", WordboundAction.UsePuzzle, "BedBridge", value: item)).Receipts.Single().Effect.Code);
            f.Ack();
        }

        [TestCase("skip"), TestCase("actor"), TestCase("hash"), TestCase("future"), TestCase("auxiliary")]
        public void ForgedOrSkippedPhysicalStageRejectsTheEntireHostTransaction(string mutation)
        {
            var f = Create(); string item = DeployBed(f);
            long start = f.Match.Tick + 1;
            var observation = Observation(f, item, mutation == "skip" ? MatchPhysicalStage.Complete : MatchPhysicalStage.Armed,
                start, 1, mutation == "future" ? start + 1 : start, mutation == "actor" ? "foreign" : "a",
                mutation == "hash" ? new string('c', 64) : null, mutation == "auxiliary" ? "item:1" : "");
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[] { observation }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void DuplicateObservationCannotAcceleratePhysicalStagesOrMachinery()
        {
            var f = Create(); string item = DeployBed(f);
            long next = f.Match.Tick + 1;
            var observation = Observation(f, item, MatchPhysicalStage.Armed, next, 1, next);
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(17, hostMechanisms: new[] { observation, observation }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [TestCase("missing"), TestCase("foreign-team"), TestCase("wrong-pose"), TestCase("phase-ahead")]
        public void RehashedSnapshotCannotChangeOrOmitAuthoredMachinery(string mutation)
        {
            var f = Create(true);
            var s = f.State;
            var fixture = s.Bodies.Single(b => b.Id.StartsWith("fixture:", StringComparison.Ordinal));
            var bodies = s.Bodies.Where(b => mutation != "missing" || b.Id != fixture.Id).Select(b =>
                b.Id != fixture.Id ? b : mutation == "foreign-team" ?
                    new WorldBodyState(b.Id, b.Kind, b.Position, kinematic: true, protectedResource: true, ownerTeam: 1, sourceId: b.SourceId) :
                mutation == "wrong-pose" ? b.At(new WorldVector(0, 100, 0)) : b);
            var mechanisms = s.Mechanisms.Select(m => mutation == "phase-ahead" && m.PuzzleId == "BedBridge" && m.Lap == 0 ?
                new MatchMechanismState(m.PuzzleId, m.Team, m.Lap, physical: m.Physical.WithMotion(1)) : m);
            var changed = new MatchSnapshot(s.Binding, s.Epoch, s.Sequence, s.NextWorldEntityId, s.Progress, s.Economy,
                s.Commands, s.Actors, bodies, s.Resources, mechanisms, s.RandomStreams, s.NarrativeFlags, s.ThrownFlights);
            Assert.IsFalse(f.Source.Import(MatchSnapshotCodec.Export(changed)).Success);
        }

        [Test]
        public void MachinerySlowPhaseRoundTripsAndRestoresTheExactPosition()
        {
            var f = Create(true);
            var site = f.Source.Course.PhysicalSites[0];
            var fixture = site.Fixtures[0];
            var body = f.State.Bodies.Single(b => b.SourceId == fixture.Id);
            const long units = 600;
            f.Match.Advance(17, hostBodyStates: new[] { body.At(CourseFixtureMotion.Position(fixture, units)) },
                hostMechanisms: new[] { new HostMechanismObservation(site.PuzzleId, 0, 0, false,
                    physical: new MatchPhysicalEvidence(siteHash: site.Hash, motionUnits: units)) });
            var stamp = f.Match.PublishSnapshot(out string json);
            var imported = f.Source.Import(json);
            Assert.IsTrue(imported.Success, imported.Error);
            f.Match.PauseForMigration();
            f.Match.RestoreCommitted(imported.Snapshot, stamp, 1, 1017);
            Assert.AreEqual(units, f.State.Mechanisms.Single(m => m.PuzzleId == site.PuzzleId && m.Lap == 0).Physical.MotionUnits);
            var position = f.State.Bodies.Single(b => b.SourceId == fixture.Id).Position;
            Assert.AreEqual(CourseFixtureMotion.Position(fixture, units).X, position.X);
            Assert.AreNotEqual(CourseFixtureMotion.Position(fixture, 1000).X, position.X);
        }

        [Test]
        public void FixturePoseAndPhaseMustCommitTogetherOrNeitherChanges()
        {
            var f = Create(true);
            var fixture = f.Source.Course.PhysicalSites[0].Fixtures[0];
            var body = f.State.Bodies.Single(b => b.SourceId == fixture.Id);
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(17,
                hostBodyStates: new[] { body.At(CourseFixtureMotion.Position(fixture, 1000)) }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void ActivePhysicalStageCannotBeReassignedOrBackdatedAfterItsFirstObservation()
        {
            var f = Create(); string item = DeployBed(f);
            long start = f.Match.Tick + 1;
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Armed, start, 1, start) });
            string before = MatchSnapshotCodec.Export(f.State);
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Complete, start, 2, start) }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
            Assert.Throws<ArgumentException>(() => f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Complete, start, 2, start + 1, actor: "b") }));
            Assert.AreEqual(before, MatchSnapshotCodec.Export(f.State));
        }

        [Test]
        public void RecipeDestroyedByTheSameTickExpiresPhysicalEvidenceInsteadOfFaultingTheMatch()
        {
            var f = Create(); f.Source.Catalogue.Get("BED").Deploy.LifetimeSeconds = .125f;
            string item = DeployBed(f);
            long start = f.Match.Tick + 1;
            f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Armed, start, 1, start) });
            long expiry = MatchStateValidation.DeploymentExpiryTick(f.Source.Catalogue.Get("BED"),
                f.State.Economy.Items.Single(i => "item:" + i.Id == item).DeploymentTick);
            f.Advance((int)(expiry - f.Match.Tick - 1));
            Assert.DoesNotThrow(() => f.Match.Advance(f.State.Progress.WallClockMs + 17, hostMechanisms: new[]
                { Observation(f, item, MatchPhysicalStage.Armed, start, (int)(expiry - start + 1), start) }));
            Assert.AreEqual(ItemState.Destroyed, f.State.Economy.Items.Single(i => "item:" + i.Id == item).State);
            Assert.AreEqual(MatchPhysicalStage.Waiting, f.State.Mechanisms.Single(m => m.PuzzleId == "BedBridge" && m.Lap == 0).Physical.Stage);
            f.Ack();
        }
    }
}
