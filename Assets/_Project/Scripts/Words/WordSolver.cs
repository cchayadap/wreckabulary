using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary
{
    public static class WordSolver
    {
        public static int[] Count(IEnumerable<char> letters)
        {
            var counts = new int[26];
            foreach (char c in letters)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i >= 0 && i < 26) counts[i]++;
            }
            return counts;
        }

        public static bool CanSpell(int[] have, string word)
        {
            var need = new int[26];
            foreach (char c in word)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i < 0 || i >= 26 || ++need[i] > have[i]) return false;
            }
            return true;
        }

        public static string Missing(int[] have, string word)
        {
            var left = (int[])have.Clone();
            var missing = new System.Text.StringBuilder();
            foreach (char c in word)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i >= 0 && i < 26 && left[i] > 0) left[i]--;
                else missing.Append(c);
            }
            return missing.ToString();
        }

        public static List<WordEntry> Spellable(IEnumerable<WordEntry> words, IEnumerable<char> letters)
        {
            var have = Count(letters);
            return words.Where(w => CanSpell(have, w.word))
                        .OrderByDescending(w => w.Score)
                        .ToList();
        }

        public static List<WordEntry> Spellable(WordDatabase db, IEnumerable<char> letters) => Spellable(db.Words, letters);

        public static List<(WordEntry entry, string missing)> Hints(IEnumerable<WordEntry> words, IEnumerable<char> letters,
                                                                    int capacity, int maxMissing = 1)
        {
            var have = Count(letters);
            return words.Where(w => !w.hidden && w.word.Length <= capacity && !CanSpell(have, w.word))
                        .Select(w => (entry: w, missing: Missing(have, w.word)))
                        .Where(h => h.missing.Length <= maxMissing)
                        .OrderByDescending(h => h.entry.Score)
                        .ToList();
        }
    }
}
