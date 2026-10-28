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
            document.Title = "James saved parcel office";
            document.Spawns.Single(spawn => spawn.Id == "Start0").Position = new CoursePoint(-2400, 0, -2600);
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
                Assert.AreEqual(new CoursePoint(-2400, 0, 0), corrected.Spawns.Single(spawn => spawn.Id == "Start0").Position);
                corrected.Spawns.Single(spawn => spawn.Id == "Start0").Position = new CoursePoint(-2400, 0, -2600);
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
            document.Spawns.Single(spawn => spawn.Id == "Start0").Position = new CoursePoint(-2400, 0, -2000);
            course.SaveDocument(document); EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string before = File.ReadAllText(path);
            string backup = Path.Combine(Path.GetTempPath(), "wbqa", "parcel-spawn-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert.Throws<InvalidOperationException>(() => WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                Assert.AreEqual(before, File.ReadAllText(path)); Assert.IsFalse(Directory.Exists(backup));
                document.Spawns.Single(spawn => spawn.Id == "Start0").Position = new CoursePoint(-2400, 0, -2600);
                course.SaveDocument(document); EditorUtility.SetDirty(course);
                Assert.Throws<InvalidOperationException>(() => WordboundParcelServiceSpawnSetup.ApplyAt(path, backup));
                Assert.AreEqual(before, File.ReadAllText(path)); Assert.IsFalse(Directory.Exists(backup));
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
            var document = course.ReadDocument(); document.Title = "James parcel office";
            document.Spawns[0].Yaw = 90; course.SaveDocument(document); course.Seed = "fedcba9876543210"; course.ModeId = "Freestyle";
            EditorUtility.SetDirty(course); AssetDatabase.SaveAssetIfDirty(course);
            string saved = File.ReadAllText(path), input = course.InputHash;
            Resources.UnloadAsset(course);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            course = Create(path);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path)); Assert.AreEqual(saved, File.ReadAllText(path));
            Assert.AreEqual(input, course.InputHash); Assert.AreEqual("James parcel office", course.ReadDocument().Title);
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
