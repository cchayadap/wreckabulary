using System;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundFieldPolicy
    {
        public const float HeightReach = 1.25f;

        public static bool ActiveNextTick(ItemDefinition definition, long deploymentTick, long completedTick)
        {
            if (definition?.Deploy == null || deploymentTick < 1 || deploymentTick > completedTick || completedTick == long.MaxValue)
                return false;
            Validate(definition.Deploy);
            long expiry = MatchStateValidation.DeploymentExpiryTick(definition, deploymentTick);
            return expiry < 0 || completedTick + 1 < expiry;
        }

        public static bool Contains(DeployStats stats, WorldBodyState source, WorldBodyState target)
        {
            Validate(stats);
            if (source == null || target == null || !source.Active || !source.Kinematic ||
                !target.Active || target.Kinematic || source.Id == target.Id) return false;
            float x = target.Position.X - source.Position.X, z = target.Position.Z - source.Position.Z;
            if (Math.Abs(target.Position.Y - source.Position.Y) > HeightReach ||
                x * x + z * z > stats.Radius * stats.Radius) return false;
            var forward = Forward(source);
            return x * x + z * z < .000001f || stats.ArcDegrees >= 360 ||
                Geometry.InFrontArc(forward.X, forward.Z, x, z, stats.ArcDegrees);
        }

        public static WordboundMovementEnvironment Environment(DeployStats stats)
        {
            Validate(stats);
            return stats.Effect == DeployEffect.SlowField ? new WordboundMovementEnvironment(stats.Strength, 1) :
                new WordboundMovementEnvironment(1, .1f);
        }

        public static WorldVector WindVelocity(DeployStats stats, WorldBodyState source, WorldVector velocity)
        {
            Validate(stats);
            if (stats.Effect != DeployEffect.WindField || source == null) throw new ArgumentException("Wind requires its deployed source.");
            var forward = Forward(source);
            float along = velocity.X * forward.X + velocity.Z * forward.Z;
            float change = Math.Max(0, Math.Min(stats.Strength / ModeDefinition.TickRate, stats.Strength - along));
            return new WorldVector(Math.Max(-1000, Math.Min(1000, velocity.X + forward.X * change)),
                velocity.Y, Math.Max(-1000, Math.Min(1000, velocity.Z + forward.Z * change)));
        }

        public static void Validate(DeployStats stats)
        {
            if (stats == null || stats.Effect != DeployEffect.WindField && stats.Effect != DeployEffect.SlowField ||
                !Positive(stats.Radius, 40) || !Positive(stats.Strength, stats.Effect == DeployEffect.SlowField ? 1 : 100) ||
                !Positive(stats.ArcDegrees, 360))
                throw new ArgumentException("Unsupported or out-of-bounds deployed field.");
        }

        static WorldVector Forward(WorldBodyState source)
        {
            var q = source.RotationXyz;
            float x = 2 * (q.X * q.Z + source.RotationW * q.Y), z = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length < .0001f) throw new ArgumentException("A deployed field needs a horizontal facing.");
            return new WorldVector(x / length, 0, z / length);
        }
        static bool Positive(float value, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0 && value <= max;
    }
}
