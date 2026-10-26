using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.EditorTools;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests.Editor
{
    public sealed class ShopGraphicPersistenceTests
    {
        string folder;

        [SetUp]
        public void CreateOwnedFolder()
        {
            folder = "Assets/_Project/Tests/ShopGraphicTemp-" + Guid.NewGuid().ToString("N");
            WorkspaceAssetCopies.EnsureFolder(folder);
        }

        [TearDown]
        public void RemoveOnlyOwnedAssets()
        {
            if (string.IsNullOrEmpty(folder)) return;
            Assert.IsTrue(folder.StartsWith("Assets/_Project/Tests/ShopGraphicTemp-", StringComparison.Ordinal));
            if (AssetDatabase.IsValidFolder(folder)) Assert.IsTrue(AssetDatabase.DeleteAsset(folder));
        }

        Sprite Persist(Sprite source)
        {
            var method = typeof(LobbyShopLayoutSetup).GetMethod("PersistSprite", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            try { return (Sprite)method.Invoke(null, new object[] { source, folder, new Dictionary<Sprite, Sprite>() }); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        static Color32[][] Pixels(bool mipmaps) => mipmaps
            ? new[] { Pixels(false)[0], new[] { new Color32(47, 112, 203, 87) } }
            : new[] { new[] {
                new Color32(13, 57, 139, 0), new Color32(63, 127, 191, 64),
                new Color32(222, 91, 38, 173), new Color32(249, 213, 157, 255)
            } };

        static Sprite Source(Color32[][] pixels, bool linear, bool unreadable, Vector2 pivot)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, pixels.Length, linear)
            {
                name = "Same graphic texture", filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, anisoLevel = 0, mipMapBias = .25f
            };
            try
            {
                for (int mip = 0; mip < pixels.Length; mip++) texture.SetPixels32(pixels[mip], mip);
                texture.Apply(false, unreadable);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), pivot, 37.5f, 0,
                    SpriteMeshType.FullRect, new Vector4(.25f, .5f, .25f, .5f));
                sprite.name = "Same graphic";
                return sprite;
            }
            catch { Object.DestroyImmediate(texture); throw; }
        }

        static void DestroySource(Sprite source)
        {
            if (!source) return;
            var texture = source.texture;
            Object.DestroyImmediate(source);
            if (texture) Object.DestroyImmediate(texture);
        }

        static void PrepareTarget(RenderTexture target, Texture2D source)
        {
            var method = typeof(LobbyShopLayoutSetup).GetMethod("PrepareReadbackTarget", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            try { method.Invoke(null, new object[] { target, source, 0 }); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }

        [TestCase(2, true)]
        [TestCase(4, false)]
        public void DeferredReadbackTargetIsCreatedAndStillRequiresExactDimensions(int targetWidth, bool matches)
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "Run target creation with graphics enabled.");
            Sprite source = null;
            RenderTexture target = null;
            var previous = RenderTexture.active;
            try
            {
                source = Source(Pixels(false), false, false, new Vector2(.25f, .75f));
                target = new RenderTexture(new RenderTextureDescriptor(targetWidth, 2)
                {
                    graphicsFormat = source.texture.graphicsFormat, depthBufferBits = 0, msaaSamples = 1,
                    useMipMap = false, autoGenerateMips = false
                });
                Assert.IsFalse(target.IsCreated(), "Exercise explicit creation of a deferred hardware resource.");
                if (matches)
                {
                    PrepareTarget(target, source.texture);
                    Assert.IsTrue(target.IsCreated());
                    Assert.AreEqual(source.texture.graphicsFormat, target.graphicsFormat);
                    Assert.AreEqual(2, target.width); Assert.AreEqual(2, target.height);
                }
                else
                {
                    var error = Assert.Throws<InvalidOperationException>(() => PrepareTarget(target, source.texture));
                    StringAssert.Contains("Expected=" + source.texture.graphicsFormat + " 2x2", error.Message);
                    StringAssert.Contains("actual=" + target.graphicsFormat + " 4x2", error.Message);
                    StringAssert.Contains("IsCreated=", error.Message);
                    StringAssert.Contains("backend=" + SystemInfo.graphicsDeviceType, error.Message);
                }
                Assert.AreSame(previous, RenderTexture.active);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target) { target.Release(); Object.DestroyImmediate(target); }
                DestroySource(source);
            }
        }

        static void AssertPixels(Sprite saved, Color32[][] pixels, bool linear)
        {
            Assert.IsNotNull(saved);
            Assert.IsTrue(EditorUtility.IsPersistent(saved));
            Assert.IsTrue(EditorUtility.IsPersistent(saved.texture));
            Assert.IsTrue(saved.texture.isReadable, "The saved asset must retain serializable CPU pixel data.");
            Assert.AreEqual(!linear, saved.texture.isDataSRGB);
            Assert.AreEqual(TextureFormat.RGBA32, saved.texture.format);
            Assert.AreEqual(pixels.Length, saved.texture.mipmapCount);
            for (int mip = 0; mip < pixels.Length; mip++)
                CollectionAssert.AreEqual(pixels[mip], saved.texture.GetPixels32(mip), "Exact RGBA bytes at mip " + mip);
            Assert.AreEqual(new Vector2(.5f, 1.5f), saved.pivot);
            Assert.AreEqual(new Vector4(.25f, .5f, .25f, .5f), saved.border);
            Assert.AreEqual(37.5f, saved.pixelsPerUnit);
            Assert.AreEqual(FilterMode.Bilinear, saved.texture.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, saved.texture.wrapModeU);
            Assert.AreEqual(.25f, saved.texture.mipMapBias);
        }

        static (string Guid, long FileId) AssetIdentity(Object asset)
        {
            Assert.IsTrue(asset && EditorUtility.IsPersistent(asset), "The reference must be a saved asset.");
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId));
            Assert.IsNotEmpty(guid);
            Assert.AreNotEqual(0L, fileId);
            return (guid, fileId);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void UnreadableRgbaPixelsAndAlphaSurviveSaveReimportUnloadAndReload(bool linear, bool mipmaps)
        {
            Assert.AreNotEqual(GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType, "Run this native pixel test with graphics enabled.");
            Assert.AreNotEqual(0, (int)(SystemInfo.copyTextureSupport & CopyTextureSupport.TextureToRT));
            var pixels = Pixels(mipmaps);
            Sprite source = null;
            RenderTexture sentinel = null;
            var previous = RenderTexture.active;
            bool previousSrgbWrite = GL.sRGBWrite;
            try
            {
                source = Source(pixels, linear, true, new Vector2(.25f, .75f));
                Assert.IsFalse(source.texture.isReadable, "Exercise the same discarded-CPU-data path as LobbyIcons.");
                sentinel = RenderTexture.GetTemporary(2, 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                RenderTexture.active = sentinel;
                GL.sRGBWrite = !previousSrgbWrite;
                var saved = Persist(source);
                Assert.AreSame(sentinel, RenderTexture.active, "Persistence must restore the caller's active target.");
                Assert.AreEqual(!previousSrgbWrite, GL.sRGBWrite, "Raw GPU copy must not change shader color-write state.");
                AssertPixels(saved, pixels, linear);
                string path = AssetDatabase.GetAssetPath(saved);
                var spriteIdentity = AssetIdentity(saved);
                var textureIdentity = AssetIdentity(saved.texture);
                AssetDatabase.SaveAssetIfDirty(saved.texture);
                byte[] serialized = File.ReadAllBytes(path);
                saved = null;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                DestroySource(source); source = null;
                var loadedAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                var currentSprite = loadedAssets.OfType<Sprite>().Single();
                var currentTexture = loadedAssets.OfType<Texture2D>().Single();
                Assert.AreEqual(spriteIdentity, AssetIdentity(currentSprite));
                Assert.AreEqual(textureIdentity, AssetIdentity(currentTexture));
                Assert.AreEqual(textureIdentity, AssetIdentity(currentSprite.texture));
                AssertPixels(currentSprite, pixels, linear);
                for (int mip = 0; mip < pixels.Length; mip++)
                {
                    var sentinelPixels = Enumerable.Repeat(new Color32(1, 2, 3, 4), pixels[mip].Length).ToArray();
                    currentTexture.SetPixels32(sentinelPixels, mip);
                }
                currentTexture.Apply(false, false);
                for (int mip = 0; mip < pixels.Length; mip++)
                    CollectionAssert.AreNotEqual(pixels[mip], currentTexture.GetPixels32(mip), "The unsaved memory sentinel must differ.");
                CollectionAssert.AreEqual(serialized, File.ReadAllBytes(path), "The memory sentinel must not alter the saved file.");
                Resources.UnloadAsset(currentSprite);
                Resources.UnloadAsset(currentTexture);
                currentSprite = null; currentTexture = null; loadedAssets = null;
                var reloaded = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
                Assert.AreEqual(spriteIdentity, AssetIdentity(reloaded));
                Assert.AreEqual(textureIdentity, AssetIdentity(reloaded.texture));
                AssertPixels(reloaded, pixels, linear);
                CollectionAssert.AreEqual(serialized, File.ReadAllBytes(path), "Unloading/reloading must preserve the saved graphic bytes.");
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                if (sentinel) RenderTexture.ReleaseTemporary(sentinel);
                DestroySource(source);
            }
        }

        [Test]
        public void PixelContentPivotAndEncodingHaveDistinctIdentitiesAcrossCultures()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            var sources = new List<Sprite>();
            try
            {
                var pixels = Pixels(false);
                var changed = Pixels(false);
                changed[0][0] = new Color32(13, 57, 139, 31);
                sources.Add(Source(pixels, false, false, new Vector2(.25f, .75f)));
                sources.Add(Source(changed, false, false, new Vector2(.25f, .75f)));
                sources.Add(Source(pixels, false, false, new Vector2(.75f, .25f)));
                sources.Add(Source(pixels, true, false, new Vector2(.25f, .75f)));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                var first = sources.Select(source => AssetDatabase.GetAssetPath(Persist(source))).ToArray();
                Assert.AreEqual(4, first.Distinct().Count(), "Same name/geometry must not alias different pixels, pivot or encoding.");
                var bytes = first.Select(File.ReadAllBytes).ToArray();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var repeated = sources.Select(source => AssetDatabase.GetAssetPath(Persist(source))).ToArray();
                CollectionAssert.AreEqual(first, repeated, "Hash metadata must not depend on decimal formatting culture.");
                for (int i = 0; i < first.Length; i++) CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(first[i]));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                foreach (var source in sources) DestroySource(source);
            }
        }

        [Test]
        public void EditedSavedGraphicIsRejectedWithoutOverwriteAndRestoresActiveTarget()
        {
            Sprite source = null;
            var previous = RenderTexture.active;
            try
            {
                source = Source(Pixels(false), false, false, new Vector2(.25f, .75f));
                var saved = Persist(source);
                string path = AssetDatabase.GetAssetPath(saved);
                saved.texture.SetPixel(0, 0, Color.magenta);
                saved.texture.Apply(false, false);
                EditorUtility.SetDirty(saved.texture);
                AssetDatabase.SaveAssetIfDirty(saved.texture);
                byte[] authored = File.ReadAllBytes(path);
                Assert.Throws<IOException>(() => Persist(source));
                CollectionAssert.AreEqual(authored, File.ReadAllBytes(path));
                Assert.AreSame(previous, RenderTexture.active);
            }
            finally { DestroySource(source); }
        }
    }
}
