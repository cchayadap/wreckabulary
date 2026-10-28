using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopShareTests
    {
        const string Seed = "fedcba9876543210";
        static CourseCatalogue Catalogue() => CourseTemplates.ProofCatalogue();
        static CoursePlayRequirements Play(string mode, RaceFormat format = RaceFormat.Solo) =>
            MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
        static WorkshopSnapshot Snapshot(WorkshopTemplate template = WorkshopTemplate.FreestyleWorld)
        {
            var draft = WorkshopTemplates.Create(template).Snapshot.Edit();
            draft.Seed = Seed; draft.SeedLocked = false; draft.Selection.Add("Start");
            draft.HiddenLayers.Add("Ground"); draft.LockedLayers.Add("Ground"); draft.GridMm = 1000;
            return new WorkshopSnapshot(draft);
        }
        static string Export() => WorkshopShare.Export(Snapshot(), Catalogue(), Play("Freestyle"));
        static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string WithCourse(string course) => Export().Replace(Quote(Snapshot().CourseJsonText), Quote(course));

        [TestCase("TimeRun", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Solo)]
        [TestCase("TeamRace", RaceFormat.Duo)]
        [TestCase("TeamRace", RaceFormat.Squad)]
        [TestCase("BrawlRace", RaceFormat.Solo)]
        [TestCase("BrawlRace", RaceFormat.Duo)]
        [TestCase("BrawlRace", RaceFormat.Squad)]
        [TestCase("FreeForAll", RaceFormat.Solo)]
        [TestCase("Freestyle", RaceFormat.Solo)]
        public void EveryApprovedModeAndFormatRoundTripsTheExactResolvedCourse(string mode, RaceFormat format)
        {
            var snapshot = Snapshot(mode == "Freestyle" ? WorkshopTemplate.FreestyleWorld :
                mode == "FreeForAll" ? WorkshopTemplate.CombatArena : WorkshopTemplate.RaceCircuit);
            var play = Play(mode, format); var catalogue = Catalogue();
            string json = WorkshopShare.Export(snapshot, catalogue, play);
            var imported = WorkshopShare.Import(json, catalogue);
            Assert.IsTrue(imported.HasPlaySettings); Assert.AreEqual(mode, imported.Mode.ModeId);
            Assert.AreEqual(format, imported.Mode.Format); Assert.AreEqual(Seed, imported.Snapshot.Seed);
            Assert.AreEqual(snapshot.CourseJsonText, imported.Snapshot.CourseJsonText);
            var before = CourseResolver.Resolve(snapshot.Course(), catalogue, new CourseResolveRequest(Seed, play));
            var after = CourseResolver.Resolve(imported.Snapshot.Course(), catalogue,
                new CourseResolveRequest(imported.Snapshot.Seed, imported.Mode.Requirements));
            Assert.IsTrue(before.Success); Assert.IsTrue(after.Success);
            Assert.AreEqual(CourseJson.ExportManifest(before.Manifest), CourseJson.ExportManifest(after.Manifest));
            Assert.AreEqual(json, WorkshopShare.Export(imported.Snapshot, catalogue, imported.Mode.Requirements));
        }

        [Test]
        public void SharingDoesNotLeakEditorSelectionsOrLocksAndProtectsTheImportedSeed()
        {
            var imported = WorkshopShare.Import(Export(), Catalogue());
            Assert.IsEmpty(imported.Snapshot.Selection); Assert.IsEmpty(imported.Snapshot.HiddenLayers);
            Assert.IsEmpty(imported.Snapshot.LockedLayers); Assert.IsEmpty(imported.Snapshot.GroupIds);
            Assert.IsTrue(imported.Snapshot.SeedLocked); Assert.AreEqual(500, imported.Snapshot.GridMm);
            var history = new WorkshopHistory(imported.Snapshot);
            Assert.IsFalse(history.Reroll("0000000000000002")); Assert.AreEqual(Seed, history.Current.Seed);
        }

        [TestCase(WorkshopTemplate.FreestyleWorld)]
        [TestCase(WorkshopTemplate.RaceCircuit)]
        public void LegacyCourseSchemasRemainImportableWithoutInventedPlaySettings(WorkshopTemplate template)
        {
            var original = WorkshopTemplates.Create(template).Snapshot;
            var imported = WorkshopShare.Import(original.CourseJsonText, null);
            Assert.IsFalse(imported.HasPlaySettings); Assert.IsNull(imported.Mode);
            Assert.AreEqual(original.CourseJsonText, imported.Snapshot.CourseJsonText);
            Assert.AreEqual("0000000000000001", imported.Snapshot.Seed);
        }

        [Test]
        public void InvalidDraftStillHasLegacyExportButCannotClaimPlayableSettings()
        {
            var draft = Snapshot().Edit(); draft.Course.Spawns.Clear();
            var incomplete = new WorkshopSnapshot(draft);
            Assert.IsNotNull(WorkshopShare.Import(incomplete.CourseJsonText, Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Export(incomplete, Catalogue(), Play("Freestyle")));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(WithCourse(incomplete.CourseJsonText), Catalogue()));
        }

        [Test]
        public void CourseWithoutPhysicalPuzzleControlsCannotClaimPlayableSettings()
        {
            var draft = Snapshot(WorkshopTemplate.RaceCircuit).Edit(); draft.Course.PhysicalSites.Clear();
            Assert.Throws<FormatException>(() => WorkshopShare.Export(new WorkshopSnapshot(draft), Catalogue(), Play("TimeRun")));
        }

        [Test]
        public void FailedImportKeepsSelectionCurrentDraftUndoRedoAndSelectedMode()
        {
            var history = new WorkshopHistory(Snapshot());
            history.Execute("Rename", draft => draft.Course.Title = "Keep this redo"); history.Undo();
            var play = Play("Freestyle"); var originalPlay = play;
            string before = history.Current.RecoveryJson; int revision = history.Revision;
            Assert.Throws<FormatException>(() => {
                var candidate = WorkshopShare.Import(Export().Replace("\"raceFormat\":\"Solo\"", "\"raceFormat\":\"Duo\""), Catalogue());
                candidate.Apply(history);
                if (candidate.HasPlaySettings) play = candidate.Mode.Requirements;
            });
            Assert.AreEqual(before, history.Current.RecoveryJson); Assert.AreEqual(revision, history.Revision);
            Assert.AreSame(originalPlay, play); Assert.IsTrue(history.CanRedo); Assert.IsFalse(history.CanUndo);
            CollectionAssert.AreEqual(new[] { "Start" }, history.Current.Selection);
            Assert.IsTrue(history.Redo()); Assert.AreEqual("Keep this redo", history.Current.Course().Title);
        }

        [Test]
        public void ImportIsOneUndoableMapAndSeedTransactionWithoutMutableAliases()
        {
            var old = Snapshot().Edit(); old.Seed = "0123456789abcdef"; old.Course.Title = "Before import";
            var history = new WorkshopHistory(new WorkshopSnapshot(old)); string before = history.Current.RecoveryJson;
            var imported = WorkshopShare.Import(WorkshopShare.Export(Snapshot(WorkshopTemplate.RaceCircuit), Catalogue(), Play("TeamRace", RaceFormat.Duo)), Catalogue());
            Assert.IsTrue(imported.Apply(history)); Assert.AreEqual(1, history.Revision);
            Assert.AreEqual(Seed, history.Current.Seed); Assert.IsEmpty(history.Current.Selection);
            string after = history.Current.RecoveryJson;
            imported.Snapshot.Edit().Course.Title = "Outside mutation";
            Assert.AreEqual(after, history.Current.RecoveryJson);
            Assert.IsTrue(history.Undo()); Assert.AreEqual(before, history.Current.RecoveryJson); Assert.IsFalse(history.CanUndo);
            Assert.IsTrue(history.Redo()); Assert.AreEqual(after, history.Current.RecoveryJson);
        }

        [TestCase("format", "wreckabulary.workshop-share", "untrusted.bundle")]
        [TestCase("seed", Seed, "000000000000000X")]
        [TestCase("seed", Seed, "1")]
        [TestCase("mode", "Freestyle", "TimeRun")]
        [TestCase("mode", "Freestyle", "CustomScript")]
        [TestCase("raceFormat", "Solo", "Duo")]
        [TestCase("raceFormat", "Solo", "solo")]
        [TestCase("raceFormat", "Solo", "4")]
        public void UnsupportedSettingsRejectBeforeImport(string key, string before, string after)
        {
            string json = Export().Replace(Quote(key) + ":" + Quote(before), Quote(key) + ":" + Quote(after));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(json, Catalogue()));
        }

        [TestCase("\"scripts\":[\"https://example.invalid/remote.cs\"],")]
        [TestCase("\"assets\":[\"https://example.invalid/model.fbx\"],")]
        [TestCase("\"schema\":1,")]
        public void UnknownAndDuplicateBundleFieldsAreRejected(string extra)
        {
            Assert.Throws<FormatException>(() => WorkshopShare.Import("{" + extra + Export().Substring(1), Catalogue()));
        }

        [Test]
        public void UnsupportedVersionMissingFieldsAndNumericFormatAreRejected()
        {
            Assert.Throws<FormatException>(() => WorkshopShare.Import(Export().Replace("\"schema\":1", "\"schema\":2"), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(Export().Replace("\"seed\":\"" + Seed + "\"", "\"seedMissing\":\"" + Seed + "\""), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(Export().Replace("\"raceFormat\":\"Solo\"", "\"raceFormat\":1"), Catalogue()));
        }

        [Test]
        public void ForgedRequirementsCannotBeExportedAsAnApprovedMode()
        {
            var forged = new CoursePlayRequirements("Freestyle", CourseCapability.Explore, 0, 0, 999, CourseOwnershipPolicy.Shared);
            Assert.Throws<ArgumentException>(() => WorkshopShare.Export(Snapshot(), Catalogue(), forged));
        }

        [Test]
        public void MismatchedRulesOrCatalogueAreRejected()
        {
            string json = Export(); var catalogue = Catalogue();
            Assert.Throws<FormatException>(() => WorkshopShare.Import(json.Replace(catalogue.Hash, new string('0', 64)), catalogue));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(json.Replace(ModeDefinition.For("Freestyle").Hash, new string('0', 64)), catalogue));
            var other = new CourseCatalogue(catalogue.Modules.Values, catalogue.Assets.Values, catalogue.PuzzlePresets,
                catalogue.Themes.Concat(new[] { "OtherTheme" }), catalogue.NarrativeEvents);
            Assert.Throws<FormatException>(() => WorkshopShare.Import(json, other));
        }

        [Test]
        public void UnapprovedCourseAssetsAndFieldsCannotEnterThroughTheBundle()
        {
            var course = Snapshot().Course(); course.Zones[0].Pool[0].AssetId = "RemoteModel";
            Assert.Throws<FormatException>(() => WorkshopShare.Import(WithCourse(CourseJson.ExportDocument(course)), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(WithCourse("{\"scripts\":[]," + Snapshot().CourseJsonText.Substring(1)), Catalogue()));
        }

        [Test]
        public void BundleAndInnerCourseHaveSeparateUtf8SizeBounds()
        {
            Assert.Throws<FormatException>(() => WorkshopShare.Import(new string(' ', WorkshopShare.MaximumBytes + 1), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(new string('\u00e9', WorkshopShare.MaximumBytes / 2 + 1), Catalogue()));
            string inner = Snapshot().CourseJsonText + new string(' ', CourseLimits.DocumentBytes);
            Assert.Less(Encoding.UTF8.GetByteCount(WithCourse(inner)), WorkshopShare.MaximumBytes);
            Assert.Throws<FormatException>(() => WorkshopShare.Import(WithCourse(inner), Catalogue()));
        }

        [Test]
        public void ExcessiveDepthOversizedMetadataAndInvalidUnicodeAreRejected()
        {
            Assert.Throws<FormatException>(() => WorkshopShare.Import(new string('[', CourseLimits.JsonDepth + 1) + new string(']', CourseLimits.JsonDepth + 1), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(Export().Replace(Quote(Seed), Quote(new string('0', 25000))), Catalogue()));
            Assert.Throws<FormatException>(() => WorkshopShare.Import(Export() + "\ud800", Catalogue()));
        }

        [Test]
        public void LargeApprovedCourseStringIsAllowedOnlyAsTheBoundedEmbeddedCourse()
        {
            var draft = Snapshot().Edit(); draft.Course.Title = "Parcel \u00e9 \"room\" \\ workshop";
            for (int index = 0; index < 300; index++) draft.Course.Supplies.Add(new CourseSupply {
                Id = "Letter" + index, ModuleId = "Garden", Position = new CoursePoint(20000, 0, 0), Letters = "A"
            });
            var snapshot = new WorkshopSnapshot(draft);
            Assert.Greater(snapshot.CourseJsonText.Length, 24578);
            var imported = WorkshopShare.Import(WorkshopShare.Export(snapshot, Catalogue(), Play("Freestyle")), Catalogue());
            Assert.AreEqual(snapshot.CourseJsonText, imported.Snapshot.CourseJsonText);
        }

        [Test]
        public void FileRoundTripPreservesSettingsAndKeepsLegacyFilesSeparate()
        {
            string folder = Path.Combine(Path.GetTempPath(), "wordbound-share-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new WorkshopStorage(folder); var snapshot = Snapshot();
                string legacy = storage.SaveShare(snapshot);
                string path = storage.SavePlayableShare(snapshot, Catalogue(), Play("Freestyle"));
                Assert.IsTrue(path.EndsWith(".wordbound.json", StringComparison.Ordinal)); Assert.AreNotEqual(legacy, path);
                var imported = storage.LoadPlayableShare(snapshot.Course().Id, Catalogue());
                Assert.AreEqual(Seed, imported.Snapshot.Seed); Assert.AreEqual("Freestyle", imported.Mode.ModeId);
                Assert.AreEqual(snapshot.CourseJsonText, storage.LoadShare(snapshot.Course().Id).CourseJsonText);
                string original = File.ReadAllText(path);
                var bad = snapshot.Edit(); bad.Course.Spawns.Clear();
                Assert.Throws<FormatException>(() => storage.SavePlayableShare(new WorkshopSnapshot(bad), Catalogue(), Play("Freestyle")));
                Assert.AreEqual(original, File.ReadAllText(path)); Assert.AreEqual(0, Directory.GetFiles(folder, "*.tmp").Length);
                Assert.Throws<ArgumentException>(() => storage.LoadPlayableShare("../escape", Catalogue()));
                File.WriteAllText(path, new string(' ', WorkshopShare.MaximumBytes + 1));
                Assert.Throws<FormatException>(() => storage.LoadPlayableShare(snapshot.Course().Id, Catalogue()));
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
    }
}
