using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundParcelServicePersistenceTests
    {
        string folder;
        WordboundCourseAsset source;
        [SetUp] public void SetUp()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            source = AssetDatabase.LoadAssetAtPath<WordboundCourseAsset>(WordboundAuthoringSetup.FlagshipPath);
            Assert.IsNotNull(source);
            folder = "Assets/__WordboundParcelService_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }
        [TearDown] public void TearDown()
        {
            if (!string.IsNullOrEmpty(folder) && folder.StartsWith("Assets/__WordboundParcelService_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(folder);
        }
        WordboundCourseAsset Create(string path) => WordboundParcelServiceSetup.CreateOrSelectAt(path,
            source.Items, source.RuleBook, source.CameraProfile, source.VisualProfile);

        [Test]
        public void SpawnRepairChangesOnlyLegacyStart0AndKeepsExactBackupGuidAndOwnerEdits()
        {
            string path = folder + "/ParcelService.asset";
            var course = Create(path); var document = course.ReadDocument();
            document.Title = "James saved parcel depot";
            document.Spawns.Single(spawn => spawn.Id == "Start0").Position = WordboundParcelServiceContent.RearStartSeat;
            document.Spawns.Single(spawn => spawn.Id == "Start6").Yaw = 90;
            course.SaveDocument(document); course.Seed = "fedcba9876543210"; course.ModeId = "Freestyle";
            EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string originalDocument = course.DocumentJson, guid = AssetDatabase.AssetPathToGUID(path);
            var originalBytes = File.ReadAllBytes(path); var originalMeta = File.ReadAllBytes(path + ".meta");
            string backup = Path.Combine(Path.GetTempPath(), "wbqa", "parcel-spawn-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.IsTrue(WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(Path.Combine(backup, "ParcelService.asset")));
                CollectionAssert.AreEqual(originalMeta, File.ReadAllBytes(Path.Combine(backup, "ParcelService.asset.meta")));
                CollectionAssert.AreEqual(originalMeta, File.ReadAllBytes(path + ".meta"));
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                var corrected = course.ReadDocument();
                Assert.AreEqual(WordboundParcelServiceContent.StartSeat, corrected.Spawns.Single(spawn => spawn.Id == "Start0").Position);
                corrected.Spawns.Single(spawn => spawn.Id == "Start0").Position = WordboundParcelServiceContent.RearStartSeat;
                Assert.AreEqual(originalDocument, CourseJson.ExportDocument(corrected), "Every other saved document field remains exact.");
                Assert.AreEqual("fedcba9876543210", course.Seed); Assert.AreEqual("Freestyle", course.ModeId);
                Assert.AreSame(source.Items, course.Items); Assert.AreSame(source.RuleBook, course.RuleBook);
                Assert.AreSame(source.CameraProfile, course.CameraProfile); Assert.AreSame(source.VisualProfile, course.VisualProfile);
                string after = File.ReadAllText(path);
                Assert.IsFalse(WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                Assert.AreEqual(after, File.ReadAllText(path), "Repeat repair is a no-write verification.");
                Assert.IsNotNull(course.CreateContent(course.Resolve()));
            }
            finally { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
        }

        [Test]
        public void SpawnRepairRejectsDirtyOrCustomStartBeforeCreatingAnyBackup()
        {
            string path = folder + "/ParcelService.asset";
            var course = Create(path); var document = course.ReadDocument();
            document.Spawns.Single(spawn => spawn.Id == "Start0").Position =
                new CoursePoint(WordboundParcelServiceContent.StartSeat.X - 1000, 0, -2000);
            course.SaveDocument(document); EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string before = File.ReadAllText(path);
            string backup = Path.Combine(Path.GetTempPath(), "wbqa", "parcel-spawn-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Throws<InvalidOperationException>(() => WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                Assert.AreEqual(before, File.ReadAllText(path)); Assert.IsFalse(Directory.Exists(backup));
                document.Spawns.Single(spawn => spawn.Id == "Start0").Position = WordboundParcelServiceContent.RearStartSeat;
                course.SaveDocument(document); EditorUtility.SetDirty(course);
                Assert.Throws<InvalidOperationException>(() => WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                Assert.AreEqual(before, File.ReadAllText(path)); Assert.IsFalse(Directory.Exists(backup));
            }
            finally { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
        }

        [Test]
        public void OnlyAnUneditedEarlierGenerationGetsCurrentFurnishingsWithExactBackupAndGuid()
        {
            string path = folder + "/ParcelService.asset";
            var course = Create(path); string current = course.DocumentJson;
            var earlier = course.ReadDocument();
            earlier.Zones.RemoveAll(zone => zone.ModuleId == "Lockers" || zone.ModuleId == "Mezzanine");
            course.SaveDocument(earlier); course.Seed = "fedcba9876543210"; course.ModeId = "Freestyle";
            EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string hash = course.SourceHash, guid = AssetDatabase.AssetPathToGUID(path);
            var bytes = File.ReadAllBytes(path); var meta = File.ReadAllBytes(path + ".meta");
            string backup = Path.Combine(Path.GetTempPath(), "wbqa", "parcel-furnishing-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.IsFalse(WordboundParcelServiceSetup.RefreshEarlierGenerationAt(path, backup), "Only recorded earlier generations are replaced.");
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path)); Assert.IsFalse(Directory.Exists(backup));
                Assert.IsTrue(WordboundParcelServiceSetup.RefreshEarlierGenerationAt(path, backup, new[] { hash }));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(backup, "ParcelService.asset")));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(Path.Combine(backup, "ParcelService.asset.meta")));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta")); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                Assert.AreEqual(current, course.DocumentJson);
                Assert.AreEqual("fedcba9876543210", course.Seed); Assert.AreEqual("Freestyle", course.ModeId);
                Assert.AreSame(source.Items, course.Items); Assert.AreSame(source.RuleBook, course.RuleBook);
                Assert.AreSame(source.CameraProfile, course.CameraProfile); Assert.AreSame(source.VisualProfile, course.VisualProfile);
                string after = File.ReadAllText(path);
                Assert.IsFalse(WordboundParcelServiceSetup.RefreshEarlierGenerationAt(path, backup, new[] { hash }), "Repeat update is a no-write verification.");
                Assert.AreEqual(after, File.ReadAllText(path));
                var edited = course.ReadDocument(); edited.Title = "James saved parcel depot";
                course.SaveDocument(edited); EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
                string owned = File.ReadAllText(path);
                Assert.IsFalse(WordboundParcelServiceSetup.RefreshEarlierGenerationAt(path, backup + "-edited", new[] { hash }), "Owner edits are never replaced.");
                Assert.AreEqual(owned, File.ReadAllText(path)); Assert.IsFalse(Directory.Exists(backup + "-edited"));
            }
            finally { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
        }

        [Test]
        public void SavedCourseReopensAndRepeatSetupPreservesOwnerEditsAndExistingSources()
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            var sourceBytes = File.ReadAllBytes(sourcePath); var sourceMeta = File.ReadAllBytes(sourcePath + ".meta");
            string path = folder + "/ParcelService.asset";
            var course = Create(path); string guid = AssetDatabase.AssetPathToGUID(path);
            var document = course.ReadDocument(); document.Title = "James parcel depot";
            document.Spawns[0].Yaw = 90; course.SaveDocument(document); course.Seed = "fedcba9876543210"; course.ModeId = "Freestyle";
            EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string saved = File.ReadAllText(path), input = course.InputHash;
            Resources.UnloadAsset(course);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            course = Create(path);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path)); Assert.AreEqual(saved, File.ReadAllText(path));
            Assert.AreEqual(input, course.InputHash); Assert.AreEqual("James parcel depot", course.ReadDocument().Title);
            Assert.AreEqual(90, course.ReadDocument().Spawns[0].Yaw); Assert.AreEqual("Freestyle", course.ModeId);
            Assert.AreSame(source.Items, course.Items); Assert.AreSame(source.RuleBook, course.RuleBook);
            Assert.AreSame(source.CameraProfile, course.CameraProfile); Assert.AreSame(source.VisualProfile, course.VisualProfile);
            Assert.IsNotNull(course.CreateContent(course.Resolve()));
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(sourcePath));
            CollectionAssert.AreEqual(sourceMeta, File.ReadAllBytes(sourcePath + ".meta"));
        }

        [Test]
        public void WrongTypeDirtyOrInvalidExistingAssetsAreNeverReplaced()
        {
            string wrongPath = folder + "/Wrong.asset";
            var wrong = ScriptableObject.CreateInstance<WordboundCameraProfile>(); AssetDatabase.CreateAsset(wrong, wrongPath);
            string before = File.ReadAllText(wrongPath), guid = AssetDatabase.AssetPathToGUID(wrongPath);
            Assert.Throws<InvalidOperationException>(() => Create(wrongPath));
            Assert.AreEqual(before, File.ReadAllText(wrongPath)); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(wrongPath));
            string path = folder + "/ParcelService.asset"; var course = Create(path); before = File.ReadAllText(path);
            course.Seed = "not-a-seed"; EditorUtility.SetDirty(course);
            Assert.Throws<InvalidOperationException>(() => Create(path)); Assert.AreEqual(before, File.ReadAllText(path));
            AssetDatabase.SaveAssetIfDirty(course); before = File.ReadAllText(path);
            Assert.Throws<ArgumentException>(() => Create(path)); Assert.AreEqual(before, File.ReadAllText(path));
        }
    }
}
