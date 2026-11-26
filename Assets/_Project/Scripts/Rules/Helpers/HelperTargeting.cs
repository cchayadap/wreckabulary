using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct HelperCandidate
    {
        public readonly int Id, Team;
        public readonly float X, Y, Z;
        public readonly bool Hittable, InSight;

        public HelperCandidate(int id, int team, float x, float y, float z, bool hittable, bool inSight)
        {
            Id = id; Team = team; X = x; Y = y; Z = z; Hittable = hittable; InSight = inSight;
        }
    }

    public readonly struct HelperVictim
    {
        public readonly LifeState Life;
        public readonly bool Protected, Dodging, Carried, Frozen, Reviving;
        public readonly int Holders;

        public HelperVictim(LifeState life, bool isProtected, bool dodging, bool carried, bool frozen, bool reviving, int holders)
        {
            Life = life; Protected = isProtected; Dodging = dodging; Carried = carried; Frozen = frozen; Reviving = reviving; Holders = holders;
        }
    }

    public static class HelperTargeting
    {
        public const float SameLevel = 1.2f;
        public const float AimCone = 35f;
        public const float StrikeSlack = .3f;
        public const float ProvokeSeconds = 5f;

        public static bool Hittable(in HelperVictim v) =>
            v.Life == LifeState.Alive && !v.Protected && !v.Dodging && !v.Carried && !v.Frozen && !v.Reviving && !HelperCaps.Crowded(v.Holders);

        public static bool Legal(HelperSpec spec, int makerId, int makerTeam, in HelperCandidate c)
        {
            if (!c.Hittable) return false;
            return Bites(spec, makerId, makerTeam, c.Id, c.Team);
        }

        public static bool Bites(HelperSpec spec, int makerId, int makerTeam, int id, int team)
        {
            if (id < 0) return false;
            if (spec.Bites == HelperBite.Anyone) return true;
            return id != makerId && Teams.AreHostile(makerTeam, team);
        }

        public static int Pick(HelperSpec spec, int makerId, int makerTeam, float x, float y, float z,
            int lockedId, int provokerId, IReadOnlyList<HelperCandidate> all)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            float reach = spec.Reach;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!Legal(spec, makerId, makerTeam, c) || Math.Abs(c.Y - y) > SameLevel) continue;
                float dx = c.X - x, dz = c.Z - z, sq = dx * dx + dz * dz;
                if (sq > reach * reach) continue;
                bool wanted = spec.Pick switch
                {
                    HelperPick.Aimed => lockedId < 0 || c.Id == lockedId,
                    HelperPick.Provoker => c.Id == provokerId,
                    _ => true,
                };
                if (!wanted || (!c.InSight && c.Id != lockedId)) continue;
                if (sq < bestSq) { bestSq = sq; best = c.Id; }
            }
            return best;
        }

        public static int Choose(HelperSpec spec, ref HelperState state, int makerId, int makerTeam, float x, float y, float z,
            IReadOnlyList<HelperCandidate> all)
        {
            if (state.LockedId >= 0 && !LockHolds(spec, makerId, makerTeam, state.LockedId, all)) state.LockedId = -1;
            int provoker = spec.Pick == HelperPick.Provoker ? state.Provoker : -1;
            return Pick(spec, makerId, makerTeam, x, y, z, state.LockedId, provoker, all);
        }

        public static bool LockHolds(HelperSpec spec, int makerId, int makerTeam, int lockedId, IReadOnlyList<HelperCandidate> all)
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i].Id == lockedId) return Legal(spec, makerId, makerTeam, all[i]);
            return false;
        }

        public static int AimedLock(HelperSpec spec, int makerId, int makerTeam, float x, float y, float z, float facingX, float facingZ,
            int pointerId, IReadOnlyList<HelperCandidate> all)
        {
            if (spec.Pick != HelperPick.Aimed) return -1;
            float reach = spec.Reach;
            float facing = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (!Legal(spec, makerId, makerTeam, c) || Math.Abs(c.Y - y) > SameLevel) continue;
                float dx = c.X - x, dz = c.Z - z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance > reach) continue;
                if (c.Id == pointerId) return c.Id;
                if (!c.InSight || facing < 1e-4f) continue;
                float angle = distance < 1e-4f ? 0f : Angle(facingX / facing, facingZ / facing, dx / distance, dz / distance);
                if (angle > AimCone) continue;
                float score = CarryRules.AimScore(distance, angle, false);
                if (score < bestScore) { bestScore = score; best = c.Id; }
            }
            return best;
        }

        public static bool StrikeLands(HelperSpec spec, float distance, bool legal) => legal && distance <= spec.Range + StrikeSlack;

        public static bool Provokes(HelperSpec spec, int makerId, int makerTeam, int attackerId, int attackerTeam) =>
            attackerId >= 0 && attackerId != makerId && Bites(spec, makerId, makerTeam, attackerId, attackerTeam);

        static float Angle(float ax, float az, float bx, float bz)
        {
            float dot = Math.Max(-1f, Math.Min(1f, ax * bx + az * bz));
            return (float)(Math.Acos(dot) * 180.0 / Math.PI);
        }
    }
}
