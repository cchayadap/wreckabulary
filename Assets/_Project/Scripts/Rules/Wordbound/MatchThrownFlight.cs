using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchThrownFlight
    {
        public string ItemBodyId { get; }
        public int LaunchRevision { get; }
        public long StartedTick { get; }
        public string ThrowerId { get; }
        public IReadOnlyList<string> HitBodyIds { get; }

        public MatchThrownFlight(string itemBodyId, int launchRevision, long startedTick, string throwerId,
            IEnumerable<string> hitBodyIds = null)
        {
            var hits = (hitBodyIds ?? Array.Empty<string>()).Take(8193).ToArray();
            if (!WordboundIdentity.IsId(itemBodyId) || !itemBodyId.StartsWith("item:", StringComparison.Ordinal) ||
                launchRevision < 1 || startedTick < 1 || startedTick > long.MaxValue - WordboundThrownPolicy.LifetimeTicks ||
                !WordboundIdentity.IsId(throwerId) || hits.Length > 8192 ||
                hits.Any(id => !WordboundIdentity.IsId(id)) || hits.Distinct(StringComparer.Ordinal).Count() != hits.Length)
                throw new ArgumentException("Invalid thrown flight identity, timing or hit ledger.");
            ItemBodyId = itemBodyId; LaunchRevision = launchRevision; StartedTick = startedTick; ThrowerId = throwerId;
            HitBodyIds = Array.AsReadOnly(hits.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class HostThrownContact
    {
        public string ItemBodyId { get; }
        public int LaunchRevision { get; }
        public string TargetBodyId { get; }
        public WorldVector ContactPoint { get; }
        public float RelativeSpeed { get; }
        public long Tick { get; }
        public HostThrownContact(string itemBodyId, int launchRevision, string targetBodyId,
            WorldVector contactPoint, float relativeSpeed, long tick)
        {
            if (!WordboundIdentity.IsId(itemBodyId) || !WordboundIdentity.IsId(targetBodyId) || itemBodyId == targetBodyId ||
                launchRevision < 1 || tick < 1 || !WorldVector.Finite(relativeSpeed) || relativeSpeed < 0 || relativeSpeed > 2000 ||
                !WorldVector.Finite(contactPoint.X) || !WorldVector.Finite(contactPoint.Y) || !WorldVector.Finite(contactPoint.Z) ||
                Math.Abs(contactPoint.X) > 10000 || Math.Abs(contactPoint.Y) > 10000 || Math.Abs(contactPoint.Z) > 10000)
                throw new ArgumentException("Invalid host thrown contact.");
            ItemBodyId = itemBodyId; LaunchRevision = launchRevision; TargetBodyId = targetBodyId;
            ContactPoint = contactPoint; RelativeSpeed = relativeSpeed; Tick = tick;
        }
    }

    public static class WordboundThrownPolicy
    {
        public const int LifetimeTicks = 120;
        public static bool Supported(ItemDefinition definition) => definition != null && definition.Id == "BALL" &&
            definition.Enabled && definition.Thrown != null && definition.Thrown.Recoverable && definition.Thrown.FuseSeconds == 0;
        public static ThrownStats Stats(ItemDefinition definition)
        {
            if (!Supported(definition)) throw new ArgumentException("Only recoverable catalogue BALL contacts are supported.");
            var stats = definition.Thrown;
            foreach (float value in new[] { stats.Speed, stats.Damage, stats.Knockback, stats.BreakPower })
                if (!WorldVector.Finite(value) || value < 0 || value > 1000) throw new ArgumentException("Thrown stats exceed their domain.");
            if (stats.Speed <= 0 || stats.Damage <= 0) throw new ArgumentException("BALL requires positive speed and damage.");
            return stats;
        }
    }
}
