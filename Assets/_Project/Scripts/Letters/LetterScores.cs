namespace Wreckabulary
{
    public enum LetterRarity { Common, Rare, Legendary }

    public static class LetterScores
    {
        public static int ValueOf(char c) => Rules.RecipeDifficulty.LetterValue(c);

        public static LetterRarity RarityOf(char c)
        {
            int s = ValueOf(c);
            if (s >= 8) return LetterRarity.Legendary;
            if (s >= 4) return LetterRarity.Rare;
            return LetterRarity.Common;
        }

        public static int ScoreOf(string word)
        {
            int total = 0;
            foreach (char c in word) total += ValueOf(c);
            return total;
        }
    }
}
