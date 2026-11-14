// Game Studio Kit (MIT). Engine-free locomotion maths, written clean-room from the kit's behaviour specification
// (ARCHITECTURE §3.8 locomotion.md): body-scale playback, stride matching, and a stable damped spring.
using System;

namespace GameStudioKit.Locomotion
{
    public static class LocoMath
    {
        public const float ReferenceHeightM = 1.8f;

        /// <summary>
        /// Baseline animation playback scale for a body of relative size <paramref name="humanScale"/> (1 = the 1.8 m
        /// reference): min(<paramref name="maxScale"/>, 1 / max(<paramref name="minHumanScale"/>, humanScale)).
        /// Smaller bodies play locomotion faster so short legs cover the same ground; the cap stops tiny bodies from
        /// looking frantic. Defaults: max 1.25, floor 0.3.
        /// </summary>
        public static float BaselineScale(float humanScale, float maxScale = 1.25f, float minHumanScale = 0.3f)
        {
            if (float.IsNaN(humanScale) || humanScale <= 0) humanScale = 1f;
            return Math.Min(maxScale, 1f / Math.Max(minHumanScale, humanScale));
        }

        /// <summary>
        /// Stride-matched playback: the rate at which the clip's feet cover ground equal to the body's ground speed.
        /// At playback 1 the clip travels <paramref name="strideAtReferenceM"/> × k per <paramref name="cycleSeconds"/>,
        /// where k = body height / 1.8 m. The result is clamped to [min, max].
        /// </summary>
        public static float StridePlayback(float groundSpeed, float strideAtReferenceM, float cycleSeconds, float k,
            float minPlayback = 0.5f, float maxPlayback = 2f)
        {
            if (strideAtReferenceM <= 0 || cycleSeconds <= 0 || k <= 0) return 1f;
            float clipSpeed = strideAtReferenceM * k / cycleSeconds;
            float playback = Math.Abs(groundSpeed) / clipSpeed;
            return Math.Max(minPlayback, Math.Min(maxPlayback, playback));
        }

        /// <summary>Body scale k = height / reference height (1 for invalid input).</summary>
        public static float BodyScale(float bodyHeightM, float referenceHeightM = ReferenceHeightM) =>
            bodyHeightM > 0 && referenceHeightM > 0 ? bodyHeightM / referenceHeightM : 1f;

        public static float SmoothStep01(float t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// One implicit-Euler step of a damped spring toward <paramref name="target"/>: frequency in Hz, damping ratio
        /// (1 = critical). Unconditionally stable for any dt, so frame hitches never make it explode.
        /// </summary>
        public static void SpringStep(ref float value, ref float velocity, float target, float frequencyHz, float dampingRatio, float dt)
        {
            if (dt <= 0) return;
            float omega = 2f * (float)Math.PI * Math.Max(0.001f, frequencyHz);
            float zeta = Math.Max(0f, dampingRatio);
            float omega2dt = omega * omega * dt;
            float det = 1f + 2f * zeta * omega * dt + omega2dt * dt;
            float x = value;
            float v = velocity;
            value = ((1f + 2f * zeta * omega * dt) * x + dt * v + omega2dt * dt * target) / det;
            velocity = (v + omega2dt * (target - x)) / det;
        }
    }
}
