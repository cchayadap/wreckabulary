using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Online;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class WordboundWardrobeTests
    {
        GameObject root;
        GameData broken;
        TextAsset invalid;

        [TearDown]
        public void TearDown()
        {
            WordboundWardrobe.Use(null);
            if (root) Object.DestroyImmediate(root);
            if (broken) Object.DestroyImmediate(broken);
            if (invalid) Object.DestroyImmediate(invalid);
        }

        GameData BrokenHouseData()
        {
            var installed = Resources.Load<GameData>(GameData.ResourcePath);
            Assert.IsTrue(installed && installed.wardrobe, "The installed avatar wardrobe must exist.");
            invalid = new TextAsset("{\"schema\":1,\"items\":[{\"id\":\"not a word\"}],\"tuning\":{\"unarmed\":\"broken\"}}");
            broken = ScriptableObject.CreateInstance<GameData>();
            broken.rules = invalid; broken.items = invalid; broken.house = invalid; broken.houseItems = invalid;
            broken.wardrobe = installed.wardrobe;
            return broken;
        }

        [Test]
        public void InstalledWardrobeLoadsAndValidatesOnItsOwn()
        {
            var wardrobe = WordboundWardrobe.Current;
            Assert.IsEmpty(wardrobe.Validate());
            Assert.IsNotEmpty(wardrobe.Pieces);
            Assert.AreSame(wardrobe, WordboundWardrobe.Current);
        }

        [Test]
        public void MissingWardrobeIsReportedInsteadOfFallingBackToHouseData()
        {
            var empty = ScriptableObject.CreateInstance<GameData>();
            try { Assert.Throws<InvalidOperationException>(() => WordboundWardrobe.Load(empty)); }
            finally { Object.DestroyImmediate(empty); }
        }

        [Test]
        public void WordboundDressingWorksWhenHouseItemsAndTuningAreInvalid()
        {
            var data = BrokenHouseData();
            Assert.That(() => data.Parse(), Throws.Exception, "The fixture must make the House configuration unusable.");
            var wardrobe = WordboundWardrobe.Load(data);
            WordboundWardrobe.Use(wardrobe);
            root = new GameObject("Wordbound wardrobe root");
            var model = new GameObject("Wordbound wardrobe model");
            model.transform.SetParent(root.transform, false);
            foreach (var piece in wardrobe.Pieces.Where(p => !string.IsNullOrEmpty(p.Mesh)))
            {
                var part = new GameObject(piece.Mesh);
                part.transform.SetParent(model.transform, false);
                part.AddComponent<SkinnedMeshRenderer>();
            }
            var visual = new WordboundVisualInstance(root, model, Vector3.zero, 1f, true);
            visual.Dress(null);
            var expected = PlayerAppearance.DefaultPresentationOutfit(wardrobe);
            Assert.AreEqual(expected.Serialize(), visual.Outfit.Serialize());
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.IsNotEmpty(renderers);
            foreach (var renderer in renderers)
            {
                var piece = wardrobe.Pieces.First(p => p.Mesh == renderer.name);
                bool selected = expected.PieceIn(piece.Slot) == piece.Id;
                Assert.AreEqual(selected, renderer.enabled, renderer.name);
                Assert.AreEqual(!selected, renderer.forceRenderingOff, renderer.name);
            }
            Assert.IsTrue(renderers.Any(r => r.enabled));
            Assert.IsTrue(renderers.Any(r => !r.enabled));
            visual.Dress(expected.Serialize());
            Assert.AreEqual(expected.Serialize(), visual.Outfit.Serialize());
        }
    }
}
