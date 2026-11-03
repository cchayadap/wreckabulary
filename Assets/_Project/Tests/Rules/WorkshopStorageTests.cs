using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopStorageTests
    {
        string folder;

        [SetUp]
        public void SetUp() => folder = Path.Combine(Path.GetTempPath(), "wordbound-storage-test-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        static WorkshopSnapshot Map(string id, string title = "Garden map", string seed = "0123456789abcdef")
        {
            var draft = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld).Snapshot.Edit();
            draft.Course.Id = id; draft.Course.Title = title; draft.Seed = seed;
            return new WorkshopSnapshot(draft);
        }

        [TestCase("garden-map", "garden-map")]
        [TestCase("Garden_Map", "garden_map")]
        [TestCase("CON", "con~")]
        [TestCase("prn", "prn~")]
        [TestCase("Aux", "aux~")]
        [TestCase("nul", "nul~")]
        [TestCase("COM1", "com1~")]
        [TestCase("lpt9", "lpt9~")]
        [TestCase("com10", "com10")]
        [TestCase("console", "console")]
        [TestCase("com0", "com0")]
        public void FileNamesAreLowerCaseAndReservedWindowsNamesAreRemapped(string id, string stem)
        {
            Assert.AreEqual(stem, WorkshopStorage.FileStem(id));
            var store = new WorkshopStorage(folder);
            store.Save(Map(id));
            Assert.IsTrue(File.Exists(Path.Combine(folder, stem + WorkshopStorage.AutosaveExtension)));
            Assert.AreEqual(id, store.Recover(id, out bool backup).Course().Id); Assert.IsFalse(backup);
            Assert.AreEqual(Path.Combine(folder, stem + ".course.json"), store.SaveShare(Map(id)));
            Assert.AreEqual(id, store.LoadShare(id).Course().Id);
            Assert.Throws<ArgumentException>(() => WorkshopStorage.FileStem("../" + id));
        }

        [Test]
        public void IdsThatDifferOnlyByCapitalsCannotOverwriteEachOther()
        {
            var store = new WorkshopStorage(folder);
            var original = Map("Garden-Map", "Original");
            store.Save(original);
            byte[] before = File.ReadAllBytes(Path.Combine(folder, "garden-map" + WorkshopStorage.AutosaveExtension));
            var other = new WorkshopStorage(folder);
            Assert.Throws<IOException>(() => other.Save(Map("garden-map", "Other")));
            Assert.Throws<IOException>(() => store.Save(Map("GARDEN-MAP", "Other")));
            Assert.Throws<IOException>(() => other.SaveShare(Map("garden-MAP", "Other")));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(folder, "garden-map" + WorkshopStorage.AutosaveExtension)));
            Assert.AreEqual("Original", other.Recover("Garden-Map", out _).Course().Title);
            Assert.Throws<FormatException>(() => other.Recover("garden-map", out _));
            store.Save(Map("Garden-Map", "Original edited"));
            Assert.AreEqual("Original edited", other.Recover("Garden-Map", out _).Course().Title);
        }

        [Test]
        public void NewMapIdsAreValidLowerCaseAndNeverCollide()
        {
            Assert.AreEqual("my-race", WorkshopStorage.NewMapId("My Race!", new string[0]));
            Assert.AreEqual("map", WorkshopStorage.NewMapId("   ", null));
            Assert.AreEqual("con-2", WorkshopStorage.NewMapId("Con", new string[0]));
            Assert.AreEqual("a-2", WorkshopStorage.NewMapId("A", new[] { "A" }));
            Assert.AreEqual("a-3", WorkshopStorage.NewMapId("a", new[] { "a", "A-2" }));
            string longest = WorkshopStorage.NewMapId(new string('x', 70), Enumerable.Range(1, 12).Select(n => n == 1 ? new string('x', 40) : new string('x', 40) + "-" + n));
            Assert.IsTrue(CourseValidator.ValidId(longest)); Assert.LessOrEqual(longest.Length, 48);
            Assert.AreEqual(new string('x', 40) + "-13", longest);
            foreach (string name in new[] { "Été", "lpt1", "Map / One", "___" })
            {
                string id = WorkshopStorage.NewMapId(name, new string[0]);
                Assert.IsTrue(CourseValidator.ValidId(id), id); Assert.IsFalse(WorkshopStorage.ReservedName(id), id);
                Assert.AreEqual(id.ToLowerInvariant(), id);
            }
        }

        [Test]
        public void SaveAsCopiesUnderAFreeIdAndKeepsTheOriginal()
        {
            var store = new WorkshopStorage(folder);
            var original = Map("garden-map", "Garden map");
            store.Save(original);
            var copy = store.SaveAs(original, "Garden map");
            Assert.AreEqual("garden-map-2", copy.Course().Id); Assert.AreEqual("Garden map", copy.Course().Title);
            var second = store.SaveAs(original, "  Night garden  ");
            Assert.AreEqual("night-garden", second.Course().Id); Assert.AreEqual("Night garden", second.Course().Title);
            Assert.AreEqual(original.RecoveryJson, store.Recover("garden-map", out _).RecoveryJson);
            Assert.AreEqual(copy.RecoveryJson, store.Recover("garden-map-2", out _).RecoveryJson);
            Assert.AreEqual(original.Seed, copy.Seed);
            var expected = original.Course(); expected.Id = copy.Course().Id;
            Assert.AreEqual(CourseJson.ExportDocument(expected), copy.CourseJsonText);
        }

        [Test]
        public void LibraryListsDraftsAcrossSourcesWithNamesDatesAndUnreadableSaves()
        {
            var root = new WorkshopStorage(folder);
            var race = new WorkshopStorage(Path.Combine(folder, "templates", "RaceCircuit"));
            var parcel = new WorkshopStorage(Path.Combine(folder, "local-courses", "ParcelServiceV1"));
            root.Save(Map("assigned-map", "Assigned map"));
            race.Save(Map("race-copy", "Race copy"));
            parcel.Save(Map("parcel-copy", "Parcel copy"));
            string broken = Path.Combine(folder, "templates", "RaceCircuit", "broken" + WorkshopStorage.AutosaveExtension);
            File.WriteAllText(broken, "not a draft");
            File.WriteAllText(Path.Combine(folder, "templates", "RaceCircuit", "ignored.autosave.json.unreadable-1"), "kept aside");
            var times = new[] { ("assigned-map", folder, 1), ("race-copy", Path.Combine(folder, "templates", "RaceCircuit"), 3),
                ("parcel-copy", Path.Combine(folder, "local-courses", "ParcelServiceV1"), 2) };
            foreach (var (id, path, hour) in times)
                File.SetLastWriteTimeUtc(Path.Combine(path, id + WorkshopStorage.AutosaveExtension), new DateTime(2026, 1, 1, hour, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(broken, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var library = WorkshopStorage.Library(folder);
            CollectionAssert.AreEqual(new[] { "race-copy", "parcel-copy", "assigned-map", "broken" }, library.Select(entry => entry.CourseId));
            CollectionAssert.AreEqual(new[] { "Race copy", "Parcel copy", "Assigned map", "" }, library.Select(entry => entry.Title));
            CollectionAssert.AreEqual(new[] { "templates/RaceCircuit", "local-courses/ParcelServiceV1", "", "templates/RaceCircuit" },
                library.Select(entry => entry.Source));
            Assert.AreEqual(new DateTime(2026, 1, 1, 3, 0, 0, DateTimeKind.Utc), library[0].SavedUtc);
            Assert.IsTrue(library.Take(3).All(entry => entry.Readable));
            Assert.IsFalse(library[3].Readable); Assert.IsNotEmpty(library[3].Problem);
            Assert.AreEqual(1, race.List("templates/RaceCircuit").Count(entry => entry.Readable));
            Assert.IsEmpty(WorkshopStorage.Library(Path.Combine(folder, "missing")));
        }

        [Test]
        public void ImportOfTheSameMapKeepsTheCurrentDraftAsItsOwnListedMap()
        {
            var store = new WorkshopStorage(folder);
            var current = Map("garden-map", "My garden");
            var shared = Map("garden-map", "Shared garden", "fedcba9876543210");
            var prepared = store.PrepareImport(current, shared, out string kept);
            Assert.AreSame(shared, prepared);
            Assert.AreEqual("my-garden-before-import", kept);
            var archived = store.Recover(kept, out _);
            Assert.AreEqual("My garden (before import)", archived.Course().Title);
            Assert.AreEqual(current.Seed, archived.Seed);
            Assert.AreEqual(current.RecoveryJson, store.Recover("garden-map", out _).RecoveryJson);
            CollectionAssert.AreEquivalent(new[] { "garden-map", kept }, store.List().Select(entry => entry.CourseId));
            Assert.AreSame(current, store.PrepareImport(current, current, out string unchanged));
            Assert.AreEqual("garden-map", unchanged);
            Assert.AreEqual(2, store.List().Count);
        }

        [Test]
        public void ImportNeverOverwritesAnotherExistingDraft()
        {
            var store = new WorkshopStorage(folder);
            var other = Map("shared-map", "Already here");
            store.Save(other);
            byte[] before = File.ReadAllBytes(Path.Combine(folder, "shared-map" + WorkshopStorage.AutosaveExtension));
            var current = Map("garden-map", "Current");
            var prepared = store.PrepareImport(current, Map("shared-map", "Incoming"), out string kept);
            Assert.AreEqual("garden-map", kept);
            Assert.AreEqual("incoming", prepared.Course().Id); Assert.AreEqual("Incoming", prepared.Course().Title);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(folder, "shared-map" + WorkshopStorage.AutosaveExtension)));
            Assert.AreEqual(current.RecoveryJson, store.Recover("garden-map", out _).RecoveryJson);
            var differentCase = store.PrepareImport(current, Map("Garden-Map", "Garden"), out _);
            Assert.AreNotEqual("Garden-Map", differentCase.Course().Id);
            Assert.AreNotEqual("garden-map", differentCase.Course().Id);
            var fresh = Map("new-arrival", "New arrival");
            Assert.AreSame(fresh, store.PrepareImport(current, fresh, out _));
        }

        [Test]
        public void UnreadableSaveWithoutBackupIsSetAsideSoTheMapCanStartFresh()
        {
            var store = new WorkshopStorage(folder);
            string primary = Path.Combine(folder, "garden-map" + WorkshopStorage.AutosaveExtension);
            File.WriteAllText(primary, "interrupted");
            Assert.IsTrue(store.HasRecovery("garden-map"));
            Assert.Throws<FormatException>(() => store.Recover("garden-map", out _));
            string kept = store.SetAside("garden-map");
            StringAssert.StartsWith(primary + ".unreadable-", kept);
            Assert.AreEqual("interrupted", File.ReadAllText(kept));
            Assert.IsFalse(store.HasRecovery("garden-map"));
            Assert.IsEmpty(store.List());
            var fresh = Map("garden-map", "Fresh");
            store.Save(fresh);
            Assert.AreEqual(fresh.RecoveryJson, store.Recover("garden-map", out bool backup).RecoveryJson); Assert.IsFalse(backup);
            Assert.AreEqual("interrupted", File.ReadAllText(kept));
            File.WriteAllText(primary, "interrupted again");
            File.WriteAllText(primary + ".bak", "broken backup");
            kept = store.SetAside("garden-map");
            Assert.IsFalse(File.Exists(primary)); Assert.IsFalse(File.Exists(primary + ".bak"));
            Assert.AreEqual(3, Directory.GetFiles(folder, "*.unreadable-*").Length);
            Assert.AreEqual("", store.SetAside("garden-map"));
        }

        [Test]
        public void ModeSettingsAndLastMapLiveInLenientSideFilesOutsideTheAutosave()
        {
            var store = new WorkshopStorage(folder);
            var map = Map("garden-map");
            store.Save(map);
            Assert.IsFalse(store.TryLoadSettings("garden-map", out _, out _));
            store.SaveSettings("garden-map", "TeamRace", RaceFormat.Duo);
            var reopened = new WorkshopStorage(folder);
            Assert.IsTrue(reopened.TryLoadSettings("garden-map", out string mode, out RaceFormat format));
            Assert.AreEqual("TeamRace", mode); Assert.AreEqual(RaceFormat.Duo, format);
            Assert.AreEqual(map.RecoveryJson, File.ReadAllText(Path.Combine(folder, "garden-map" + WorkshopStorage.AutosaveExtension)));
            Assert.AreEqual(map.RecoveryJson, WorkshopSnapshot.Recover(map.RecoveryJson).RecoveryJson);
            Assert.Throws<ArgumentException>(() => store.SaveSettings("garden-map", "Golf", RaceFormat.Solo));
            File.WriteAllText(Path.Combine(folder, "garden-map" + WorkshopStorage.SettingsExtension), "{ broken");
            Assert.IsFalse(reopened.TryLoadSettings("garden-map", out _, out _));
            File.WriteAllText(Path.Combine(folder, "garden-map" + WorkshopStorage.SettingsExtension),
                "{\"format\":\"wreckabulary.workshop-settings\",\"schema\":1,\"mode\":\"TimeRun\",\"raceFormat\":\"Trio\"}");
            Assert.IsFalse(reopened.TryLoadSettings("garden-map", out _, out _));
            Assert.IsNull(store.LastMap());
            store.RememberLastMap("garden-map");
            Assert.AreEqual("garden-map", new WorkshopStorage(folder).LastMap());
            File.WriteAllText(Path.Combine(folder, "last-map.json"), "[]");
            Assert.IsNull(new WorkshopStorage(folder).LastMap());
            Assert.AreEqual(1, store.List().Count);
        }

        [Test]
        public void SavedMapFilesAreListedAndOpenedThroughTheShareReaders()
        {
            var store = new WorkshopStorage(folder);
            var map = Map("garden-map");
            string course = store.SaveShare(map);
            string playable = store.SavePlayableShare(map, CourseTemplates.ProofCatalogue(),
                MatchStateValidation.Requirements(ModeDefinition.For("Freestyle")));
            var files = store.Files();
            CollectionAssert.AreEquivalent(new[] { course, playable }, files.Select(file => file.Path));
            Assert.IsTrue(files.All(file => file.Name == "garden-map"));
            var fromCourse = store.OpenFile(course, CourseTemplates.ProofCatalogue());
            Assert.IsFalse(fromCourse.HasPlaySettings); Assert.AreEqual(map.CourseJsonText, fromCourse.Snapshot.CourseJsonText);
            var fromPlayable = store.OpenFile(playable, CourseTemplates.ProofCatalogue());
            Assert.IsTrue(fromPlayable.HasPlaySettings); Assert.AreEqual(map.Seed, fromPlayable.Snapshot.Seed);
            Assert.AreEqual("Freestyle", fromPlayable.Mode.ModeId);
            string outside = Path.Combine(Path.GetTempPath(), "garden-map.wordbound.json");
            Assert.Throws<ArgumentException>(() => store.OpenFile(outside, CourseTemplates.ProofCatalogue()));
            Assert.Throws<ArgumentException>(() => store.OpenFile(Path.Combine(folder, "garden-map" + WorkshopStorage.AutosaveExtension), CourseTemplates.ProofCatalogue()));
        }
    }
}
