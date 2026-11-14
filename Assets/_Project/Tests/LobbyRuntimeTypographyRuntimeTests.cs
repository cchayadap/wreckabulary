using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class LobbyRuntimeTypographyRuntimeTests
    {
        GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedShopTextGrowsOnlyOwnedFontsAndPreservesAuthoredStyles()
        {
            Assert.IsTrue(Application.isPlaying, "Run this fixture in Play mode.");
            var layout = Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath);
            Assert.IsNotNull(layout, "Run the explicit saved Shop setup first.");
            var sources = layout.Offer.GetComponentsInChildren<TMP_Text>(true);
            Assert.Greater(sources.Length, 0);
            var fonts = sources.Select(label => label.font).Distinct().ToArray();
            var glyphs = fonts.Select(font => font.characterTable.Count).ToArray();
            var pixels = fonts.Select(font => font.atlasTexture.GetPixels32()).ToArray();
            var presets = sources.Select(label => label.fontSharedMaterial).Distinct().ToArray();
            var ratios = presets.Select(material => material.GetFloat("_ScaleRatioA")).ToArray();
            root = new GameObject("Typography runtime test", typeof(RectTransform), typeof(Canvas));
            var copy = LobbyRuntimeTypography.Instantiate(layout.Offer, root.transform, false);
            var labels = copy.GetComponentsInChildren<TMP_Text>(true);
            Assert.AreEqual(sources.Length, labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                Assert.AreNotSame(sources[i].font, labels[i].font);
                Assert.AreSame(LobbyRuntimeTypography.ResolveFont(sources[i].font), labels[i].font);
                Assert.AreSame(LobbyRuntimeTypography.ResolveMaterial(sources[i].font, sources[i].fontSharedMaterial), labels[i].fontSharedMaterial);
                Assert.AreEqual(sources[i].fontSharedMaterial.GetColor("_FaceColor"), labels[i].fontSharedMaterial.GetColor("_FaceColor"));
                labels[i].text = "Courier’s ‹kit›";
                labels[i].ForceMeshUpdate(true, true);
            }
            yield return null;
            for (int i = 0; i < fonts.Length; i++)
            {
                Assert.AreEqual(glyphs[i], fonts[i].characterTable.Count);
                CollectionAssert.AreEqual(pixels[i], fonts[i].atlasTexture.GetPixels32());
            }
            for (int i = 0; i < presets.Length; i++) Assert.AreEqual(ratios[i], presets[i].GetFloat("_ScaleRatioA"));
            var second = LobbyRuntimeTypography.Instantiate(layout.Offer, root.transform, false);
            Assert.AreSame(copy.Title.font, second.Title.font);
            Assert.AreSame(copy.Title.fontSharedMaterial, second.Title.fontSharedMaterial);
        }
    }
}
