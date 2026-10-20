using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public class CourseSeedTests
    {
        [Test]
        public void InstalledPcgMatchesReferenceVectorAndStreamsRemainIndependent()
        {
            var reference = new GameStudioKit.Determinism.Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7U, 0x7b47f409U, 0xba1d3330U, 0x83d2f293U, 0xbfa4784bU, 0xcbed606eU };
            foreach (uint value in expected) Assert.AreEqual(value, reference.NextUInt());
            var first = CourseRandom.Stream("0123456789abcdef", "Props", "ZoneA");
            var repeat = CourseRandom.Stream("0123456789abcdef", "Props", "ZoneA");
            var independent = CourseRandom.Stream("0123456789abcdef", "Decoration", "ZoneA");
            for (int i = 0; i < 100; i++) independent.NextUInt();
            for (int i = 0; i < 20; i++) Assert.AreEqual(first.NextUInt(), repeat.NextUInt());
            var otherScope = CourseRandom.Stream("0123456789abcdef", "Props", "ZoneB");
            var otherLap = CourseRandom.Stream("0123456789abcdef", "Props", "ZoneA", 1);
            var otherAttempt = CourseRandom.Stream("0123456789abcdef", "Props", "ZoneA", 0, 1);
            Assert.AreNotEqual(otherScope.NextUInt(), otherLap.NextUInt());
            Assert.AreNotEqual(otherAttempt.NextUInt(), CourseRandom.Stream("0123456789abcdef", "Props", "ZoneA").NextUInt());
        }
        [TestCase("TimeRun", 4, 4)]
        [TestCase("TeamRace", 1, 2), TestCase("TeamRace", 2, 4), TestCase("TeamRace", 4, 8)]
        [TestCase("BrawlRace", 1, 2), TestCase("BrawlRace", 2, 4), TestCase("BrawlRace", 4, 8)]
        [TestCase("FreeForAll", 1, 2), TestCase("FreeForAll", 1, 8)]
        [TestCase("Freestyle", 0, 1), TestCase("Freestyle", 0, 8)]
        public void OneThousandSeedsPerProofConfiguration(string mode, int capacity, int occupants)
        {
            var cat = CourseTemplates.ProofCatalogue();
            var d = CourseCoreTests.Template(mode);
            var play = CourseCoreTests.Play(mode, capacity, occupants);
            for (int seed = 0; seed < 1000; seed++)
            {
                string hex = seed.ToString("x16", CultureInfo.InvariantCulture);
                var result = CourseResolver.Resolve(d, cat, new CourseResolveRequest(hex, play));
                Assert.IsTrue(result.Success, mode + "/" + seed + "\n" + CourseCoreTests.Errors(result.Issues));
                var verified = CourseValidator.ValidateResolved(result.Manifest, cat, play);
                Assert.IsTrue(verified.Ok, mode + "/" + seed + "\n" + CourseCoreTests.Errors(verified.Issues));
                if (seed % 100 != 0) continue;
                var replay = CourseResolver.Resolve(d, cat, new CourseResolveRequest(hex, play));
                Assert.AreEqual(result.Manifest.Hash, replay.Manifest.Hash);
                var imported = CourseJson.ImportManifest(CourseJson.ExportManifest(result.Manifest), cat);
                Assert.IsTrue(imported.Success, CourseCoreTests.Errors(imported.Issues));
                Assert.AreEqual(result.Manifest.Hash, imported.Manifest.Hash);
            }
        }

        [Test]
        public void CanonicalOrderAndCultureDoNotChangeTheManifest()
        {
            var d = CourseTemplates.RaceCircuit();
            var shuffled = d.Clone();
            shuffled.Modules.Reverse(); shuffled.Layers.Reverse(); shuffled.Puzzles.Reverse();
            shuffled.Supplies.Reverse(); shuffled.Connections.Reverse(); shuffled.Checkpoints.Reverse();
            shuffled.Spawns.Reverse(); shuffled.Zones.Reverse(); shuffled.Laps.Reverse();
            foreach (var zone in shuffled.Zones) { zone.Pool.Reverse(); zone.Yaws.Reverse(); }
            foreach (var section in shuffled.RouteSections) section.Alternatives.Reverse();
            Assert.AreEqual(CourseJson.ExportDocument(d), CourseJson.ExportDocument(shuffled));
            var cat = CourseTemplates.ProofCatalogue();
            var request = new CourseResolveRequest("ffffffffffffffff", CourseCoreTests.Play("BrawlRace"));
            string expected = CourseResolver.Resolve(d, cat, request).Manifest.Hash;
            var before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.AreEqual(expected, CourseResolver.Resolve(shuffled, cat, request).Manifest.Hash);
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        [Test]
        public void AddingDecorationDoesNotChangeRoutesResourcesOrProps()
        {
            var d = CourseTemplates.RaceCircuit();
            var changed = d.Clone();
            changed.Zones.Add(new CourseZone {
                Id = "NewDecoration", ModuleId = "Upper", Purpose = "Decoration",
                Bounds = new CourseBox(new CoursePoint(-9000, 6000, 5000), new CoursePoint(-4000, 6000, 9000)),
                LayerIds = new System.Collections.Generic.List<string> { "Loft" }, MinCount = 1, MaxCount = 2,
                Pool = new System.Collections.Generic.List<CourseWeightedAsset> { new CourseWeightedAsset { AssetId = "PLATE" } }
            });
            var cat = CourseTemplates.ProofCatalogue();
            var request = new CourseResolveRequest("123456789abcdef0", CourseCoreTests.Play("TeamRace"));
            var first = CourseResolver.Resolve(d, cat, request);
            var second = CourseResolver.Resolve(changed, cat, request);
            Assert.IsTrue(first.Success, CourseCoreTests.Errors(first.Issues));
            Assert.IsTrue(second.Success, CourseCoreTests.Errors(second.Issues));
            CollectionAssert.AreEqual(first.Manifest.Laps.SelectMany(x => x.ConnectionIds), second.Manifest.Laps.SelectMany(x => x.ConnectionIds));
            CollectionAssert.AreEqual(first.Manifest.Placements.Select(Stamp), second.Manifest.Placements.Where(p => p.Purpose != "Decoration").Select(Stamp));
            Assert.AreNotEqual(first.Manifest.SourceHash, second.Manifest.SourceHash);
        }

        [TestCase("TeamRace"), TestCase("BrawlRace")]
        public void TeamOpportunitiesAreIdenticalAndCriticalRecoveryIsScoped(string mode)
        {
            var result = CourseResolver.Resolve(CourseTemplates.RaceCircuit(), CourseTemplates.ProofCatalogue(),
                new CourseResolveRequest("1111111111111111", CourseCoreTests.Play(mode)));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            foreach (var p in result.Manifest.Placements.Where(p => p.TeamIndex == 0))
            {
                var peer = result.Manifest.Placements.Single(x => x.Id == p.Id.Substring(0, p.Id.Length - 1) + "1");
                Assert.AreEqual(p.AssetId, peer.AssetId); Assert.AreEqual(p.Position, peer.Position); Assert.AreEqual(p.Yaw, peer.Yaw);
            }
            Assert.IsTrue(result.Manifest.Supplies.All(s => s.TeamIndex == 0 || s.TeamIndex == 1));
            Assert.IsTrue(result.Manifest.Placements.Where(p => p.Purpose == "Resources").All(p => p.TeamIndex >= 0));
            Assert.IsTrue(result.Manifest.Placements.Where(p => p.Purpose == "Props").All(p => mode == "TeamRace" ? p.TeamIndex >= 0 : p.TeamIndex == -1));
        }
        static string Stamp(ResolvedCoursePlacement p) => p.Id + "|" + p.AssetId + "|" + p.Position.X + "|" + p.Position.Y + "|" + p.Position.Z + "|" + p.Yaw;
    }
}
