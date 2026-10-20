using System;
using System.Globalization;
using System.Text;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public static class WordboundRulesFingerprint
    {
        public const int Version = 3;

        public static string Compute(GameRules rules)
        {
            if (rules == null || rules.Unarmed == null || rules.BleedOutSeconds == null || rules.BleedOutSeconds.Length > 64)
                throw new ArgumentException("Rules fingerprint requires complete bounded gameplay rules.");
            var text = new StringBuilder("wordbound.game-rules|3|");
            void Value(string name, string value)
            {
                if (value == null || value.Length > 256) throw new ArgumentException("Rule strings exceed the fingerprint bound.");
                text.Append(name).Append(':').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append('|');
            }
            void F(string name, float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Gameplay rules must be finite.");
                Value(name, unchecked((uint)BitConverter.SingleToInt32Bits(value)).ToString("x8", CultureInfo.InvariantCulture));
            }
            void I(string name, int value) => Value(name, value.ToString(CultureInfo.InvariantCulture));
            void B(string name, bool value) => Value(name, value ? "1" : "0");
            Value("EntityBudget", WordboundEntityBudget.Hash);
            Value("Defense", WordboundDefensePolicy.Hash);
            Value("Mode", rules.Mode);
            F("MaxHealth", rules.MaxHealth);
            F("SpawnProtectionSeconds", rules.SpawnProtectionSeconds);
            I("MaxLetters", rules.MaxLetters);
            I("MaxCarried", rules.MaxCarried);
            I("MaxDeployed", rules.MaxDeployed);
            Value("StarterLetters", rules.StarterLetters);
            I("LettersDroppedPerHit", rules.LettersDroppedPerHit);
            F("FurnitureToughnessBase", rules.FurnitureToughnessBase);
            F("FurnitureToughnessPerLetter", rules.FurnitureToughnessPerLetter);
            F("CraftBaseSeconds", rules.CraftBaseSeconds);
            F("CraftPerLetterSeconds", rules.CraftPerLetterSeconds);
            F("CraftMoveSpeed", rules.CraftMoveSpeed);
            F("HitStunMax", rules.HitStunMax);
            F("StaggerImmunitySeconds", rules.StaggerImmunitySeconds);
            F("DodgeSeconds", rules.DodgeSeconds);
            F("DodgeInvulnerableSeconds", rules.DodgeInvulnerableSeconds);
            F("DodgeDistance", rules.DodgeDistance);
            F("DodgeCooldown", rules.DodgeCooldown);
            F("MaxStamina", rules.MaxStamina);
            F("DodgeStamina", rules.DodgeStamina);
            F("StaminaRegen", rules.StaminaRegen);
            F("StaminaRegenDelay", rules.StaminaRegenDelay);
            F("JumpHeight", rules.JumpHeight);
            F("GroundAccel", rules.GroundAccel);
            F("GroundFriction", rules.GroundFriction);
            F("Unarmed.Damage", rules.Unarmed.Damage);
            F("Unarmed.Reach", rules.Unarmed.Reach);
            F("Unarmed.ArcDegrees", rules.Unarmed.ArcDegrees);
            F("Unarmed.Windup", rules.Unarmed.Windup);
            F("Unarmed.Active", rules.Unarmed.Active);
            F("Unarmed.Recovery", rules.Unarmed.Recovery);
            F("Unarmed.Knockback", rules.Unarmed.Knockback);
            F("Unarmed.BreakPower", rules.Unarmed.BreakPower);
            F("Unarmed.HitStun", rules.Unarmed.HitStun);
            I("TeamSize", rules.TeamSize);
            B("FriendlyFire", rules.FriendlyFire);
            B("DownedEnabled", rules.DownedEnabled);
            I("BleedOutSeconds.Length", rules.BleedOutSeconds.Length);
            for (int i = 0; i < rules.BleedOutSeconds.Length; i++)
                F("BleedOutSeconds." + i.ToString(CultureInfo.InvariantCulture), rules.BleedOutSeconds[i]);
            F("ReviveSeconds", rules.ReviveSeconds);
            F("ReviveHealth", rules.ReviveHealth);
            F("ReviveRange", rules.ReviveRange);
            I("RoundsToWin", rules.RoundsToWin);
            F("RespawnSeconds", rules.RespawnSeconds);
            F("ReconnectGraceSeconds", rules.ReconnectGraceSeconds);
            F("RoundTimeLimitSeconds", rules.RoundTimeLimitSeconds);
            B("ClearOutEnabled", rules.ClearOutEnabled);
            F("ClearOutFirstAt", rules.ClearOutFirstAt);
            F("ClearOutInterval", rules.ClearOutInterval);
            F("ClearOutWarnSeconds", rules.ClearOutWarnSeconds);
            F("ClearOutFillSeconds", rules.ClearOutFillSeconds);
            F("ClearOutDamagePerSecond", rules.ClearOutDamagePerSecond);
            F("ClearOutDamageGrowth", rules.ClearOutDamageGrowth);
            B("EscapeNeedsKey", rules.EscapeNeedsKey);
            I("EscapeKeyAfterRooms", rules.EscapeKeyAfterRooms);
            B("EscapeRandomExit", rules.EscapeRandomExit);
            I("EscapeKeys", rules.EscapeKeys);
            return WordboundIdentity.Sha256(text.ToString());
        }
    }
}
