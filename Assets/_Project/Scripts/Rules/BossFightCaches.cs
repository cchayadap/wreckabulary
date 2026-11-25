using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class BossFightCaches
    {
        public const int Decoys = 2;
        const int CoverWeight = 100;

        public static readonly IReadOnlyList<string> Starters = new[] { "BAT", "LAMP", "BALL", "ZAP", "TABLE" };

        public static readonly IReadOnlyList<string> Carriers = new[]
        {
            "CLOCK", "CAKE", "BAT", "BALL", "TABLE", "LAMP", "BED", "SOFA",
            "BOOK", "APPLE", "MUG", "SPEAR", "SOAP", "SODA", "PIE", "BOMB", "BROOM", "PLATE",
        };

        public static readonly IReadOnlyList<string> DecoyPool = new[] { "BOOK", "BED", "SOFA", "BAT" };

        public static IReadOnlyList<string> StartersFor(int count) =>
            Take(Starters, Math.Max(1, Math.Min(count, Starters.Count)));

        static string[] Take(IReadOnlyList<string> from, int count)
        {
            var taken = new string[count];
            for (int i = 0; i < count; i++) taken[i] = from[i];
            return taken;
        }

        public static List<string> CarriersFor(string word)
        {
            var need = Count(word);
            var chosen = new List<string>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            while (need.Count > 0)
            {
                string best = null;
                int bestScore = int.MinValue;
                foreach (string prop in Carriers)
                {
                    if (used.Contains(prop)) continue;
                    int covered = Covered(prop, need);
                    if (covered == 0) continue;
                    int score = covered * CoverWeight - prop.Length;
                    if (score > bestScore) { bestScore = score; best = prop; }
                }
                if (best == null) break;
                chosen.Add(best);
                used.Add(best);
                Spend(best, need);
            }
            return chosen;
        }

        public static List<string> For(string word, int layer)
        {
            var props = CarriersFor(word);
            var used = new HashSet<string>(props, StringComparer.Ordinal);
            for (int d = 0; d < Decoys; d++)
                for (int t = 0; t < DecoyPool.Count; t++)
                {
                    string candidate = DecoyPool[(Math.Max(0, layer) * Decoys + d + t) % DecoyPool.Count];
                    if (!used.Add(candidate)) continue;
                    props.Add(candidate);
                    break;
                }
            return props;
        }

        static Dictionary<char, int> Count(string word)
        {
            var need = new Dictionary<char, int>();
            foreach (char raw in word ?? "")
            {
                char c = char.ToUpperInvariant(raw);
                if (c < 'A' || c > 'Z') continue;
                need.TryGetValue(c, out int count);
                need[c] = count + 1;
            }
            return need;
        }

        static int Covered(string prop, Dictionary<char, int> need)
        {
            var left = new Dictionary<char, int>(need);
            int covered = 0;
            foreach (char c in prop)
            {
                if (!left.TryGetValue(c, out int count) || count <= 0) continue;
                covered++;
                left[c] = count - 1;
            }
            return covered;
        }

        static void Spend(string prop, Dictionary<char, int> need)
        {
            foreach (char c in prop)
            {
                if (!need.TryGetValue(c, out int count)) continue;
                if (count == 1) need.Remove(c); else need[c] = count - 1;
            }
        }
    }
}
