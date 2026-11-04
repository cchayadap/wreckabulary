using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundEventDeckTests
    {
        const int Samples = 200000;
        const double Tolerance = 5.0;

        static WordboundDeckProfile Deck(string ruleset) => WordboundRuleset.Parse(ruleset).Deck;

        static int[] Ints(string csv) => csv.Split(',').Select(int.Parse).ToArray();

        static WordboundDirectorPlan Director(int express, int inspection) =>
            new WordboundDirectorPlan(true, 1, 0, 9000, express, inspection, Array.Empty<WordboundDirectorBias>());

        [TestCase("DeadLetter.Rush", 2700, 4200, "200,150,150,150,100,100,100,50", -1)]
        [TestCase("House.Storm", 1800, 2400, "150,50,200,150,180,100,120,50", -1)]
        [TestCase("Unspeller.LightsOut", 2700, 4200, "200,0,150,150,100,100,100,50", 2)]
        [TestCase("DeadLetter.Free", 3600, 5400, "200,0,150,0,100,100,100,50", -1)]
        public void ShiftsFollowTheScheduleTable(string ruleset, int first, int interval, string weights, int darkFromLayer)
        {
            var deck = Deck(ruleset);
            Assert.AreEqual(first, deck.FirstDrawTicks);
            Assert.AreEqual(interval, deck.IntervalTicks);
            Assert.AreEqual(900, deck.LeadTicks);
            CollectionAssert.AreEqual(Ints(weights), deck.BaseWeights.ToArray());
            Assert.AreEqual(darkFromLayer, deck.DarkFromLayer);
        }

        [TestCase("DeadLetter.Rush", "200,150,150,150,100,100,100,50,0,0")]
        [TestCase("House.Storm.Steady", "150,50,200,150,180,100,120,50,0,0")]
        [TestCase("Unspeller.LightsOut", "235,0,176,176,118,118,118,59,0,0")]
        [TestCase("DeadLetter.Free", "286,0,214,0,143,143,143,71,0,0")]
        public void OpeningOddsRedistributeDisabledCardsByLargestRemainder(string ruleset, string odds)
        {
            var shown = WordboundEventDeck.Odds(Deck(ruleset), WordboundCard.None);
            CollectionAssert.AreEqual(Ints(odds), shown.ToArray());
            Assert.AreEqual(WordboundEventDeck.OddsTotal, shown.Sum());
        }

        [Test]
        public void RemainderTiesGoToTheEarlierCard()
        {
            var shown = WordboundEventDeck.Odds(Deck("DeadLetter.Rush"), WordboundCard.Rush, 150, 100);
            CollectionAssert.AreEqual(Ints("0,141,140,140,94,94,94,47,150,100"), shown.ToArray());
        }

        [Test]
        public void NoBaseCardCanBeDrawnTwiceInARow()
        {
            foreach (var ruleset in WordboundRuleset.Standard.Where(r => !r.IsClassic))
            {
                var deck = ruleset.Deck;
                int express = deck.ExpressMaximumPermille, inspection = deck.InspectionMaximumPermille;
                var opening = WordboundEventDeck.Odds(deck, WordboundCard.None, express, inspection);
                foreach (var last in WordboundEventDeck.Cards)
                {
                    var shown = WordboundEventDeck.Odds(deck, last, express, inspection);
                    Assert.AreEqual(WordboundEventDeck.OddsTotal, shown.Sum(), ruleset.Id);
                    foreach (var card in WordboundEventDeck.Cards.Where(c => !deck.Allows(c)))
                        Assert.AreEqual(0, shown[WordboundEventDeck.Index(card)], ruleset.Id + " " + card);
                    if (WordboundEventDeck.IsDirectorCard(last))
                        CollectionAssert.AreEqual(opening.ToArray(), shown.ToArray(), ruleset.Id + " " + last);
                    else
                        Assert.AreEqual(0, shown[WordboundEventDeck.Index(last)], ruleset.Id + " " + last);
                }
            }
        }

        [TestCase("DeadLetter.Rush", WordboundCard.None, 150, 100)]
        [TestCase("House.Storm", WordboundCard.Gust, 0, 0)]
        [TestCase("Unspeller.LightsOut", WordboundCard.Rush, 75, 44)]
        [TestCase("DeadLetter.Free", WordboundCard.MailStorm, 0, 0)]
        public void TwoHundredThousandDrawsMatchTheShownOdds(string ruleset, WordboundCard last, int express, int inspection)
        {
            var odds = WordboundEventDeck.Odds(Deck(ruleset), last, express, inspection);
            var stream = WordboundStream.ForStream(0x0123456789abcdefUL, "story.deck");
            var counts = new int[WordboundEventDeck.CardCount];
            for (int i = 0; i < Samples; i++)
                counts[WordboundEventDeck.Index(WordboundEventDeck.Pick(odds, stream.Below(WordboundEventDeck.OddsTotal)))]++;
            for (int i = 0; i < WordboundEventDeck.CardCount; i++)
            {
                if (odds[i] == 0) Assert.AreEqual(0, counts[i], WordboundEventDeck.Cards[i].ToString());
                Assert.AreEqual(odds[i], counts[i] * 1000.0 / Samples, Tolerance, WordboundEventDeck.Cards[i].ToString());
            }
        }

        [Test]
        public void ChainedDrawsNeverRepeatAndMatchTheAverageShownOdds()
        {
            var deck = Deck("DeadLetter.Rush");
            var director = Director(150, 100);
            var stream = WordboundStream.ForStream(0xfeedfacecafebeefUL, "story.deck");
            var counts = new long[WordboundEventDeck.CardCount];
            var shown = new long[WordboundEventDeck.CardCount];
            var last = WordboundCard.None;
            for (int i = 0; i < Samples; i++)
            {
                var draw = WordboundEventDeck.Draw(deck, i, last, director, ref stream);
                if (draw.Card == last && !WordboundEventDeck.IsDirectorCard(last)) Assert.Fail($"{last} repeated at draw {i}");
                for (int c = 0; c < WordboundEventDeck.CardCount; c++) shown[c] += draw.OddsPermille[c];
                counts[WordboundEventDeck.Index(draw.Card)]++;
                int target = draw.Card == WordboundCard.ExpressChute ? 1 : draw.Card == WordboundCard.InspectionDelay ? 0 : -1;
                Assert.AreEqual(target, draw.TargetTeam);
                Assert.IsTrue(draw.Card == WordboundCard.Gust ? draw.Param >= 0 && draw.Param < 4 : draw.Param == 0);
                last = draw.Card;
            }
            for (int c = 0; c < WordboundEventDeck.CardCount; c++)
                Assert.AreEqual(shown[c] / (double)Samples, counts[c] * 1000.0 / Samples, Tolerance,
                    WordboundEventDeck.Cards[c].ToString());
        }

        [Test]
        public void PickWalksCumulativeOddsInEnumOrder()
        {
            var plain = WordboundEventDeck.Odds(Deck("DeadLetter.Rush"), WordboundCard.None);
            Assert.AreEqual(WordboundCard.Rush, WordboundEventDeck.Pick(plain, 0));
            Assert.AreEqual(WordboundCard.Rush, WordboundEventDeck.Pick(plain, 199));
            Assert.AreEqual(WordboundCard.LightsOut, WordboundEventDeck.Pick(plain, 200));
            Assert.AreEqual(WordboundCard.Gust, WordboundEventDeck.Pick(plain, 350));
            Assert.AreEqual(WordboundCard.Sweep, WordboundEventDeck.Pick(plain, 649));
            Assert.AreEqual(WordboundCard.MailStorm, WordboundEventDeck.Pick(plain, 650));
            Assert.AreEqual(WordboundCard.Spill, WordboundEventDeck.Pick(plain, 949));
            Assert.AreEqual(WordboundCard.GoldenWord, WordboundEventDeck.Pick(plain, 999));
            var biased = WordboundEventDeck.Odds(Deck("DeadLetter.Rush"), WordboundCard.Rush, 150, 100);
            Assert.AreEqual(WordboundCard.LightsOut, WordboundEventDeck.Pick(biased, 0));
            Assert.AreEqual(WordboundCard.GoldenWord, WordboundEventDeck.Pick(biased, 749));
            Assert.AreEqual(WordboundCard.ExpressChute, WordboundEventDeck.Pick(biased, 750));
            Assert.AreEqual(WordboundCard.ExpressChute, WordboundEventDeck.Pick(biased, 899));
            Assert.AreEqual(WordboundCard.InspectionDelay, WordboundEventDeck.Pick(biased, 900));
            Assert.AreEqual(WordboundCard.InspectionDelay, WordboundEventDeck.Pick(biased, 999));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Pick(plain, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Pick(plain, 1000));
            Assert.Throws<ArgumentException>(() => WordboundEventDeck.Pick(new[] { 999, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0));
        }

        [TestCase("DeadLetter.Rush", 2700, 4200)]
        [TestCase("House.Storm", 1800, 2400)]
        [TestCase("Unspeller.LightsOut.Steady", 2700, 4200)]
        [TestCase("House.Free", 3600, 5400)]
        public void ForecastsRevealAtTheDrawTickAndStartOneLeadLater(string ruleset, int first, int interval)
        {
            var deck = Deck(ruleset);
            var stream = WordboundStream.ForStream(42UL, "story.deck");
            var last = WordboundCard.None;
            for (int draws = 0; draws < 50; draws++)
            {
                var draw = WordboundEventDeck.Draw(deck, draws, last, null, ref stream);
                Assert.AreEqual(first + (long)draws * interval, draw.RevealTick);
                Assert.AreEqual(draw.RevealTick, WordboundEventDeck.NextDrawTick(deck, draws));
                Assert.AreEqual(draw.RevealTick + 900, draw.StartTick);
                Assert.AreEqual(draw.StartTick + WordboundEventDeck.DurationTicks(draw.Card), draw.EndTick);
                Assert.Less(draw.StartTick, WordboundEventDeck.NextDrawTick(deck, draws + 1));
                Assert.IsFalse(WordboundEventDeck.IsDirectorCard(draw.Card));
                Assert.AreEqual(-1, draw.TargetTeam);
                last = draw.Card;
            }
        }

        [Test]
        public void RushShiftForecastsAtFortyFiveSecondsAndStartsAtSixty()
        {
            var stream = WordboundStream.ForStream(1UL, "story.deck");
            var draw = WordboundEventDeck.Draw(Deck("DeadLetter.Rush"), 0, WordboundCard.None, null, ref stream);
            Assert.AreEqual(45 * ModeDefinition.TickRate, draw.RevealTick);
            Assert.AreEqual(60 * ModeDefinition.TickRate, draw.StartTick);
        }

        [Test]
        public void DrawsThroughCountsEveryDrawTickUpToTheTick()
        {
            var deck = Deck("DeadLetter.Rush");
            Assert.AreEqual(0, WordboundEventDeck.DrawsThrough(deck, 0));
            Assert.AreEqual(0, WordboundEventDeck.DrawsThrough(deck, 2699));
            Assert.AreEqual(1, WordboundEventDeck.DrawsThrough(deck, 2700));
            Assert.AreEqual(1, WordboundEventDeck.DrawsThrough(deck, 6899));
            Assert.AreEqual(2, WordboundEventDeck.DrawsThrough(deck, 6900));
            foreach (var ruleset in WordboundRuleset.Standard.Where(r => !r.IsClassic))
                for (long tick = 0; tick < 100000; tick += 37)
                {
                    int draws = WordboundEventDeck.DrawsThrough(ruleset.Deck, tick);
                    Assert.Greater(WordboundEventDeck.NextDrawTick(ruleset.Deck, draws), tick);
                    if (draws > 0) Assert.LessOrEqual(WordboundEventDeck.NextDrawTick(ruleset.Deck, draws - 1), tick);
                }
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.NextDrawTick(deck, -1));
        }

        [TestCase(WordboundCard.Rush, 1200)]
        [TestCase(WordboundCard.LightsOut, 1800)]
        [TestCase(WordboundCard.Gust, 900)]
        [TestCase(WordboundCard.Sweep, 0)]
        [TestCase(WordboundCard.MailStorm, 1200)]
        [TestCase(WordboundCard.LostPet, 2400)]
        [TestCase(WordboundCard.Spill, 1200)]
        [TestCase(WordboundCard.GoldenWord, 2700)]
        [TestCase(WordboundCard.ExpressChute, 1800)]
        [TestCase(WordboundCard.InspectionDelay, 3600)]
        public void CardDurationsFollowTheCardTable(WordboundCard card, int ticks)
        {
            Assert.AreEqual(ticks, WordboundEventDeck.DurationTicks(card));
        }

        [Test]
        public void DrawRollsTheCardThenTheGustDirection()
        {
            var deck = Deck("House.Storm");
            var stream = WordboundStream.ForStream(77UL, "story.deck");
            var last = WordboundCard.None;
            int gusts = 0;
            for (int i = 0; i < 2000; i++)
            {
                var replay = stream;
                var draw = WordboundEventDeck.Draw(deck, i, last, null, ref stream);
                int roll = replay.Below(1000);
                var card = WordboundEventDeck.Pick(WordboundEventDeck.Odds(deck, last), roll);
                int param = card == WordboundCard.Gust ? replay.Below(WordboundEventDeck.GustDirections) : 0;
                Assert.AreEqual(roll, draw.Roll);
                Assert.AreEqual(card, draw.Card);
                Assert.AreEqual(param, draw.Param);
                Assert.IsTrue(replay.State == stream.State);
                if (card == WordboundCard.Gust) gusts++;
                last = draw.Card;
            }
            Assert.Greater(gusts, 100);
        }

        [Test]
        public void OldestActiveEventEndsWhenTwoAreRunning()
        {
            Assert.AreEqual(-1, WordboundEventDeck.OldestToEnd(Array.Empty<long>()));
            Assert.AreEqual(-1, WordboundEventDeck.OldestToEnd(new[] { 3600L }));
            Assert.AreEqual(0, WordboundEventDeck.OldestToEnd(new[] { 3600L, 7800L }));
            Assert.AreEqual(1, WordboundEventDeck.OldestToEnd(new[] { 7800L, 3600L }));
            Assert.AreEqual(0, WordboundEventDeck.OldestToEnd(new[] { 3600L, 3600L }));
        }

        [Test]
        public void ClassicHasNoDeckToDraw()
        {
            var classic = WordboundRuleset.Classic.Deck;
            var stream = WordboundStream.ForStream(1UL, "story.deck");
            Assert.Throws<InvalidOperationException>(() => WordboundEventDeck.Odds(classic, WordboundCard.None));
            Assert.Throws<InvalidOperationException>(() => WordboundEventDeck.NextDrawTick(classic, 0));
            Assert.Throws<InvalidOperationException>(() => WordboundEventDeck.Draw(classic, 0, WordboundCard.None, null, ref stream));
            Assert.IsFalse(WordboundEventDeck.Cards.Any(classic.Allows));
        }

        [Test]
        public void DirectorShareStaysWithinTheRulesetCaps()
        {
            var director = Deck("DeadLetter.Rush");
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Odds(director, WordboundCard.None, 151, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Odds(director, WordboundCard.None, 0, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Odds(director, WordboundCard.None, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Odds(Deck("DeadLetter.Rush.Steady"), WordboundCard.None, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WordboundEventDeck.Odds(Deck("DeadLetter.Free"), WordboundCard.None, 0, 1));
            Assert.AreEqual(150, director.ExpressMaximumPermille);
            Assert.AreEqual(100, director.InspectionMaximumPermille);
            Assert.AreEqual(4, director.ExpressPerMatch);
            Assert.AreEqual(3, director.InspectionPerMatch);
        }

        [Test]
        public void ShiftsAllowOnlyTheirCards()
        {
            var free = Deck("Unspeller.Free");
            Assert.IsFalse(free.Allows(WordboundCard.Sweep));
            Assert.IsFalse(free.Allows(WordboundCard.LightsOut));
            Assert.IsFalse(free.Allows(WordboundCard.ExpressChute));
            Assert.IsTrue(free.Allows(WordboundCard.Rush));
            Assert.IsTrue(free.JobBoard);
            Assert.IsFalse(Deck("House.LightsOut").Allows(WordboundCard.LightsOut));
            Assert.IsTrue(Deck("House.Storm").Allows(WordboundCard.LightsOut));
            Assert.IsFalse(Deck("House.Storm.Steady").Allows(WordboundCard.InspectionDelay));
            Assert.IsFalse(Deck("House.Storm").Allows(WordboundCard.None));
            Assert.IsTrue(WordboundEventDeck.Cards.Where(c => !WordboundEventDeck.IsDirectorCard(c)).All(Deck("House.Storm").Allows));
        }
    }
}
