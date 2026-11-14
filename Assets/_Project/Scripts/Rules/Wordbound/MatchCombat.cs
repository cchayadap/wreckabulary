using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class MatchAttackState
    {
        public long StartedTick { get; }
        public string WeaponEntityId { get; }
        public IReadOnlyList<string> HitBodyIds { get; }

        public MatchAttackState(long startedTick, string weaponEntityId = "", IEnumerable<string> hitBodyIds = null)
        {
            var hits = hitBodyIds?.Take(8193).ToArray() ?? Array.Empty<string>();
            if (startedTick < 1 || weaponEntityId == null ||
                weaponEntityId != "" && !WordboundIdentity.IsId(weaponEntityId) || hits.Length > 8192 ||
                hits.Any(id => !WordboundIdentity.IsId(id)) ||
                hits.Distinct(StringComparer.Ordinal).Count() != hits.Length)
                throw new ArgumentException("Invalid authoritative attack state.");
            StartedTick = startedTick; WeaponEntityId = weaponEntityId;
            HitBodyIds = Array.AsReadOnly(hits.OrderBy(id => id, StringComparer.Ordinal).ToArray());
        }
    }

    public static class WordboundCombatPolicy
    {
        public const int MaximumCycleTicks = 3600;

        public static int Ticks(float seconds)
        {
            if (!WorldVector.Finite(seconds) || seconds < 0 || seconds > 60)
                throw new ArgumentException("Combat duration is outside the bounded tick domain.");
            return checked((int)Math.Ceiling((double)seconds * ModeDefinition.TickRate));
        }

        public static int WindupTicks(MeleeStats stats) { Validate(stats); return Ticks(stats.Windup); }
        public static int ActiveTicks(MeleeStats stats) { Validate(stats); return Math.Max(1, Ticks(stats.Active)); }
        public static int CycleTicks(MeleeStats stats)
        {
            Validate(stats);
            return checked(Ticks(stats.Windup) + Math.Max(1, Ticks(stats.Active)) + Ticks(stats.Recovery));
        }

        public static string EquippedItemId(MatchSnapshot snapshot, string playerId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            int index = snapshot.Binding.Seats.ToList().FindIndex(s => s.PlayerId == playerId);
            var actor = snapshot.Actors.FirstOrDefault(a => a.PlayerId == playerId);
            var inventory = snapshot.Economy.Players.FirstOrDefault(p => p.PlayerId == index);
            if (actor == null || inventory == null || inventory.CarriedFurniture >= 0 ||
                actor.EquippedSlot < 0 || actor.EquippedSlot >= inventory.Slots.Length) return "";
            int itemId = inventory.Slots[actor.EquippedSlot];
            return itemId > 0 ? "item:" + itemId : "";
        }

        internal static void Validate(MeleeStats stats)
        {
            if (stats == null || !Positive(stats.Damage, 1000) || !Positive(stats.Reach, 40) ||
                !Positive(stats.ArcDegrees, 360) || !Positive(stats.Active, 60) ||
                !NonNegative(stats.BreakPower, 1000) || !NonNegative(stats.Knockback, 100) ||
                !NonNegative(stats.HitStun, 60) ||
                Ticks(stats.Windup) + Math.Max(1, Ticks(stats.Active)) + Ticks(stats.Recovery) > MaximumCycleTicks)
                throw new ArgumentException("Melee statistics exceed the authoritative combat bounds.");
        }

        internal static bool InReach(WorldBodyState origin, WorldBodyState target, MeleeStats stats)
        {
            var delta = new WorldVector(target.Position.X - origin.Position.X,
                target.Position.Y - origin.Position.Y, target.Position.Z - origin.Position.Z);
            if (delta.DistanceSquared(default) > stats.Reach * stats.Reach) return false;
            var q = origin.RotationXyz;
            float x = 2 * (q.X * q.Z + origin.RotationW * q.Y), z = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            return delta.X * delta.X + delta.Z * delta.Z <= .000001f ||
                Geometry.InFrontArc(x, z, delta.X, delta.Z, stats.ArcDegrees);
        }

        static bool Positive(float value, float max) => WorldVector.Finite(value) && value > 0 && value <= max;
        static bool NonNegative(float value, float max) => WorldVector.Finite(value) && value >= 0 && value <= max;
    }
}
