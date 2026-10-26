using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class LobbyCameraProfileTests
    {
        string root;

        [SetUp]
        public void OwnFolder()
        {
            root = "Assets/_Project/Tests/LobbyCameraTemp-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/_Project/Tests", Path.GetFileName(root));
        }

        [TearDown]
        public void RemoveOwnedFolder()
        {
            Assert.IsTrue(root.StartsWith("Assets/_Project/Tests/LobbyCameraTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(root)) Assert.IsTrue(AssetDatabase.DeleteAsset(root));
        }

        [Test]
        public void DefaultsMatchTheExistingLobbyFramingExactly()
        {
            var profile = ScriptableObject.CreateInstance<LobbyCameraProfile>();
            try
            {
                Assert.IsTrue(profile.TryValidate(out string reason), reason);
                Assert.AreEqual(4.2f, profile.Distance); Assert.AreEqual(.95f, profile.EyeHeight);
                Assert.AreEqual(.5f, profile.LookHeight); Assert.AreEqual(30f, profile.FieldOfView);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [TestCase("Distance", float.NaN)]
        [TestCase("EyeHeight", float.PositiveInfinity)]
        [TestCase("LookHeight", -1f)]
        [TestCase("FieldOfView", 90f)]
        public void InvalidAuthoredValuesAreRejectedWithoutClamping(string field, float value)
        {
            var profile = ScriptableObject.CreateInstance<LobbyCameraProfile>();
            try
            {
                typeof(LobbyCameraProfile).GetField(field).SetValue(profile, value);
                Assert.IsFalse(profile.TryValidate(out string reason));
                Assert.IsNotEmpty(reason);
                float actual = (float)typeof(LobbyCameraProfile).GetField(field).GetValue(profile);
                if (float.IsNaN(value)) Assert.IsTrue(float.IsNaN(actual)); else Assert.AreEqual(value, actual);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void SavedAuthoredValuesSurviveReloadRepeatAndAnUnsavedEdit()
        {
            string path = root + "/Camera.asset";
            var profile = LobbyCameraSetup.CreateOrVerifyAt(path);
            Assert.IsNotNull(profile);
            profile.Distance = 5.1f; profile.EyeHeight = 1.2f; profile.LookHeight = .8f; profile.FieldOfView = 36f;
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            byte[] before = File.ReadAllBytes(path);
            Resources.UnloadAsset(profile);
            var reloaded = AssetDatabase.LoadAssetAtPath<LobbyCameraProfile>(path);
            Assert.AreEqual(5.1f, reloaded.Distance); Assert.AreEqual(1.2f, reloaded.EyeHeight);
            Assert.AreEqual(.8f, reloaded.LookHeight); Assert.AreEqual(36f, reloaded.FieldOfView);
            Assert.AreSame(reloaded, LobbyCameraSetup.CreateOrVerifyAt(path));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            reloaded.EyeHeight = 1.3f; EditorUtility.SetDirty(reloaded);
            Assert.AreSame(reloaded, LobbyCameraSetup.CreateOrVerifyAt(path));
            Assert.AreEqual(1.3f, reloaded.EyeHeight); Assert.IsTrue(EditorUtility.IsDirty(reloaded));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "Verify must not save an artist's pending edit.");
        }

        [Test]
        public void SetupDoesNotSaveOtherDirtyAssetsOrReplaceACollision()
        {
            string collision = root + "/Occupied.asset";
            var other = ScriptableObject.CreateInstance<LobbyTypographyProfile>();
            AssetDatabase.CreateAsset(other, collision); AssetDatabase.SaveAssetIfDirty(other);
            byte[] before = File.ReadAllBytes(collision);
            other.name = "Unsaved authored name"; EditorUtility.SetDirty(other);
            Assert.IsNotNull(LobbyCameraSetup.CreateOrVerifyAt(root + "/Camera.asset"));
            Assert.IsTrue(EditorUtility.IsDirty(other));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(collision));
            LogAssert.Expect(LogType.Warning, new Regex("Lobby camera setup stopped.*occupied by another asset"));
            Assert.IsNull(LobbyCameraSetup.CreateOrVerifyAt(collision));
            Assert.AreSame(other, AssetDatabase.LoadMainAssetAtPath(collision));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(collision));
        }

        [Test]
        public void InvalidSavedProfileIsReportedWithoutRepairOrReplacement()
        {
            string path = root + "/Invalid.asset";
            var profile = LobbyCameraSetup.CreateOrVerifyAt(path);
            profile.FieldOfView = 90; EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            byte[] before = File.ReadAllBytes(path);
            LogAssert.Expect(LogType.Warning, new Regex("Lobby camera setup stopped.*Authored profile needs attention"));
            Assert.IsNull(LobbyCameraSetup.CreateOrVerifyAt(path));
            Assert.AreSame(profile, AssetDatabase.LoadMainAssetAtPath(path)); Assert.AreEqual(90, profile.FieldOfView);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
}
