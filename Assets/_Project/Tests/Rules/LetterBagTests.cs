using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class LetterBagTests
    {
        [Test]
        public void FromWordCountsRepeatedLetters()
        {
            var bag = LetterBag.FromWord("BALL");
            Assert.AreEqual(1, bag['B']);
            Assert.AreEqual(1, bag['A']);
            Assert.AreEqual(2, bag['L']);
            Assert.AreEqual(4, bag.Count);
            Assert.AreEqual("ABLL", bag.ToString());
        }

        [Test]
        public void OneLCantMakeBall()
        {
            var have = LetterBag.FromWord("BAL");
            var need = LetterBag.FromWord("BALL");
            Assert.IsFalse(have.Contains(need));
            Assert.AreEqual("L", have.Missing(need).ToString());
            have.Add('L');
            Assert.IsTrue(have.Contains(need));
            Assert.IsTrue(have.Missing(need).IsEmpty);
        }

        [TestCase("BOMB", 'B', 2)]
        [TestCase("SOAP", 'O', 1)]
        [TestCase("TABLE", 'E', 1)]
        [TestCase("UMBRELLA", 'L', 2)]
        public void RepeatsAreCounted(string word, char letter, int expected)
        {
            Assert.AreEqual(expected, LetterBag.FromWord(word)[letter]);
        }

        [Test]
        public void RemovingABagIsAllOrNothing()
        {
            var bag = LetterBag.FromWord("BAL");
            Assert.IsFalse(bag.TryRemove(LetterBag.FromWord("BALL")));
            Assert.AreEqual("ABL", bag.ToString(), "a failed removal changes nothing");
            Assert.IsTrue(bag.TryRemove(LetterBag.FromWord("AB")));
            Assert.AreEqual("L", bag.ToString());
            Assert.IsFalse(bag.TryRemove('Z'));
            Assert.AreEqual(1, bag.Count);
        }

        [Test]
        public void TakeAllEmptiesAndReturnsACopy()
        {
            var bag = LetterBag.FromWord("SOFA");
            var taken = bag.TakeAll();
            Assert.IsTrue(bag.IsEmpty);
            Assert.AreEqual("AFOS", taken.ToString());
            taken.Add('Z');
            Assert.IsTrue(bag.IsEmpty, "the returned bag is not shared");
        }

        [Test]
        public void EqualityIsByContents()
        {
            Assert.AreEqual(LetterBag.FromWord("MAT"), LetterBag.FromWord("TAM"));
            Assert.AreEqual(LetterBag.FromWord("MAT").GetHashCode(), LetterBag.FromWord("ATM").GetHashCode());
            Assert.AreNotEqual(LetterBag.FromWord("MAT"), LetterBag.FromWord("MATT"));
        }

        [TestCase("BAT", true)]
        [TestCase("bat", false)]
        [TestCase("B4T", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void OnlyCapitalAToZIsAWord(string word, bool expected)
        {
            Assert.AreEqual(expected, LetterBag.IsWord(word));
        }

        [Test]
        public void NonLettersAreRejected()
        {
            Assert.Throws<ArgumentException>(() => LetterBag.FromWord("BA T"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LetterBag().Add('?'));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LetterBag().Add('A', -1));
        }
    }
}
