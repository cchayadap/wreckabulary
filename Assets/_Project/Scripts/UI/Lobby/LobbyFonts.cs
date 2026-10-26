using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Wreckabulary
{
    public static class LobbyFonts
    {
        static TMP_FontAsset display, body, black;
        static Material stroke, drop;
        static readonly Dictionary<TMP_FontAsset, Material> foreground = new();
        static readonly List<TMP_FontAsset> generatedFonts = new();
        static readonly List<Material> generatedMaterials = new();
        static bool failed, cacheForPlay;

        public static TMP_FontAsset Display { get { Load(); return display; } }
        public static TMP_FontAsset Body { get { Load(); return body; } }
        public static TMP_FontAsset Black { get { Load(); return black; } }
        public static Material Stroke { get { Load(); return stroke; } }
        public static Material Drop { get { Load(); return drop; } }

        public static Material Foreground(TMP_FontAsset font)
        {
            if (!font) return null;
            Load();
            if (foreground.TryGetValue(font, out var cached) && cached) return cached;
            var material = new Material(font.material) { name = font.name + " Artwork foreground" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", new Color(1f, .976f, .914f, .96f));
            material.SetFloat("_OutlineWidth", .18f);
            material.SetFloat("_FaceDilate", .02f);
            ShaderUtilities.UpdateShaderRatios(material);
            foreground[font] = material;
            Own(material);
            return material;
        }

        static void Load()
        {
            if (cacheForPlay != Application.isPlaying)
            {
                ResetCache();
                cacheForPlay = Application.isPlaying;
            }
            if (display || failed) return;
            var profile = Resources.Load<LobbyTypographyProfile>(LobbyTypographyProfile.ResourcePath);
            if (profile)
            {
                if (TryUseProfile(profile)) return;
            }
            display = Make("LilitaOne-Regular", 96, 12);
            body = Make("Nunito-ExtraBold", 80, 10);
            black = Make("Nunito-Black", 80, 10);
            if (!display || !body || !black) { failed = true; display = body = black = null; return; }
            // TMP's styled-glyph lookup requires an explicit face for bold text, including ellipsis.
            display.fontWeightTable[7].regularTypeface = display;
            body.fontWeightTable[7].regularTypeface = black;
            black.fontWeightTable[7].regularTypeface = black;
            stroke = Preset(display, false);
            drop = Preset(display, true);
        }

        static bool TryUseProfile(LobbyTypographyProfile profile)
        {
            if (!profile.TryValidate(out string reason))
            {
                Debug.LogWarning("Lobby typography profile is invalid; using generated defaults. " + reason, profile);
                return false;
            }
            display = LobbyRuntimeTypography.ResolveFont(profile.Display);
            body = LobbyRuntimeTypography.ResolveFont(profile.Body);
            black = LobbyRuntimeTypography.ResolveFont(profile.Black);
            stroke = LobbyRuntimeTypography.ResolveMaterial(profile.Display, profile.Stroke);
            drop = LobbyRuntimeTypography.ResolveMaterial(profile.Display, profile.Drop);
            foreground[display] = LobbyRuntimeTypography.ResolveMaterial(profile.Display, profile.DisplayForeground);
            foreground[body] = LobbyRuntimeTypography.ResolveMaterial(profile.Body, profile.BodyForeground);
            foreground[black] = LobbyRuntimeTypography.ResolveMaterial(profile.Black, profile.BlackForeground);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            display = body = black = null; stroke = drop = null; failed = false;
            foreground.Clear();
            foreach (var material in generatedMaterials) Release(material);
            foreach (var font in generatedFonts) Release(font);
            generatedMaterials.Clear(); generatedFonts.Clear();
        }

        static void Release(Object value)
        {
            if (!value) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(value)) return;
#endif
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        static TMP_FontAsset Make(string file, int size, int padding)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (!font) { Debug.LogWarning("Lobby font missing: Resources/Fonts/" + file); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, size, padding, GlyphRenderMode.SDFAA, 2048, 2048);
            if (!asset) return null;
            if (Application.isPlaying) LobbyRuntimeTypography.OwnGenerated(asset);
            else generatedFonts.Add(asset);
            asset.name = file;
            asset.isMultiAtlasTexturesEnabled = true;
            // TMP resolves ellipsis and underline before laying out the first visible text.
            asset.TryAddCharacters(LobbyTypographyProfile.InitialCharacters);
            foreach (var atlas in asset.atlasTextures) if (atlas) atlas.filterMode = FilterMode.Bilinear;
            if (GameAssets.I && GameAssets.I.font)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { LobbyRuntimeTypography.ResolveFont(GameAssets.I.font) };
            return asset;
        }

        static Material Preset(TMP_FontAsset font, bool withDrop)
        {
            var material = new Material(font.material) { name = font.name + (withDrop ? " Drop" : " Stroke") };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", LobbyKit.Navy);
            material.SetFloat("_OutlineWidth", .28f);
            material.SetFloat("_FaceDilate", .28f);
            if (withDrop)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor("_UnderlayColor", LobbyKit.Navy);
                material.SetFloat("_UnderlayOffsetX", 0f);
                material.SetFloat("_UnderlayOffsetY", -.55f);
                material.SetFloat("_UnderlayDilate", .28f);
                material.SetFloat("_UnderlaySoftness", 0f);
            }
            ShaderUtilities.UpdateShaderRatios(material);
            Own(material);
            return material;
        }

        static void Own(Material material)
        {
            if (Application.isPlaying) LobbyRuntimeTypography.OwnGenerated(material);
            else generatedMaterials.Add(material);
        }
    }
}
