using System;
using System.IO;
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
            Assert.AreEqual(modeId == "TimeRun" ? 72 : 0, content.Plan.Sites.Count);
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
    }
}
