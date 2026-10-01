using System;

namespace Wreckabulary.Rules
{
    public enum LifeState { Alive, Downed, Eliminated }

    public enum HitSource { Unarmed, Melee, Thrown, Explosion, Hazard }

    /// <summary>
    /// One hit, as the server resolves it. Every damage source fills one of these, so health has
    /// a single entry point (<see cref="HealthModel.ApplyHit"/>).
    /// </summary>
    public struct HitInfo
    {
        public float Damage;
        public float Knockback;
        public float HitStun;
        /// <summary>How hard the hit is on furniture: a punch is 1, a BOMB 4. Players ignore it.</summary>
        public float BreakPower;
        public int AttackerId;
        public int AttackerTeam;
        public string ItemId;
        public HitSource Source;
        /// <summary>Horizontal direction the hit travels, from the attacker towards the target.</summary>
        public float DirX, DirZ;
        /// <summary>False for hazards: a PLATE can't block the Movers.</summary>
        public bool Blockable;

        public static HitInfo From(int attackerId, int attackerTeam, MeleeStats stats, string itemId, float dirX, float dirZ) => new HitInfo
        {
            Damage = stats.Damage,
            Knockback = stats.Knockback,
            HitStun = stats.HitStun,
            BreakPower = stats.BreakPower,
            AttackerId = attackerId,
            AttackerTeam = attackerTeam,
            ItemId = itemId,
            Source = itemId == null ? HitSource.Unarmed : HitSource.Melee,
            DirX = dirX,
            DirZ = dirZ,
            Blockable = true,
        };

        public static HitInfo Hazard(float damage) => new HitInfo
        {
            Damage = damage, AttackerId = -1, AttackerTeam = Teams.NoTeam, Source = HitSource.Hazard, Blockable = false,
        };
    }

    public enum HitIgnored { No, Eliminated, Downed, Invulnerable, FriendlyFire }

    public struct HitResult
    {
        public HitIgnored Ignored;
        public float Damage;
        public float Absorbed;
        public bool Blocked;
        /// <summary>Damage the shield stopped. The PLATE loses this much durability.</summary>
        public float BlockedDamage;
        /// <summary>Seconds of hit-stun to play, 0 while stagger-immune.</summary>
        public float HitStun;
        public float Knockback;
        public bool BecameDowned;
        public bool BecameEliminated;

        public bool Landed => Ignored == HitIgnored.No;
    }

    public static class Geometry
    {
        /// <summary>
        /// True if something in direction (toX, toZ) lies inside the front arc of a player facing
        /// (facingX, facingZ). Used for the PLATE's frontal block (brief §7).
        /// </summary>
        public static bool InFrontArc(float facingX, float facingZ, float toX, float toZ, float arcDegrees)
        {
            float fl = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            float tl = (float)Math.Sqrt(toX * toX + toZ * toZ);
            if (fl < 1e-5f || tl < 1e-5f) return false;
            float dot = (facingX * toX + facingZ * toZ) / (fl * tl);
            float half = arcDegrees * 0.5f * (float)Math.PI / 180f;
            return dot >= Math.Cos(half) - 1e-5;
        }
    }

    /// <summary>
    /// One player's 100 HP, protection and life state (brief §7). All times are server time in
    /// seconds.
    /// </summary>
    public sealed class HealthModel
    {
        readonly GameRules rules;
        public int PlayerId { get; }
        public int Team { get; }
        public float Current { get; private set; }
        public float Max => rules.MaxHealth;
        public LifeState State { get; private set; } = LifeState.Alive;
        public int TimesDowned { get; private set; }
        public double BleedOutAt { get; private set; }
        public float Bubble { get; private set; }
        public double BubbleUntil { get; private set; }
        public double InvulnerableUntil { get; private set; }
        public double StaggerImmuneUntil { get; private set; }
        public double LastDodgeAt { get; private set; } = double.NegativeInfinity;
        public int ReviverId { get; private set; } = -1;
        public double ReviveStartedAt { get; private set; }

        bool blocking;
        float facingX, facingZ = 1f;
        ShieldStats shield;

        public HealthModel(GameRules rules, int playerId, int team)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            PlayerId = playerId;
            Team = team;
            Current = rules.MaxHealth;
        }

        public bool IsAlive => State == LifeState.Alive;
        public bool IsBlocking => blocking;

        public void SetFacing(float x, float z)
        {
            facingX = x;
            facingZ = z;
        }

        /// <summary>Right click with a PLATE held. Pass null to lower it.</summary>
        public void SetBlocking(ShieldStats stats)
        {
            shield = stats;
            blocking = stats != null && IsAlive;
        }

        public HitResult ApplyHit(in HitInfo hit, double now)
        {
            if (now >= BubbleUntil) ClearBubble();
            var r = new HitResult();
            if (State == LifeState.Eliminated) { r.Ignored = HitIgnored.Eliminated; return r; }
            if (State == LifeState.Downed) { r.Ignored = HitIgnored.Downed; return r; }
            bool self = hit.AttackerId == PlayerId;
            if (!self && hit.AttackerId >= 0 && !rules.FriendlyFire && Teams.AreTeammates(hit.AttackerTeam, Team))
            {
                r.Ignored = HitIgnored.FriendlyFire;
                return r;
            }
            if (now < InvulnerableUntil) { r.Ignored = HitIgnored.Invulnerable; return r; }

            float damage = Math.Max(0f, hit.Damage);
            float knockback = hit.Knockback;
            // The attacker is opposite the hit's direction of travel.
            if (blocking && hit.Blockable && Geometry.InFrontArc(facingX, facingZ, -hit.DirX, -hit.DirZ, shield.FrontArcDegrees))
            {
                r.Blocked = true;
                float kept = damage * (1f - shield.DamageReduction);
                r.BlockedDamage = damage - kept;
                damage = kept;
                knockback *= 0.3f;
            }
            if (now < BubbleUntil && Bubble > 0f)
            {
                r.Absorbed = Math.Min(Bubble, damage);
                Bubble -= r.Absorbed;
                damage -= r.Absorbed;
                if (Bubble <= 0f) ClearBubble();
            }
            r.Damage = Math.Min(damage, Current);
            r.Knockback = knockback;
            Current -= r.Damage;

            if (!r.Blocked && r.Damage > 0f && now >= StaggerImmuneUntil && hit.HitStun > 0f)
            {
                r.HitStun = Math.Min(hit.HitStun, rules.HitStunMax);
                StaggerImmuneUntil = now + rules.StaggerImmunitySeconds;
            }

            if (Current <= 0f)
            {
                Current = 0f;
                blocking = false;
                if (rules.DownedEnabled)
                {
                    State = LifeState.Downed;
                    TimesDowned++;
                    BleedOutAt = now + rules.BleedOutFor(TimesDowned);
                    r.BecameDowned = true;
                }
                else
                {
                    State = LifeState.Eliminated;
                    r.BecameEliminated = true;
                }
            }
            return r;
        }

        /// <summary>Call every tick. Returns true when a downed player bleeds out.</summary>
        public bool Tick(double now)
        {
            if (now >= BubbleUntil) ClearBubble();
            if (State == LifeState.Downed && now >= BleedOutAt)
            {
                State = LifeState.Eliminated;
                ReviverId = -1;
                return true;
            }
            return false;
        }

        /// <summary>Ends a downed player's match at once, for example when their whole team is down.</summary>
        public void Eliminate()
        {
            if (State == LifeState.Eliminated) return;
            State = LifeState.Eliminated;
            Current = 0f;
            blocking = false;
            ReviverId = -1;
        }

        /// <summary>A teammate starts holding E on this downed player.</summary>
        public bool BeginRevive(int reviverId, int reviverTeam, double now)
        {
            if (State != LifeState.Downed || reviverId == PlayerId || !Teams.AreTeammates(reviverTeam, Team)) return false;
            if (ReviverId >= 0) return ReviverId == reviverId;
            ReviverId = reviverId;
            ReviveStartedAt = now;
            return true;
        }

        public void CancelRevive(int reviverId)
        {
            if (ReviverId == reviverId) ReviverId = -1;
        }

        /// <summary>Returns true once the reviver has held on long enough; the player stands up with some health.</summary>
        public bool TryFinishRevive(int reviverId, double now)
        {
            if (State != LifeState.Downed || ReviverId != reviverId) return false;
            if (now - ReviveStartedAt < rules.ReviveSeconds) return false;
            State = LifeState.Alive;
            Current = rules.ReviveHealth;
            ReviverId = -1;
            InvulnerableUntil = now + 1.0;
            return true;
        }

        public float Heal(float amount)
        {
            if (State != LifeState.Alive || amount <= 0f) return 0f;
            float healed = Math.Min(amount, Max - Current);
            Current += healed;
            return healed;
        }

        /// <summary>FOAM: protection that refreshes instead of stacking (brief §5, "non-stacking").</summary>
        public void GiveBubble(float amount, double until)
        {
            Bubble = amount;
            BubbleUntil = until;
        }

        /// <summary>Ends bubble protection, including its expiry deadline.</summary>
        public void ClearBubble()
        {
            Bubble = 0f;
            BubbleUntil = 0;
        }

        public bool CanDodge(double now) => IsAlive && now - LastDodgeAt >= rules.DodgeCooldown;

        public bool Dodge(double now)
        {
            if (!CanDodge(now)) return false;
            LastDodgeAt = now;
            InvulnerableUntil = Math.Max(InvulnerableUntil, now + rules.DodgeInvulnerableSeconds);
            return true;
        }

        public void Respawn(double now)
        {
            State = LifeState.Alive;
            Current = rules.MaxHealth;
            ClearBubble();
            blocking = false;
            shield = null;
            ReviverId = -1;
            ReviveStartedAt = 0;
            BleedOutAt = 0;
            LastDodgeAt = double.NegativeInfinity;
            StaggerImmuneUntil = 0;
            InvulnerableUntil = now + rules.SpawnProtectionSeconds;
        }

        /// <summary>A new round: full health and the downed count starts again.</summary>
        public void ResetForRound(double now)
        {
            TimesDowned = 0;
            Respawn(now);
        }
    }
}
