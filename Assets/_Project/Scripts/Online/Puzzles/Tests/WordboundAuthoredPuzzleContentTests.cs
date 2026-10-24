using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoredPuzzleContentTests
    {
        string items, rules;
        WordboundPuzzleProof proof;
        ResolvedCourse course;
        WordboundAuthoredPuzzleContent observationContent;
        MatchSnapshot observationFrame;

        [OneTimeSetUp]
        public void ReadPinnedGameplaySources()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string folder = Path.Combine(dir.FullName, "Assets/_Project/Data/Config");
                    if (!File.Exists(Path.Combine(folder, "items.json"))) continue;
                    items = File.ReadAllText(Path.Combine(folder, "items.json"));
                    rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
                    proof = WordboundPuzzleProof.Create(items, ruleBookJson: rules);
                    var document = proof.Course.CopyDocument();
                    document.Schema = 2;
                    document.PhysicalSites.AddRange(proof.Plan.Sites.Select(s => new CoursePhysicalSite
                    {
                        Lap = s.Lap + 1, PuzzleId = s.PuzzleId, CheckpointId = s.CheckpointId, RecoverySupplyId = s.RecoverySupplyId,
                        Position = new CoursePoint((int)(s.Socket.Position.X * 1000), (int)(s.Socket.Position.Y * 1000), (int)(s.Socket.Position.Z * 1000))
                    }));
                    var resolved = CourseResolver.Resolve(document, proof.Geometry, new CourseResolveRequest(proof.Course.Seed, proof.Course.Play));
                    Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
                    course = resolved.Manifest;
                    return;
                }
            Assert.Fail("The actual item catalogue and rule book are required.");
        }

        WordboundAuthoredPuzzleContent Content(WordboundPuzzlePlan plan = null, string itemSource = null, string ruleSource = null) =>
            new WordboundAuthoredPuzzleContent(proof.Mode, proof.Geometry, course,
                plan ?? new WordboundPuzzlePlan(course), itemSource ?? items, ruleSource ?? rules);

        static WordboundPhysicalSite Copy(WordboundPhysicalSite site, int? lap = null, WordboundPlacementSocket socket = null,
            string checkpoint = null, string recovery = null) =>
            new WordboundPhysicalSite(lap ?? site.Lap, checkpoint ?? site.CheckpointId, recovery ?? site.RecoverySupplyId,
                socket ?? site.Socket);

        [Test]
        public void AuthoredPlanUsesExplicitSitesAndIsIndependentOfEnumerationOrder()
        {
            var ordered = new WordboundPuzzlePlan(course);
            var shuffled = new WordboundPuzzlePlan(course, ordered.Sites.Reverse());
            Assert.AreEqual(ordered.Hash, shuffled.Hash);
            Assert.AreEqual(6, ordered.Sites.Count);
            Assert.AreEqual(0, ordered.Platforms.Count, "An authored course must not inherit proof-only UpA/UpB platform motion.");
            var draft = course.CopyDocument(); draft.PhysicalSites[0].HoldTicks++;
            var changed = CourseResolver.Resolve(draft, proof.Geometry, new CourseResolveRequest(course.Seed, course.Play));
            Assert.IsTrue(changed.Success);
            Assert.AreNotEqual(ordered.Hash, new WordboundPuzzlePlan(changed.Manifest).Hash);
        }

        [TestCase("missing"), TestCase("duplicate"), TestCase("checkpoint"), TestCase("recovery"), TestCase("position")]
        public void MissingOrUnboundPhysicalSitesCannotEnterTheContentBundle(string mutation)
        {
            var sites = new WordboundPuzzlePlan(course).Sites.ToArray();
            if (mutation == "missing") sites = sites.Skip(1).ToArray();
            else if (mutation == "duplicate") sites[0] = sites[1];
            else
            {
                var s = sites[0].Socket;
                sites[0] = Copy(sites[0],
                    checkpoint: mutation == "checkpoint" ? "foreign" : null,
                    recovery: mutation == "recovery" ? "foreign" : null,
                    socket: mutation == "position" ? new WordboundPlacementSocket(s.PuzzleId, new WorldVector(999, 0, 0),
                        s.Yaw, s.RadiusMm, s.YawToleranceDegrees, s.HoldTicks) : null);
            }
            Assert.Throws<ArgumentException>(() => new WordboundPuzzlePlan(course, sites));
        }

        [Test]
        public void AuthoredAdmissionPinsExactSiteItemAndRuleSourcesAndDefendsMutableCopies()
        {
            var content = Content();
            var binding = new MatchBinding("authored", MatchSnapshotCodec.ProtocolVersion, content.Mode.Hash,
                content.Course.SourceHash, content.Course.Hash, new string('a', 64), content.CatalogueHash,
                new[] { new WordboundSeat("a", 0) });
            Assert.IsTrue(content.BindsRules);
            Assert.DoesNotThrow(() => content.Validate(binding, content.Rules));
            Assert.AreNotEqual(content.CatalogueHash, Content(itemSource: items + "\n").CatalogueHash);
            Assert.AreNotEqual(content.CatalogueHash, Content(ruleSource: rules + "\n").CatalogueHash);
            var changed = content.Rules; changed.MaxHealth += 1;
            Assert.Throws<ArgumentException>(() => content.Validate(binding, changed));
            Assert.AreNotEqual(changed.MaxHealth, content.Rules.MaxHealth);
            var catalogue = content.Items; catalogue.Get("FAN").Deploy.Strength = 0;
            Assert.Greater(content.Items.Get("FAN").Deploy.Strength, 0);
        }

        [Test]
        public void AuthoredContentRejectsMissingOrOverNestedSources()
        {
            var plan = new WordboundPuzzlePlan(course);
            Assert.Throws<ArgumentException>(() => new WordboundAuthoredPuzzleContent(proof.Mode, proof.Geometry,
                course, plan, items, null));
            Assert.Throws<ArgumentException>(() => Content(ruleSource: new string('[', 17) + new string(']', 17)));
            Assert.Throws<ArgumentException>(() => Content(itemSource: "{}]"));
            Assert.Throws<ArgumentException>(() => Content(ruleSource: "{\"unterminated"));
        }

        [Test]
        public void SchemaOneRemainsByteCompatibleAndSchemaTwoRoundTripsImmutableProfiles()
        {
            string legacy = CourseJson.ExportDocument(proof.Course.CopyDocument());
            Assert.IsFalse(legacy.Contains("physicalSites"));
            Assert.AreEqual(legacy, CourseJson.ExportDocument(CourseJson.ImportDocument(legacy).Document));
            string wire = CourseJson.ExportManifest(course);
            var imported = CourseJson.ImportManifest(wire, proof.Geometry);
            Assert.IsTrue(imported.Success, string.Join("\n", imported.Issues));
            Assert.AreEqual(wire, CourseJson.ExportManifest(imported.Manifest));
            Assert.AreEqual(2, imported.Manifest.Schema);
            var copy = imported.Manifest.CopyDocument(); copy.PhysicalSites[0].HoldTicks++;
            Assert.AreNotEqual(copy.PhysicalSites[0].HoldTicks, imported.Manifest.PhysicalSites[0].HoldTicks);
            Assert.IsFalse(CourseJson.ImportDocument(CourseJson.ExportDocument(copy).Replace("\"schema\":2", "\"schema\":1")).Success);
        }

        [Test]
        public void WorkshopDraftCanTemporarilyLoseLinksButCannotResolveForPlay()
        {
            var draft = course.CopyDocument();
            draft.PhysicalSites[0].CheckpointId = "NotLinkedYet";
            Assert.IsTrue(CourseJson.ImportDocument(CourseJson.ExportDocument(draft)).Success);
            Assert.IsFalse(CourseResolver.Resolve(draft, proof.Geometry, new CourseResolveRequest(course.Seed, course.Play)).Success);
        }

        [TestCase("duplicate"), TestCase("range"), TestCase("depth"), TestCase("unknown")]
        public void MalformedPhysicalCourseDataIsRejectedBeforeUse(string mutation)
        {
            var draft = course.CopyDocument();
            if (mutation == "duplicate") draft.PhysicalSites.Add(draft.PhysicalSites[0]);
            else if (mutation == "range") draft.PhysicalSites[0].HoldTicks = int.MaxValue;
            if (mutation == "duplicate" || mutation == "range")
                Assert.Throws<ArgumentException>(() => CourseJson.ExportDocument(draft));
            else
            {
                string wire = CourseJson.ExportDocument(draft);
                wire = mutation == "depth" ? wire.Replace("\"physicalSites\":[", "\"physicalSites\":[" + new string('[', 30)) :
                    wire.Replace("\"physicalSites\":[{", "\"physicalSites\":[{\"clientSolved\":true,");
                Assert.IsFalse(CourseJson.ImportDocument(wire).Success);
            }
        }

        [Test]
        public void ObservationLookupRetainsActualAuthoredTeamSiteOrderAndExactReferences()
        {
            var frame = ObservationFrame();
            string retained = MatchSnapshotCodec.Export(frame);
            var states = new WordboundPuzzleRuntime.AuthoredMechanismLookup(frame);
            var expected = new List<MatchMechanismState>();
            var actual = new List<MatchMechanismState>();
            Assert.AreEqual(2, frame.Progress.Teams.Count);
            Assert.AreEqual(72, observationContent.Plan.Sites.Count);
            Assert.AreEqual(432, frame.Mechanisms.Count);
            foreach (var team in frame.Progress.Teams.Where(t => !t.Finished))
                foreach (var site in observationContent.Plan.Sites)
                {
                    expected.Add(Original(frame, team.Team, site.Lap, site.PuzzleId));
                    actual.Add(states.Single(team.Team, site.Lap, site.PuzzleId));
                }
            Assert.AreEqual(144, actual.Count);
            for (int i = 0; i < actual.Count; i++)
            {
                Assert.AreSame(expected[i], actual[i]);
                Assert.AreSame(actual[i], states.Single(actual[i].Team, actual[i].Lap, actual[i].PuzzleId));
            }
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(frame));
        }

        [TestCase(false), TestCase(true)]
        public void ObservationLookupRetainsMissingAndDuplicatePredicateSingleErrors(bool duplicate)
        {
            var frame = ObservationFrame();
            var target = frame.Mechanisms[0];
            var safe = frame.Mechanisms.First(m => m.Team != target.Team);
            var records = duplicate ? frame.Mechanisms.Concat(new[] { target }) : frame.Mechanisms.Where(m => m != target);
            var malformed = WithMechanisms(frame, records);
            string retained = MatchSnapshotCodec.Export(malformed);
            var states = new WordboundPuzzleRuntime.AuthoredMechanismLookup(malformed);
            Assert.AreSame(safe, states.Single(safe.Team, safe.Lap, safe.PuzzleId));
            var expected = Assert.Throws<InvalidOperationException>(() => Original(malformed, target.Team, target.Lap, target.PuzzleId));
            var actual = Assert.Throws<InvalidOperationException>(() => states.Single(target.Team, target.Lap, target.PuzzleId));
            Assert.AreEqual(expected.Message, actual.Message);
            Assert.AreSame(safe, states.Single(safe.Team, safe.Lap, safe.PuzzleId));
            var fresh = new WordboundPuzzleRuntime.AuthoredMechanismLookup(frame);
            Assert.AreSame(target, fresh.Single(target.Team, target.Lap, target.PuzzleId));
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(malformed));
        }

        [TestCase(false), TestCase(true)]
        public void ObservationLookupRejectsOnlyTheFirstQueriedFaultInOriginalOrder(bool duplicateFirst)
        {
            var frame = ObservationFrame();
            var missing = frame.Mechanisms[0];
            var duplicate = frame.Mechanisms[1];
            var safe = frame.Mechanisms[2];
            var malformed = WithMechanisms(frame, frame.Mechanisms.Where(m => m != missing).Concat(new[] { duplicate }));
            var queries = duplicateFirst ? new[] { safe, duplicate, missing } : new[] { safe, missing, duplicate };
            var states = new WordboundPuzzleRuntime.AuthoredMechanismLookup(malformed);
            var expectedPrefix = new List<MatchMechanismState>();
            var actualPrefix = new List<MatchMechanismState>();
            var expected = Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var q in queries) expectedPrefix.Add(Original(malformed, q.Team, q.Lap, q.PuzzleId));
            });
            var actual = Assert.Throws<InvalidOperationException>(() =>
            {
                foreach (var q in queries) actualPrefix.Add(states.Single(q.Team, q.Lap, q.PuzzleId));
            });
            Assert.AreEqual(expected.Message, actual.Message);
            Assert.AreEqual(1, expectedPrefix.Count);
            Assert.AreEqual(1, actualPrefix.Count);
            Assert.AreSame(safe, actualPrefix[0]);
        }

        [Test]
        public void ObservationLookupPreservesOrdinalPuzzleIdentityBesideAnUnqueriedDuplicate()
        {
            var frame = ObservationFrame();
            var original = frame.Mechanisms[0];
            string alternateId = original.PuzzleId.ToLowerInvariant();
            if (alternateId == original.PuzzleId) alternateId = original.PuzzleId.ToUpperInvariant();
            Assert.AreNotEqual(original.PuzzleId, alternateId);
            Assert.IsFalse(frame.Mechanisms.Any(m => m.PuzzleId == alternateId));
            var alternate = new MatchMechanismState(alternateId, original.Team, original.Lap);
            var unrelated = frame.Mechanisms.First(m => m.Team != original.Team);
            var mixed = WithMechanisms(frame, frame.Mechanisms.Concat(new[] { alternate, unrelated }));
            var states = new WordboundPuzzleRuntime.AuthoredMechanismLookup(mixed);
            Assert.AreSame(original, states.Single(original.Team, original.Lap, original.PuzzleId));
            Assert.AreSame(alternate, states.Single(alternate.Team, alternate.Lap, alternate.PuzzleId));
            Assert.AreSame(Original(mixed, original.Team, original.Lap, original.PuzzleId),
                states.Single(original.Team, original.Lap, original.PuzzleId));
        }

        [Test]
        public void ObservationLookupOwnsOneFrozenFrameAcrossCallerMutationAndReplacement()
        {
            var frame = ObservationFrame();
            var site = observationContent.Plan.Sites.First(s => s.Authored.Fixtures.Count > 0);
            var records = frame.Mechanisms.ToArray();
            int at = Array.FindIndex(records, m => m.Team == 0 && m.Lap == site.Lap && m.PuzzleId == site.PuzzleId);
            Assert.GreaterOrEqual(at, 0);
            var original = records[at];
            var frozen = WithMechanisms(frame, records);
            string retained = MatchSnapshotCodec.Export(frozen);
            var oldLookup = new WordboundPuzzleRuntime.AuthoredMechanismLookup(frozen);
            records[at] = new MatchMechanismState(original.PuzzleId, original.Team, original.Lap, solved: true,
                physical: original.Physical.WithMotion(CourseFixtureMotion.UnitsPerTick));
            Assert.AreSame(original, oldLookup.Single(original.Team, original.Lap, original.PuzzleId));
            var replacement = WithMechanisms(frame, records);
            var newLookup = new WordboundPuzzleRuntime.AuthoredMechanismLookup(replacement);
            var changed = newLookup.Single(original.Team, original.Lap, original.PuzzleId);
            Assert.AreSame(records[at], changed);
            Assert.IsTrue(changed.Solved);
            Assert.AreEqual(CourseFixtureMotion.UnitsPerTick, changed.Physical.MotionUnits);
            Assert.IsFalse(oldLookup.Single(original.Team, original.Lap, original.PuzzleId).Solved);
            Assert.AreEqual(0, original.Physical.MotionUnits);
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(frozen));
        }

        [Test]
        public void ObservationLookupSeesRealAdvancedFixtureMotionWithoutChangingRetainedFrame()
        {
            var match = ObservationMatch();
            var before = match.Capture(1);
            string retained = MatchSnapshotCodec.Export(before);
            var site = observationContent.Plan.Sites.First(s => s.Authored.Fixtures.Count > 0);
            var previous = Original(before, 0, site.Lap, site.PuzzleId);
            var oldLookup = new WordboundPuzzleRuntime.AuthoredMechanismLookup(before);
            Assert.AreSame(previous, oldLookup.Single(0, site.Lap, site.PuzzleId));
            var poses = site.Authored.Fixtures.Select(f => before.Bodies.Single(b => b.Id == CourseFixtureMotion.BodyId(f.Id, 0))
                .At(CourseFixtureMotion.Position(f, CourseFixtureMotion.UnitsPerTick))).ToArray();
            var timing = new HostMechanismObservation(site.PuzzleId, 0, site.Lap, false,
                physical: previous.Physical.WithMotion(CourseFixtureMotion.UnitsPerTick));
            Assert.IsTrue(match.Advance(17, hostBodyStates: poses, hostMechanisms: new[] { timing }).Advanced);
            var after = match.Capture(1);
            Assert.AreEqual("", MatchStateValidation.Validate(after, after.Binding, observationContent.Mode,
                observationContent.Course, observationContent.Items, observationContent.Rules));
            var current = new WordboundPuzzleRuntime.AuthoredMechanismLookup(after).Single(0, site.Lap, site.PuzzleId);
            Assert.AreSame(Original(after, 0, site.Lap, site.PuzzleId), current);
            Assert.AreNotSame(previous, current);
            Assert.AreEqual(CourseFixtureMotion.UnitsPerTick, current.Physical.MotionUnits);
            foreach (var fixture in site.Authored.Fixtures)
                Assert.AreEqual(0, after.Bodies.Single(b => b.Id == CourseFixtureMotion.BodyId(fixture.Id, 0)).Position
                    .DistanceSquared(CourseFixtureMotion.Position(fixture, current.Physical.MotionUnits)));
            Assert.AreSame(previous, oldLookup.Single(0, site.Lap, site.PuzzleId));
            Assert.AreEqual(retained, MatchSnapshotCodec.Export(before));
        }

        MatchSnapshot ObservationFrame() => observationFrame ??= ObservationMatch().Capture(1);

        AuthoritativeMatch ObservationMatch()
        {
            if (observationContent == null)
            {
                var flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items));
                var mode = ModeDefinition.For("TeamRace", RaceFormat.Solo);
                var resolved = flagship.Resolve("0123456789abcdef", MatchStateValidation.Requirements(mode));
                Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
                observationContent = new WordboundAuthoredPuzzleContent(mode, flagship.Geometry, resolved.Manifest, items, rules);
            }
            var content = observationContent;
            var binding = new MatchBinding("observe-lookup", MatchSnapshotCodec.ProtocolVersion, content.Mode.Hash,
                content.Course.SourceHash, content.Course.Hash, new string('a', 64), content.CatalogueHash,
                new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) });
            return new AuthoritativeMatch(binding, content.Mode, content.Course, content.Geometry,
                content.Items, content.Rules, new ObservationWorld());
        }

        static MatchMechanismState Original(MatchSnapshot frame, int team, int lap, string puzzleId) =>
            frame.Mechanisms.Single(m => m.Team == team && m.Lap == lap && m.PuzzleId == puzzleId);

        static MatchSnapshot WithMechanisms(MatchSnapshot f, IEnumerable<MatchMechanismState> mechanisms) =>
            new MatchSnapshot(f.Binding, f.Epoch, f.Sequence, f.NextWorldEntityId, f.Progress, f.Economy, f.Commands,
                f.Actors, f.Bodies, f.Resources, mechanisms, f.RandomStreams, f.NarrativeFlags, f.ThrownFlights, f.Practice);

        sealed class ObservationWorld : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState body) => false;
        }
    }
}
