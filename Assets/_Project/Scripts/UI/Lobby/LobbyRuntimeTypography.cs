using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Wreckabulary
{
    public static class LobbyRuntimeTypography
    {
        const HideFlags RuntimeFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        static OwnedCache cache;
        static OwnedCache Cache
        {
            get
            {
#if UNITY_EDITOR
                RestoreAfterReload();
#endif
                return cache ??= new OwnedCache();
            }
        }

        public static TMP_FontAsset ResolveFont(TMP_FontAsset font) => Application.isPlaying ? Cache.Font(font) : font;
        public static Material ResolveMaterial(TMP_FontAsset font, Material material) =>
            Application.isPlaying ? Cache.Material(font, material) : material;

        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays = false) where T : Component
        {
            if (!Application.isPlaying) return Object.Instantiate(original, parent, worldPositionStays);
            var staging = new GameObject("Typography staging", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(parent, false);
            T copy = null;
            try
            {
                copy = Object.Instantiate(original, staging.transform, worldPositionStays);
                foreach (var label in copy.GetComponentsInChildren<TMP_Text>(true))
                {
                    var font = label.font ? label.font : TMP_Settings.defaultFontAsset;
                    if (!font) throw new InvalidOperationException("A saved lobby text label requires a font: " + label.name);
                    var material = label.fontSharedMaterial;
                    var ownedFont = ResolveFont(font);
                    var ownedMaterial = ResolveMaterial(font, material ? material : font.material);
                    label.font = ownedFont;
                    label.fontSharedMaterial = ownedMaterial;
                }
                copy.transform.SetParent(parent, worldPositionStays);
                return copy;
            }
            catch
            {
                if (copy) Release(copy.gameObject);
                throw;
            }
            finally { Release(staging); }
        }

        internal static void OwnGenerated(TMP_FontAsset font)
        {
            if (Application.isPlaying) Cache.Adopt(font);
        }

        internal static void OwnGenerated(Material material)
        {
            if (Application.isPlaying) Cache.Adopt(material);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeginSession() => CollectUnused();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void WatchEditorLifetime()
        {
            RestoreAfterReload();
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }

        static void BeforeReload()
        {
            CollectUnused();
            if (cache == null) return;
            cache.DetachForReload().Session = true;
            cache = null;
        }

        static void RestoreAfterReload()
        {
            if (cache != null) return;
            foreach (var state in Resources.FindObjectsOfTypeAll<LobbyTypographyReloadState>())
                if (state.Session)
                {
                    cache = OwnedCache.Restore(state);
                    return;
                }
        }

        static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                UnityEditor.EditorApplication.delayCall -= CollectUnused;
                UnityEditor.EditorApplication.delayCall += CollectUnused;
            }
        }
#endif

        static void CollectUnused()
        {
            if (cache == null || !cache.TryDisposeUnused()) return;
            cache = null;
        }

        static void Release(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        public sealed class OwnedCache : IDisposable
        {
            readonly Dictionary<TMP_FontAsset, TMP_FontAsset> fonts = new();
            readonly Dictionary<TMP_FontAsset, TMP_FontAsset> sources = new();
            readonly Dictionary<(TMP_FontAsset, Material), Material> presets = new();
            readonly HashSet<Material> materials = new();
            readonly HashSet<TMP_FontAsset> extendedFallbacks = new();
            readonly List<TMP_FontAsset> pending = new();
            bool disposed;

            public int FontCount => sources.Count;
            public int MaterialCount => materials.Count;

            public TMP_FontAsset Font(TMP_FontAsset source)
            {
                if (disposed) throw new ObjectDisposedException(nameof(OwnedCache));
                if (!source) return null;
                if (sources.ContainsKey(source)) return source;
                pending.Clear();
                try
                {
                    var result = CloneGraph(source);
                    if (!extendedFallbacks.Contains(result))
                    {
                        var fallbacks = new List<TMP_FontAsset>(result.fallbackFontAssetTable);
                        if (TMP_Settings.fallbackFontAssets != null)
                            foreach (var fallback in TMP_Settings.fallbackFontAssets)
                            {
                                if (!fallback) break;
                                AddFallback(fallbacks, CloneGraph(fallback), result);
                            }
                        if (TMP_Settings.defaultFontAsset)
                            AddFallback(fallbacks, CloneGraph(TMP_Settings.defaultFontAsset), result);
                        result.fallbackFontAssetTable = fallbacks;
                        extendedFallbacks.Add(result);
                    }
                    return result;
                }
                catch
                {
                    foreach (var created in pending)
                    {
                        if (sources.TryGetValue(created, out var original)) fonts.Remove(original);
                        sources.Remove(created);
                        extendedFallbacks.Remove(created);
                        if (created && created.material) materials.Remove(created.material);
                        Release(created);
                    }
                    throw;
                }
                finally { pending.Clear(); }
            }

            static void AddFallback(List<TMP_FontAsset> list, TMP_FontAsset font, TMP_FontAsset primary)
            {
                if (font && font != primary && !list.Contains(font)) list.Add(font);
            }

            TMP_FontAsset CloneGraph(TMP_FontAsset source)
            {
                if (!source) return null;
                if (sources.ContainsKey(source)) return source;
                if (fonts.TryGetValue(source, out var cached) && cached) return cached;
                if (string.IsNullOrEmpty(source.version) || !source.material || source.atlasTextures == null || source.atlasTextures.Length == 0)
                    throw new InvalidOperationException("Lobby typography requires a current font asset with a material and atlas: " + source.name);
                var copy = Object.Instantiate(source);
                copy.atlasTextures = Array.Empty<Texture2D>();
                copy.material = null;
                copy.name = source.name + " [Lobby runtime]";
                copy.hideFlags = RuntimeFlags;
                fonts[source] = copy;
                sources[copy] = source;
                pending.Add(copy);
                var atlases = new Texture2D[source.atlasTextures.Length];
                copy.atlasTextures = atlases;
                for (int i = 0; i < atlases.Length; i++)
                {
                    var atlas = source.atlasTextures[i];
                    if (!atlas) continue;
                    if (source.atlasPopulationMode != AtlasPopulationMode.Static && !atlas.isReadable)
                        throw new InvalidOperationException("A dynamic lobby font requires a readable authored atlas: " + source.name);
                    atlases[i] = atlas.isReadable ? Object.Instantiate(atlas) : CopyStaticAtlas(atlas);
                    atlases[i].hideFlags = RuntimeFlags;
                }
                if (!atlases[0] || copy.atlasTexture != atlases[0] || copy.atlasTexture == source.atlasTextures[0])
                    throw new InvalidOperationException("Lobby font clone retained an authored atlas reference: " + source.name);
                copy.material = CopyMaterial(source, copy, source.material);
                if (ReferenceEquals(copy.fontWeightTable, source.fontWeightTable) || ReferenceEquals(copy.characterTable, source.characterTable) ||
                    ReferenceEquals(copy.glyphTable, source.glyphTable) || ReferenceEquals(copy.fontFeatureTable, source.fontFeatureTable))
                    throw new InvalidOperationException("Lobby font clone retained authored data tables: " + source.name);
                for (int i = 0; i < copy.fontWeightTable.Length; i++)
                {
                    copy.fontWeightTable[i].regularTypeface = CloneGraph(source.fontWeightTable[i].regularTypeface);
                    copy.fontWeightTable[i].italicTypeface = CloneGraph(source.fontWeightTable[i].italicTypeface);
                }
                var fallbacks = new List<TMP_FontAsset>();
                if (source.fallbackFontAssetTable != null)
                    foreach (var fallback in source.fallbackFontAssetTable)
                    {
                        if (!fallback) break;
                        fallbacks.Add(CloneGraph(fallback));
                    }
                copy.fallbackFontAssetTable = fallbacks;
                copy.ReadFontAssetDefinition();
                return copy;
            }

            static Texture2D CopyStaticAtlas(Texture2D source)
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                    (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0 ||
                    !SystemInfo.SupportsTextureFormat(source.format) || source.activeMipmapLimit != 0)
                    throw new InvalidOperationException($"Cannot copy the static lobby atlas {source.name} without changing its data. " +
                        $"Format={source.format}/{source.graphicsFormat}, mipLimit={source.activeMipmapLimit}, backend={SystemInfo.graphicsDeviceType}.");
                Texture2D copy = null;
                try
                {
                    copy = new Texture2D(source.width, source.height, source.format, source.mipmapCount, !source.isDataSRGB)
                    {
                        name = source.name + " [Lobby runtime]", hideFlags = RuntimeFlags,
                        filterMode = source.filterMode, wrapModeU = source.wrapModeU, wrapModeV = source.wrapModeV,
                        wrapModeW = source.wrapModeW, anisoLevel = source.anisoLevel, mipMapBias = source.mipMapBias,
                        ignoreMipmapLimit = true
                    };
                    copy.Apply(false, true);
                    if (copy.format != source.format || copy.graphicsFormat != source.graphicsFormat ||
                        copy.width != source.width || copy.height != source.height || copy.mipmapCount != source.mipmapCount ||
                        copy.isDataSRGB != source.isDataSRGB || copy.activeMipmapLimit != 0)
                        throw new InvalidOperationException($"Static lobby atlas copy changed its format or mip layout: {source.name}. " +
                            $"Expected={source.format}/{source.graphicsFormat}, actual={copy.format}/{copy.graphicsFormat}, backend={SystemInfo.graphicsDeviceType}.");
                    for (int mip = 0; mip < source.mipmapCount; mip++) Graphics.CopyTexture(source, 0, mip, copy, 0, mip);
                    return copy;
                }
                catch
                {
                    Release(copy);
                    throw;
                }
            }

            public Material Material(TMP_FontAsset font, Material source)
            {
                if (disposed) throw new ObjectDisposedException(nameof(OwnedCache));
                if (!source) return null;
                if (materials.Contains(source)) return source;
                var copy = Font(font);
                if (!copy) throw new ArgumentException("A lobby text material requires its authored font.", nameof(font));
                var original = sources[copy];
                if (source == original.material) return copy.material;
                var key = (original, source);
                if (presets.TryGetValue(key, out var cached) && cached) return cached;
                var result = CopyMaterial(original, copy, source);
                presets[key] = result;
                return result;
            }

            Material CopyMaterial(TMP_FontAsset original, TMP_FontAsset copy, Material source)
            {
                int index = Array.IndexOf(original.atlasTextures, source.mainTexture);
                if (index < 0 || !copy.atlasTextures[index])
                    throw new InvalidOperationException("Lobby text material must reference an atlas belonging to its font: " + source.name);
                var result = new Material(source) { name = source.name + " [Lobby runtime]", hideFlags = RuntimeFlags };
                result.mainTexture = copy.atlasTextures[index];
                materials.Add(result);
                return result;
            }

            internal void Adopt(TMP_FontAsset font)
            {
                font.hideFlags = RuntimeFlags;
                foreach (var atlas in font.atlasTextures) if (atlas) atlas.hideFlags = RuntimeFlags;
                sources[font] = font;
                fonts[font] = font;
                Adopt(font.material);
            }

            internal void Adopt(Material material)
            {
                material.hideFlags = RuntimeFlags;
                materials.Add(material);
            }

            public bool TryDisposeUnused()
            {
                foreach (var label in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (label && (label.font && sources.ContainsKey(label.font) || label.fontSharedMaterial && materials.Contains(label.fontSharedMaterial)))
                        return false;
                Dispose();
                return true;
            }

#if UNITY_EDITOR
            public LobbyTypographyReloadState DetachForReload()
            {
                if (disposed) throw new ObjectDisposedException(nameof(OwnedCache));
                var state = ScriptableObject.CreateInstance<LobbyTypographyReloadState>();
                state.hideFlags = HideFlags.HideAndDontSave;
                foreach (var pair in sources)
                    state.Fonts.Add(new LobbyTypographyReloadState.FontPair { Source = pair.Value, Owned = pair.Key,
                        Extended = extendedFallbacks.Contains(pair.Key) });
                foreach (var pair in presets)
                    state.Presets.Add(new LobbyTypographyReloadState.MaterialPair
                        { Font = pair.Key.Item1, Source = pair.Key.Item2, Owned = pair.Value });
                state.Materials.AddRange(materials);
                disposed = true;
                Clear();
                return state;
            }

            public static OwnedCache Restore(LobbyTypographyReloadState state)
            {
                if (!state) throw new ArgumentNullException(nameof(state));
                var result = new OwnedCache();
                foreach (var pair in state.Fonts)
                    if (pair.Source && pair.Owned)
                    {
                        result.fonts[pair.Source] = pair.Owned;
                        result.sources[pair.Owned] = pair.Source;
                        if (pair.Extended) result.extendedFallbacks.Add(pair.Owned);
                    }
                foreach (var pair in state.Presets)
                    if (pair.Font && pair.Source && pair.Owned) result.presets[(pair.Font, pair.Source)] = pair.Owned;
                foreach (var material in state.Materials) if (material) result.materials.Add(material);
                Object.DestroyImmediate(state);
                return result;
            }
#endif

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                foreach (var font in sources.Keys)
                {
                    if (font && font.material) materials.Remove(font.material);
                    Release(font);
                }
                foreach (var material in materials) Release(material);
                Clear();
            }

            void Clear()
            {
                fonts.Clear(); sources.Clear(); presets.Clear(); materials.Clear(); extendedFallbacks.Clear(); pending.Clear();
            }
        }
    }
}
