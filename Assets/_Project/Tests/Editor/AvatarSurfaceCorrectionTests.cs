using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTests
{
    public sealed class AvatarSurfaceCorrectionTests
    {
        AvatarSurfaceProfile profile;
        GameObject source;
        readonly List<Object> owned = new List<Object>();

        [SetUp]
        public void LoadSavedCorrection()
        {
            profile = AssetDatabase.LoadAssetAtPath<AvatarSurfaceProfile>(AvatarSurfaceCorrectionSetup.ProfilePath);
            Assert.IsNotNull(profile, "Run the missing-only avatar surface setup before these native tests.");
            source = ModelLibrary.Load().Find("Avatar/Avatar");
            Assert.AreSame(source, profile.SourceAvatar);
        }

        [TearDown]
        public void ReleaseOwnedObjects()
        {
            foreach (var value in owned.AsEnumerable().Reverse()) if (value) Object.DestroyImmediate(value);
            owned.Clear();
        }

        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        SkinnedMeshRenderer Head(GameObject root) => root.transform.Find(profile.RendererPath).GetComponent<SkinnedMeshRenderer>();
        GameObject Raw() => Own(Object.Instantiate(source));

        [Test]
        public void SavedMeshChangesOnlyTheReviewedSurfaceData()
        {
            AvatarSurfaceCorrectionSetup.ValidateSource(profile.SourceMesh);
            AvatarSurfaceCorrectionSetup.ValidateCorrected(profile.SourceMesh, profile.CorrectedMesh);
            Assert.AreEqual(AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh, false), AvatarSurfaceMeshData.Fingerprint(profile.CorrectedMesh, false));
            Assert.AreNotEqual(AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh), AvatarSurfaceMeshData.Fingerprint(profile.CorrectedMesh));
            Assert.AreEqual(AvatarSurfaceCorrectionSetup.MeshPath, AssetDatabase.GetAssetPath(profile.CorrectedMesh));
            Assert.AreEqual(AvatarSurfaceCorrectionSetup.ExpectedSource, profile.SourceFingerprint);
        }

        [Test]
        public void FreshSpawnsShareOneSavedMeshAndLeaveLibraryEntryUnchanged()
        {
            var library = ModelLibrary.Load();
            var first = Own(library.Spawn("Avatar/Avatar", null));
            var second = Own(library.Spawn("Avatar/Avatar", null));
            Assert.AreSame(profile.CorrectedMesh, Head(first).sharedMesh);
            Assert.AreSame(Head(first).sharedMesh, Head(second).sharedMesh);
            Assert.AreSame(source, library.Find("Avatar/Avatar"));
            Assert.AreSame(profile.SourceMesh, Head(source).sharedMesh);
            Assert.IsFalse(profile.TryApply(source, first), "Applying again is an idempotent no-op.");
        }

        [Test]
        public void ExistingAuthoredInstanceKeepsPoseWeightsMaterialsAndBounds()
        {
            var model = Raw(); var head = Head(model);
            model.transform.localPosition = new Vector3(.4f, .2f, -.3f);
            model.transform.localScale = Vector3.one * .87f;
            head.enabled = false;
            int blink = head.sharedMesh.GetBlendShapeIndex("Blink"), brow = head.sharedMesh.GetBlendShapeIndex("BrowRelax");
            Assert.That(blink, Is.GreaterThanOrEqualTo(0)); Assert.That(brow, Is.GreaterThanOrEqualTo(0));
            head.SetBlendShapeWeight(blink, 37); head.SetBlendShapeWeight(brow, 81);
            var localBounds = new Bounds(head.localBounds.center + Vector3.up * .1f, head.localBounds.size * 1.1f);
            head.localBounds = localBounds;
            var materials = head.sharedMaterials; var bones = head.bones; var rootBone = head.rootBone;
            var matrix = head.localToWorldMatrix;
            var block = new MaterialPropertyBlock(); block.SetFloat("_DiagnosticPreserved", .73f); head.SetPropertyBlock(block);
            Assert.IsTrue(profile.TryApply(source, model));
            Assert.AreEqual(matrix, head.localToWorldMatrix); Assert.AreEqual(localBounds, head.localBounds);
            CollectionAssert.AreEqual(materials, head.sharedMaterials); CollectionAssert.AreEqual(bones, head.bones);
            Assert.AreSame(rootBone, head.rootBone); Assert.IsFalse(head.enabled);
            Assert.AreEqual(37, head.GetBlendShapeWeight(blink)); Assert.AreEqual(81, head.GetBlendShapeWeight(brow));
            var afterBlock = new MaterialPropertyBlock(); head.GetPropertyBlock(afterBlock);
            Assert.AreEqual(.73f, afterBlock.GetFloat("_DiagnosticPreserved"));
            Assert.AreEqual(AvatarSurfaceCorrectionSetup.ExpectedSource, AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh));
        }

        [Test]
        public void UnrelatedMeshSourceAndOriginalAssetAreNeverRebound()
        {
            Assert.IsFalse(profile.TryApply(source, source));
            Assert.AreSame(profile.SourceMesh, Head(source).sharedMesh);
            var model = Raw(); var head = Head(model); var custom = Own(Object.Instantiate(profile.SourceMesh));
            head.sharedMesh = custom;
            Assert.IsFalse(profile.TryApply(source, model)); Assert.AreSame(custom, head.sharedMesh);
            var foreignSource = Own(new GameObject("Different avatar source"));
            var originalMeshInstance = Raw();
            Assert.IsFalse(profile.TryApply(foreignSource, originalMeshInstance));
            Assert.AreSame(profile.SourceMesh, Head(originalMeshInstance).sharedMesh);
            Assert.IsFalse(AvatarSurfaceProfile.ApplyDefault("Items/Head", source, originalMeshInstance));
        }

        [Test]
        public void ExactIdentityAllowanceRejectsAnyOtherChangedMesh()
        {
            var model = Raw();
            Assert.IsTrue(profile.TryApply(source, model));
            Mesh[] Read(GameObject value) => value.GetComponentsInChildren<MeshFilter>(true).Select(renderer => renderer.sharedMesh)
                .Concat(value.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(renderer => renderer.sharedMesh)).ToArray();
            Assert.IsTrue(profile.MatchesOnlyBoundHeadReplacement(source, model, Read(source), Read(model)));
            var other = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(renderer => renderer.name != "SK_Head");
            other.sharedMesh = Own(Object.Instantiate(other.sharedMesh));
            Assert.IsFalse(profile.MatchesOnlyBoundHeadReplacement(source, model, Read(source), Read(model)));
            Assert.IsFalse(profile.MatchesOnlyBoundHeadReplacement(source, source, Read(source), Read(source)));
        }

        [Test]
        public void NewEditorPreviewUsesSavedCorrectionAndExistingPreviewRemainsAuthored()
        {
            var parent = Own(new GameObject("Avatar surface preview test"));
            var preview = PlayerAuthoring.CreatePreview(parent.transform);
            var skin = preview.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(renderer => renderer.name == "SK_Head");
            Assert.AreSame(profile.CorrectedMesh, skin.sharedMesh);
            var custom = Own(Object.Instantiate(profile.SourceMesh));
            skin.sharedMesh = custom;
            preview.transform.localPosition = new Vector3(.2f, .1f, -.1f);
            Assert.AreSame(preview, PlayerAuthoring.CreatePreview(parent.transform));
            Assert.AreSame(custom, skin.sharedMesh, "Existing authoring preview overrides must remain untouched.");
            Assert.AreEqual(new Vector3(.2f, .1f, -.1f), preview.transform.localPosition);
        }

        [Test]
        public void RepeatedMissingOnlySetupPreservesSavedAssetsAndOriginalSources()
        {
            var paths = new List<string> { AvatarSurfaceCorrectionSetup.AvatarPath, AvatarSurfaceCorrectionSetup.MeshPath,
                AvatarSurfaceCorrectionSetup.ProfilePath, AssetDatabase.GetAssetPath(ModelLibrary.Load()) };
            paths.AddRange(Head(source).sharedMaterials.Select(AssetDatabase.GetAssetPath));
            paths.AddRange(Directory.EnumerateFiles("Assets/_Project/UI/Fonts", "*", SearchOption.TopDirectoryOnly).Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)));
            paths.AddRange(Directory.EnumerateFiles("Assets/_Project/Resources/Fonts", "*", SearchOption.TopDirectoryOnly).Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)));
            paths = paths.Where(path => !string.IsNullOrEmpty(path)).Distinct().SelectMany(path => new[] { path, path + ".meta" }).ToList();
            var hashes = paths.ToDictionary(path => path, path => AvatarSurfaceMeshData.Hash(File.ReadAllBytes(path)));
            string sceneState = SceneState();
            string sourceMemory = AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh);
            var clips = ModelLibrary.Load().Entries.Single(entry => entry.key == "Avatar/Avatar").clips.ToArray();
            Assert.AreSame(profile, AvatarSurfaceCorrectionSetup.EnsureDefaults());
            Assert.AreSame(profile, AvatarSurfaceCorrectionSetup.EnsureDefaults());
            foreach (var pair in hashes) Assert.AreEqual(pair.Value, AvatarSurfaceMeshData.Hash(File.ReadAllBytes(pair.Key)), pair.Key);
            Assert.AreEqual(sourceMemory, AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh));
            CollectionAssert.AreEqual(clips, ModelLibrary.Load().Entries.Single(entry => entry.key == "Avatar/Avatar").clips);
            Assert.AreEqual(sceneState, SceneState(), "Repeat setup changed active/loaded scenes, roots or dirty state.");
        }

        static string SceneState()
        {
            var state = new List<string> { "active=" + SceneManager.GetActiveScene().handle.GetRawData() };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                state.Add(scene.handle.GetRawData() + ":" + scene.path + ":" + scene.isLoaded + ":" + scene.isDirty);
                state.AddRange(scene.GetRootGameObjects().OrderBy(root => root.GetEntityId().ToString(), StringComparer.Ordinal)
                    .Select(root => root.GetEntityId() + ":" + root.name));
            }
            return string.Join("\n", state);
        }

        [TestCase("Idle", 0f)]
        [TestCase("Walk_InPlace", .37f)]
        [TestCase("Swing_OneHand", .41f)]
        public void ExpressionsAndAnimationPreserveDeformedPositionsAndCorrectFaceNormals(string clipName, float phase)
        {
            var original = Raw(); var corrected = Raw();
            Assert.IsTrue(profile.TryApply(source, corrected));
            var clip = ModelLibrary.Load().FindClip("Avatar/Avatar", clipName);
            Assert.IsNotNull(clip);
            clip.SampleAnimation(original, clip.length * phase); clip.SampleAnimation(corrected, clip.length * phase);
            var originalHead = Head(original); var correctedHead = Head(corrected);
            var originalBaked = Own(new Mesh()); var correctedBaked = Own(new Mesh());
            int blink = profile.SourceMesh.GetBlendShapeIndex("Blink"), brow = profile.SourceMesh.GetBlendShapeIndex("BrowRelax");
            foreach (float blinkWeight in new[] { 0f, 50f, 100f })
                foreach (float browWeight in new[] { 0f, 50f, 100f })
                {
                    foreach (var skin in new[] { originalHead, correctedHead })
                    { skin.SetBlendShapeWeight(blink, blinkWeight); skin.SetBlendShapeWeight(brow, browWeight); }
                    originalHead.BakeMesh(originalBaked, false); correctedHead.BakeMesh(correctedBaked, false);
                    var left = originalBaked.vertices; var right = correctedBaked.vertices;
                    Assert.AreEqual(left.Length, right.Length);
                    float maxError = 0;
                    for (int i = 0; i < left.Length; i++) maxError = Mathf.Max(maxError, Vector3.Distance(left[i], right[i]));
                    string context = clipName + " blink=" + blinkWeight + " brow=" + browWeight;
                    Assert.That(maxError, Is.LessThan(0.000001f), context + " changed deformed positions");
                    var face = AvatarSurfaceCorrectionSetup.Measure(correctedBaked, 0);
                    Assert.AreEqual(0, face.invalid, context); Assert.AreEqual(0, face.zero, context);
                    Assert.AreEqual(0, face.opposed, context); Assert.That(face.meanDot, Is.GreaterThan(.97), context);
                    for (int submesh = 1; submesh < correctedBaked.subMeshCount; submesh++)
                    {
                        var surface = AvatarSurfaceCorrectionSetup.Measure(correctedBaked, submesh);
                        Assert.AreEqual(0, surface.invalid, context);
                        Assert.That(surface.meanDot, Is.GreaterThan(.8), context + " submesh=" + submesh);
                    }
                }
            CollectionAssert.AreEqual(originalHead.sharedMaterials, correctedHead.sharedMaterials);
            Assert.AreEqual(AvatarSurfaceCorrectionSetup.ExpectedSource, AvatarSurfaceMeshData.Fingerprint(profile.SourceMesh));
        }
    }
}
