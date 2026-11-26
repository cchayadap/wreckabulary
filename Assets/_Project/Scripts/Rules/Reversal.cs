using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wreckabulary.Rules
{
    public enum ReverseMode { None, Mirror, Flip, Turnabout }

    public enum BotReversalStep { Steady, Stumble, Wait, Adapted }

    public static class Reversal
    {
        public const float BotStumbleSeconds = .6f;
        public const float BotWaitSeconds = .8f;
        public const float MinHitSeconds = 1.5f;
        public const float MaxZoneTailSeconds = 1.5f;
        public const float MaxRaisedTailSeconds = 1f;
        public const float MaxRaisedRadius = 3.5f;
        public const float OtherTraitPenalty = .5f;
        public const float DazePenalty = 1f;
        public const float HeightReach = 1.25f;

        public static bool SwapsSides(ReverseMode mode) => mode is ReverseMode.Mirror or ReverseMode.Turnabout;

        public static bool SwapsEnds(ReverseMode mode) => mode is ReverseMode.Flip or ReverseMode.Turnabout;

        public static ReverseMode Of(bool sides, bool ends) =>
            sides && ends ? ReverseMode.Turnabout : sides ? ReverseMode.Mirror : ends ? ReverseMode.Flip : ReverseMode.None;

        public static ReverseMode Combine(ReverseMode a, ReverseMode b) => Of(SwapsSides(a) || SwapsSides(b), SwapsEnds(a) || SwapsEnds(b));

        public static void Apply(ReverseMode mode, ref float sideways, ref float forward)
        {
            if (SwapsSides(mode)) sideways = -sideways;
            if (SwapsEnds(mode)) forward = -forward;
        }

        public static float BaseSeconds(RecipeTier tier) => tier switch
        {
            RecipeTier.Uncommon => 3f,
            RecipeTier.Rare => 4f,
            RecipeTier.Epic => 5f,
            _ => 0f,
        };

        public static float MaxSeconds(RecipeTier tier, IEnumerable<TraitKind> otherHitTraits)
        {
            float cap = BaseSeconds(tier);
            if (cap <= 0f) return 0f;
            if (otherHitTraits != null)
                foreach (var kind in otherHitTraits)
                    if (kind != TraitKind.Reverse) cap -= kind == TraitKind.Daze ? DazePenalty : OtherTraitPenalty;
            return Math.Max(MinHitSeconds, cap);
        }

        public static BotReversalStep BotStep(ReverseMode mode, float secondsReversed)
        {
            if (mode == ReverseMode.None) return BotReversalStep.Steady;
            if (secondsReversed < BotStumbleSeconds) return BotReversalStep.Stumble;
            return secondsReversed < BotStumbleSeconds + BotWaitSeconds ? BotReversalStep.Wait : BotReversalStep.Adapted;
        }

        public static void BotSteer(ReverseMode mode, float secondsReversed, ref float sideways, ref float forward)
        {
            switch (BotStep(mode, secondsReversed))
            {
                case BotReversalStep.Wait:
                    sideways = forward = 0f;
                    break;
                case BotReversalStep.Adapted:
                    Apply(mode, ref sideways, ref forward);
                    break;
            }
        }

        public static bool Affects(int maker, int makerTeam, int victim, int victimTeam, bool friendlyFire, bool makerToo)
        {
            if (victim == maker) return makerToo;
            return maker < 0 || friendlyFire || !Teams.AreTeammates(makerTeam, victimTeam);
        }

        public static string Effect(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => "swaps left and right",
            ReverseMode.Flip => "swaps forward and back",
            ReverseMode.Turnabout => "reverses every direction",
            _ => "",
        };

        public static string Sentence(ItemTrait trait)
        {
            if (trait == null || trait.Kind != TraitKind.Reverse || trait.Turn == ReverseMode.None) return "";
            string seconds = trait.Seconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";
            return trait.Delivery switch
            {
                TraitDelivery.WhileRaised => $"While raised, {Verb(trait.Turn, true)} {Things(trait.Turn)} within {trait.Radius.ToString("0.#", CultureInfo.InvariantCulture)} m, yours too.",
                TraitDelivery.InZone => $"{Capital(Verb(trait.Turn, true))} {Things(trait.Turn)} for others inside, and for {seconds} after.",
                TraitDelivery.BuildUp => $"Every {Ordinal(trait.Every)} hit {Verb(trait.Turn, true)} the target's {Things(trait.Turn)} for {seconds}.",
                TraitDelivery.Chance => $"{(trait.Chance * 100f).ToString("0", CultureInfo.InvariantCulture)}% of hits {Verb(trait.Turn, false)} the target's {Things(trait.Turn)} for {seconds}.",
                _ => $"A hit {Verb(trait.Turn, true)} the target's {Things(trait.Turn)} for {seconds}.",
            };
        }

        static string Verb(ReverseMode mode, bool one) => mode == ReverseMode.Turnabout ? (one ? "reverses" : "reverse") : (one ? "swaps" : "swap");

        static string Things(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => "left and right",
            ReverseMode.Flip => "forward and back",
            _ => "directions",
        };

        static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        static string Ordinal(int n) => n + (n % 100 is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

        public static string Chip(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => "Mirror",
            ReverseMode.Flip => "Flip",
            _ => "Reverse",
        };

        public static string Proc(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => "MIRRORED!",
            ReverseMode.Flip => "FLIPPED!",
            _ => "REVERSED!",
        };

        public static string HudName(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => "LEFT-RIGHT SWAPPED",
            ReverseMode.Flip => "FRONT-BACK SWAPPED",
            ReverseMode.Turnabout => "REVERSED",
            _ => "",
        };

        public static string HudText(ReverseMode mode, float secondsLeft) =>
            mode == ReverseMode.None ? "" : HudName(mode) + " " + HudSeconds(secondsLeft) + "s";

        public static int HudSeconds(float secondsLeft) => Math.Max(1, (int)Math.Ceiling(secondsLeft - 1e-4f));

        public static int Tint(ReverseMode mode) => mode switch
        {
            ReverseMode.Mirror => 0xFF9AD5,
            ReverseMode.Flip => 0x6EDCFF,
            ReverseMode.Turnabout => 0xFFD15C,
            _ => 0xFFFFFF,
        };
    }

    public sealed class ReversalClock
    {
        float sidesUntil, endsUntil, since;

        public ReverseMode ModeAt(float now) => Reversal.Of(now < sidesUntil, now < endsUntil);

        public float LeftAt(float now) => Math.Max(0f, Math.Max(sidesUntil, endsUntil) - now);

        public float ReversedFor(float now) => ModeAt(now) == ReverseMode.None ? 0f : now - since;

        public bool Apply(ReverseMode mode, float seconds, float now)
        {
            if (mode == ReverseMode.None || !(seconds > 0f) || float.IsInfinity(seconds)) return false;
            var before = ModeAt(now);
            if (Reversal.SwapsSides(mode)) sidesUntil = Math.Max(sidesUntil, now + seconds);
            if (Reversal.SwapsEnds(mode)) endsUntil = Math.Max(endsUntil, now + seconds);
            if (ModeAt(now) != before) since = now;
            return true;
        }

        public void Clear()
        {
            sidesUntil = endsUntil = since = 0f;
        }
    }
}
