using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ReversalTests
    {
        static readonly (float x, float y)[] EightWays =
        {
            (0f, 1f), (1f, 1f), (1f, 0f), (1f, -1f), (0f, -1f), (-1f, -1f), (-1f, 0f), (-1f, 1f),
        };

        static (float x, float y) Map(ReverseMode mode, float x, float y)
        {
            Reversal.Apply(mode, ref x, ref y);
            return (x, y);
        }

        [Test]
        public void EachModeMapsEveryEightWayInput()
        {
            foreach (var (x, y) in EightWays)
            {
                Assert.AreEqual((x, y), Map(ReverseMode.None, x, y), "no reversal leaves the stick alone");
                Assert.AreEqual((-x, y), Map(ReverseMode.Mirror, x, y), $"Mirror swaps left and right only ({x}, {y})");
                Assert.AreEqual((x, -y), Map(ReverseMode.Flip, x, y), $"Flip swaps forward and back only ({x}, {y})");
                Assert.AreEqual((-x, -y), Map(ReverseMode.Turnabout, x, y), $"Turnabout swaps both ({x}, {y})");
            }
            Assert.AreEqual((1f, 0f), Map(ReverseMode.Mirror, -1f, 0f), "left becomes right");
            Assert.AreEqual((-1f, 0f), Map(ReverseMode.Mirror, 1f, 0f), "right becomes left");
            Assert.AreEqual((0f, -1f), Map(ReverseMode.Flip, 0f, 1f), "forward becomes back");
            Assert.AreEqual((0f, 1f), Map(ReverseMode.Flip, 0f, -1f), "back becomes forward");
            Assert.AreEqual((0f, 0f), Map(ReverseMode.Turnabout, 0f, 0f), "standing still stays still");
        }

        [Test]
        public void MappingKeepsTheSpeedAndUndoesItself()
        {
            foreach (ReverseMode mode in Enum.GetValues(typeof(ReverseMode)))
                foreach (var (x, y) in EightWays)
                {
                    var (mx, my) = Map(mode, x * .7f, y * .3f);
                    Assert.AreEqual(Math.Sqrt(x * x * .49f + y * y * .09f), Math.Sqrt(mx * mx + my * my), 1e-6, "a reversed stick is never faster or slower, so bounds and speed caps still hold");
                    Assert.AreEqual((x * .7f, y * .3f), Map(mode, mx, my), "reversing a reversed input gives the original back");
                }
        }

        [Test]
        public void TwoModesJoinAndNeverCancelBackToNormal()
        {
            foreach (ReverseMode a in Enum.GetValues(typeof(ReverseMode)))
                foreach (ReverseMode b in Enum.GetValues(typeof(ReverseMode)))
                {
                    var both = Reversal.Combine(a, b);
                    Assert.AreEqual(Reversal.SwapsSides(a) || Reversal.SwapsSides(b), Reversal.SwapsSides(both), $"{a}+{b}");
                    Assert.AreEqual(Reversal.SwapsEnds(a) || Reversal.SwapsEnds(b), Reversal.SwapsEnds(both), $"{a}+{b}");
                    if (a != ReverseMode.None || b != ReverseMode.None) Assert.AreNotEqual(ReverseMode.None, both, "two reversals never add up to normal controls");
                }
            Assert.AreEqual(ReverseMode.Mirror, Reversal.Combine(ReverseMode.Mirror, ReverseMode.Mirror), "the same reversal twice is still that reversal, not undone");
            Assert.AreEqual(ReverseMode.Turnabout, Reversal.Combine(ReverseMode.Mirror, ReverseMode.Flip));
        }

        [Test]
        public void ASecondHitRefreshesButNeverStacks()
        {
            var clock = new ReversalClock();
            Assert.IsTrue(clock.Apply(ReverseMode.Turnabout, 3f, 10f));
            Assert.AreEqual(ReverseMode.Turnabout, clock.ModeAt(10f));
            Assert.AreEqual(3f, clock.LeftAt(10f), 1e-5f);
            Assert.IsTrue(clock.Apply(ReverseMode.Turnabout, 3f, 11f));
            Assert.AreEqual(3f, clock.LeftAt(11f), 1e-5f, "refreshed to a full 3 s, not 5 s");
            Assert.AreEqual(ReverseMode.Turnabout, clock.ModeAt(11f), "the second hit does not turn the controls back");
            Assert.IsTrue(clock.Apply(ReverseMode.Turnabout, 1f, 12f));
            Assert.AreEqual(2f, clock.LeftAt(12f), 1e-5f, "a shorter hit never cuts a longer one short");
            Assert.AreEqual(ReverseMode.Turnabout, clock.ModeAt(13.99f));
            Assert.AreEqual(ReverseMode.None, clock.ModeAt(14f), "it ends on time");
            Assert.AreEqual(0f, clock.LeftAt(14.5f));
        }

        [Test]
        public void TwoDifferentModesCoverBothAxesAndEndOnTheirOwnClocks()
        {
            var clock = new ReversalClock();
            clock.Apply(ReverseMode.Mirror, 3f, 0f);
            clock.Apply(ReverseMode.Flip, 3f, 1f);
            Assert.AreEqual(ReverseMode.Turnabout, clock.ModeAt(2f), "mirror plus flip is a full turnabout while both last");
            Assert.AreEqual(ReverseMode.Flip, clock.ModeAt(3.5f), "the mirror half ends first");
            Assert.AreEqual(ReverseMode.None, clock.ModeAt(4f));
            clock.Clear();
            Assert.AreEqual(ReverseMode.None, clock.ModeAt(0.5f), "a cleared clock ends both halves");
            Assert.AreEqual(0f, clock.LeftAt(0.5f));
        }

        [Test]
        public void NothingOrNonsenseDoesNotReverse()
        {
            var clock = new ReversalClock();
            Assert.IsFalse(clock.Apply(ReverseMode.None, 3f, 0f));
            Assert.IsFalse(clock.Apply(ReverseMode.Mirror, 0f, 0f));
            Assert.IsFalse(clock.Apply(ReverseMode.Mirror, -1f, 0f));
            Assert.IsFalse(clock.Apply(ReverseMode.Mirror, float.NaN, 0f));
            Assert.IsFalse(clock.Apply(ReverseMode.Mirror, float.PositiveInfinity, 0f), "nothing reverses forever");
            Assert.AreEqual(ReverseMode.None, clock.ModeAt(0f));
        }

        [Test]
        public void TheReversedTimerRestartsOnlyWhenTheControlsChange()
        {
            var clock = new ReversalClock();
            clock.Apply(ReverseMode.Mirror, 3f, 5f);
            Assert.AreEqual(1f, clock.ReversedFor(6f), 1e-5f);
            clock.Apply(ReverseMode.Mirror, 3f, 6f);
            Assert.AreEqual(2f, clock.ReversedFor(7f), 1e-5f, "a refresh keeps counting");
            clock.Apply(ReverseMode.Flip, 3f, 7f);
            Assert.AreEqual(0f, clock.ReversedFor(7f), 1e-5f, "a new axis is a new surprise");
            Assert.AreEqual(0f, clock.ReversedFor(20f), "not reversed: no time counted");
        }

        [Test]
        public void HarderWordsMayReverseLongerAndOtherEffectsShortenIt()
        {
            Assert.AreEqual(0f, Reversal.MaxSeconds(RecipeTier.Common, null), "plain words carry nothing");
            Assert.AreEqual(3f, Reversal.MaxSeconds(RecipeTier.Uncommon, null));
            Assert.AreEqual(4f, Reversal.MaxSeconds(RecipeTier.Rare, null));
            Assert.AreEqual(5f, Reversal.MaxSeconds(RecipeTier.Epic, null));
            Assert.AreEqual(4.5f, Reversal.MaxSeconds(RecipeTier.Epic, new[] { TraitKind.Heavy }));
            Assert.AreEqual(4f, Reversal.MaxSeconds(RecipeTier.Epic, new[] { TraitKind.Daze }), "a daze on the same item costs a whole second");
            Assert.AreEqual(3.5f, Reversal.MaxSeconds(RecipeTier.Epic, new[] { TraitKind.Heavy, TraitKind.Daze }));
            Assert.AreEqual(Reversal.MinHitSeconds, Reversal.MaxSeconds(RecipeTier.Uncommon, new[] { TraitKind.Daze, TraitKind.Bleed, TraitKind.Heavy }), "never below the floor");
            Assert.AreEqual(4f, Reversal.MaxSeconds(RecipeTier.Rare, new[] { TraitKind.Reverse }), "the reversal itself costs nothing");
        }

        [Test]
        public void ABotStumblesThenStopsThenFindsItsFeet()
        {
            Assert.AreEqual(BotReversalStep.Steady, Reversal.BotStep(ReverseMode.None, 5f));
            Assert.AreEqual(BotReversalStep.Stumble, Reversal.BotStep(ReverseMode.Mirror, 0f));
            Assert.AreEqual(BotReversalStep.Wait, Reversal.BotStep(ReverseMode.Mirror, Reversal.BotStumbleSeconds));
            Assert.AreEqual(BotReversalStep.Adapted, Reversal.BotStep(ReverseMode.Mirror, Reversal.BotStumbleSeconds + Reversal.BotWaitSeconds));
            foreach (ReverseMode mode in new[] { ReverseMode.Mirror, ReverseMode.Flip, ReverseMode.Turnabout })
                foreach (var (x, y) in EightWays)
                {
                    float Walks(float t, out float wy)
                    {
                        float sx = x, sy = y;
                        Reversal.BotSteer(mode, t, ref sx, ref sy);
                        Reversal.Apply(mode, ref sx, ref sy);
                        wy = sy;
                        return sx;
                    }
                    Assert.AreEqual(Map(mode, x, y), (Walks(.1f, out float a), a), "at first a bot stumbles the wrong way like a person");
                    Assert.AreEqual((0f, 0f), (Walks(Reversal.BotStumbleSeconds + .1f, out float b), b), "then it stops and waits instead of pushing into a wall");
                    Assert.AreEqual((x, y), (Walks(Reversal.BotStumbleSeconds + Reversal.BotWaitSeconds + .1f, out float c), c), "then it walks where it means to, so it is never stuck for the whole effect");
                }
            Assert.Less(Reversal.BotStumbleSeconds + Reversal.BotWaitSeconds, Reversal.MinHitSeconds, "a bot recovers before even the shortest reversal ends");
        }

        [Test]
        public void TheMakerAndTeammatesFollowTheTeamRules()
        {
            Assert.IsFalse(Reversal.Affects(0, 0, 0, 0, true, false), "the maker never reverses themselves");
            Assert.IsTrue(Reversal.Affects(0, 0, 0, 0, true, true), "unless the item says so");
            Assert.IsFalse(Reversal.Affects(0, 1, 2, 1, false, false), "teammates are spared when friendly fire is off");
            Assert.IsTrue(Reversal.Affects(0, 1, 2, 1, true, false), "and caught when it is on");
            Assert.IsTrue(Reversal.Affects(0, 0, 1, 1, false, false), "rivals are always caught");
            Assert.IsTrue(Reversal.Affects(-1, Teams.NoTeam, 1, 1, false, false), "a maker-less zone catches everyone");
            Assert.IsTrue(Reversal.Affects(0, Teams.NoTeam, 1, Teams.NoTeam, false, false), "no team is nobody's teammate");
        }

        [Test]
        public void TheHudSaysWhatIsSwappedAndForHowLong()
        {
            Assert.AreEqual("", Reversal.HudText(ReverseMode.None, 3f));
            Assert.AreEqual("LEFT-RIGHT SWAPPED 3s", Reversal.HudText(ReverseMode.Mirror, 2.2f));
            Assert.AreEqual("FRONT-BACK SWAPPED 1s", Reversal.HudText(ReverseMode.Flip, .05f));
            Assert.AreEqual("REVERSED 4s", Reversal.HudText(ReverseMode.Turnabout, 4f));
            var tints = new[] { ReverseMode.Mirror, ReverseMode.Flip, ReverseMode.Turnabout }.Select(Reversal.Tint).ToArray();
            Assert.AreEqual(3, tints.Distinct().Count(), "each mode has its own colour");
            CollectionAssert.AreEqual(new[] { "Mirror", "Flip", "Reverse" }, new[] { ReverseMode.Mirror, ReverseMode.Flip, ReverseMode.Turnabout }.Select(Reversal.Chip));
            foreach (ReverseMode mode in new[] { ReverseMode.Mirror, ReverseMode.Flip, ReverseMode.Turnabout })
                Assert.LessOrEqual(Reversal.HudText(mode, 9f).Length, 22, "short enough for the effects line");
        }

        [Test]
        public void TuningDescribesEachReversalInOneLine()
        {
            var tuning = HouseTuning.FromJson(@"{ ""items"": [], ""tuning"": { ""items"": {
                ""SPEAR"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Mirror"", ""seconds"": 2.5, ""every"": 3 } ] },
                ""CLOCK"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Turnabout"", ""seconds"": 1, ""inside"": true } ] },
                ""SHIELD"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 0.5, ""radius"": 3 } ] },
                ""BALL"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Turnabout"", ""seconds"": 3, ""chance"": 0.5 } ] } } } }");
            Assert.AreEqual("every 3rd hit swaps left and right for 2.5 s", tuning.Describe("SPEAR"));
            Assert.AreEqual(TraitDelivery.BuildUp, tuning.Trait("SPEAR", TraitKind.Reverse).Delivery);
            Assert.AreEqual("inside: reverses every direction, and for 1 s after", tuning.Describe("CLOCK"));
            Assert.AreEqual(TraitDelivery.InZone, tuning.Trait("CLOCK", TraitKind.Reverse).Delivery);
            Assert.AreEqual("while raised: swaps forward and back for you and everyone within 3 m", tuning.Describe("SHIELD"));
            Assert.AreEqual(TraitDelivery.WhileRaised, tuning.Trait("SHIELD", TraitKind.Reverse).Delivery);
            Assert.AreEqual("50% chance: reverses every direction for 3 s", tuning.Describe("BALL"));
            Assert.IsTrue(tuning.Trait("BALL", TraitKind.Reverse).OnHit);
            Assert.IsFalse(tuning.Trait("CLOCK", TraitKind.Reverse).OnHit, "a zone never lands through a hit");
            Assert.IsFalse(tuning.Trait("SHIELD", TraitKind.Reverse).OnHit);
        }

        [Test]
        public void ValidationRefusesReversalsTheWordHasNotEarned()
        {
            var items = TestData.Catalogue();
            var rules = TestData.LoadRuleBook();
            var bad = HouseTuning.FromJson(@"{ ""items"": [], ""tuning"": { ""items"": {
                ""BAT"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Mirror"", ""seconds"": 2 } ] },
                ""SPEAR"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Mirror"", ""seconds"": 3.5 } ] },
                ""HAMMER"": { ""traits"": [ { ""kind"": ""Daze"", ""amount"": 0.7, ""seconds"": 1 }, { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 4.5 } ] },
                ""BLADE"": { ""traits"": [ { ""kind"": ""Reverse"", ""seconds"": 2 } ] },
                ""SOFA"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 1, ""inside"": true } ] },
                ""CLOCK"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 4, ""inside"": true } ] },
                ""FAN"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 1, ""inside"": true, ""chance"": 0.5 } ] },
                ""SODA"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 0.5, ""radius"": 3 } ] },
                ""SHIELD"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 0.5, ""radius"": 9 } ] },
                ""PLATE"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Flip"", ""seconds"": 0.5, ""radius"": 2, ""inside"": true } ] },
                ""BROOM"": { ""traits"": [ { ""kind"": ""Heavy"", ""amount"": 1.2, ""radius"": 2 } ] },
                ""APPLE"": { ""traits"": [ { ""kind"": ""Reverse"", ""mode"": ""Turnabout"", ""seconds"": 2 } ] } } } }");
            var problems = bad.Validate(items, rules.Defaults);
            string all = string.Join("\n", problems);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BAT is Common")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BAT: Common words cannot reverse")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("SPEAR: Uncommon words reverse for at most 3 s")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("HAMMER: Epic words reverse for at most 4 s")), "a daze on the same item shortens it:\n" + all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BLADE: a reversal names its mode")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("SOFA: a zone reversal needs a placed zone")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("CLOCK: a zone reversal lingers")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("FAN: an area reversal is constant")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("SODA: a raised reversal needs a shield")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("SHIELD: a raised reversal reaches at most")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("PLATE: a reversal works inside a zone or around a raised shield, not both")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("BROOM: only a reversal has a mode")), all);
            Assert.IsTrue(problems.Any(p => p.StartsWith("APPLE: Reverse needs a hit to land")), all);
            Assert.IsFalse(problems.Any(p => p.StartsWith("CLOCK: Reverse needs a hit")), "a zone needs no hit:\n" + all);
            Assert.IsFalse(problems.Any(p => p.StartsWith("SHIELD: Reverse needs a hit")), "nor does a raised shield:\n" + all);
        }
    }
}
