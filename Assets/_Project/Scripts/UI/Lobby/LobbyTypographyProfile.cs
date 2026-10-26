using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Lobby/Typography", fileName = "LobbyTypography")]
    public sealed class LobbyTypographyProfile : ScriptableObject
    {
        public const string ResourcePath = "UI/LobbyTypography";
        public const string InitialCharacters = "_… ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,:;!?'-–—()/+%&=·−";

        [Header("Fonts")]
        public TMP_FontAsset Display;
        public TMP_FontAsset Body;
        public TMP_FontAsset Black;
        [Header("Display presets")]
        public Material Stroke;
        public Material Drop;
        [Header("Artwork foreground presets")]
        public Material DisplayForeground;
        public Material BodyForeground;
        public Material BlackForeground;

        public bool TryValidate(out string reason)
        {
            if (!FontValid(Display, nameof(Display), out reason) || !FontValid(Body, nameof(Body), out reason) ||
                !FontValid(Black, nameof(Black), out reason)) return false;
            if (Display.fontWeightTable[7].regularTypeface != Display || Body.fontWeightTable[7].regularTypeface != Black ||
                Black.fontWeightTable[7].regularTypeface != Black)
            { reason = "Set each font's Bold typeface: Display to Display, Body to Black, and Black to Black."; return false; }
            if (!MaterialValid(Display, Stroke, nameof(Stroke), out reason) || !MaterialValid(Display, Drop, nameof(Drop), out reason) ||
                !MaterialValid(Display, DisplayForeground, nameof(DisplayForeground), out reason) ||
                !MaterialValid(Body, BodyForeground, nameof(BodyForeground), out reason) ||
                !MaterialValid(Black, BlackForeground, nameof(BlackForeground), out reason)) return false;
            if (Display == Body && DisplayForeground != BodyForeground || Display == Black && DisplayForeground != BlackForeground ||
                Body == Black && BodyForeground != BlackForeground)
            { reason = "A font shared between roles must use the same artwork foreground material."; return false; }
            reason = null;
            return true;
        }

        static bool FontValid(TMP_FontAsset font, string role, out string reason)
        {
            if (!font || font.atlasTextures == null || font.atlasTextures.Length == 0 || !font.atlasTextures[0] ||
                font.fontWeightTable == null || font.fontWeightTable.Length <= 7 || !font.material ||
                font.atlasPopulationMode == AtlasPopulationMode.Dynamic && !font.sourceFontFile)
            { reason = role + " requires a font, atlas, base material, weight table and source font for dynamic glyphs."; return false; }
            return MaterialValid(font, font.material, role + " base material", out reason);
        }

        static bool MaterialValid(TMP_FontAsset font, Material material, string role, out string reason)
        {
            var original = font.material;
            if (!material || !material.shader || material.shader != original.shader ||
                !material.HasProperty(ShaderUtilities.ID_MainTex) || material.mainTexture != font.atlasTextures[0] ||
                !Matches(material, original, ShaderUtilities.ID_GradientScale) ||
                !Matches(material, original, ShaderUtilities.ID_TextureWidth) ||
                !Matches(material, original, ShaderUtilities.ID_TextureHeight) ||
                !Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_TextureWidth), font.atlasWidth) ||
                !Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_TextureHeight), font.atlasHeight))
            { reason = role + " must use its font's SDF shader, atlas texture, gradient scale and atlas dimensions."; return false; }
            reason = null;
            return true;
        }

        static bool Matches(Material material, Material original, int property) =>
            material.HasProperty(property) && original.HasProperty(property) && original.GetFloat(property) > 0 &&
            !float.IsInfinity(original.GetFloat(property)) && Mathf.Approximately(material.GetFloat(property), original.GetFloat(property));
    }
}
