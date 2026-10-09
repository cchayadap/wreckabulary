using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public class NativeAnimationBindingTests
    {
        const string AvatarKey = "Avatar/Avatar";

        [Test]
        public void RuntimeBindingsAreTheSeventeenImportedClipSubassets()
        {
            var report = ImportedArtSettings.ReadReport().Single(file => file.Kind == "avatar");
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            Assert.IsNotNull(library);
            var entry = library.Entries.Single(model => model.key == AvatarKey);
            var imported = AssetDatabase.LoadAllAssetsAtPath(report.Output).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => ImportedArtSettings.ClipName(clip.name), StringComparer.Ordinal).ToArray();
            Assert.AreEqual(17, imported.Length);
            Assert.IsNotNull(entry.clips);
            Assert.AreEqual(17, entry.clips.Length);
            CollectionAssert.AreEqual(imported, entry.clips, "retain the actual FBX subasset identities in deterministic order");
            CollectionAssert.AreEquivalent(report.Clips, entry.clips.Select(clip => ImportedArtSettings.ClipName(clip.name)));
            foreach (var clip in imported)
            {
                Assert.AreEqual(report.Output, AssetDatabase.GetAssetPath(clip));
                Assert.AreEqual(clip, library.FindClip(AvatarKey, ImportedArtSettings.ClipName(clip.name)));
            }
        }

        [Test]
        public void BindingRefreshPreservesTheImportedModelAndTwentyTwoBoneRig()
        {
            var report = ImportedArtSettings.ReadReport().Single(file => file.Kind == "avatar");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(report.Output);
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ImportedArtSettings.ModelLibraryAssetPath);
            Assert.AreEqual(model, library.Find(AvatarKey), "the library references the supplied FBX model");
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.AreEqual(11, skins.Length);
            var bones = skins.SelectMany(skin => skin.bones).Distinct().ToArray();
            Assert.AreEqual(22, bones.Length);
            CollectionAssert.AreEquivalent(report.Bones, bones.Select(bone => bone.name));
            var animator = model.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator);
            Assert.IsFalse(animator.isHuman, "preserve the supplied Generic rig");
            Assert.IsNull(animator.runtimeAnimatorController, "the runtime graph owns clip playback");
        }

        [Test]
        public void MissingReportedClipRejectsRefreshBeforeSavingTheLibrary()
        {
            var report = ImportedArtSettings.ReadReport();
            report.Single(file => file.Kind == "avatar").Clips.Add("MissingBindingRegression");
            string before = File.ReadAllText(ImportedArtSettings.ModelLibraryAssetPath);
            var failure = Assert.Throws<InvalidOperationException>(() => ModelLibraryBuilder.Build(report));
            StringAssert.Contains("MissingBindingRegression", failure.Message);
            Assert.AreEqual(before, File.ReadAllText(ImportedArtSettings.ModelLibraryAssetPath));
        }

        [Test]
        public void WalkAndRunCadenceFollowsTheAuthoredStrides()
        {
            var root = Json.Parse(File.ReadAllText(ImportedArtSettings.ReportPath), "build_report.json");
            var locomotion = root["files"].Items.Single(file => file["kind"].String() == "avatar")["locomotion"];
            Assert.That(PlayerAppearance.WalkStride, Is.EqualTo(locomotion["Walk_InPlace"]["stride"].Float()).Within(.0001f));
            Assert.That(PlayerAppearance.RunStride, Is.EqualTo(locomotion["Run_InPlace"]["stride"].Float()).Within(.0001f));
            Assert.Greater(PlayerAppearance.CyclesPerSecond(false, 1f), PlayerAppearance.CyclesPerSecond(false, .5f));
            Assert.Greater(PlayerAppearance.CyclesPerSecond(true, 3.5f), PlayerAppearance.CyclesPerSecond(true, 3f));
            Assert.That(PlayerAppearance.CyclesPerSecond(true, 6.5f), Is.InRange(3f, 3.6f), "a sprint stays legible");
            Assert.That(PlayerAppearance.CyclesPerSecond(false, 2.5f), Is.InRange(2f, 2.4f));
            Assert.That(PlayerAppearance.CyclesPerSecond(false, .05f), Is.GreaterThanOrEqualTo(.6f), "a creep still steps");
        }
    }
}
