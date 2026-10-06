using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public interface IDamageable
    {
        bool ApplyDamage(in HitInfo hit);
    }

    public static class Hits
    {
        public const float KnockbackSpeed = 2f;

        public static HitInfo Melee(PlayerController attacker, Vector3 direction, MeleeStats stats, string itemId)
        {
            var dir = Flat(direction, attacker);
            return HitInfo.From(IdOf(attacker), TeamOf(attacker), stats, itemId, dir.x, dir.z);
        }

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

        public static HitInfo Hazard(Vector3 direction, float damage, float knockback = 0f)
        {
            var hit = HitInfo.Hazard(damage);
            var dir = Flat(direction, null);
            hit.DirX = dir.x;
            hit.DirZ = dir.z;
            hit.Knockback = knockback;
            return hit;
        }

        public static Vector3 Direction(in HitInfo hit) => new(hit.DirX, 0f, hit.DirZ);

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
