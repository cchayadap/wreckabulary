using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class LetterDropsTests
    {
        const int Seeds = 400;
        static readonly string[] Maps = { "house_pinwheel.json", "house_courtyard.json", "house_flat.json", "house_terrace.json", "house_walkup.json" };
        static readonly string[] PlayedModes =
        {
            HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.MovingOut, HouseModes.MovingDay,
            HouseModes.Escape, HouseModes.ShoppingList, HouseModes.RiddleRush, HouseModes.BossFight,
        };

        static HouseLayout Map(string file) => HouseLayout.FromJson(TestData.Read(file), file);

        static IEnumerable<string> FurnitureWords() => Maps.SelectMany(m => Map(m).Furniture.Select(f => f.Word)).Distinct();

        static LetterDropTuning Shipped => HouseTuning.FromJson(TestData.Read("items_house.json")).Drops;

        static int[] Pick(string word, int seed, IReadOnlyDictionary<char, int> needed = null) =>
            LetterDrops.Pick(word, LetterDropTuning.Default, new Random(seed), needed);

        static Dictionary<char, int> Need(string letters)
        {
            var need = new Dictionary<char, int>();
            LetterDrops.Require(need, letters);
            return need;
        }

        static bool Holds(string have, string word) => LetterBag.FromWord(have).Contains(LetterBag.FromWord(word));

        [Test]
        public void TheOwnersExamplesSetTheBounds()
        {
            var tuning = LetterDropTuning.Default;
            Assert.AreEqual(3, tuning.FewestFor(5), "TABLE drops 3 to 5");
            Assert.AreEqual(2, tuning.FewestFor(3), "BED drops 2 to 3");
            Assert.AreEqual(5, tuning.FewestFor(7), "CABINET drops 5 to 7");
            Assert.AreEqual(3, tuning.FewestFor(4), "LAMP drops 3 or 4");
            Assert.AreEqual(4, tuning.FewestFor(6));
            Assert.AreEqual(2, tuning.FewestFor(2), "a two-letter word drops whole");
            Assert.AreEqual(1, tuning.FewestFor(1), "a single letter always drops");
            Assert.AreEqual(0, tuning.FewestFor(0));
            foreach (int length in Enumerable.Range(3, 10))
                Assert.AreEqual(Math.Max(2, (int)Math.Ceiling(length * 0.6m)), tuning.FewestFor(length), length + " letters");
        }

        [Test]
        public void ShippedTuningIsTheDefaultAndOnlyHousePlayDropsPart()
        {
            var tuning = Shipped;
            Assert.AreEqual(LetterDropTuning.DefaultShare, tuning.Share);
            Assert.AreEqual(LetterDropTuning.DefaultFewest, tuning.Fewest);
            CollectionAssert.AreEqual(new[] { HouseModes.Tutorial, HouseModes.ShoppingList }, tuning.WholeIn.ToArray());
            CollectionAssert.IsEmpty(tuning.Validate());
            foreach (string mode in PlayedModes.Where(m => m != HouseModes.ShoppingList)) Assert.IsTrue(tuning.Partial(mode), mode + " drops part of a word");
            foreach (string mode in new[] { HouseModes.Tutorial, HouseModes.ShoppingList, "Hub", "Default", "TimeRun", "Freestyle", "TeamRace", "BrawlRace", "", null })
                Assert.IsFalse(tuning.Partial(mode), (mode ?? "null") + " keeps every letter");
            Assert.IsFalse(HouseTuning.FromJson("{}").Drops.Partial(HouseModes.Tutorial), "without House tuning the Tutorial still drops in full");
        }

        [Test]
        public void SharedDataStaysByteIdentical()
        {
            string Sha(string file)
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return string.Concat(sha.ComputeHash(File.ReadAllBytes(Path.Combine(TestData.Root, "Assets/_Project/Data/Config", file))).Select(b => b.ToString("x2")));
            }
            StringAssert.StartsWith("79363c8f", Sha("items.json"));
            StringAssert.StartsWith("f183d9b7", Sha("rules.json"));
        }

        [Test]
        public void BadTuningIsRefused()
        {
            HouseTuning Parse(string drops) => HouseTuning.FromJson(@"{ ""tuning"": { ""drops"": " + drops + " } }");
            Assert.Throws<FormatException>(() => Parse(@"{ ""share"": 0 }"));
            Assert.Throws<FormatException>(() => Parse(@"{ ""share"": 1.5 }"));
            Assert.Throws<FormatException>(() => Parse(@"{ ""share"": -0.2 }"));
            Assert.Throws<FormatException>(() => Parse(@"{ ""fewest"": 0 }"));
            Assert.Throws<FormatException>(() => Parse(@"{ ""wholeIn"": [""Tutorial"", ""Picnic""] }"));
            Assert.Throws<FormatException>(() => Parse(@"{ ""wholeIn"": [""Escape""] }"), "the Tutorial always keeps every letter");
            Assert.Throws<FormatException>(() => Parse(@"{ ""chance"": 0.5 }"));
            Assert.Throws<ArgumentException>(() => LetterDropTuning.Of(2f, 2, HouseModes.Tutorial));
            var custom = Parse(@"{ ""share"": 1, ""fewest"": 3, ""wholeIn"": [""Tutorial"", ""Escape""] }").Drops;
            Assert.AreEqual(5, custom.FewestFor(5), "share 1 brings back full drops");
            Assert.IsFalse(custom.Partial(HouseModes.Escape));
            Assert.AreEqual(3, LetterDropTuning.Of(.1f, 3, HouseModes.Tutorial).FewestFor(9), "fewest is a floor");
            Assert.AreEqual(LetterDropTuning.DefaultShare, Parse(@"{ ""note"": ""defaults"" }").Drops.Share);
        }

        [Test]
        public void EveryFurnitureWordInTheFiveHousesDropsWithinItsBoundsFromItsOwnLetters()
        {
            var words = FurnitureWords().ToList();
            Assert.Greater(words.Count, 20, "the five houses are furnished");
            foreach (string word in words)
            {
                int fewest = LetterDropTuning.Default.FewestFor(word.Length);
                var counts = new HashSet<int>();
                for (int seed = 0; seed < Seeds; seed++)
                {
                    var picked = Pick(word, seed);
                    counts.Add(picked.Length);
                    Assert.That(picked.Length, Is.InRange(fewest, word.Length), word);
                    Assert.AreEqual(picked.Length, picked.Distinct().Count(), word + " never drops one block twice");
                    CollectionAssert.IsOrdered(picked, word);
                    Assert.IsTrue(picked.All(i => i >= 0 && i < word.Length), word);
                    string letters = LetterDrops.Letters(word, picked);
                    Assert.IsTrue(Holds(word, letters), $"{word} dropped '{letters}', which is not from its own letters");
                }
                CollectionAssert.AreEquivalent(Enumerable.Range(fewest, word.Length - fewest + 1), counts, word + " reaches every count between its bounds");
            }
        }

        [Test]
        public void ALetterThatAppearsTwiceDropsAtMostTwice()
        {
            foreach (string word in new[] { "BARREL", "COFFEE", "BOOKSHELF", "PILLOW", "DRESSER" })
                for (int seed = 0; seed < Seeds; seed++)
                {
                    string letters = LetterDrops.Letters(word, Pick(word, seed));
                    foreach (char c in letters.Distinct())
                        Assert.LessOrEqual(letters.Count(x => x == c), word.Count(x => x == c), word);
                }
        }

        [Test]
        public void CountsAndLettersAreUniform()
        {
            const int Trials = 6000;
            var byCount = new int[6];
            var byIndex = new int[5];
            var random = new Random(12345);
            for (int trial = 0; trial < Trials; trial++)
            {
                var picked = LetterDrops.Pick("TABLE", LetterDropTuning.Default, random);
                byCount[picked.Length]++;
                foreach (int i in picked) byIndex[i]++;
            }
            for (int n = 3; n <= 5; n++) Assert.That(byCount[n] / (float)Trials, Is.EqualTo(1f / 3f).Within(.04f), n + " letters");
            float each = (3f + 4f + 5f) / 3f / 5f;
            for (int i = 0; i < 5; i++) Assert.That(byIndex[i] / (float)Trials, Is.EqualTo(each).Within(.04f), "TABLE"[i] + " drops as often as the others");
        }

        [Test]
        public void ASeedAlwaysDropsTheSameLetters()
        {
            foreach (string word in new[] { "TABLE", "CABINET", "BED", "BOOKSHELF" })
                for (int seed = 0; seed < 50; seed++)
                    CollectionAssert.AreEqual(Pick(word, seed), Pick(word, seed), word);
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++) seen.Add(LetterDrops.Letters("CABINET", Pick("CABINET", seed)));
            Assert.Greater(seen.Count, 10, "different seeds give different drops");
            Assert.Throws<ArgumentNullException>(() => LetterDrops.Pick("TABLE", LetterDropTuning.Default, null));
            CollectionAssert.IsEmpty(Pick("", 1));
            CollectionAssert.IsEmpty(Pick(null, 1));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, LetterDrops.All(3));
        }

        [Test]
        public void LettersAGoalStillNeedsAlwaysDropFirst()
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                var bat = LetterDrops.Letters("TABLE", Pick("TABLE", seed, Need("BAT")));
                Assert.IsTrue(Holds(bat, "BAT"), $"TABLE dropped '{bat}' while BAT was the goal");
                Assert.That(bat.Length, Is.InRange(3, 5));

                var all = Pick("TABLE", seed, Need("TABLE"));
                Assert.AreEqual(5, all.Length, "a goal that needs every letter takes them all");

                var rr = LetterDrops.Letters("BARREL", Pick("BARREL", seed, Need("RR")));
                Assert.AreEqual(2, rr.Count(c => c == 'R'), "both Rs drop when the goal needs both");

                var foreign = Pick("BED", seed, Need("XYZ"));
                Assert.That(foreign.Length, Is.InRange(2, 3), "goal letters the word lacks change nothing");

                var lower = LetterDrops.Letters("TABLE", Pick("TABLE", seed, new Dictionary<char, int> { ['b'] = 1, ['t'] = 1 }));
                Assert.IsTrue(Holds(lower, "BT"), "goal letters are matched without case");
            }
        }

        [Test]
        public void AGoalOnlyNeedsWhatNobodyHoldsYet()
        {
            var need = LetterDrops.StillNeeded(new[] { "BAT", "BAT", "MAT" }, "AB".ToCharArray());
            Assert.AreEqual(3, need.Count, "one BAT and one MAT, less the A and B in hand");
            Assert.AreEqual(2, need['T']);
            Assert.AreEqual(1, need['M']);
            Assert.AreEqual(1, need['A']);
            Assert.IsFalse(need.ContainsKey('B'));
            CollectionAssert.IsEmpty(LetterDrops.StillNeeded(null, null));
            CollectionAssert.IsEmpty(LetterDrops.StillNeeded(new[] { "BAT" }, "TAB".ToCharArray()));
            var lower = LetterDrops.StillNeeded(new[] { "bat", null, "" }, null);
            Assert.AreEqual(3, lower.Count);

            var required = LetterDrops.StillNeeded(new[] { "BAT" }, "B".ToCharArray());
            LetterDrops.Require(required, "TT");
            Assert.AreEqual(2, required['T'], "a piece's own must-drop letters stack with the mode's goal by the larger count");
            Assert.AreEqual(1, required['A']);
            Assert.IsFalse(required.ContainsKey('B'));
            LetterDrops.Require(required, null);
            LetterDrops.Require(null, "X");
        }

        [Test]
        public void EscapeDoorWordsCanAlwaysBeSpelledFromTheStockedFurniture()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            var doorWords = items.Enabled.Select(i => i.Id).Where(id => id.Length >= 3 && id.Length <= 5).Distinct().ToList();
            var furniture = FurnitureWords().ToList();
            Assert.Greater(doorWords.Count, 20);
            int checkedDoors = 0;
            foreach (string door in doorWords)
            {
                var stock = Stock(door, furniture);
                for (int seed = 0; seed < 25; seed++)
                {
                    var random = new Random(seed * 7919 + checkedDoors);
                    string have = "";
                    foreach (string prop in stock.OrderBy(_ => random.Next()))
                    {
                        var need = LetterDrops.StillNeeded(new[] { door }, have.ToCharArray());
                        have += LetterDrops.Letters(prop, LetterDrops.Pick(prop, LetterDropTuning.Default, random, need));
                    }
                    Assert.IsTrue(Holds(have, door), $"door {door} stocked with {string.Join(",", stock)} left only '{have}'");
                }
                checkedDoors++;
            }
            Assert.AreEqual(doorWords.Count, checkedDoors);
        }

        static List<string> Stock(string door, List<string> furniture)
        {
            var left = door.ToList();
            var stock = new List<string>();
            while (left.Count > 0)
            {
                string best = furniture.OrderByDescending(f => Overlap(f, left)).ThenBy(f => f, StringComparer.Ordinal).First();
                if (Overlap(best, left) == 0) best = left[0].ToString();
                foreach (char c in best) left.Remove(c);
                stock.Add(best);
            }
            return stock;
        }

        static int Overlap(string word, List<char> left)
        {
            var spare = left.ToList();
            return word.Count(c => spare.Remove(c));
        }

        [Test]
        public void ARoundStillHasEnoughLettersBecauseDeliveriesTopUpWhatIsInPlay()
        {
            var tuning = HouseTuning.FromJson(TestData.Read("items_house.json"));
            foreach (string file in Maps)
            {
                var house = Map(file);
                int full = house.Furniture.Sum(f => f.Word.Length);
                long partial = 0;
                for (int seed = 0; seed < 50; seed++)
                {
                    var random = new Random(seed);
                    foreach (var f in house.Furniture) partial += LetterDrops.Pick(f.Word, tuning.Drops, random).Length;
                }
                float mean = partial / 50f;
                float expected = house.Furniture.Sum(f => (tuning.Drops.FewestFor(f.Word.Length) + f.Word.Length) / 2f);
                Assert.That(mean, Is.EqualTo(expected).Within(expected * .03f), house.Name + " drops about the midpoint of every piece");
                Assert.Greater(mean, full * .75f, house.Name + " still drops most of its letters");
                Assert.Less(mean, full, house.Name + " drops fewer than every letter");
                int wanted = tuning.LettersInPlay(house, 26);
                Assert.Greater(wanted, 0);
                TestContext.WriteLine($"{house.Name}: {house.Furniture.Count} pieces, {full} letters in full, about {mean:0} dropped, deliveries keep {wanted} in play");
            }
        }
    }
}
