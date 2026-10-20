using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Online;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundPracticeContentTests
    {
        WordboundAuthoredPuzzleContent content;

        [OneTimeSetUp]
        public void ResolveActualAuthoredContentForExplore()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string folder = Path.Combine(dir.FullName, "Assets/_Project/Data/Config");
                    if (!File.Exists(Path.Combine(folder, "items.json"))) continue;
                    string items = File.ReadAllText(Path.Combine(folder, "items.json"));
                    string rules = File.ReadAllText(Path.Combine(folder, "rules.json"));
                    var proof = WordboundPuzzleProof.Create(items, ruleBookJson: rules);
                    var document = proof.Course.CopyDocument();
                    document.Schema = 2;
                    document.SupportedModes.Add("Freestyle");
                    foreach (var connection in document.Connections) connection.GatePuzzleId = "";
                    document.PhysicalSites.AddRange(proof.Plan.Sites.Select(s => new CoursePhysicalSite
                    {
                        Lap = s.Lap + 1, PuzzleId = s.PuzzleId, CheckpointId = s.CheckpointId,
                        RecoverySupplyId = s.RecoverySupplyId,
                        Position = new CoursePoint((int)(s.Socket.Position.X * 1000),
                            (int)(s.Socket.Position.Y * 1000), (int)(s.Socket.Position.Z * 1000))
                    }));
                    var mode = ModeDefinition.For("Freestyle");
                    var resolved = CourseResolver.Resolve(document, proof.Geometry,
                        new CourseResolveRequest(proof.Course.Seed, MatchStateValidation.Requirements(mode)));
                    Assert.IsTrue(resolved.Success, string.Join("\n", resolved.Issues));
                    content = new WordboundAuthoredPuzzleContent(mode, proof.Geometry, resolved.Manifest, items, rules);
                    return;
                }
            Assert.Fail("Actual repository gameplay sources are required.");
        }

        [Test]
        public void ExplorePlanIncludesAllAuthoredStationsWithoutSelectingRaceLaps()
        {
            Assert.AreEqual(0, content.Course.Laps.Count);
            Assert.AreEqual(6, content.Plan.Sites.Count);
            CollectionAssert.AreEquivalent(content.Course.PhysicalSites.Select(s => s.Hash), content.Plan.Sites.Select(s => s.Hash));
            Assert.IsTrue(content.BindsRules);
            Assert.AreEqual(content.Plan.Hash, new WordboundPuzzlePlan(content.Course, content.Plan.Sites.Reverse()).Hash);
        }

        [Test]
        public void MissingOrForeignPracticeStationFailsBeforeAdmission()
        {
            Assert.Throws<ArgumentException>(() => new WordboundPuzzlePlan(content.Course, content.Plan.Sites.Skip(1)));
            var first = content.Plan.Sites[0];
            var changed = new WordboundPhysicalSite(first.Lap, first.CheckpointId, first.RecoverySupplyId, first.Socket);
            Assert.Throws<ArgumentException>(() => new WordboundPuzzlePlan(content.Course,
                content.Plan.Sites.Skip(1).Concat(new[] { changed })));
        }

        [Test]
        public void PracticeBundlePinsExactModeRulesAndManifest()
        {
            var binding = new MatchBinding("practice-content", MatchSnapshotCodec.ProtocolVersion, content.Mode.Hash,
                content.Course.SourceHash, content.Course.Hash, new string('a', 64), content.CatalogueHash,
                new[] { new WordboundSeat("a", 0) });
            Assert.DoesNotThrow(() => content.Validate(binding, content.Rules));
            var changed = content.Rules; changed.MaxHealth++;
            Assert.Throws<ArgumentException>(() => content.Validate(binding, changed));
            Assert.AreEqual(0, content.Mode.TimeLimitSeconds);
            Assert.IsFalse(content.Mode.EnemyDamage);
            Assert.IsFalse(content.Mode.HazardDamage);
        }
    }
}
