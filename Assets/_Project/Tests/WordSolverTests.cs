using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Tests
{
    public class WordSolverTests
    {
        const string Csv = "word,category,hidden,notes\n" +
                           "AXE,Weapon,false,\n" +
                           "BLADE,Weapon,false,\n" +
                           "SWORD,Weapon,false,\n" +
                           "UMBRELLA,Defence,true,\n" +
                           "ZAP,Chaos,true,\n" +
                           "BEES,Chaos,false,swarm\n";

        static WordDatabase Db() => WordDatabase.FromCsv(Csv);

        [Test]
        public void ParsesCsvRows()
        {
            var db = Db();
            Assert.AreEqual(6, db.Words.Count);
            Assert.AreEqual(WordCategory.Chaos, db.Find("bees").category);
            Assert.IsTrue(db.Find("ZAP").hidden);
            Assert.AreEqual("swarm", db.Find("BEES").notes);
        }

        [Test]
        public void CanSpellNeedsEveryLetterIncludingRepeats()
        {
            Assert.IsTrue(WordSolver.CanSpell(WordSolver.Count("SEEB"), "BEES"));
            Assert.IsFalse(WordSolver.CanSpell(WordSolver.Count("SEB"), "BEES"));
        }

        [Test]
        public void MissingListsWhatIsLeft()
        {
            Assert.AreEqual("O", WordSolver.Missing(WordSolver.Count("SWRD"), "SWORD"));
        }

        [Test]
        public void SpellableIsSortedByScore()
        {
            var words = WordSolver.Spellable(Db(), "ABLDEXZP");
            CollectionAssert.AreEqual(new[] { "ZAP", "AXE", "BLADE" }, words.Select(w => w.word).ToArray());
        }

        [Test]
        public void HintsSkipHiddenAndTooLongWords()
        {
            var hints = WordSolver.Hints(Db().Words, "SWRDZA", capacity: 6);
            var words = hints.Select(h => h.entry.word).ToArray();
            CollectionAssert.Contains(words, "SWORD");
            CollectionAssert.DoesNotContain(words, "ZAP"); // hidden, one letter short
            Assert.AreEqual("O", hints.First(h => h.entry.word == "SWORD").missing);

            var umbrella = WordSolver.Hints(Db().Words, "UMBRELL", capacity: 6);
            CollectionAssert.IsEmpty(umbrella.Where(h => h.entry.word == "UMBRELLA"));
        }

        [Test]
        public void ShippedWordListParses()
        {
            var db = UnityEngine.Resources.Load<GameAssets>("GameAssets").words;
            Assert.AreEqual(GameConfig.Current.Items.Enabled.Count(), db.Words.Count);
            Assert.IsNotNull(db.Find("BLADE"));
        }
    }
}
