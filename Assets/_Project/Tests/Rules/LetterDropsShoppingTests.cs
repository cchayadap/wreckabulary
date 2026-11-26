using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class LetterDropsShoppingTests
    {
        const int Seats = 4;
        const int Capacity = 10;
        const int Rounds = 300;
        const int SmashBudget = 3;

        public enum Rule { Full, Pooled, PerSeat }

        static ItemCatalogue House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static readonly ItemCatalogue Items = House();

        static HouseLayout Map(string name) => HouseLayout.FromJson(TestData.Read($"house_{name}.json"), $"house_{name}.json");

        static int Need(string word, List<char> bag, char c) => Math.Max(0, word.Count(x => x == c) - bag.Count(x => x == c));

        static bool Spells(List<char> bag, string word) => word.All(c => bag.Count(x => x == c) >= word.Count(x => x == c));

        static Dictionary<char, int> Needed(Rule rule, string[] wanted, List<char>[] bags, List<char> floor)
        {
            if (rule == Rule.Pooled)
                return LetterDrops.StillNeeded(wanted.Where(w => w != null), bags.SelectMany(b => b).Concat(floor));
            var needed = LetterDrops.StillNeeded(null, null);
            for (int s = 0; s < Seats; s++)
                LetterDrops.RequireFor(needed, wanted[s] != null ? new[] { wanted[s] } : null, bags[s]);
            return needed;
        }

        public static int[] PlayRound(string map, int seed, Rule rule, int smashesEach = int.MaxValue)
        {
            var random = new Random(seed);
            var list = ShoppingList.Draw(Items.Enabled, ShoppingList.Supply(Map(map)), Capacity, seed).Select(i => i.Word).ToList();
            var pieces = Map(map).Furniture.Select(f => f.Word).Where(LetterBag.IsWord).OrderBy(_ => random.Next()).ToList();
            var bags = Enumerable.Range(0, Seats).Select(_ => new List<char>()).ToArray();
            var done = new int[Seats];
            var floor = new List<char>();
            var spots = Enumerable.Range(0, Seats).Select(_ => new List<char>()).ToArray();
            string Wanted(int s) => done[s] < list.Count ? list[done[s]] : null;
            for (int turn = 0; pieces.Count > 0 && done.All(d => d < list.Count) && turn < smashesEach * (long)Seats; turn++)
            {
                int smasher = turn % Seats;
                string goal = Wanted(smasher);
                int at = pieces.FindIndex(p => goal != null && p.Any(c => Need(goal, bags[smasher], c) > 0));
                if (at < 0) at = 0;
                string piece = pieces[at];
                pieces.RemoveAt(at);
                var wanted = Enumerable.Range(0, Seats).Select(Wanted).ToArray();
                var drop = rule == Rule.Full ? LetterDrops.All(piece.Length)
                    : LetterDrops.Pick(piece, LetterDropTuning.Default, random, Needed(rule, wanted, bags, floor));
                string fell = LetterDrops.Letters(piece, drop);
                floor.AddRange(fell);
                spots[smasher].AddRange(fell);
                string mine = Wanted(smasher);
                if (mine == null) continue;
                foreach (char c in spots[smasher].ToList())
                    if (bags[smasher].Count < Capacity && Need(mine, bags[smasher], c) > 0)
                    {
                        bags[smasher].Add(c);
                        spots[smasher].Remove(c);
                        floor.Remove(c);
                    }
                if (!Spells(bags[smasher], mine)) continue;
                foreach (char c in mine) bags[smasher].Remove(c);
                done[smasher]++;
            }
            return done;
        }

        static (int stalls, float leader) Play(string map, Rule rule, int smashesEach = SmashBudget)
        {
            int stalls = 0;
            long best = 0;
            for (int seed = 0; seed < Rounds; seed++)
            {
                var done = PlayRound(map, seed, rule, smashesEach);
                if (done.Max() == 0) stalls++;
                best += done.Max();
            }
            return (stalls, best / (float)Rounds);
        }

        [Test]
        public void SharedListWordsReachEverySeatThatStillNeedsThem()
        {
            var bags = new[] { "P".ToList(), new List<char>(), new List<char>(), new List<char>() };
            var needed = LetterDrops.StillNeeded(null, null);
            foreach (var bag in bags) LetterDrops.RequireFor(needed, new[] { "PAN" }, bag);
            Assert.AreEqual(1, needed['P'], "three seats still need a P although one seat holds one");
            Assert.AreEqual(1, needed['A']);
            Assert.AreEqual(1, needed['N']);
            LetterDrops.RequireFor(needed, new[] { "APPLE" }, new List<char>());
            Assert.AreEqual(2, needed['P'], "the largest single-seat need wins");
            LetterDrops.RequireFor(needed, null, null);
            LetterDrops.RequireFor(null, new[] { "BAT" }, null);
        }

        [Test]
        public void FourSeatsChasingOneListGetTheirLettersAsIfEveryLetterDropped([Values("flat", "pinwheel", "courtyard", "terrace", "walkup")] string map)
        {
            var full = Play(map, Rule.Full);
            var pooled = Play(map, Rule.Pooled);
            var perSeat = Play(map, Rule.PerSeat);
            TestContext.WriteLine($"{map}: {SmashBudget} smashes a seat, rounds where nobody finishes an item: full {full.stalls}/{Rounds}, pooled {pooled.stalls}/{Rounds}, per seat {perSeat.stalls}/{Rounds}; leader items full {full.leader:0.00}, pooled {pooled.leader:0.00}, per seat {perSeat.leader:0.00}");
            Assert.Greater(pooled.stalls, full.stalls, map + ": the simulation sees the pooled guarantee starve seats");
            Assert.LessOrEqual(perSeat.stalls, full.stalls, map + ": a seat's list letters drop as reliably as with full drops");
            Assert.GreaterOrEqual(perSeat.leader, full.leader - .02f, map);
        }

        [Test]
        public void EveryHouseLetsAListBeFinishedOnceItsFurnitureIsSmashed([Values("flat", "pinwheel", "courtyard", "terrace", "walkup")] string map)
        {
            var perSeat = Play(map, Rule.PerSeat, int.MaxValue);
            Assert.AreEqual(0, perSeat.stalls, map + ": some round ended with nobody finishing an item");
            Assert.Greater(perSeat.leader, 3.9f, map + ": the leader nearly always finishes the whole list");
        }
    }
}
