using System;

namespace Wreckabulary.Rules
{
    public static class CarryRules
    {
        public const float FacingPenalty = 1.2f;
        public const float StickyShare = .75f;
        public const float MaxThrowSpeed = 28f;
        public const float ThrowSpin = 3f;
        public const float DropSpin = .6f;
        public const float FlightSeconds = 1.6f;
        public const float SpotStep = .35f;
        public const int SpotRings = 3;
        public const int SpotDirections = 8;
        public const int SpotCount = 1 + SpotRings * SpotDirections;
        public const float FitSkin = .03f;
        public const float SideProbeRadius = .2f;
        public const float EmbedTolerance = .02f;
        public const float SetDownGap = .08f;
        public const float SetDownLift = .04f;

        static readonly float[] Turns = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };

        public static float AimScore(float distance, float angleDegrees, bool sticky)
        {
            float score = Math.Max(0f, distance) + FacingPenalty * Math.Min(180f, Math.Abs(angleDegrees)) / 90f;
            return sticky ? score * StickyShare : score;
        }

        public static float SideShift(float halfWidth, float roomPlus, float roomMinus)
        {
            float overPlus = Math.Max(0f, halfWidth - roomPlus);
            float overMinus = Math.Max(0f, halfWidth - roomMinus);
            if (overPlus > 0f && overMinus > 0f) return (roomPlus - roomMinus) * .5f;
            return overMinus - overPlus;
        }

        public static bool Spot(int index, float facingX, float facingZ, float step, out float x, out float z)
        {
            x = z = 0f;
            if (index < 0 || index >= SpotCount) return false;
            if (index == 0) return true;
            int ring = (index - 1) / SpotDirections + 1;
            float turn = Turns[(index - 1) % SpotDirections] * (float)Math.PI / 180f;
            float length = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            float fx = length > 1e-5f ? facingX / length : 0f;
            float fz = length > 1e-5f ? facingZ / length : 1f;
            float cos = (float)Math.Cos(turn), sin = (float)Math.Sin(turn);
            float distance = ring * Math.Max(0f, step);
            x = (fx * cos + fz * sin) * distance;
            z = (fz * cos - fx * sin) * distance;
            return true;
        }

        public static void ClampSpeed(ref float x, ref float y, ref float z, float max)
        {
            float squared = x * x + y * y + z * z;
            if (max <= 0f || squared <= max * max) return;
            float scale = max / (float)Math.Sqrt(squared);
            x *= scale; y *= scale; z *= scale;
        }
    }
}
