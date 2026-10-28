using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopShareTests
    {
        const string Seed = "fedcba9876543210", PriorSeed = "0123456789abcdef";
        GameObject owner;
        WordboundWorkshop workshop;
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-share-native-" + Guid.NewGuid().ToString("N"));
            owner = new GameObject("Workshop share import test");
            workshop = owner.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
            workshop.ConfigureLocalPlaytest(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (owner) Object.DestroyImmediate(owner);
            string prefix = Path.Combine(Path.GetTempPath(), "wordbound-share-native-");
            if (folder != null && Path.GetFullPath(folder).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        static WorkshopSnapshot SharedSnapshot(string mode)
        {
            var preset = WorkshopTemplates.Create(mode == "Freestyle" ? WorkshopTemplate.FreestyleWorld :
                mode == "FreeForAll" ? WorkshopTemplate.CombatArena : WorkshopTemplate.RaceCircuit);
            var draft = preset.Snapshot.Edit(); draft.Seed = Seed; draft.Course.Title = "Shared parcel map";
            return new WorkshopSnapshot(draft);
        }
        static CoursePlayRequirements Play(string mode, RaceFormat format = RaceFormat.Solo) =>
            MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
        static string Bundle(string mode = "Freestyle", RaceFormat format = RaceFormat.Solo) =>
            WorkshopShare.Export(SharedSnapshot(mode), CourseTemplates.ProofCatalogue(), Play(mode, format));
        static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        string Field(string name) => (string)typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workshop);

        void OpenPrior()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            workshop.OpenAuthored(preset.Snapshot.Course(), preset.Catalogue, preset.Play, PriorSeed, folder);
            workshop.History.Select(new[] { "Start" }); workshop.ResumeEditing();
        }
        void PopulatePlaytest()
        {
            int dispatched = 0;
            Action<ResolvedCourse> receive = _ => dispatched++;
            workshop.PlaytestRequested += receive;
            try { workshop.RequestPlaytest(); }
            finally { workshop.PlaytestRequested -= receive; }
            Assert.AreEqual(1, dispatched); Assert.IsNotNull(workshop.LastPreview); Assert.IsNotNull(workshop.LastPlaytestDraft);
        }
        void AssertPlay(CoursePlayRequirements expected)
        {
            var actual = workshop.PlayRequirements;
            Assert.AreEqual(expected.ModeId, actual.ModeId); Assert.AreEqual(expected.Capability, actual.Capability);
            Assert.AreEqual(expected.TeamCount, actual.TeamCount); Assert.AreEqual(expected.TeamCapacity, actual.TeamCapacity);
            Assert.AreEqual(expected.MaxOccupants, actual.MaxOccupants); Assert.AreEqual(expected.Ownership, actual.Ownership);
        }

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("FreeForAll", RaceFormat.Solo)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        public void ImportRestoresExactSeedModeFormatAndVisibleFieldsThenInvalidatesOldPlaytest(string mode, RaceFormat format)
        {
            OpenPrior(); PopulatePlaytest(); var history = workshop.History;
            int revision = history.Revision;
            workshop.ImportSharedMap(Bundle(mode, format));
            Assert.AreSame(history, workshop.History); Assert.AreEqual(revision + 1, history.Revision);
            Assert.AreEqual(SharedSnapshot(mode).CourseJsonText, history.Current.CourseJsonText);
            Assert.AreEqual(Seed, history.Current.Seed); Assert.IsTrue(history.Current.SeedLocked);
            Assert.AreEqual(Seed, Field("seed")); Assert.AreEqual("Shared parcel map", Field("title"));
            Assert.IsEmpty(history.Current.Selection); AssertPlay(Play(mode, format));
            Assert.IsNull(workshop.LastPreview); Assert.IsNull(workshop.LastPlaytestDraft);
            Assert.AreEqual(mode == "TimeRun" || mode == "Freestyle", workshop.CanPlaytestLocally);
            var preview = workshop.Preview(); Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues));
            var expected = CourseResolver.Resolve(SharedSnapshot(mode).Course(), workshop.Catalogue, new CourseResolveRequest(Seed, Play(mode, format)));
            Assert.AreEqual(expected.Manifest.Hash, preview.Manifest.Hash);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedEarlyOrLateValidationLeavesAllCurrentImportStateAndRedoIntact(bool lateFailure)
        {
            OpenPrior(); var history = workshop.History;
            history.Execute("Rename", draft => draft.Course.Title = "Keep redo"); history.Undo();
            PopulatePlaytest();
            string before = history.Current.RecoveryJson, seedField = Field("seed"), titleField = Field("title");
            int revision = history.Revision;
            var play = workshop.PlayRequirements; var preview = workshop.LastPreview; var playtest = workshop.LastPlaytestDraft;
            string json = Bundle();
            if (lateFailure)
            {
                var source = SharedSnapshot("Freestyle"); var invalid = source.Course(); invalid.Spawns.Clear();
                json = json.Replace(Quote(source.CourseJsonText), Quote(CourseJson.ExportDocument(invalid)));
            }
            else json = json.Replace(Seed, "not-a-valid-seed");
            Assert.Throws<FormatException>(() => workshop.ImportSharedMap(json));
            Assert.AreSame(history, workshop.History); Assert.AreEqual(before, history.Current.RecoveryJson);
            Assert.AreEqual(revision, history.Revision); Assert.IsTrue(history.CanRedo); Assert.IsFalse(history.CanUndo);
            Assert.AreSame(play, workshop.PlayRequirements); Assert.AreSame(preview, workshop.LastPreview);
            Assert.AreSame(playtest, workshop.LastPlaytestDraft); Assert.AreEqual(seedField, Field("seed"));
            Assert.AreEqual(titleField, Field("title")); CollectionAssert.AreEqual(new[] { "Start" }, history.Current.Selection);
            Assert.IsTrue(history.Redo()); Assert.AreEqual("Keep redo", history.Current.Course().Title);
        }

        [TestCase(WorkshopTemplate.FreestyleWorld)]
        [TestCase(WorkshopTemplate.RaceCircuit)]
        public void LegacyImportKeepsChosenModeAndUsesLegacyDefaultSeed(WorkshopTemplate template)
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder); workshop.SelectMode("TeamRace", RaceFormat.Duo);
            var play = workshop.PlayRequirements; var legacy = WorkshopTemplates.Create(template).Snapshot;
            workshop.History.Execute("Seed", draft => draft.Seed = PriorSeed);
            workshop.ImportSharedMap(legacy.CourseJsonText);
            Assert.AreSame(play, workshop.PlayRequirements); Assert.AreEqual(legacy.CourseJsonText, workshop.History.Current.CourseJsonText);
            Assert.AreEqual("0000000000000001", workshop.History.Current.Seed);
            Assert.AreEqual("0000000000000001", Field("seed")); Assert.IsFalse(workshop.CanPlaytestLocally);
            if (template == WorkshopTemplate.FreestyleWorld)
                Assert.Throws<InvalidOperationException>(() => workshop.Preview());
            else Assert.IsTrue(workshop.Preview().Success);
        }

        [Test]
        public void UndoRestoresMapSeedAndSelectionWhileModeRemainsASeparateSessionChoice()
        {
            OpenPrior(); var history = workshop.History; string before = history.Current.RecoveryJson;
            workshop.ImportSharedMap(Bundle("TeamRace", RaceFormat.Duo));
            string imported = history.Current.RecoveryJson; var selectedMode = workshop.PlayRequirements;
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.RecoveryJson); Assert.IsFalse(history.CanUndo);
            workshop.ResumeEditing(); Assert.AreEqual(PriorSeed, Field("seed")); Assert.AreSame(selectedMode, workshop.PlayRequirements);
            Assert.IsFalse(workshop.CanPlaytestLocally);
            Assert.Throws<InvalidOperationException>(() => workshop.Preview());
            Assert.IsTrue(history.Redo()); workshop.ResumeEditing();
            Assert.AreEqual(imported, history.Current.RecoveryJson); Assert.AreEqual(Seed, Field("seed"));
            Assert.AreSame(selectedMode, workshop.PlayRequirements); Assert.IsTrue(workshop.Preview().Success);
        }

        [Test]
        public void ModeOnlyImportUpdatesRequirementsAndInvalidatesPreviewWithoutAddingMapHistory()
        {
            OpenPrior(); workshop.ImportSharedMap(Bundle("TeamRace", RaceFormat.Duo));
            var history = workshop.History; int revision = history.Revision; string current = history.Current.RecoveryJson;
            Assert.IsTrue(workshop.Preview().Success);
            workshop.ImportSharedMap(Bundle("BrawlRace", RaceFormat.Squad));
            Assert.AreSame(history, workshop.History); Assert.AreEqual(revision, history.Revision);
            Assert.AreEqual(current, history.Current.RecoveryJson); AssertPlay(Play("BrawlRace", RaceFormat.Squad));
            Assert.IsNull(workshop.LastPreview); Assert.AreEqual(Seed, Field("seed"));
        }

        [Test]
        public void PendingRecoveryRejectsImportAndPreservesTheSavedFile()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld);
            var storage = new WorkshopStorage(folder); storage.Save(SharedSnapshot("Freestyle"));
            string path = Path.Combine(folder, preset.Snapshot.Course().Id + ".autosave.json");
            byte[] saved = File.ReadAllBytes(path);
            workshop.OpenAuthored(preset.Snapshot.Course(), preset.Catalogue, preset.Play, PriorSeed, folder);
            Assert.IsTrue(workshop.RecoveryChoicePending);
            var history = workshop.History; string before = history.Current.RecoveryJson; var play = workshop.PlayRequirements;
            Assert.Throws<InvalidOperationException>(() => workshop.ImportSharedMap(Bundle()));
            Assert.IsTrue(workshop.RecoveryChoicePending); Assert.AreSame(history, workshop.History);
            Assert.AreEqual(before, history.Current.RecoveryJson); Assert.AreSame(play, workshop.PlayRequirements);
            Assert.IsFalse(history.CanUndo); CollectionAssert.AreEqual(saved, File.ReadAllBytes(path));
        }

        [Test]
        public void OnlinePreparationRejectsImportWithoutChangingItsCapturedDraft()
        {
            OpenPrior(); workshop.CanStartOnline = () => true;
            WorkshopSnapshot captured = null;
            workshop.OnlinePlaytestRequested += (snapshot, _, __) => captured = snapshot;
            workshop.RequestOnlinePlaytest(); Assert.IsTrue(workshop.OnlineStarting); Assert.IsNotNull(captured);
            var history = workshop.History; var play = workshop.PlayRequirements; var preview = workshop.LastPreview;
            int revision = history.Revision;
            Assert.Throws<InvalidOperationException>(() => workshop.ImportSharedMap(Bundle("TimeRun")));
            Assert.IsTrue(workshop.OnlineStarting); Assert.AreSame(history, workshop.History);
            Assert.AreSame(captured, history.Current); Assert.AreEqual(revision, history.Revision);
            Assert.AreSame(play, workshop.PlayRequirements); Assert.AreSame(preview, workshop.LastPreview);
            workshop.FinishOnlinePreparation();
        }
    }
}
