using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class BossFightCachesTests
    {
        [TestCase("CALC")]
        [TestCase("ULA")]
        [TestCase("TOR")]
        public void CarriersNaturallyContainTheWholeShieldWord(string shield)
        {
            var props = BossFightCaches.CarriersFor(shield);
            Assert.IsNotEmpty(props, shield + " needs natural prop carriers");
            string letters = string.Concat(props);
            foreach (var group in shield.GroupBy(c => c))
                Assert.GreaterOrEqual(letters.Count(c => c == group.Key), group.Count(), "props must naturally contain every " + group.Key + " needed by " + shield);
            Assert.LessOrEqual(letters.Length, 12, "carriers should fit the bag without excessive junk");
        }

        [Test]
        public void EveryShieldWordGetsTwoDecoysThatNeverRepeatACarrier()
        {
            for (int layer = 0; layer < BossFightTuning.Words.Count; layer++)
            {
                string word = BossFightTuning.WordFor(layer);
                var carriers = BossFightCaches.CarriersFor(word);
                var props = BossFightCaches.For(word, layer);
                CollectionAssert.AllItemsAreUnique(props, word);
                CollectionAssert.AreEqual(carriers, props.Take(carriers.Count).ToArray(), word);
                Assert.AreEqual(carriers.Count + BossFightCaches.Decoys, props.Count, word);
                foreach (string decoy in props.Skip(carriers.Count)) CollectionAssert.Contains(BossFightCaches.DecoyPool.ToArray(), decoy);
            }
        }

        [Test]
        public void TheFiveStarterPropsAndEveryCacheAreRealItems()
        {
            var items = TestData.Catalogue();
            Assert.AreEqual(5, BossFightCaches.StartersFor(5).Count);
            Assert.AreEqual(1, BossFightCaches.StartersFor(0).Count, "at least one prop");
            Assert.AreEqual(BossFightCaches.Starters.Count, BossFightCaches.StartersFor(99).Count);
            foreach (string word in BossFightCaches.Starters.Concat(BossFightCaches.Carriers).Concat(BossFightCaches.DecoyPool))
            {
                Assert.IsTrue(items.TryGet(word, out _), word + " is an item");
                Assert.IsTrue(word.All(c => c >= 'A' && c <= 'Z'), word);
            }
        }

        [Test]
        public void ShieldWordsAreNotItemsSoTheyNeverSpawnGear()
        {
            var items = TestData.Catalogue();
            foreach (string word in BossFightTuning.Words) Assert.IsFalse(items.TryGet(word, out _), word);
        }
    }
}
