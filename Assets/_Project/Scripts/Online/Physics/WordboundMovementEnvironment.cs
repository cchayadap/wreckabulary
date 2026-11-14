using System;

namespace Wreckabulary.Online
{
    public readonly struct WordboundMovementEnvironment
    {
        public float SpeedMultiplier { get; }
        public float AccelerationMultiplier { get; }
        public static WordboundMovementEnvironment Neutral => new WordboundMovementEnvironment(1, 1);

        public WordboundMovementEnvironment(float speedMultiplier, float accelerationMultiplier)
        {
            SpeedMultiplier = speedMultiplier; AccelerationMultiplier = accelerationMultiplier;
            Validate();
        }

        public void Validate()
        {
            if (!Valid(SpeedMultiplier) || !Valid(AccelerationMultiplier))
                throw new ArgumentException("Movement environment scales must be finite and in (0, 1].");
        }

        public WordboundMovementEnvironment Combine(WordboundMovementEnvironment other)
        {
            Validate(); other.Validate();
            return new WordboundMovementEnvironment(Math.Min(SpeedMultiplier, other.SpeedMultiplier),
                Math.Min(AccelerationMultiplier, other.AccelerationMultiplier));
        }

        static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0 && value <= 1;
    }
}
