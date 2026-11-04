using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum WordboundCard
    {
        None, Rush, LightsOut, Gust, Sweep, MailStorm, LostPet, Spill, GoldenWord, ExpressChute, InspectionDelay
    }

    public sealed class WordboundDeckProfile
    {
        public const int ForecastLeadTicks = 900;
        public const int PermanentDarkLayer = 2;
        public const int NoDarkLayer = -1;

        static readonly int[] rushWeights = { 200, 150, 150, 150, 100, 100, 100, 50 };
        static readonly int[] stormWeights = { 150, 50, 200, 150, 180, 100, 120, 50 };
        static readonly int[] lightsOutWeights = { 200, 0, 150, 150, 100, 100, 100, 50 };
        static readonly int[] freeWeights = { 200, 0, 150, 0, 100, 100, 100, 50 };

        public static WordboundDeckProfile None { get; } =
            new WordboundDeckProfile(0, 0, 0, new int[WordboundEventDeck.BaseCardCount], false, NoDarkLayer, false);

        public int FirstDrawTicks { get; }
        public int IntervalTicks { get; }
        public int LeadTicks { get; }
        public IReadOnlyList<int> BaseWeights { get; }
        public int ExpressMaximumPermille { get; }
        public int InspectionMaximumPermille { get; }
        public int ExpressPerMatch { get; }
        public int InspectionPerMatch { get; }
        public int DarkFromLayer { get; }
        public bool JobBoard { get; }
        public bool Enabled => IntervalTicks > 0;
        internal string Canonical { get; }

        WordboundDeckProfile(int first, int interval, int lead, int[] weights, bool director, int darkFromLayer, bool jobBoard)
        {
            FirstDrawTicks = first; IntervalTicks = interval; LeadTicks = lead;
            BaseWeights = Array.AsReadOnly((int[])weights.Clone());
            ExpressMaximumPermille = director ? WordboundRaceDirector.TrailMaximumPermille : 0;
            InspectionMaximumPermille = director ? WordboundRaceDirector.LeadMaximumPermille : 0;
            ExpressPerMatch = director ? WordboundRaceDirector.ExpressPerMatch : 0;
            InspectionPerMatch = director ? WordboundRaceDirector.InspectionPerMatch : 0;
            DarkFromLayer = darkFromLayer; JobBoard = jobBoard;
            Canonical = string.Join("|", new object[] { "wordbound.deck", WordboundEventDeck.Version, first, interval, lead,
                string.Join(",", BaseWeights), ExpressMaximumPermille, InspectionMaximumPermille, ExpressPerMatch,
                InspectionPerMatch, darkFromLayer, jobBoard,
                string.Join(",", WordboundEventDeck.Cards.Select(WordboundEventDeck.DurationTicks)),
                WordboundEventDeck.GustDirections }
                .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        public static WordboundDeckProfile For(WordboundShift shift, bool director) => shift switch
        {
            WordboundShift.Rush => new WordboundDeckProfile(2700, 4200, ForecastLeadTicks, rushWeights, director, NoDarkLayer, false),
            WordboundShift.Storm => new WordboundDeckProfile(1800, 2400, ForecastLeadTicks, stormWeights, director, NoDarkLayer, false),
            WordboundShift.LightsOut => new WordboundDeckProfile(2700, 4200, ForecastLeadTicks, lightsOutWeights, director,
                PermanentDarkLayer, false),
            WordboundShift.Free => new WordboundDeckProfile(3600, 5400, ForecastLeadTicks, freeWeights, false, NoDarkLayer, true),
            _ => throw new ArgumentOutOfRangeException(nameof(shift))
        };

        public int Weight(WordboundCard card) => card switch
        {
            WordboundCard.ExpressChute => ExpressMaximumPermille,
            WordboundCard.InspectionDelay => InspectionMaximumPermille,
            _ => BaseWeights[WordboundEventDeck.Index(card)]
        };

        public bool Allows(WordboundCard card) => card != WordboundCard.None && Weight(card) > 0;
    }

    public sealed class WordboundDeckDraw
    {
        public WordboundCard Card { get; }
        public int TargetTeam { get; }
        public long RevealTick { get; }
        public long StartTick { get; }
        public long EndTick { get; }
        public IReadOnlyList<int> OddsPermille { get; }
        public int Roll { get; }
        public int Param { get; }

        public WordboundDeckDraw(WordboundCard card, int targetTeam, long revealTick, long startTick, long endTick,
            IReadOnlyList<int> oddsPermille, int roll, int param)
        {
            Card = card; TargetTeam = targetTeam; RevealTick = revealTick; StartTick = startTick; EndTick = endTick;
            OddsPermille = oddsPermille; Roll = roll; Param = param;
        }
    }

    public static class WordboundEventDeck
    {
        public const int Version = 1;
        public const int OddsTotal = 1000;
        public const int BaseCardCount = 8;
        public const int CardCount = 10;
        public const int MaximumActive = 2;
        public const int GustDirections = 4;
        public const int InspectionWindowTicks = 3600;
        public const int GlobalTarget = -1;

        public static IReadOnlyList<WordboundCard> Cards { get; } =
            Array.AsReadOnly(Enumerable.Range(1, CardCount).Select(i => (WordboundCard)i).ToArray());

        public static int Index(WordboundCard card)
        {
            if (card <= WordboundCard.None || card > WordboundCard.InspectionDelay)
                throw new ArgumentOutOfRangeException(nameof(card));
            return (int)card - 1;
        }

        public static bool IsDirectorCard(WordboundCard card) =>
            card == WordboundCard.ExpressChute || card == WordboundCard.InspectionDelay;

        public static int DurationTicks(WordboundCard card) => card switch
        {
            WordboundCard.Rush => 1200,
            WordboundCard.LightsOut => 1800,
            WordboundCard.Gust => 900,
            WordboundCard.Sweep => 0,
            WordboundCard.MailStorm => 1200,
            WordboundCard.LostPet => 2400,
            WordboundCard.Spill => 1200,
            WordboundCard.GoldenWord => 2700,
            WordboundCard.ExpressChute => 1800,
            WordboundCard.InspectionDelay => InspectionWindowTicks,
            _ => throw new ArgumentOutOfRangeException(nameof(card))
        };

        public static long NextDrawTick(WordboundDeckProfile profile, int draws)
        {
            Require(profile);
            if (draws < 0) throw new ArgumentOutOfRangeException(nameof(draws));
            return profile.FirstDrawTicks + (long)draws * profile.IntervalTicks;
        }

        public static int DrawsThrough(WordboundDeckProfile profile, long tick)
        {
            Require(profile);
            return tick < profile.FirstDrawTicks ? 0 : checked((int)((tick - profile.FirstDrawTicks) / profile.IntervalTicks + 1));
        }

        public static IReadOnlyList<int> Odds(WordboundDeckProfile profile, WordboundCard lastCard,
            int expressPermille = 0, int inspectionPermille = 0)
        {
            Require(profile);
            if (expressPermille < 0 || expressPermille > profile.ExpressMaximumPermille)
                throw new ArgumentOutOfRangeException(nameof(expressPermille));
            if (inspectionPermille < 0 || inspectionPermille > profile.InspectionMaximumPermille)
                throw new ArgumentOutOfRangeException(nameof(inspectionPermille));
            var weights = profile.BaseWeights.ToArray();
            if (lastCard != WordboundCard.None && !IsDirectorCard(lastCard)) weights[Index(lastCard)] = 0;
            long total = weights.Sum();
            if (total <= 0) throw new InvalidOperationException("The deck has no card left to draw.");
            int share = OddsTotal - expressPermille - inspectionPermille;
            var odds = new int[CardCount];
            var remainders = new long[BaseCardCount];
            int assigned = 0;
            for (int i = 0; i < BaseCardCount; i++)
            {
                long scaled = weights[i] * (long)share;
                odds[i] = (int)(scaled / total);
                remainders[i] = scaled % total;
                assigned += odds[i];
            }
            foreach (int i in Enumerable.Range(0, BaseCardCount).OrderByDescending(i => remainders[i]).ThenBy(i => i)
                .Take(share - assigned))
                odds[i]++;
            odds[Index(WordboundCard.ExpressChute)] = expressPermille;
            odds[Index(WordboundCard.InspectionDelay)] = inspectionPermille;
            return Array.AsReadOnly(odds);
        }

        public static WordboundCard Pick(IReadOnlyList<int> odds, int roll)
        {
            if (odds == null || odds.Count != CardCount || odds.Any(o => o < 0) || odds.Sum() != OddsTotal)
                throw new ArgumentException("Odds need ten non-negative entries summing to 1000.", nameof(odds));
            if (roll < 0 || roll >= OddsTotal) throw new ArgumentOutOfRangeException(nameof(roll));
            int cumulative = 0;
            for (int i = 0; i < CardCount; i++)
            {
                cumulative += odds[i];
                if (roll < cumulative) return Cards[i];
            }
            throw new InvalidOperationException("Odds did not cover the roll.");
        }

        public static WordboundDeckDraw Draw(WordboundDeckProfile profile, int draws, WordboundCard lastCard,
            WordboundDirectorPlan director, ref WordboundStream deck)
        {
            director ??= WordboundDirectorPlan.Off;
            var odds = Odds(profile, lastCard, director.ExpressPermille, director.InspectionPermille);
            long reveal = NextDrawTick(profile, draws);
            long start = reveal + profile.LeadTicks;
            int roll = deck.Below(OddsTotal);
            var card = Pick(odds, roll);
            int param = card == WordboundCard.Gust ? deck.Below(GustDirections) : 0;
            int target = card == WordboundCard.ExpressChute ? director.TrailTeam
                : card == WordboundCard.InspectionDelay ? director.LeadTeam : GlobalTarget;
            return new WordboundDeckDraw(card, target, reveal, start, start + DurationTicks(card), odds, roll, param);
        }

        public static int OldestToEnd(IReadOnlyList<long> activeStartTicks)
        {
            if (activeStartTicks == null) throw new ArgumentNullException(nameof(activeStartTicks));
            if (activeStartTicks.Count < MaximumActive) return -1;
            int oldest = 0;
            for (int i = 1; i < activeStartTicks.Count; i++)
                if (activeStartTicks[i] < activeStartTicks[oldest]) oldest = i;
            return oldest;
        }

        static void Require(WordboundDeckProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (!profile.Enabled) throw new InvalidOperationException("Classic rulesets have no event deck.");
        }
    }
}
