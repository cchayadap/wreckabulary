using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class LobbyRuntimeTypographyTests
    {
        readonly List<Object> originals = new();
        LobbyRuntimeTypography.OwnedCache cache;

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(Application.isPlaying, "Run this ownership fixture in Edit mode.");
            cache = new LobbyRuntimeTypography.OwnedCache();
        }

        [TearDown]
        public void TearDown()
        {
            cache?.Dispose();
            for (int i = originals.Count - 1; i >= 0; i--)
                if (originals[i]) Object.DestroyImmediate(originals[i]);
            originals.Clear();
        }

        TMP_FontAsset Font()
        {
            var source = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
            Assert.IsNotNull(source);
            var font = TMP_FontAsset.CreateFontAsset(source, 32, 4, GlyphRenderMode.SDFAA, 256, 256);
            Assert.IsNotNull(font);
            originals.Add(font);
            Assert.IsTrue(font.TryAddCharacters("A_…"));
            return font;
        }

        [Test]
        public void DynamicGlyphsUseIndependentTablesAndTheOwnedCachedAtlas()
        {
            var source = Font();
            var originalAtlas = source.atlasTexture;
            var pixels = originalAtlas.GetPixels32();
            string definition = EditorJsonUtility.ToJson(source);
            var copy = cache.Font(source);
            Assert.AreNotSame(source, copy);
            Assert.AreSame(copy.atlasTextures[0], copy.atlasTexture);
            Assert.AreNotSame(originalAtlas, copy.atlasTexture);
            CollectionAssert.AreEqual(pixels, copy.atlasTexture.GetPixels32());
            Assert.AreNotSame(source.characterTable[0], copy.characterTable[0]);
            Assert.AreNotSame(source.glyphTable[0], copy.glyphTable[0]);
            Assert.AreNotSame(source.fontFeatureTable, copy.fontFeatureTable);
            Assert.AreSame(copy, copy.characterTable[0].textAsset);
            Assert.IsTrue(copy.TryAddCharacters("B’‹›"));
            Assert.Greater(copy.characterTable.Count, source.characterTable.Count);
            Assert.AreEqual(definition, EditorJsonUtility.ToJson(source));
            CollectionAssert.AreEqual(pixels, originalAtlas.GetPixels32());
            Assert.IsFalse(EditorUtility.IsPersistent(copy));
            Assert.IsFalse(EditorUtility.IsPersistent(copy.material));
            Assert.IsFalse(EditorUtility.IsPersistent(copy.atlasTexture));
        }

        [Test]
        public void CyclicFallbackAndEveryWeightReferenceResolveToCachedOwnedFonts()
        {
            var body = Font();
            var bold = Font();
            body.fontWeightTable[7].regularTypeface = bold;
            body.fontWeightTable[7].italicTypeface = body;
            body.fallbackFontAssetTable = new List<TMP_FontAsset> { bold };
            bold.fallbackFontAssetTable = new List<TMP_FontAsset> { body };
            bold.fontWeightTable[7].regularTypeface = bold;
            var copy = cache.Font(body);
            var copiedBold = cache.Font(bold);
            int fonts = cache.FontCount;
            Assert.AreSame(copiedBold, copy.fontWeightTable[7].regularTypeface);
            Assert.AreSame(copy, copy.fontWeightTable[7].italicTypeface);
            Assert.AreSame(copiedBold, copiedBold.fontWeightTable[7].regularTypeface);
            Assert.AreSame(copiedBold, copy.fallbackFontAssetTable[0]);
            Assert.AreSame(copy, copiedBold.fallbackFontAssetTable[0]);
            Assert.AreSame(copy, cache.Font(body));
            Assert.AreSame(copy, cache.Font(copy));
            Assert.AreEqual(fonts, cache.FontCount);
            Assert.AreSame(bold, body.fontWeightTable[7].regularTypeface);
            Assert.AreSame(body, bold.fallbackFontAssetTable[0]);
        }

        [Test]
        public void AuthoredPresetPropertiesArePreservedWhilePaddingWritesStayOwned()
        {
            var font = Font();
            var preset = new Material(font.material);
            originals.Add(preset);
            preset.EnableKeyword(ShaderUtilities.Keyword_Outline);
            preset.SetFloat("_OutlineWidth", .19f);
            preset.SetColor("_FaceColor", new Color(.2f, .4f, .6f, .8f));
            string before = EditorJsonUtility.ToJson(preset);
            var copy = cache.Material(font, preset);
            Assert.AreNotSame(preset, copy);
            Assert.AreSame(cache.Font(font).atlasTexture, copy.mainTexture);
            Assert.AreEqual(preset.shader, copy.shader);
            Assert.AreEqual(preset.GetFloat("_OutlineWidth"), copy.GetFloat("_OutlineWidth"));
            Assert.AreEqual(preset.GetColor("_FaceColor"), copy.GetColor("_FaceColor"));
            CollectionAssert.AreEquivalent(preset.shaderKeywords, copy.shaderKeywords);
            int materials = cache.MaterialCount;
            Assert.AreSame(copy, cache.Material(cache.Font(font), preset));
            Assert.AreSame(copy, cache.Material(font, copy));
            Assert.AreEqual(materials, cache.MaterialCount);
            ShaderUtilities.UpdateShaderRatios(copy);
            Assert.AreEqual(before, EditorJsonUtility.ToJson(preset));
        }

        [Test]
        public void SessionBoundaryRetainsSurvivingConsumerThenReleasesOnlyOwnedResources()
        {
            var font = Font();
            var sourceAtlas = font.atlasTexture;
            var sourceMaterial = font.material;
            var labelObject = new GameObject("Owned typography consumer", typeof(RectTransform));
            originals.Add(labelObject);
            labelObject.SetActive(false);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = cache.Font(font);
            var owned = label.font;
            var atlas = owned.atlasTexture;
            var material = owned.material;
            Assert.IsFalse(cache.TryDisposeUnused(), "A stopped session may retain consumers when scene reload is disabled.");
            Assert.AreSame(owned, cache.Font(font));
            Assert.IsTrue(atlas);
            Object.DestroyImmediate(labelObject);
            Assert.IsTrue(cache.TryDisposeUnused());
            Assert.IsFalse(owned);
            Assert.IsFalse(atlas);
            Assert.IsFalse(material);
            Assert.IsTrue(sourceAtlas);
            Assert.IsTrue(sourceMaterial);
            Assert.IsTrue(font);
            Assert.DoesNotThrow(() => cache.Dispose());
        }

        [Test]
        public void FailedGraphCreationReleasesPartialClonesAndPreservesOriginals()
        {
            var primary = Font();
            var invalid = Font();
            primary.fallbackFontAssetTable = new List<TMP_FontAsset> { invalid };
            var wrongAtlas = new Texture2D(2, 2);
            originals.Add(wrongAtlas);
            invalid.material.mainTexture = wrongAtlas;
            var primaryAtlas = primary.atlasTexture;
            var invalidAtlas = invalid.atlasTexture;
            Assert.Throws<InvalidOperationException>(() => cache.Font(primary));
            Assert.AreEqual(0, cache.FontCount);
            Assert.AreEqual(0, cache.MaterialCount);
            Assert.IsTrue(primaryAtlas);
            Assert.IsTrue(invalidAtlas);
            Assert.IsTrue(primary.material);
            Assert.IsTrue(invalid.material);
            invalid.material.mainTexture = invalidAtlas;
            Assert.AreNotSame(primary, cache.Font(primary), "The same cache can retry after repairing a rejected source.");
        }

        [Test]
        public void EditModeResolutionKeepsAuthoringReferences()
        {
            var font = Font();
            Assert.AreSame(font, LobbyRuntimeTypography.ResolveFont(font));
            Assert.AreSame(font.material, LobbyRuntimeTypography.ResolveMaterial(font, font.material));
        }

        [Test]
        public void ReloadHandoffRetainsOwnedIdentityAndOriginalProvenance()
        {
            var source = Font();
            var preset = new Material(source.material);
            originals.Add(preset);
            var ownedFont = cache.Font(source);
            var ownedPreset = cache.Material(source, preset);
            var state = cache.DetachForReload();
            originals.Add(state);
            Assert.Throws<ObjectDisposedException>(() => cache.Font(source));
            cache = LobbyRuntimeTypography.OwnedCache.Restore(state);
            Assert.IsFalse(state);
            Assert.AreSame(ownedFont, cache.Font(source));
            Assert.AreSame(ownedFont, cache.Font(ownedFont));
            Assert.AreSame(ownedPreset, cache.Material(source, preset));
            Assert.AreSame(ownedPreset, cache.Material(ownedFont, preset));
            cache.Dispose();
            Assert.IsFalse(ownedFont);
            Assert.IsFalse(ownedPreset);
            Assert.IsTrue(source);
            Assert.IsTrue(preset);
        }

        [UnityTest]
        public IEnumerator NonReadableStaticFallbackCopiesEveryAlphaMipAndOwnsItsTexture()
        {
            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback, "Exact native atlas-byte verification requires GPU readback.");
            var primary = Font();
            var fallback = Font();
            var oldAtlas = fallback.atlasTextures[0];
            var expected = new byte[3][];
            expected[0] = oldAtlas.GetRawTextureData<byte>().ToArray();
            for (int mip = 1; mip < expected.Length; mip++)
            {
                expected[mip] = new byte[(oldAtlas.width >> mip) * (oldAtlas.height >> mip)];
                for (int i = 0; i < expected[mip].Length; i++) expected[mip][i] = (byte)((i * 37 + mip * 11) % 251);
            }
            var atlas = new Texture2D(oldAtlas.width, oldAtlas.height, TextureFormat.Alpha8, expected.Length, true)
            {
                name = "Non-readable static fallback atlas", filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Mirror,
                wrapModeW = TextureWrapMode.Repeat, anisoLevel = 2, mipMapBias = .25f, ignoreMipmapLimit = true
            };
            originals.Add(atlas);
            var bytes = new List<byte>();
            foreach (var mip in expected) bytes.AddRange(mip);
            atlas.LoadRawTextureData(bytes.ToArray());
            atlas.Apply(false, true);
            fallback.atlasTextures = new[] { atlas };
            fallback.material.mainTexture = atlas;
            fallback.atlasPopulationMode = AtlasPopulationMode.Static;
            Object.DestroyImmediate(oldAtlas);
            primary.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            string sourceDefinition = EditorJsonUtility.ToJson(fallback);
            var copy = cache.Font(primary).fallbackFontAssetTable[0];
            var copiedAtlas = copy.atlasTextures[0];
            Assert.AreNotSame(fallback, copy);
            Assert.AreNotSame(atlas, copiedAtlas);
            Assert.AreSame(copiedAtlas, copy.atlasTexture);
            Assert.AreSame(copiedAtlas, copy.material.mainTexture);
            Assert.IsFalse(atlas.isReadable);
            Assert.IsFalse(copiedAtlas.isReadable);
            Assert.AreEqual(TextureFormat.Alpha8, copiedAtlas.format);
            Assert.AreEqual(atlas.graphicsFormat, copiedAtlas.graphicsFormat);
            Assert.AreEqual(atlas.isDataSRGB, copiedAtlas.isDataSRGB);
            Assert.AreEqual(atlas.mipmapCount, copiedAtlas.mipmapCount);
            Assert.AreEqual(atlas.filterMode, copiedAtlas.filterMode);
            Assert.AreEqual(atlas.wrapModeU, copiedAtlas.wrapModeU);
            Assert.AreEqual(atlas.wrapModeV, copiedAtlas.wrapModeV);
            Assert.AreEqual(atlas.wrapModeW, copiedAtlas.wrapModeW);
            Assert.AreEqual(atlas.anisoLevel, copiedAtlas.anisoLevel);
            Assert.AreEqual(atlas.mipMapBias, copiedAtlas.mipMapBias);
            for (int mip = 0; mip < expected.Length; mip++)
            {
                yield return AssertGpuBytes(atlas, mip, expected[mip]);
                yield return AssertGpuBytes(copiedAtlas, mip, expected[mip]);
            }
            Assert.AreEqual(sourceDefinition, EditorJsonUtility.ToJson(fallback));
            cache.Dispose();
            Assert.IsFalse(copy);
            Assert.IsFalse(copiedAtlas);
            Assert.IsTrue(atlas);
            Assert.IsTrue(fallback.material);
            for (int mip = 0; mip < expected.Length; mip++) yield return AssertGpuBytes(atlas, mip, expected[mip]);
        }

        static IEnumerator AssertGpuBytes(Texture2D texture, int mip, byte[] expected)
        {
            var request = AsyncGPUReadback.Request(texture, mip);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!request.done && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(request.done, "GPU atlas readback timed out: " + texture.name);
            Assert.IsFalse(request.hasError, "GPU atlas readback failed: " + texture.name);
            CollectionAssert.AreEqual(expected, request.GetData<byte>().ToArray(), texture.name + " mip " + mip);
        }
    }
}
