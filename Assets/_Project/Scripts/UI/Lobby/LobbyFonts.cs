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
        static bool failed;

        public static TMP_FontAsset Display { get { Load(); return display; } }
        public static TMP_FontAsset Body { get { Load(); return body; } }
        public static TMP_FontAsset Black { get { Load(); return black; } }
        public static Material Stroke { get { Load(); return stroke; } }
        public static Material Drop { get { Load(); return drop; } }

        static void Load()
        {
            if (display || failed) return;
            display = Make("LilitaOne-Regular", 72, 10);
            body = Make("Nunito-ExtraBold", 56, 7);
            black = Make("Nunito-Black", 56, 7);
            if (!display || !body || !black) { failed = true; display = body = black = null; return; }
            stroke = Preset(display, false);
            drop = Preset(display, true);
        }

        static TMP_FontAsset Make(string file, int size, int padding)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (!font) { Debug.LogWarning("Lobby font missing: Resources/Fonts/" + file); return null; }
            var asset = TMP_FontAsset.CreateFontAsset(font, size, padding, GlyphRenderMode.SDFAA, 1024, 1024);
            if (!asset) return null;
            asset.name = file;
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
