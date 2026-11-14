using System;
using System.IO;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundWorkshopOnlineLaunchTests
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

        [TestCase("TeamRace", RaceFormat.Squad), TestCase("FreeForAll", RaceFormat.Solo)]
        public void OnlineLaunchRetainsExactEditedDraftAndCompetitiveRoster(string modeId, RaceFormat format)
        {
            var geometry = modeId == "FreeForAll" ? CourseTemplates.ProofCatalogue() : flagship.Geometry;
            var document = modeId == "FreeForAll" ? CourseTemplates.CombatArena() : flagship.CopyDocument();
            document.Title = "James edited course";
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = document, Seed = Seed });
            var selected = MatchStateValidation.Requirements(ModeDefinition.For(modeId, format));
            var preview = CourseResolver.Resolve(draft.Course(), geometry, new CourseResolveRequest(Seed, selected));
            Assert.IsTrue(preview.Success);
            var content = WordboundWorkshopOnlineContent.Create(draft, preview.Manifest, geometry, selected, items, rules);
            Assert.AreEqual(modeId, content.Mode.Id);
            Assert.AreEqual(format, content.Mode.Format);
            Assert.AreEqual(preview.Manifest.Hash, content.Course.Hash);
            Assert.AreEqual(draft.CourseJsonText, CourseJson.ExportDocument(content.Course.CopyDocument()));
            Assert.AreEqual(Seed, content.Seed);
            Assert.NotNull(content.WorkshopBundle);
            Assert.AreEqual("Workshop", content.PrivateBinding.Kind);
        }

        [Test]
        public void ChangedDraftSeedAndSelectedTeamSizeRejectOldPreviewBeforePartyCreation()
        {
            var selected = MatchStateValidation.Requirements(ModeDefinition.For("TeamRace", RaceFormat.Duo));
            var draft = new WorkshopSnapshot(new WorkshopDraft { Course = flagship.CopyDocument(), Seed = Seed });
            var preview = CourseResolver.Resolve(draft.Course(), flagship.Geometry, new CourseResolveRequest(Seed, selected)).Manifest;
            var renamed = draft.Course();
            renamed.Title = "Changed after preview";
            var changed = new WorkshopSnapshot(new WorkshopDraft { Course = renamed, Seed = Seed });
            Assert.Throws<ArgumentException>(() => WordboundWorkshopOnlineContent.Create(changed, preview, flagship.Geometry, selected, items, rules));
            var reseeded = new WorkshopSnapshot(new WorkshopDraft { Course = draft.Course(), Seed = "fedcba9876543210" });
            Assert.Throws<ArgumentException>(() => WordboundWorkshopOnlineContent.Create(reseeded, preview, flagship.Geometry, selected, items, rules));
            var squad = MatchStateValidation.Requirements(ModeDefinition.For("TeamRace", RaceFormat.Squad));
            Assert.Throws<ArgumentException>(() => WordboundWorkshopOnlineContent.Create(draft, preview, flagship.Geometry, squad, items, rules));
            Assert.AreEqual(Seed, draft.Seed);
            Assert.AreEqual(preview.SourceHash, CourseJson.Sha256(draft.CourseJsonText));
        }
    }
}
