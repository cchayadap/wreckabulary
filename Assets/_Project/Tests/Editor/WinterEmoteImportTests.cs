using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class WinterEmoteImportTests
    {
        [Test]
        public void SeparateGenericTakeTargetsTheExistingAvatarWithoutReplacingItsClips()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(WinterEmoteImporter.ClipPath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(1, clips.Length);
            var clip = clips[0];
            Assert.AreEqual("WinterShuffle", clip.name);
            Assert.That(clip.length, Is.EqualTo(2.4f).Within(.001f));
            Assert.IsFalse(clip.isLooping);
            Assert.IsFalse(clip.legacy);
            Assert.IsFalse(clip.isHumanMotion);
            var importer = (ModelImporter)AssetImporter.GetAtPath(WinterEmoteImporter.ClipPath);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterAnimationCompression.Off, importer.animationCompression);
            var library = ModelLibrary.Load();
            var model = library.Find("Avatar/Avatar");
            var animator = model.GetComponentInChildren<Animator>(true);
            var bindings = AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)).ToArray();
            Assert.IsNotEmpty(bindings, "A named take must contain actual skeletal motion.");
            foreach (var binding in bindings)
                Assert.NotNull(string.IsNullOrEmpty(binding.path) ? animator.transform : animator.transform.Find(binding.path),
                    "Emote binding does not resolve on the original avatar: " + binding.path);
            foreach (string bone in new[] { "head", "hand_L", "hand_R", "shin_L", "shin_R", "foot_L", "foot_R" })
                Assert.IsTrue(bindings.Any(binding => binding.path.EndsWith("/" + bone, StringComparison.Ordinal)), bone);
            Assert.AreEqual(17, library.Entries.Single(entry => entry.key == "Avatar/Avatar").clips.Length);
            Assert.IsEmpty(AssetDatabase.LoadAssetAtPath<GameObject>(WinterEmoteImporter.ClipPath).GetComponentsInChildren<Renderer>(true),
                "The seasonal take contains only its compatible skeleton, not a second avatar model.");
        }
    }
}
