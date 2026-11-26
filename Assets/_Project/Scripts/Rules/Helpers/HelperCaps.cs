using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class HelperCaps
    {
        public const float MinTelegraph = .35f;
        public const float MinTouchWindup = .15f;
        public const float MinCooldown = .6f;
        public const float MaxKnockback = 6f;
        public const float MinSlow = .5f;
        public const float MaxSlowSeconds = 1.5f;
        public const int MinStrikes = 1, MaxStrikes = 8;
        public const float MaxTrapRadius = 1.2f;
        public const float AnyoneTotalBonus = 1.25f;
        public const float MaxCloseRange = MeleeFairness.MaxSwingReach;
        public const int MaxMoversPerPlayer = 1;
        public const int MaxInPlay = 6;
        public const int MaxPerTarget = 2;
        const float Slack = 1e-3f;

        static readonly float[] StrikeDamage = { 8f, 10f, 12f, 14f };
        static readonly float[] DamagePerSecond = { 6f, 7f, 8f, 9f };
        static readonly float[] TotalDamage = { 18f, 26f, 34f, 42f };
        static readonly float[] MoverLife = { 8f, 10f, 12f, 14f };
        static readonly float[] StayerLife = { 12f, 15f, 18f, 20f };
        static readonly float[] WallLife = { 8f, 10f, 12f, 15f };
        static readonly float[] Speed = { 3.0f, 3.4f, 3.8f, 4.2f };
        static readonly float[] Sight = { 8f, 8f, 9f, 10f };
        static readonly float[] Leash = { 10f, 10f, 11f, 12f };
        static readonly float[] Radius = { 2.5f, 3.0f, 3.5f, 4.0f };
        static readonly float[] MinHealth = { 12f, 16f, 20f, 24f };
        static readonly float[] MaxHealth = { 20f, 26f, 32f, 40f };
        static readonly float[] ShotSpeed = { 10f, 11f, 12f, 13f };

        public static float MaxStrikeDamage(RecipeTier tier) => StrikeDamage[(int)tier];
        public static float MaxDamagePerSecond(RecipeTier tier) => DamagePerSecond[(int)tier];
        public static float MaxTotalDamage(RecipeTier tier, HelperBite bites) => TotalDamage[(int)tier] * (bites == HelperBite.Anyone ? AnyoneTotalBonus : 1f);
        public static float MaxLife(RecipeTier tier, bool moves) => (moves ? MoverLife : StayerLife)[(int)tier];
        public static float MaxWallLife(RecipeTier tier) => WallLife[(int)tier];
        public static float MaxSpeed(RecipeTier tier) => Speed[(int)tier];
        public static float MaxSight(RecipeTier tier) => Sight[(int)tier];
        public static float MaxLeash(RecipeTier tier) => Leash[(int)tier];
        public static float MaxRadius(RecipeTier tier) => Radius[(int)tier];
        public static float LeastHealth(RecipeTier tier) => MinHealth[(int)tier];
        public static float MostHealth(RecipeTier tier) => MaxHealth[(int)tier];
        public static float MaxShotSpeed(RecipeTier tier) => ShotSpeed[(int)tier];

        public static bool Telegraphed(HelperAttack attack) => attack is HelperAttack.Swing or HelperAttack.Shot or HelperAttack.Burst;

        public static float LeastWindup(HelperAttack attack) => Telegraphed(attack) ? MinTelegraph : attack == HelperAttack.Touch ? MinTouchWindup : 0f;

        public static List<string> Check(HelperSpec spec) => Check(spec, RecipeDifficulty.TierOf(spec.Id));

        public static List<string> Check(HelperSpec spec, RecipeTier tier)
        {
            var problems = new List<string>();
            void Require(bool valid, string message) { if (!valid) problems.Add($"{spec.Id} is {tier}: {message}."); }
            Require(Finite(spec.Seconds) && spec.Seconds > 0f && spec.Seconds <= MaxLife(tier, spec.Moves) + Slack,
                $"life {spec.Seconds} s must be above 0 and at most {MaxLife(tier, spec.Moves)} s");
            Require(Finite(spec.Health) && spec.Health >= LeastHealth(tier) - Slack && spec.Health <= MostHealth(tier) + Slack,
                $"health {spec.Health} must be {LeastHealth(tier)}-{MostHealth(tier)}");
            if (spec.Moves)
            {
                Require(Positive(spec.Speed) && spec.Speed <= MaxSpeed(tier) + Slack, $"speed {spec.Speed} must be above 0 and at most {MaxSpeed(tier)} m/s");
                Require(Positive(spec.Sight) && spec.Sight <= MaxSight(tier) + Slack, $"sight {spec.Sight} must be above 0 and at most {MaxSight(tier)} m");
                Require(Positive(spec.Leash) && spec.Leash <= MaxLeash(tier) + Slack, $"leash {spec.Leash} must be above 0 and at most {MaxLeash(tier)} m");
                Require(spec.Pick != HelperPick.Stepper, "a trap must stay where it is placed");
            }
            else
            {
                Require(Positive(spec.Radius) && spec.Radius <= MaxRadius(tier) + Slack, $"radius {spec.Radius} must be above 0 and at most {MaxRadius(tier)} m");
                Require(spec.Speed == 0f, "a helper that stays has no speed");
            }
            if (spec.IsTrap) Require(spec.Radius <= MaxTrapRadius + Slack, $"a trap reaches at most {MaxTrapRadius} m");
            if (!spec.Hurts)
            {
                Require(spec.Attack == HelperAttack.None && spec.Damage == 0f, "a helper without an attack does no damage");
                Require(!spec.Slows && spec.Knockback == 0f, "a helper without an attack has no rider or knockback");
                return problems;
            }
            Require(Positive(spec.Damage) && spec.Damage <= MaxStrikeDamage(tier) + Slack, $"strike damage {spec.Damage} must be above 0 and at most {MaxStrikeDamage(tier)}");
            Require(Finite(spec.Windup) && spec.Windup >= LeastWindup(spec.Attack) - Slack, $"{spec.Attack} winds up at least {LeastWindup(spec.Attack)} s");
            Require(Finite(spec.Cooldown) && spec.Cooldown >= MinCooldown - Slack, $"cooldown must be at least {MinCooldown} s");
            Require(spec.DamagePerSecond <= MaxDamagePerSecond(tier) + Slack, $"{spec.DamagePerSecond:0.0} damage a second is over its {MaxDamagePerSecond(tier)}");
            Require(spec.TotalDamage <= MaxTotalDamage(tier, spec.Bites) + Slack, $"{spec.TotalDamage:0.#} total damage is over its {MaxTotalDamage(tier, spec.Bites):0.#}");
            Require(Finite(spec.Knockback) && spec.Knockback >= 0f && spec.Knockback <= MaxKnockback + Slack, $"knockback must be 0-{MaxKnockback}");
            Require(spec.Strikes >= MinStrikes && spec.Strikes <= MaxStrikes, $"strikes must be {MinStrikes}-{MaxStrikes}");
            if (spec.Attack == HelperAttack.Burst) Require(spec.Strikes == 1, "a burst strikes once and ends");
            Require(Positive(spec.Range) && spec.Range <= spec.Reach + Slack, $"range {spec.Range} must be above 0 and within its {spec.Reach} m sense");
            if (spec.Attack is HelperAttack.Touch or HelperAttack.Swing)
                Require(spec.Range <= MaxCloseRange + Slack, $"a close strike reaches at most {MaxCloseRange} m");
            if (spec.Attack == HelperAttack.Shot)
                Require(Positive(spec.ShotSpeed) && spec.ShotSpeed <= MaxShotSpeed(tier) + Slack, $"shot speed {spec.ShotSpeed} must be above 0 and at most {MaxShotSpeed(tier)} m/s");
            else Require(spec.ShotSpeed == 0f, "only a shot has a shot speed");
            if (spec.Slow != 0f || spec.SlowSeconds != 0f)
                Require(spec.Slow >= MinSlow - Slack && spec.Slow < 1f && Positive(spec.SlowSeconds) && spec.SlowSeconds <= MaxSlowSeconds + Slack,
                    $"a slow is {MinSlow * 100f:0}-99% for at most {MaxSlowSeconds} s");
            return problems;
        }

        public static List<string> CheckWall(ItemDefinition item) => CheckWall(item, RecipeDifficulty.TierOf(item.Id));

        public static List<string> CheckWall(ItemDefinition item, RecipeTier tier)
        {
            var problems = new List<string>();
            float life = item.Deploy?.LifetimeSeconds ?? 0f;
            if (!Positive(life) || life > MaxWallLife(tier) + Slack) problems.Add($"{item.Id} is {tier}: a wall lasts above 0 and at most {MaxWallLife(tier)} s, not {life}.");
            if (item.Durability <= 0) problems.Add($"{item.Id}: a wall needs durability.");
            return problems;
        }

        public static float WallStrength(ItemDefinition item) => item.Durability * (item.Deploy?.LifetimeSeconds ?? 0f);

        public static IEnumerable<string> Inversions(IReadOnlyList<HelperSpec> specs)
        {
            foreach (var spec in specs)
            {
                if (!spec.Hurts) continue;
                var tier = RecipeDifficulty.TierOf(spec.Id);
                HelperSpec rival = null;
                foreach (var other in specs)
                    if (other.Hurts && other.Moves == spec.Moves && RecipeDifficulty.TierOf(other.Id) < tier && (rival == null || other.Strength > rival.Strength)) rival = other;
                if (rival != null && spec.Strength < rival.Strength - Slack)
                    yield return $"{spec.Id} is {tier} but its strength ({spec.Strength:0.0}) is below {RecipeDifficulty.TierOf(rival.Id)} {rival.Id} ({rival.Strength:0.0}); a harder word must never make a weaker helper.";
            }
        }

        public static IEnumerable<string> WallInversions(IReadOnlyList<ItemDefinition> walls)
        {
            foreach (var wall in walls)
            {
                var tier = RecipeDifficulty.TierOf(wall.Id);
                ItemDefinition rival = null;
                foreach (var other in walls)
                    if (RecipeDifficulty.TierOf(other.Id) < tier && (rival == null || WallStrength(other) > WallStrength(rival))) rival = other;
                if (rival != null && WallStrength(wall) < WallStrength(rival) - Slack)
                    yield return $"{wall.Id} is {tier} but its durability x lifetime ({WallStrength(wall):0}) is below {RecipeDifficulty.TierOf(rival.Id)} {rival.Id} ({WallStrength(rival):0}); a harder word must never make a weaker wall.";
            }
        }

        public static bool Admits(int makerMovers, bool moves) => !moves || makerMovers < MaxMoversPerPlayer;

        public static bool RetiresOldest(int inPlay) => inPlay >= MaxInPlay;

        public static bool Crowded(int holders) => holders >= MaxPerTarget;

        public static bool SlowReady(HelperSpec spec, float sinceLastSlow) => spec.Slows && sinceLastSlow >= spec.SlowSeconds + spec.Cooldown;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0f;
    }
}
