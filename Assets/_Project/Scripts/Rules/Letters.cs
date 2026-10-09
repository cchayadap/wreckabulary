using System;
using System.Collections.Generic;
using System.Text;

namespace Wreckabulary.Rules
{
    public sealed class LetterBag : IEquatable<LetterBag>
    {
        public const int Alphabet = 26;
        readonly int[] counts = new int[Alphabet];

        public LetterBag() { }

        public LetterBag(LetterBag other)
        {
            Array.Copy(other.counts, counts, Alphabet);
        }

        public static LetterBag FromWord(string word)
        {
            var bag = new LetterBag();
            if (word == null) return bag;
            foreach (char c in word)
            {
                if (!IsLetter(c)) throw new ArgumentException($"'{word}' contains '{c}', which is not A-Z.");
                bag.counts[Index(c)]++;
            }
            return bag;
        }

        public static bool IsLetter(char c) => c >= 'A' && c <= 'Z';

        public static bool IsWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (char c in word) if (!IsLetter(c)) return false;
            return true;
        }

        static int Index(char c)
        {
            if (!IsLetter(c)) throw new ArgumentOutOfRangeException(nameof(c), $"'{c}' is not A-Z.");
            return c - 'A';
        }

        public int this[char letter] => counts[Index(letter)];

        public int Count
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Alphabet; i++) total += counts[i];
                return total;
            }
        }

        public bool IsEmpty => Count == 0;

        public void Add(char letter, int amount = 1)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            counts[Index(letter)] += amount;
        }

        public void Add(LetterBag other)
        {
            for (int i = 0; i < Alphabet; i++) counts[i] += other.counts[i];
        }

        public bool Contains(LetterBag needed)
        {
            for (int i = 0; i < Alphabet; i++)
                if (counts[i] < needed.counts[i]) return false;
            return true;
        }

        public bool TryRemove(char letter)
        {
            int i = Index(letter);
            if (counts[i] == 0) return false;
            counts[i]--;
            return true;
        }

        public bool TryRemove(LetterBag needed)
        {
            if (!Contains(needed)) return false;
            for (int i = 0; i < Alphabet; i++) counts[i] -= needed.counts[i];
            return true;
        }

        public LetterBag Missing(LetterBag needed)
        {
            var missing = new LetterBag();
            for (int i = 0; i < Alphabet; i++)
                missing.counts[i] = Math.Max(0, needed.counts[i] - counts[i]);
            return missing;
        }

        public LetterBag TakeAll()
        {
            var all = new LetterBag(this);
            Array.Clear(counts, 0, Alphabet);
            return all;
        }

        public IEnumerable<char> Letters()
        {
            for (int i = 0; i < Alphabet; i++)
                for (int n = 0; n < counts[i]; n++)
                    yield return (char)('A' + i);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (char c in Letters()) sb.Append(c);
            return sb.ToString();
        }

        public bool Equals(LetterBag other)
        {
            if (other is null) return false;
            for (int i = 0; i < Alphabet; i++)
                if (counts[i] != other.counts[i]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is LetterBag other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < Alphabet; i++) hash = hash * 31 + counts[i];
            return hash;
        }
    }
}
