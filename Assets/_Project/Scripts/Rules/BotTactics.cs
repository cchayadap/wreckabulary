using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct ThrowWindow
    {
        public readonly float Near, Far;

        public ThrowWindow(float near, float far)
        {
            Near = near;
            Far = far;
        }

        public bool IsEmpty => Far <= 0f;
        public bool Contains(float distance) => !IsEmpty && distance >= Near && distance <= Far;
        public override string ToString() => $"{Near:0.0}-{Far:0.0} m";
    }

    public static class BotTactics
    {
        public const float FistRange = 1.35f, ReachSlack = .15f, PointBlank = .8f, MinAimDegrees = 4f, ThrowAimDegrees = 8f;
        public const float TelegraphSeconds = .45f, DodgeMargin = .8f, ThreatArcSlack = 25f, ReactionSeconds = .15f, GuardSeconds = 1f;
        public const float SafeToSpell = 4f, CloseQuarters = 3f, ShootRange = 9f;
        public const float FlatLift = .3f, LobLift = 4f, LaunchHeight = 1.1f, LowestHit = .25f, HighestHit = 1.6f;
        public const float HelperReach = 2f, HelperPatience = 4f, HelperRest = 6f, HelperDodgeLead = .1f, TrapMargin = .45f, TrapProbe = .85f;
        public const float ObstacleNormal = .6f;

        public static bool Throws(ItemDefinition held) => held != null && held.Use == null && held.Thrown != null;

        public static bool Shoots(ItemDefinition held) => held != null && held.Use == null && held.Ranged != null;

        public static bool Swings(ItemDefinition held) =>
            held != null && held.Use == null && held.Thrown == null && held.Deploy == null && held.Shield == null && held.Melee != null;

        public static float StrikeRange(ItemDefinition held) => Swings(held) ? Math.Max(FistRange, held.Melee.Reach + ReachSlack) : FistRange;

        public static float AimDegrees(ItemDefinition held, float fistArc) =>
            Throws(held) || Shoots(held) ? ThrowAimDegrees : Math.Max(MinAimDegrees, (Swings(held) ? held.Melee.ArcDegrees : fistArc) * .5f);

        public static ThrowWindow Window(ThrownStats thrown, float gravity)
        {
            if (thrown == null || thrown.Speed <= 0f || gravity <= 0f) return default;
            float lift = thrown.Lob ? LobLift : FlatLift;
            float far = thrown.Speed * Descending(lift, gravity, LowestHit - LaunchHeight);
            float apex = LaunchHeight + lift * lift / (2f * gravity);
            float near = apex > HighestHit ? thrown.Speed * Descending(lift, gravity, HighestHit - LaunchHeight) : 0f;
            return new ThrowWindow(near, far);
        }

        static float Descending(float lift, float gravity, float rise) => (lift + (float)Math.Sqrt(Math.Max(0f, lift * lift - 2f * gravity * rise))) / gravity;

        public static bool Telegraphed(MeleeStats swing) => swing != null && swing.Windup >= TelegraphSeconds;

        public static bool Threatens(MeleeStats swing, float distance, float degreesOffFacing) =>
            Telegraphed(swing) && distance <= swing.Reach + DodgeMargin && degreesOffFacing <= swing.ArcDegrees * .5f + ThreatArcSlack;

        public static bool AvoidsHelper(bool bites, HelperPhase phase, float distance, float range, float windup) =>
            bites && phase == HelperPhase.WindUp && windup >= ReactionSeconds + HelperDodgeLead && distance <= range + DodgeMargin;

        public static bool SmashesHelper(bool armed, bool bites, bool ending, float distance, float chasedSeconds) =>
            armed && bites && !ending && distance <= HelperReach && chasedSeconds < HelperPatience;

        public static bool GivesUpOnHelper(float chasedSeconds) => chasedSeconds >= HelperPatience;

        public static bool StepsAroundTrap(bool bites, bool isTrap, bool ending, float distance, float radius) =>
            bites && isTrap && !ending && distance <= radius + TrapMargin;

        public static bool SteersAround(float surfaceNormalY) => surfaceNormalY < ObstacleNormal;

        public static bool Helps(IReadOnlyList<char> bag, char letter, IEnumerable<string> wanted)
        {
            letter = char.ToUpperInvariant(letter);
            int have = Count(bag, letter);
            foreach (string word in wanted)
                if (Count(word, letter) > have) return true;
            return false;
        }

        public static char Junk(IReadOnlyList<char> bag, IEnumerable<string> wanted)
        {
            if (bag == null || bag.Count == 0) return '\0';
            var have = new int[26];
            foreach (char c in bag) if (Index(c) >= 0) have[Index(c)]++;
            var value = new float[26];
            foreach (string word in wanted)
            {
                var need = new int[26];
                foreach (char c in word) if (Index(c) >= 0) need[Index(c)]++;
                int missing = 0;
                for (int i = 0; i < 26; i++) missing += Math.Max(0, need[i] - have[i]);
                float weight = 1f / (1 + missing);
                for (int i = 0; i < 26; i++)
                    if (have[i] > 0 && need[i] >= have[i]) value[i] += weight;
            }
            char junk = '\0';
            int best = -1;
            foreach (char c in bag)
            {
                int i = Index(c);
                if (i < 0) return char.ToUpperInvariant(c);
                if (best < 0 || value[i] < value[best] || (value[i] == value[best] && have[i] > have[best])) { best = i; junk = (char)('A' + i); }
            }
            return junk;
        }

        static int Index(char c)
        {
            int i = char.ToUpperInvariant(c) - 'A';
            return i >= 0 && i < 26 ? i : -1;
        }

        static int Count(IEnumerable<char> letters, char letter)
        {
            int n = 0;
            foreach (char c in letters) if (char.ToUpperInvariant(c) == letter) n++;
            return n;
        }
    }
}
