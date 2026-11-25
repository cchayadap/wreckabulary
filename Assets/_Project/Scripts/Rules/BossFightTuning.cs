using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public sealed class BossFightTuning
    {
        public const string Map = "pinwheel", Name = "CALCULATOR";
        public const int Phases = 4;
        public const float FeetGap = .55f;
        public static readonly IReadOnlyList<string> Words = new[] { "CALC", "ULA", "TOR" };

        public float BossMaxHealth = 240f, BossVulnerability = 1.5f;
        public float PrepSeconds = 20f, AppearSeconds = 12f, StaggerSeconds = 4f;
        public int StarterProps = 5;
        public float SupplyDropSeconds = 30f;
        public float AoeWarnSeconds = 1.6f, AoeActiveSeconds = .45f;
        public float[] AoeRadius = { 1.6f, 1.8f, 2f, 2.2f };
        public float[] AoeDamage = { 10f, 12f, 14f, 14f };
        public float[] AoeCooldown = { 5.5f, 5f, 4.4f, 4f };
        public int AoeBurstMin = 1, AoeBurstMax = 3, MaxAoe = 3, LettersLostOnHit = 1;
        public float MiniFirstSeconds = 25f, MiniIntervalSeconds = 30f;
        public int MiniMaxAlive = 2;
        public float MiniSpeed = 2.2f, MiniStunSeconds = .18f, MiniKnockback = 3.5f, MiniHitCooldownSeconds = 3f, MiniHealth = 42f;
        public float EventWarnSeconds = 3f;
        public float SmashEverySeconds = 6f, SmashWarnSeconds = 1f, SmashRadius = 2.4f, SmashDamage = 18f;

        public int Layers => Words.Count;

        public static string WordFor(int layer) => Words[Math.Max(0, Math.Min(Words.Count - 1, layer))];

        public static BossFightTuning From(GameRules rules)
        {
            var t = new BossFightTuning();
            if (rules == null) return t;
            t.BossMaxHealth = rules.Setting("bossMaxHealth", t.BossMaxHealth);
            t.BossVulnerability = rules.Setting("bossVulnerability", t.BossVulnerability);
            t.PrepSeconds = rules.Setting("prepSeconds", t.PrepSeconds);
            t.AppearSeconds = rules.Setting("appearSeconds", t.AppearSeconds);
            t.StaggerSeconds = rules.Setting("staggerSeconds", t.StaggerSeconds);
            t.StarterProps = rules.Setting("starterProps", t.StarterProps);
            t.SupplyDropSeconds = rules.Setting("supplyDropSeconds", t.SupplyDropSeconds);
            t.AoeWarnSeconds = rules.Setting("aoeWarnSeconds", t.AoeWarnSeconds);
            t.AoeActiveSeconds = rules.Setting("aoeActiveSeconds", t.AoeActiveSeconds);
            for (int i = 0; i < Phases; i++)
            {
                t.AoeRadius[i] = rules.Setting("aoeRadius" + (i + 1), t.AoeRadius[i]);
                t.AoeDamage[i] = rules.Setting("aoeDamage" + (i + 1), t.AoeDamage[i]);
                t.AoeCooldown[i] = rules.Setting("aoeCooldown" + (i + 1), t.AoeCooldown[i]);
            }
            t.AoeBurstMin = rules.Setting("aoeBurstMin", t.AoeBurstMin);
            t.AoeBurstMax = rules.Setting("aoeBurstMax", t.AoeBurstMax);
            t.MaxAoe = rules.Setting("maxAoe", t.MaxAoe);
            t.LettersLostOnHit = rules.Setting("lettersLostOnHit", t.LettersLostOnHit);
            t.MiniFirstSeconds = rules.Setting("miniFirstSeconds", t.MiniFirstSeconds);
            t.MiniIntervalSeconds = rules.Setting("miniIntervalSeconds", t.MiniIntervalSeconds);
            t.MiniMaxAlive = rules.Setting("miniMaxAlive", t.MiniMaxAlive);
            t.MiniSpeed = rules.Setting("miniSpeed", t.MiniSpeed);
            t.MiniStunSeconds = rules.Setting("miniStunSeconds", t.MiniStunSeconds);
            t.MiniKnockback = rules.Setting("miniKnockback", t.MiniKnockback);
            t.MiniHitCooldownSeconds = rules.Setting("miniHitCooldownSeconds", t.MiniHitCooldownSeconds);
            t.MiniHealth = rules.Setting("miniHealth", t.MiniHealth);
            t.EventWarnSeconds = rules.Setting("eventWarnSeconds", t.EventWarnSeconds);
            t.SmashEverySeconds = rules.Setting("smashEverySeconds", t.SmashEverySeconds);
            t.SmashWarnSeconds = rules.Setting("smashWarnSeconds", t.SmashWarnSeconds);
            t.SmashRadius = rules.Setting("smashRadius", t.SmashRadius);
            t.SmashDamage = rules.Setting("smashDamage", t.SmashDamage);
            return t;
        }

        static float ByPhase(float[] values, int phase) => values[Math.Max(0, Math.Min(values.Length - 1, phase))];

        public float AoeRadiusFor(int phase) => ByPhase(AoeRadius, phase);

        public float AoeDamageFor(int phase) => ByPhase(AoeDamage, phase);

        public float AoeCooldownFor(int phase) => ByPhase(AoeCooldown, phase);

        public float MiniHitCap => MiniHealth * .5f;

        public List<string> Validate()
        {
            var problems = new List<string>();
            void Need(bool ok, string problem) { if (!ok) problems.Add(problem); }
            Need(Words.Count == 3, "the boss needs exactly three shield words");
            Need(BossMaxHealth > 0f, "bossMaxHealth must be positive");
            Need(BossVulnerability > 0f, "bossVulnerability must be positive");
            Need(PrepSeconds >= 0f && AppearSeconds >= 0f, "prep and appear phases cannot be negative");
            Need(StaggerSeconds >= 0f, "staggerSeconds cannot be negative");
            Need(StarterProps >= 1, "starterProps must place at least one prop");
            Need(SupplyDropSeconds > 0f, "supplyDropSeconds must be positive");
            Need(AoeWarnSeconds > 0f, "aoeWarnSeconds must be positive");
            Need(AoeActiveSeconds > 0f, "aoeActiveSeconds must be positive");
            for (int i = 0; i < Phases; i++)
            {
                Need(AoeRadius[i] > 0f, "aoeRadius" + (i + 1) + " must be positive");
                Need(AoeDamage[i] > 0f, "aoeDamage" + (i + 1) + " must be positive");
                Need(AoeCooldown[i] > 0f, "aoeCooldown" + (i + 1) + " must be positive");
            }
            Need(AoeBurstMin >= 1 && AoeBurstMax >= AoeBurstMin, "aoeBurstMax must be at least aoeBurstMin");
            Need(MaxAoe >= AoeBurstMax, "maxAoe must fit the largest AoE burst");
            Need(LettersLostOnHit >= 0, "lettersLostOnHit cannot be negative");
            Need(MiniFirstSeconds >= 0f && MiniIntervalSeconds > 0f, "mini calculator schedule must be positive");
            Need(MiniMaxAlive >= 0 && MiniSpeed > 0f, "mini calculators need a speed and a non-negative cap");
            Need(MiniStunSeconds > 0f && MiniStunSeconds < .5f, "miniStunSeconds must be a brief stun");
            Need(MiniKnockback > 0f && MiniHitCooldownSeconds > MiniStunSeconds * 2f, "a mini must wait more than twice its stun before touching again");
            Need(MiniHealth > 0f, "miniHealth must be positive so minis survive more than one hit");
            Need(EventWarnSeconds > 0f, "eventWarnSeconds must be positive");
            Need(SmashEverySeconds > 0f && SmashWarnSeconds > 0f, "jump smash timings must be positive");
            Need(SmashRadius > 0f && SmashDamage > 0f, "jump smash needs a radius and damage");
            return problems;
        }
    }
}
