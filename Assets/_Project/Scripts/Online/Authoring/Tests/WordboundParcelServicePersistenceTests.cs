using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Online.Editor;

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
