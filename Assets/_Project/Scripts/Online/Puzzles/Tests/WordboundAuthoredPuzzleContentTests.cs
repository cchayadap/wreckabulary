using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundAuthoredPuzzleContentTests
    {
        string items, rules;
        WordboundPuzzleProof proof;
        ResolvedCourse course;

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
    }
}
