using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Anything a hit can land on: players and smashable furniture. Every damage source (fists,
    /// weapons, throws, projectiles, blasts, hazards) builds a <see cref="HitInfo"/> with
    /// <see cref="Hits"/> and passes it here, so each target has a single entry point.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Returns true if the hit landed (not blocked, not ignored).</summary>
        bool ApplyDamage(in HitInfo hit);
    }

    /// <summary>Builds <see cref="HitInfo"/>s from world positions and players.</summary>
    public static class Hits
    {
        /// <summary>
        /// Knockback in rules.json and items.json is in shove units. One unit is this many metres
        /// per second of push, so a punch (3) shoves at 6 m/s and a BAT (7) at 14 m/s.
        /// </summary>
        public const float KnockbackSpeed = 2f;

        /// <summary>A fist or a melee item, with the numbers from rules.json or items.json.</summary>
        public static HitInfo Melee(PlayerController attacker, Vector3 direction, MeleeStats stats, string itemId)
        {
            var dir = Flat(direction, attacker);
            return HitInfo.From(IdOf(attacker), TeamOf(attacker), stats, itemId, dir.x, dir.z);
        }

        /// <summary>Any other hit: a throw, a projectile, a blast, a summon.</summary>
        public static HitInfo Of(PlayerController attacker, Vector3 direction, HitSource source, float damage, float knockback,
                                 float hitStun = 0.2f, float breakPower = 1f, string itemId = null)
        {
            var dir = Flat(direction, attacker);
            return new HitInfo
            {
                Damage = damage,
                Knockback = knockback,
                HitStun = hitStun,
                BreakPower = breakPower,
                AttackerId = IdOf(attacker),
                AttackerTeam = TeamOf(attacker),
                ItemId = itemId,
                Source = source,
                DirX = dir.x,
                DirZ = dir.z,
                Blockable = source != HitSource.Hazard,
            };
        }

        /// <summary>The Movers' clear-out: no attacker, and a PLATE can't block it.</summary>
        public static HitInfo Hazard(Vector3 direction, float damage, float knockback = 0f)
        {
            var hit = HitInfo.Hazard(damage);
            var dir = Flat(direction, null);
            hit.DirX = dir.x;
            hit.DirZ = dir.z;
            hit.Knockback = knockback;
            return hit;
        }

        /// <summary>The direction the hit travels, flat, or zero if it had none.</summary>
        public static Vector3 Direction(in HitInfo hit) => new(hit.DirX, 0f, hit.DirZ);

        /// <summary>The player who landed the hit, or null for hazards and falling boxes.</summary>
        public static PlayerController Attacker(in HitInfo hit) => World.PlayerById(hit.AttackerId);

        static int IdOf(PlayerController p) => p ? p.Index : -1;
        static int TeamOf(PlayerController p) => p ? p.Team : Teams.NoTeam;

        static Vector3 Flat(Vector3 direction, PlayerController attacker)
        {
            var d = World.Flat(direction);
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
            return attacker ? attacker.Facing : Vector3.zero;
        }
    }
}
