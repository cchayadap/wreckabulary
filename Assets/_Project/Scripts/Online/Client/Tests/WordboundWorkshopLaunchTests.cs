using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundWorkshopLaunchTests
    {
        const string Seed = "0123456789abcdef";
        string items, rules;
        WordboundFlagshipContent flagship;

        [SetUp]
        public void SetUp()
        {
            string root = Environment.GetEnvironmentVariable("WORDBOUND_PROJECT_ROOT") ?? Directory.GetCurrentDirectory();
            items = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/items.json"));
            rules = File.ReadAllText(Path.Combine(root, "Assets/_Project/Data/Config/rules.json"));
            flagship = WordboundFlagshipContent.Create(ItemCatalogue.FromJson(items));
        }

        [Test]
        public void FreshStarterCreatesSoloAuthoredContentWithoutCompletingItsPhysicalPuzzles()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var draft = preset.Snapshot;
            var preview = CourseResolver.Resolve(draft.Course(), preset.Catalogue, new CourseResolveRequest(draft.Seed, preset.Play));
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            var content = WordboundWorkshopPlaytest.Create(draft, preview.Manifest, preset.Catalogue, preset.Play, items, rules);
            Assert.AreSame(preview.Manifest, content.Course);
            Assert.AreEqual(3, content.Plan.Sites.Count);
            Assert.IsEmpty(content.Plan.Platforms, "The starter must not inherit moving platforms from the old proof.");
            var binding = new MatchBinding("starter-solo", MatchSnapshotCodec.ProtocolVersion, content.Mode.Hash,
                content.Course.SourceHash, content.Course.Hash, new string('a', 64), content.CatalogueHash,
                new[] { new WordboundSeat("creator", 0, 0) });
            var authority = new AuthoritativeMatch(binding, content.Mode, content.Course, content.Geometry,
                content.Items, content.Rules, new NoPhysicalCompletion());
            var frame = authority.Capture(1);
            Assert.AreEqual(1, frame.Progress.Players.Count);
            Assert.IsNull(frame.Progress.Result);
            Assert.IsEmpty(frame.Progress.Teams.Single().CompletedPuzzles);
            Assert.AreEqual(0, frame.Progress.Teams.Single().Lap);
            Assert.AreEqual(3, frame.Mechanisms.Count);
            Assert.IsTrue(frame.Mechanisms.All(mechanism => !mechanism.Solved && mechanism.ProgressTicks == 0));
            Assert.IsTrue(frame.Bodies.Any(body => body.Kind == WorldBodyKind.Keepsake && body.Active));
            Assert.AreEqual("", MatchStateValidation.Validate(frame, binding, content.Mode, content.Course,
                content.Items, content.Rules));
        }

        [Test]
        public void FreshStarterPrivateBundleRoundTripsExactDraftAndPhysicalPlan()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var draft = preset.Snapshot;
            var preview = CourseResolver.Resolve(draft.Course(), preset.Catalogue, new CourseResolveRequest(draft.Seed, preset.Play));
            Assert.IsTrue(preview.Success);
            var content = WordboundWorkshopOnlineContent.Create(draft, preview.Manifest, preset.Catalogue, preset.Play, items, rules);
            Assert.AreEqual("Workshop", content.PrivateBinding.Kind);
            Assert.AreEqual(preview.Manifest.Hash, content.Course.Hash);
            Assert.AreEqual(draft.CourseJsonText, CourseJson.ExportDocument(content.Course.CopyDocument()));
            Assert.IsNotNull(content.WorkshopBundle);
            var downloaded = WordboundClientContent.FromDownloaded(content.WorkshopBundle, preset.Catalogue, items, rules);
            Assert.AreEqual(content.Course.Hash, downloaded.Course.Hash);
            Assert.AreEqual(content.PhysicalPuzzles.Plan.Hash, downloaded.PhysicalPuzzles.Plan.Hash);
        }

        sealed class NoPhysicalCompletion : IWordboundWorldQuery
        {
            public bool HasLineOfSight(string actor, WorldVector from, WorldVector to) => false;
            public bool CanDeploy(string actor, string recipe, WorldVector at) => false;
            public bool PuzzleSatisfied(string actor, ResolvedCoursePuzzle puzzle, WorldBodyState body) => false;
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void LocalLaunchRetainsTheExactDraftSeedAndManifest(string modeId)
        {
            var selected = MatchStateValidation.Requirements(ModeDefinition.For(modeId));
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = flagship.CopyDocument(), Seed = Seed });
            var preview = CourseResolver.Resolve(draft.Course(), flagship.Geometry, new CourseResolveRequest(Seed, selected));
            Assert.IsTrue(preview.Success);
            var content = WordboundWorkshopPlaytest.Create(draft, preview.Manifest, flagship.Geometry, selected, items, rules);
            Assert.AreSame(preview.Manifest, content.Course);
            Assert.AreEqual(draft.Seed, content.Course.Seed);
            Assert.AreEqual(CourseJson.Sha256(draft.CourseJsonText), content.Course.SourceHash);
            Assert.AreEqual(72, content.Plan.Sites.Count, "Freestyle retains the authored stations for canonical practice.");
            if (modeId == "Freestyle") Assert.IsEmpty(content.Course.Laps, "Practice must not create race progression.");
        }

        [Test]
        public void AChangedDraftSeedOrModeCannotReuseAnOldPreview()
        {
            var selected = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = flagship.CopyDocument(), Seed = Seed });
            var preview = CourseResolver.Resolve(draft.Course(), flagship.Geometry, new CourseResolveRequest(Seed, selected)).Manifest;
            var renamed = draft.Course();
            renamed.Title = "Edited after preview";
            var changed = new WorkshopSnapshot(new WorkshopDraft { Course = renamed, Seed = Seed });
            Assert.Throws<ArgumentException>(() => WordboundWorkshopPlaytest.Create(changed, preview, flagship.Geometry, selected, items, rules));
            var reseeded = new WorkshopSnapshot(new WorkshopDraft { Course = draft.Course(), Seed = "fedcba9876543210" });
            Assert.Throws<ArgumentException>(() => WordboundWorkshopPlaytest.Create(reseeded, preview, flagship.Geometry, selected, items, rules));
            var otherMode = MatchStateValidation.Requirements(ModeDefinition.For("Freestyle"));
            Assert.Throws<ArgumentException>(() => WordboundWorkshopPlaytest.Create(draft, preview, flagship.Geometry, otherMode, items, rules));
        }

        [Test]
        public void LocalPlaytestCannotRelabelACompetitiveRoster()
        {
            var selected = MatchStateValidation.Requirements(ModeDefinition.For("TeamRace", RaceFormat.Squad));
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = flagship.CopyDocument(), Seed = Seed });
            var preview = CourseResolver.Resolve(draft.Course(), flagship.Geometry, new CourseResolveRequest(Seed, selected)).Manifest;
            Assert.Throws<ArgumentException>(() => WordboundWorkshopPlaytest.Create(draft, preview, flagship.Geometry, selected, items, rules));
        }

        [TestCase("TimeRun"), TestCase("Freestyle")]
        public void LocalContentRejectsExcessiveInventoryReservesBeforeCreatingARunner(string modeId)
        {
            string excessive = rules.Replace("\"Dibs\": {", "\"Dibs\": {\"maxLetters\":200,");
            Assert.AreNotEqual(rules, excessive);
            Assert.IsEmpty(RuleBook.FromJson(excessive).Validate());
            var selected = MatchStateValidation.Requirements(ModeDefinition.For(modeId));
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = flagship.CopyDocument(), Seed = Seed });
            var preview = CourseResolver.Resolve(draft.Course(), flagship.Geometry, new CourseResolveRequest(Seed, selected));
            Assert.IsTrue(preview.Success);
            var error = Assert.Throws<ArgumentException>(() => WordboundWorkshopPlaytest.Create(draft,
                preview.Manifest, flagship.Geometry, selected, items, excessive));
            StringAssert.Contains("512-body budget", error.Message);
            Assert.AreEqual(Seed, draft.Seed);
        }

        [Test]
        public void ArenaContentRejectsExcessiveInventoryReservesBeforePartyCreation()
        {
            string excessive = rules.Replace("\"Dibs\": {", "\"Dibs\": {\"maxLetters\":200,");
            Assert.AreNotEqual(rules, excessive);
            Assert.IsEmpty(RuleBook.FromJson(excessive).Validate());
            var error = Assert.Throws<ArgumentException>(() =>
                new WordboundClientContent(items, excessive, "FreeForAll", "Solo", Seed));
            StringAssert.Contains("512-body budget", error.Message);
        }
    }
}
