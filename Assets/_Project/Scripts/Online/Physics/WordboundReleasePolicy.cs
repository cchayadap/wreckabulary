using System;
using System.Collections.Generic;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundReleasePolicy
    {
        public static IReadOnlyList<WorldVector> Candidates(WorldBodyState actor, WorldVector size, float playerRadius, bool throwing)
        {
            if (actor == null || actor.Kind != WorldBodyKind.Player || !actor.Active ||
                !Size(size.X) || !Size(size.Y) || !Size(size.Z) || !Size(playerRadius) || playerRadius > 2)
                throw new ArgumentException("Release geometry needs a live actor and bounded actual collider dimensions.");
            var q = actor.RotationXyz;
            if (1 - 2 * (q.X * q.X + q.Z * q.Z) < .98f)
                throw new ArgumentException("Release geometry requires an upright actor.");
            float fx = 2 * (q.X * q.Z + actor.RotationW * q.Y), fz = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            float length = (float)Math.Sqrt(fx * fx + fz * fz);
            fx /= length; fz /= length;
            var positions = new List<WorldVector>();
            foreach (double angle in throwing ? new[] { 0d } : new[] { 0d, Math.PI / 3, -Math.PI / 3 })
            {
                float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);
                float x = fx * cos + fz * sin, z = fz * cos - fx * sin;
                float extent = Math.Abs(sin) * size.X / 2 + Math.Abs(cos) * size.Z / 2;
                float distance = playerRadius + extent + .15f;
                positions.Add(new WorldVector(actor.Position.X + x * distance,
                    actor.Position.Y + (throwing ? .75f : .08f), actor.Position.Z + z * distance));
            }
            return positions.AsReadOnly();
        }
        static bool Size(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= .01f && value <= 10;
    }
}
