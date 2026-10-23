using System;
using UnityEngine;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Wordbound/Interface theme")]
    public sealed class WordboundUiTheme : ScriptableObject
    {
        [Header("Type")]
        public Font BodyFont;
        public Font HeadingFont;
        [Range(12, 22)] public int BodySize = 15;
        [Range(16, 30)] public int HeadingSize = 20;
        [Header("Colour")]
        [ColorUsage(false)] public Color Panel = Hex(0xfff0d9);
        [ColorUsage(false)] public Color Text = Hex(0x173b3c);
        [ColorUsage(false)] public Color Muted = Hex(0x546763);
        [ColorUsage(false)] public Color Button = Hex(0xfff7e8);
        [ColorUsage(false)] public Color Hover = Hex(0xdeedcf);
        [ColorUsage(false)] public Color Primary = Hex(0x173b3c);
        [ColorUsage(false)] public Color PrimaryText = Hex(0xfff0d9);
        [ColorUsage(false)] public Color Focus = Hex(0xf08a4b);
        [ColorUsage(false)] public Color Disabled = Hex(0xead9b7);
        [ColorUsage(false)] public Color DisabledText = Hex(0x4c5b5c);
        [ColorUsage(false)] public Color Error = Hex(0xb3533b);
        [Header("Spacing")]
        [Range(8, 24)] public int Padding = 16;
        [Range(2, 12)] public int Gap = 6;
        [Range(30, 52)] public int RowHeight = 34;
        [Range(44, 90)] public int ModuleHeight = 56;
        [Range(480, 820)] public int MenuWidth = 620;
        [Range(0, 10)] public int CornerRadius = 5;

        public void Validate()
        {
            CheckRange(BodySize, 12, 22, nameof(BodySize)); CheckRange(HeadingSize, 16, 30, nameof(HeadingSize));
            CheckRange(Padding, 8, 24, nameof(Padding)); CheckRange(Gap, 2, 12, nameof(Gap));
            CheckRange(RowHeight, 30, 52, nameof(RowHeight)); CheckRange(ModuleHeight, 44, 90, nameof(ModuleHeight));
            CheckRange(MenuWidth, 480, 820, nameof(MenuWidth)); CheckRange(CornerRadius, 0, 10, nameof(CornerRadius));
            if (RowHeight < BodySize + 12 || ModuleHeight < BodySize * 2 + 16)
                throw new ArgumentException("Control heights must leave room for the selected type size.");
            Check(Panel, nameof(Panel)); Check(Text, nameof(Text)); Check(Muted, nameof(Muted));
            Check(Button, nameof(Button)); Check(Hover, nameof(Hover)); Check(Primary, nameof(Primary));
            Check(PrimaryText, nameof(PrimaryText)); Check(Focus, nameof(Focus));
            Check(Disabled, nameof(Disabled)); Check(DisabledText, nameof(DisabledText)); Check(Error, nameof(Error));
        }

        internal int Fingerprint()
        {
            unchecked
            {
                int value = BodyFont ? BodyFont.GetHashCode() : 0;
                value = value * 31 + (HeadingFont ? HeadingFont.GetHashCode() : 0);
                value = value * 31 + BodySize; value = value * 31 + HeadingSize;
                value = value * 31 + Padding; value = value * 31 + Gap; value = value * 31 + RowHeight;
                value = value * 31 + ModuleHeight; value = value * 31 + MenuWidth; value = value * 31 + CornerRadius;
                value = value * 31 + Panel.GetHashCode(); value = value * 31 + Text.GetHashCode();
                value = value * 31 + Muted.GetHashCode(); value = value * 31 + Button.GetHashCode();
                value = value * 31 + Hover.GetHashCode(); value = value * 31 + Primary.GetHashCode();
                value = value * 31 + PrimaryText.GetHashCode(); value = value * 31 + Focus.GetHashCode();
                value = value * 31 + Disabled.GetHashCode(); value = value * 31 + DisabledText.GetHashCode();
                return value * 31 + Error.GetHashCode();
            }
        }
        static Color Hex(int rgb) => new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1);
        static void CheckRange(int value, int min, int max, string name)
        { if (value < min || value > max) throw new ArgumentException(name + " is outside its supported range."); }
        static void Check(Color colour, string name)
        {
            if (!Good(colour.r) || !Good(colour.g) || !Good(colour.b) || colour.a != 1)
                throw new ArgumentException(name + " must be an opaque, finite colour between 0 and 1.");
        }
        static bool Good(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
    }
}
