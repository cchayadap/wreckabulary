using System;

namespace Wreckabulary.Rules
{
    public static class SlotWear
    {
        public const float LowFraction = .3f;
        public const float SlotInset = 12f, SlotBottom = 5f, SlotTall = 6f;
        public const float SlotIconBottom = 12f, SlotWordBottom = 22f;

        public static bool Shows(bool hasGear, bool carrying, int durability) => hasGear && !carrying && durability > 0;

        public static float Fraction(float left, int durability) => durability <= 0 ? 0f : Math.Max(0f, Math.Min(1f, left / durability));

        public static bool Low(float fraction) => fraction <= LowFraction;

        public static float SlotWide(float slotWide) => Math.Max(0f, slotWide - 2f * SlotInset);
    }
}
