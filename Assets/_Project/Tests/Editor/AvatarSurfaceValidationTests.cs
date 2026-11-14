using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class AvatarSurfaceValidationTests
    {
        [Test]
        public void DeliberatelyDisabledCorrectionAllowsSourceEditingButCannotProduceCorrectedPortraits()
        {
            var stored = AssetDatabase.LoadAssetAtPath<AvatarSurfaceProfile>(AvatarSurfaceCorrectionSetup.ProfilePath);
            Assert.IsNotNull(stored);
            byte[] file = File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath);
            byte[] meta = File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath + ".meta");
            bool dirty = EditorUtility.IsDirty(stored);
            string source = AvatarSurfaceMeshData.Fingerprint(stored.SourceMesh);
            var copy = ScriptableObject.CreateInstance<AvatarSurfaceProfile>();
            try
            {
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("enabledCorrection").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(copy.EnabledCorrection);
                Assert.IsNull(AvatarSurfaceCorrectionSetup.VerifyProfile(copy));
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.VerifyProfile(copy, true));
                serialized.FindProperty("enabledCorrection").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.VerifyProfile(copy));
            }
            finally { Object.DestroyImmediate(copy); }
            CollectionAssert.AreEqual(file, File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath));
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(AvatarSurfaceCorrectionSetup.ProfilePath + ".meta"));
            Assert.AreEqual(dirty, EditorUtility.IsDirty(stored));
            Assert.AreEqual(source, AvatarSurfaceMeshData.Fingerprint(stored.SourceMesh));
        }

        [Test]
        public void SameCountSourceEditIsRejectedWithoutChangingOriginal()
        {
            var profile = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            string original = AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh);
            var copy = Object.Instantiate(profile.SourceMesh);
            try
            {
                Assert.DoesNotThrow(() => AvatarSurfaceCorrectionSetup.RequireReviewedMeshContent(copy));
                var vertices = copy.vertices; vertices[0] += Vector3.right * .001f; copy.vertices = vertices;
                Assert.AreEqual(profile.SourceMesh.vertexCount, copy.vertexCount);
                Assert.AreEqual(profile.SourceMesh.subMeshCount, copy.subMeshCount);
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.RequireReviewedMeshContent(copy));
                Assert.Throws<InvalidOperationException>(() => AvatarSurfaceCorrectionSetup.ValidateCorrected(copy, profile.CorrectedMesh));
            }
            finally { Object.DestroyImmediate(copy); }
            Assert.AreEqual(original, AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh));
        }

        [Test]
        public void ExistingValidationReadsWithoutSavingOrRegenerating()
        {
            string[] paths = { AvatarSurfaceCorrectionSetup.AvatarPath, AvatarSurfaceCorrectionSetup.MeshPath, AvatarSurfaceCorrectionSetup.ProfilePath };
            var bytes = Array.ConvertAll(paths, path => File.ReadAllBytes(path));
            var metadata = Array.ConvertAll(paths, path => File.ReadAllBytes(path + ".meta"));
            var assets = Array.ConvertAll(paths, AssetDatabase.LoadMainAssetAtPath);
            var dirty = Array.ConvertAll(assets, EditorUtility.IsDirty);
            var first = AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true);
            Assert.AreSame(first, AvatarSurfaceCorrectionSetup.VerifyExistingProfile(true));
            new AvatarSurfaceValidationGuard().OnPreprocessBuild(null);
            for (int i = 0; i < paths.Length; i++)
            {
                CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]));
                CollectionAssert.AreEqual(metadata[i], File.ReadAllBytes(paths[i] + ".meta"));
                Assert.AreSame(assets[i], AssetDatabase.LoadMainAssetAtPath(paths[i]));
                Assert.AreEqual(dirty[i], EditorUtility.IsDirty(assets[i]));
            }
        }
    }
}
