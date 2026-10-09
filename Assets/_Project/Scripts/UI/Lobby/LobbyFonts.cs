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
        static bool failed;

        public static TMP_FontAsset Display { get { Load(); return display; } }
        public static TMP_FontAsset Body { get { Load(); return body; } }
        public static TMP_FontAsset Black { get { Load(); return black; } }
        public static Material Stroke { get { Load(); return stroke; } }
        public static Material Drop { get { Load(); return drop; } }

        public static Material Foreground(TMP_FontAsset font)
        {
            if (!font) return null;
            if (foreground.TryGetValue(font, out var cached) && cached) return cached;
            var material = new Material(font.material) { name = font.name + " Artwork foreground" };
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", new Color(1f, .976f, .914f, .96f));
            material.SetFloat("_OutlineWidth", .18f);
            material.SetFloat("_FaceDilate", .02f);
            ShaderUtilities.UpdateShaderRatios(material);
            foreground[font] = material;
            return material;
        }

        static void Load()
        {
            if (display || failed) return;
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

        static TMP_FontAsset Make(string file, int size, int padding)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (!font) { Debug.LogWarning("Lobby font missing: Resources/Fonts/" + file); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, size, padding, GlyphRenderMode.SDFAA, 2048, 2048);
            if (!asset) return null;
            asset.name = file;
            asset.isMultiAtlasTexturesEnabled = true;
            // TMP resolves ellipsis and underline before laying out the first visible text.
            asset.TryAddCharacters("_… ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?'-–—()/+%&=·−");
            foreach (var atlas in asset.atlasTextures) if (atlas) atlas.filterMode = FilterMode.Bilinear;
            if (GameAssets.I && GameAssets.I.font) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { GameAssets.I.font };
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
            return material;
        }
    }
}
